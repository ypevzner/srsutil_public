using FDA.SRS.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace FDA.SRS.ObjectModel
{
	public class Mixture : SrsObject
	{

        public class DerivationProcess
        {
            public class Interactor
            {
                public string typeCode;
                public string unii;
                public string name;
                public string codeSystem;
                public string UID()
                {
                    return (unii + typeCode + codeSystem);
                }
            }

            public string UID()
            {
                string components_part = (components.Count == 0 ? "" : "[" + String.Join("|", components.OrderBy(t => t.Item1.UID()).Select(t => t.Item1.UID() + "_" + t.Item2.ToString())) + "]");
                string interactors_part = (interactors.Count == 0 ? "" : "[" + String.Join("|", interactors.OrderBy(t => t.UID()).Select(t => t.UID())) + "]");
                return (code + codeSystem + components_part + interactors_part);
            }

            public List<Interactor> interactors = new List<Interactor>();

            public string DisplayName;

            public string code;

            public string codeSystem;

            //YP child derivation processes with sequence numbes
            public List<Tuple<DerivationProcess, int>> components = new List<Tuple<DerivationProcess, int>>();

        }

        public DerivationProcess derivation_process;

        public MixtureSubstance MixtureSubstance { get; set; }

		public override string UID
		{
			get {
                //return (MixtureSubstance?.Moieties?.UID() + (derivation_process != null ? ("_" + derivation_process.UID()) : "")).GetMD5String();
                //return (MixtureSubstance?.Moieties?.UID() + (derivation_process != null ? ("_" + derivation_process.UID().GetMD5String()) : ""));
                if (derivation_process != null)
                {
                    return (MixtureSubstance?.Moieties?.UID() + "_" + derivation_process.UID()).GetMD5String();
                }
                else
                {
                    return MixtureSubstance?.Moieties?.UID();
                }
                //return MixtureSubstance?.Moieties?.UID();
            }
		}

		public override IEnumerable<XElement> Subjects
		{
			get
			{
                // Main complex entity - identifiedSubstance
                XElement xIdentifiedSubstanceMain =
                    new XElement(xmlns.spl + "identifiedSubstance",
                          new XElement(xmlns.spl + "id",
                          new XAttribute("extension", UNII),
                          new XAttribute("root", "2.16.840.1.113883.4.9")
                   ));

                XElement xIdentifiedSubstance2 =
						new XElement(xmlns.spl + "identifiedSubstance",
							new XElement(xmlns.spl + "code", new XAttribute("code", UNII ?? ""), new XAttribute("codeSystem", "2.16.840.1.113883.4.9")),
							new SplHash(UID?.FormatAsGuid()).SPL);

                xIdentifiedSubstanceMain.Add(xIdentifiedSubstance2);
                 // Top level subject containing the main entity
                 /* XElement xSubject =
                      new XElement(xmlns.spl + "subject",
                          new XElement(xmlns.spl + "identifiedSubstance",
                              new XElement(xmlns.spl + "id", new XAttribute("extension", UNII), new XAttribute("root", "2.16.840.1.113883.4.9")),
                              xIdentifiedSubstance2
                          ));*/
                 XElement xSubject =
                    new XElement(xmlns.spl + "subject", xIdentifiedSubstanceMain);


                if ( MixtureSubstance != null && MixtureSubstance.Moieties != null ) {

                    Moiety firstUndefiend = MixtureSubstance.Moieties.Find(m => m.UndefinedAmount);
                                        
                    foreach (var m in MixtureSubstance.Moieties)
                    {
                        m.ParentMixtureCount = MixtureSubstance.Moieties.Count;
                        if (firstUndefiend != null){
                            m.isParentUndefined = true;
                        }
                        xIdentifiedSubstance2.Add(m.SPL);
                    }
				}

                //Added for Ticket 349
                if (MixtureSubstance != null && MixtureSubstance.SourceMaterialExists == true)
                {
                    XElement xProductOf = new XElement(xmlns.spl + "productOf");
                    XElement xDerivationProcessMain = new XElement(xmlns.spl + "derivationProcess");

                    //Added for Ticket 375:Add code to all top layer <derivationProcess> elements
                    var xCode = new XElement(xmlns.spl + "code", new XAttribute("code", "C25572"),
                                    new XAttribute("displayName", "Modification"),
                                    new XAttribute("codeSystem", "2.16.840.1.113883.3.26.1.1"));


                    var xInteractor = new XElement(xmlns.spl + "interactor", new XAttribute("typeCode", "CSM"));

                    var xIdentifiedSubstanceOuter = new XElement(xmlns.spl + "identifiedSubstance",
                        new XElement(xmlns.spl + "id", new XAttribute("extension", MixtureSubstance.RefApprovalId ?? ""),
                        new XAttribute("root", "2.16.840.1.113883.4.9")));

                    var xIdentifiedSubstanceInternal = new XElement(xmlns.spl + "identifiedSubstance",
                                   new XElement(xmlns.spl + "code", new XAttribute("code", MixtureSubstance.RefApprovalId ?? ""),
                                   new XAttribute("codeSystem", "2.16.840.1.113883.4.9")),
                                   new XElement(xmlns.spl + "name", MixtureSubstance.RefPName ?? ""));

                    var xComponent = new XElement(xmlns.spl + "component");
                    var xSequenceNumber = new XElement(xmlns.spl + "sequenceNumber",
                                    new XAttribute("value", "1"));
                    var xDerivationProcess = new XElement(xmlns.spl + "derivationProcess",
                                new XElement(xmlns.spl + "code", new XAttribute("code", MixtureSubstance.Code ?? ""),
                                new XAttribute("displayName", MixtureSubstance.DisplayName ?? ""),
                                new XAttribute("codeSystem", MixtureSubstance.CodeSystem ?? "")));

                    xProductOf.Add(xDerivationProcessMain);

                    xDerivationProcessMain.Add(xCode);//Ticket 375
                    xDerivationProcessMain.Add(xInteractor);
                    xDerivationProcessMain.Add(xComponent);

                    xIdentifiedSubstanceOuter.Add(xIdentifiedSubstanceInternal);
                    xInteractor.Add(xIdentifiedSubstanceOuter);

                    xComponent.Add(xSequenceNumber);
                    xComponent.Add(xDerivationProcess);

                    xIdentifiedSubstanceMain.Add(xProductOf);

                }
                //// 

                    yield return xSubject;
			}
		}
	}
}
