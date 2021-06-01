using FDA.SRS.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using System.Xml.XPath;

namespace FDA.SRS.ObjectModel
{
	public class Substance : SrsObject
	{
		public SdfRecord Sdf { get; set; }
		public string PrimaryName { get; set; }
		// public string OptAct { get; set; }
		public string SpecialStereo { get; set; }
		public IEnumerable<Moiety> Moieties { get; set; }

		public override string UID
		{
			get {
				return Moieties?.UID();
			}
		}

		public void Preprocess(XDocument desc_xml)
		{
			string names = DecodeNames(Sdf.GetFieldValue("SUBSTANCE_NAME"));
			PrimaryName = DecodePrimaryName(names);

			// Special Stereo and optical activity
			if ( desc_xml != null ) {
				
                //Get out special stereo from stereo comments, if possible
                XElement xelComplex = desc_xml.XPathSelectElement("/STEREOCHEMISTRY/COMMENTS");
                if (!String.IsNullOrWhiteSpace(xelComplex?.Value)) {
                    string complexStereo = xelComplex.Value;
                    if (Moiety.decodeStereoType(xelComplex.Value) != null) {
                        SpecialStereo = complexStereo;
                    }
                }

                XElement xel = desc_xml.XPathSelectElement("/STEREOCHEMISTRY/TYPE");
                if (SpecialStereo == null) {
                    if (!String.IsNullOrWhiteSpace(xel?.Value)) {
                        if (xel.Value.Trim().ToUpper() != "UNKNOWN") {
                            SpecialStereo = xel.Value;
                        } else {
                            XElement xel1 = desc_xml.XPathSelectElement("/STEREOCHEMISTRY/OPT_ACT");
                            if (xel1 != null && !String.IsNullOrWhiteSpace(xel1.Value)) {
                                SpecialStereo = xel1.Value;
                            }
                        }
                    }
                }



			}
		}

		[Obsolete]
		public static string DecodeNames(string names)
		{
			Regex rgs = new Regex("(#4#)(.+)(#5#)");
			if ( names != null ) names = rgs.Replace(names, "$1$3");
			return names == null ? null : names.Replace("&", "&amp;").
				Replace("<", "&lt;").
				Replace("#1#", "<asNamedEntity><code code=\"").
				Replace("#2#", "\" displayName=\"").
				Replace("#3#", "\" codeSystem=\"2.16.840.1.113883.3.26.1.1\"/><name>").
				Replace("#4#", "</name>").
				Replace("#5#", "</asNamedEntity>").
				Replace("#6#", "<subjectOf><document><id extension=\"").
				Replace("#7#", "\" root=\"2.16.840.1.113883.3.2968\"/><title>").
				Replace("#8#", "</title></document></subjectOf>").
				Replace("#9#", "<identifiedSubstance><id extension=\"").
				Replace("#A#", "\" root=\"2.16.840.1.113883.4.9\"/><identifiedSubstance><code code=\"").
				Replace("#B#", "\" displayName=\"").
				Replace("#C#", "\" codeSystem=\"2.16.840.1.113883.4.9\"/>");
		}

		public static string DecodePrimaryName(string names)
		{
			string str = String.Concat("<Root>", names, "</Root>");
			XDocument xdoc = XDocument.Parse(str);
			if ( xdoc != null ) {
				IEnumerable<XElement> xels = xdoc.XPathSelectElements("/Root/asNamedEntity");
				foreach ( XElement xel in xels ) {
					if ( xel.XPathSelectElement("code").Attribute("displayName").Value == "primary name" ) {
						return xel.XPathSelectElement("name").Value.Replace("&", "&amp;").Replace("<", "&lt;");
					}
				}
				return null;
			}
			return null;
		}

		public override IEnumerable<XElement> Subjects {
			get {
				// Main complex entity - identifiedSubstance
				XElement xIdentifiedSubstance2 =
						new XElement(xmlns.spl + "identifiedSubstance",
							new XElement(xmlns.spl + "code", new XAttribute("code", UNII ?? ""), new XAttribute("codeSystem", "2.16.840.1.113883.4.9")),
							new SplHash(UID?.FormatAsGuid()).SPL
					);

                XElement xIdentifiedSubstance1 =
                        new XElement(xmlns.spl + "identifiedSubstance",
                            new XElement(xmlns.spl + "id", new XAttribute("extension", UNII), new XAttribute("root", "2.16.840.1.113883.4.9")),
                            xIdentifiedSubstance2
                        );

                // Top level subject containing the main entity
                XElement xSubject =
					new XElement(xmlns.spl + "subject",
                        xIdentifiedSubstance1);
                
				if ( Moieties != null ) {
                    IList<Moiety> mlist = new List<Moiety>();
                    
                    Boolean undef = false;
                    

                    foreach (var m in Moieties){
                        mlist.Add(m);
                        if (m.UndefinedAmount){
                            undef = true;
                        }
                    }

                    //Add stereo characteristic if there is only one moeity,
                    //AND it has stereoSPL
                    if (mlist.Count() == 1) {
                        Moiety moiety=mlist[0];
                        XElement opt = moiety.OpticalActivitySPL;
                        if (opt != null) {
                            xIdentifiedSubstance1.Add(opt);
                        }
                    }

                    foreach (var m in Moieties){
                        m.ParentMixtureCount = mlist.Count;
                        m.isParentUndefined = undef;


                        XElement MoietySPL = m.SPL;
                        //YP Check to make sure that the parent moiety is not the only moiety in the mixture
                        if (m.IsMixture && m.ParentMixtureCount == 1 && m.RepresentativeStructure==false) {
                            //if it is the case then add parent's submoieties as the parent moieties
                            foreach (Moiety sm in m.Submoieties) {
                                xIdentifiedSubstance2.Add(sm.SPL);
                            }
                        }
                        else {
                            xIdentifiedSubstance2.Add(m.SPL);
                        }

                        
                    }
                }

				yield return xSubject;
			}
		}
	}
}
