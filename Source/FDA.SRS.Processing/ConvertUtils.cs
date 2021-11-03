using FDA.SRS.Utils;
using System;
using System.Net;
using System.Configuration;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using System.Xml.XPath;
using Newtonsoft.Json.Linq;
using com.epam.indigo;

namespace FDA.SRS.Processing
{
	public static partial class Converter
	{
		/// <summary>
		/// Traverse SDF, extract XML snippet specified by XPath and write snippets into file
		/// </summary>
		/// <param name="options"></param>
		public static void SdfExtractXmlSnippets(ImportOptions impOpt, ConvertOptions opt, ExportOptions expOpt)
		{
			TraceUtils.WriteUNIITrace(TraceEventType.Information, null, null,
				"Processing file {0} with output into {1}...", impOpt.InputFile, expOpt.OutputFile);

			HashSet<string> unique = new HashSet<string>();

			using ( StreamWriter sr = new StreamWriter(expOpt.OutputFile) ) {
				SrsSdfUtils.TraverseSrsSdfs(impOpt, (file, unii, sdf, xdoc) => {
					xdoc.XPathSelectElements(impOpt.XPath).ForAll(x => {
						if ( !String.IsNullOrWhiteSpace(x.Value) ) {
							while ( true ) {
								if ( opt.Unique ) {
									string hash = x.Value.GetMD5String();
									if ( unique.Contains(hash) )
										break;
									unique.Add(hash);
								}
								x.Add(new XAttribute("UNII", unii));
								sr.WriteLine(x.ToString());
								break;
							}
						}
					});
				});
			}

			TraceUtils.WriteUNIITrace(TraceEventType.Information, null, null, "Processed file {0}", impOpt.InputFile);
		}

		/// <summary>
		/// Traverse SDF, extract SDF records having XPath pattern in SRS XML description
		/// </summary>
		/// <param name="options"></param>
		public static void SdfExtractSdf(ImportOptions impOpt, ConvertOptions opt, ExportOptions expOpt)
		{
			OperationalParameters pars = impOpt.PrepareConversion(expOpt);

			TraceUtils.WriteUNIITrace(TraceEventType.Information, null, null,
				"Processing file {0} with output into {1}...", impOpt.InputFile, expOpt.OutDir);

			SrsSdfUtils.TraverseSrsSdfs(impOpt, (file, unii, sdf, xdoc) => {
				if ( Filter(sdf, opt.Filters) &&
                        //GALOG
                        //(sdf.GetType().Equals(SGroupType.DAT)) &&
                        sdf.HasSGroup(opt.SGroupTypes) &&
                        (String.IsNullOrEmpty(impOpt.XPath) || xdoc.XPathSelectElements(impOpt.XPath).Any()) &&
					(!impOpt.Uniis.Any() || impOpt.Uniis.Contains(unii)) )
				{
					// Figure directory
					string outDir = expOpt.OutDir;
					if ( expOpt.Separate )
						outDir = Path.Combine(outDir, Path.GetFileNameWithoutExtension(file));
					if ( !Directory.Exists(outDir) )
						Directory.CreateDirectory(outDir);

					// SDF
					File.WriteAllText(Path.Combine(outDir, unii + ".sdf"), sdf.ToString());

					// MOL
					if ( expOpt.MolFile && !String.IsNullOrWhiteSpace(sdf.Mol) )
						File.WriteAllText(Path.Combine(outDir, unii + ".mol"), sdf.Mol);

					// PNG
					if ( expOpt.GenerateImage && !String.IsNullOrWhiteSpace(sdf.Mol) )
						sdf.Mol.RenderToFile(Path.Combine(outDir, unii + ".png"), expOpt.ImageOptions);

					// SRS XML
					if ( xdoc != null )
						File.WriteAllText(Path.Combine(outDir, unii + ".srs.xml"), xdoc.ToString());
				}
			}, (file, unii, sdf, ex) => {
				// Figure directory
				string outDir = expOpt.OutDir;
				if ( expOpt.Separate )
					outDir = Path.Combine(outDir, Path.GetFileNameWithoutExtension(file));
				if ( !Directory.Exists(outDir) )
					Directory.CreateDirectory(outDir);

				string errorsDir = Path.Combine(outDir, "errors");
				if ( !Directory.Exists(errorsDir) )
					Directory.CreateDirectory(errorsDir);

				string descr = sdf.GetConcatXmlFields("DESC_PART");
				XDocument xdoc = new XDocument(new XElement("root",
					new XElement("exception", new XCData(ex.ToString())),
					new XElement("srs-xml", new XCData(descr))
					));

				File.WriteAllText(Path.Combine(errorsDir, unii + ".sdf"), sdf.ToString());
				File.WriteAllText(Path.Combine(errorsDir, unii + ".srs.xml"), descr);
				File.WriteAllText(Path.Combine(errorsDir, unii + ".err.xml"), xdoc.ToString());
			});

			TraceUtils.WriteUNIITrace(TraceEventType.Information, null, null, "Processed file {0}", impOpt.InputFile);
		}

		public static void SdfProcessSdf(ImportOptions impOpt, ConvertOptions opt, ExportOptions expOpt)
        {
            OperationalParameters pars = impOpt.PrepareConversion(expOpt);

            TraceUtils.WriteUNIITrace(TraceEventType.Information, null, null,
                "Processing file {0} with output into {1}...", impOpt.InputFile, expOpt.OutDir);

            using (StreamWriter sr = new StreamWriter(Path.Combine(expOpt.OutDir, Path.GetFileName(impOpt.InputFile))))
            using (SdfReader r = new SdfReader(impOpt.InputFile, impOpt.InputFileEncoding) { FieldsMap = impOpt.SdfMapping })
            {
                foreach (SdfRecord sdf in r.Records)
                {
                    
					string unii = sdf.GetFieldValue("UNII");
                    //YP Issue 6, allow sdf without UNII
					//if (String.IsNullOrEmpty(unii) && opt.GenerateMode != GenerateMode.NewSubstance)
                    //    throw new SrsException("mandatory_field_missing", "UNII is missing and GenerateMode != NewSubstance");

                    if (expOpt.Canonicalize)
                    {
						SDFUtil.NewMolecule nm = new SDFUtil.NewMolecule(sdf.Mol);
						int[] atom_mapping = nm.CanonicalNumbers();
						var new_conns = new List<string>();

						if (sdf["CONNECTORS"] != null)
						{
							IList<string> conn_pairs = (sdf["CONNECTORS"] ?? new List<string>())
							.SelectMany(s => s.Split(new char[] { '\n' }, StringSplitOptions.RemoveEmptyEntries))
							.ToList();

							
							
							for (int j = 0; j < conn_pairs.Count; j++)
							{
								IList<int> conns = conn_pairs[j].Split(';').Select(s => int.Parse(s.Trim())).ToList();
								var new_conns_pair = new List<int>();
								for (int i = 0; i < conns.Count; i++)
								{
									if (conns[i] == 0)
									{
										new_conns_pair.Add(0);
									}
									else
									{
										new_conns_pair.Add(atom_mapping[conns[i] - 1] + 1);
									}

								}
								new_conns.Add(string.Join(";", new_conns_pair));
							}
						}
						SDFUtil.NewMolecule canonicalized_molecule = nm.ReorderCanonically();
                        sdf.Molecule = canonicalized_molecule;
						if (sdf["CONNECTORS"] != null) { sdf.Properties["CONNECTORS"][0] = string.Join(Environment.NewLine, new_conns); }
                        
                    }
                    sr.Write(sdf);
                    
                }
            }
            TraceUtils.WriteUNIITrace(TraceEventType.Information, null, null, "Processed file {0}", impOpt.InputFile);
        }

        public static void GenerateNAComponentsMolfile(ImportOptions impOpt, ConvertOptions opt, NAComponentsExportOptions expOpt)
        {
            //OperationalParameters pars = impOpt.PrepareConversion(expOpt);

            TraceUtils.WriteUNIITrace(TraceEventType.Information, null, null,
                "Generating Nucleic Acid components and dictionary files {0} and {1}...", expOpt.ComponentsOutputFile, expOpt.DictionaryOutputFile);

			string json_string = "";
			
			TextReader reader = null;
			string uri = ConfigurationManager.AppSettings["NucleicAcidComponentsURI"];
			if (!Regex.IsMatch(uri, @"^[a-z]{3,5}://"))
				uri = "file:///" + uri;
			Uri t;
			if (Uri.TryCreate(uri, UriKind.RelativeOrAbsolute, out t))
			{
				try
				{
					reader = SubstanceIndexing._openTextUri(t);
				}
				catch (Exception e)
				{

					reader = null;
				}
			}

			if (reader == null)
			{
				Trace.TraceWarning("Cannot access '{0}' - falling back to an internal version of nucleic_acids.json", uri);
				reader = new StringReader(File.ReadAllText(ConfigurationManager.AppSettings["NucleicAcidComponentsLocal"]));
			}
			json_string = reader.ReadToEnd();
			//YB begin
			try
			{
				JObject json1 = JObject.Parse(json_string);
			}
			catch (Exception e)
			{
				Trace.TraceWarning("JSON read error from '{0}' - falling back to an internal version of nucleic_acids.json", uri);
				reader = new StringReader(File.ReadAllText(ConfigurationManager.AppSettings["NucleicAcidComponentsLocal"]));
				json_string = reader.ReadToEnd();
			}
			//YB end
			JObject json = JObject.Parse(json_string);
			IndigoObject saver = getIndigo().writeFile(expOpt.ComponentsOutputFile);
            StreamWriter sw = File.CreateText(expOpt.DictionaryOutputFile);
            JToken content = json.SelectToken("$..content");
            List<JToken> component_groups = content.ToList();
			bool firstline = true;
            foreach (JToken component_group in component_groups)
            {
                string domain = component_group["domain"].ToString();
				
                foreach (JToken component in component_group.SelectToken("$..terms"))
                {
					
					
					string component_value = CleanChars(component["value"].ToString());
					
                    if (component_value == "N")
                    {
                        continue;
                    }

					if (!firstline)
					{
						sw.Write(Environment.NewLine);
					}
					firstline = false;

					string fragment_structure = component["fragmentStructure"].ToString();
                    string simplified_structure = component["simplifiedStructure"].ToString();
                    getIndigo().setOption("aromaticity-model", "generic");
                    string component_smarts = fragment_structure.Split(' ')[0];
                    //string component_smarts = fragment_structure.Split(' ')[0].Replace("*","He");
                    var component_atom_aliases = fragment_structure.Split(new[] { " " }, StringSplitOptions.RemoveEmptyEntries)[1].Replace("|","").Replace("$","").Split(';').ToList();
                    //IndigoObject mol = getIndigo().loadSmarts(component_smarts);
                    IndigoObject mol = getIndigo().loadMolecule(component_smarts);
                    mol.dearomatize();

                    int i = 0;
                    int added_connectors_count=0;
                    List<string> connectors_layout = new List<string>();
                    List<string> connectors = new List<string>();
                    string connectors_layout_str ="";
                    string connectors_str="";
                    foreach (IndigoObject atom in mol.iterateAtoms())
                    {
                        if (component_atom_aliases[i]!="")
                        {
                            
                            added_connectors_count++;
                            if (added_connectors_count % 2 == 0)
                            {
                                connectors_layout_str = connectors_layout_str + component_atom_aliases[i] + Environment.NewLine;
                                connectors_str = connectors_str + (atom.index() + 1).ToString() + Environment.NewLine;
                            }
                            else
                            {
                                connectors_layout_str = connectors_layout_str + component_atom_aliases[i] + ";";
                                connectors_str = connectors_str + (atom.index() + 1).ToString() + ";";
                            }
                            //connectors_layout.Add(component_atom_aliases[i]);
                            //connectors.Add((atom.index()+1).ToString());
                            
                        }
                        
                        i++;
                    }
                   
                    mol.layout();
					//YP SRS-394 Need to include both "G" and UNII as keys that map to unii for things that have origin in the CV json
					if (component["origin"] != null)
                    {
						string component_origin = component["origin"].ToString();
						mol.setProperty("UNII", component_origin);
						sw.Write(component_origin + "\t" + component_origin);
						sw.Write(Environment.NewLine);
						sw.Write(component_value + "\t" + component_origin);
						//sw.Write(component_value + "\t" + ("FKUN" + component_value).PadRight(8, 'X').Substring(0, 8).ToUpper());
						mol.setProperty("NAME", component_origin);
					}
					else
                    {
//YB This way does not work. It creates duplicative fake UNIIs
//						sw.Write(component_value + "\t" + ("FKUN" + component_value).PadRight(8, 'X').Substring(0, 8).ToUpper());
						sw.Write(component_value + "\t" + ("F" + component_value.GetMD5String()).PadRight(8, 'X').Substring(0, 8).ToUpper());
//						mol.setProperty("UNII", ("FKUN" + component_value).PadRight(8, 'X').Substring(0, 8).ToUpper());
	string tmp = ("F" + component_value.GetMD5String()).PadRight(8, 'X').Substring(0, 8).ToUpper();
						mol.setProperty("UNII", ("F" + component_value.GetMD5String()).PadRight(8, 'X').Substring(0, 8).ToUpper());
//YB end
						mol.setProperty("NAME", component_value);
					}

					//sw.Write(component_value + "\t" + ("FKUN" + component_value).PadRight(8, 'X').Substring(0, 8).ToUpper());
					//mol.setProperty("NAME", component_value);
					mol.setProperty("NA", component_value);
                    
                    //mol.setProperty("CONNECTORS_LAYOUT", String.Join(";",connectors_layout.ToArray()));
                    if (connectors_layout_str[connectors_layout_str.Length - 1].ToString()==Environment.NewLine)
                    {
                        connectors_layout_str = connectors_layout_str.ToString().TrimEnd('\r', '\n');
                        connectors_str = connectors_str.ToString().TrimEnd('\r', '\n');
                    }
                    else if (connectors_layout_str[connectors_layout_str.Length - 1].ToString() == ";")
                    {
                        connectors_layout_str = connectors_layout_str + "0";
                        connectors_str = connectors_str + "0";
                    }

                    mol.setProperty("CONNECTORS_LAYOUT", connectors_layout_str);
                    mol.setProperty("CONNECTORS", connectors_str);
                    //mol.setProperty("CONNECTORS", String.Join(";", connectors.ToArray()));

                    saver.sdfAppend(mol);

                }
            }
            sw.Close();    
            
            TraceUtils.WriteUNIITrace(TraceEventType.Information, null, null, "Generated components and dictionary files {0} and {1}...", expOpt.ComponentsOutputFile, expOpt.DictionaryOutputFile);
        }

		private static string CleanChars(string input)
        {
			return input.Replace("�", "'");
        }
        private static bool Filter(SdfRecord sdf, IEnumerable<ConvertOptions.Filter> filters)
		{
			if ( !filters.Any() )
				return true;

			foreach ( var f in filters ) {
				var v = sdf[f.Subject].FirstOrDefault();
				if ( f.Predicate == ConvertOptions.FltPred.Equals ) {
					if ( String.IsNullOrWhiteSpace(v) || v != f.Object )
						return false;
				}
				else if ( f.Predicate == ConvertOptions.FltPred.NotEquals ) {
					if ( !String.IsNullOrWhiteSpace(v) && v == f.Object )
						return false;
				}
				else if ( f.Predicate == ConvertOptions.FltPred.Like ) {
					if ( String.IsNullOrWhiteSpace(v) || !Regex.IsMatch(v, f.Object) )
						return false;
				}
				else if ( f.Predicate == ConvertOptions.FltPred.NotLike ) {
					if ( String.IsNullOrWhiteSpace(v) && Regex.IsMatch(v, f.Object) )
						return false;
				}
				else if ( f.Predicate == ConvertOptions.FltPred.Exists ) {
					if ( String.IsNullOrWhiteSpace(v) )
						return false;
				}
				else if ( f.Predicate == ConvertOptions.FltPred.NotExists ) {
					if ( !String.IsNullOrWhiteSpace(v) )
						return false;
				}
			}

			return true;
		}

		/// <summary>
		/// Traverse SDF, extract combined keys specified by XSort, sort, and output along with the whole XML into file
		/// </summary>
		/// <param name="options"></param>
		public static void SdfSortByXmlKey(ImportOptions impOpt, ExportOptions expOpt)
		{
			OperationalParameters pars = impOpt.PrepareConversion(expOpt);

			if ( String.IsNullOrEmpty(impOpt.XPath) )
				throw new ArgumentException("/xpath is not specified");

			TraceUtils.WriteUNIITrace(TraceEventType.Information, null, null,
				"Processing file {0} with output into directory {1}...", impOpt.InputFile, expOpt.OutDir);

			List<string> xsort = impOpt.XSort.Split(';', ',').ToList();
			var agg = new Dictionary<string, Dictionary<string, Tuple<SdfRecord, XElement>>>(); // XPathKey, (UNII, (SDF, XML))

			SrsSdfUtils.TraverseSrsSdfs(impOpt, (file, unii, sdf, xdoc) => {
				string key =
					String.Join(";", xdoc
						.XPathSelectElements(impOpt.XPath)
						.Where(x => !String.IsNullOrWhiteSpace(x.Value))
						.Select(_xpath =>
							String.Join(",", xsort
								.Select(_xsort =>
									String.Join("-",
										_xpath
										.XPathSelectElements(_xsort)
										.Where(_x => !String.IsNullOrWhiteSpace(_x.Value))
										.Select(_x => _x.Value.Trim())
									)
								)
							)
						)
					);

				if ( !agg.ContainsKey(key) )
					agg.Add(key, new Dictionary<string, Tuple<SdfRecord, XElement>> { { unii, new Tuple<SdfRecord, XElement>(sdf, xdoc.Root) } });
				else
					agg[key].Add(unii, new Tuple<SdfRecord, XElement>(sdf, xdoc.Root));
			});

			foreach ( var k in agg.Keys.OrderBy(k => k) ) {
				string file = Path.Combine(expOpt.OutDir, Path.GetFileNameWithoutExtension(expOpt.OutputFile) + "-" + k.Replace(" ", "_").Replace(",", "_").Replace("__", "_") + Path.GetExtension(expOpt.OutputFile));
				using ( StreamWriter sr = new StreamWriter(file) ) {
					if ( expOpt.OutputFile.EndsWith(".xml", StringComparison.CurrentCultureIgnoreCase) )
						sr.Write("<ROOT>");

					if ( expOpt.OutputFile.EndsWith(".csv", StringComparison.CurrentCultureIgnoreCase) ) {
						foreach ( var x in agg[k].Keys )
							sr.WriteLine(x);
					}
					else if ( expOpt.OutputFile.EndsWith(".xml", StringComparison.CurrentCultureIgnoreCase) ) {
						foreach ( var x in agg[k].Values )
							sr.Write(x.Item2);
					}
					else if ( expOpt.OutputFile.EndsWith(".sdf", StringComparison.CurrentCultureIgnoreCase) ) {
						foreach ( var x in agg[k].Values )
							sr.Write(x.Item1);
					}

					if ( expOpt.OutputFile.EndsWith(".xml", StringComparison.CurrentCultureIgnoreCase) )
						sr.Write("</ROOT>");
				}
			}

			TraceUtils.WriteUNIITrace(TraceEventType.Information, null, null, "Processed file {0}", impOpt.InputFile);
		}

		/// <summary>
		/// Traverse SDF and extract parts of it into directories based on root element name
		/// </summary>
		/// <param name="options"></param>
		public static void Sdf2Dirs(ImportOptions impOpt, ConvertOptions opt, ExportOptions expOpt)
		{
			TraceUtils.WriteUNIITrace(TraceEventType.Information, null, null,
				"Processing file {0} with output into directory {1}...", impOpt.InputFile, expOpt.OutDir);

			if ( !Directory.Exists(expOpt.OutDir) )
				Directory.CreateDirectory(expOpt.OutDir);

			using ( StreamWriter sr = new StreamWriter(Path.Combine(expOpt.OutDir, Path.GetFileName(impOpt.InputFile))) )
			using ( SdfReader r = new SdfReader(impOpt.InputFile, impOpt.InputFileEncoding) { FieldsMap = impOpt.SdfMapping } ) {
				foreach ( SdfRecord sdf in r.Records ) {
					string unii = sdf.GetFieldValue("UNII");
					if ( String.IsNullOrEmpty(unii) && opt.GenerateMode != GenerateMode.NewSubstance )
						throw new SrsException("mandatory_field_missing", "UNII is missing and GenerateMode != NewSubstance");

					string descr = SrsSdfUtils.GetConcatXmlFields(sdf, "DESC_PART");
					if ( String.IsNullOrEmpty(descr) )
						sr.Write(sdf);
					else {
						try {
							XDocument xdoc = sdf.GetDescXml("DESC_PART", SrsDomain.Any, impOpt);
							string odir = Path.Combine(expOpt.OutDir, xdoc.Root.Name.ToString());
							if ( !Directory.Exists(odir) )
								Directory.CreateDirectory(odir);
							File.WriteAllText(Path.Combine(odir, unii + ".srs.xml"), xdoc.ToString());
							if ( !String.IsNullOrEmpty(sdf.Mol) )
								File.WriteAllText(Path.Combine(odir, unii + ".sdf"), sdf.ToString());
						}
						catch ( Exception ex ) {
							File.WriteAllText(Path.Combine(expOpt.OutDir, unii + ".srs.xml"), descr);
							File.WriteAllText(Path.Combine(expOpt.OutDir, unii + ".err"), ex.ToString());
						}
					}
				}
			}

			TraceUtils.WriteUNIITrace(TraceEventType.Information, null, null, "Processed file {0}", impOpt.InputFile);
		}

        private static Indigo _indigo;

        public static Indigo getIndigo()
        {
            if (_indigo == null) _indigo = new Indigo();
            return _indigo;
        }

        public static Indigo getOgindni()
        {
            if (_indigo == null) _indigo = new Indigo();
            return _indigo;
        }
    }
}
