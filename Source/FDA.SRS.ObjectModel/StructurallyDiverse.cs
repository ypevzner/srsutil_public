using FDA.SRS.Utils;
using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Linq;
using System.Linq;

namespace FDA.SRS.ObjectModel {
    public class StructurallyDiverse : SrsObject {

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

        public StructurallyDiverse Parent { get; set; } // TODO: should be reference to a general record, not necessarily SD
        public StructurallyDiverse Agent { get; set; }  // TODO: should be reference to a general record, not necessarily SD

        public List<AgentModification> AgentModifications { get; set; } = new List<AgentModification>(); //Added for Ticket 312  : Structurally Diverse
        public static int SeqCount { get; set; } = 0; //Added for Ticket 369

        private string _authors;
        public string Authors {
            get { return _authors; }
            set { _authors = String.IsNullOrWhiteSpace(value) ? null : value.CasifyAuthors(); }
        }

        public string BibReference {
            get {
                return String.Join(" ", SourceMaterial == null ? "" : SourceMaterial.Reference, Authors).Trim();
            }
        }

        public SourceMaterial SourceMaterial { get; set; }

        public AgentModification AgentModification { get; set; }

        public override string UID {
            get {
                StringBuilder sb = new StringBuilder(BibReference);
                XElement xCharacteristic = InfraspecificSpl;
                if (xCharacteristic != null)
                    sb.Append(xCharacteristic.Value);
                sb.Append((derivation_process != null ? ("_" + derivation_process.UID()) : ""));
                return sb.ToString().GetMD5String();
            }
        }

        public override IEnumerable<XElement> Subjects {
            get {

                /* Commented for ticket 312
				XElement xIdentifiedSubstance =
					new XElement(xmlns.spl + "identifiedSubstance",
						new XElement(xmlns.spl + "id", new XAttribute("extension", UNII), new XAttribute("root", "2.16.840.1.113883.4.9")),
						new XElement(xmlns.spl + "identifiedSubstance",
							new XElement(xmlns.spl + "code", new XAttribute("code", UNII ?? ""), new XAttribute("codeSystem", "2.16.840.1.113883.4.9")),
							new SplHash(UID?.FormatAsGuid()).SPL
						)
					);

				xIdentifiedSubstance.Add(
					new XElement(xmlns.spl + "subjectOf",
						new XElement(xmlns.spl + "document",
							new XElement(xmlns.spl + "bibliographicDesignationText", BibReference)
						)
					));

				XElement xCharacteristic = InfraspecificSpl;
				if ( xCharacteristic != null )
					xIdentifiedSubstance.Add(xCharacteristic);

				/* TODO: we don't deal with parents as of yet
				if ( Parent != null )
					composeParentStuff(xIdentifiedSubstance);

                 yield return new XElement(xmlns.spl + "subject", xIdentifiedSubstance);

                 }
                }
				 * */

                //Added for Ticket 312 : Structurally Diverse

                XElement xIdentifiedSubstanceMain =
                 new XElement(xmlns.spl + "identifiedSubstance",
                     new XElement(xmlns.spl + "id",
                            new XAttribute("extension", UNII),
                            new XAttribute("root", "2.16.840.1.113883.4.9")
                     ));


                XElement xIdentifiedSubstanceMainInternal =
                  new XElement(xmlns.spl + "identifiedSubstance",
                          new XElement(xmlns.spl + "code", new XAttribute("code", UNII ?? ""),
                          new XAttribute("codeSystem", "2.16.840.1.113883.4.9")) );
                          // new XElement(xmlns.spl + "name", SourceMaterial.Name ?? ""));
                          

                if (SourceMaterial != null)
                {

                    /*Commented as not required
                    //Add the name as a high-level tag, but only if there's a parent
                    if (SourceMaterial.hasParent() && SourceMaterial.ApprovalId != null) {
                        if (!String.IsNullOrEmpty(SourceMaterial.Name)) {
                            xIdentifiedSubstanceMainInternal.Add(new XElement(xmlns.spl + "name", SourceMaterial.Name));
                        }
                    }*/

                    xIdentifiedSubstanceMainInternal.Add(new SplHash(UID?.FormatAsGuid()).SPL);

                    xIdentifiedSubstanceMain.Add(xIdentifiedSubstanceMainInternal);


                    //The following only happens if there's a Reference Parent
                    //otherwise it's just a whole self-contained record
                    if (SourceMaterial.hasParent() && SourceMaterial.ApprovalId != null) //second clause added by L
                    {
                        var xProductOf = new XElement(xmlns.spl + "productOf");
                        var xDerivationProcessOuter = new XElement(xmlns.spl + "derivationProcess");
                        xProductOf.Add(xDerivationProcessOuter);

                        //Added for Ticket 375:Add code to all top layer <derivationProcess> elements
                        var xCode = new XElement(xmlns.spl + "code", new XAttribute("code", "C25572"),
                                    new XAttribute("displayName", "Modification"),
                                    new XAttribute("codeSystem", "2.16.840.1.113883.3.26.1.1"));
                        xDerivationProcessOuter.Add(xCode);
                        

                        xIdentifiedSubstanceMain.Add(xProductOf);

                        var xInteractorOuter = new XElement(xmlns.spl + "interactor", new XAttribute("typeCode", "CSM"));
                        var xIdentifiedSubstanceOuter = new XElement(xmlns.spl + "identifiedSubstance",
                            new XElement(xmlns.spl + "id", new XAttribute("extension", SourceMaterial.ApprovalId),
                            new XAttribute("root", "2.16.840.1.113883.4.9")));
                        var xIdentifiedSubstanceInternalOuter = new XElement(xmlns.spl + "identifiedSubstance",
                           new XElement(xmlns.spl + "code", new XAttribute("code", SourceMaterial.ApprovalId),
                           new XAttribute("codeSystem", "2.16.840.1.113883.4.9")),
                           new XElement(xmlns.spl + "name", SourceMaterial.refPname));

                        xInteractorOuter.Add(xIdentifiedSubstanceOuter);
                        xIdentifiedSubstanceOuter.Add(xIdentifiedSubstanceInternalOuter);
                        xDerivationProcessOuter.Add(xInteractorOuter);

                        SeqCount = 1;

                        //Material Type:Virus
                        if ((!String.IsNullOrEmpty(SourceMaterial.MaterialType)) && (String.Equals(SourceMaterial.MaterialType, "VIRUS", StringComparison.InvariantCultureIgnoreCase)))
                        {
                            //Process Step - 1 :Antigen presentation
                            if (!String.IsNullOrEmpty(SourceMaterial.Fraction))
                            {
                                StructurallyDiverseTerms at = new StructurallyDiverseTerms(SourceMaterial.Fraction + "#####" + SourceMaterial.MaterialType);

                                if (at != null && at.isReal())
                                {
                                    SourceMaterial.Code = at.Code;
                                    SourceMaterial.CodeSystem = at.CodeSystem;
                                    SourceMaterial.DispName = at.DisplayName;
                                    SourceMaterial.InteractorCode = at.InteractorCode;
                                    SourceMaterial.InteractorCodeSystem = at.InteractorCodeSystem;
                                    SourceMaterial.InteractorDisplayName = at.InteractorDisplayName;

                                    if (SourceMaterial.InteractorCode != null && SourceMaterial.InteractorCodeSystem != null
                                         && SourceMaterial.InteractorDisplayName != null){

                                        var xComponent = new XElement(xmlns.spl + "component");

                                        var xSequenceNumber = new XElement(xmlns.spl + "sequenceNumber",
                                           new XAttribute("value", SeqCount));
                                        ++SeqCount;
                                        var xDerivationProcess = new XElement(xmlns.spl + "derivationProcess",
                                           new XElement(xmlns.spl + "code", new XAttribute("code", SourceMaterial.Code),
                                           new XAttribute("displayName", SourceMaterial.DispName),
                                           new XAttribute("codeSystem", SourceMaterial.CodeSystem)));

                                        xComponent.Add(xSequenceNumber);
                                        xComponent.Add(xDerivationProcess);

                                        var xInteractor = new XElement(xmlns.spl + "interactor", new XAttribute("typeCode", "CSM"));
                                        var xIdentifiedSubstanceOut = new XElement(xmlns.spl + "identifiedSubstance",
                                             new XElement(xmlns.spl + "id", new XAttribute("extension", SourceMaterial.InteractorCode),
                                             new XAttribute("root", SourceMaterial.InteractorCodeSystem)));
                                        var xIdentifiedSubstanceInternal = new XElement(xmlns.spl + "identifiedSubstance",
                                        new XElement(xmlns.spl + "code", new XAttribute("code", SourceMaterial.InteractorCode),
                                        new XAttribute("codeSystem", SourceMaterial.InteractorCodeSystem)),
                                        new XElement(xmlns.spl + "name", SourceMaterial.InteractorDisplayName));

                                        xDerivationProcess.Add(xInteractor);
                                        xInteractor.Add(xIdentifiedSubstanceOut);
                                        xIdentifiedSubstanceOut.Add(xIdentifiedSubstanceInternal);


                                        xDerivationProcessOuter.Add(xComponent);
                                    }
                                }
                            }
                            //Take agent as definition of the process
                            //Process Step - 2 :Attenuation Agent
                            if (AgentModifications.Count > 0)
                            {
                                //++SeqCount;
                                foreach (var s in AgentModifications)
                                {
                                    StructurallyDiverseTerms at = new StructurallyDiverseTerms(s.Role + "###" + s.ModificationType);
                                    //StructurallyDiverseTerms at = new StructurallyDiverseTerms("MODIFICATION" + "###" + s.Role);

                                    if (at != null && at.isReal())
                                       {
                                            SourceMaterial.Code = at.Code;
                                            SourceMaterial.CodeSystem = at.CodeSystem;
                                            SourceMaterial.DispName = at.DisplayName;

                                            var xComponent = new XElement(xmlns.spl + "component");
                                           
                                            var xSequenceNumber = new XElement(xmlns.spl + "sequenceNumber",
                                                            new XAttribute("value", SeqCount));
                                           
                                            var xDerivationProcess = new XElement(xmlns.spl + "derivationProcess",
                                                        new XElement(xmlns.spl + "code", new XAttribute("code", SourceMaterial.Code),
                                                        new XAttribute("displayName", SourceMaterial.DispName),
                                                        new XAttribute("codeSystem", SourceMaterial.CodeSystem)));

                                            if (s.AgentId != null)
                                            {
                                                var xInteractor = new XElement(xmlns.spl + "interactor", new XAttribute("typeCode", "CSM"));//Ticket 312
                                                var xIdentifiedSubstanceOut = new XElement(xmlns.spl + "identifiedSubstance",
                                                        new XElement(xmlns.spl + "id", new XAttribute("extension", s.AgentId),
                                                        new XAttribute("root", "2.16.840.1.113883.4.9")));
                                                var xIdentifiedSubstanceInternal = new XElement(xmlns.spl + "identifiedSubstance",
                                                        new XElement(xmlns.spl + "code", new XAttribute("code", s.AgentId),
                                                        new XAttribute("codeSystem", "2.16.840.1.113883.4.9")),
                                                        new XElement(xmlns.spl + "name", s.Name));

                                                xDerivationProcess.Add(xInteractor);
                                                xInteractor.Add(xIdentifiedSubstanceOut);
                                                xIdentifiedSubstanceOut.Add(xIdentifiedSubstanceInternal);

                                                xComponent.Add(xSequenceNumber);
                                                xComponent.Add(xDerivationProcess);
                                                xDerivationProcessOuter.Add(xComponent);
                                            }
                                            else
                                            {
                                                StructurallyDiverseTerms at1 = new StructurallyDiverseTerms(s.Name + "###" + "");

                                                if(at1 != null && at1.isReal())
                                                {
                                                    SourceMaterial.Code = at1.Code;
                                                    SourceMaterial.CodeSystem = at1.CodeSystem;
                                                    SourceMaterial.DispName = at1.DisplayName;

                                                    var xDerivationProcessI = new XElement(xmlns.spl + "derivationProcess",
                                                            new XElement(xmlns.spl + "code", new XAttribute("code", SourceMaterial.Code),
                                                            new XAttribute("displayName", SourceMaterial.DispName),
                                                            new XAttribute("codeSystem", SourceMaterial.CodeSystem)));

                                                    xComponent.Add(xSequenceNumber);
                                                    xComponent.Add(xDerivationProcessI);
                                                    xDerivationProcessOuter.Add(xComponent);
                                                }
                                            }

                                       }
                               }
                               ++SeqCount;
                            }


                            //Process Step - 3 :Hemagglutinin - Neuraminidase
                            //Purification - Based on part/fraction
                            if (!String.IsNullOrEmpty(SourceMaterial.Part) && !String.Equals(SourceMaterial.Part, "WHOLE", StringComparison.InvariantCultureIgnoreCase))
                            {
                                //++SeqCount;
                                var xSequenceNumber = new XElement(xmlns.spl + "sequenceNumber",
                                          new XAttribute("value", SeqCount));

                                if (String.IsNullOrEmpty(SourceMaterial.Fraction))
                                {
                                    // StructurallyDiverseTerms at = new StructurallyDiverseTerms(SourceMaterial.Part + "#####" + SourceMaterial.MaterialType);
                                    StructurallyDiverseTerms at = new StructurallyDiverseTerms(SourceMaterial.Part + "###" + SourceMaterial.MaterialType);

                                    if (at != null && at.isReal())
                                    {
                                        SourceMaterial.Code = at.Code;
                                        SourceMaterial.CodeSystem = at.CodeSystem;
                                        SourceMaterial.DispName = at.DisplayName;

                                        var xComponent = new XElement(xmlns.spl + "component");
                                        
                                        var xDerivationProcess = new XElement(xmlns.spl + "derivationProcess",
                                               new XElement(xmlns.spl + "code", new XAttribute("code", SourceMaterial.Code),
                                               new XAttribute("displayName", SourceMaterial.DispName),
                                               new XAttribute("codeSystem", SourceMaterial.CodeSystem)));

                                        xComponent.Add(xSequenceNumber);
                                        xComponent.Add(xDerivationProcess);
                                        xDerivationProcessOuter.Add(xComponent);
                                    }

                                }

                                else if (!String.IsNullOrEmpty(SourceMaterial.Fraction))
                                {
                                   StructurallyDiverseTerms at = null;
                                  //  at = new StructurallyDiverseTerms(SourceMaterial.Fraction + "#####" + SourceMaterial.MaterialType);
                                   // if(at.Code == null)
                                       at = new StructurallyDiverseTerms(SourceMaterial.Fraction + "###" + SourceMaterial.MaterialType);

                                    if (at != null && at.isReal())
                                    {
                                        SourceMaterial.Code = at.Code;
                                        SourceMaterial.CodeSystem = at.CodeSystem;
                                        SourceMaterial.DispName = at.DisplayName;

                                        var xComponent = new XElement(xmlns.spl + "component");
                                        var xDerivationProcess = new XElement(xmlns.spl + "derivationProcess",
                                               new XElement(xmlns.spl + "code", new XAttribute("code", SourceMaterial.Code),
                                               new XAttribute("codeSystem", SourceMaterial.CodeSystem),
                                               new XAttribute("displayName", SourceMaterial.DispName)));

                                        xComponent.Add(xSequenceNumber);
                                        xComponent.Add(xDerivationProcess);
                                        xDerivationProcessOuter.Add(xComponent);
                                    }

                                }
                            }
                        }

                        //Added for ticket 275
                        //Material Type:Plant
                        if ((!String.IsNullOrEmpty(SourceMaterial.MaterialType)) && (String.Equals(SourceMaterial.MaterialType, "PLANT", StringComparison.InvariantCultureIgnoreCase)))
                        {
                            //Process Step - 1 
                            if (!String.IsNullOrEmpty(SourceMaterial.Part) &&
                                !String.Equals(SourceMaterial.Part, "whole", StringComparison.InvariantCultureIgnoreCase))
                            {
                                StructurallyDiverseTerms at1 = new StructurallyDiverseTerms(SourceMaterial.Part + "###" + SourceMaterial.MaterialType);
                               
                                if (at1 != null && at1.Code != null)
                                {
                                    SourceMaterial.Code = at1.Code;
                                    SourceMaterial.CodeSystem = at1.CodeSystem;
                                    SourceMaterial.DispName = at1.DisplayName;

                                    var xComponent = new XElement(xmlns.spl + "component");
                                    var xSequenceNumber = new XElement(xmlns.spl + "sequenceNumber",
                                        new XAttribute("value", SeqCount));
                                      ++SeqCount;
                                    var xDerivationProcess = new XElement(xmlns.spl + "derivationProcess",
                                        new XElement(xmlns.spl + "code", new XAttribute("code", SourceMaterial.Code),
                                        new XAttribute("displayName", SourceMaterial.DispName),
                                        new XAttribute("codeSystem", SourceMaterial.CodeSystem)));

                                    xComponent.Add(xSequenceNumber);
                                    xComponent.Add(xDerivationProcess);

                                    xDerivationProcessOuter.Add(xComponent);
                                }
                            }
                            else if (String.IsNullOrEmpty(SourceMaterial.Part) ||
                                String.Equals(SourceMaterial.Part, "whole", StringComparison.InvariantCultureIgnoreCase)
                                && !String.IsNullOrEmpty(SourceMaterial.Fraction))
                               
                            {
                                StructurallyDiverseTerms at2 = new StructurallyDiverseTerms(SourceMaterial.Fraction + "###" + SourceMaterial.MaterialType);
                                if (at2 != null && at2.Code != null)
                                {
                                    SourceMaterial.Code = at2.Code;
                                    SourceMaterial.CodeSystem = at2.CodeSystem;
                                    SourceMaterial.DispName = at2.DisplayName;

                                    var xComponent = new XElement(xmlns.spl + "component");
                                    var xSequenceNumber = new XElement(xmlns.spl + "sequenceNumber",
                                        new XAttribute("value", SeqCount));
                                    ++SeqCount;
                                    var xDerivationProcess = new XElement(xmlns.spl + "derivationProcess",
                                        new XElement(xmlns.spl + "code", new XAttribute("code", SourceMaterial.Code),
                                        new XAttribute("displayName", SourceMaterial.DispName),
                                        new XAttribute("codeSystem", SourceMaterial.CodeSystem)));

                                    xComponent.Add(xSequenceNumber);
                                    xComponent.Add(xDerivationProcess);

                                    xDerivationProcessOuter.Add(xComponent);

                                }
                            }

                            //Take agent as definition of the process
                            //Process Step - 2 :Attenuation Agent
                            if (AgentModifications.Count > 0)
                            {
                                
                                foreach (var s in AgentModifications)
                                    {
                                        StructurallyDiverseTerms at = new StructurallyDiverseTerms(s.Role + "###" + s.ModificationType);
                                        //StructurallyDiverseTerms at = new StructurallyDiverseTerms("MODIFICATION" + "###" + s.Role);

                                    if (at != null && at.isReal())
                                        {
                                            SourceMaterial.Code = at.Code;
                                            SourceMaterial.CodeSystem = at.CodeSystem;
                                            SourceMaterial.DispName = at.DisplayName;

                                            var xComponent = new XElement(xmlns.spl + "component");

                                            var xSequenceNumber = new XElement(xmlns.spl + "sequenceNumber",
                                                            new XAttribute("value", SeqCount));

                                            var xDerivationProcess = new XElement(xmlns.spl + "derivationProcess",
                                                        new XElement(xmlns.spl + "code", new XAttribute("code", SourceMaterial.Code),
                                                        new XAttribute("displayName", SourceMaterial.DispName),
                                                        new XAttribute("codeSystem", SourceMaterial.CodeSystem)));

                                            if (s.AgentId != null)
                                            {
                                                var xInteractor = new XElement(xmlns.spl + "interactor", new XAttribute("typeCode", "CSM"));//Ticket 312
                                                var xIdentifiedSubstanceOut = new XElement(xmlns.spl + "identifiedSubstance",
                                                        new XElement(xmlns.spl + "id", new XAttribute("extension", s.AgentId),
                                                        new XAttribute("root", "2.16.840.1.113883.4.9")));
                                                var xIdentifiedSubstanceInternal = new XElement(xmlns.spl + "identifiedSubstance",
                                                        new XElement(xmlns.spl + "code", new XAttribute("code", s.AgentId),
                                                        new XAttribute("codeSystem", "2.16.840.1.113883.4.9")),
                                                        new XElement(xmlns.spl + "name", s.Name));

                                                xDerivationProcess.Add(xInteractor);
                                                xInteractor.Add(xIdentifiedSubstanceOut);
                                                xIdentifiedSubstanceOut.Add(xIdentifiedSubstanceInternal);

                                                xComponent.Add(xSequenceNumber);
                                                xComponent.Add(xDerivationProcess);
                                                xDerivationProcessOuter.Add(xComponent);
                                            }
                                            else
                                            {
                                                StructurallyDiverseTerms at1 = new StructurallyDiverseTerms(s.Name + "###" + "");

                                                if (at1 != null && at1.isReal())
                                                {
                                                    SourceMaterial.Code = at1.Code;
                                                    SourceMaterial.CodeSystem = at1.CodeSystem;
                                                    SourceMaterial.DispName = at1.DisplayName;

                                                    var xDerivationProcessI = new XElement(xmlns.spl + "derivationProcess",
                                                            new XElement(xmlns.spl + "code", new XAttribute("code", SourceMaterial.Code),
                                                            new XAttribute("displayName", SourceMaterial.DispName),
                                                            new XAttribute("codeSystem", SourceMaterial.CodeSystem)));

                                                    xComponent.Add(xSequenceNumber);
                                                    xComponent.Add(xDerivationProcessI);
                                                    xDerivationProcessOuter.Add(xComponent);
                                                }
                                            }

                                        }
                                  }
                                   ++SeqCount;
                                }


                                //Process Step - 3 
                                if (!String.Equals(SourceMaterial.Part, "whole", StringComparison.InvariantCultureIgnoreCase)
                                 && !String.IsNullOrEmpty(SourceMaterial.Fraction)
                                 && !String.IsNullOrEmpty(SourceMaterial.Part))
                               {
                                 
                                 StructurallyDiverseTerms at3 = new StructurallyDiverseTerms(SourceMaterial.Fraction + "###" + SourceMaterial.MaterialType);
                                 if (at3 != null && at3.Code != null)
                                 {
                                     SourceMaterial.Code = at3.Code;
                                     SourceMaterial.CodeSystem = at3.CodeSystem;
                                     SourceMaterial.DispName = at3.DisplayName;

                                     var xComponent = new XElement(xmlns.spl + "component");
                                     var xSequenceNumber = new XElement(xmlns.spl + "sequenceNumber",
                                         new XAttribute("value", SeqCount));
                                     var xDerivationProcess = new XElement(xmlns.spl + "derivationProcess",
                                         new XElement(xmlns.spl + "code", new XAttribute("code", SourceMaterial.Code),
                                         new XAttribute("displayName", SourceMaterial.DispName),
                                         new XAttribute("codeSystem", SourceMaterial.CodeSystem)));

                                     xComponent.Add(xSequenceNumber);
                                     xComponent.Add(xDerivationProcess);

                                     xDerivationProcessOuter.Add(xComponent);
                                 }

                             }
                             
                        }
                        //End 275
                    }


                    if (!String.IsNullOrEmpty(BibReference))
                    {
                        xIdentifiedSubstanceMain.Add(
                            new XElement(xmlns.spl + "subjectOf",
                                new XElement(xmlns.spl + "document",
                                    new XElement(xmlns.spl + "bibliographicDesignationText", BibReference)
                                )
                            ));
                    }

                    
                    if (SourceMaterial != null && SourceMaterial.Organism != null &&
                       !String.IsNullOrEmpty(SourceMaterial.Organism.IntraspecificType) &&
                       !String.IsNullOrEmpty(SourceMaterial.Organism.IntraspecificDescription))
                    {
                        StructurallyDiverseTerms at4 = new StructurallyDiverseTerms(SourceMaterial.Organism.IntraspecificType + "###" + "");
                        if (at4 != null && at4.Code != null)
                        {
                            SourceMaterial.Code = at4.Code;
                            SourceMaterial.CodeSystem = at4.CodeSystem;
                            SourceMaterial.DispName = at4.DisplayName;

                            xIdentifiedSubstanceMain.Add(
                               new XElement(xmlns.spl + "subjectOf",
                                   new XElement(xmlns.spl + "characteristic",
                                     new XElement(xmlns.spl + "code", new XAttribute("code", SourceMaterial.Code),
                                       new XAttribute("codeSystem", SourceMaterial.CodeSystem),
                                       new XAttribute("displayName", SourceMaterial.DispName)),
                                     new XElement(xmlns.spl + "value", SourceMaterial.Organism.IntraspecificDescription)
                                         )));
                        }
                    }

                    
                }
                         yield return new XElement(xmlns.spl + "subject", xIdentifiedSubstanceMain);
                    
            }
        }
        //End of Ticket 312



        private XElement InfraspecificSpl {
            get {
                XElement xCharacteristic = null;
                if (SourceMaterial != null &&
                     SourceMaterial.Organism != null &&
                     !String.IsNullOrEmpty(SourceMaterial.Organism.IntraspecificType) &&
                     !String.IsNullOrEmpty(SourceMaterial.Organism.IntraspecificDescription)) {
                    if (new List<string> { "SEROTYPE", "SEROVAR", "SEROGROUP" }.Contains(SourceMaterial.Organism.IntraspecificType.ToUpper()))
                        xCharacteristic = new SplCharacteristic("serotype", SourceMaterial.Organism.IntraspecificDescription).SPL;
                    else if (new List<string> { "CULTIVAR" }.Contains(SourceMaterial.Organism.IntraspecificType.ToUpper()))
                        xCharacteristic = new SplCharacteristic("cultivar", SourceMaterial.Organism.IntraspecificDescription).SPL;
                    else if (new List<string> { "STRAIN", "SUBSTRAIN" }.Contains(SourceMaterial.Organism.IntraspecificType.ToUpper()))
                        xCharacteristic = new SplCharacteristic("strain", SourceMaterial.Organism.IntraspecificDescription).SPL;
                }
                return xCharacteristic;
            }
        }

        private void composeParentStuff(XElement xIdentifiedSubstance) {
            /* Commented for Ticket 312
            XElement xDeriv =
				new XElement(xmlns.spl + "derivationProcess",
					new XElement(xmlns.spl + "interactor", new XAttribute("typeCode", "CSM"),
						new XElement(xmlns.spl + "identifiedSubstance",
							new XElement(xmlns.spl + "id", new XAttribute("extension", Parent.UNII), new XAttribute("root", "2.16.840.1.113883.4.9")),
							new SplCodedItem("identifiedSubstance", "unii", code: Parent.UNII, name: Parent.SourceMaterial.Name).SPL
						)
					)
				);

			// Take PART as definition of the process
			if ( !String.IsNullOrEmpty(SourceMaterial.Part) && !String.Equals(SourceMaterial.Part, "WHOLE", StringComparison.InvariantCultureIgnoreCase) ) {
				xDeriv.Add(
					new XElement(xmlns.spl + "component",
						new XElement(xmlns.spl + "derivationProcess",
							new SplCodedItem("identifiedSubstance", "unii", code: Parent.UNII, name: Parent.SourceMaterial.Name).SPL
					//new XElement(xmlns.spl + "code",
						//new XAttribute("code", "C0924"),
						//new XAttribute("codeSystem", "1.2.3.99.999.1"),
						//new XAttribute("displayName", SourceMaterial.DisplayName))
						)
					)
				);
			}
        
        
			// Take FRACTION as definition of the process
			if ( !String.IsNullOrEmpty(SourceMaterial.Fraction) && !String.Equals(SourceMaterial.Part, "WHOLE", StringComparison.InvariantCultureIgnoreCase) ) {
				xDeriv.Add(
					new XElement(xmlns.spl + "component",
						new XElement(xmlns.spl + "derivationProcess",
							new XElement(xmlns.spl + "code",
								new XAttribute("code", "C0924"),
								new XAttribute("codeSystem", "1.2.3.99.999.1"),
								new XAttribute("displayName", SourceMaterial.DisplayName))
						)
					)
				);
			}

			// Take agent as definition of the process
			if ( AgentModification != null ) {
				xDeriv.Add(
					new XElement(xmlns.spl + "component",
						new XElement(xmlns.spl + "derivationProcess",
							new XElement(xmlns.spl + "code",
								new XAttribute("code", "C0654"),
								new XAttribute("codeSystem", "1.2.3.99.999.1"),
								new XAttribute("displayName", AgentModification.DisplayName))
						)
					)
				);
			}

			xIdentifiedSubstance.Add(new XElement(xmlns.spl + "productOf", xDeriv));
            
            */

        }
    }

    

}
