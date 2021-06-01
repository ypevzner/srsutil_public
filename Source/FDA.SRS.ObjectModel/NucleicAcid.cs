using FDA.SRS.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace FDA.SRS.ObjectModel
{
    public class NucleicAcid : PolymerBase
    {
        public string SeqType { get; set; }

        public List<NASubunit> Subunits { get; set; } = new List<NASubunit>();

        public List<NALink> Links { get; set; } = new List<NALink>();

        public List<NASequence> Sequences { get; set; } = new List<NASequence>();

        public List<NAFragment> Bases = new List<NAFragment>();
        public List<NAFragment> SugarLinkers = new List<NAFragment>();
        public List<NAFragment> Sugars = new List<NAFragment>();

        //YP these are used to store names and positions of components
        // for example a linkage naP on chain 1, residue 2 would be stored as a tuple ("naP","1_2")
        public List<Tuple<String,String>> Bases_listing = new List<Tuple<String, String>>();
        public List<Tuple<String, String>> SugarLinkers_listing = new List<Tuple<String, String>>();
        public List<Tuple<String, String>> Sugars_listing = new List<Tuple<String, String>>();
        public List<Tuple<String, String>> StructuralModifications_listing = new List<Tuple<String, String>>();

        private List<List<Tuple<int, int>>> _ConnectorsCache { get; set; } = new List<List<Tuple<int, int>>>();

        
        
        public NAFeatures Type
        {
            get
            {
                return
                    (Subunits.Count() > 0 ? NAFeatures.Basic : 0) |
                    (NAModifications.Count() > 0 ? NAFeatures.Modified : 0);
            }
        }

        public override string UID
        {
            get
            {
                return (
                    String.Join("|", Subunits.OrderBy(s => s.Sequence.ToString()).Select(s => s.UID)) + "_" +
                    String.Join("|", NAModifications.Select(s => s.UID)) + "_" +
                    String.Join("|", NAFragments.Select(s => s.UID))
                ).GetMD5String();
            }
        }

        //YP SRS-383
        //this would indicate if the substance only has one subunit and no other subunits and no modifications, links, etc.
        public bool isSingleMoietySubstance
        {
            get
            {
                return (this.Subunits.Count == 1 && this.NAModifications.Count == 0 && this.NAFragments.Count == 0 && this.Links.Count == 0);
            }
        }

        public override IEnumerable<XElement> Subjects
        {
            get
            {
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

                if (MolecularWeight != null && SplOptions.ExportOptions.Features.Has("include-na-mass"))
                    xSubject.Descendants().First().Add(new SplCharacteristic("mass-from-formula", MolecularWeight.Amount.UID).SPL);

                // Components of the main entity
                // xIdentifiedSubstance2.Add(new XComment("subunits start"));
                foreach (var s in Subunits)
                    xIdentifiedSubstance2.Add(s.SPL);
                // xIdentifiedSubstance2.Add(new XComment("subunits end"));

                // xIdentifiedSubstance2.Add(new XComment("modifications start"));
                NAModifications.Reverse();
                foreach (var s in NAModifications)
                {
                    if (s is NAStructuralModificationGroup)
                    {
                        foreach (var x in ((NAStructuralModificationGroup)s).SPL2)
                            xIdentifiedSubstance2.Add(x);
                    }
                }
                /*List<NAModification> NAModifications_temp = NAModifications;
                
                
                for (int i = 0; i < NAModifications.Count(); i++)
                {
                    if (NAModifications[i] is NAStructuralModificationGroup)
                    {
                        foreach (var x in ((NAStructuralModificationGroup)NAModifications[i]).SPL2)
                            xIdentifiedSubstance2.Add(x);
                        //var spl2s = ((NAStructuralModificationGroup)NAModifications[i]).SPL2;
                        //for (int j=0; j < spl2s.Count(); j++)
                        //{
                        //    xIdentifiedSubstance2.Add(spl2s[j]);
                        //}
                    }
                }
                */



                foreach (var s in Links)
                    xIdentifiedSubstance2.Add(s.SPL);
                // xIdentifiedSubstance2.Add(new XComment("modifications end"));

                yield return xSubject;

                // Independent entities used/referenced in the main identifiedSubstance
                if (SplOptions.ExportOptions.Features.Has("separate-sequence-definition"))
                {
                    foreach (var s in Sequences)
                        yield return s.SPL;
                }

                foreach (var s in NAFragments)
                    if (!s.isDeletion)
                        yield return s.SPL;
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

        public List<NASite> reorderNASites(List<NASite> olist)
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
