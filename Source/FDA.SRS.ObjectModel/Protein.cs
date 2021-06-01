using FDA.SRS.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace FDA.SRS.ObjectModel
{
    public class Protein : PolymerBase
    {
        public string SeqType { get; set; }

        public List<Subunit> Subunits { get; set; } = new List<Subunit>();

        public List<Link> Links { get; set; } = new List<Link>();

        public List<Glycosylation> Glycosylations { get; set; } = new List<Glycosylation>();

        public List<Sequence> Sequences { get; set; } = new List<Sequence>();

        private List<List<Tuple<int, int>>> _ConnectorsCache { get; set; } = new List<List<Tuple<int, int>>>();

        public class DerivationProcess
        {
            public class Interactor
            {
                public string typeCode;
                public string unii;
                public string AgentHashCode;
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

        //YP SRS-383
        //this would indicate if the substance only has one subunit and no other subunits and no modifications, links, etc.
        public bool isSingleMoietySubstance
        {
            get
            {
                return (this.Subunits.Count == 1 && this.Modifications.Count == 0 && this.NAFragments.Count == 0 && this.Links.Count == 0 && this.Glycosylations.Count == 0);
            }
        }

        public ProteinFeatures ProteinType
        {
            get
            {
                return
                    (Subunits.Count() > 0 ? ProteinFeatures.Basic : 0) |
                    (Glycosylations.Count() > 0 ? ProteinFeatures.Glycosilated : 0) |
                    (Links.Count() > 0 ? ProteinFeatures.Linked : 0) |
                    (Modifications.Count() > 0 ? ProteinFeatures.Modified : 0);
            }
        }

        public override string UID
        {
            get
            {
                return (
                    String.Join("|", Subunits.OrderBy(s => s.Sequence.ToString()).Select(s => s.UID)) + "_" +
                    String.Join("|", Links.Select(s => s.UID)) + "_" +
                    String.Join("|", Glycosylations.Select(s => s.UID)) + "_" +
                    String.Join("|", Modifications.Select(s => s.UID)) + "_" +
                    String.Join("|", Fragments.Select(s => s.UID)) +
                    (derivation_process != null ? ("_" + derivation_process.UID()) : "")
                ).GetMD5String();
            }
        }

        public string subunit_UID
        {
            get
            {
                return (
                    String.Join("|", Subunits.OrderBy(s => s.Sequence.ToString()).Select(s => s.UID))
                ).GetMD5String();
            }
        }

        public string LegacyUID {
            get{
                int c = this.Modifications
                      .Where(m => m is PhysicalModification)
                      .Count();
                List<ProteinModification> omods = this.Modifications;

                //This is a terrible idea to try to sometimes have backwards
                //compatibility.
                if (c == 0) {
                            PhysicalModification m = new PhysicalModification(this.Subunits[0].RootObject);
                
                                this.Modifications = new List<ProteinModification>();
                                this.Modifications.Add(m);
                                this.Modifications.AddRange(omods);
                }
                String uid1 = this.UID;
                this.Modifications = omods;
                return uid1;
            }
        }

        public override IEnumerable<XElement> Subjects
        {
            get
            {
                //Ticket 271: code for Agent Modification
                var xProductOf = new XElement(xmlns.spl + "productOf");
                var xDerivationProcessOuter = new XElement(xmlns.spl + "derivationProcess");
                xProductOf.Add(xDerivationProcessOuter);
                Boolean AgentFlag = true, AgentExists = false, AgentBipass = false;
                //End

                // Main complex entity - identifiedSubstance
                XElement xIdentifiedSubstance2 =
                        new XElement(xmlns.spl + "identifiedSubstance",
                            new XElement(xmlns.spl + "code", new XAttribute("code", UNII ?? ""), new XAttribute("codeSystem", "2.16.840.1.113883.4.9")),
                            new SplHash(UID?.FormatAsGuid()).SPL
                    // new XElement(xmlns.spl + "name", "unknown")
                    );

                // Top level subject containing the main entity
                XElement xSubject =
                    new XElement(xmlns.spl + "subject",
                        new XElement(xmlns.spl + "identifiedSubstance",
                            new XElement(xmlns.spl + "id", new XAttribute("extension", UNII), new XAttribute("root", "2.16.840.1.113883.4.9")),
                            xIdentifiedSubstance2
                        ));
                //Added to avoid other than Agent Modification
                foreach (var s in Modifications)
                {
                    if (s is AgentModification)
                    {
                        AgentBipass = true;
                        break;
                    }
                    continue;

                }
                ////

                // xIdentifiedSubstance2.Add(new XComment("modifications start"));
                foreach (var s in Modifications)
                {

                    if (s is PhysicalModification)
                    {
                        throw new SrsException("general_error", "Physical Modification Exists");
                    }
                    //Ticket 271: code for Agent Modification
                    if (s is AgentModification)
                    {

                        if (AgentFlag == true)
                        {
                            AgentExists = true;
                            var xCode = 
                                new XElement(xmlns.spl + "code", new XAttribute("code", "C25572"),
                                new XAttribute("displayName", "Modification"),
                                new XAttribute("codeSystem", "2.16.840.1.113883.3.26.1.1"));
                            xDerivationProcessOuter.Add(xCode);

                            var y = ((AgentModification)s).SPL1;
                            xDerivationProcessOuter.Add(y);
                        }
                        AgentFlag = false;

                        foreach (var x in ((AgentModification)s).SPL2)
                        {
                            xDerivationProcessOuter.Add(x);
                            AgentModification.SeqCount++;
                        }

                    }


                    if (s is StructuralModificationGroup)
                    {
                        if (AgentExists == false && AgentBipass == false)
                        {
                            //Commented on 10/10/18 to maintain the order 
                            //foreach (var x in ((StructuralModificationGroup)s).SPL2)
                            //xIdentifiedSubstance2.Add(x);
                        }
                        else
                        {
                            //Uncommented on 10/10/18
                            throw new SrsException("general_error", "Structural Modification Exists");
                        }
                    }

                }

                if (AgentExists == true)
                    xIdentifiedSubstance2.Add(xProductOf);
                else
                {
                    //End of Ticket 271

                    if (MolecularWeight != null && SplOptions.ExportOptions.Features.Has("include-protein-mass"))
                        xSubject.Descendants().First().Add(new SplCharacteristic("mass-from-formula", MolecularWeight.Amount.UID).SPL);

                    // Components of the main entity
                    // xIdentifiedSubstance2.Add(new XComment("subunits start"));
                    foreach (var s in Subunits)
                        xIdentifiedSubstance2.Add(s.SPL);
                    // xIdentifiedSubstance2.Add(new XComment("subunits end"));

                    //Added on 10/10/18 to maintain the order 
                    foreach (var s in Modifications)
                    {
                        //YP comment this out to include AMINO ACID REMOVAL modifications
//                        if (s.ModificationType != "AMINO ACID REMOVAL")
//                        {
                            if (s is StructuralModificationGroup)
                            {
                                foreach (var x in ((StructuralModificationGroup)s).SPL2)
                                    xIdentifiedSubstance2.Add(x);
                            }
//                        }
                    }
                    ////

                    // xIdentifiedSubstance2.Add(new XComment("glycosilation start"));
                    foreach (var g in Glycosylations)
                        xIdentifiedSubstance2.Add(g.SPL);
                    // xIdentifiedSubstance2.Add(new XComment("glycosilation end"));


                    // xIdentifiedSubstance2.Add(new XComment("modifications end"));
                    // xIdentifiedSubstance2.Add(new XComment("disulfide bridges start"));
                    // All links - not just disulfide
                    foreach (var s in Links)
                        xIdentifiedSubstance2.Add(s.SPL);
                    // xIdentifiedSubstance2.Add(new XComment("disulfide bridges end"));

                }//added for ticket 271


                yield return xSubject;

                // Independent entities used/referenced in the main identifiedSubstance
                //Added to avoid other than Agent Modification
                if (AgentBipass == false)
                {
                    if (SplOptions.ExportOptions.Features.Has("separate-sequence-definition"))
                    {
                        foreach (var s in Sequences)
                            yield return s.SPL;
                    }

                    foreach (var s in Fragments)
                        if (!s.isDeletion)
                            yield return s.SPL;
                }

            }
        }

        /*
         * Set the connector order for the sites used in a linker.
         * 
         * 
         */
        public void setConnectorsData(List<List<Tuple<int, int>>> refCon)
        {
            _ConnectorsCache = refCon;

        }


        public List<ProteinSite> reorderProteinSites(List<ProteinSite> olist)
        {
            if (_ConnectorsCache != null)
            {
                List<Tuple<int, int>> otuplist = olist.Map(s => s.ToTuple())
                                                   .ToList();


                List<List<Tuple<int, int>>> filtered = _ConnectorsCache.Filter(l => {
                    //filter out all sites not relavent
                    //make sure that all sites are found
                    return otuplist.Filter(csite => {
                        return !l.Contains(csite);
                    }).ToList()
                      .Count() == 0;
                }).ToList();

                try
                {
                    if (filtered.Count() > 0)
                    {
                        //Order them
                        return olist.OrderBy((a) => {
                            return filtered[0].IndexOf(a.ToTuple());
                        }).ToList();
                    }
                }
                catch (Exception e)
                {
                    throw e;
                }
            }
            return olist;
        }

    }
}
