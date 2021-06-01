using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using Newtonsoft.Json.Linq;
using com.epam.indigo;

namespace FDA.SRS.Utils
{
	public enum SrsDomain { Any, Substance, Mixture, Protein, StructurallyDiverse, Polymer, NucleicAcid };
	public static class SrsSdfUtils
	{

        /// <summary>
        /// Read JSON from the supplied property
        /// </summary>
        /// <param name="sdf">SDF record to work with</param>
        /// <param name="field_name">SDF Property to attempt to parse as JSON</param>
        /// <returns></returns>
        public static JObject GetJsonFromProperty(this SdfRecord sdf, string field_name) {
            String json1 = sdf.GetFieldValue(field_name);
            try {
                return JObject.Parse(json1);
            } catch (Exception e) {
                throw new Exception("Cannot read JSON from property \"" + field_name +  "\"");
            }
        }

        /// <summary>
        /// Read GSRS JSON property, defaulting to "GSRS_JSON" in the SDF.
        /// </summary>
        /// <param name="sdf">SDF record to work with</param>
        /// <returns></returns>
        public static JObject GetGSRSJson(this SdfRecord sdf) {
            return sdf.GetJsonFromProperty("GSRS_JSON");
        }

        /// <summary>
        /// Read, optionally try to fix and validate SRS XML description field
        /// </summary>
        /// <param name="sdf">SDF record to work with</param>
        /// <param name="prefix">SDF filed name - usually DESC_PART</param>
        /// <param name="noAutoFix">Do not attempt to fix invalid XML</param>
        /// <returns></returns>
        public static XDocument GetDescXml(this SdfRecord sdf, string prefix, SrsDomain domain, ImportOptions impOpt)
		{
			if ( impOpt.Features.Has("debug-ignore-description") )
				return null;

			XDocument desc_xml = null;

			string desc = sdf.GetConcatXmlFields(prefix);
			if ( !String.IsNullOrEmpty(desc) ) {
				if ( !impOpt.NoAutoFix ) {
					desc = desc.Trim();
					// ==> some dirty tweaking and filtering
					if ( desc.StartsWith("<") && !desc.EndsWith(">") )
						desc = desc + ">";
					if ( !desc.StartsWith("<") && desc.EndsWith(">") )
						desc = "<" + desc;

					desc = Regex.Replace(desc, @"<(\S+)>(.*?)</\1/>", "<$1>$2</$1>");   // <C_TERMINAL_MODIFICATION_ID>1EX3D44NQD</C_TERMINAL_MODIFICATION_ID/>
					desc = Regex.Replace(desc, @"</?.*?\r?\n.*?>", m => m.Value.Replace("\r", "").Replace("\n", ""));   // Element tags broken by new-line
					// desc = Regex.Replace(desc, @"(?<!<$2>)(.*?)</(\S+)>", "$1", RegexOptions.Singleline);	// closing tags without matching opening ones
					// desc = Regex.Replace(desc, @"(?<!<$2>)(.*?)</(\S+)>", "$1", RegexOptions.Singleline);	// opening tags without closing ones
					// <== some dirty tweaking and filtering
				}

			try_again:
				try {
					desc_xml = XDocument.Parse(desc);
				}
				catch ( XmlException ex ) {
					if ( !impOpt.NoAutoFix ) {
						if ( ex.Message.Contains("There are multiple root elements.") ) {
							desc = String.Format("<COMPLEX>{0}</COMPLEX>", desc);
							goto try_again;
						}
					}

					throw new SrsException("invalid_srs_xml", ex.Message, ex);
				}

                if (domain != SrsDomain.Any) {
                    string top_tag = desc_xml.Root.Name.ToString();
                    if (!SrsSdfValidators.RootDescriptionTags[domain.ToString()].Any(re => re.IsMatch(top_tag))) {
                        throw new SrsException("fields-validation", String.Format("Invalid top level tag {0} in description: {1}", top_tag, desc));
                    }
                    if (SrsSdfValidators.BadDescriptionTags.ContainsKey(domain.ToString())) {
                        SrsSdfValidators.BadDescriptionTags[domain.ToString()].Any(path => {
                            if (desc_xml.XPathSelectElement(path) != null) {
                                throw new SrsException("fields-validation", String.Format("Invalid tag {0} in description: {1}", path, desc));
                            }
                            return false;
                        });
                    }
                    
				}
			}

			return desc_xml;
		}

		public static string GetConcatXmlFields(this SdfRecord sdf, string prefix)
		{
			int i = 1;
			string desc;
			StringBuilder sb = new StringBuilder();
			while ( true ) {
				// Could be missing numeration
				if ( String.IsNullOrEmpty(desc = sdf.GetFieldValue(String.Format("{0}{1}", prefix, i))) )
					i++;

				// But we don't skip over two missing parts
				if ( String.IsNullOrEmpty(desc) && String.IsNullOrEmpty(desc = sdf.GetFieldValue(String.Format("{0}{1}", prefix, i))) )
					break;

				sb.Append(desc);
				i++;
			}

			return Regex.Replace(sb.ToString(), @"&(?!\w{,5};)", "&amp;");
		}

		public static void TraverseSrsSdfs(ImportOptions impOpt, Action<string, string, SdfRecord, XDocument> onRecord, Action<string, string, SdfRecord, Exception> onException = null)
		{
			Action<string> a = file => {
				using ( SdfReader r = new SdfReader(file, impOpt.InputFileEncoding) { FieldsMap = impOpt.SdfMapping } ) {
					foreach ( SdfRecord sdf in r.Records ) {
						string unii = sdf.GetFieldValue("UNII");
						try {
							XDocument xdoc = GetDescXml(sdf, "DESC_PART", SrsDomain.Any, impOpt);
							onRecord(file, unii, sdf, xdoc);
						}
						catch ( Exception ex ) {
							TraceUtils.WriteUNIITrace(TraceEventType.Error, unii, null, ex.ToString());
							if ( onException != null )
								onException(file, unii, sdf, ex);
						}
					}
				}
			};

			if ( !impOpt.InputFile.Contains("*") )
				a(impOpt.InputFile);
			else {
				var dir = Path.GetDirectoryName(impOpt.InputFile);
				if ( String.IsNullOrEmpty(dir) )
					dir = ".";
				Directory.GetFiles(dir, Path.GetFileName(impOpt.InputFile), SearchOption.TopDirectoryOnly)
					.ToList()
					.ForEach(f => a(f));
			}
		}

		public static string ToString(this IEnumerable<SdfRecord> sdfRecs)
		{
			StringBuilder sb = new StringBuilder();
			foreach ( var sdf in sdfRecs )
			sb.AppendLine(sdf.ToString());
			return sb.ToString();
		}

        public static SDFUtil.NewMolecule reorderBasedOn(this SDFUtil.NewMolecule nmol, int[] order) {
            String originalMol = nmol.Mol;
            String[] lines = originalMol.TrimEnd().Split('\n'); // raw lines
            int atomCount = int.Parse(lines[3].Substring(0, 3).Trim());
            int bondCount = int.Parse(lines[3].Substring(3, 3).Trim());
            String[] atomLines = lines.Skip(3 + 1).Take(atomCount).ToArray();
            String[] bondLines = lines.Skip(3 + 1 + atomCount).Take(bondCount).ToArray();
            String[] everythingElseLines = lines.Skip(3 + 1 + atomCount + bondCount).ToArray();

            //reorder atoms
            for (int i = 0; i < atomCount; i++) {
                int msetLine = 4 + order[i];
                lines[msetLine] = atomLines[i];
            }
            //fix bonds
            for (int i = 0; i < bondCount; i++) {
                int msetLine = 4 + atomCount + i;
                String bondLine = bondLines[i];
                int atom1 = int.Parse(bondLine.Substring(0, 3).Trim());
                int atom2 = int.Parse(bondLine.Substring(3, 3).Trim());

                int natom1 = order[atom1 - 1] + 1;
                int natom2 = order[atom2 - 1] + 1;
                String newLine = (natom1 + "").PadLeft(3) + (natom2 + "").PadLeft(3) + bondLine.Substring(6);
                lines[msetLine] = newLine;
            }
            //SAFE TO IGNORE:
            //M  STY [refers to SGROUP meta]
            //M  SMT [refers to SGROUP meta]
            //M  SDI [refers to SGROUP meta]
            //M  SBL [refers to SGROUP meta on bonds, which are not reordered]

            //Charge-like format:
            //M  ISO
            //M  CHG
            //M  RGP

            //Atom list-like format:
            //M  SAL   1  2   2   3
            //..........nnnaaaabbbb

            //Alias format:
            //A    1
            //A..nnn
            bool lastAlias = false;
            bool lastValue = false;
            for (int i = 0; i < everythingElseLines.Length; i++) {
                if (lastAlias) {
                    lastAlias = false;
                    continue;
                }
                if (lastValue)
                {
                    lastValue = false;
                    continue;
                }
                int msetLine = 4 + atomCount + bondCount + i;
                String extraLine = everythingElseLines[i];
                String newLine = extraLine;
                if (extraLine.StartsWith("A ")) {
                    lastAlias = true;
                    int oldIndex = int.Parse(extraLine.Substring(2, 4).Trim());
                    int newIndex = order[oldIndex - 1] + 1;
                    newLine = extraLine.Substring(0, 2) + ("" + newIndex).PadLeft(4) + extraLine.Substring(6);
                } else if (extraLine.StartsWith("M  ISO") || extraLine.StartsWith("M  CHG") || extraLine.StartsWith("M  RGP")) {
                    int repCount = int.Parse(extraLine.Substring(6, 3).Trim());
                    for (int j = 0; j < repCount; j++) {
                        int oldIndex = int.Parse(extraLine.Substring(9 + j * 8, 4).Trim());
                        int newIndex = order[oldIndex - 1] + 1;
                        newLine = newLine.Substring(0, 9 + j * 8) + (newIndex + "").PadLeft(4) + newLine.Substring(9 + j * 8 + 4);
                    }
                } else if (extraLine.StartsWith("M  SAL")) {
                    int repCount = int.Parse(extraLine.Substring(10, 3).Trim());
                    for (int j = 0; j < repCount; j++) {
                        int oldIndex = int.Parse(extraLine.Substring(13 + j * 4, 4).Trim());
                        int newIndex = order[oldIndex - 1] + 1;
                        newLine = newLine.Substring(0, 13 + j * 4) + (newIndex + "").PadLeft(4) + newLine.Substring(13 + j * 4 + 4);
                    }
                } else if (extraLine.StartsWith("V ")) {
                    lastValue = true;
                    int oldIndex = int.Parse(extraLine.Substring(2, 4).Trim());
                    int newIndex = order[oldIndex - 1] + 1;
                    newLine = extraLine.Substring(0, 2) + ("" + newIndex).PadLeft(4) + extraLine.Substring(6);
                }
                else if (extraLine.StartsWith("M  STY")
                        || extraLine.StartsWith("M  SMT")
                        || extraLine.StartsWith("M  SDI")
                        || extraLine.StartsWith("M  SBL")) {
                    //these are safe to ignore
                    continue;
                } else if (extraLine.StartsWith("M  END")) {
                    //don't bother doing anything after the end
                    break;
                } else {
                    throw new Exception("Unknown molfile signifier format:\"" + extraLine + "\"");
                }

                lines[msetLine] = newLine;

            }

            return new SDFUtil.NewMolecule(lines.JoinToString("\n"));
        }

    }
}
