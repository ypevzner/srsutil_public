using FDA.SRS.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace FDA.SRS.ObjectModel
{
    public class Plmr : PolymerBase
    {
        public string SeqType { get; set; }

        public List<Chain> Subunits { get; set; } = new List<Chain>();

        public new List<PlmrModification> Modifications { get; set; } = new List<PlmrModification>();

        private List<List<Tuple<int, int>>> _ConnectorsCache { get; set; } = new List<List<Tuple<int, int>>>();

        public SplObject RootObject { get; set; }
        public String plmr_geometry { get; set; }
        public String plmr_class { get; set; }
        public String plmr_subclass { get; set; }

        public double? polimerization_factor { get; set; }

        public override string UID
        {
            get
            {
                //YP need to order by uid, both subunits and modifications
                return (
                    String.Join("|", Subunits.OrderBy(s=>s.ToString()).Select(s => s.UID)) + "_" +
                    String.Join("|", Modifications.OrderBy(s=>s.ToString()).Select(s => s.UID))
                ).GetMD5String();
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

                foreach (var s in Subunits)
                    xIdentifiedSubstance2.Add(s.SPL);

                // xIdentifiedSubstance2.Add(new XComment("modifications start"));
                foreach (var s in Modifications)
                {
                    if (s is PlmrStructuralModificationGroup)
                    {
                        //foreach (var x in ((PlmrStructuralModificationGroup)s).SPL2)
                        var mod = ((PlmrStructuralModificationGroup)s).SPL2;
                        xIdentifiedSubstance2.Add(mod);
                        
                    }
                }
                ////


                yield return xSubject;

                foreach (var sub in Subunits)
                {
                    if (sub.SRUs != null)
                    {
                        foreach (var sru in sub.SRUs)
                        {
                            yield return sru.SPL;
                        }
                    }
                }

                foreach (var s in Modifications)
                {
                    if (s is PlmrStructuralModificationGroup)
                    {
                        //foreach (var x in ((PlmrStructuralModificationGroup)s).SPL2)
                        yield return ((PlmrStructuralModificationGroup)s).Modification.Fragment.SPL;

                    }
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
