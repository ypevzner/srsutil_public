using com.epam.indigo;
using FDA.SRS.ObjectModel;
using FDA.SRS.Services;
using FDA.SRS.Utils;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Xml.Linq;
using System.Xml.XPath;
using System.Threading.Tasks;
using System.IO;
using System.Collections.Specialized;
using System.Drawing;

namespace FDA.SRS.Processing
{
    public static class NAExtensions
    {
        private static ConvertOptions conv_opt = null;
        private static int layout_timeout = 3600000;
        private static List<string> standard_sugars = new List<string>() { "R", "dR" };
        private static List<string> standard_linkages = new List<string>() { "P" };

        //YP this is to keep track of which links have already been incorporated into a chain for subsequent joining to other chains
        //each element consists of subunit index, the chain molecule representing that subunit and the pair of indexes of the atoms on that molecule to connect to
        //private static List<Tuple<int, IndigoObject, Tuple<int,int>>> partially_connected_links = new List<Tuple<int, IndigoObject, Tuple<int,int>>>();
        //private static List<Tuple<NALink, Tuple<int, int>>> partially_connected_links = new List<Tuple<NALink, Tuple<int, int>>>();

        private static List<PartiallyConnectedLink> partially_connected_links = new List<PartiallyConnectedLink>();

        private static bool IsSugarStandard(string input_sugar)
        {
            return standard_sugars.Contains(input_sugar);
        }

        private static bool IsLinkageStandard(string input_linkage)
        {
            return standard_linkages.Contains(input_linkage);
        }

        //xNA is the xml document   
        public static NucleicAcid ReadNA(this NucleicAcid na, XElement xNA, SplObject rootObject, ImportOptions impOpt, ConvertOptions opt)
        {
            PolymerBaseReadingState state = new PolymerBaseReadingState { RootObject = rootObject };

            SRSReadingUtils.readElement(xNA, "SEQUENCE_TYPE", st => ValidatedValues.SequenceTypes.Keys.Contains(st), st => na.SeqType = st, na.UNII);

            // Subunits
            int nDeclSubunits = 0;
            SRSReadingUtils.readElement(xNA, "NUMBER_OF_SUBUNITS", n => int.TryParse(n, out nDeclSubunits), n => nDeclSubunits = int.Parse(n), na.UNII);

            IEnumerable<XElement> xels = xNA.XPathSelectElements("SUBUNIT_GROUP");
            if (nDeclSubunits != xels.Count())
                TraceUtils.WriteUNIITrace(TraceEventType.Warning, na.UNII, null, "Declared NUMBER_OF_SUBUNITS does not match found one: {0} != {1}", nDeclSubunits, xels.Count());

            foreach (XElement x in xels)
            {
                NASubunit su = na.ReadSubunit(x, state);
                na.Subunits.Add(su);
                //begin YB
                if (su.Sequence.ToString().Any(c => String.IsNullOrEmpty(Nucleotides.GetNameByLetter(c))))
                    TraceUtils.ReportError("seq_ref", na.UNII, "Unknown letter(s) used in sequence");
                //end YB
            }

            /* Not needed for NAs
            // Disulfide links
            xNA
                .XPathSelectElements("DISULFIDE_LINKAGE")
                .ForAll(x => {
                    if (!String.IsNullOrWhiteSpace(x.Value)) {
                        na
                            .Links
                            .AddRange(
                                Helpers.SequenceLinks(x.Value)
                                .Select(tt => PolymerBaseExtensions.NAFragmentFactory.CreateLink(na, tt, "cys-cys", rootObject))
                            );
                    }
                });

            // Check if any disulfide links were referenced and resolve/add cystein in such case
            if (na.Links.Any(l => l.LinkType == "cys-cys" && l.Linker == null))
                throw new SrsException("fragment", "Disulfide link (cys-cys) cannot be resolved - make sure you have proper registry.sdf");

            // Other links. Constructed based on the same principle as disulfide links.
            xNA
                .XPathSelectElements("OTHER_LINKAGE")
                .ForAll(x => {
                    if (!String.IsNullOrWhiteSpace(x.Value)) {
                        var ol = na.ReadOtherLinkage(x);
                        if (!String.IsNullOrEmpty(ol.Item1)) {
                            na
                                .Links
                                .AddRange(
                                    Helpers.SequenceLinks(ol.Item1)
                                    .Select(tt => PolymerBaseExtensions.NAFragmentFactory.CreateLink(na, tt, ol.Item2, rootObject))
                                );
                        }
                    }
                });
            */

            // Physical modifications
            xNA
                .XPathSelectElements("MODIFICATION_GROUP/PHYSICAL_MODIFICATION_GROUP")
                .ForAll(x => {
                    if (!String.IsNullOrWhiteSpace(x.Value))
                    {
                        var g = na.ReadPhysicalModificationGroup(x, state);
                        if (g != null)
                            na.Modifications.Add(g);
                    }
                });

            // Agent modifications

            xNA
               .XPathSelectElements("MODIFICATION_GROUP/AGENT_MODIFICATION_GROUP")
               .ForAll(x => {
                   if (!String.IsNullOrWhiteSpace(x.Value))
                   {
                       var g = na.ReadAgentModification(x, state);
                       if (g != null)
                           na.Modifications.Add(g);
                   }
               });


            // Structural modifications
            xNA
                .XPathSelectElements("MODIFICATION_GROUP/STRUCTURAL_MODIFICATION_GROUP")
                .ForAll(x => {
                    string inner = x.Value.Trim('0', ' ', ';')
                                  .Replace(" ", "")
                                  .Replace("\r", "")
                                  .Replace("\n", "");
                    //Sometimes a stray amount is the only thing present,
                    //with just a few numbers. In such a case, it's almost certainly
                    //a copy/paste error
                    //
                    //A real structural mod should at LEAST have 3 characters
                    //
                    //If it does not, ignore it
                    //

                    if (!String.IsNullOrWhiteSpace(inner) && (inner.Length > 3))
                    {
                        var g = na.ReadNAStructuralModificationGroup(x, state);
                        na.ProcessStructuralModificationGroup(g, state);
                    }
                });

            // In strict mode check that if MOL exists in SDF record then it was used to resolve at least one fragment
            if (opt.Strict && !state.ExternalMolUsed && MoleculeExtensions.IsCorrectMol(na.Sdf.Mol))
                throw new SrsException("external_mol", "External MOL exists but wasn't referenced in any of fragments in SRS record");

            // Check that all links have associated fragments and all connection points are defined for all fragments
            foreach (var link in na.Links.Where(l => l.LinkType != "cys-cys"))
            {
                if (link.Linker == null)
                    throw new SrsException("link", "Link from OTHER_LINKAGE has no fragment associated/resolved");
                if (link.Linker.Connectors.Count != link.Sites.Count)
                    throw new SrsException("link", "Not all link sites from OTHER_LINKAGE have connection points for fragment defined");
                if (!link.IsCompletelyDefined)
                    throw new SrsException("link", "Link from OTHER_LINKAGE is not completely defined");

                // and register Fragment in an enclosing container (nucleic acid)
                na.RegisterFragment(link.Linker);
            }

            // Molecular Weight
            var xMW = xNA.XPathSelectElement("MOLECULAR_WEIGHT");
            if (xMW != null && !String.IsNullOrEmpty(xMW.Value))
                na.MolecularWeight = na.ReadMolecularWeight(xMW, state);

            na.Canonicalize();

            return na;
        }



        public static void ProcessStructuralModificationGroup(this NucleicAcid na, NAStructuralModificationGroup g, PolymerBaseReadingState state)
        {
            if (g != null)
            {
                bool groupUsedInLinks = false;

                //Okay, the idea here is that a structural modification is _for_ a linkage
                //if (and only if) all sites specified in that structural modification are the same
                //as the sites specified in the linkage. At least that's how it SHOULD work.

                HashSet<String> uniqueModSitesList = g.NucleotideSites.Select(s => s.UID).OrderBy(u => u).ToHashSet();

                String uniqueModSites = String.Join(";", uniqueModSitesList);

                // Iterate through non cys-cys links (e.g. <OTHER_LINKAGE><SITE>1_125-1_275-1_368</SITE><LINKAGE_TYPE/></OTHER_LINKAGE>)
                foreach (var link in na.Links)
                {
                    HashSet<String> uniqueLinkSitesList = link.Sites.Select(s => s.UID).OrderBy(u => u).ToHashSet();
                    int intersect = uniqueLinkSitesList.Intersect(uniqueModSitesList).Count();
                    String uniqueLinkSites = String.Join(";", uniqueLinkSitesList);

                    if (intersect != uniqueModSitesList.Count())
                    {
                        if (intersect > 0)
                        {
                            throw new SrsException("linkFragment", "Some but not all sites in a structural modification are in an other link. If a structural modification references an other link site then all sites must reference that same link. Structural mod sites:'" + uniqueModSites + "' but link sites are: '" + uniqueLinkSites + "'");
                        }
                        continue;
                    }



                    if (uniqueLinkSites.Equals(uniqueModSites))
                    {
                        //They're the same!
                        Console.WriteLine("Found");
                    }
                    else
                    {
                        //continue;
                    }

                    foreach (var linkSite in link.Sites)
                    {
                        foreach (var modSite in g.NucleotideSites)
                        {
                            if (linkSite.UID == modSite.UID)
                            {
                                if (g.Modification.Fragment.Connectors.Count != 1 && !state.CachedFragment)
                                    throw new SrsException("link", "Two and only two connectors (P, O) per fragment associated with LINKER is allowed");
                                else if (link.Linker == null)
                                    // This is the first time we hit the fragment associated with previously identified OTHER_LINK(AGE)
                                    link.Linker = g.Modification.Fragment;
                                //else if ( link.Linker.UID == g.Modification.Fragment.UID )  // ???
                                //	throw new SRSException("link", "Same fragment (molecule + connectors) cannot be used twice in the same link");
                                else if (link.Linker.Molecule.InChIKey != g.Modification.Fragment.Molecule.InChIKey)
                                    throw new SrsException("linkFragment", "Different structural moiety (molecule) is not allowed in the same OTHER_LINK(AGE)");
                                else
                                { // Keep adding connectors to a previously identified Linker
                                    NAFragment.Connector con = g.Modification.Fragment.Connectors.First();

                                    if (!link.Linker.Connectors.Contains(con))
                                    {
                                        link.Linker.Connectors.Add(con);
                                    }
                                }

                                // Link nucleic acid site with fragment's connector and update connector id

                                if (link.NextConnector >= link.Linker.Connectors.Count)
                                {
                                    throw new SrsException("linkSites", "Not enough connectors are available for specified linker (" + link.Linker.UNII + "). (" + link.Linker.Connectors.Count
                                                                + ") available connectors for linker, but structural modification sites reference other link at least ("
                                                                + (link.NextConnector + 1) + ") times");
                                }
                                linkSite.ConnectorRef = link.Linker.Connectors[link.NextConnector];
                                link.IncrementConnector();

                                //per SRS-357 copying modification amount to link so that it shows up in SPL
                                link.Amount = g.Amount;

                                groupUsedInLinks = true;
                            }
                        }
                    }
                }

                // This modification is not related to any link, so add to modifications...
                if (!groupUsedInLinks)
                {
                    na.NAModifications.Add(g);
                    na.RegisterFragment(g.Modification.Fragment);  // ...and add Fragment if not added yet
                }
            }
        }

        public static void Canonicalize(this NucleicAcid na)
        {
            // Sort connectors in canonical order and assigned id as an ordinal in canonical order
            foreach (var f in na.NAFragments)
            {
                f.Connectors = f.Connectors.OrderBy(c => c.Snip).ToList();
                f.Connectors.ForEachWithIndex((c, i) => c.Id = i);
            }


            /* YP comment out the rest of the canonicalization as it is brought in from proteins and I'm not sure if all/any of it is needed for NAs
            
            ////YP overwriting the above assignment of Ids to connectors per SRS-337, Yulia enters things in pre-defined order that needs to be kept
            ////regardless of the order of connectors in registry
            //foreach (NALink link in na.Links)
            //{
            //    link.Sites.ForEachWithIndex((s, i) => {
            //        if (s.ConnectorRef != null)
            //        {
            //            s.ConnectorRef.Id = i;
            //        }
            //    });
            //}

            ////Ticket 271: Commented for Agent Mdification
            ////Remove all nonsense structural mods
            ///*
            //            protein.Modifications.RemoveAll(m => {
            //                if (m is StructuralModificationGroup) {
            //                    return ((StructuralModificationGroup)m).Modification == null;
            //                }
            //                return false;
            //            });
            //*/

            //na.Modifications
            //       .RemoveAll(m => m.DefiningParts.Equals(""));


            ////remove empty sequences
            //na.Subunits.RemoveAll(su => {
            //    if (su.Sequence == null) return true;
            //    return su.Sequence.ToString().Trim().Equals("");
            //});

            //Dictionary<string, HashSet<string>> fragPosMap = new Dictionary<string, HashSet<string>>();
            //Dictionary<string, NAFragment> fragMap = new Dictionary<string, NAFragment>();



            //na.NAModifications
            //    .Where(m => m is NAStructuralModificationGroup)
            //    .Select(m => (NAStructuralModificationGroup)m)
            //    .ForAll(m => {
            //        NAFragment f = m.Modification.Fragment;
            //        HashSet<string> pos;
            //        pos = fragPosMap.TryGetValue(f.Id, out pos) ? pos : null;
            //        if (pos == null) {
            //            pos = new HashSet<string>();
            //            fragPosMap.Add(f.Id, pos);
            //            fragMap.Add(f.Id, f);
            //        }

            //        m.NucleotideSites.ForEach(s => {

            //            if (s.Position == 0) {
            //                pos.Add("N");
            //            } else if (s.Position == s.Subunit.Sequence.Length - 1) {
            //                pos.Add("C");
            //            } else {
            //                pos.Add("M");
            //            }
            //        });
            //    });

            //fragPosMap.AsEnumerable().ForAll(kv => {
            //    NAFragment frag = fragMap[kv.Key];
            //    if (kv.Value.Count() == 1) {

            //        if (frag.Connectors.Count() == 1) {
            //            NAFragment.Connector con = frag.Connectors.First();
            //            //C-term only
            //            if (kv.Value.Contains("C")) {
            //                con.Snip = Tuple.Create(con.Snip.Item1, 0);
            //            } else if (kv.Value.Contains("N")) {
            //                con.Snip = Tuple.Create(0, con.Snip.Item2);
            //            }
            //        }
            //    }

            //});

            ////Cannonicalize modification order
            //na.NAModifications = na.NAModifications.OrderBy(m => m.UID).ToList();

            ///*Commented for Ticket 271:Agent Modification
            //            //For now, throw an excpetion if there are any agent modifications
            //            protein.Modifications
            //                .Where(m => m is AgentModification)
            //                .ForAll(m => {
            //                    throw new Exception("Non-empty agent modifications not supported at this time!");
            //                });
            //*/
            ////For now, throw an excpetion if there are any physical modifications
            //na.NAModifications
            //    .Where(m => m is NAPhysicalModification)
            //    .ForAll(m => {
            //        throw new Exception("Non-empty physical modifications not supported at this time!");
            //    });



        }

        public static void RegisterFragment(this NucleicAcid na, NAFragment fragment)
        {
            if (!na.NAFragments.Any(f => f.UID == fragment.UID))
                na.NAFragments.Add(fragment);
        }

        public static void UnregisterFragment(this NucleicAcid na, NAFragment fragment_in)
        {
            na.NAFragments.RemoveAll(x => x.UID == fragment_in.UID);
        }

        /*
		 * <SITE>3_99-4_99</SITE>
		 * <LINKAGE_TYPE>DIAMIDE </LINKAGE_TYPE>
		 */
        public static Tuple<string, string> ReadOtherLinkage(this NucleicAcid na, XElement xOtherLinkage)
        {
            string site = null, linkageType = null;
            SRSReadingUtils.readElement(xOtherLinkage, "SITE", null, v => site = v.Trim(), na.UNII);
            SRSReadingUtils.readElement(xOtherLinkage, "LINKAGE_TYPE", null, v => linkageType = v.Trim(), na.UNII);
            return new Tuple<string, string>(site, linkageType);
        }


        /*
		 * Get the sites for a specific residue (e.g. "C")
		 */
        public static List<Tuple<NASubunit, int>> SitesMatchingResidue(this NucleicAcid na, char res)
        {
            List<Tuple<NASubunit, int>> sites = new List<Tuple<NASubunit, int>>();

            na.Subunits.ForEach(sub => {

                sub.Sequence.SitesMatchingResidue(res)
                .Map(i => Tuple.Create(sub, i))
                .ForAll(t => sites.Add(t));
            });
            return sites;

        }

        public static NAStructuralModificationGroup ReadNAStructuralModificationGroup(this NucleicAcid na, XElement xStrModGroup, PolymerBaseReadingState state)
        {
            NAStructuralModificationGroup g = new NAStructuralModificationGroup(state.RootObject);

            // Check if modification group is not empty
            XElement xel = xStrModGroup.XPathSelectElement("RESIDUE_SITE");
            if (xel == null || String.IsNullOrWhiteSpace(xel.Value))
                TraceUtils.ReportError("mandatory_field_missing", na.UNII, "Residue sites are not specified - skipping the rest of modification");

            // Try to read fragment
            xel = xStrModGroup.XPathSelectElement("MOLECULAR_FRAGMENT_MOIETY");
            if (xel != null)
            {
                NAStructuralModification m = new NAStructuralModification(state.RootObject);
                g.Modification = na.ReadNAStructuralModification(m, xel, state);
                if (g.Modification == null)
                    throw new SrsException("fragment", String.Format("Cannot read fragment: {0}", xel));
                if (g.Modification.Fragment == null)
                    throw new SrsException("fragment", String.Format("Cannot resolve fragment: {0}", xel));
                if (g.Modification.Fragment.IsMolecule)
                    throw new SrsException("fragment", String.Format("Cannot define fragment connection points: {0}", xel));
                if (g.Modification.Fragment.IsLinker && !state.CachedFragment)
                    // Normally a fragment can only be defined with one pair of connectors, but in OTHER_LINKAGE sitiation can be more complicated by repeatedly adding one pair per STRUCTURE_MODIFICATION
                    throw new SrsException("fragment", String.Format("Fragment is defined with multiple connection points: {0}", xel));

                // AMOUNT_TYPE here superceeds the one defined on MOLECULAR_FRAGMENT_MOIETY level - see SRS-24 comments
                SRSReadingUtils.readElement(xStrModGroup, "AMOUNT_TYPE", ValidatedValues.AmountTypes.Keys.Contains, v => g.Modification.Amount.SrsAmountType = v, na.UNII);
            }

            // Read nucleic acid sites
            xel = xStrModGroup.XPathSelectElement("NUCLEOTIDE_SITE");
            g.NucleotideSites =
                Helpers.SequencePositions(xel.Value)
                .Select(t => {
                    if (t.Item1 == -1)
                    {
                        if (na.Subunits.Count != 1)
                            throw new SrsException("subunit_ref", string.Format("Non-recoveranle reference to non-existing subunit: {0}", xel.Value));

                        t = new Tuple<int, int>(0, t.Item2);
                        TraceUtils.WriteUNIITrace(TraceEventType.Warning, na.UNII, null, "Reference corrected as the only unit is available");
                    }
                    if (t.Item1 >= na.Subunits.Count)
                        throw new SrsException("subunit_ref", String.Format("Reference to non-existing subunit: {0}", t));
                    if (t.Item2 >= na.Subunits[t.Item1].Sequence.Length)
                        throw new SrsException("subunit_ref", String.Format("Reference to non-existing position: {0}", t));

                    //begin YB: added position on proximal moiety (fragment)
                    return new NASite(state.RootObject, "AMINO ACID SUBSTITUTION POINT") { Subunit = na.Subunits[t.Item1], Position = t.Item2, ConnectorRef = g.Modification.Fragment.Connectors.First() };
                    //end YB: added position on proximal moiety (fragment)
                }).ToList();

            SRSReadingUtils.readElement(xStrModGroup, "RESIDUE_MODIFIED", aa => Nucleotides.IsValidAminoAcidName(aa), v => g.Nucleotide = v, na.UNII);

            if (!String.IsNullOrEmpty(g.Nucleotide) && g.NucleotideSites.Any(s => !String.Equals(Nucleotides.GetNameByLetter(s.Letter), g.Nucleotide, StringComparison.InvariantCultureIgnoreCase)))
                TraceUtils.WriteUNIITrace(TraceEventType.Error, na.UNII, null, "Residue {0} does not match all positions", g.Nucleotide);

            if (String.IsNullOrEmpty(g.Nucleotide) && g.NucleotideSites.Count == 1)
            {
                NASite site = g.NucleotideSites.First();
                g.Nucleotide = AminoAcids.GetNameByLetter(site.Letter);
                TraceUtils.WriteUNIITrace(TraceEventType.Information, na.UNII, null, "Residue restored from position: {0} => {1} => {2}", site, site.Letter, g.Nucleotide);
            }

            return g;
        }

        public static char ResolveNAName(String res)
        {
            Dictionary<string, string> aadict = @"ADENINE	A
CYTOSINE	C
GUANINE	G
THYMINE T
URACIL U
A	A
C	C
G   G
T   T
U   U".Replace("\r", "")
               .Split('\n')
               .ToDictionary(l => l.Split('\t')[0], l => l.Split('\t')[1]);

            string aaSingleLetter = aadict.TryGetValue(res.ToUpper(), out aaSingleLetter) ? aaSingleLetter : null;

            if (aaSingleLetter == null) return '0';
            return aaSingleLetter.ToCharArray()[0];

        }


        public static NAStructuralModificationGroup MakeImplicitNAStructuralModification(this NucleicAcid na, NASubunit site_subunit, int site_position, String substitution_type, PolymerBaseReadingState state, NAFragment modification_fragment)
        {
            NAStructuralModificationGroup g = new NAStructuralModificationGroup(state.RootObject);
            NAStructuralModification m = new NAStructuralModification(state.RootObject);

            //YP SRS-381 check if a connector fragment is at first or last positions on the chain and set the appropriate connecting atom to 0 in order to generate nullFlavor in SPL
            if (site_position == 0)
            {
                modification_fragment.Connectors.First().Snip = new Tuple<int,int>(0, modification_fragment.Connectors.First().Snip.Item2);
            } else if (site_position == site_subunit.Sugars.Count()-1)
            {
                modification_fragment.Connectors.First().Snip = new Tuple<int, int>(modification_fragment.Connectors.First().Snip.Item1,0);
            }
            m.Fragment = modification_fragment;
            g.Modification = m;
            g.NucleotideSites.Add(
                new NASite(state.RootObject, substitution_type)
                {
                    Subunit = site_subunit,
                    Position = site_position,
                    ConnectorRef = m.Fragment.Connectors.First()
                });
            g.Amount = new Amount(1);
            g.Modification.Amount = g.Amount;
            return g;
        }

        public static NAStructuralModificationGroup MakeImplicitNAStructuralModification(this NucleicAcid na, List<NASite> sites, PolymerBaseReadingState state, NAFragment modification_fragment)
        {
            NAStructuralModificationGroup g = new NAStructuralModificationGroup(state.RootObject);
            NAStructuralModification m = new NAStructuralModification(state.RootObject);
            m.Fragment = modification_fragment;
            g.Modification = m;
            g.NucleotideSites = sites;
            return g;
        }

        public static NAFragment CreateAssembledFragment(this NucleicAcid na, string fragment_molfile, List<int> connector_atom_indices, List<string> connector_atom_types, PolymerBaseReadingState state)
        {
            NAFragment frag = new NAFragment(state.RootObject) { Molecule = new SDFUtil.NewMolecule(fragment_molfile) };
            frag.AddConnectorsPair((connector_atom_indices[0] == 0?0:frag.getCanonicalAtoms()[connector_atom_indices[0]-1]+1), (connector_atom_indices[1] == 0 ? 0 : frag.getCanonicalAtoms()[connector_atom_indices[1]-1]+1), "implicit_fragment");
            frag.Connectors.ForEachWithIndex((c, i) => c.Id = i);

            try
            {
                return na.NAFragments.Where(f => f.UID == frag.UID).First();
            }
            catch
            {
                //YP SRS-429. I know this looks totally pointless but this is to trigger the generation of fragment ID
                string id = frag.Id;
                return frag;
            }

        }

        public static NAFragment CreateAssembledFragment(this NucleicAcid na, string fragment_molfile, PolymerBaseReadingState state)
        {
            NAFragment frag = new NAFragment(state.RootObject) { Molecule = new SDFUtil.NewMolecule(fragment_molfile) };

            try
            {
                return na.NAFragments.Where(f => f.UID == frag.UID).First();
            }
            catch
            {
                //YP SRS-429. I know this looks totally pointless but this is to trigger the generation of fragment ID
                string id = frag.Id;
                return frag;
            }
        }

        public static NAStructuralModificationGroup ReadNAStructuralModificationJson(this NucleicAcid na, JToken jStrModGroup, PolymerBaseReadingState state)
        {
            NAStructuralModificationGroup g = new NAStructuralModificationGroup(state.RootObject);

            /*
      "[
  {
    "structuralModificationType": "AMINO ACID SUBSTITUTION",
    "residueModified": "GLYCINE",
    "extent": "PARTIAL",
    "extentAmount": {
      "nonNumericValue": "COMPLETE"
    },
    "molecularFragment": {
      "refPname": "GLYCINAMIDE",
      "refuuid": "FAKE_ID:4JDT453NWO",
      "approvalID": "4JDT453NWO",
    },
    "sites": [
      {
        "subunitIndex": 1,
        "residueIndex": 9
      }
    ]
  }
]"       
    */

            String residue = null;
            String modType = null;


            SRSReadingUtils.readJsonElement(jStrModGroup, "residueModified", ex => true, v => residue = v, na.UNII);


            SRSReadingUtils.readJsonElement(jStrModGroup, "structuralModificationType", tp => true, v => modType = v, na.UNII);



            // Check if modification group is not empty
            JToken jsites = jStrModGroup.SelectToken("sites");
            if ((jsites == null || jsites.Count() == 0) && residue == null)
                TraceUtils.ReportError("mandatory_field_missing", na.UNII, "Nucleotide sites are not specified, and no Nucleotide specified - skipping the rest of modification");

            List<Tuple<NASubunit, int>> csites = new List<Tuple<NASubunit, int>>();



            if ((jsites == null || jsites.Count() == 0))
            {
                if (!modType.Equals("MOIETY"))
                {
                    TraceUtils.WriteUNIITrace(TraceEventType.Warning, na.UNII, null, "Nucleotide sites are not specified, but NA specified, calculating sites.");
                    char res1Let = ResolveNAName(residue);

                    if (res1Let == '0')
                    {
                        TraceUtils.WriteUNIITrace(TraceEventType.Error, na.UNII, null, "Unknown Nucleotide designator: {0}", residue);
                    }

                    csites = na.SitesMatchingResidue(res1Let);

                    if (csites.Count <= 0)
                    {
                        TraceUtils.WriteUNIITrace(TraceEventType.Error, na.UNII, null, "No matching nucleotide sites for supplied nucleotide: {0}", res1Let);
                    }
                }
            }
            else
            {
                csites = NAExtensions.fromJsonSites(jsites)
                            .Map(t => NAExtensions.toSiteTuple(t, na))
                            .ToList();
            }



            String extent = "COMPLETE";
            SRSReadingUtils.readJsonElement(jStrModGroup, "extent", ex => true, v => extent = v, na.UNII);

            // Try to read fragment
            JToken jel = jStrModGroup.SelectToken("molecularFragment");
            if (jel != null)
            {
                NAStructuralModification m = new NAStructuralModification(state.RootObject);

                g.Modification = na.ReadNAStructuralModificationJson(m, jStrModGroup, state);

                if (g.ModificationType == "NUCLEIC ACID REMOVAL")
                    g.Modification.Fragment.isDeletion = true;

                if (g.ModificationType == "NUCLEIC ACID REMOVAL" && g.Modification.Amount.isDefaultNumerator)
                {
                    g.Modification.Amount.AmountType = AmountType.UncertainZero;
                }

                g.Amount = g.Modification.Amount;


                if (g.Modification == null)
                    throw new SrsException("fragment", String.Format("Cannot read fragment: {0}", jStrModGroup));
                if (g.Modification.Fragment == null)
                    throw new SrsException("fragment", String.Format("Cannot resolve fragment: {0}", jStrModGroup));
                if (g.Modification.Fragment.IsMolecule)
                    throw new SrsException("fragment", String.Format("Cannot define fragment connection points: {0}", jStrModGroup));
                //YP commenting this one out as it seems not applicable to NAs
                //if (g.Modification.Fragment.IsLinker && !state.CachedFragment)
                // Normally a fragment can only be defined with one pair of connectors, but in OTHER_LINKAGE sitiation can be more complicated by repeatedly adding one pair per STRUCTURE_MODIFICATION
                //throw new SrsException("fragment", String.Format("Fragment is defined with multiple connection points: {0}", jStrModGroup));

                // AMOUNT_TYPE here superceeds the one defined on MOLECULAR_FRAGMENT_MOIETY level - see SRS-24 comments
                SRSReadingUtils.readJsonElement(jStrModGroup, "extentAmount.type", ValidatedValues.AmountTypes.Keys.Contains, v => g.Modification.Amount.SrsAmountType = v, na.UNII);

                //YP
                //read in extentAmount.units -see SRS-374
                //need to create controlled vocabulary for validation of units as with extentAmount.type above, for now just read in value without validation
                //SRSReadingUtils.readJsonElement(jStrModGroup, "extentAmount.type", ValidatedValues.AmountTypes.Keys.Contains, v => g.Modification.Amount.SrsAmountType = v, na.UNII)


            }

            g.NucleotideSites = csites
                             .Map(s => {
                                 if (g.Modification == null) throw new SrsException("modification", String.Format("There is no specific modification, even though there are specified sites for the modification : {0}", jStrModGroup));
                                 NAFragment f = g.Modification.Fragment;
                                 if (f == null) throw new SrsException("connectors", String.Format("There is no fragment associated with modification, even though there are sites specified: {0}", jStrModGroup));
                                 if (f.Connectors == null && f.ComponentConnectors == null) throw new SrsException("connectors", String.Format("There is no connectors associated with fragment for modification, even though there are sites specified: {0}", jStrModGroup));
                                 if (f.Connectors.Count == 0 && f.ComponentConnectors.Count == 0) throw new SrsException("connectors", String.Format("There is no connectors associated with fragment for modification, even though there are sites specified: {0}", jStrModGroup));
                                 //YP SRS-394
                                 //return new NASite(state.RootObject, g.ModificationType == "NUCLEIC ACID REMOVAL" ? "MONOMER DELETION SITE" : "Nucleotide substitution site") { Subunit = s.Item1, Position = s.Item2, ConnectorRef = f.Connectors.First() };
                                 return new NASite(state.RootObject, g.ModificationType == "NUCLEIC ACID REMOVAL" ? "MONOMER DELETION SITE" : "Nucleotide substitution site") { Subunit = s.Item1, Position = s.Item2, ConnectorRef = (f.Connectors.Count != 0 ? f.Connectors.First() : f.ComponentConnectors.First()) };
                             })
                             .ToList();

            //if (g.Modification.Amount.SrsAmountType == "MOLE PERCENT")
            //{
            //    g.Modification.Amount.DivideBy100();
            //}

            ApplyAmountRules(g);

            bool isComplete = false;

            if (extent == "COMPLETE")
            {
                isComplete = true;
            }
            else if (g.Modification.Amount.Numerator == 1 && g.Modification.Amount.AmountType == AmountType.Exact && g.Modification.Amount.Unit == "mol")
            {
                isComplete = true;
            }
            else if (g.Modification.Amount.Numerator == 100 && g.Modification.Amount.Unit.ToUpper().Contains("PERCENT"))
            {
                isComplete = true;
            }
            else if (g.Modification.Amount.Numerator == 100 && (g.Modification.Amount.SrsAmountType + "").ToUpper().Contains("PERCENT"))
            {
                isComplete = true;
            }
            else if (g.Modification.Amount.NonNumericValue == "COMPLETE")
            {
                isComplete = true;
            }
            else if (g.Modification.Amount.SrsAmountType == "COMPLETE")
            {
                isComplete = true;
            }

            if (g.NucleotideSites.Count == 0)
            {

            }


            //Calculate probabilities
            if (!isComplete)
            {

                if (g.NucleotideSites.Count == 0)
                {
                    TraceUtils.WriteUNIITrace(TraceEventType.Error, na.UNII, null, "Structural Modification has non-complete amount and specifies no sites");
                }
                g.Modification.Amount.Denominator = g.NucleotideSites.Count;
                g.Modification.Amount.SrsAmountType = "PROBABILITY";

                if (g.Modification.Amount.Numerator > g.Modification.Amount.Denominator)
                {
                    TraceUtils.WriteUNIITrace(TraceEventType.Error, na.UNII, null, "Structural Modification amount numerator {0} greater than the denominator {1}", g.Modification.Amount.Numerator, g.Modification.Amount.Denominator);
                }
            }



            SRSReadingUtils.readJsonElement(jStrModGroup, "residueModified", aa => AminoAcids.IsValidAminoAcidName(aa), v => g.Nucleotide = v, na.UNII);

            if (!String.IsNullOrEmpty(g.Nucleotide) && g.NucleotideSites.Any(s => !String.Equals(AminoAcids.GetNameByLetter(s.Letter), g.Nucleotide, StringComparison.InvariantCultureIgnoreCase)))
                TraceUtils.WriteUNIITrace(TraceEventType.Error, na.UNII, null, "Residue {0} does not match all positions", g.Nucleotide);

            if (String.IsNullOrEmpty(g.Nucleotide) && g.NucleotideSites.Count == 1)
            {
                NASite site = g.NucleotideSites.First();
                g.Nucleotide = Nucleotides.GetNameByLetter(site.Letter);
                TraceUtils.WriteUNIITrace(TraceEventType.Information, na.UNII, null, "Residue restored from position: {0} => {1} => {2}", site, site.Letter, g.Nucleotide);
            }

            //if type is "LINKER"
            if (modType.Equals("LINKER"))
            {
                List<Tuple<int, int>> tlist = new List<Tuple<int, int>>();
                jStrModGroup.SelectToken("sites").ForEachWithIndex((l, i) =>
                {
                    tlist.Add(ProteinExtensions.fromJsonSite(l));
                });

                NALink ll = PolymerBaseExtensions.NAFragmentFactory.CreateLink(na, tlist, g.Modification.Fragment.UNII, state.RootObject);
                na.Links.Add(ll);
                //return null;
            }
            //end of linker stuff

            return g;
        }


        public static Tuple<int, int> fromJsonSite(JToken l)
        {
            int si = int.Parse(l.SelectToken("subunitIndex").ToString());
            int ri = int.Parse(l.SelectToken("residueIndex").ToString());

            //0-indexed
            //Note: this isn't good. Specifically because the Subunit Index for
            //the subunits isn't necessarily in the right order (it should be)
            return Tuple.Create(si - 1, ri - 1);
        }
        public static List<Tuple<int, int>> fromJsonSites(JToken l)
        {
            List<Tuple<int, int>> list = new List<Tuple<int, int>>();
            if (l != null)
            {
                l.ForEachWithIndex((s, i) =>
                {
                    list.Add(fromJsonSite(s));
                });
            }
            return list;
        }

        public static Tuple<NASubunit, int> toSiteTuple(Tuple<int, int> t, NucleicAcid na)
        {
            if (t.Item1 >= na.Subunits.Count)
                throw new SrsException("subunit_ref", string.Format("Reference to non-existing subunit: {0}", t.ToString()));
            if (t.Item2 >= na.Subunits[t.Item1].Sequence.Length)
                throw new SrsException("subunit_ref", string.Format("Reference to non-existing position: {0}", t.ToString()));
            return new Tuple<NASubunit, int>(na.Subunits[t.Item1], t.Item2);
        }

        public static NASubunit ReadSubunit(this NucleicAcid na, XElement xSubunit, PolymerBaseReadingState state)
        {
            NASubunit su = new NASubunit(state.RootObject);
            if (su.Id == null)
                TraceUtils.WriteUNIITrace(TraceEventType.Error, na.UNII, null, "Element <SUBUNIT> is not found or not interpreted: {0}", xSubunit);

            int len = 0;
            var xLen = xSubunit.Element("LENGTH");
            if (xLen != null && !String.IsNullOrEmpty(xLen.Value))
                int.TryParse(xLen.Value, out len);

            string seq = xSubunit.Element("SEQUENCE").Value.Trim()
                                                           .Replace(" ", "")
                                                           .Replace("\t", "")
                                                           .Replace("\n", "")
                                                           .Replace("\r", "");

            // TODO: I'd do sequence validation here...
            NASequence _seq = new NASequence(su.RootObject, seq);
            NASequence sequence = na.Sequences.Find(s => s.UID == _seq.UID);
            if (sequence == null)
            {
                sequence = _seq;
                na.Sequences.Add(sequence);
            }
            su.Sequence = sequence;

            if (len != su.Sequence.Length)
                TraceUtils.WriteUNIITrace(TraceEventType.Warning, na.UNII, null, "The declared length of the sequence does not match");
            return su;
        }

        public static NASubunit MakeSubunit(this NucleicAcid na, string seq, PolymerBaseReadingState state)
        {
            NASubunit su = new NASubunit(state.RootObject);

            NASequence _seq = new NASequence(su.RootObject, seq);
            NASequence sequence = na.Sequences.Find(s => s.UID == _seq.UID);
            if (sequence == null)
            {
                sequence = _seq;
                na.Sequences.Add(sequence);
            }
            su.Sequence = sequence;
            return su;
        }

        public static void MakeSugarSensitiveSequence(this NucleicAcid na, PolymerBaseReadingState state)
        {


            foreach (NASubunit su in na.Subunits)
            {
                String sugar_sensitive_sequence = "";
                for (int i = 0; i < su.Sequence.ToString().Length; i = i + 1)
                {
                    char next_character = 'X';
                    if (GetSugarAtPosition(su, i + 1) == "R")
                    {
                        next_character = char.ToLower(su.Sequence.ToString()[i]);
                    }
                    else if (GetSugarAtPosition(su, i + 1) == "dR")
                    {
                        next_character = char.ToUpper(su.Sequence.ToString()[i]);
                    }
                    else if (GetSugarAtPosition(su, i + 1) != "dR")
                    {
                        next_character = 'X';
                    }
                    if (GetLinkageAtPosition(su, i + 1) != "P")
                    {
                        next_character = 'X';
                    }
                    sugar_sensitive_sequence = sugar_sensitive_sequence + next_character;
                }
                su.SugarSensitiveSequence = new NASequence(state.RootObject, sugar_sensitive_sequence);
            }
        }

        public static NucleicAcid readFromJson(this NucleicAcid na, JObject o, SplDocument splDoc)
        {
            return readFromJson(na, o, splDoc, true, true);
        }

        public static NucleicAcid readFromJson(this NucleicAcid na, JObject o, SplDocument splDoc, Boolean canonicalize)
        {
            return readFromJson(na, o, splDoc, canonicalize, true);
        }

        //Parse a protein from a given JSON object
        public static NucleicAcid readFromJson(this NucleicAcid na, JObject o, SplDocument splDoc, Boolean canonicalize, Boolean validateLinks)
        {



            JToken jseqType = o.SelectToken("$..nucleicAcid..sequenceType");

            string seqType = null;

            if (jseqType != null)
            {
                seqType = jseqType.ToString();
            }

            PolymerBaseReadingState state = new PolymerBaseReadingState { RootObject = splDoc };

            SRSReadingUtils.readJsonElement(o, "$..nucleicAcid..sequenceType", st => ValidatedValues.SequenceTypes.Keys.Contains(st), st => na.SeqType = st, na.UNII);



            //Reference connector sites
            List<Tuple<Predicate<NAFragment>, List<Tuple<int, int>>>> fragRefCon = null;
            Optional<JToken>.ofNullable(o.SelectToken("$..notes"))
                    .ifPresent(notes => {
                        /*
                         * >  <CONNECTORS>
                            27;26
                            40;41
                         */

                        //Expected format:
                        /*
                         * FRAGMENT CONNECTORS<UNII>:
                         * 
                         * 27;26
                         * 40;41
                         * 
                         */

                        fragRefCon = notes.AsJEnumerable()
                            .Map(n => {
                                String note = null;
                                SRSReadingUtils.readJsonElement(n, "$..note", (n2) => true, v => note = v, na.UNII);
                                return note.ToUpper().Trim();
                            })
                            .Filter(n => n.StartsWith("FRAGMENT CONNECTORS<"))
                            .Select(n => {
                                String[] splitted = n.Split(':');
                                String ident = splitted[0].Split('<')[1].Split('>')[0];
                                Predicate<NAFragment> pred = (frag) => {
                                    if (frag.UNII.Equals(ident))
                                    {
                                        return true;
                                    }
                                    return false;
                                };

                                List<string> lines = new List<string>();
                                lines.Add(splitted[1]);
                                IList<int> conns = (lines)
                                            .SelectMany(s => s.Split(new char[] { ',', ';', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                                            .Select(s => int.Parse(s.Trim()))
                                            .ToList();

                                if (conns.Count % 2 != 0)
                                    TraceUtils.WriteUNIITrace(TraceEventType.Warning, na.UNII, null, "Odd connection points - only first 0, 2, 4, etc will be used");

                                List<Tuple<int, int>> cList = new List<Tuple<int, int>>();

                                for (int i = 0; i < conns.Count; i += 2)
                                {
                                    cList.Add(Tuple.Create(conns[i], conns[i + 1]));
                                }
                                return Tuple.Create(pred, cList);
                            })
                            .ToList();

                    });
            na.setNAFragmentConnectorsData(fragRefCon);

            //Agent modifications


            Optional<JToken>.ofNullable(o.SelectToken("$..agentModifications"))
                    .ifPresent(mods => {
                        mods.AsJEnumerable()
                            .Map(am => {
                                AgentModification m = new AgentModification(state.RootObject);

                                //Commented and added for Ticket 271
                                //SRSReadingUtils.readJsonElement(am, "$..agentModificationRole", ValidatedValues.AgentModificationRoles.Keys.Contains, v => m.Role = v, protein.UNII);
                                //SRSReadingUtils.readJsonElement(am, "$..agentModificationType", ValidatedValues.AgentModificationTypes.Keys.Contains, v => m.ModificationType = v, protein.UNII);
                                SRSReadingUtils.readJsonElement(am, "$..agentModificationRole", null, v => m.Role = v, na.UNII);
                                SRSReadingUtils.readJsonElement(am, "$..agentModificationType", null, v => m.ModificationType = v, na.UNII);

                                SRSReadingUtils.readJsonElement(am, "$..refPname", null, v => m.Agent = v, na.UNII);
                                SRSReadingUtils.readJsonElement(am, "$..approvalID", null, v => m.AgentId = v, na.UNII);
                                //Added for Ticket 271
                                //String AgentSubLinkID;
                                SRSReadingUtils.readJsonElement(am, "$..agentSubstance.linkingID", null, v => m.AgentSubLinkID = v, na.UNII);

                                //String RelationTypeLinkID;
                                SRSReadingUtils.readJsonElement(o, "$..relationships[?(@.type == 'STARTING MATERIAL->INGREDIENT')].relatedSubstance.linkingID", null, v => m.RelationTypeLinkID = v, na.UNII);

                                SRSReadingUtils.readJsonElement(o, "$..relationships[?(@.type == 'STARTING MATERIAL->INGREDIENT')].relatedSubstance.approvalID", null, v => m.RelationTypeApprovalID = v, na.UNII);

                                SubstanceIndexing ind = new SubstanceIndexing(ConfigurationManager.AppSettings["SubstanceIndexingDat"]);
                                // UNII|Primary Name|Hash Code|Link|SetId|Version Number|Load Time|Citation
                                // 5I9835JO3M||2841ba24-3dc4-3d7d-0743-e9c87e86e1d5|1c7a69fd-d641-48cb-8ad4-ba84025f2906|141b28a9-4ac2-4c1a-986c-7fc160bb5446|1|20140505122508|
                                if (ind != null)
                                {
                                    var ent = ind.GetExisting(na.UNII);
                                    //var ent = ind.GetExisting("5I9835JO3M");
                                    if (ent != null)
                                        m.AgentHashCode = ent.Hash.ToString();
                                }


                                if (String.IsNullOrEmpty(m.RelationTypeLinkID) || String.IsNullOrEmpty(m.RelationTypeApprovalID))
                                    throw new SrsException("mandatory_field_missing", "Type in Relationships is not STARTING MATERIAL INGREDIENT/no approvalID available for Agent Modification");

                                if (String.IsNullOrEmpty(m.AgentId))
                                    throw new SrsException("mandatory_field_missing", "Approval Id doesnot exist for Agent Modification");

                                AgentTerms at = new AgentTerms(m.Role + "###" + m.ModificationType);

                                if (at.Id == null)
                                    throw new SrsException("mandatory_field_missing", "Role/Type don't exist for Agent Modification");

                                if (at != null)
                                {
                                    if (String.IsNullOrEmpty(at.Code))
                                        throw new SrsException("mandatory_field_missing", "Code don't exist for Agent Modification");
                                    else
                                    {
                                        m.AgentCode = at.Code;
                                        m.AgentDisplayName = at.DisplayName;
                                    }

                                }
                                ////End of 271////

                                m.Amount = PolymerBaseExtensions.ReadAmountJson(na, am.SelectToken("amount"), state);
                                na.Modifications.Add(m);
                                return m;
                            });
                    });

            /*
             * 
            IEnumerable<JToken> jsubs = o.SelectTokens("$..nucleicAcid..subunits..sequence");

            foreach (JToken x in jsubs)
            {
                JToken seq = x;
                NASubunit su = na.MakeSubunit(seq.ToString(), state);
                na.Subunits.Add(su);

                //begin YB
                if (su.Sequence.ToString().Any(c => String.IsNullOrEmpty(Nucleotides.GetNameByLetter(c))))
                    TraceUtils.ReportError("seq_ref", na.UNII, "Unknown letter(s) used in sequence");
                //end YB
            }
            */



            bool isNACircular = false;
            try
            {
                List<JToken> subtypes = o.SelectToken("$..nucleicAcid..nucleicAcidSubType").ToList();

                foreach (JToken subtype in subtypes)
                {
                    if (subtype.ToString() == "CIRCULAR")
                    {
                        isNACircular = true;
                        break;
                    }
                }
            }
            catch
            {
                isNACircular = false;
            }


            IEnumerable<JToken> jsubs = o.SelectTokens("$..nucleicAcid..subunits");

            foreach (JToken subunit_token in jsubs.Children())
            {
                JToken x = subunit_token.SelectToken("sequence");
                JToken seq = x;
                NASubunit su = na.MakeSubunit(seq.ToString(), state);

                JToken amt_token = subunit_token.SelectToken("amount");
                if (amt_token != null)
                {
                    JToken amt_avg = amt_token.SelectToken("average");
                    if (amt_avg != null)
                    {
                        su.SubunitAmount.AmountType = AmountType.Statistical;
                        su.SubunitAmount.Numerator = Convert.ToDouble(amt_avg.ToString());
                    }
                    JToken amt_low = amt_token.SelectToken("low");
                    if (amt_low != null)
                    {
                        su.SubunitAmount.Low = Convert.ToDouble(amt_low.ToString());
                    }
                    JToken amt_high = amt_token.SelectToken("high");
                    if (amt_high != null)
                    {
                        su.SubunitAmount.High = Convert.ToDouble(amt_high.ToString());
                    }
                }
                su.isCircular = isNACircular;
                na.Subunits.Add(su);

                //begin YB
                if (su.Sequence.ToString().Any(c => String.IsNullOrEmpty(Nucleotides.GetNameByLetter(c))))
                    TraceUtils.ReportError("seq_ref", na.UNII, "Unknown letter(s) used in sequence");
                //end YB
            }

            //foreach (List<> linkage_tokens in o.SelectTokens("$..nucleicAcid..linkages").ToList())
            for (int i = 0; i < o.SelectTokens("$..nucleicAcid..linkages").ToList().Count; i++)
            {
                foreach (JToken linkage_token in o.SelectTokens("$..nucleicAcid..linkages").ToList()[i])
                {
                    foreach (JToken linkage_site in linkage_token.SelectTokens("$..sites").Children().ToList())
                    {
                        na.SugarLinkers_listing.Add(new Tuple<string, string>((string)linkage_token.SelectToken("$..linkage"), linkage_site["subunitIndex"].ToString() + "_" + linkage_site["residueIndex"].ToString()));
                        na.Subunits[System.Convert.ToInt32(linkage_site["subunitIndex"].ToString()) - 1].SugarLinkers.Add(new Tuple<int, String>(int.Parse(linkage_site["residueIndex"].ToString()), (string)linkage_token.SelectToken("$..linkage")));
                        //na.Subunits[System.Convert.ToInt32(linkage_site["subunitIndex"].ToString()) - 1].SugarLinkers.Add((string)linkage_token.SelectToken("$..linkage"));
                    }
                }

            }

            foreach (JToken sugar_tokens in o.SelectTokens("$..nucleicAcid..sugars").ToList())
            {
                foreach (JToken sugar_token in sugar_tokens.ToList())
                {
                    foreach (JToken sugar_site in sugar_token.SelectTokens("$..sites").Children().ToList())
                    {
                        na.Sugars_listing.Add(new Tuple<string, string>((string)sugar_token.SelectToken("$..sugar"), sugar_site["subunitIndex"].ToString() + "_" + sugar_site["residueIndex"].ToString()));
                        na.Subunits[System.Convert.ToInt32(sugar_site["subunitIndex"].ToString()) - 1].Sugars.Add(new Tuple<int, String>(int.Parse(sugar_site["residueIndex"].ToString()), (string)sugar_token.SelectToken("$..sugar")));
                        //na.Subunits[System.Convert.ToInt32(sugar_site["subunitIndex"].ToString()) - 1].Sugars.Add((string)sugar_token.SelectToken("$..sugar"));
                    }
                }
            }

            MakeSugarSensitiveSequence(na, state);

            foreach (JToken smod_tokens in o.SelectTokens("$..modifications..structuralModifications").ToList())
            {
                foreach (JToken smod_token in smod_tokens.ToList())
                {

                    JToken molecular_fragment = smod_token.SelectToken("$..molecularFragment");
                    foreach (JToken smod_site in smod_token.SelectTokens("$..sites").Children().ToList())
                    {
                        if (molecular_fragment["approvalID"] == null)
                        {
                            throw new Exception("Nucleic Acid cannot be converted. molecularFragment is missing approvalID field");
                        }
                        string smod_identifier = molecular_fragment["approvalID"].ToString();
                        na.StructuralModifications_listing.Add(new Tuple<string, string>(smod_identifier, smod_site["subunitIndex"].ToString() + "_" + smod_site["residueIndex"].ToString()));
                        na.Subunits[System.Convert.ToInt32(smod_site["subunitIndex"].ToString()) - 1].StructuralModifications.Add(new Tuple<int, String>(int.Parse(smod_site["residueIndex"].ToString()), smod_identifier));
                        //na.Subunits[System.Convert.ToInt32(smod_site["subunitIndex"].ToString()) - 1].StructuralModifications.Add(smod_identifier);
                    }
                }
            }

            // Disulfide links
            //YP comment out links for NAs
            /*
            o.SelectTokens("$..disulfideLinks..sites")
                .ForEachWithIndex((x, h) => {
                    List<Tuple<int, int>> tlist = new List<Tuple<int, int>>();

                    x.ForEachWithIndex((l, i) => {
                        tlist.Add(ProteinExtensions.fromJsonSite(l));
                    });
                    NALink li = PolymerBaseExtensions.NAFragmentFactory.CreateLink(na, tlist, "cys-cys", splDoc);
                    na.Links
                           .Add(li);
                });
            // Other links. Constructed based on the same principle as disulfide links.
            o.SelectToken("$..otherLinks").ForEachWithIndex((x, h) => {

                

                JToken ltypeJson = x.SelectToken("$.linkageType");

                if (ltypeJson == null) throw new SrsException("otherLinkType", String.Format("Other link does not specify a type : {0}", x));

                string ltype = ltypeJson.ToString();


                List<Tuple<int, int>> tlist = new List<Tuple<int, int>>();

                x.SelectToken("sites").ForEachWithIndex((l, i) => {
                    tlist.Add(ProteinExtensions.fromJsonSite(l));
                });

                NALink ll = PolymerBaseExtensions.NAFragmentFactory.CreateLink(na, tlist, ltype, splDoc);
                
                na.Links.Add(ll);
            });

            */

            Func<string, Boolean> roleValidator = ValidatedValues.NAStructureModificationTypes.Keys.Contains;

            //Reference connector sites
            List<List<Tuple<int, int>>> refCon = null;
            Optional<JToken>.ofNullable(o.SelectToken("$..notes"))
                    .ifPresent(notes => {
                        refCon = notes.AsJEnumerable()
                            .Map(n => {
                                String note = null;
                                SRSReadingUtils.readJsonElement(n, "$..note", (n2) => true, v => note = v, na.UNII);
                                return note.ToUpper().Trim();
                            })
                            .Filter(n => n.StartsWith("CONNECTORS:"))
                            .SelectMany(n => {
                                return n.Split(':')[1].Replace("\r", "")
                                               .Split('\n')
                                               .Map(c => c.Trim())
                                               .Filter(c => c.Length > 0)
                                               .Map(c => Helpers.SequencePositions(c).ToList());
                            })
                            .ToList();

                    });
            na.setConnectorsData(refCon);


            //TODO: really fix this
            // Physical modifications
            // Note that the only thing that they have is the ROLE currently
            // Parameters are NOT currently used in SRSUtil
            o.SelectTokens("$..physicalModificationRole")
                            .Map(r => r.ToString())
                            .ForEachWithIndex((r, i) => {
                                PhysicalModification m = new PhysicalModification(state.RootObject);
                                //I don't think this makes sense ... 
                                SRSReadingUtils.readSingleElement(r, "PHYSICAL_MODIFICATION_ROLE", roleValidator, v => m.Role = v, na.UNII);
                                na.Modifications.Add(m);
                            });

            /*
                        //Agent modifications
                        //Commented for Ticket 271

                        Optional<JToken>.ofNullable(o.SelectToken("$..agentModifications"))
                                .ifPresent(mods => {
                                    mods.AsJEnumerable()
                                        .Map(am => {
                                            AgentModification m = new AgentModification(state.RootObject);

                                            SRSReadingUtils.readJsonElement(am, "$..agentModificationRole", ValidatedValues.AgentModificationRoles.Keys.Contains, v => m.Role = v, protein.UNII);
                                            SRSReadingUtils.readJsonElement(am, "$..agentModificationType", ValidatedValues.AgentModificationTypes.Keys.Contains, v => m.ModificationType = v, protein.UNII);
                                            SRSReadingUtils.readJsonElement(am, "$..refPname", null, v => m.Agent = v, protein.UNII);
                                            SRSReadingUtils.readJsonElement(am, "$..approvalID", null, v => m.AgentId = v, protein.UNII);
                                            if (String.IsNullOrEmpty(m.AgentId))
                                                throw new SrsException("general_error", "Approval Id doesnot exist for Agent Modification");
                                            m.Amount = PolymerBaseExtensions.ReadAmountJson(protein, am.SelectToken("amount"), state);
                                            protein.Modifications.Add(m);
                                            return m;
                                        });
                                });
            */

            /* YP Commenting this "properties" block as many structures don't contain it
             * Should really error it out so it can be fixed, but for testing purposes it can be commented
             * 
            Optional<JToken>.ofNullable(o.SelectToken("properties"))
                .map(t => t.AsJEnumerable()
                         .Filter(v => v.SelectToken("name").ToString().Contains("MOL_WEIGHT"))
                         .First())
                .ifPresent(jmw => {
                                //TODO: This is really due to improper import of old SRS records into
                                //GSRS. This should remain supported, but be updated in the future to
                                //use parameters of the property.

                                string mwname = jmw.SelectToken("name").ToString();

                    MolecularWeight mw = new MolecularWeight();
                    mw.Amount = PolymerBaseExtensions.ReadAmountJson(na, jmw.SelectToken("value"), state);


                    string mwcolon = Optional<JToken>.ofNullable(jmw.SelectToken("value.type"))
                                                    .map(v => v.ToString())
                                                    .orElse(null);




                    string mwparan = null;

                                //read from property value
                                if (mwcolon == null || !mwcolon.Contains("(")) {
                        string[] split = mwname.Split(new char[] { ':' }, 2);
                        if (split.Length > 1) {
                            string[] split2 = split[1].Split(new char[] { '(' }, 2);
                            string mwcolon1 = split2[0].Trim();
                            if (split2.Length > 1) {
                                mwparan = split2[1].Trim().Replace(")", "");
                                mwcolon = split2[0].Trim();
                            }
                        }
                        if (mwparan == null) {
                            string[] split2 = mwname.Split(new char[] { '(' }, 2);
                            if (split2.Length > 1) {
                                mwparan = split2[1].Replace(")", "").Trim();
                            }
                        }
                    }

                                //mw.WeightType = mwcolon;
                                //mw.WeightMethod = mwparan;
                                SRSReadingUtils.readSingleElement(mwcolon, "MOLECULAR_WEIGHT_TYPE", ValidatedValues.MWTypes.Keys.Contains, v => mw.WeightType = v, na.UNII);
                    SRSReadingUtils.readSingleElement(mwparan, "MOLECULAR_WEIGHT_METHOD", ValidatedValues.MWMethods.Keys.Contains, v => mw.WeightMethod = v, na.UNII);

                    if (mw.WeightMethod != null
                       || mw.WeightType != null
                       || mw.Amount.Low != null
                       || mw.Amount.High != null
                       || mw.Amount.Numerator != null
                       || mw.Amount.NonNumericValue != null) {

                        if (mw.Amount.Unit == null || mw.Amount.Unit.Equals("mol")) {
                            mw.Amount.Unit = "DA";
                        }
                        if (mw.Amount.DenominatorUnit == null || mw.Amount.DenominatorUnit.Equals("mol")) {
                            mw.Amount.DenominatorUnit = "1";
                        }
                        na.MolecularWeight = mw;
                    }
                });
                */
            Optional<JToken>.ofNullable(o.SelectToken("$..structuralModifications"))
                    .ifPresent(mods => {
                        mods.AsJEnumerable()
                            .ForEachWithIndex((sm, i) => {
                                var g = na.ReadNAStructuralModificationJson(sm, state);
                                na.ProcessStructuralModificationGroup(g, state);
                            });
                    });

            // Check that all links have associated fragments and all connection points are defined for all fragments
            if (validateLinks)
            {
                foreach (var link in na.Links.Where(l => l.LinkType != "cys-cys"))
                {
                    if (link.Sites.Count() == 0)
                    {
                        throw new SrsException("linkMissingSites", "Link from OTHER_LINKAGE has no sites specified.");
                    }
                    else if (link.Sites.Count() == 1)
                    {
                        throw new SrsException("linkMissingSites", "Link from OTHER_LINKAGE has only 1 site specified, must have at least 2.");
                    }
                    if (link.Linker == null)
                        throw new SrsException("linkModification", "Link from OTHER_LINKAGE has no fragment associated/resolved. There is no adequately defined structural modification for the same sites.");
                    if (link.Linker.Connectors.Count != link.Sites.Count)
                        throw new SrsException("linkSites", "Not all link sites from OTHER_LINKAGE have connection points for fragment defined");
                    if (!link.IsCompletelyDefined)
                        throw new SrsException("linkDefined", "Link from OTHER_LINKAGE is not completely defined");


                    na.reorderNASites(link.Sites)
                           .ForEachWithIndex((s, i) => {
                               s.ConnectorRef = link.Linker.Connectors[i];
                           });

                    bool needsReorder = false;

                    link.Sites.ForEachWithIndex((s, i) => {
                        if (!s.ConnectorRef.CanUseNucleotide(s.Letter + ""))
                        {
                            needsReorder = true;
                            TraceUtils.WriteUNIITrace(TraceEventType.Warning, na.UNII, null, "Connector {0} can not use residue {1}. Will reshuffle", i, s.Letter);
                        }
                    });


                    if (needsReorder)
                    {

                        IList<String> rlist = link.Sites.Select(s => s.Letter + "").ToList();

                        List<NAFragment.Connector> oldCons = link.Linker.Connectors.Select(c => c).ToList();
                        List<NAFragment.Connector> newCons = new List<NAFragment.Connector>();

                        for (int i = 0; i < rlist.Count; i++)
                        {
                            NAFragment.Connector fcon = oldCons[0];
                            IList<NAFragment.Connector> fcons = oldCons.Filter(o1 => o1.CanUseNucleotide(rlist[i])).ToList();
                            if (fcons.Count > 0)
                            {
                                fcon = fcons[0];
                            }

                            newCons.Add(fcon);
                            oldCons.Remove(fcon);
                        }
                        link.Linker.Connectors = newCons;
                        //Order them
                        link.Sites.ForEachWithIndex((s, i) => {
                            s.ConnectorRef = link.Linker.Connectors[i];
                        });
                    }




                    // and register Fragment in an enclosing container (protein)
                    na.RegisterFragment(link.Linker);
                }
            }

            ProcessImplicitModifications(na, state);

            if (canonicalize)
            {
                na.Canonicalize();
            }

            if (na.isSingleMoietySubstance)
            {
                na.Subunits[0].isTheOnlyMoiety = true;
                na.Subunits[0].parent_unii = na.UNII;
            }


            return na;

        }

        public static Tuple<string, string> GetListing(int subunit, int position, List<Tuple<String, String>> listings)
        {
            Tuple<string, string> returnvalue = null;
            foreach (Tuple<string, string> listing in listings)
            {
                if (listing.Item2 == subunit.ToString() + "_" + position.ToString())
                {
                    returnvalue = listing;
                    break;
                }
            }
            return returnvalue;
        }

        
        private static PartiallyConnectedLink GetPartiallyConnectedLink(NALink link)
        {
            for (int i = 0; i < partially_connected_links.Count; i += 1)
            {
                foreach (NASite pcl_site in partially_connected_links[i].original_link.Sites)
                {
                    foreach (NASite site in link.Sites)
                    {
                        if (pcl_site.ToString() == site.ToString())
                        {
                            return partially_connected_links[i];
                        }

                    }
                }
            }
            return null;
        }
        
        /*
        private static bool PartiallyConnectedLinkExists(NALink link)
        {

            for (int i = 0; i < partially_connected_links.Count; i += 1)
            {
                foreach (NASite pcl_site in partially_connected_links[i].Item1.Sites)
                {
                    foreach (NASite site in link.Sites)
                    {
                        if (pcl_site.ToString() == site.ToString())
                        {
                            return true;
                        }

                    }
                }
            }
            return false;
        }
        */

        private static bool PartiallyConnectedLinkExists(NALink link)
        {

            for (int i = 0; i < partially_connected_links.Count; i += 1)
            {
                foreach (NASite pcl_site in partially_connected_links[i].original_link.Sites)
                {
                    foreach (NASite site in link.Sites)
                    {
                        if (pcl_site.ToString() == site.ToString())
                        {
                            return true;
                        }

                    }
                }
            }
            return false;
        }
        private static NALink GetNALink(NucleicAcid na, int subunit, int position)
        {
            foreach (NALink link in na.Links)
            {
                for (int i = 0; i < link.Sites.Count; i += 1)
                {
                    if (link.Sites[i].Subunit.Ordinal == subunit && link.Sites[i].Position == position)
                    {
                        return link;
                    }
                }
            }
            return null;
        }

        private static bool IsLink(NucleicAcid na, int subunit, int position)
        {
            foreach (NALink link in na.Links)
            {
                for (int i = 0; i < link.Sites.Count; i += 1)
                {
                    if (link.Sites[i].Subunit.Ordinal == subunit && link.Sites[i].Position == position)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private static bool PartiallyConnectedLinkExists(NucleicAcid na, int subunit, int position)
        {
            foreach (NALink link in na.Links)
            {
                for (int i = 0; i < link.Sites.Count; i += 1)
                {
                    if (link.Sites[i].Subunit.Ordinal == subunit && link.Sites[i].Position == position)
                    {
                        if (PartiallyConnectedLinkExists(link))
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        public static NAFragment GetStructuralModificationFragment(NucleicAcid na, int subunit, int position, List<string> modification_types_criteria)
        {
            NAFragment returnvalue = null;
            foreach (NALink link in na.Links)
            {
                //the implicit check is needed to make links that are implicit (already built into the chain components and aren't explicitly mentioned in JSON (e.g. circular links)) are not picked up by this
                if (!link.IsImplicit)
                {
                    for (int i = 0; i < link.Sites.Count; i += 1)
                    {
                        if (link.Sites[i].Subunit.Ordinal == subunit && link.Sites[i].Position == position)
                        {
                            returnvalue = link.Linker;
                            //if (!PartiallyConnectedLinkExists(link))
                            //{
                            //    partially_connected_links.Add(new Tuple<NALink, Tuple<int, int>>(link, new Tuple<int, int>(-1, -1)));
                            //}
                            break;
                        }
                    }
                }
            }
            foreach (NAStructuralModificationGroup smod in na.NAModifications)
            //for (int i = 0; i < na.NAModifications.Count; i += 1)
            {
                for (int i = 0; i < smod.NucleotideSites.Count; i += 1)
                {
                    if (smod.NucleotideSites[i].Subunit.Ordinal == subunit && smod.NucleotideSites[i].Position == position && modification_types_criteria.Contains(smod.Modification.ModificationType))
                    {
                        returnvalue = smod.Modification.Fragment;
                        break;
                    }
                }
            }

            return returnvalue;
        }

        public class PNAComponent
        {
            public string sym;
            public NAFragment Fragment;

            private int addCount = 0;
            private int offset = 0;
            private List<int> removed = new List<int>();

            public Boolean isMoiety = false;

            private string _mol;





            public static PNAComponent from(String s, PolymerBaseReadingState state)
            {
                PNAComponent pac = new PNAComponent { sym = s };

                //YP SRS-395
                /*
                 * string dictionary_contents = File.ReadAllText("NA_Fragments_dictionary.txt");
                Dictionary<string, string> aadict = dictionary_contents.Replace("\r", "")
               .Split('\n')
               .ToDictionary(l => l.Split('\t')[0], l => l.Split('\t')[1]);
                */
                /*
                Dictionary<string, string> aadict = @"cEtR	FKUNCETR
aFR	FKUNAFRX
eR	FKUNERXX
hx	FKUNHXXX
5cGT	FKUN5CGT
FR	FKUNFRXX
4sR	FKUN4SRX
3SS6	FKUN3SS6
5FAM	FKUN5FAM
SGNA	FKUNSGNA
12ddR	FKUN12DD
LR	FKUNLRXX
am12	FKUNAM12
fR	FKUNFRXX
qR	FKUNQRXX
am6	FKUNAM6X
3A6	FKUN3A6X
RGNA	FKUNRGNA
PONA	FKUNPONA
5A6	FKUN5A6X
3FAM	FKUN3FAM
mph	FKUNMPHX
MOE	FKUNMOEX
dR	FKUNDRXX
R	FKUNRXXX
LR2	FKUNLR2X
aR	FKUNARXX
mR	FKUNMRXX
25R	FKUN25RX
FMOE	FKUNFMOE
UNA	FKUNUNAX
lLR	FKUNLLRX
5FBC6	FKUN5FBC
L-DEOXYRIBOSE	FKUNL-DE
dier	FKUNDIER
sP	FKUNSPXX
P	FKUNPXXX
bP	FKUNBPXX
nasP	FKUNNASP
ccnP	FKUNCCNP
pP	FKUNPPXX
naP	FKUNNAPX
G	FKUNGXXX
T	FKUNTXXX
U	FKUNUXXX
A	FKUNAXXX
C	FKUNCXXX
P-ETHOXY	FKUNPETH".Replace("\r", "")
               .Split('\n')
               .ToDictionary(l => l.Split('\t')[0], l => l.Split('\t')[1]);
                */
                Dictionary<string, string> nadict = PolymerBaseExtensions.NAFragmentFactory.components_dictionary;
                string naunii = nadict.TryGetValue(s, out naunii) ? naunii : null;

                if (naunii != null)
                {
                    pac.Fragment = FragmentResolve(naunii, state);

                    if (s.Equals(s.ToLower()))
                    {
                        if (pac.Fragment != null)
                        {
                            SDFUtil.IMolecule m = pac.Fragment.Molecule;
                            IndigoObject io = getIndigo().loadMolecule(m.Mol);
                            foreach (IndigoObject matom in io.iterateStereocenters())
                            {
                                //matom.
                                matom.invertStereo();
                            }
                            pac._mol = io.molfile();

                        }
                    }
                }


                return pac;
            }

            public string getMolfile()
            {
                if (!this.alreadyAdded())
                {
                    if (_mol != null) return _mol;
                    return Fragment.Molecule.Mol;
                }
                else
                {
                    Indigo ind = new Indigo();
                    IndigoObject io = ind.loadMolecule((_mol != null) ? _mol : Fragment.Molecule.Mol);

                    //hopefully removes bonds too
                    io.removeAtoms(removed);
                    io = ind.loadMolecule(io.molfile());
                    return io.molfile();
                }
            }

            public bool mergeMolecule(IndigoObject mol)
            {
                if (this.alreadyAdded()) return false;
                IndigoObject mol2 = getIndigo().loadMolecule(getMolfile());
                mol.merge(mol2);
                return true;
            }

            public Tuple<int, int> getPosition()
            {
                return getPosition(this.offset);
            }
            public Tuple<int, int> getPosition(int poffset)
            {
                if (this.alreadyAdded())
                {
                    poffset = offset;
                }
                Tuple<int, int> tup1 = Fragment.Connectors[this.addCount].Snip;


                Tuple<int, int> tup = Tuple.Create(tup1.Item1 + poffset, tup1.Item2 + poffset);

                removed.ForEach((ri) => {
                    //adjust the position
                    if (ri < tup.Item1)
                    {
                        tup = Tuple.Create(tup.Item1 - 1, tup.Item2);
                    }

                    if (ri < tup.Item2)
                    {
                        tup = Tuple.Create(tup.Item1, tup.Item2 - 1);
                    }
                });

                if (tup1.Item1 == 0)
                {
                    tup = Tuple.Create(0, tup.Item2);
                }
                if (tup1.Item2 == 0)
                {
                    tup = Tuple.Create(tup.Item1, 0);
                }

                return tup;
            }

            public Boolean alreadyAdded()
            {
                return addCount > 0;
            }

            public Boolean isLinker()
            {
                return Fragment.Connectors.Count() > 1;
            }


            public void incrementAdded(int os)
            {
                addCount++;
                this.offset = os;
            }

            public void markRemoved(int r)
            {
                this.removed.Add(r);
            }




        }

        public class PNucleotide
        {
            public string sym;
            public NAFragment Fragment;

            private int addCount = 0;
            private int offset = 0;
            private List<int> removed = new List<int>();

            public Boolean isMoiety = false;

            private string _mol;





            public static PNucleotide from(String s, PolymerBaseReadingState state)
            {
                PNucleotide pac = new PNucleotide { sym = s };

                Dictionary<string, string> nadict = @"A	FKUNIIDAPP
T	FKUNIIDTPP
C	FKUNIIDCPP
G	FKUNIIDGPP
U	FKUNIIDUPP
a	FKUNIIRAPP
t	FKUNIIRTPP
c	FKUNIIRCPP
g	FKUNIIRGPP
u	FKUNIIRUPP
A_NOP	FKUNIIDANP
T_NOP	FKUNIIDTNP
C_NOP	FKUNIIDCNP
G_NOP	FKUNIIDGNP
U_NOP	FKUNIIDUNP
a_nop	FKUNIIRANP
t_nop	FKUNIIRTNP
c_nop	FKUNIIRCNP
g_nop	FKUNIIRGNP
u_nop	FKUNIIRUNP".Replace("\r", "")
               .Split('\n')
               .ToDictionary(l => l.Split('\t')[0], l => l.Split('\t')[1]);
                string naunii = nadict.TryGetValue(s, out naunii) ? naunii : null;

                if (naunii != null)
                {
                    pac.Fragment = FragmentResolve(naunii, state);

                    if (s.Equals(s.ToLower()))
                    {
                        if (pac.Fragment != null)
                        {
                            SDFUtil.IMolecule m = pac.Fragment.Molecule;
                            IndigoObject io = getIndigo().loadMolecule(m.Mol);
                            foreach (IndigoObject matom in io.iterateStereocenters())
                            {
                                //matom.
                                matom.invertStereo();
                            }
                            pac._mol = io.molfile();

                        }
                    }
                }


                return pac;
            }

            public string getMolfile()
            {
                if (!this.alreadyAdded())
                {
                    if (_mol != null) return _mol;
                    return Fragment.Molecule.Mol;
                }
                else
                {
                    Indigo ind = new Indigo();
                    IndigoObject io = ind.loadMolecule((_mol != null) ? _mol : Fragment.Molecule.Mol);

                    //hopefully removes bonds too
                    io.removeAtoms(removed);
                    io = ind.loadMolecule(io.molfile());
                    return io.molfile();
                }
            }

            public bool mergeMolecule(IndigoObject mol)
            {
                if (this.alreadyAdded()) return false;
                IndigoObject mol2 = getIndigo().loadMolecule(getMolfile());
                mol.merge(mol2);
                return true;
            }

            public Tuple<int, int> getPosition()
            {
                return getPosition(this.offset);
            }
            public Tuple<int, int> getPosition(int poffset)
            {
                if (this.alreadyAdded())
                {
                    poffset = offset;
                }
                Tuple<int, int> tup1 = Fragment.Connectors[this.addCount].Snip;


                Tuple<int, int> tup = Tuple.Create(tup1.Item1 + poffset, tup1.Item2 + poffset);

                removed.ForEach((ri) => {
                    //adjust the position
                    if (ri < tup.Item1)
                    {
                        tup = Tuple.Create(tup.Item1 - 1, tup.Item2);
                    }

                    if (ri < tup.Item2)
                    {
                        tup = Tuple.Create(tup.Item1, tup.Item2 - 1);
                    }
                });

                if (tup1.Item1 == 0)
                {
                    tup = Tuple.Create(0, tup.Item2);
                }
                if (tup1.Item2 == 0)
                {
                    tup = Tuple.Create(tup.Item1, 0);
                }

                return tup;
            }

            public Boolean alreadyAdded()
            {
                return addCount > 0;
            }

            public Boolean isLinker()
            {
                return Fragment.Connectors.Count() > 1;
            }


            public void incrementAdded(int os)
            {
                addCount++;
                this.offset = os;
            }

            public void markRemoved(int r)
            {
                this.removed.Add(r);
            }




        }

        public class NANucleobase
        {
            public PNAComponent component;
            public IndigoObject molecule;
            public IndigoObject sugar_connecting_star_atom;
            public int sugar_connecting_star_atom_idx;

            public NANucleobase(PNAComponent component_in)
            {
                component = component_in;
                
                molecule = getIndigo().loadQueryMolecule(component.Fragment.Molecule.Mol);
                sugar_connecting_star_atom_idx = component.Fragment.getStarAtomIndex("_R90") - 1;
                sugar_connecting_star_atom = molecule.getAtom(sugar_connecting_star_atom_idx);
            }
        }

        public class NAModNucleobase
        {
            public IndigoObject molecule;
            public IndigoObject sugar_connecting_atom;
            public int sugar_connecting_atom_idx;

            public NAModNucleobase(NAStructuralModification modification_in)
            {
                molecule = getIndigo().loadQueryMolecule(modification_in.Fragment.Molecule.Mol);
                sugar_connecting_atom_idx = modification_in.Fragment.ComponentConnectors[0].Snip.Item1 - 1;
                sugar_connecting_atom = molecule.getAtom(sugar_connecting_atom_idx);
            }
        }

        public class NASugar
        {
            public PNAComponent component;
            public IndigoObject molecule;
            public IndigoObject base_connecting_star_atom;
            public IndigoObject five_prime_star_atom;
            public IndigoObject three_prime_star_atom;
            public int base_connecting_star_atom_idx;
            public int five_prime_star_atom_idx;
            public int three_prime_star_atom_idx;

            public NASugar(PNAComponent component_in)
            {
                component = component_in;
                molecule = getIndigo().loadQueryMolecule(component.Fragment.Molecule.Mol);
                base_connecting_star_atom_idx = component.Fragment.getStarAtomIndex("_R90") - 1;
                five_prime_star_atom_idx = component.Fragment.getStarAtomIndex("_R91") - 1;
                three_prime_star_atom_idx = component.Fragment.getStarAtomIndex("_R92") - 1;
                base_connecting_star_atom = molecule.getAtom(base_connecting_star_atom_idx);
                five_prime_star_atom = molecule.getAtom(five_prime_star_atom_idx);
                three_prime_star_atom = molecule.getAtom(three_prime_star_atom_idx);
            }
        }

        public class NASugarLinker
        {
            public PNAComponent component;
            public IndigoObject molecule;
            public IndigoObject five_prime_connecting_star_atom;
            public IndigoObject three_prime_connecting_star_atom;
            public int five_prime_connecting_star_atom_idx;
            public int three_prime_connecting_star_atom_idx;

            public NASugarLinker(PNAComponent component_in)
            {
                component = component_in;
                molecule = getIndigo().loadQueryMolecule(component.Fragment.Molecule.Mol);
                five_prime_connecting_star_atom_idx = component.Fragment.getStarAtomIndex("_R91") - 1;
                three_prime_connecting_star_atom_idx = component.Fragment.getStarAtomIndex("_R92") - 1;
                five_prime_connecting_star_atom = molecule.getAtom(five_prime_connecting_star_atom_idx);
                three_prime_connecting_star_atom = molecule.getAtom(three_prime_connecting_star_atom_idx);
            }
        }

        public class Nucleoside
        {
            public NANucleobase nucleobase;
            public NAModNucleobase mod_nucleobase;
            public NASugar sugar;
            public IndigoObject molecule;
            public IndigoObject five_prime_star_atom;
            public IndigoObject three_prime_star_atom;
            public int five_prime_star_atom_idx;
            public int three_prime_star_atom_idx;
            
            public Nucleoside(NAFragment nucleoside_smod_fragment)
            {
                //found nucleoside modification, need to attach
                molecule = getIndigo().loadQueryMolecule(nucleoside_smod_fragment.Molecule.Mol);

                //assuming the order of connectors 5', 3'
                //designate the attachment points

                //create the linker atoms from the connectors specified in the molfile
                int three_prime_index = nucleoside_smod_fragment.Connectors[0].Snip.Item2 - 1;
                int five_prime_index = nucleoside_smod_fragment.Connectors[0].Snip.Item1 - 1;
                if (three_prime_index == -1 || five_prime_index == -1)
                {
                    throw new Exception("Nucleic Acid cannot be converted. Missing a connector on fragment " + nucleoside_smod_fragment.Id);
                }
                IndigoObject nucleoside_three_prime_atom = molecule.getAtom(three_prime_index);
                IndigoObject nucleoside_five_prime_atom = molecule.getAtom(five_prime_index);

                //adding an "attachment" or "star" atom R to the 3' and 5' connection atoms to conform to the way nucleotides/sides and their components are assembled
                five_prime_star_atom = molecule.addAtom("R");
                nucleoside_five_prime_atom.addBond(five_prime_star_atom, 1);
                five_prime_star_atom_idx = five_prime_star_atom.index();

                three_prime_star_atom = molecule.addAtom("R");
                nucleoside_three_prime_atom.addBond(three_prime_star_atom, 1);
                three_prime_star_atom_idx = three_prime_star_atom.index();

            }

            public Nucleoside(IndigoObject molecule_in, int three_prime_star_atom_idx_in, int five_prime_star_atom_idx_in)
            {
                molecule = molecule_in.clone();
                three_prime_star_atom_idx = three_prime_star_atom_idx_in;
                three_prime_star_atom = molecule.getAtom(three_prime_star_atom_idx);
                five_prime_star_atom_idx = five_prime_star_atom_idx_in;
                five_prime_star_atom = molecule.getAtom(five_prime_star_atom_idx);
            }

            public Nucleoside (NANucleobase nucleobase_in, NASugar sugar_in)
            {
                nucleobase = nucleobase_in;
                sugar = sugar_in;


                IndigoObject base_component_mol = nucleobase.molecule;
                IndigoObject sugar_component_mol = sugar.molecule;

                //connect base and sugar at _R90
                int base_r90_star_atom_idx = nucleobase.sugar_connecting_star_atom_idx;
                int sugar_r90_star_atom_idx = sugar.base_connecting_star_atom_idx;
                IndigoObject sugar_r90_star_atom = sugar.base_connecting_star_atom;
                IndigoObject base_r90_star_atom = nucleobase.sugar_connecting_star_atom;

                int sugar_5prime_star_atom_index = sugar.five_prime_star_atom_idx;
                int sugar_3prime_star_atom_index = sugar.three_prime_star_atom_idx;
                IndigoObject sugar_3prime_star_atom = sugar.three_prime_star_atom;

                //both sugar and base have a star atom
                //we need to remove both star atoms and their bonds
                //if one of the deleted bonds is a stereo bond, we need to remember it and restore it
                //first look at the base star atom and its bond to parent
                int base_atom_that_connects_sugar_idx = -1;
                Tuple<int, bool> remembered_bond_stereo = new Tuple<int, bool>(0, false);
                foreach (IndigoObject base_r90_star_atom_parent in nucleobase.sugar_connecting_star_atom.iterateNeighbors())
                {
                    base_atom_that_connects_sugar_idx = base_r90_star_atom_parent.index();
                    IndigoObject base_r90_star_atom_parent_bond = base_r90_star_atom_parent.bond();
                    if (base_r90_star_atom_parent_bond.bondStereo() != 0)
                    {
                        remembered_bond_stereo = RememberStereo(sugar.molecule, base_atom_that_connects_sugar_idx + 1, nucleobase.sugar_connecting_star_atom_idx + 1);
                    }
                    //base_r90_star_atom_parent_bond.remove();
                    nucleobase.molecule.getBond(base_r90_star_atom_parent_bond.index()).remove();
                    nucleobase.molecule.getAtom(nucleobase.sugar_connecting_star_atom.index()).remove();
                    //nucleobase.sugar_connecting_star_atom.remove();
                    break;
                }

                //now look at the sugar star atom and its bond to parent
                int sugar_atom_that_connects_base_idx = -1;
                foreach (IndigoObject sugar_r90_star_atom_parent in sugar.base_connecting_star_atom.iterateNeighbors())
                {
                    sugar_atom_that_connects_base_idx = sugar_r90_star_atom_parent.index();
                    IndigoObject sugar_r90_star_atom_parent_bond = sugar_r90_star_atom_parent.bond();
                    if (sugar_r90_star_atom_parent_bond.bondStereo() != 0)
                    {
                        remembered_bond_stereo = RememberStereo(sugar.molecule, sugar_atom_that_connects_base_idx + 1, sugar.base_connecting_star_atom_idx + 1);
                    }
                    //sugar_r90_star_atom_parent_bond.remove();
                    sugar.molecule.getBond(sugar_r90_star_atom_parent_bond.index()).remove();
                    sugar.molecule.getAtom(sugar.base_connecting_star_atom.index()).remove();
                    //sugar.base_connecting_star_atom.remove();
                    break;
                }
                IndigoObject base_sugar_mapping = nucleobase.molecule.merge(sugar.molecule);
                base_sugar_mapping.mapAtom(sugar.molecule.getAtom(sugar_atom_that_connects_base_idx)).addBond(nucleobase.molecule.getAtom(base_atom_that_connects_sugar_idx), 1);
                //remember stereo of all the bonds before the layout generation
                List<Tuple<int, int>> base_component_mol_bonds_before_layout = GetBondsStereo(nucleobase.molecule);
                nucleobase.molecule.layout();
                //get the stereo of all the bonds after layout generation
                List<Tuple<int, int>> base_component_mol_bonds_after_layout = GetBondsStereo(nucleobase.molecule);

                //see if the stereo has inverted
                bool stereo_has_inverted = InvertedStereo(base_component_mol_bonds_before_layout, base_component_mol_bonds_after_layout);

                //restore stereo if necessary
                nucleobase.molecule = RestoreStereoBond(nucleobase.molecule, base_sugar_mapping.mapAtom(sugar.molecule.getAtom(sugar_atom_that_connects_base_idx)).index() + 1, base_atom_that_connects_sugar_idx + 1, remembered_bond_stereo.Item2, remembered_bond_stereo.Item1, stereo_has_inverted);

                five_prime_star_atom = base_sugar_mapping.mapAtom(sugar.five_prime_star_atom);
                five_prime_star_atom_idx = five_prime_star_atom.index();
                three_prime_star_atom = base_sugar_mapping.mapAtom(sugar.three_prime_star_atom);
                three_prime_star_atom_idx = three_prime_star_atom.index();

                //this now becomes the molecule of the nucleotide
                molecule = nucleobase.molecule;

                //restoring the nucleobase to its original
                nucleobase = nucleobase_in;

            }

            //YP SRS-394 This is to allow assembly of a nucleotide from a nucleobase that's not a stadard NA component with star atoms but a modification that doesn't have star atoms
            public Nucleoside(NAModNucleobase mod_nucleobase_in, NASugar sugar_in)
            {
                mod_nucleobase = mod_nucleobase_in;
                sugar = sugar_in;

                IndigoObject base_component_mol = mod_nucleobase.molecule;
                IndigoObject sugar_component_mol = sugar.molecule;

                //connect base and sugar at _R90
                int base_sugar_connecting_atom_idx = mod_nucleobase.sugar_connecting_atom_idx;
                int sugar_r90_star_atom_idx = sugar.base_connecting_star_atom_idx;
                IndigoObject sugar_r90_star_atom = sugar.base_connecting_star_atom;

                int sugar_5prime_star_atom_index = sugar.five_prime_star_atom_idx;
                int sugar_3prime_star_atom_index = sugar.three_prime_star_atom_idx;
                IndigoObject sugar_3prime_star_atom = sugar.three_prime_star_atom;


                //only sugar has a star atom
                //we need to remove both star atoms and their bonds
                //if sugar star atom bond is a stereo bond, we need to remember it and restore it
                int base_atom_that_connects_sugar_idx = base_sugar_connecting_atom_idx;
                Tuple<int, bool> remembered_bond_stereo = new Tuple<int, bool>(0, false);
                
                //now look at the sugar star atom and its bond to parent
                int sugar_atom_that_connects_base_idx = -1;
                foreach (IndigoObject sugar_r90_star_atom_parent in sugar.base_connecting_star_atom.iterateNeighbors())
                {
                    sugar_atom_that_connects_base_idx = sugar_r90_star_atom_parent.index();
                    IndigoObject sugar_r90_star_atom_parent_bond = sugar_r90_star_atom_parent.bond();
                    if (sugar_r90_star_atom_parent_bond.bondStereo() != 0)
                    {
                        remembered_bond_stereo = RememberStereo(sugar.molecule, sugar_atom_that_connects_base_idx + 1, sugar.base_connecting_star_atom_idx + 1);
                    }
                    //sugar_r90_star_atom_parent_bond.remove();
                    sugar.molecule.getBond(sugar_r90_star_atom_parent_bond.index()).remove();
                    sugar.molecule.getAtom(sugar.base_connecting_star_atom.index()).remove();
                    //sugar.base_connecting_star_atom.remove();
                    break;
                }
                IndigoObject base_sugar_mapping = mod_nucleobase.molecule.merge(sugar.molecule);
                base_sugar_mapping.mapAtom(sugar.molecule.getAtom(sugar_atom_that_connects_base_idx)).addBond(mod_nucleobase.molecule.getAtom(base_atom_that_connects_sugar_idx), 1);

                
                //remember stereo of all the bonds before the layout generation
                List<Tuple<int, int>> base_component_mol_bonds_before_layout = GetBondsStereo(mod_nucleobase.molecule);
                mod_nucleobase.molecule.layout();
                //get the stereo of all the bonds after layout generation
                List<Tuple<int, int>> base_component_mol_bonds_after_layout = GetBondsStereo(mod_nucleobase.molecule);

                //see if the stereo has inverted
                bool stereo_has_inverted = InvertedStereo(base_component_mol_bonds_before_layout, base_component_mol_bonds_after_layout);

                //restore stereo if necessary
                mod_nucleobase.molecule = RestoreStereoBond(mod_nucleobase.molecule, base_sugar_mapping.mapAtom(sugar.molecule.getAtom(sugar_atom_that_connects_base_idx)).index() + 1, base_atom_that_connects_sugar_idx + 1, remembered_bond_stereo.Item2, remembered_bond_stereo.Item1, stereo_has_inverted);

                //YP SRS-394 try to increase the charge of connecting atom to see if it resolves bad bond order
                //mod_nucleobase.molecule.getAtom(base_atom_that_connects_sugar_idx).setCharge(1);

                five_prime_star_atom = base_sugar_mapping.mapAtom(sugar.five_prime_star_atom);
                five_prime_star_atom_idx = five_prime_star_atom.index();
                three_prime_star_atom = base_sugar_mapping.mapAtom(sugar.three_prime_star_atom);
                three_prime_star_atom_idx = three_prime_star_atom.index();

                //this now becomes the molecule of the nucleotide
                molecule = mod_nucleobase.molecule;

                //restoring the nucleobase to its original
                mod_nucleobase = mod_nucleobase_in;

            }
            public Nucleoside clone()
            {
                return new Nucleoside(molecule, three_prime_star_atom_idx, five_prime_star_atom_idx);
            }
        }

        public class Nucleotide
        {
            public Nucleoside nucleoside;
            public NASugarLinker sugar_linker;
            public IndigoObject molecule;
            public IndigoObject three_prime_star_atom;
            public IndigoObject linker_star_atom;
            public int linker_star_atom_idx;
            public int three_prime_star_atom_idx;

            public void Orient(int five_prime_index, int three_prime_index)
            {

                //while ((Math.Abs(this.molecule.getAtom(five_prime_index).xyz()[1] - molecule.getAtom(three_prime_index).xyz()[1]) > 0.5) || (this.molecule.getAtom(five_prime_index).xyz()[0]< molecule.getAtom(three_prime_index).xyz()[0]))
                //{
                //    molecule = SDFUtil.Rotate2DMolecule(getIndigo(), molecule, 3);
                //}
                while ((Math.Abs(this.molecule.getAtom(five_prime_index).xyz()[1] - molecule.getAtom(three_prime_index).xyz()[1]) > 0.5) || (this.molecule.getAtom(five_prime_index).xyz()[0] > molecule.getAtom(three_prime_index).xyz()[0]))
                {
                    molecule = SDFUtil.Rotate2DMolecule(getIndigo(), molecule, 3);
                }
                
            }
            public Nucleotide() {  }

            public Nucleotide(NAFragment nucleotide_smod_fragment)
            {
                molecule = getIndigo().loadQueryMolecule(nucleotide_smod_fragment.Molecule.Mol);

                //assuming the order of connectors  linker(P), 3'
                //designate the attachment points

                //create the linker atoms from the connectors specified in the molfile
                foreach (NAFragment.Connector connector in nucleotide_smod_fragment.Connectors)
                {
                    if (connector.Snip.Item2 > 0)
                    {
                        int three_prime_index = connector.Snip.Item2 - 1;
                        IndigoObject nucleotide_three_prime_atom = molecule.getAtom(three_prime_index);
                        //adding an "attachment" or "star" atom R to the 3' and linker connection atoms to conform to the way nucleotides/sides and their components are assembled
                        three_prime_star_atom = molecule.addAtom("R");
                        three_prime_star_atom.addBond(nucleotide_three_prime_atom, 1);
                        three_prime_star_atom_idx = three_prime_star_atom.index();
                    }
                    if (connector.Snip.Item1 > 0)
                    {
                        int linker_attachment_atom_index = connector.Snip.Item1 - 1;
                        IndigoObject nucleotide_linker_attachment_atom = molecule.getAtom(linker_attachment_atom_index);
                        //YP since fragments don't have star atoms on the phosphate but have a complete valence we need to chage on of the existing oxygen atoms to star atom
                        foreach (IndigoObject five_prime_atom_neighbor in molecule.getAtom(linker_attachment_atom_index).iterateNeighbors())
                        {
                            if (five_prime_atom_neighbor.symbol() == "O" && five_prime_atom_neighbor.degree() == 1)
                            {
                                five_prime_atom_neighbor.resetAtom("R");
                                linker_star_atom = five_prime_atom_neighbor;
                                linker_star_atom_idx = five_prime_atom_neighbor.index();
                                break;
                            }
                        }
                        //linker_star_atom = molecule.addAtom("R");
                        //linker_star_atom.addBond(nucleotide_linker_attachment_atom, 1);
                        //linker_star_atom_idx = linker_star_atom.index();
                    }
                }
                
                
            }

            public Nucleotide(Nucleoside nucleoside_in, NASugarLinker sugar_linker_in=null)
            {
                nucleoside = nucleoside_in.clone();
                //nucleoside = nucleoside_in;
                if (sugar_linker_in != null)
                {
                    sugar_linker = sugar_linker_in;
                    //connect linker and sugar at 5prime 

                    //both sugar and linker have a star atom
                    //we need to remove both star atoms and their bonds
                    //if one of the deleted bonds is a stereo bond, we need to remember it and restore it
                    //first look at the linker star atom and its bond to parent
                    Tuple<int, bool> remembered_bond_stereo = new Tuple<int, bool>(0, false);
                    int linker_atom_that_connects_sugar_idx = -1;
                    bool linker_5prime_star_parent_atom_bond_removed = false;
                    foreach (IndigoObject linker_5prime_star_parent_atom in sugar_linker.five_prime_connecting_star_atom.iterateNeighbors())
                    {
                        linker_atom_that_connects_sugar_idx = linker_5prime_star_parent_atom.index();
                        IndigoObject linker_5prime_star_parent_atom_bond = linker_5prime_star_parent_atom.bond();
                        if (linker_5prime_star_parent_atom_bond.bondStereo() != 0)
                        {
                            remembered_bond_stereo = RememberStereo(nucleoside.molecule, linker_atom_that_connects_sugar_idx + 1, sugar_linker.five_prime_connecting_star_atom_idx + 1);
                        }
                        //linker_5prime_star_parent_atom_bond.remove();
                        sugar_linker.molecule.getBond(linker_5prime_star_parent_atom_bond.index()).remove();
                        sugar_linker.molecule.getAtom(sugar_linker.five_prime_connecting_star_atom.index()).remove();
                        //sugar_linker.five_prime_connecting_star_atom.remove();
                        break;
                    }

                    //now look at the sugar star atom and its bond to parent

                    int sugar_atom_that_connects_linker_idx = -1;
                    foreach (IndigoObject sugar_5prime_star_atom_parent in nucleoside.five_prime_star_atom.iterateNeighbors())
                    {
                        sugar_atom_that_connects_linker_idx = sugar_5prime_star_atom_parent.index();
                        IndigoObject sugar_5prime_star_atom_parent_bond = sugar_5prime_star_atom_parent.bond();
                        if (sugar_5prime_star_atom_parent_bond.bondStereo() != 0)
                        {
                            remembered_bond_stereo = RememberStereo(nucleoside.molecule, sugar_atom_that_connects_linker_idx + 1, nucleoside.five_prime_star_atom_idx + 1);
                        }
                        //sugar_5prime_star_atom_parent_bond.remove();
                        nucleoside.molecule.getBond(sugar_5prime_star_atom_parent_bond.index()).remove();
                        nucleoside.molecule.getAtom(nucleoside.five_prime_star_atom.index()).remove();
                        //nucleoside.five_prime_star_atom.remove(); 
                        break;
                    }

                    //here we need to keep track what the new index of the 3prime star atom will be after we merge nucleoside with linker
                    //first see what the original index of the 3prime start atom was before merging sugar and base

                    //proceed to merge linker with nucleoside and make the bond
                    IndigoObject nucleotide_mapping = nucleoside.molecule.merge(sugar_linker.molecule);
                    nucleotide_mapping.mapAtom(sugar_linker.molecule.getAtom(linker_atom_that_connects_sugar_idx)).addBond(nucleoside.molecule.getAtom(sugar_atom_that_connects_linker_idx), 1);

                    three_prime_star_atom = nucleoside_in.three_prime_star_atom;
                    three_prime_star_atom_idx = three_prime_star_atom.index();

                    linker_star_atom = nucleotide_mapping.mapAtom(sugar_linker.three_prime_connecting_star_atom);
                    linker_star_atom_idx = linker_star_atom.index();

                    //remember stereo of all the bonds before the layout generation
                    List<Tuple<int, int>> nucleoside_bonds_before_layout = GetBondsStereo(nucleoside.molecule);
                    nucleoside.molecule.layout();
                    //get the stereo of all the bonds after layout generation
                    List<Tuple<int, int>> nucleoside_bonds_after_layout = GetBondsStereo(nucleoside.molecule);

                    //see if the stereo has inverted
                    bool stereo_has_inverted = InvertedStereo(nucleoside_bonds_before_layout, nucleoside_bonds_after_layout);

                    //restore stereo if necessary
                    //nucleoside.molecule.getAtom(sugar_atom_that_connects_linker_idx)).index() + 1, linker_atom_that_connects_sugar_idx + 1, remembered_bond_stereo.Item2, remembered_bond_stereo.Item1, stereo_has_inverted);

                    nucleoside.molecule = RestoreStereoBond(nucleoside.molecule, sugar_atom_that_connects_linker_idx + 1, linker_atom_that_connects_sugar_idx + 1, remembered_bond_stereo.Item2, remembered_bond_stereo.Item1, stereo_has_inverted);

                    three_prime_star_atom = nucleoside.three_prime_star_atom;
                    three_prime_star_atom_idx = three_prime_star_atom.index();
                    linker_star_atom = nucleotide_mapping.mapAtom(sugar_linker.three_prime_connecting_star_atom);
                    linker_star_atom_idx = linker_star_atom.index();

                    //this now becomes the molecule of the nucleotide
                    //YP
                    //1. find coordinates of an atom with max X coordinate
                    //PointF max_x_point = SDFUtil.GetMaxXPoint(nucleoside.molecule);
                    PointF max_y_point = SDFUtil.GetMaxYPoint(nucleoside.molecule);
                    PointF min_y_point = SDFUtil.GetMinYPoint(nucleoside.molecule);
                    //2. find coordinates of 3' atom
                    //nucleoside.molecule.getAtom(three_prime_star_atom_idx).xyz();

                    //3. find midpoint of the molecule
                    PointF center_point = SDFUtil.Get2DMoleculeCenterPoint(nucleoside.molecule);

                    //4. find angle between vectors (center, max x) and (center, 3')
                    //one size fits all
                    //Double rotation_angle = Math.Atan2(max_y_point.Y - center_point.Y, max_y_point.X - center_point.X) - Math.Atan2(nucleoside.molecule.getAtom(three_prime_star_atom_idx).xyz()[1] - center_point.Y, nucleoside.molecule.getAtom(three_prime_star_atom_idx).xyz()[0] - center_point.X) *(180.0 / Math.PI);
                    
                    //unflipped only
                    Double rotation_angle = Math.Atan2(min_y_point.Y - center_point.Y, min_y_point.X - center_point.X) - Math.Atan2(nucleoside.molecule.getAtom(three_prime_star_atom_idx).xyz()[1] - center_point.Y, nucleoside.molecule.getAtom(three_prime_star_atom_idx).xyz()[0] - center_point.X) * (180.0 / Math.PI);
                    //Double rotation_angle = Math.Atan2(max_y_point.Y - center_point.Y, max_y_point.X - center_point.X) - Math.Atan2(nucleoside.molecule.getAtom(three_prime_star_atom_idx).xyz()[1] - center_point.Y, nucleoside.molecule.getAtom(three_prime_star_atom_idx).xyz()[0] - center_point.X) * (180.0 / Math.PI);
                    //5. rotate by that angle
                    //one size fits all
                    //molecule = SDFUtil.Rotate2DMolecule(getIndigo(), nucleoside.molecule, (float)rotation_angle);

                    //unflipped only
                    //molecule = SDFUtil.Rotate2DMolecule(getIndigo(), nucleoside.molecule, (float)rotation_angle-50);

                    molecule = nucleoside.molecule;
                    this.Orient(linker_star_atom_idx,three_prime_star_atom_idx);

                    
                    //restoring the nucleoside to its original
                    nucleoside = nucleoside_in;
                }
                else
                {
                    nucleoside = nucleoside_in;
                    //YP
                    //1. find coordinates of an atom with max X coordinate
                    //PointF max_x_point = SDFUtil.GetMaxXPoint(nucleoside.molecule);
                    PointF max_y_point = SDFUtil.GetMaxYPoint(nucleoside.molecule);
                    //2. find coordinates of 3' atom
                    //nucleoside.molecule.getAtom(three_prime_star_atom_idx).xyz();

                    //3. find midpoint of the molecule
                    PointF center_point = SDFUtil.Get2DMoleculeCenterPoint(nucleoside.molecule);

                    //4. find angle between vectors (center, max x) and (center, 3')
                    Double rotation_angle = Math.Atan2(max_y_point.Y - center_point.Y, max_y_point.X - center_point.X) - Math.Atan2(nucleoside.molecule.getAtom(nucleoside.three_prime_star_atom_idx).xyz()[1] - center_point.Y, nucleoside.molecule.getAtom(nucleoside.three_prime_star_atom_idx).xyz()[0] - center_point.X) * (180.0 / Math.PI);

                    //5. rotate by that angle
                    //molecule = SDFUtil.Rotate2DMolecule(getIndigo(), nucleoside.molecule, (float)rotation_angle);
                    //molecule = SDFUtil.Rotate2DMolecule(getIndigo(), nucleoside_in.molecule, (float)0.0);
                    molecule = nucleoside.molecule;
                    three_prime_star_atom = nucleoside_in.three_prime_star_atom;
                    three_prime_star_atom_idx = three_prime_star_atom.index();

                    this.Orient(nucleoside.five_prime_star_atom_idx,nucleoside.three_prime_star_atom_idx);
                }
            }
        }

        //subclassing this, as a nucleotide that is made up of a link can have multiple 5 prime and 3 prime attachment points (multiple connector pairs)
        public class LinkNucleotide : Nucleotide
        {
            public bool is_link;
            public List<int> three_prime_star_atom_indices = new List<int>();
            public List<int> linker_star_atom_indices = new List<int>();
            public List<Tuple<int,int>> star_atom_indices = new List<Tuple<int,int>>();
            public NALink link;

            public LinkNucleotide(NALink link_in)
            {
                molecule = getIndigo().loadQueryMolecule(link_in.Linker.Molecule.Mol);
                link = link_in;
                //assuming the order of connectors  linker(P), 3'
                //designate the attachment points

                //create the linker atoms from the connectors specified in the molfile
                foreach (NAFragment.Connector connector in link_in.Linker.Connectors)
                {   
                    
                    if (connector.Snip.Item2 > 0)
                    {
                        int three_prime_index = connector.Snip.Item2 - 1;
                        IndigoObject nucleotide_three_prime_atom = molecule.getAtom(three_prime_index);
                        //adding an "attachment" or "star" atom R to the 3' and linker connection atoms to conform to the way nucleotides/sides and their components are assembled
                        three_prime_star_atom = molecule.addAtom("R");
                        three_prime_star_atom.addBond(nucleotide_three_prime_atom, 1);
                        three_prime_star_atom_idx = three_prime_star_atom.index();
                        three_prime_star_atom_indices.Add(three_prime_star_atom_idx);
                    }
                    if (connector.Snip.Item1 > 0)
                    {
                        int linker_attachment_atom_index = connector.Snip.Item1 - 1;
                        IndigoObject nucleotide_linker_attachment_atom = molecule.getAtom(linker_attachment_atom_index);
                        //YP since fragments don't have star atoms on the phosphate but have a complete valence we need to chage on of the existing oxygen atoms to star atom
                        foreach (IndigoObject five_prime_atom_neighbor in molecule.getAtom(linker_attachment_atom_index).iterateNeighbors())
                        {
                            if (five_prime_atom_neighbor.symbol() == "O" && five_prime_atom_neighbor.degree() == 1)
                            {
                                five_prime_atom_neighbor.resetAtom("R");
                                linker_star_atom = five_prime_atom_neighbor;
                                linker_star_atom_idx = five_prime_atom_neighbor.index();
                                linker_star_atom_indices.Add(linker_star_atom_idx);
                                break;
                            }
                        }
                        //linker_star_atom = molecule.addAtom("R");
                        //linker_star_atom.addBond(nucleotide_linker_attachment_atom, 1);
                        //linker_star_atom_idx = linker_star_atom.index();
                    }
                    star_atom_indices.Add(new Tuple<int, int>(linker_star_atom_idx, three_prime_star_atom_idx));
                }


            }
        }

        public class NAChain
        {
            public IndigoObject molecule;
            public IndigoObject three_prime_star_atom;
            public IndigoObject linker_star_atom;
            public int linker_star_atom_idx;
            public int three_prime_star_atom_idx;

            public NAChain(IndigoObject molecule_in, int three_prime_star_atom_index_in, int linker_star_atom_index_in)
            {
                molecule = molecule_in.clone();
                three_prime_star_atom = molecule.getAtom(three_prime_star_atom_index_in);
                three_prime_star_atom_idx = three_prime_star_atom_index_in;
                linker_star_atom = (linker_star_atom_index_in != -1 ) ? molecule.getAtom(linker_star_atom_index_in) : null;
                linker_star_atom_idx = linker_star_atom_index_in;
            }

            public NAChain(NAChain chain_in)
            {
                molecule = chain_in.molecule.clone();
                three_prime_star_atom = molecule.getAtom(chain_in.three_prime_star_atom_idx);
                three_prime_star_atom_idx = chain_in.three_prime_star_atom_idx;
                linker_star_atom = (chain_in.linker_star_atom_idx != -1) ? molecule.getAtom(chain_in.linker_star_atom_idx) : null;
                linker_star_atom_idx = chain_in.linker_star_atom_idx;
            }

            //connect chain's last nucleotide's 3' atom with chain's first nucleotide linker atom
            public void ConnectTheEnds()
            {
                IndigoObject three_prime_atom = null;
                IndigoObject linker_atom = null;
                foreach (IndigoObject three_prime_star_neighbor in molecule.getAtom(three_prime_star_atom_idx).iterateNeighbors())
                {
                    three_prime_atom = three_prime_star_neighbor;
                    break;
                }
                molecule.getAtom(three_prime_star_atom_idx).remove();

                foreach (IndigoObject linker_star_atom_neighbor in molecule.getAtom(linker_star_atom_idx).iterateNeighbors())
                {
                    linker_atom = linker_star_atom_neighbor;
                }
                molecule.getAtom(linker_star_atom_idx).remove();
                //create bond between parents
                three_prime_atom.addBond(linker_atom, 1);
            }

            //remove first nucleotide 5 prime star atom and last nucleotide 3 prime star atom
            public void CapEnds()
            {
                IndigoObject three_prime_atom = null;
                IndigoObject five_prime_linker_atom = null;

                foreach (IndigoObject three_prime_star_neighbor in molecule.getAtom(three_prime_star_atom_idx).iterateNeighbors())
                {
                    three_prime_atom = three_prime_star_neighbor;
                    break;
                }
                molecule.getAtom(three_prime_star_atom_idx).remove();
                //three_prime_atom.addBond(molecule.addAtom("H"), 1);

                foreach (IndigoObject five_prime_linker_star_neighbor in molecule.getAtom(linker_star_atom_idx).iterateNeighbors())
                {
                    five_prime_linker_atom = five_prime_linker_star_neighbor;
                    break;
                }
                molecule.getAtom(linker_star_atom_idx).remove();
                five_prime_linker_atom.addBond(molecule.addAtom("O"), 1);
                if (conv_opt == null || conv_opt.Coord)
                {
                    if (!Task.Run(() => molecule.layout()).Wait(layout_timeout))
                    {
                        throw new TimeoutException("2D Coordinates took too long to generate.");
                    }
                    //na_mol.layout();
                }
                
            }

            public void AttachNucleotide(Nucleotide nucleotide)
            {
                //uncomment for manual layout generation
                //nucleotide.molecule= SDFUtil.TranslateMolecule(getIndigo(), nucleotide.molecule, SDFUtil.GetMaxX(getIndigo(), molecule) +9, 0, 0);

                //YP SRS-386 revisited
                //first translate the nucleotide along the x-axis before attaching to chain
                float x_shift = SDFUtil.GetMaxX(molecule) + SDFUtil.GetMaxX(nucleotide.molecule)+6;
                nucleotide.molecule = SDFUtil.TranslateMolecule(getIndigo(), nucleotide.molecule, x_shift, 0, 0);
                //nucleotide.molecule= SDFUtil.TranslateMolecule(getIndigo(), nucleotide.molecule, SDFUtil.GetMinX(molecule) - 5, 0, 0);


                //NAChain return_chain = new NAChain(chain_in.molecule,chain_in.three_prime_star_atom_idx,chain_in.linker_star_atom_idx);
                int nucleotide_5prime_atom_idx = -1;
                Tuple<int, bool> remembered_bond_stereo = new Tuple<int, bool>(0, false);
                foreach (IndigoObject nucleotide_5prime_star_parent_atom in nucleotide.linker_star_atom.iterateNeighbors())
                {
                    nucleotide_5prime_atom_idx = nucleotide_5prime_star_parent_atom.index();
                    IndigoObject nucleotide_5prime_star_parent_atom_bond = nucleotide_5prime_star_parent_atom.bond();
                    if (nucleotide_5prime_star_parent_atom_bond.bondStereo() != 0)
                    {
                        remembered_bond_stereo = RememberStereo(nucleotide.molecule, nucleotide_5prime_atom_idx + 1, nucleotide.linker_star_atom_idx + 1);
                    }
                    //nucleotide_5prime_star_parent_atom_bond.remove();
                    nucleotide.molecule.getBond(nucleotide_5prime_star_parent_atom_bond.index()).remove();
                    nucleotide.molecule.getAtom(nucleotide.linker_star_atom.index()).remove();
                    //nucleotide.linker_star_atom.remove();
                    break;
                }

                //now look at the sugar star atom and its bond to parent
                int chain_3prime_atom_idx = -1;
                foreach (IndigoObject chain_3prime_star_atom_parent in molecule.getAtom(three_prime_star_atom_idx).iterateNeighbors())
                {
                    chain_3prime_atom_idx = chain_3prime_star_atom_parent.index();
                    IndigoObject chain_3prime_star_atom_parent_bond = chain_3prime_star_atom_parent.bond();
                    if (chain_3prime_star_atom_parent_bond.bondStereo() != 0)
                    {
                        remembered_bond_stereo = RememberStereo(molecule, chain_3prime_atom_idx + 1, three_prime_star_atom_idx + 1);
                    }
                    //chain_3prime_star_atom_parent_bond.remove();
                    molecule.getBond(chain_3prime_star_atom_parent_bond.index()).remove();
                    molecule.getAtom(three_prime_star_atom.index()).remove();
                    //return_chain.three_prime_star_atom.remove();
                    break;
                }

                //here we need to keep track what the new index of the 3prime star atom will be after we merge nucleoside with linker
                //first see what the original index of the 3prime start atom was before merging sugar and base

                //proceed to merge linker with nucleoside and make the bond
                IndigoObject chain_mapping = molecule.merge(nucleotide.molecule);
                chain_mapping.mapAtom(nucleotide.molecule.getAtom(nucleotide_5prime_atom_idx)).addBond(molecule.getAtom(chain_3prime_atom_idx), 1);

                three_prime_star_atom = chain_mapping.mapAtom(nucleotide.molecule.getAtom(nucleotide.three_prime_star_atom_idx));
                three_prime_star_atom_idx = three_prime_star_atom.index();

                //remember stereo of all the bonds before the layout generation
                List<Tuple<int, int>> nucleoside_bonds_before_layout = GetBondsStereo(molecule);

                //uncomment for automatic layout generation
                //if (conv_opt == null || conv_opt.Coord)
                //{
                //    if (!Task.Run(() => molecule.layout()).Wait(layout_timeout))
                //    {
                //        throw new TimeoutException("2D Coordinates took too long to generate.");
                //    }
                //    //na_mol.layout();
                //}

               

                //get the stereo of all the bonds after layout generation
                List<Tuple<int, int>> nucleoside_bonds_after_layout = GetBondsStereo(molecule);

                //see if the stereo has inverted
                bool stereo_has_inverted = InvertedStereo(nucleoside_bonds_before_layout, nucleoside_bonds_after_layout);

                //restore stereo if necessary
                //return_chain.molecule = RestoreStereoBond(return_chain.molecule, chain_mapping.mapAtom(return_chain.molecule.getAtom(chain_3prime_atom_idx)).index() + 1, nucleotide_5prime_atom_idx + 1, remembered_bond_stereo.Item2, remembered_bond_stereo.Item1, stereo_has_inverted);
                //YP commenting out for now as it breaks stereo on the 5-3 connection between first and second nucleotide
                //molecule = RestoreStereoBond(molecule, chain_3prime_atom_idx + 1, nucleotide_5prime_atom_idx + 1, remembered_bond_stereo.Item2, remembered_bond_stereo.Item1, stereo_has_inverted);

            }

            public void AttachLinkNucleotide(LinkNucleotide nucleotide)
            {
                //NAChain return_chain = new NAChain(chain_in.molecule,chain_in.three_prime_star_atom_idx,chain_in.linker_star_atom_idx);
                //float x_shift = SDFUtil.GetMinX(molecule) - SDFUtil.GetMinX(nucleotide.molecule) - 15;
                float x_shift = SDFUtil.GetMaxX(molecule) + SDFUtil.GetMaxX(nucleotide.molecule);
                float y_shift = SDFUtil.GetMinY(molecule) - SDFUtil.GetMinY(nucleotide.molecule) - 8;
                nucleotide.molecule = SDFUtil.TranslateMolecule(getIndigo(), nucleotide.molecule, x_shift, y_shift, 0);
                //nucleotide.molecule = SDFUtil.TranslateMolecule(getIndigo(), nucleotide.molecule, SDFUtil.GetMinX(molecule) - 5, 0, 0);

                int nucleotide_5prime_atom_idx = -1;
                Tuple<int, bool> remembered_bond_stereo = new Tuple<int, bool>(0, false);
                foreach (IndigoObject nucleotide_5prime_star_parent_atom in nucleotide.molecule.getAtom(nucleotide.linker_star_atom_indices[0]).iterateNeighbors())
                {
                    nucleotide_5prime_atom_idx = nucleotide_5prime_star_parent_atom.index();
                    IndigoObject nucleotide_5prime_star_parent_atom_bond = nucleotide_5prime_star_parent_atom.bond();
                    if (nucleotide_5prime_star_parent_atom_bond.bondStereo() != 0)
                    {
                        remembered_bond_stereo = RememberStereo(nucleotide.molecule, nucleotide_5prime_atom_idx + 1, nucleotide.linker_star_atom_idx + 1);
                    }
                    //nucleotide_5prime_star_parent_atom_bond.remove();
                    nucleotide.molecule.getBond(nucleotide_5prime_star_parent_atom_bond.index()).remove();
                    nucleotide.molecule.getAtom(nucleotide.linker_star_atom_indices[0]).remove();
                    //nucleotide.linker_star_atom.remove();
                    break;
                }

                //now look at the sugar star atom and its bond to parent
                int chain_3prime_atom_idx = -1;
                foreach (IndigoObject chain_3prime_star_atom_parent in molecule.getAtom(three_prime_star_atom_idx).iterateNeighbors())
                {
                    chain_3prime_atom_idx = chain_3prime_star_atom_parent.index();
                    IndigoObject chain_3prime_star_atom_parent_bond = chain_3prime_star_atom_parent.bond();
                    if (chain_3prime_star_atom_parent_bond.bondStereo() != 0)
                    {
                        remembered_bond_stereo = RememberStereo(molecule, chain_3prime_atom_idx + 1, three_prime_star_atom_idx + 1);
                    }
                    //chain_3prime_star_atom_parent_bond.remove();
                    molecule.getBond(chain_3prime_star_atom_parent_bond.index()).remove();
                    molecule.getAtom(three_prime_star_atom.index()).remove();
                    //return_chain.three_prime_star_atom.remove();
                    break;
                }

                //here we need to keep track what the new index of the 3prime star atom will be after we merge nucleoside with linker
                //first see what the original index of the 3prime start atom was before merging sugar and base

                //proceed to merge linker with nucleoside and make the bond
                IndigoObject chain_mapping = molecule.merge(nucleotide.molecule);
                chain_mapping.mapAtom(nucleotide.molecule.getAtom(nucleotide_5prime_atom_idx)).addBond(molecule.getAtom(chain_3prime_atom_idx), 1);

                if (nucleotide.three_prime_star_atom_indices.Count > 0)
                {
                    three_prime_star_atom = chain_mapping.mapAtom(nucleotide.molecule.getAtom(nucleotide.three_prime_star_atom_indices[0]));
                    three_prime_star_atom_idx = three_prime_star_atom.index();
                }

                //remove first of linker_star_atom_indices as it is no longer available for chain-link connection
                nucleotide.linker_star_atom_indices.RemoveAt(0);

                //now that the link has been connected to the chain, the indices of star atoms have changed, so need to re-map them
                for (int i = 0; i <= nucleotide.linker_star_atom_indices.Count - 1; i += 1)
                {
                    nucleotide.linker_star_atom_indices[i] = chain_mapping.mapAtom(nucleotide.molecule.getAtom(nucleotide.linker_star_atom_indices[i])).index();
                }
                for (int i = 0; i <= nucleotide.three_prime_star_atom_indices.Count - 1; i += 1)
                {
                    nucleotide.three_prime_star_atom_indices[i] = chain_mapping.mapAtom(nucleotide.molecule.getAtom(nucleotide.three_prime_star_atom_indices[i])).index();
                }
                //add this link to a list of links that have been already incorporated into a chain but still need to be connected to other chains
                partially_connected_links.Add(new PartiallyConnectedLink(nucleotide.three_prime_star_atom_indices, nucleotide.linker_star_atom_indices, nucleotide.link));

                //remember stereo of all the bonds before the layout generation
                List<Tuple<int, int>> nucleoside_bonds_before_layout = GetBondsStereo(molecule);
                //if (conv_opt == null || conv_opt.Coord)
                //{
                //    if (!Task.Run(() => molecule.layout()).Wait(layout_timeout))
                //    {
                //        throw new TimeoutException("2D Coordinates took too long to generate.");
                //    }
                //    //na_mol.layout();
                //}
                //get the stereo of all the bonds after layout generation
                List<Tuple<int, int>> nucleoside_bonds_after_layout = GetBondsStereo(molecule);

                //see if the stereo has inverted
                bool stereo_has_inverted = InvertedStereo(nucleoside_bonds_before_layout, nucleoside_bonds_after_layout);

                //restore stereo if necessary
                //return_chain.molecule = RestoreStereoBond(return_chain.molecule, chain_mapping.mapAtom(return_chain.molecule.getAtom(chain_3prime_atom_idx)).index() + 1, nucleotide_5prime_atom_idx + 1, remembered_bond_stereo.Item2, remembered_bond_stereo.Item1, stereo_has_inverted);
                molecule = RestoreStereoBond(molecule, chain_3prime_atom_idx + 1, nucleotide_5prime_atom_idx + 1, remembered_bond_stereo.Item2, remembered_bond_stereo.Item1, stereo_has_inverted);

            }
        }

        public class PartiallyConnectedLink
        {
            //public List<NAChain> chains_connected_to = new List<NAChain>();
            //public List<Tuple<int, int>> star_atom_indices = new List<Tuple<int, int>>();
            public List<int> three_prime_star_atom_indices = new List<int>();
            public List<int> linker_star_atom_indices = new List<int>();
            public NALink original_link;
            //public int linker_star_atom_idx;
            //public int three_prime_star_atom_idx;

            public PartiallyConnectedLink(List<int> three_prime_star_atom_indices_in, List<int> linker_star_atom_indices_in, NALink original_link_in)
            {
                three_prime_star_atom_indices = three_prime_star_atom_indices_in;
                linker_star_atom_indices = linker_star_atom_indices_in;
                original_link = original_link_in;
                //chain_connected_to = chain_connected_to_in;
                //three_prime_star_atom_idx = three_prime_star_atom_index_in;
                //linker_star_atom_idx = linker_star_atom_index_in;
            }
        }

        //this attaches a chain to a link that is already connected to one or more other chains connected into na_mol_in
        public static NAChain ConnectChainToNAViaLink(IndigoObject na_mol_in, NAChain chain_in, int chain_index,PartiallyConnectedLink pcl_in)
        {
            //IndigoObject connected_mol = na_mol_in.clone();

            /////////
            //YP first shift the chain so it is laid out not on top of the na mol
            na_mol_in = SDFUtil.TranslateMolecule(getIndigo(), na_mol_in, 0, (float)((float)chain_index * 15.0), 0);
            //5' attachment on the na_mol

            int link_5prime_atom_idx = -1;
            Tuple<int, bool> remembered_bond_stereo = new Tuple<int, bool>(0, false);
            foreach (IndigoObject link_5prime_star_parent_atom in na_mol_in.getAtom(pcl_in.linker_star_atom_indices[0]).iterateNeighbors())
            {
                link_5prime_atom_idx = link_5prime_star_parent_atom.index();
                IndigoObject link_5prime_star_parent_atom_bond = link_5prime_star_parent_atom.bond();
                //if (link_5prime_star_parent_atom_bond.bondStereo() != 0)
                //{
                //    remembered_bond_stereo = RememberStereo(nucleotide.molecule, nucleotide_5prime_atom_idx + 1, nucleotide.linker_star_atom_idx + 1);
                //}
                //nucleotide_5prime_star_parent_atom_bond.remove();
                na_mol_in.getBond(link_5prime_star_parent_atom_bond.index()).remove();
                na_mol_in.getAtom(pcl_in.linker_star_atom_indices[0]).remove();
                //nucleotide.linker_star_atom.remove();
                break;
            }

            //now look at the sugar star atom and its bond to parent
            int chain_3prime_atom_idx = -1;
            foreach (IndigoObject chain_3prime_star_atom_parent in chain_in.molecule.getAtom(chain_in.three_prime_star_atom_idx).iterateNeighbors())
            {
                chain_3prime_atom_idx = chain_3prime_star_atom_parent.index();
                IndigoObject chain_3prime_star_atom_parent_bond = chain_3prime_star_atom_parent.bond();
                //if (chain_3prime_star_atom_parent_bond.bondStereo() != 0)
                //{
                //   remembered_bond_stereo = RememberStereo(chain_in.molecule, chain_3prime_atom_idx + 1, three_prime_star_atom_idx + 1);
                //}
                //chain_3prime_star_atom_parent_bond.remove();
                chain_in.molecule.getBond(chain_3prime_star_atom_parent_bond.index()).remove();
                chain_in.molecule.getAtom(chain_in.three_prime_star_atom_idx).remove();
                //return_chain.three_prime_star_atom.remove();
                break;
            }

            //here we need to keep track what the new index of the 3prime star atom will be after we merge chain with the na_mol 
            //first see what the original index of the 3prime start atom was before merging

            //proceed to merge linker with nucleoside and make the bond
            IndigoObject na_mol_link_chain_mapping = na_mol_in.merge(chain_in.molecule);
            //na_mol_link_chain_mapping.mapAtom(nucleotide.molecule.getAtom(link_5prime_atom_idx)).addBond(molecule.getAtom(chain_3prime_atom_idx), 1);
            na_mol_link_chain_mapping.mapAtom(chain_in.molecule.getAtom(chain_3prime_atom_idx)).addBond(na_mol_in.getAtom(link_5prime_atom_idx), 1);

            //three_prime_star_atom = chain_mapping.mapAtom(nucleotide.molecule.getAtom(nucleotide.three_prime_star_atom_idx));
            //IndigoObject three_prime_star_atom = chain_mapping.mapAtom(nucleotide.molecule.getAtom(nucleotide.three_prime_star_atom_idx));
            //three_prime_star_atom_idx = three_prime_star_atom.index();

            //remember stereo of all the bonds before the layout generation
            //List<Tuple<int, int>> nucleoside_bonds_before_layout = GetBondsStereo(molecule);
            //molecule.layout();
            //get the stereo of all the bonds after layout generation
            //List<Tuple<int, int>> nucleoside_bonds_after_layout = GetBondsStereo(molecule);

            //see if the stereo has inverted
            //bool stereo_has_inverted = InvertedStereo(nucleoside_bonds_before_layout, nucleoside_bonds_after_layout);

            //restore stereo if necessary
            //return_chain.molecule = RestoreStereoBond(return_chain.molecule, chain_mapping.mapAtom(return_chain.molecule.getAtom(chain_3prime_atom_idx)).index() + 1, nucleotide_5prime_atom_idx + 1, remembered_bond_stereo.Item2, remembered_bond_stereo.Item1, stereo_has_inverted);
            //molecule = RestoreStereoBond(molecule, chain_3prime_atom_idx + 1, nucleotide_5prime_atom_idx + 1, remembered_bond_stereo.Item2, remembered_bond_stereo.Item1, stereo_has_inverted);


            ////////////
            //instantiate new chain based on the merged molecule, partially connected link's 3' star atom index as chain's new 3' star atom index and keep chain's original 5' star atom index
            //na_mol_in.layout();
            NAChain return_chain = new NAChain(na_mol_in, (pcl_in.three_prime_star_atom_indices.Count > 0 ? pcl_in.three_prime_star_atom_indices[0] : -1), chain_in.linker_star_atom_idx);

            //pop partially connected link's star indices from 0th position as they have just been utilized to make the connection with the chain
            pcl_in.linker_star_atom_indices.RemoveAt(0);
            //pcl_in.three_prime_star_atom_indices.RemoveAt(0);

            return return_chain;
        }

        public static String GetSugarAtPosition(NASubunit subunit, int position)
        {
            String sugar_identifier = "";
            foreach (Tuple<int, String> sugar in subunit.Sugars)
            {
                if (sugar.Item1 == position)
                {
                    sugar_identifier = sugar.Item2;
                    break;
                }

            }
            
            return sugar_identifier;
        }

        public static String GetLinkageAtPosition(NASubunit subunit, int position)
        {
            String linkage_identifier = "";
            foreach (Tuple<int, String> sugar_linker in subunit.SugarLinkers)
            {
                if (sugar_linker.Item1 == position)
                {
                    linkage_identifier = sugar_linker.Item2;
                    break;
                }

            }
            return linkage_identifier;
        }


        /*
        public static Tuple<IndigoObject,IndigoObject,IndigoObject,IndigoObject> GetMonomerAtPosition(this NucleicAcid na, PolymerBaseReadingState state, NASubunit subunit, int position_index)
        {

            NAFragment nucleotide_smod_fragment = GetStructuralModificationFragment(na, subunit.Ordinal, position_index, new List<string>(new string[] { "NUCLEOTIDE_SUBSTITUTION", "NUCLEOTIDE SUBSTITUTION" }));
            if (nucleotide_smod_fragment == null)
            //no nucleotide modification found at this position, so proceed to check nucleoside modification if present at this position
            {
                NAFragment nucleoside_smod_fragment = GetStructuralModificationFragment(na, subunit.Ordinal, position_index, new List<string>(new string[] { "NUCLEOSIDE_SUBSTITUTION", "NUCLEOSIDE SUBSTITUTION" }));
                if (nucleoside_smod_fragment == null)
                //no nucleotide or nucleoside modification found at this position, so proceed to build up sugar+base+linker nucleotide
                {
                    return AssembleNucleotide(na, state, subunit, position_index);
                }
                else
                {
                    return getIndigo().loadQueryMolecule(nucleoside_smod_fragment.Molecule.Mol);
                }
            }
            else
            {
                return getIndigo().loadQueryMolecule(nucleotide_smod_fragment.Molecule.Mol);
            }

        }
        */


        //this removes all "R" atoms from three prime end and converts "R" atoms on five prime ends to "O"
        public static void CapMoleculeEnds(IndigoObject molecule)
        {
            foreach (IndigoObject atom in molecule.iterateAtoms())
            {
                if (atom.symbol() == "R" || atom.atomicNumber() == 0)
                {
                    bool three_prime = true;
                    foreach (IndigoObject nei in atom.iterateNeighbors())
                    {
                        if (nei.symbol() == "P")
                        {
                            three_prime = false;
                            atom.resetAtom("O");
                            
                        }
                    }
                    if (three_prime) { atom.remove(); }
                }
            }
        }

        //this removes all "R" atoms from three prime end and converts "R" atoms on five prime ends to "O"
        public static void CapMoleculeEnds(Nucleotide in_nucleotide)
        {
            
            foreach (IndigoObject atom in in_nucleotide.molecule.iterateAtoms())
            {
                if (atom.symbol() == "R" || atom.atomicNumber() == 0)
                {
                    bool three_prime = true;
                    foreach (IndigoObject nei in atom.iterateNeighbors())
                    {
                        if (nei.symbol() == "P")
                        {
                            three_prime = false;
                            atom.resetAtom("O");
                            in_nucleotide.linker_star_atom_idx = nei.index();

                        }
                    }
                    if (three_prime)
                    {
                        foreach (IndigoObject nei in atom.iterateNeighbors())
                        {
                            if (nei.symbol() == "O")
                            {
                                in_nucleotide.three_prime_star_atom_idx = nei.index();
                            }
                        }
                        atom.remove();

                    }
                }
            }
        }

        public static SdfRecord asChemical(this NucleicAcid na, PolymerBaseReadingState state, ConvertOptions opt)
        {

            int MAX_ATOMS = 999;
            int MAX_BONDS = 999;
            conv_opt = opt;

            try { 
            //if (1==1){

                IndigoObject na_mol = getIndigo().loadMolecule("");
                na.Subunits.ForEachWithIndex((su, i) =>
                {
                    //for testing purposes only
                    //su.isCircular = true;
                    NAChain chain = null;
                    //this bool indicates if entire na molecule has already been merged into one chain (usually happens when there are cross-chain links that turn multiple chains in one connected molecule
                    bool chains_already_merged = false;
                    List<char> nucleobases = new List<char>();
                    nucleobases.AddRange(su.Sequence.ToString());
                    Nucleotide nucleotide = null;
                
                    for (int j = 0; j <= nucleobases.Count - 1; j += 1)
                    {
                        bool linker_absent = false;
                        //check if explicit (specified in JSON as a modification) nucleotide structural modification exists at this site
                        NAFragment nucleotide_smod_fragment = GetStructuralModificationFragment(na, su.Ordinal, j, new List<string>(new string[] { "NUCLEOTIDE_SUBSTITUTION", "NUCLEOTIDE SUBSTITUTION", "NUCLEOSIDE BASE SUBSTITUTION" }));
                        if (nucleotide_smod_fragment != null)
                        //explicit (specified in JSON as a modification) nucleotide structural modification exists at this site, thus create a nuclotide object based on that rather than assembly from base+sugar+linker
                        {
                                //if (PartiallyConnectedLinkExists(na, su.Ordinal, j))
                                //{
                                //int smod_p_index = GetPartiallyConnectedLink(GetNALink(na, su.Ordinal, j)).Item2.Item1;
                                //smod_o_index = GetPartiallyConnectedLink(GetNALink(na, su.Ordinal, j)).Item2.Item2;
                                //}
                                //check if there is a link at this location, which would indicate that current nucleotide should be treated as a link
                                if (!PartiallyConnectedLinkExists(na, su.Ordinal, j))
                                {
                                    if (IsLink(na, su.Ordinal, j))
                                    {
                                        nucleotide = new LinkNucleotide(GetNALink(na, su.Ordinal, j));
                                    }
                                    else
                                    {
                                        nucleotide = new Nucleotide(nucleotide_smod_fragment);
                                    }
                                }
                        }
                        else
                        {
                            //check if explicit (specified in JSON as a modification) nucseotide structural modification exists at this site
                            NAFragment nucleoside_smod_fragment = GetStructuralModificationFragment(na, su.Ordinal, j, new List<string>(new string[] { "NUCLEOSIDE_SUBSTITUTION", "NUCLEOSIDE SUBSTITUTION" }));

                            if (nucleoside_smod_fragment != null)
                            //explicit (specified in JSON as a modification) nucleoside structural modification exists at this site, thus create a nucloside object based on that rather than assembly from base+sugar
                            {
                                Nucleoside nucleoside = new Nucleoside(nucleoside_smod_fragment);
                                PNAComponent current_linker_component = PNAComponent.from(GetLinkageAtPosition(su, j + 1), state);
                                linker_absent = current_linker_component.Fragment == null;
                                nucleotide = new Nucleotide(nucleoside, (current_linker_component.Fragment != null) ? new NASugarLinker(current_linker_component) : null);
                            }
                            else
                            {
                                //no explicit NUCLEOSIDE/NUCLEOTIDE modifications found, just assemble from components base+sugar+linker
                                PNAComponent current_nucleobase_component = PNAComponent.from(nucleobases[j].ToString(), state);
                                PNAComponent current_sugar_component = PNAComponent.from(GetSugarAtPosition(su, j + 1), state);
                                PNAComponent current_linker_component = PNAComponent.from(GetLinkageAtPosition(su, j + 1), state);
                                if (current_nucleobase_component.Fragment == null)
                                {
                                    throw new Exception("Nucleic Acid cannot be converted, missing definition for fragment " + current_nucleobase_component.sym + " representing nucleobase at position " + (j + 1) + " on chain " + (i + 1));
                                }
                                if (current_sugar_component.Fragment == null)
                                {
                                    throw new Exception("Nucleic Acid cannot be converted, missing definition for fragment " + current_sugar_component.sym + " representing sugar at position " + (j + 1) + " on chain " + (i + 1));
                                }
                            
                                linker_absent = current_linker_component.Fragment == null;
                                Nucleoside nucleoside = new Nucleoside(new NANucleobase(current_nucleobase_component), new NASugar(current_sugar_component));
                                //IndigoObject linkermol = getIndigo().loadMolecule(current_linker_component.Fragment.Molecule.Mol);
                                nucleotide = new Nucleotide(nucleoside, (current_linker_component.Fragment != null) ? new NASugarLinker(current_linker_component) : null);
                            }
                            
                        }

                        if (j == 0)
                        //first position
                        {
                            if (su.isCircular && linker_absent)
                            {
                                throw new Exception("Nucleic Acid cannot be converted. Circular subunit " + su.Ordinal + " is missing required phosphate at first position");
                            }
                            //if this is the very first nucleotide, designate it as chain along with designating nucleotide's star atoms as chain's star atoms
                            chain = new NAChain(nucleotide.molecule, nucleotide.three_prime_star_atom_idx, nucleotide.linker_star_atom_idx);
                        }
                        else
                        {

                            if (j <= nucleobases.Count - 2)
                            //mid chain
                            {
                                if (linker_absent)
                                {
                                    throw new Exception("Nucleic Acid cannot be converted. Missing linkage at position " + (j + 1) + " on chain " + (i + 1));
                                }
                                chain.AttachNucleotide(nucleotide);
                            }
                            else
                            //last nucleotide
                            {
                                if (linker_absent)
                                {
                                    throw new Exception("Nucleic Acid cannot be converted. Missing linkage at position " + (j + 1) + " on chain " + (i + 1));
                                }

                                if (PartiallyConnectedLinkExists(na, su.Ordinal, j) && nucleotide_smod_fragment != null)
                                {
                                    //connect to na_mol at star_atom_indices[0]
                                    chain = ConnectChainToNAViaLink(na_mol, chain, i, GetPartiallyConnectedLink(GetNALink(na, su.Ordinal, j)));
                                    //since above statement connects the chain to already built up chains, no need to merge this chain with na_mol later anymore
                                    chains_already_merged = true;
                                    //int smod_p_index = GetPartiallyConnectedLink(GetNALink(na, su.Ordinal, j)).Item2.Item1;
                                    //smod_o_index = GetPartiallyConnectedLink(GetNALink(na, su.Ordinal, j)).Item2.Item2;
                                }
                                else
                                {
                                    
                                    //if (GetNALink(na, su.Ordinal, j) != null)
                                    if (IsLink(na, su.Ordinal, j))
                                    {
                                        chain.AttachLinkNucleotide((LinkNucleotide) nucleotide);
                                        //partially_connected_links.Add(new Tuple<NALink, Tuple<int, int>>(GetNALink(na, su.Ordinal, j), new Tuple<int, int>(nucleotide_smod_fragment.Connectors[1].Snip.Item1 - 1, nucleotide_smod_fragment.Connectors[1].Snip.Item2 - 1)));
                                        //partially_connected_links.Add(new PartiallyConnectedLink()
                                    }
                                    else
                                    {
                                        chain.AttachNucleotide(nucleotide);
                                    }
                                    
                                }
                                
                                if (su.isCircular)
                                {
                                    //connect last nucleotide 3' with first nucleotide linker
                                    chain.ConnectTheEnds();
                                }
                                else
                                {
                                    //remove star atoms hanging off of the ends of the chain
                                    //might have to do this at the very end for the entire NA molecule
                                    //chain.CapEnds();
                                }
                            }
                            

                        }
                        
                        if ((chain.molecule.countAtoms() + na_mol.countAtoms()) > MAX_ATOMS || (chain.molecule.countBonds() + na_mol.countBonds()) > MAX_BONDS)
                        {
                            throw new Exception("Nucleic Acid is too large to be interpretted as chemical");
                        }
                        //na_mol = MoveStereoFromRings(na_mol);
                    }
                    if (i == 0)
                    {
                        na_mol = getIndigo().loadQueryMolecule(chain.molecule.molfile());
                    }
                    else
                    {
                        
                        if (chains_already_merged)
                        {
                            na_mol = chain.molecule.clone();
                        }
                        else
                        {
                            //YP shift down any additional chains
                            //PointF max_y_point = SDFUtil.GetMaxYPoint(na_mol.molfile());
                            chain.molecule=SDFUtil.TranslateMolecule(getIndigo(), chain.molecule, 0, (float)(i * 20), 0);
                            na_mol.merge(chain.molecule);
                        }                        
                    }
                }
                );
                CapMoleculeEnds(na_mol);
                List<Tuple<int, int>> bonds_before_layout = GetBondsStereo(na_mol);
                if (opt == null || opt.Coord)
                {
                    //if (!Task.Run(() => na_mol.layout()).Wait(layout_timeout))
                    //{
                    //    throw new TimeoutException("2D Coordinates took too long to generate.");
                    //}
                    //na_mol.layout();
                }
                List<Tuple<int, int>> bonds_after_layout = GetBondsStereo(na_mol);
                bool stereo_has_inverted = InvertedStereo(bonds_before_layout, bonds_after_layout);
                //na_mol = MoveStereoFromRings(na_mol);
                SdfRecord sdfmol = SdfRecord.FromString(na_mol.molfile());
                sdfmol.AddField("UNII", na.UNII);
                sdfmol.AddField("SUBSTANCE_ID", na.UNII);
                sdfmol.AddField("STRUCTURE_ID", na.UNII);
                return sdfmol;

            }
            
            catch (Exception e)
            {

                System.Console.WriteLine(e.Message);
                System.Console.WriteLine(e.StackTrace);
                throw e;
            }
            
            return null;

        }

        
        //public static SdfRecord asChemical_giveup(this NucleicAcid na, PolymerBaseReadingState state, ConvertOptions opt)
        //{

        //    int MAX_ATOMS = 999;
        //    int layout_timeout = 3600000;
        //    //int layout_timeout = 120000;

        //    try
        //    {
        //        List<SdfRecord> SdfRecords = new List<SdfRecord>();
        //        IndigoObject chain_mol = getIndigo().loadMolecule("");
        //        IndigoObject na_mol = getIndigo().loadMolecule("");
        //        IndigoObject chain_base_sugar_mapping = null;
        //        IndigoObject chain_mapping = null;
        //        IndigoObject chain_sugar_mapping = null;
        //        IndigoObject chain_linker_mapping = null;
        //        IndigoObject chain_smod_mapping = null;
        //        getIndigo().setOption("standardize-stereo", true);
        //        na.Subunits.ForEachWithIndex((su, i) =>
        //        {
        //            //for testing purposes only
        //            //su.isCircular = false;
        //            List<char> nucleobases = new List<char>();
        //            nucleobases.AddRange(su.Sequence.ToString());
        //            IndigoObject chain_r92_atom = null;
        //            int last_nucleotide_circular_attachment_atom_idx = -1;
        //            for (int j = nucleobases.Count - 1; j >= 0; j -= 1)

        //            {
        //                //bool make_modification = false;
        //                //YP connect nucleobase with sugar
        //                //nucleobases[j]+su.Sugar[j]

        //                PNAComponent na_nucleobase = PNAComponent.from(nucleobases[j].ToString(), state);
        //                PNAComponent na_sugar = PNAComponent.from(GetSugarAtPosition(su, j + 1), state);
        //                //if sugar is non standard, remember this so that the resulting nucleotide can be treated as a modification
        //                //if (!IsSugarStandard(na_sugar.sym)) { make_modification = true; }
        //                //if (this.alreadyAdded()) return false;

        //                IndigoObject base_component_mol = getIndigo().loadQueryMolecule(na_nucleobase.Fragment.Molecule.Mol);

        //                int sugar_r90_atom_idx = na_sugar.Fragment.getStarAtomIndex("_R90") - 1;
        //                //Tuple<int, int, int, IndigoObject> preserved_link_stereo = PreserveLinkBondStereo(getIndigo().loadQueryMolecule(na_sugar.Fragment.Molecule.Mol), sugar_r90_atom_idx);
        //                //IndigoObject sugar_component_mol = preserved_link_stereo.Item4;
        //                IndigoObject sugar_component_mol = getIndigo().loadQueryMolecule(na_sugar.Fragment.Molecule.Mol);

        //                //IndigoObject mol2 = getIndigo().loadMolecule("ON");
        //                //IndigoObject base_sugar_connecting_atom = null;
        //                //IndigoObject sugar_base_connecting_atom = null;
        //                int base_sugar_connecting_atom_idx = -1;
        //                int sugar_base_connecting_atom_idx = -1;
        //                int sugar_linker_connecting_atom_idx = -1;
        //                int linker_sugar_connecting_atom_idx = -1;
        //                int sugar_chain_connecting_atom_idx = -1;
        //                int chain_sugar_connecting_atom_idx = -1;
        //                int connecting_bond_stereo = 0;
        //                int sugar_r91_index = -1;
        //                int sugar_r92_index = -1;

        //                IndigoObject base_sugar_mapping = null;
        //                IndigoObject nucleotide_mapping = null;
        //                //int sugar_base_bond_stereo = 0;
        //                IndigoObject sugar_r92_atom = null;
        //                IndigoObject linker_r92_atom = null;
        //                IndigoObject modification_mol = null;
        //                //first check if an explicit modification is at this spot
        //                NAFragment nucleotide_smod_fragment = GetStructuralModificationFragment(na, su.Ordinal, j, new List<string>(new string[] { "NUCLEOTIDE_SUBSTITUTION", "NUCLEOTIDE SUBSTITUTION" }));

        //                if (nucleotide_smod_fragment == null)
        //                //no nucleotide modification found at this position, so proceed to build up sugar-base or nucleoside modification if present
        //                {
        //                    NAFragment nucleoside_smod_fragment = GetStructuralModificationFragment(na, su.Ordinal, j, new List<string>(new string[] { "NUCLEOSIDE_SUBSTITUTION", "NUCLEOSIDE SUBSTITUTION" }));
        //                    if (nucleoside_smod_fragment == null)
        //                    //no nucleoside modification found at this position, so proceed to build up sugar-base
        //                    {

        //                        //YP first connect base and sugar at _R90
        //                        //this is accomplished by first deleting the star atoms (_R90 atoms) on base and sugar
        //                        //Ideally a stereo bond (if found) between either sugar's or base's star atom and its parent should be kept
        //                        //and then used to connect star atom's respective parents to each other
        //                        //however I haven't figured out how to preserve such bond, so currently if there is a stereochemistry, it's lost
        //                        int base_r90_atom_idx = na_nucleobase.Fragment.getStarAtomIndex("_R90") - 1;
        //                        int sugar_r90_parent_stereo_type = 0;
        //                        IndigoObject sugar_r90_atom = sugar_component_mol.getAtom(sugar_r90_atom_idx);
        //                        IndigoObject base_r90_atom = base_component_mol.getAtom(base_r90_atom_idx);


        //                        //IndigoObject modified_sugar_component_mol = MoveStereoToRing(sugar_component_mol, sugar_r90_atom_idx+1);

        //                        bool r90_parent_bond_removed = false;
        //                        sugar_r91_index = na_sugar.Fragment.getStarAtomIndex("_R91") - 1;
        //                        sugar_r92_index = na_sugar.Fragment.getStarAtomIndex("_R92") - 1;
        //                        sugar_r92_atom = sugar_component_mol.getAtom(sugar_r92_index);


        //                        if (j == nucleobases.Count - 1)
        //                        //first processed nucleotide
        //                        {
        //                            if (su.isCircular)
        //                            {
        //                                last_nucleotide_circular_attachment_atom_idx = sugar_r92_index;
        //                            }
        //                            else
        //                            {
        //                                //remove _R92 (3') star atom
        //                                sugar_r92_atom.remove();
        //                            }


        //                        }

        //                        foreach (IndigoObject base_r90_parent_atom in base_r90_atom.iterateNeighbors())
        //                        {
        //                            base_sugar_connecting_atom_idx = base_r90_parent_atom.index();
        //                            IndigoObject base_r90_parent_bond = base_r90_parent_atom.bond();
        //                            if (base_r90_parent_bond.bondStereo() == 0)
        //                            {
        //                                base_r90_parent_bond.remove();
        //                                r90_parent_bond_removed = true;
        //                            }
        //                            else
        //                            {
        //                                connecting_bond_stereo = base_r90_parent_bond.bondStereo();
        //                            }
        //                            base_r90_atom.remove();
        //                            break;
        //                        }
        //                        Tuple<int, bool> remembered_bond_stereo = new Tuple<int, bool>(0, false);
        //                        foreach (IndigoObject sugar_r90_parent_atom in sugar_r90_atom.iterateNeighbors())
        //                        {

        //                            sugar_base_connecting_atom_idx = sugar_r90_parent_atom.index();
        //                            sugar_r90_parent_stereo_type = sugar_r90_parent_atom.stereocenterType();
        //                            IndigoObject sugar_r90_parent_bond = sugar_r90_parent_atom.bond();
        //                            if (sugar_r90_parent_bond.bondStereo() == 0 && !r90_parent_bond_removed)
        //                            {
        //                                sugar_r90_parent_bond.remove();
        //                                r90_parent_bond_removed = true;
        //                            }
        //                            else
        //                            {
        //                                remembered_bond_stereo = RememberStereo(sugar_component_mol, sugar_base_connecting_atom_idx + 1, sugar_r90_atom_idx + 1);
        //                                connecting_bond_stereo = sugar_r90_parent_bond.bondStereo();
        //                            }

        //                            sugar_r90_atom.remove();
        //                            break;
        //                        }

        //                        base_sugar_mapping = base_component_mol.merge(sugar_component_mol);
        //                        //base_component_mol = RestoreStereoBond(base_component_mol,base_sugar_mapping.mapAtom(sugar_component_mol.getAtom(preserved_link_stereo.Item1-1)).index()+1, base_sugar_mapping.mapAtom(sugar_component_mol.getAtom(preserved_link_stereo.Item2-1)).index()+1, preserved_link_stereo.Item3);
        //                        base_sugar_mapping.mapAtom(sugar_component_mol.getAtom(sugar_base_connecting_atom_idx)).addBond(base_component_mol.getAtom(base_sugar_connecting_atom_idx), 1);
        //                        List<Tuple<int, int>> base_component_mol_bonds_before_layout = GetBondsStereo(base_component_mol);

        //                        if (opt == null || opt.Coord)
        //                        {
        //                            base_component_mol.layout();
        //                        }

        //                        List<Tuple<int, int>> base_component_mol_bonds_after_layout = GetBondsStereo(base_component_mol);
        //                        bool stereo_has_inverted = InvertedStereo(base_component_mol_bonds_before_layout, base_component_mol_bonds_after_layout);

        //                        base_component_mol = RestoreStereoBond(base_component_mol, base_sugar_mapping.mapAtom(sugar_component_mol.getAtom(sugar_base_connecting_atom_idx)).index() + 1, base_sugar_connecting_atom_idx + 1, remembered_bond_stereo.Item2, remembered_bond_stereo.Item1, stereo_has_inverted);

        //                        //base_component_mol = MoveStereoFromRings(base_component_mol);
        //                        sugar_r92_atom = base_sugar_mapping.mapAtom(sugar_component_mol.getAtom(sugar_r92_index));
        //                        sugar_r91_index = base_sugar_mapping.mapAtom(sugar_component_mol.getAtom(sugar_r91_index)).index();
        //                        /*
        //                         * 
        //                        if (make_modification)
        //                        {
        //                            modification_mol = base_component_mol;
        //                        }
        //                        */
        //                    }
        //                    else
        //                    //there is a nucleoside modification at this position, so use attach the modification instead of building up sugar,base
        //                    {
        //                        //found nucleoside modification, need to attach
        //                        IndigoObject nucleoside_smod_mol = getIndigo().loadQueryMolecule(nucleoside_smod_fragment.Molecule.Mol);

        //                        //assuming the order of connectors 5', 3'
        //                        //designate the attachment points for next component(or modification)
        //                        int three_prime_index = nucleoside_smod_fragment.Connectors[0].Snip.Item2 - 1;
        //                        int five_prime_index = nucleoside_smod_fragment.Connectors[0].Snip.Item1 - 1;

        //                        IndigoObject nucleoside_three_prime_atom = nucleoside_smod_mol.getAtom(three_prime_index);
        //                        IndigoObject nucleoside_five_prime_atom = nucleoside_smod_mol.getAtom(five_prime_index);

        //                        //here adding an "attachment" atom R to the three and five prime connection atoms
        //                        //this way the component-assembly code that expects those, can be used to attach further components
        //                        //to the modification, rather than treating modification as a special case
        //                        IndigoObject nucleoside_smod_r91_atom = nucleoside_smod_mol.addAtom("R");
        //                        nucleoside_five_prime_atom.addBond(nucleoside_smod_r91_atom, 1);
        //                        //designate the attachment points for next component(or modification)
        //                        sugar_r91_index = nucleoside_smod_r91_atom.index();

        //                        //only create R atom on 3' if it's not the last nucleoside in a chain
        //                        if (j != nucleobases.Count - 1)
        //                        {
        //                            IndigoObject nucleoside_smod_r92_atom = nucleoside_smod_mol.addAtom("R");
        //                            nucleoside_three_prime_atom.addBond(nucleoside_smod_r92_atom, 1);
        //                            //designate the attachment points for next component(or modification)
        //                            sugar_r92_atom = nucleoside_smod_r92_atom;
        //                        }

        //                        base_component_mol = nucleoside_smod_mol;
        //                        if (opt == null || opt.Coord)
        //                        {
        //                            base_component_mol.layout();
        //                        }


        //                    }
        //                }
        //                else
        //                //there is a nucleotide modification at this position, so use attach the modification instead of building up sugar,base or nucleoside substitution
        //                {
        //                    //found nucleotide modification, need to attach
        //                    IndigoObject nucleotide_smod_mol = getIndigo().loadQueryMolecule(nucleotide_smod_fragment.Molecule.Mol);

        //                    //assuming the order of connectors  linker(P), 3'
        //                    //designate the attachment points for next component(or modification)

        //                    //YP SRS-381 strange that this is looking for second connector pair which would only exist for links
        //                    //commenting out for now and making it look at first connector pair instead
        //                    //int three_prime_index = nucleotide_smod_fragment.Connectors[1].Snip.Item1 - 1;
        //                    int three_prime_index = nucleotide_smod_fragment.Connectors[0].Snip.Item2 - 1;

        //                    //YP SRS-381 move this inside the if statement below
        //                    //int linker_attachment_atom_index = nucleotide_smod_fragment.Connectors[0].Snip.Item1 - 1;

        //                    IndigoObject nucleotide_three_prime_atom = nucleotide_smod_mol.getAtom(three_prime_index);

        //                    //YP SRS-381 move this inside the if statement below
        //                    //IndigoObject nucleotide_linker_attachment_atom = nucleotide_smod_mol.getAtom(linker_attachment_atom_index);

        //                    //here adding an "attachment" atom R to the three connection atoms
        //                    //this way the component-assembly code that expects those, can be used to attach further components
        //                    //to the modification, rather than treating modification as a special case
        //                    IndigoObject nucleotide_smod_sugar_r92_atom = nucleotide_smod_mol.addAtom("R");
        //                    nucleotide_three_prime_atom.addBond(nucleotide_smod_sugar_r92_atom, 1);

        //                    //YP SRS-381 move this inside the if statement below
        //                    //IndigoObject nucleotide_smod_linker_r92_atom = nucleotide_smod_mol.addAtom("R");
        //                    //nucleotide_linker_attachment_atom.addBond(nucleotide_smod_linker_r92_atom, 1);

        //                    //designate the attachment points for next component(or modification)
        //                    sugar_r92_atom = nucleotide_smod_sugar_r92_atom;

        //                    if (j != 0)
        //                    {
        //                        if (!su.isCircular)
        //                        {
        //                            int linker_attachment_atom_index = nucleotide_smod_fragment.Connectors[0].Snip.Item1 - 1;
        //                            IndigoObject nucleotide_linker_attachment_atom = nucleotide_smod_mol.getAtom(linker_attachment_atom_index);

        //                            IndigoObject nucleotide_smod_linker_r92_atom = nucleotide_smod_mol.addAtom("R");
        //                            nucleotide_linker_attachment_atom.addBond(nucleotide_smod_linker_r92_atom, 1);

        //                            chain_r92_atom = nucleotide_smod_linker_r92_atom;
        //                        }
        //                    }


        //                    base_component_mol = nucleotide_smod_mol;
        //                    if (opt == null || opt.Coord)
        //                    {
        //                        base_component_mol.layout();
        //                    }

        //                }

        //                if (j == nucleobases.Count - 1)
        //                {
        //                    //first processed nucleoside, making it the "chain"
        //                    chain_mol = getIndigo().loadQueryMolecule(base_component_mol.molfile());
        //                    chain_base_sugar_mapping = base_sugar_mapping;
        //                }
        //                else
        //                {
        //                    //connect to already built up chain at _R92 (3')
        //                    //rather than mapping linker's _R92 atom to the already built up chain
        //                    //use the fact that it will be the only query ("A") atom
        //                    bool r92_parent_bond_removed = false;
        //                    foreach (IndigoObject sugar_r92_parent_atom in sugar_r92_atom.iterateNeighbors())
        //                    {
        //                        sugar_chain_connecting_atom_idx = sugar_r92_parent_atom.index();
        //                        IndigoObject sugar_r92_parent_bond = sugar_r92_parent_atom.bond();
        //                        if (sugar_r92_parent_bond.bondStereo() == 0 && !r92_parent_bond_removed)
        //                        {
        //                            sugar_r92_parent_bond.remove();
        //                            r92_parent_bond_removed = true;
        //                        }
        //                        else
        //                        {
        //                            connecting_bond_stereo = sugar_r92_parent_bond.bondStereo();
        //                        }

        //                        //sugar_r92_atom.remove();
        //                        base_component_mol.removeAtoms(new int[] { sugar_r92_atom.index() });
        //                        break;
        //                    }
        //                    foreach (IndigoObject chain_r92_parent_atom in chain_r92_atom.iterateNeighbors())
        //                    {
        //                        chain_sugar_connecting_atom_idx = chain_r92_parent_atom.index();
        //                        IndigoObject na_r92_parent_bond = chain_r92_parent_atom.bond();
        //                        if (na_r92_parent_bond.bondStereo() == 0)
        //                        {
        //                            na_r92_parent_bond.remove();
        //                            r92_parent_bond_removed = true;
        //                        }
        //                        else
        //                        {
        //                            connecting_bond_stereo = na_r92_parent_bond.bondStereo();
        //                        }
        //                        chain_r92_atom.remove();
        //                        break;
        //                    }

        //                    //base_component_mol.alignAtoms(Enumerable.Range(0, base_component_mol.countAtoms() - 1).ToArray(), SDFUtil.TranslateCoordinates(base_component_mol.xyz(),x_shift: -2));
        //                    float maxx = SDFUtil.GetMaxX(getIndigo(),chain_mol);
        //                    //base_component_mol = SDFUtil.TranslateMolecule(getIndigo(), base_component_mol, x_shift: SDFUtil.GetMaxX(getIndigo(), chain_mol) + SDFUtil.GetMaxX(getIndigo(), base_component_mol) + 10);
        //                    base_component_mol = SDFUtil.TranslateMolecule(getIndigo(), base_component_mol, x_shift: SDFUtil.GetMinX(getIndigo(), chain_mol) - SDFUtil.GetMinX(getIndigo(), base_component_mol) - 8);

        //                    chain_base_sugar_mapping = chain_mol.merge(base_component_mol);
        //                    chain_base_sugar_mapping.mapAtom(base_component_mol.getAtom(sugar_chain_connecting_atom_idx)).addBond(chain_mol.getAtom(chain_sugar_connecting_atom_idx), 1);
        //                    //YP SRS-381 nested if didn't work, so trying to just make sure that sugar_r91_index is not -1
        //                    //if (j != 0)
        //                    //{
        //                    //if (!su.isCircular)
        //                    if (sugar_r91_index != -1)
        //                    {
        //                        sugar_r91_index = chain_base_sugar_mapping.mapAtom(base_component_mol.getAtom(sugar_r91_index)).index();
        //                    }
        //                    //}
        //                    //chain_r92_atom = chain_base_sugar_mapping.mapAtom(chain_r92_atom);
        //                }
        //                //IndigoObject sugar_r91_atom = nucleotide_mol.getAtom(sugar_r91_index);
        //                //foreach (IndigoObject attachment_atom in base_sugar_mapping.mapAtom(sugar_component_mol.getAtom(sugar_base_connecting_atom_idx)).iterateNeighbors())
        //                //{
        //                //attachment_atom.bond().set
        //                //}


        //                //sugar_r91_index = chain_sugar_mapping.mapAtom(base_component_mol.getAtom(sugar_r91_index)).index();
        //                IndigoObject sugar_r91_atom = chain_mol.getAtom(sugar_r91_index);

        //                //IndigoObject sugar_r92_atom = base_sugar_mapping.mapAtom(sugar_component_mol.getAtom(sugar_r92_index));


        //                NAFragment terminal_smod_fragment = null;
        //                if (j == nucleobases.Count - 1)
        //                //last nucleotide, check if there is a terminal structural modification
        //                {
        //                    terminal_smod_fragment = GetStructuralModificationFragment(na, su.Ordinal, nucleobases.Count, new List<string>(new string[] { "NUCLEOSIDE_SUBSTITUTION" }));

        //                    /*This is here for historical reasons if implicit modifications should be processed as part of asChemical
        //                     * right now they are moved out of asChemical as a separate routine
        //                     * 
        //                    if (make_modification && terminal_smod_fragment == null)
        //                    //if this is supposed to be implicit modification (non-standard sugar present)
        //                    //create modification object and add to NA modifications list. No linker is needed since it's the last nuceloside
        //                    {
        //                        Tuple<IndigoObject, int> modification_connector = GetR91ParentAtomIndex(modification_mol);
        //                        na.ProcessStructuralModificationGroup(MakeImplicitNAStructuralModification(na, su, j, "NUCLEOTIDE SUBSTITUTION POINT", state, CreateAssembledFragment(modification_connector.Item1.molfile(), new List<int>() { modification_connector.Item2 + 1, 0 }, new List<String>() { "_R91", "0" }, state)), state);
        //                        //NAModification implicit_nucleoside_modification = new NAModification
        //                    }
        //                    //need to check if there are terminal modifications that would be attached to r91 sugar atom
        //                    //Tuple<String, String> terminal_smod_listing = GetListing(su.Ordinal, nucleobases.Count, na.StructuralModifications_listing);
        //                    //if (terminal_smod_listing != null)
        //                    //Tuple<String, String> terminal_smod_listing = 
        //                    */

        //                    if (terminal_smod_fragment != null)
        //                    {
        //                        //found terminal modification, need to attach
        //                        //NAFragment terminal_smod_fragment = FragmentResolve(terminal_smod_listing.Item1, state);

        //                        IndigoObject terminal_smod_mol = getIndigo().loadQueryMolecule(terminal_smod_fragment.Molecule.Mol);
        //                        //assuming here that because this is terminal smod (by nucleobases.Count, last nucleotide), the only possible choice is that the only connector is _R91 (connecting to _R91(5') sugar)
        //                        //int smod_r91_index = terminal_smod_fragment.Connectors[0].Snip.Item1 - 1;
        //                        int smod_p_index = -1;
        //                        int smod_o_index = -1;
        //                        if (PartiallyConnectedLinkExists(na, su.Ordinal, nucleobases.Count))
        //                        {
        //                            //partially_connected_links[partially_connected_links.Count - 1] = new Tuple<NALink, Tuple<int, int>>(partially_connected_links[partially_connected_links.Count - 1].Item1, new Tuple<int, int>(smod_p_index, smod_o_index));
        //                            smod_p_index = GetPartiallyConnectedLink(GetNALink(na, su.Ordinal, nucleobases.Count)).Item2.Item1;
        //                            smod_o_index = GetPartiallyConnectedLink(GetNALink(na, su.Ordinal, nucleobases.Count)).Item2.Item2;
        //                            //this link already built into another chain, thus already part of the molecule, thus we need to operate on the built up molecule from here
        //                            chain_mol = na_mol.clone();
        //                            na_mol = getIndigo().loadQueryMolecule("");
        //                        }
        //                        else
        //                        {
        //                            partially_connected_links.Add(new Tuple<NALink, Tuple<int, int>>(GetNALink(na, su.Ordinal, nucleobases.Count), new Tuple<int, int>(terminal_smod_fragment.Connectors[1].Snip.Item1 - 1, terminal_smod_fragment.Connectors[1].Snip.Item2 - 1)));
        //                            smod_p_index = terminal_smod_fragment.Connectors[0].Snip.Item1 - 1;
        //                            smod_o_index = terminal_smod_fragment.Connectors[0].Snip.Item2 - 1;
        //                            chain_mol = terminal_smod_mol;
        //                        }


        //                        //commenting this out because if the last (first to be processed in this case) nucleotide is a mod then it becomes a chain, as nothing alse has been added
        //                        //chain_smod_mapping = chain_mol.merge(terminal_smod_mol);
        //                        //chain_mol = terminal_smod_mol;

        //                        foreach (IndigoObject p_neighbor in chain_mol.getAtom(smod_p_index).iterateNeighbors())
        //                        {
        //                            if (p_neighbor.bond().bondOrder() == 1 && p_neighbor.bond().bondStereo() == 0 && p_neighbor.symbol() == "O" && p_neighbor.degree() == 1)
        //                            {
        //                                //p_neighbor.remove();
        //                                linker_sugar_connecting_atom_idx = p_neighbor.index();
        //                                break;
        //                            }
        //                        }
        //                        //linker_sugar_connecting_atom_idx = smod_p_index;
        //                        chain_r92_atom = chain_mol.getAtom(linker_sugar_connecting_atom_idx);

        //                        //chain_smod_mapping.mapAtom(terminal_smod_mol.getAtom(smod_r91_index)).addBond(chain_mol.getAtom(sugar_linker_connecting_atom_idx), 1);
        //                        /*foreach (IndigoObject p_neighbor in chain_smod_mapping.mapAtom(terminal_smod_mol.getAtom(smod_r91_index)).iterateNeighbors())
        //                        {
        //                            if (p_neighbor.bond().bondOrder() == 1 && p_neighbor.bond().bondStereo() == 0 && p_neighbor.symbol() == "O" && p_neighbor.degree() == 1)
        //                            {
        //                                p_neighbor.remove();
        //                                break;
        //                            }
        //                        }
        //                        */
        //                    }

        //                }

        //                //remove _R91 star atom (5')
        //                //sugar_r91_atom.remove();
        //                //sugar-linker attachment code

        //                if (nucleotide_smod_fragment == null && terminal_smod_fragment == null)
        //                {
        //                    //attach linker at _R91
        //                    PNAComponent na_sugar_linker = PNAComponent.from(GetLinkageAtPosition(su, j + 1), state);
        //                    if (na_sugar_linker.Fragment != null)
        //                    {
        //                        IndigoObject linker_component_mol = getIndigo().loadQueryMolecule(na_sugar_linker.Fragment.Molecule.Mol);
        //                        int linker_r92_index = na_sugar_linker.Fragment.getStarAtomIndex("_R92") - 1;
        //                        linker_r92_atom = linker_component_mol.getAtom(linker_r92_index);

        //                        //if (j < su.SugarLinkers.Count)
        //                        //if (j < nucleobases.Count - 1)
        //                        //{
        //                        //YP connect the sugar linker to the sugar that it already connected to the nucleobase
        //                        //(nucleobases[j]+su.Sugar[j])+su.SugarLinkers[j]

        //                        int linker_r91_index = na_sugar_linker.Fragment.getStarAtomIndex("_R91") - 1;

        //                        IndigoObject linker_r91_atom = linker_component_mol.getAtom(linker_r91_index);


        //                        bool r91_bond_removed = false;
        //                        foreach (IndigoObject sugar_r91_parent_atom in sugar_r91_atom.iterateNeighbors())
        //                        {
        //                            sugar_linker_connecting_atom_idx = sugar_r91_parent_atom.index();
        //                            IndigoObject sugar_r91_parent_bond = sugar_r91_parent_atom.bond();
        //                            if (sugar_r91_parent_bond.bondStereo() == 0)
        //                            {
        //                                sugar_r91_parent_bond.remove();
        //                                r91_bond_removed = true;
        //                            }
        //                            else
        //                            {
        //                                connecting_bond_stereo = sugar_r91_parent_atom.bondStereo();
        //                            }
        //                            sugar_r91_atom.remove();
        //                            break;
        //                        }

        //                        foreach (IndigoObject linker_r91_parent_atom in linker_r91_atom.iterateNeighbors())
        //                        {
        //                            linker_sugar_connecting_atom_idx = linker_r91_parent_atom.index();
        //                            IndigoObject linker_r91_parent_bond = linker_r91_parent_atom.bond();
        //                            if (linker_r91_parent_bond.bondStereo() == 0 && !r91_bond_removed)
        //                            {
        //                                linker_r91_parent_bond.remove();
        //                                r91_bond_removed = true;
        //                            }
        //                            else
        //                            {
        //                                connecting_bond_stereo = linker_r91_parent_bond.bondStereo();
        //                            }

        //                            linker_r91_atom.remove();
        //                            break;
        //                        }
        //                        //float maxx = SDFUtil.GetMinX(getIndigo(), chain_mol);
        //                        //linker_component_mol = SDFUtil.TranslateMolecule(getIndigo(), linker_component_mol, x_shift: SDFUtil.GetMaxX(getIndigo(), chain_mol) - SDFUtil.GetMaxX(getIndigo(), linker_component_mol) + 5);
        //                        linker_component_mol = SDFUtil.TranslateMolecule(getIndigo(), linker_component_mol, x_shift: SDFUtil.GetMinX(getIndigo(), chain_mol) - SDFUtil.GetMinX(getIndigo(), linker_component_mol) - 5);
        //                        chain_linker_mapping = chain_mol.merge(linker_component_mol);
        //                        chain_linker_mapping.mapAtom(linker_component_mol.getAtom(linker_sugar_connecting_atom_idx)).addBond(chain_mol.getAtom(sugar_linker_connecting_atom_idx), 1);

        //                        //chain_mol.layout();
        //                        //chain_mol = MoveStereoFromRings(chain_mol);

        //                        if (j == 0)
        //                        //if first nucleotide, need to turn R# connecting atom to Oxygen
        //                        {
        //                            //if circular need to connect to last nucleotide of the chain, otherwise cap linkage with O
        //                            if (su.isCircular)
        //                            {
        //                                //here we're just assuming that we have an R atom hanging off of each of the ends of the chain
        //                                //given this assumption we just need to connect the parents of those R atoms to each other and remove R atoms themselves
        //                                foreach (IndigoObject r_atom1 in chain_mol.iterateAtoms())
        //                                {
        //                                    if (r_atom1.symbol().Equals("R"))
        //                                    {
        //                                        int r_atom1_index = r_atom1.index();
        //                                        IndigoObject r_atom1_parent = null;
        //                                        foreach (IndigoObject r_atom1_neighbor in r_atom1.iterateNeighbors())
        //                                        {
        //                                            //R atom presumably has only one neighbor/parent atom that it is bonded to
        //                                            r_atom1_parent = r_atom1_neighbor;
        //                                        }
        //                                        r_atom1.remove();
        //                                        foreach (IndigoObject r_atom2 in chain_mol.iterateAtoms())
        //                                        {
        //                                            if (r_atom2.symbol().Equals("R") && r_atom2.index() != r_atom1_index)
        //                                            {
        //                                                IndigoObject r_atom2_parent = null;
        //                                                foreach (IndigoObject r_atom2_neighbor in r_atom2.iterateNeighbors())
        //                                                {
        //                                                    //R atom presumably has only one neighbor/parent atom that it is bonded to
        //                                                    r_atom2_parent = r_atom2_neighbor;
        //                                                }
        //                                                r_atom2.remove();
        //                                                //create bond between parents
        //                                                r_atom1_parent.addBond(r_atom2_parent, 1);
        //                                            }
        //                                        }
        //                                        break;
        //                                    }
        //                                }
        //                            }
        //                            else
        //                            {
        //                                chain_mol = CapLinkage(chain_mol, chain_linker_mapping.mapAtom(linker_component_mol.getAtom(linker_sugar_connecting_atom_idx)));
        //                            }

        //                        }
        //                        //}    
        //                    }
        //                    else
        //                    {
        //                        //linker is not present
        //                        if (j > 0)
        //                        //if anything but first nucleotide, error it, as linkers are required
        //                        {
        //                            throw new Exception("Nucleic Acid cannot be converted, missing linkage at position " + (j + 1) + " on chain " + (i + 1));
        //                        }
        //                        else
        //                        {
        //                            //need to error here if circular as linkage is required on first nucleotide if circular
        //                            //first nucleotide with missing linkage
        //                            //remove the r91 R atom
        //                            if (su.isCircular)
        //                            {
        //                                throw new Exception("Circular subunit " + su.Ordinal + " is missing required phosphate at first position");
        //                            }
        //                            else
        //                            {
        //                                sugar_r91_atom.remove();
        //                            }

        //                        }
        //                    }
        //                }
        //                else
        //                {
        //                    //nucleotide_smod_fragment !=null or terminal_smod_fragment != null

        //                }


        //                if (j > 0 && terminal_smod_fragment == null)
        //                //if not the first nucleotide
        //                {
        //                    //need to remember which one is the linker's _R92 atom to which the next nucleotide will be attached
        //                    //IndigoObject nucleotide_r92_atom = nucleotide_mapping.mapAtom(linker_r92_atom);
        //                    chain_r92_atom = chain_linker_mapping.mapAtom(linker_r92_atom);
        //                    /*
        //                     * if (j == 0)
        //                    {
        //                        //if first nucleotide, create the chain_mol
        //                        chain_mol = getIndigo().loadQueryMolecule(nucleotide_mol.molfile());
        //                        chain_r92_atom = nucleotide_r92_atom;
        //                    }
        //                    else
        //                    {
        //                        //here chain_mol would have been created so just merge it with the newly built nucleotide
        //                        chain_mapping = chain_mol.merge(nucleotide_mol);
        //                        chain_r92_atom = chain_mapping.mapAtom(nucleotide_r92_atom);
        //                    }
        //                    */



        //                }
        //                if (chain_mol.countAtoms() > MAX_ATOMS)
        //                {
        //                    throw new Exception("Nucleic Acid is too large to be interpretted as chemical");
        //                }
        //                //YP This code generates layout every time a new nucleotide is added
        //                //if (opt == null || opt.Coord)
        //                //{
        //                //    if (!Task.Run(() => chain_mol.layout()).Wait(layout_timeout))
        //                //    {
        //                //        throw new TimeoutException("2D Coordinates took too long to generate.");
        //                //    }
        //                //}



        //                //IndigoObject assembled_mol = getIndigo().loadMolecule(base_component_mol.smiles());
        //                //assembled_mol.standardize();
        //                //assembled_mol.layout();


        //            }
        //            if (i == 0)
        //            {
        //                na_mol = getIndigo().loadQueryMolecule(chain_mol.molfile());
        //            }
        //            else
        //            {
        //                na_mol.merge(chain_mol);
        //            }

        //        });
        //        //YP This code would generate layout once the entire chain is put together
                
        //        if (opt == null || opt.Coord)
        //        {
        //            if (!Task.Run(() => na_mol.layout()).Wait(layout_timeout))
        //            {
        //                throw new TimeoutException("2D Coordinates took too long to generate.");
        //            }
        //            //na_mol.layout();
        //        }
                
        //        na_mol = MoveStereoFromRings(na_mol);
        //        SdfRecord sdfmol = SdfRecord.FromString(na_mol.molfile());
        //        sdfmol.AddField("UNII", na.UNII);
        //        sdfmol.AddField("SUBSTANCE_ID", na.UNII);
        //        sdfmol.AddField("STRUCTURE_ID", na.UNII);
        //        return sdfmol;
        //        //SdfRecords.Add(sdfmol);
        //        //return SdfRecords;
        //    }
        //    catch (Exception e)
        //    {
        //        //if (e.Message == "2D Coordinates took too long to generate.")
        //        //{
        //        //    throw e;
        //        //}
        //        System.Console.WriteLine(e.Message);
        //        System.Console.WriteLine(e.StackTrace);
        //        throw e;
        //    }

        //    return null;

        //}
        

        ////YP
        ////ConvertOptions opt parameter is a bit hacky but required to pass along the command line arguments that govern
        ////the converstion/output format such as generation of 2D coordinates
        //public static SdfRecord asChemical_layout(this NucleicAcid na, PolymerBaseReadingState state, ConvertOptions opt)
        //{
        //    int MAX_ATOMS = 999;
        //    int layout_timeout = 3600000;
        //    //int layout_timeout = 120000;

        //    try
        //    {
        //        List<SdfRecord> SdfRecords = new List<SdfRecord>();
        //        IndigoObject chain_mol = getIndigo().loadMolecule("");
        //        IndigoObject na_mol = getIndigo().loadMolecule("");
        //        IndigoObject chain_base_sugar_mapping = null;
        //        IndigoObject chain_mapping = null;
        //        IndigoObject chain_sugar_mapping = null;
        //        IndigoObject chain_linker_mapping = null;
        //        IndigoObject chain_smod_mapping = null;
        //        getIndigo().setOption("standardize-stereo", true);
        //        na.Subunits.ForEachWithIndex((su, i) =>
        //        {
        //            //for testing purposes only
        //            //su.isCircular = false;
        //            List<char> nucleobases = new List<char>();
        //            nucleobases.AddRange(su.Sequence.ToString());
        //            IndigoObject chain_r92_atom = null;
        //            int last_nucleotide_circular_attachment_atom_idx = -1;
        //            for (int j = nucleobases.Count - 1; j >= 0; j -= 1)

        //            {
        //                //bool make_modification = false;
        //                //YP connect nucleobase with sugar
        //                //nucleobases[j]+su.Sugar[j]

        //                PNAComponent na_nucleobase = PNAComponent.from(nucleobases[j].ToString(), state);
        //                PNAComponent na_sugar = PNAComponent.from(GetSugarAtPosition(su, j + 1), state);
        //                //if sugar is non standard, remember this so that the resulting nucleotide can be treated as a modification
        //                //if (!IsSugarStandard(na_sugar.sym)) { make_modification = true; }
        //                //if (this.alreadyAdded()) return false;

        //                IndigoObject base_component_mol = getIndigo().loadQueryMolecule(na_nucleobase.Fragment.Molecule.Mol);

        //                int sugar_r90_atom_idx = na_sugar.Fragment.getStarAtomIndex("_R90") - 1;
        //                //Tuple<int, int, int, IndigoObject> preserved_link_stereo = PreserveLinkBondStereo(getIndigo().loadQueryMolecule(na_sugar.Fragment.Molecule.Mol), sugar_r90_atom_idx);
        //                //IndigoObject sugar_component_mol = preserved_link_stereo.Item4;
        //                IndigoObject sugar_component_mol = getIndigo().loadQueryMolecule(na_sugar.Fragment.Molecule.Mol);

        //                //IndigoObject mol2 = getIndigo().loadMolecule("ON");
        //                //IndigoObject base_sugar_connecting_atom = null;
        //                //IndigoObject sugar_base_connecting_atom = null;
        //                int base_sugar_connecting_atom_idx = -1;
        //                int sugar_base_connecting_atom_idx = -1;
        //                int sugar_linker_connecting_atom_idx = -1;
        //                int linker_sugar_connecting_atom_idx = -1;
        //                int sugar_chain_connecting_atom_idx = -1;
        //                int chain_sugar_connecting_atom_idx = -1;
        //                int connecting_bond_stereo = 0;
        //                int sugar_r91_index = -1;
        //                int sugar_r92_index = -1;

        //                IndigoObject base_sugar_mapping = null;
        //                IndigoObject nucleotide_mapping = null;
        //                //int sugar_base_bond_stereo = 0;
        //                IndigoObject sugar_r92_atom = null;
        //                IndigoObject linker_r92_atom = null;
        //                IndigoObject modification_mol = null;
        //                //first check if an explicit modification is at this spot
        //                NAFragment nucleotide_smod_fragment = GetStructuralModificationFragment(na, su.Ordinal, j, new List<string>(new string[] { "NUCLEOTIDE_SUBSTITUTION", "NUCLEOTIDE SUBSTITUTION" }));

        //                if (nucleotide_smod_fragment == null)
        //                //no nucleotide modification found at this position, so proceed to build up sugar-base or nucleoside modification if present
        //                {
        //                    NAFragment nucleoside_smod_fragment = GetStructuralModificationFragment(na, su.Ordinal, j, new List<string>(new string[] { "NUCLEOSIDE_SUBSTITUTION", "NUCLEOSIDE SUBSTITUTION" }));
        //                    if (nucleoside_smod_fragment == null)
        //                    //no nucleoside modification found at this position, so proceed to build up sugar-base
        //                    {

        //                        //YP first connect base and sugar at _R90
        //                        //this is accomplished by first deleting the star atoms (_R90 atoms) on base and sugar
        //                        //Ideally a stereo bond (if found) between either sugar's or base's star atom and its parent should be kept
        //                        //and then used to connect star atom's respective parents to each other
        //                        //however I haven't figured out how to preserve such bond, so currently if there is a stereochemistry, it's lost
        //                        int base_r90_atom_idx = na_nucleobase.Fragment.getStarAtomIndex("_R90") - 1;
        //                        int sugar_r90_parent_stereo_type = 0;
        //                        IndigoObject sugar_r90_atom = sugar_component_mol.getAtom(sugar_r90_atom_idx);
        //                        IndigoObject base_r90_atom = base_component_mol.getAtom(base_r90_atom_idx);


        //                        //IndigoObject modified_sugar_component_mol = MoveStereoToRing(sugar_component_mol, sugar_r90_atom_idx+1);

        //                        bool r90_parent_bond_removed = false;
        //                        sugar_r91_index = na_sugar.Fragment.getStarAtomIndex("_R91") - 1;
        //                        sugar_r92_index = na_sugar.Fragment.getStarAtomIndex("_R92") - 1;
        //                        sugar_r92_atom = sugar_component_mol.getAtom(sugar_r92_index);


        //                        if (j == nucleobases.Count - 1)
        //                        //first processed nucleotide
        //                        {
        //                            if (su.isCircular)
        //                            {
        //                                last_nucleotide_circular_attachment_atom_idx = sugar_r92_index;
        //                            }
        //                            else
        //                            {
        //                                //remove _R92 (3') star atom
        //                                sugar_r92_atom.remove();
        //                            }


        //                        }

        //                        foreach (IndigoObject base_r90_parent_atom in base_r90_atom.iterateNeighbors())
        //                        {
        //                            base_sugar_connecting_atom_idx = base_r90_parent_atom.index();
        //                            IndigoObject base_r90_parent_bond = base_r90_parent_atom.bond();
        //                            if (base_r90_parent_bond.bondStereo() == 0)
        //                            {
        //                                base_r90_parent_bond.remove();
        //                                r90_parent_bond_removed = true;
        //                            }
        //                            else
        //                            {
        //                                connecting_bond_stereo = base_r90_parent_bond.bondStereo();
        //                            }
        //                            base_r90_atom.remove();
        //                            break;
        //                        }
        //                        Tuple<int, bool> remembered_bond_stereo = new Tuple<int, bool>(0, false);
        //                        foreach (IndigoObject sugar_r90_parent_atom in sugar_r90_atom.iterateNeighbors())
        //                        {

        //                            sugar_base_connecting_atom_idx = sugar_r90_parent_atom.index();
        //                            sugar_r90_parent_stereo_type = sugar_r90_parent_atom.stereocenterType();
        //                            IndigoObject sugar_r90_parent_bond = sugar_r90_parent_atom.bond();
        //                            if (sugar_r90_parent_bond.bondStereo() == 0 && !r90_parent_bond_removed)
        //                            {
        //                                sugar_r90_parent_bond.remove();
        //                                r90_parent_bond_removed = true;
        //                            }
        //                            else
        //                            {
        //                                remembered_bond_stereo = RememberStereo(sugar_component_mol, sugar_base_connecting_atom_idx + 1, sugar_r90_atom_idx + 1);
        //                                connecting_bond_stereo = sugar_r90_parent_bond.bondStereo();
        //                            }

        //                            sugar_r90_atom.remove();
        //                            break;
        //                        }

        //                        base_sugar_mapping = base_component_mol.merge(sugar_component_mol);
        //                        //base_component_mol = RestoreStereoBond(base_component_mol,base_sugar_mapping.mapAtom(sugar_component_mol.getAtom(preserved_link_stereo.Item1-1)).index()+1, base_sugar_mapping.mapAtom(sugar_component_mol.getAtom(preserved_link_stereo.Item2-1)).index()+1, preserved_link_stereo.Item3);
        //                        base_sugar_mapping.mapAtom(sugar_component_mol.getAtom(sugar_base_connecting_atom_idx)).addBond(base_component_mol.getAtom(base_sugar_connecting_atom_idx), 1);
        //                        List<Tuple<int, int>> base_component_mol_bonds_before_layout = GetBondsStereo(base_component_mol);

        //                        if (opt == null || opt.Coord)
        //                        {
        //                            base_component_mol.layout();
        //                        }
        //                        List<Tuple<int, int>> base_component_mol_bonds_after_layout = GetBondsStereo(base_component_mol);
        //                        bool stereo_has_inverted = InvertedStereo(base_component_mol_bonds_before_layout, base_component_mol_bonds_after_layout);

        //                        base_component_mol = RestoreStereoBond(base_component_mol, base_sugar_mapping.mapAtom(sugar_component_mol.getAtom(sugar_base_connecting_atom_idx)).index() + 1, base_sugar_connecting_atom_idx + 1, remembered_bond_stereo.Item2, remembered_bond_stereo.Item1, stereo_has_inverted);

        //                        //base_component_mol = MoveStereoFromRings(base_component_mol);
        //                        sugar_r92_atom = base_sugar_mapping.mapAtom(sugar_component_mol.getAtom(sugar_r92_index));
        //                        sugar_r91_index = base_sugar_mapping.mapAtom(sugar_component_mol.getAtom(sugar_r91_index)).index();
        //                        /*
        //                         * 
        //                        if (make_modification)
        //                        {
        //                            modification_mol = base_component_mol;
        //                        }
        //                        */
        //                    }
        //                    else
        //                    //there is a nucleoside modification at this position, so use attach the modification instead of building up sugar,base
        //                    {
        //                        //found nucleoside modification, need to attach
        //                        IndigoObject nucleoside_smod_mol = getIndigo().loadQueryMolecule(nucleoside_smod_fragment.Molecule.Mol);

        //                        //assuming the order of connectors 5', 3'
        //                        //designate the attachment points for next component(or modification)
        //                        int three_prime_index = nucleoside_smod_fragment.Connectors[0].Snip.Item2 - 1;
        //                        int five_prime_index = nucleoside_smod_fragment.Connectors[0].Snip.Item1 - 1;

        //                        IndigoObject nucleoside_three_prime_atom = nucleoside_smod_mol.getAtom(three_prime_index);
        //                        IndigoObject nucleoside_five_prime_atom = nucleoside_smod_mol.getAtom(five_prime_index);

        //                        //here adding an "attachment" atom R to the three and five prime connection atoms
        //                        //this way the component-assembly code that expects those, can be used to attach further components
        //                        //to the modification, rather than treating modification as a special case
        //                        IndigoObject nucleoside_smod_r91_atom = nucleoside_smod_mol.addAtom("R");
        //                        nucleoside_five_prime_atom.addBond(nucleoside_smod_r91_atom, 1);
        //                        //designate the attachment points for next component(or modification)
        //                        sugar_r91_index = nucleoside_smod_r91_atom.index();

        //                        //only create R atom on 3' if it's not the last nucleoside in a chain
        //                        if (j != nucleobases.Count - 1)
        //                        {
        //                            IndigoObject nucleoside_smod_r92_atom = nucleoside_smod_mol.addAtom("R");
        //                            nucleoside_three_prime_atom.addBond(nucleoside_smod_r92_atom, 1);
        //                            //designate the attachment points for next component(or modification)
        //                            sugar_r92_atom = nucleoside_smod_r92_atom;
        //                        }

        //                        base_component_mol = nucleoside_smod_mol;
        //                        if (opt == null || opt.Coord)
        //                        {
        //                            base_component_mol.layout();
        //                        }


        //                    }
        //                }
        //                else
        //                //there is a nucleotide modification at this position, so use attach the modification instead of building up sugar,base or nucleoside substitution
        //                {
        //                    //found nucleotide modification, need to attach
        //                    IndigoObject nucleotide_smod_mol = getIndigo().loadQueryMolecule(nucleotide_smod_fragment.Molecule.Mol);

        //                    //assuming the order of connectors  linker(P), 3'
        //                    //designate the attachment points for next component(or modification)

        //                    //YP SRS-381 strange that this is looking for second connector pair which would only exist for links
        //                    //commenting out for now and making it look at first connector pair instead
        //                    //int three_prime_index = nucleotide_smod_fragment.Connectors[1].Snip.Item1 - 1;
        //                    int three_prime_index = nucleotide_smod_fragment.Connectors[0].Snip.Item2 - 1;

        //                    //YP SRS-381 move this inside the if statement below
        //                    //int linker_attachment_atom_index = nucleotide_smod_fragment.Connectors[0].Snip.Item1 - 1;

        //                    IndigoObject nucleotide_three_prime_atom = nucleotide_smod_mol.getAtom(three_prime_index);

        //                    //YP SRS-381 move this inside the if statement below
        //                    //IndigoObject nucleotide_linker_attachment_atom = nucleotide_smod_mol.getAtom(linker_attachment_atom_index);

        //                    //here adding an "attachment" atom R to the three connection atoms
        //                    //this way the component-assembly code that expects those, can be used to attach further components
        //                    //to the modification, rather than treating modification as a special case
        //                    IndigoObject nucleotide_smod_sugar_r92_atom = nucleotide_smod_mol.addAtom("R");
        //                    nucleotide_three_prime_atom.addBond(nucleotide_smod_sugar_r92_atom, 1);

        //                    //YP SRS-381 move this inside the if statement below
        //                    //IndigoObject nucleotide_smod_linker_r92_atom = nucleotide_smod_mol.addAtom("R");
        //                    //nucleotide_linker_attachment_atom.addBond(nucleotide_smod_linker_r92_atom, 1);

        //                    //designate the attachment points for next component(or modification)
        //                    sugar_r92_atom = nucleotide_smod_sugar_r92_atom;

        //                    if (j != 0)
        //                    {
        //                        if (!su.isCircular)
        //                        {
        //                            int linker_attachment_atom_index = nucleotide_smod_fragment.Connectors[0].Snip.Item1 - 1;
        //                            IndigoObject nucleotide_linker_attachment_atom = nucleotide_smod_mol.getAtom(linker_attachment_atom_index);

        //                            IndigoObject nucleotide_smod_linker_r92_atom = nucleotide_smod_mol.addAtom("R");
        //                            nucleotide_linker_attachment_atom.addBond(nucleotide_smod_linker_r92_atom, 1);

        //                            chain_r92_atom = nucleotide_smod_linker_r92_atom;
        //                        }
        //                    }


        //                    base_component_mol = nucleotide_smod_mol;
        //                    if (opt == null || opt.Coord)
        //                    {
        //                        base_component_mol.layout();
        //                    }

        //                }

        //                if (j == nucleobases.Count - 1)
        //                {
        //                    //first processed nucleoside, making it the "chain"
        //                    chain_mol = getIndigo().loadQueryMolecule(base_component_mol.molfile());
        //                    chain_base_sugar_mapping = base_sugar_mapping;
        //                }
        //                else
        //                {
        //                    //connect to already built up chain at _R92 (3')
        //                    //rather than mapping linker's _R92 atom to the already built up chain
        //                    //use the fact that it will be the only query ("A") atom
        //                    bool r92_parent_bond_removed = false;
        //                    foreach (IndigoObject sugar_r92_parent_atom in sugar_r92_atom.iterateNeighbors())
        //                    {
        //                        sugar_chain_connecting_atom_idx = sugar_r92_parent_atom.index();
        //                        IndigoObject sugar_r92_parent_bond = sugar_r92_parent_atom.bond();
        //                        if (sugar_r92_parent_bond.bondStereo() == 0 && !r92_parent_bond_removed)
        //                        {
        //                            sugar_r92_parent_bond.remove();
        //                            r92_parent_bond_removed = true;
        //                        }
        //                        else
        //                        {
        //                            connecting_bond_stereo = sugar_r92_parent_bond.bondStereo();
        //                        }

        //                        //sugar_r92_atom.remove();
        //                        base_component_mol.removeAtoms(new int[] { sugar_r92_atom.index() });
        //                        break;
        //                    }
        //                    foreach (IndigoObject chain_r92_parent_atom in chain_r92_atom.iterateNeighbors())
        //                    {
        //                        chain_sugar_connecting_atom_idx = chain_r92_parent_atom.index();
        //                        IndigoObject na_r92_parent_bond = chain_r92_parent_atom.bond();
        //                        if (na_r92_parent_bond.bondStereo() == 0)
        //                        {
        //                            na_r92_parent_bond.remove();
        //                            r92_parent_bond_removed = true;
        //                        }
        //                        else
        //                        {
        //                            connecting_bond_stereo = na_r92_parent_bond.bondStereo();
        //                        }
        //                        chain_r92_atom.remove();
        //                        break;
        //                    }
        //                    chain_base_sugar_mapping = chain_mol.merge(base_component_mol);
        //                    chain_base_sugar_mapping.mapAtom(base_component_mol.getAtom(sugar_chain_connecting_atom_idx)).addBond(chain_mol.getAtom(chain_sugar_connecting_atom_idx), 1);
        //                    //YP SRS-381 nested if didn't work, so trying to just make sure that sugar_r91_index is not -1
        //                    //if (j != 0)
        //                    //{
        //                    //if (!su.isCircular)
        //                    if (sugar_r91_index != -1)
        //                    {
        //                        sugar_r91_index = chain_base_sugar_mapping.mapAtom(base_component_mol.getAtom(sugar_r91_index)).index();
        //                    }
        //                    //}
        //                    //chain_r92_atom = chain_base_sugar_mapping.mapAtom(chain_r92_atom);
        //                }
        //                //IndigoObject sugar_r91_atom = nucleotide_mol.getAtom(sugar_r91_index);
        //                //foreach (IndigoObject attachment_atom in base_sugar_mapping.mapAtom(sugar_component_mol.getAtom(sugar_base_connecting_atom_idx)).iterateNeighbors())
        //                //{
        //                //attachment_atom.bond().set
        //                //}


        //                //sugar_r91_index = chain_sugar_mapping.mapAtom(base_component_mol.getAtom(sugar_r91_index)).index();
        //                IndigoObject sugar_r91_atom = chain_mol.getAtom(sugar_r91_index);

        //                //IndigoObject sugar_r92_atom = base_sugar_mapping.mapAtom(sugar_component_mol.getAtom(sugar_r92_index));


        //                NAFragment terminal_smod_fragment = null;
        //                if (j == nucleobases.Count - 1)
        //                //last nucleotide, check if there is a terminal structural modification
        //                {
        //                    terminal_smod_fragment = GetStructuralModificationFragment(na, su.Ordinal, nucleobases.Count, new List<string>(new string[] { "NUCLEOSIDE_SUBSTITUTION" }));

        //                    /*This is here for historical reasons if implicit modifications should be processed as part of asChemical
        //                     * right now they are moved out of asChemical as a separate routine
        //                     * 
        //                    if (make_modification && terminal_smod_fragment == null)
        //                    //if this is supposed to be implicit modification (non-standard sugar present)
        //                    //create modification object and add to NA modifications list. No linker is needed since it's the last nuceloside
        //                    {
        //                        Tuple<IndigoObject, int> modification_connector = GetR91ParentAtomIndex(modification_mol);
        //                        na.ProcessStructuralModificationGroup(MakeImplicitNAStructuralModification(na, su, j, "NUCLEOTIDE SUBSTITUTION POINT", state, CreateAssembledFragment(modification_connector.Item1.molfile(), new List<int>() { modification_connector.Item2 + 1, 0 }, new List<String>() { "_R91", "0" }, state)), state);
        //                        //NAModification implicit_nucleoside_modification = new NAModification
        //                    }
        //                    //need to check if there are terminal modifications that would be attached to r91 sugar atom
        //                    //Tuple<String, String> terminal_smod_listing = GetListing(su.Ordinal, nucleobases.Count, na.StructuralModifications_listing);
        //                    //if (terminal_smod_listing != null)
        //                    //Tuple<String, String> terminal_smod_listing = 
        //                    */

        //                    if (terminal_smod_fragment != null)
        //                    {
        //                        //found terminal modification, need to attach
        //                        //NAFragment terminal_smod_fragment = FragmentResolve(terminal_smod_listing.Item1, state);

        //                        IndigoObject terminal_smod_mol = getIndigo().loadQueryMolecule(terminal_smod_fragment.Molecule.Mol);
        //                        //assuming here that because this is terminal smod (by nucleobases.Count, last nucleotide), the only possible choice is that the only connector is _R91 (connecting to _R91(5') sugar)
        //                        //int smod_r91_index = terminal_smod_fragment.Connectors[0].Snip.Item1 - 1;
        //                        int smod_p_index = -1;
        //                        int smod_o_index = -1;
        //                        if (PartiallyConnectedLinkExists(na, su.Ordinal, nucleobases.Count))
        //                        {
        //                            //partially_connected_links[partially_connected_links.Count - 1] = new Tuple<NALink, Tuple<int, int>>(partially_connected_links[partially_connected_links.Count - 1].Item1, new Tuple<int, int>(smod_p_index, smod_o_index));
        //                            smod_p_index = GetPartiallyConnectedLink(GetNALink(na, su.Ordinal, nucleobases.Count)).Item2.Item1;
        //                            smod_o_index = GetPartiallyConnectedLink(GetNALink(na, su.Ordinal, nucleobases.Count)).Item2.Item2;
        //                            //this link already built into another chain, thus already part of the molecule, thus we need to operate on the built up molecule from here
        //                            chain_mol = na_mol.clone();
        //                            na_mol = getIndigo().loadQueryMolecule("");
        //                        }
        //                        else
        //                        {
        //                            partially_connected_links.Add(new Tuple<NALink, Tuple<int, int>>(GetNALink(na, su.Ordinal, nucleobases.Count), new Tuple<int, int>(terminal_smod_fragment.Connectors[1].Snip.Item1 - 1, terminal_smod_fragment.Connectors[1].Snip.Item2 - 1)));
        //                            smod_p_index = terminal_smod_fragment.Connectors[0].Snip.Item1 - 1;
        //                            smod_o_index = terminal_smod_fragment.Connectors[0].Snip.Item2 - 1;
        //                            chain_mol = terminal_smod_mol;
        //                        }


        //                        //commenting this out because if the last (first to be processed in this case) nucleotide is a mod then it becomes a chain, as nothing alse has been added
        //                        //chain_smod_mapping = chain_mol.merge(terminal_smod_mol);
        //                        //chain_mol = terminal_smod_mol;

        //                        foreach (IndigoObject p_neighbor in chain_mol.getAtom(smod_p_index).iterateNeighbors())
        //                        {
        //                            if (p_neighbor.bond().bondOrder() == 1 && p_neighbor.bond().bondStereo() == 0 && p_neighbor.symbol() == "O" && p_neighbor.degree() == 1)
        //                            {
        //                                //p_neighbor.remove();
        //                                linker_sugar_connecting_atom_idx = p_neighbor.index();
        //                                break;
        //                            }
        //                        }
        //                        //linker_sugar_connecting_atom_idx = smod_p_index;
        //                        chain_r92_atom = chain_mol.getAtom(linker_sugar_connecting_atom_idx);

        //                        //chain_smod_mapping.mapAtom(terminal_smod_mol.getAtom(smod_r91_index)).addBond(chain_mol.getAtom(sugar_linker_connecting_atom_idx), 1);
        //                        /*foreach (IndigoObject p_neighbor in chain_smod_mapping.mapAtom(terminal_smod_mol.getAtom(smod_r91_index)).iterateNeighbors())
        //                        {
        //                            if (p_neighbor.bond().bondOrder() == 1 && p_neighbor.bond().bondStereo() == 0 && p_neighbor.symbol() == "O" && p_neighbor.degree() == 1)
        //                            {
        //                                p_neighbor.remove();
        //                                break;
        //                            }
        //                        }
        //                        */
        //                    }

        //                }

        //                //remove _R91 star atom (5')
        //                //sugar_r91_atom.remove();
        //                //sugar-linker attachment code

        //                if (nucleotide_smod_fragment == null && terminal_smod_fragment == null)
        //                {
        //                    //attach linker at _R91
        //                    PNAComponent na_sugar_linker = PNAComponent.from(GetLinkageAtPosition(su, j + 1), state);
        //                    if (na_sugar_linker.Fragment != null)
        //                    {
        //                        IndigoObject linker_component_mol = getIndigo().loadQueryMolecule(na_sugar_linker.Fragment.Molecule.Mol);
        //                        int linker_r92_index = na_sugar_linker.Fragment.getStarAtomIndex("_R92") - 1;
        //                        linker_r92_atom = linker_component_mol.getAtom(linker_r92_index);

        //                        //if (j < su.SugarLinkers.Count)
        //                        //if (j < nucleobases.Count - 1)
        //                        //{
        //                        //YP connect the sugar linker to the sugar that it already connected to the nucleobase
        //                        //(nucleobases[j]+su.Sugar[j])+su.SugarLinkers[j]

        //                        int linker_r91_index = na_sugar_linker.Fragment.getStarAtomIndex("_R91") - 1;

        //                        IndigoObject linker_r91_atom = linker_component_mol.getAtom(linker_r91_index);


        //                        bool r91_bond_removed = false;
        //                        foreach (IndigoObject sugar_r91_parent_atom in sugar_r91_atom.iterateNeighbors())
        //                        {
        //                            sugar_linker_connecting_atom_idx = sugar_r91_parent_atom.index();
        //                            IndigoObject sugar_r91_parent_bond = sugar_r91_parent_atom.bond();
        //                            if (sugar_r91_parent_bond.bondStereo() == 0)
        //                            {
        //                                sugar_r91_parent_bond.remove();
        //                                r91_bond_removed = true;
        //                            }
        //                            else
        //                            {
        //                                connecting_bond_stereo = sugar_r91_parent_atom.bondStereo();
        //                            }
        //                            sugar_r91_atom.remove();
        //                            break;
        //                        }

        //                        foreach (IndigoObject linker_r91_parent_atom in linker_r91_atom.iterateNeighbors())
        //                        {
        //                            linker_sugar_connecting_atom_idx = linker_r91_parent_atom.index();
        //                            IndigoObject linker_r91_parent_bond = linker_r91_parent_atom.bond();
        //                            if (linker_r91_parent_bond.bondStereo() == 0 && !r91_bond_removed)
        //                            {
        //                                linker_r91_parent_bond.remove();
        //                                r91_bond_removed = true;
        //                            }
        //                            else
        //                            {
        //                                connecting_bond_stereo = linker_r91_parent_bond.bondStereo();
        //                            }

        //                            linker_r91_atom.remove();
        //                            break;
        //                        }
        //                        chain_linker_mapping = chain_mol.merge(linker_component_mol);
        //                        chain_linker_mapping.mapAtom(linker_component_mol.getAtom(linker_sugar_connecting_atom_idx)).addBond(chain_mol.getAtom(sugar_linker_connecting_atom_idx), 1);

        //                        //chain_mol.layout();
        //                        //chain_mol = MoveStereoFromRings(chain_mol);

        //                        if (j == 0)
        //                        //if first nucleotide, need to turn R# connecting atom to Oxygen
        //                        {
        //                            //if circular need to connect to last nucleotide of the chain, otherwise cap linkage with O
        //                            if (su.isCircular)
        //                            {
        //                                //here we're just assuming that we have an R atom hanging off of each of the ends of the chain
        //                                //given this assumption we just need to connect the parents of those R atoms to each other and remove R atoms themselves
        //                                foreach (IndigoObject r_atom1 in chain_mol.iterateAtoms())
        //                                {
        //                                    if (r_atom1.symbol().Equals("R"))
        //                                    {
        //                                        int r_atom1_index = r_atom1.index();
        //                                        IndigoObject r_atom1_parent = null;
        //                                        foreach (IndigoObject r_atom1_neighbor in r_atom1.iterateNeighbors())
        //                                        {
        //                                            //R atom presumably has only one neighbor/parent atom that it is bonded to
        //                                            r_atom1_parent = r_atom1_neighbor;
        //                                        }
        //                                        r_atom1.remove();
        //                                        foreach (IndigoObject r_atom2 in chain_mol.iterateAtoms())
        //                                        {
        //                                            if (r_atom2.symbol().Equals("R") && r_atom2.index() != r_atom1_index)
        //                                            {
        //                                                IndigoObject r_atom2_parent = null;
        //                                                foreach (IndigoObject r_atom2_neighbor in r_atom2.iterateNeighbors())
        //                                                {
        //                                                    //R atom presumably has only one neighbor/parent atom that it is bonded to
        //                                                    r_atom2_parent = r_atom2_neighbor;
        //                                                }
        //                                                r_atom2.remove();
        //                                                //create bond between parents
        //                                                r_atom1_parent.addBond(r_atom2_parent, 1);
        //                                            }
        //                                        }
        //                                        break;
        //                                    }
        //                                }
        //                            }
        //                            else
        //                            {
        //                                chain_mol = CapLinkage(chain_mol, chain_linker_mapping.mapAtom(linker_component_mol.getAtom(linker_sugar_connecting_atom_idx)));
        //                            }

        //                        }
        //                        //}    
        //                    }
        //                    else
        //                    {
        //                        //linker is not present
        //                        if (j > 0)
        //                        //if anything but first nucleotide, error it, as linkers are required
        //                        {
        //                            throw new Exception("Nucleic Acid cannot be converted, missing linkage at position " + (j + 1) + " on chain " + (i + 1));
        //                        }
        //                        else
        //                        {
        //                            //need to error here if circular as linkage is required on first nucleotide if circular
        //                            //first nucleotide with missing linkage
        //                            //remove the r91 R atom
        //                            if (su.isCircular)
        //                            {
        //                                throw new Exception("Circular subunit " + su.Ordinal + " is missing required phosphate at first position");
        //                            }
        //                            else
        //                            {
        //                                sugar_r91_atom.remove();
        //                            }

        //                        }
        //                    }
        //                }
        //                else
        //                {
        //                    //nucleotide_smod_fragment !=null or terminal_smod_fragment != null

        //                }


        //                if (j > 0 && terminal_smod_fragment == null)
        //                //if not the first nucleotide
        //                {
        //                    //need to remember which one is the linker's _R92 atom to which the next nucleotide will be attached
        //                    //IndigoObject nucleotide_r92_atom = nucleotide_mapping.mapAtom(linker_r92_atom);
        //                    chain_r92_atom = chain_linker_mapping.mapAtom(linker_r92_atom);
        //                    /*
        //                     * if (j == 0)
        //                    {
        //                        //if first nucleotide, create the chain_mol
        //                        chain_mol = getIndigo().loadQueryMolecule(nucleotide_mol.molfile());
        //                        chain_r92_atom = nucleotide_r92_atom;
        //                    }
        //                    else
        //                    {
        //                        //here chain_mol would have been created so just merge it with the newly built nucleotide
        //                        chain_mapping = chain_mol.merge(nucleotide_mol);
        //                        chain_r92_atom = chain_mapping.mapAtom(nucleotide_r92_atom);
        //                    }
        //                    */



        //                }                        
        //                if (chain_mol.countAtoms() > MAX_ATOMS)
        //                {
        //                    throw new Exception("Nucleic Acid is too large to be interpretted as chemical");
        //                }
        //                //YP This code generates layout every time a new nucleotide is added
        //                if (opt == null || opt.Coord)
        //                {
        //                    if (!Task.Run(() => chain_mol.layout()).Wait(layout_timeout))
        //                    {
        //                        throw new TimeoutException("2D Coordinates took too long to generate.");
        //                    }
        //                }
                        


        //                //IndigoObject assembled_mol = getIndigo().loadMolecule(base_component_mol.smiles());
        //                //assembled_mol.standardize();
        //                //assembled_mol.layout();
                        

        //            }
        //            if (i == 0)
        //            {
        //                na_mol = getIndigo().loadQueryMolecule(chain_mol.molfile());
        //            }
        //            else
        //            {
        //                na_mol.merge(chain_mol);
        //            }

        //        });
        //        //YP This code would generate layout once the entire chain is put together
        //        /*
        //        if (opt == null || opt.Coord)
        //        {
        //            if (!Task.Run(() => na_mol.layout()).Wait(layout_timeout))
        //            {
        //                throw new TimeoutException("2D Coordinates took too long to generate.");
        //            }
        //            //na_mol.layout();
        //        }
        //        */
        //        na_mol = MoveStereoFromRings(na_mol);
        //        SdfRecord sdfmol = SdfRecord.FromString(na_mol.molfile());
        //        sdfmol.AddField("UNII", na.UNII);
        //        sdfmol.AddField("SUBSTANCE_ID", na.UNII);
        //        sdfmol.AddField("STRUCTURE_ID", na.UNII);
        //        return sdfmol;
        //        //SdfRecords.Add(sdfmol);
        //        //return SdfRecords;
        //    }
        //    catch (Exception e)
        //    {
        //        //if (e.Message == "2D Coordinates took too long to generate.")
        //        //{
        //        //    throw e;
        //        //}
        //        System.Console.WriteLine(e.Message);
        //        System.Console.WriteLine(e.StackTrace);
        //        throw e;
        //    }

        //    return null;

        //}



        //YP If stereo of ALL bonds are inverted (5->6 or 6->5) return true
        private static bool InvertedStereo(List<Tuple<int, int>> bonds_stereo1, List<Tuple<int, int>> bonds_stereo2)
        {
            bool returnvalue = false;
            for (int i = 0; i < bonds_stereo1.Count(); i++)
            {
                if (bonds_stereo1[i].Item2 == 5 && bonds_stereo2[i].Item2 == 6)
                {
                    returnvalue = true;
                }
                else if ((bonds_stereo1[i].Item2 == 5 && bonds_stereo2[i].Item2 == 5) || (bonds_stereo1[i].Item2 == 6 && bonds_stereo2[i].Item2 == 6))
                {
                    return false;
                }
            }
            return returnvalue;
        }
        public static IndigoObject CapLinkage(IndigoObject in_molecule, IndigoObject Patom)
        {
            foreach (IndigoObject neighbor in Patom.iterateNeighbors())
            {
                if (neighbor.symbol() == "R")
                {
                    neighbor.remove();
                    break;
                }
            }
            Patom.addBond(in_molecule.addAtom("O"), 1);
            return in_molecule;
        }

        public static bool ExplicitModificationExistsAtSite(NucleicAcid na, int subunit_id, int position)
        {
            bool returnvalue = false;

            foreach (NAStructuralModificationGroup mod in na.NAModifications)
            {
                returnvalue = mod.NucleotideSites.Exists(x => x.Subunit.Ordinal == subunit_id + 1 && x.Position == position);
                if (returnvalue) { return true; }
            }

            foreach (NALink link in na.Links)
            {
                returnvalue = link.Sites.Exists(x => x.Subunit.Ordinal == subunit_id + 1 && x.Position == position);
                if (returnvalue) { return true; }
            }

            return returnvalue;
        }

        //This creates Implicit modifications.
        //Implicit modifications are modifications not explicitly mentioned in the JSON as modifications/substitutions/links
        //but result from nucleotides/nucleosides that incorporate non-standard sugar or sugar-linker
        public static void ProcessImplicitModifications(NucleicAcid na, PolymerBaseReadingState state)
        {
            
            IndigoObject na_mol = getIndigo().loadMolecule("");
            Nucleoside nucleoside = null;
            Nucleotide nucleotide = null;
            na.Subunits.ForEachWithIndex((su, i) =>
            {
                //for testing purposes only
                //su.isCircular = false;
                NAChain chain = null;
                List<char> nucleobases = new List<char>();
                nucleobases.AddRange(su.Sequence.ToString());

                for (int j = 0; j <= nucleobases.Count - 1; j += 1)
                {
                    Tuple<IndigoObject, Tuple<int, int>> modification_connectors;

                    //if there is an explicit modification (or link) at this site, it will override the implicit modification, so no need to create implicit modification
                    if (ExplicitModificationExistsAtSite(na, i, j))
                    {
                        //YP SRS-394. Here assembling nucleotide(side) if the modification is not a nucleotide(side) but a nucleobase for example
                        foreach (NAStructuralModificationGroup smod in na.NAModifications)
                        {
                            for (int k = 0; k < smod.NucleotideSites.Count; k += 1)
                            {
                                if (smod.NucleotideSites[k].Subunit.Ordinal == i+1 && smod.NucleotideSites[k].Position == j && smod.Modification.ModificationType=="NUCLEOSIDE BASE SUBSTITUTION")
                                {
                                    //YP SRS-393 Assemble nucleotide here and overwrite smod.Fragment with it, along with code
                                    PNAComponent mod_current_sugar_component = PNAComponent.from(GetSugarAtPosition(su, j + 1), state);
                                    PNAComponent mod_current_linker_component = PNAComponent.from(GetLinkageAtPosition(su, j + 1), state);
                                    PNAComponent mod_current_nucleobase_component = PNAComponent.from(smod.Modification.Fragment.UNII, state);
                                    if (mod_current_sugar_component.Fragment == null)
                                    {
                                        throw new Exception("Nucleic Acid cannot be converted, missing definition for fragment " + mod_current_sugar_component.sym + " representing sugar at position " + (j + 1) + " on chain " + (i + 1));
                                    }

                                    bool linker_absent = mod_current_linker_component.Fragment == null;
                                    //Nucleoside mod_nucleoside = new Nucleoside(new NAModNucleobase(smod.Modification), new NASugar(mod_current_sugar_component));
                                    Nucleoside mod_nucleoside = new Nucleoside(new NANucleobase(mod_current_nucleobase_component), new NASugar(mod_current_sugar_component));
                                    if (linker_absent)
                                    {
                                        modification_connectors = GetNucleosideConnectors(mod_nucleoside);
                                    }
                                    else
                                    {
                                        nucleotide = new Nucleotide(mod_nucleoside, (mod_current_linker_component.Fragment != null) ? new NASugarLinker(mod_current_linker_component) : null);
                                        //CapMoleculeEnds(nucleotide);
                                        //YP GetNucleotideConnectors requires R atoms, which may be absent here, so need to rethink this
                                        //GetNucleotideConnectors doesn't cap the P and O, but CapMoleculeEnds removes R groups which are required for GetNucleotideConnectors
                                        //Need to be able to either cap P and O in GetNucleotideConnectors or ...?
                                        //Also need to make GetNucleotideConnectors P/O agnostic as they could be reversed as with inverse Ribose sugar in mRNA capping, see GetNucleosideConnectors
                                        modification_connectors = GetNucleotideConnectors(nucleotide.molecule);

                                    }
                                    String mod_type = "";

                                    UnregisterFragment(na, smod.Modification.Fragment);

                                    mod_type = "Nucleotide substitution site";
                                    smod.ModificationType = mod_type;
                                    smod.Modification.Fragment = CreateAssembledFragment(
                                                na, modification_connectors.Item1.molfile(), new List<int>() { modification_connectors.Item2.Item1 + 1, modification_connectors.Item2.Item2 + 1 }, new List<String>() { "0", "0" }, state);
                                    IndigoObject newmol = getIndigo().loadMolecule(smod.Modification.Fragment.Molecule.Mol);
                                    getIndigo().setOption("standardize-charges", true);
                                    newmol.standardize();
                                    //newmol.layout();
                                    
                                    smod.Modification.Fragment.Molecule = new SDFUtil.NewMolecule(newmol.molfile());

                                    RegisterFragment(na, smod.Modification.Fragment);

                                    //na.ProcessStructuralModificationGroup(
                                    //    MakeImplicitNAStructuralModification(
                                    //        na, su, j, mod_type, state, CreateAssembledFragment(
                                    //            modification_connectors.Item1.molfile(), new List<int>() { modification_connectors.Item2.Item1 + 1, modification_connectors.Item2.Item2 + 1 }, new List<String>() { "0", "0" }, state)
                                    //            ),
                                    //    state);
                                    //returnvalue = smod.Modification.Fragment;
                                    break;
                                    
                                }
                            }
                        }
                        continue;
                    }
                    PNAComponent current_nucleobase_component = PNAComponent.from(nucleobases[j].ToString(), state);
                    string sugar_identifier = GetSugarAtPosition(su, j + 1);
                    if (sugar_identifier == "")
                    { 
                        throw new Exception("Nucleic Acid cannot be converted. No sugar specified for chain " + su.Ordinal + " position " + (j + 1));
                    }
                    PNAComponent current_sugar_component = PNAComponent.from(sugar_identifier, state);
                    if (current_sugar_component.Fragment == null && j > 0)
                    {
                        throw new Exception("Nucleic Acid cannot be converted, missing definition for fragment " + sugar_identifier + " representing sugar at position " + (j + 1) + " on chain " + (i + 1));
                    }
                    String linker_fragment_name = GetLinkageAtPosition(su, j + 1);
                    PNAComponent current_linker_component = PNAComponent.from(linker_fragment_name, state);
                    //bool is_mod_nucleoside = (na_sugar_linker.Fragment == null);
                    
                    if (current_linker_component.Fragment == null && j > 0)
                    {
                        throw new Exception("Nucleic Acid cannot be converted, missing definition for fragment " + linker_fragment_name + " representing sugar linker at position " + (j + 1) + " on chain " + (i + 1));
                    }
                    if (!IsSugarStandard(current_sugar_component.sym) || !IsLinkageStandard(current_linker_component.sym))
                    {
                        nucleoside = new Nucleoside(new NANucleobase(current_nucleobase_component), new NASugar(current_sugar_component));
                        if (current_linker_component.Fragment ==null)
                        {
                            modification_connectors = GetNucleosideConnectors(nucleoside);
                        }
                        else
                        {
                            nucleotide = new Nucleotide(nucleoside, new NASugarLinker(current_linker_component));
                            //YP SRS-394. Need to cap Phosphate properly here as it ends up missing an OH in the SPL
                            //CapMoleculeEnds(nucleotide);
                            modification_connectors = GetNucleotideConnectors(nucleotide.molecule);
                        }
                        //CapMoleculeEnds(nucleotide.molecule);
                        //CapMoleculeEnds(modification_connectors.Item1);
                        String mod_type = "";

                        mod_type = "Nucleotide substitution site";
                        na.ProcessStructuralModificationGroup(
                            MakeImplicitNAStructuralModification(
                                na, su, j, mod_type, state, CreateAssembledFragment(
                                    na, modification_connectors.Item1.molfile(), new List<int>() { modification_connectors.Item2.Item1 + 1, modification_connectors.Item2.Item2 + 1 }, new List<String>() { "0", "0" }, state)
                                    ),
                            state);
                    }


                    if (su.isCircular)
                    {
                        //build the first nucleotide
                        PNAComponent first_na_nucleobase = PNAComponent.from(nucleobases[0].ToString(), state);
                        if (first_na_nucleobase.Fragment == null)
                        {
                            throw new Exception("Nucleic Acid cannot be converted, missing base at first position on chain " + (i + 1));
                        }

                        PNAComponent first_na_sugar = PNAComponent.from(GetSugarAtPosition(su, 1), state);
                        if (first_na_sugar.Fragment == null)
                        {
                            throw new Exception("Nucleic Acid cannot be converted, missing sugar at first position on chain " + (i + 1));
                        }

                        PNAComponent first_na_sugar_linker = PNAComponent.from(GetLinkageAtPosition(su, 1), state);

                        //need to throw an error here if first_na_sugar_linker is null, because linker on first nucleotide is required for circular chains
                        if (first_na_sugar_linker.Fragment == null)
                        {
                            throw new Exception("Nucleic Acid cannot be converted, missing linkage at first position on circular chain " + (i + 1));
                        }

                        Nucleoside first_nucleoside = new Nucleoside(new NANucleobase(first_na_nucleobase), new NASugar(first_na_sugar));
                        Nucleotide first_nucleotide = new Nucleotide(first_nucleoside, new NASugarLinker(first_na_sugar_linker));

                        //build the last nucleotide
                        PNAComponent last_na_nucleobase = PNAComponent.from(nucleobases[nucleobases.Count - 1].ToString(), state);
                        if (last_na_nucleobase.Fragment == null)
                        {
                            throw new Exception("Nucleic Acid cannot be converted, missing base at last position on chain " + (i + 1));
                        }

                        PNAComponent last_na_sugar = PNAComponent.from(GetSugarAtPosition(su, nucleobases.Count), state);
                        if (last_na_sugar.Fragment == null)
                        {
                            throw new Exception("Nucleic Acid cannot be converted, missing sugar at last position on chain " + (i + 1));
                        }

                        PNAComponent last_na_sugar_linker = PNAComponent.from(GetLinkageAtPosition(su, nucleobases.Count), state);
                        //need to throw an error here if last_na_sugar_linker is null, because linker on first nucleotide is required for circular chains
                        if (last_na_sugar_linker.Fragment == null)
                        {
                            throw new Exception("Nucleic Acid cannot be converted, missing linkage at last position on chain " + (i + 1));
                        }

                        Nucleoside last_nucleoside = new Nucleoside(new NANucleobase(last_na_nucleobase), new NASugar(last_na_sugar));
                        Nucleotide last_nucleotide = new Nucleotide(last_nucleoside, new NASugarLinker(last_na_sugar_linker));

                        Tuple<IndigoObject, Tuple<int, int>> first_nucleotide_connectors = GetNucleotideConnectors(first_nucleotide.molecule);
                        Tuple<IndigoObject, Tuple<int, int>> last_nucleotide_connectors = GetNucleotideConnectors(last_nucleotide.molecule);

                        string mod_type = "";

                        //connet first_base_component_mol and last_base_component_mol
                        //specifically connect 3' of last nucleotide to 5' of first nucleotide
                        //this corresponds to connecting atom index last_nucleotide_connectors.Item2.Item2 of last_nucleotide_connectors.Item1 to first_nucleotide_connectors.Item2.Item1 of first_nucleotide_connectors.Item1

                        //create modification molecule
                        IndigoObject circular_link_mod_fragment = first_nucleotide.molecule;
                        IndigoObject circular_link_mod_mapping = first_nucleotide.molecule.merge(last_nucleotide.molecule);
                        //YP need to check if star atoms will be left dangling since we're not using them here but directly connecting their parents
                        circular_link_mod_mapping.mapAtom(last_nucleotide.molecule.getAtom(last_nucleotide_connectors.Item2.Item2)).addBond(circular_link_mod_fragment.getAtom(first_nucleotide_connectors.Item2.Item1), 1);
                        circular_link_mod_fragment.layout();
                        mod_type = "Circular Link";

                        NAFragment link_frag = CreateAssembledFragment(na,circular_link_mod_fragment.molfile(), state);

                        //YP once we have an example of circular nucleic acid, this will need to be tested and possibly modified to satisfy the requirements of SRS-424
                        //YP - Canonical atom/connector ordering
                        link_frag.AddConnectorsPair(link_frag.getCanonicalAtoms()[first_nucleotide_connectors.Item2.Item2] + 1, 0, "implicit_fragment");
                        link_frag.AddConnectorsPair(link_frag.getCanonicalAtoms()[circular_link_mod_mapping.mapAtom(last_nucleotide.molecule.getAtom(last_nucleotide_connectors.Item2.Item1)).index()] + 1, 0, "implicit_fragment");

                        //YP - Original atom/connector ordering
                        //link_frag.AddConnectorsPair(first_nucleotide_connectors.Item2.Item2 + 1, 0, "implicit_fragment");
                        //link_frag.AddConnectorsPair(circular_link_mod_mapping.mapAtom(last_nucleotide.molecule.getAtom(last_nucleotide_connectors.Item2.Item1)).index() + 1, 0, "implicit_fragment");

                        link_frag.Connectors.ForEachWithIndex((c, k) => c.Id = k);

                        NALink link = new NALink(state.RootObject) { LinkType = null };
                        link.Linker = link_frag;
                        TraceUtils.WriteUNIITrace(TraceEventType.Information, na.UNII, null, "Circular link present");

                        link.Sites.Add(new NASite(state.RootObject, "NUCLEOTIDE SUBSTITUTION POINT") { Subunit = su, Position = 0, ConnectorRef = link.Linker.Connectors[0] });
                        link.Sites.Add(new NASite(state.RootObject, "NUCLEOTIDE SUBSTITUTION POINT") { Subunit = su, Position = nucleobases.Count - 1, ConnectorRef = link.Linker.Connectors[1] });
                        link.IsImplicit = true;
                        link.Amount = new Amount();
                        link.Amount.AdjustAmount();
                        na.Links.Add(link);
                        na.NAFragments.Add(link_frag);

                    }
                }
            });

        }

        public static void ProcessImplicitModifications_old(NucleicAcid na, PolymerBaseReadingState state)
        {
            List<SdfRecord> SdfRecords = new List<SdfRecord>();
            IndigoObject chain_mol = getIndigo().loadMolecule("");
            IndigoObject na_mol = getIndigo().loadMolecule("");
            IndigoObject chain_base_sugar_mapping = null;
            IndigoObject chain_mapping = null;
            IndigoObject chain_sugar_mapping = null;
            IndigoObject chain_linker_mapping = null;
            IndigoObject first_chain_linker_mapping = null;
            IndigoObject last_chain_linker_mapping = null;

            IndigoObject chain_smod_mapping = null;
            getIndigo().setOption("standardize-stereo", true);

            na.Subunits.ForEachWithIndex((su, i) =>
            {



                List<char> nucleobases = new List<char>();
                nucleobases.AddRange(su.Sequence.ToString());
                IndigoObject chain_r92_atom = null;


                //for testing purposes only
                //su.isCircular = false;

                for (int j = nucleobases.Count - 1; j >= 0; j -= 1)
                {
                    //if there is an explicit modification (or link) at this site, it will override the implicit modification, so no need to create it
                    if (ExplicitModificationExistsAtSite(na, i, j))
                    {
                        continue;
                    }
                    //YP connect nucleobase with sugar
                    PNAComponent na_nucleobase = PNAComponent.from(nucleobases[j].ToString(), state);
                    PNAComponent na_sugar = PNAComponent.from(GetSugarAtPosition(su, j + 1), state);
                    PNAComponent na_sugar_linker = PNAComponent.from(GetLinkageAtPosition(su, j + 1), state);
                    bool is_mod_nucleoside = (na_sugar_linker.Fragment == null);
                    if (na_sugar_linker.Fragment == null && j > 0)
                    {
                        throw new Exception("Nucleic Acid cannot be converted, missing linkage at position " + (j + 1) + " on chain " + (i + 1));
                    }
                    if (!IsSugarStandard(na_sugar.sym) || !IsLinkageStandard(na_sugar_linker.sym))
                    {

                        IndigoObject base_component_mol = getIndigo().loadQueryMolecule(na_nucleobase.Fragment.Molecule.Mol);

                        int sugar_r90_atom_idx = na_sugar.Fragment.getStarAtomIndex("_R90") - 1;
                        IndigoObject sugar_component_mol = getIndigo().loadQueryMolecule(na_sugar.Fragment.Molecule.Mol);

                        int base_sugar_connecting_atom_idx = -1;
                        int sugar_base_connecting_atom_idx = -1;
                        int sugar_linker_connecting_atom_idx = -1;
                        int linker_sugar_connecting_atom_idx = -1;
                        int sugar_chain_connecting_atom_idx = -1;
                        int chain_sugar_connecting_atom_idx = -1;
                        int connecting_bond_stereo = 0;
                        int sugar_r91_index = -1;
                        int sugar_r92_index = -1;
                        IndigoObject base_sugar_mapping = null;
                        IndigoObject nucleotide_mapping = null;
                        //int sugar_base_bond_stereo = 0;
                        IndigoObject sugar_r92_atom = null;
                        IndigoObject linker_r92_atom = null;
                        IndigoObject modification_mol = null;

                        int base_r90_atom_idx = na_nucleobase.Fragment.getStarAtomIndex("_R90") - 1;
                        int sugar_r90_parent_stereo_type = 0;
                        IndigoObject sugar_r90_atom = sugar_component_mol.getAtom(sugar_r90_atom_idx);
                        IndigoObject base_r90_atom = base_component_mol.getAtom(base_r90_atom_idx);

                        bool r90_parent_bond_removed = false;
                        sugar_r91_index = na_sugar.Fragment.getStarAtomIndex("_R91") - 1;
                        sugar_r92_index = na_sugar.Fragment.getStarAtomIndex("_R92") - 1;
                        sugar_r92_atom = sugar_component_mol.getAtom(sugar_r92_index);

                        foreach (IndigoObject base_r90_parent_atom in base_r90_atom.iterateNeighbors())
                        {
                            base_sugar_connecting_atom_idx = base_r90_parent_atom.index();
                            IndigoObject base_r90_parent_bond = base_r90_parent_atom.bond();
                            if (base_r90_parent_bond.bondStereo() == 0)
                            {
                                base_r90_parent_bond.remove();
                                r90_parent_bond_removed = true;
                            }
                            else
                            {
                                connecting_bond_stereo = base_r90_parent_bond.bondStereo();
                            }
                            base_r90_atom.remove();
                            break;
                        }
                        Tuple<int, bool> remembered_bond_stereo = new Tuple<int, bool>(0, false);
                        foreach (IndigoObject sugar_r90_parent_atom in sugar_r90_atom.iterateNeighbors())
                        {

                            sugar_base_connecting_atom_idx = sugar_r90_parent_atom.index();
                            sugar_r90_parent_stereo_type = sugar_r90_parent_atom.stereocenterType();
                            IndigoObject sugar_r90_parent_bond = sugar_r90_parent_atom.bond();
                            if (sugar_r90_parent_bond.bondStereo() == 0 && !r90_parent_bond_removed)
                            {
                                sugar_r90_parent_bond.remove();
                                r90_parent_bond_removed = true;
                            }
                            else
                            {
                                remembered_bond_stereo = RememberStereo(sugar_component_mol, sugar_base_connecting_atom_idx + 1, sugar_r90_atom_idx + 1);
                                connecting_bond_stereo = sugar_r90_parent_bond.bondStereo();
                            }

                            sugar_r90_atom.remove();
                            break;
                        }

                        base_sugar_mapping = base_component_mol.merge(sugar_component_mol);
                        //base_component_mol = RestoreStereoBond(base_component_mol,base_sugar_mapping.mapAtom(sugar_component_mol.getAtom(preserved_link_stereo.Item1-1)).index()+1, base_sugar_mapping.mapAtom(sugar_component_mol.getAtom(preserved_link_stereo.Item2-1)).index()+1, preserved_link_stereo.Item3);
                        base_sugar_mapping.mapAtom(sugar_component_mol.getAtom(sugar_base_connecting_atom_idx)).addBond(base_component_mol.getAtom(base_sugar_connecting_atom_idx), 1);
                        IndigoObject base_sugar_3prime_Ratom = base_sugar_mapping.mapAtom(sugar_component_mol.getAtom(sugar_base_connecting_atom_idx));

                        base_component_mol.layout();

                        base_component_mol = RestoreStereoBond(base_component_mol, base_sugar_mapping.mapAtom(sugar_component_mol.getAtom(sugar_base_connecting_atom_idx)).index() + 1, base_sugar_connecting_atom_idx + 1, remembered_bond_stereo.Item2, remembered_bond_stereo.Item1);

                        //base_component_mol = MoveStereoFromRings(base_component_mol);
                        sugar_r92_atom = base_sugar_mapping.mapAtom(sugar_component_mol.getAtom(sugar_r92_index));
                        sugar_r91_index = base_sugar_mapping.mapAtom(sugar_component_mol.getAtom(sugar_r91_index)).index();
                        sugar_r92_index = base_sugar_mapping.mapAtom(sugar_component_mol.getAtom(sugar_r92_index)).index();

                        //PNAComponent na_sugar_linker = PNAComponent.from(su.SugarLinkers[j].ToString(), state);
                        if (!is_mod_nucleoside)
                        {
                            IndigoObject linker_component_mol = getIndigo().loadQueryMolecule(na_sugar_linker.Fragment.Molecule.Mol);
                            int linker_r92_index = na_sugar_linker.Fragment.getStarAtomIndex("_R92") - 1;
                            linker_r92_atom = linker_component_mol.getAtom(linker_r92_index);

                            //YP connect the sugar linker to the sugar that it already connected to the nucleobase
                            int linker_r91_index = na_sugar_linker.Fragment.getStarAtomIndex("_R91") - 1;
                            IndigoObject linker_r91_atom = linker_component_mol.getAtom(linker_r91_index);

                            bool r91_bond_removed = false;
                            IndigoObject sugar_r91_atom = base_component_mol.getAtom(sugar_r91_index);
                            foreach (IndigoObject sugar_r91_parent_atom in sugar_r91_atom.iterateNeighbors())
                            {
                                sugar_linker_connecting_atom_idx = sugar_r91_parent_atom.index();
                                IndigoObject sugar_r91_parent_bond = sugar_r91_parent_atom.bond();
                                if (sugar_r91_parent_bond.bondStereo() == 0)
                                {
                                    sugar_r91_parent_bond.remove();
                                    r91_bond_removed = true;
                                }
                                else
                                {
                                    connecting_bond_stereo = sugar_r91_parent_atom.bondStereo();
                                }
                                sugar_r91_atom.remove();
                                break;
                            }

                            foreach (IndigoObject linker_r91_parent_atom in linker_r91_atom.iterateNeighbors())
                            {
                                linker_sugar_connecting_atom_idx = linker_r91_parent_atom.index();
                                IndigoObject linker_r91_parent_bond = linker_r91_parent_atom.bond();
                                if (linker_r91_parent_bond.bondStereo() == 0 && !r91_bond_removed)
                                {
                                    linker_r91_parent_bond.remove();
                                    r91_bond_removed = true;
                                }
                                else
                                {
                                    connecting_bond_stereo = linker_r91_parent_bond.bondStereo();
                                }

                                linker_r91_atom.remove();
                                break;
                            }
                            chain_linker_mapping = base_component_mol.merge(linker_component_mol);
                            chain_linker_mapping.mapAtom(linker_component_mol.getAtom(linker_sugar_connecting_atom_idx)).addBond(base_component_mol.getAtom(sugar_linker_connecting_atom_idx), 1);
                        }
                        base_component_mol.layout();
                        Tuple<IndigoObject, Tuple<int, int>> modification_connectors = GetNucleotideConnectors(base_component_mol);
                        String mod_type = "";
                        /*
                         * if (is_mod_nucleoside)
                        {
                            mod_type = "NUCLEOSIDE SUBSTITUTION POINT";
                        }
                        else
                        {
                            mod_type = "NUCLEOTIDE SUBSTITUTION POINT";
                        }
                        */

                        //mod_type = "NUCLEOTIDE SUBSTITUTION POINT";
                        mod_type = "Nucleotide substitution site";
                        na.ProcessStructuralModificationGroup(
                            MakeImplicitNAStructuralModification(
                                na, su, j, mod_type, state, CreateAssembledFragment(
                                    na, modification_connectors.Item1.molfile(), new List<int>() { modification_connectors.Item2.Item1 + 1, modification_connectors.Item2.Item2 + 1 }, new List<String>() { "0", "0" }, state)
                                    ),
                            state);

                        base_component_mol.layout();

                    }
                }


                //check if circular, create circular link modification

                if (su.isCircular)
                {
                    //build the first nucleotide
                    PNAComponent first_na_nucleobase = PNAComponent.from(nucleobases[0].ToString(), state);
                    if (first_na_nucleobase.Fragment == null)
                    {
                        throw new Exception("Nucleic Acid cannot be converted, missing base at first position on chain " + (i + 1));
                    }

                    PNAComponent first_na_sugar = PNAComponent.from(GetSugarAtPosition(su, 1), state);
                    if (first_na_sugar.Fragment == null)
                    {
                        throw new Exception("Nucleic Acid cannot be converted, missing sugar at first position on chain " + (i + 1));
                    }

                    PNAComponent first_na_sugar_linker = PNAComponent.from(GetLinkageAtPosition(su, 1), state);

                    //need to throw an error here if first_na_sugar_linker is null, because linker on first nucleotide is required for circular chains
                    if (first_na_sugar_linker.Fragment == null)
                    {
                        throw new Exception("Nucleic Acid cannot be converted, missing linkage at first position on circular chain " + (i + 1));
                    }
                    IndigoObject first_base_component_mol = getIndigo().loadQueryMolecule(first_na_nucleobase.Fragment.Molecule.Mol);

                    int first_sugar_r90_atom_idx = first_na_sugar.Fragment.getStarAtomIndex("_R90") - 1;
                    IndigoObject first_sugar_component_mol = getIndigo().loadQueryMolecule(first_na_sugar.Fragment.Molecule.Mol);

                    int first_base_sugar_connecting_atom_idx = -1;
                    int first_sugar_base_connecting_atom_idx = -1;
                    int first_sugar_linker_connecting_atom_idx = -1;
                    int first_linker_sugar_connecting_atom_idx = -1;
                    int first_sugar_chain_connecting_atom_idx = -1;
                    int first_chain_sugar_connecting_atom_idx = -1;
                    int first_connecting_bond_stereo = 0;
                    int first_sugar_r91_index = -1;
                    int first_sugar_r92_index = -1;
                    IndigoObject first_base_sugar_mapping = null;
                    IndigoObject first_nucleotide_mapping = null;
                    //int sugar_base_bond_stereo = 0;
                    IndigoObject first_sugar_r92_atom = null;
                    IndigoObject first_linker_r92_atom = null;
                    IndigoObject first_modification_mol = null;

                    int first_base_r90_atom_idx = first_na_nucleobase.Fragment.getStarAtomIndex("_R90") - 1;
                    int first_sugar_r90_parent_stereo_type = 0;
                    IndigoObject first_sugar_r90_atom = first_sugar_component_mol.getAtom(first_sugar_r90_atom_idx);
                    IndigoObject first_base_r90_atom = first_base_component_mol.getAtom(first_base_r90_atom_idx);

                    bool first_r90_parent_bond_removed = false;
                    first_sugar_r91_index = first_na_sugar.Fragment.getStarAtomIndex("_R91") - 1;
                    first_sugar_r92_index = first_na_sugar.Fragment.getStarAtomIndex("_R92") - 1;
                    first_sugar_r92_atom = first_sugar_component_mol.getAtom(first_sugar_r92_index);

                    foreach (IndigoObject first_base_r90_parent_atom in first_base_r90_atom.iterateNeighbors())
                    {
                        first_base_sugar_connecting_atom_idx = first_base_r90_parent_atom.index();
                        IndigoObject first_base_r90_parent_bond = first_base_r90_parent_atom.bond();
                        if (first_base_r90_parent_bond.bondStereo() == 0)
                        {
                            first_base_r90_parent_bond.remove();
                            first_r90_parent_bond_removed = true;
                        }
                        else
                        {
                            first_connecting_bond_stereo = first_base_r90_parent_bond.bondStereo();
                        }
                        first_base_r90_atom.remove();
                        break;
                    }
                    Tuple<int, bool> first_remembered_bond_stereo = new Tuple<int, bool>(0, false);
                    foreach (IndigoObject first_sugar_r90_parent_atom in first_sugar_r90_atom.iterateNeighbors())
                    {

                        first_sugar_base_connecting_atom_idx = first_sugar_r90_parent_atom.index();
                        first_sugar_r90_parent_stereo_type = first_sugar_r90_parent_atom.stereocenterType();
                        IndigoObject first_sugar_r90_parent_bond = first_sugar_r90_parent_atom.bond();
                        if (first_sugar_r90_parent_bond.bondStereo() == 0 && !first_r90_parent_bond_removed)
                        {
                            first_sugar_r90_parent_bond.remove();
                            first_r90_parent_bond_removed = true;
                        }
                        else
                        {
                            first_remembered_bond_stereo = RememberStereo(first_sugar_component_mol, first_sugar_base_connecting_atom_idx + 1, first_sugar_r90_atom_idx + 1);
                            first_connecting_bond_stereo = first_sugar_r90_parent_bond.bondStereo();
                        }

                        first_sugar_r90_atom.remove();
                        break;
                    }

                    first_base_sugar_mapping = first_base_component_mol.merge(first_sugar_component_mol);
                    //base_component_mol = RestoreStereoBond(base_component_mol,base_sugar_mapping.mapAtom(sugar_component_mol.getAtom(preserved_link_stereo.Item1-1)).index()+1, base_sugar_mapping.mapAtom(sugar_component_mol.getAtom(preserved_link_stereo.Item2-1)).index()+1, preserved_link_stereo.Item3);
                    first_base_sugar_mapping.mapAtom(first_sugar_component_mol.getAtom(first_sugar_base_connecting_atom_idx)).addBond(first_base_component_mol.getAtom(first_base_sugar_connecting_atom_idx), 1);
                    IndigoObject first_base_sugar_3prime_Ratom = first_base_sugar_mapping.mapAtom(first_sugar_component_mol.getAtom(first_sugar_base_connecting_atom_idx));

                    first_base_component_mol.layout();

                    first_base_component_mol = RestoreStereoBond(first_base_component_mol, first_base_sugar_mapping.mapAtom(first_sugar_component_mol.getAtom(first_sugar_base_connecting_atom_idx)).index() + 1, first_base_sugar_connecting_atom_idx + 1, first_remembered_bond_stereo.Item2, first_remembered_bond_stereo.Item1);

                    //base_component_mol = MoveStereoFromRings(base_component_mol);
                    first_sugar_r92_atom = first_base_sugar_mapping.mapAtom(first_sugar_component_mol.getAtom(first_sugar_r92_index));
                    first_sugar_r91_index = first_base_sugar_mapping.mapAtom(first_sugar_component_mol.getAtom(first_sugar_r91_index)).index();
                    first_sugar_r92_index = first_base_sugar_mapping.mapAtom(first_sugar_component_mol.getAtom(first_sugar_r92_index)).index();

                    //PNAComponent na_sugar_linker = PNAComponent.from(su.SugarLinkers[j].ToString(), state);

                    IndigoObject first_linker_component_mol = getIndigo().loadQueryMolecule(first_na_sugar_linker.Fragment.Molecule.Mol);
                    int first_linker_r92_index = first_na_sugar_linker.Fragment.getStarAtomIndex("_R92") - 1;
                    first_linker_r92_atom = first_linker_component_mol.getAtom(first_linker_r92_index);

                    //YP connect the sugar linker to the sugar that it already connected to the nucleobase
                    int first_linker_r91_index = first_na_sugar_linker.Fragment.getStarAtomIndex("_R91") - 1;
                    IndigoObject first_linker_r91_atom = first_linker_component_mol.getAtom(first_linker_r91_index);

                    bool first_r91_bond_removed = false;
                    IndigoObject first_sugar_r91_atom = first_base_component_mol.getAtom(first_sugar_r91_index);
                    foreach (IndigoObject first_sugar_r91_parent_atom in first_sugar_r91_atom.iterateNeighbors())
                    {
                        first_sugar_linker_connecting_atom_idx = first_sugar_r91_parent_atom.index();
                        IndigoObject first_sugar_r91_parent_bond = first_sugar_r91_parent_atom.bond();
                        if (first_sugar_r91_parent_bond.bondStereo() == 0)
                        {
                            first_sugar_r91_parent_bond.remove();
                            first_r91_bond_removed = true;
                        }
                        else
                        {
                            first_connecting_bond_stereo = first_sugar_r91_parent_atom.bondStereo();
                        }
                        first_sugar_r91_atom.remove();
                        break;
                    }

                    foreach (IndigoObject first_linker_r91_parent_atom in first_linker_r91_atom.iterateNeighbors())
                    {
                        first_linker_sugar_connecting_atom_idx = first_linker_r91_parent_atom.index();
                        IndigoObject first_linker_r91_parent_bond = first_linker_r91_parent_atom.bond();
                        if (first_linker_r91_parent_bond.bondStereo() == 0 && !first_r91_bond_removed)
                        {
                            first_linker_r91_parent_bond.remove();
                            first_r91_bond_removed = true;
                        }
                        else
                        {
                            first_connecting_bond_stereo = first_linker_r91_parent_bond.bondStereo();
                        }

                        first_linker_r91_atom.remove();
                        break;
                    }
                    first_chain_linker_mapping = first_base_component_mol.merge(first_linker_component_mol);
                    first_chain_linker_mapping.mapAtom(first_linker_component_mol.getAtom(first_linker_sugar_connecting_atom_idx)).addBond(first_base_component_mol.getAtom(first_sugar_linker_connecting_atom_idx), 1);

                    first_base_component_mol.layout();
                    Tuple<IndigoObject, Tuple<int, int>> first_nucleotide_connectors = GetNucleotideConnectors(first_base_component_mol);

                    String mod_type = "";


                    //build the last nucleotide
                    PNAComponent last_na_nucleobase = PNAComponent.from(nucleobases[nucleobases.Count - 1].ToString(), state);
                    if (last_na_nucleobase.Fragment == null)
                    {
                        throw new Exception("Nucleic Acid cannot be converted, missing base at last position on chain " + (i + 1));
                    }

                    PNAComponent last_na_sugar = PNAComponent.from(GetSugarAtPosition(su, nucleobases.Count), state);
                    if (last_na_sugar.Fragment == null)
                    {
                        throw new Exception("Nucleic Acid cannot be converted, missing sugar at last position on chain " + (i + 1));
                    }

                    PNAComponent last_na_sugar_linker = PNAComponent.from(GetLinkageAtPosition(su, nucleobases.Count), state);
                    //need to throw an error here if last_na_sugar_linker is null, because linker on first nucleotide is required for circular chains
                    if (last_na_sugar_linker.Fragment == null)
                    {
                        throw new Exception("Nucleic Acid cannot be converted, missing linkage at last position on chain " + (i + 1));
                    }

                    IndigoObject last_base_component_mol = getIndigo().loadQueryMolecule(last_na_nucleobase.Fragment.Molecule.Mol);

                    int last_sugar_r90_atom_idx = last_na_sugar.Fragment.getStarAtomIndex("_R90") - 1;
                    IndigoObject last_sugar_component_mol = getIndigo().loadQueryMolecule(last_na_sugar.Fragment.Molecule.Mol);

                    int last_base_sugar_connecting_atom_idx = -1;
                    int last_sugar_base_connecting_atom_idx = -1;
                    int last_sugar_linker_connecting_atom_idx = -1;
                    int last_linker_sugar_connecting_atom_idx = -1;
                    int last_sugar_chain_connecting_atom_idx = -1;
                    int last_chain_sugar_connecting_atom_idx = -1;
                    int last_connecting_bond_stereo = 0;
                    int last_sugar_r91_index = -1;
                    int last_sugar_r92_index = -1;
                    IndigoObject last_base_sugar_mapping = null;
                    IndigoObject last_nucleotide_mapping = null;
                    //int sugar_base_bond_stereo = 0;
                    IndigoObject last_sugar_r92_atom = null;
                    IndigoObject last_linker_r92_atom = null;
                    IndigoObject last_modification_mol = null;

                    int last_base_r90_atom_idx = last_na_nucleobase.Fragment.getStarAtomIndex("_R90") - 1;
                    int last_sugar_r90_parent_stereo_type = 0;
                    IndigoObject last_sugar_r90_atom = last_sugar_component_mol.getAtom(last_sugar_r90_atom_idx);
                    IndigoObject last_base_r90_atom = last_base_component_mol.getAtom(last_base_r90_atom_idx);

                    bool last_r90_parent_bond_removed = false;
                    last_sugar_r91_index = last_na_sugar.Fragment.getStarAtomIndex("_R91") - 1;
                    last_sugar_r92_index = last_na_sugar.Fragment.getStarAtomIndex("_R92") - 1;
                    last_sugar_r92_atom = last_sugar_component_mol.getAtom(last_sugar_r92_index);

                    foreach (IndigoObject last_base_r90_parent_atom in last_base_r90_atom.iterateNeighbors())
                    {
                        last_base_sugar_connecting_atom_idx = last_base_r90_parent_atom.index();
                        IndigoObject last_base_r90_parent_bond = last_base_r90_parent_atom.bond();
                        if (last_base_r90_parent_bond.bondStereo() == 0)
                        {
                            last_base_r90_parent_bond.remove();
                            last_r90_parent_bond_removed = true;
                        }
                        else
                        {
                            last_connecting_bond_stereo = last_base_r90_parent_bond.bondStereo();
                        }
                        last_base_r90_atom.remove();
                        break;
                    }
                    Tuple<int, bool> last_remembered_bond_stereo = new Tuple<int, bool>(0, false);
                    foreach (IndigoObject last_sugar_r90_parent_atom in last_sugar_r90_atom.iterateNeighbors())
                    {

                        last_sugar_base_connecting_atom_idx = last_sugar_r90_parent_atom.index();
                        last_sugar_r90_parent_stereo_type = last_sugar_r90_parent_atom.stereocenterType();
                        IndigoObject last_sugar_r90_parent_bond = last_sugar_r90_parent_atom.bond();
                        if (last_sugar_r90_parent_bond.bondStereo() == 0 && !last_r90_parent_bond_removed)
                        {
                            last_sugar_r90_parent_bond.remove();
                            last_r90_parent_bond_removed = true;
                        }
                        else
                        {
                            last_remembered_bond_stereo = RememberStereo(last_sugar_component_mol, last_sugar_base_connecting_atom_idx + 1, last_sugar_r90_atom_idx + 1);
                            last_connecting_bond_stereo = last_sugar_r90_parent_bond.bondStereo();
                        }

                        last_sugar_r90_atom.remove();
                        break;
                    }

                    last_base_sugar_mapping = last_base_component_mol.merge(last_sugar_component_mol);
                    //base_component_mol = RestoreStereoBond(base_component_mol,base_sugar_mapping.mapAtom(sugar_component_mol.getAtom(preserved_link_stereo.Item1-1)).index()+1, base_sugar_mapping.mapAtom(sugar_component_mol.getAtom(preserved_link_stereo.Item2-1)).index()+1, preserved_link_stereo.Item3);
                    last_base_sugar_mapping.mapAtom(last_sugar_component_mol.getAtom(last_sugar_base_connecting_atom_idx)).addBond(last_base_component_mol.getAtom(last_base_sugar_connecting_atom_idx), 1);
                    IndigoObject last_base_sugar_3prime_Ratom = last_base_sugar_mapping.mapAtom(last_sugar_component_mol.getAtom(last_sugar_base_connecting_atom_idx));

                    last_base_component_mol.layout();

                    last_base_component_mol = RestoreStereoBond(last_base_component_mol, last_base_sugar_mapping.mapAtom(last_sugar_component_mol.getAtom(last_sugar_base_connecting_atom_idx)).index() + 1, last_base_sugar_connecting_atom_idx + 1, last_remembered_bond_stereo.Item2, last_remembered_bond_stereo.Item1);

                    //base_component_mol = MoveStereoFromRings(base_component_mol);
                    last_sugar_r92_atom = last_base_sugar_mapping.mapAtom(last_sugar_component_mol.getAtom(last_sugar_r92_index));
                    last_sugar_r91_index = last_base_sugar_mapping.mapAtom(last_sugar_component_mol.getAtom(last_sugar_r91_index)).index();
                    last_sugar_r92_index = last_base_sugar_mapping.mapAtom(last_sugar_component_mol.getAtom(last_sugar_r92_index)).index();

                    //PNAComponent na_sugar_linker = PNAComponent.from(su.SugarLinkers[j].ToString(), state);

                    IndigoObject last_linker_component_mol = getIndigo().loadQueryMolecule(last_na_sugar_linker.Fragment.Molecule.Mol);
                    int last_linker_r92_index = last_na_sugar_linker.Fragment.getStarAtomIndex("_R92") - 1;
                    last_linker_r92_atom = last_linker_component_mol.getAtom(last_linker_r92_index);

                    //YP connect the sugar linker to the sugar that it already connected to the nucleobase
                    int last_linker_r91_index = last_na_sugar_linker.Fragment.getStarAtomIndex("_R91") - 1;
                    IndigoObject last_linker_r91_atom = last_linker_component_mol.getAtom(last_linker_r91_index);

                    bool last_r91_bond_removed = false;
                    IndigoObject last_sugar_r91_atom = last_base_component_mol.getAtom(last_sugar_r91_index);
                    foreach (IndigoObject last_sugar_r91_parent_atom in last_sugar_r91_atom.iterateNeighbors())
                    {
                        last_sugar_linker_connecting_atom_idx = last_sugar_r91_parent_atom.index();
                        IndigoObject last_sugar_r91_parent_bond = last_sugar_r91_parent_atom.bond();
                        if (last_sugar_r91_parent_bond.bondStereo() == 0)
                        {
                            last_sugar_r91_parent_bond.remove();
                            last_r91_bond_removed = true;
                        }
                        else
                        {
                            last_connecting_bond_stereo = last_sugar_r91_parent_atom.bondStereo();
                        }
                        last_sugar_r91_atom.remove();
                        break;
                    }

                    foreach (IndigoObject last_linker_r91_parent_atom in last_linker_r91_atom.iterateNeighbors())
                    {
                        last_linker_sugar_connecting_atom_idx = last_linker_r91_parent_atom.index();
                        IndigoObject last_linker_r91_parent_bond = last_linker_r91_parent_atom.bond();
                        if (last_linker_r91_parent_bond.bondStereo() == 0 && !last_r91_bond_removed)
                        {
                            last_linker_r91_parent_bond.remove();
                            last_r91_bond_removed = true;
                        }
                        else
                        {
                            last_connecting_bond_stereo = last_linker_r91_parent_bond.bondStereo();
                        }

                        last_linker_r91_atom.remove();
                        break;
                    }
                    last_chain_linker_mapping = last_base_component_mol.merge(last_linker_component_mol);
                    last_chain_linker_mapping.mapAtom(last_linker_component_mol.getAtom(last_linker_sugar_connecting_atom_idx)).addBond(last_base_component_mol.getAtom(last_sugar_linker_connecting_atom_idx), 1);

                    last_base_component_mol.layout();
                    Tuple<IndigoObject, Tuple<int, int>> last_nucleotide_connectors = GetNucleotideConnectors(last_base_component_mol);

                    //need to add oxygen since GetNucleotideConnectors stripped off R atom from Phosphate, need to put oxygen in its place
                    last_base_component_mol.getAtom(last_nucleotide_connectors.Item2.Item1).addBond(last_base_component_mol.addAtom("O"), 1);

                    mod_type = "";

                    //connet first_base_component_mol and last_base_component_mol
                    //specifically connect 3' of last_base_component_mol to 5' of first_base_component_mol
                    //this corresponds to connecting atom index last_nucleotide_connectors.Item2.Item2 of last_nucleotide_connectors.Item1 to first_nucleotide_connectors.Item2.Item1 of first_nucleotide_connectors.Item1

                    //create modification molecule
                    IndigoObject circular_link_mod_fragment = first_base_component_mol;
                    IndigoObject circular_link_mod_mapping = first_base_component_mol.merge(last_base_component_mol);
                    circular_link_mod_mapping.mapAtom(last_base_component_mol.getAtom(last_nucleotide_connectors.Item2.Item2)).addBond(circular_link_mod_fragment.getAtom(first_nucleotide_connectors.Item2.Item1), 1);
                    circular_link_mod_fragment.layout();
                    mod_type = "Circular Link";


                    NAFragment link_frag = CreateAssembledFragment(na, circular_link_mod_fragment.molfile(), state);

                    link_frag.AddConnectorsPair(first_nucleotide_connectors.Item2.Item2 + 1, 0, "implicit_fragment");
                    link_frag.AddConnectorsPair(circular_link_mod_mapping.mapAtom(last_base_component_mol.getAtom(last_nucleotide_connectors.Item2.Item1)).index() + 1, 0, "implicit_fragment");

                    link_frag.Connectors.ForEachWithIndex((c, k) => c.Id = k);

                    //List <NAFragment.Connector> link_frag_connectors = new List<NAFragment.Connector>(link_frag.Connectors);

                    //List<Tuple<int, int>> sites_list = new List<Tuple<int, int>>();

                    //sites_list.Add(new Tuple<int, int>(su.Ordinal - 1, 0));
                    //sites_list.Add(new Tuple<int, int>(su.Ordinal - 1, nucleobases.Count - 1));

                    NALink link = new NALink(state.RootObject) { LinkType = null };
                    //NALink link1 = new NALink(state.RootObject) { LinkType = null };
                    link.Linker = link_frag;
                    //link1.Linker = link_frag;

                    TraceUtils.WriteUNIITrace(TraceEventType.Information, na.UNII, null, "Circular link present");

                    link.Sites.Add(new NASite(state.RootObject, "NUCLEOTIDE SUBSTITUTION POINT") { Subunit = su, Position = 0, ConnectorRef = link.Linker.Connectors[0] });
                    link.Sites.Add(new NASite(state.RootObject, "NUCLEOTIDE SUBSTITUTION POINT") { Subunit = su, Position = nucleobases.Count - 1, ConnectorRef = link.Linker.Connectors[1] });
                    link.IsImplicit = true;
                    link.Amount = new Amount();
                    link.Amount.AdjustAmount();
                    na.Links.Add(link);
                    na.NAFragments.Add(link_frag);


                }

            });
        }

        public static Tuple<IndigoObject, int> GetR91ParentAtomIndex(IndigoObject in_molecule)
        {
            int r91index = 0;

            foreach (IndigoObject atom in in_molecule.iterateAtoms())
            {
                if (atom.symbol() == "R")
                {
                    foreach (IndigoObject neighbor in atom.iterateNeighbors())
                    {
                        //float[] xyz = neighbor.xyz();
                        atom.remove();
                        r91index = neighbor.index();
                        return new Tuple<IndigoObject, int>(in_molecule, r91index);
                        /*foreach (IndigoObject atom2 in in_molecule.iterateAtoms())
                        {
                            if (atom2.xyz().SequenceEqual(xyz))
                            {
                                r91index = atom2.index();
                                return new Tuple<IndigoObject, int>(in_molecule, r91index);
                            }
                        }
                        */
                    }
                }
            }
            return new Tuple<IndigoObject, int>(null, 0);
        }

        public static Tuple<IndigoObject, Tuple<int, int>> GetNucleotideConnectors(IndigoObject in_molecule)
        {
            int five_prime_index = 0;
            int three_prime_index = 0;
            int atom_index = -1;
            foreach (IndigoObject atom in in_molecule.iterateAtoms())
            {
                if (atom.symbol() == "R")
                {
                    foreach (IndigoObject neighbor in atom.iterateNeighbors())
                    {
                        //float[] xyz = neighbor.xyz();
                        
                        if (neighbor.symbol() == "P")
                        {
                            five_prime_index = neighbor.index();
                            atom.resetAtom("O");
                        }
                        else if (neighbor.symbol() == "O")
                        {
                            atom_index = atom.index();
                            atom.remove();
                            three_prime_index = neighbor.index();
                            if (atom_index < three_prime_index) { three_prime_index--; }
                            if (atom_index < five_prime_index) { five_prime_index--; }
                            break;
                        }
                        
                        /*foreach (IndigoObject atom2 in in_molecule.iterateAtoms())
                        {
                            if (atom2.xyz().SequenceEqual(xyz))
                            {
                                r91index = atom2.index();
                                return new Tuple<IndigoObject, int>(in_molecule, r91index);
                            }
                        }
                        */
                    }
                    
                    break;
                }
                
            }
            IndigoObject molecule2 = getIndigo().loadMolecule(in_molecule.molfile());
            foreach (IndigoObject atom in molecule2.iterateAtoms())
            {
                if (atom.symbol() == "R")
                {
                    foreach (IndigoObject neighbor in atom.iterateNeighbors())
                    {
                        //float[] xyz = neighbor.xyz();
                        
                        if (neighbor.symbol() == "P")
                        {
                            five_prime_index = neighbor.index();
                            atom.resetAtom("O");
                        }
                        else if (neighbor.symbol() == "O")
                        {
                            atom_index = atom.index();
                            atom.remove();
                            three_prime_index = neighbor.index();
                            if (atom_index < three_prime_index) { three_prime_index--; }
                            if (atom_index < five_prime_index) { five_prime_index--; }
                        }
                        break;
                        /*foreach (IndigoObject atom2 in in_molecule.iterateAtoms())
                        {
                            if (atom2.xyz().SequenceEqual(xyz))
                            {
                                r91index = atom2.index();
                                return new Tuple<IndigoObject, int>(in_molecule, r91index);
                            }
                        }
                        */
                    }
                    break;
                }

            }

            return new Tuple<IndigoObject, Tuple<int, int>>(molecule2, new Tuple<int, int>(five_prime_index, three_prime_index));
        }

        /*
         * public static Tuple<IndigoObject, Tuple<int, int>> GetNucleotideConnectors(Nucleotide in_nucleotide)
        {

            int five_prime_index = 0;
            int three_prime_index = 0;

            IndigoObject linker_star_atom = null;
            IndigoObject three_prime_atom = null;
            foreach (IndigoObject neighbor in in_nucleotide.molecule.getAtom(in_nucleotide.linker_star_atom_idx).iterateNeighbors())
            {
                five_prime_index = neighbor.index();
                break;
            }
            foreach (IndigoObject neighbor in in_nucleotide.molecule.getAtom(in_nucleotide.linker_star_atom_idx).iterateNeighbors())
            {
                three_prime_index = neighbor.index();
                break;
            }

            in_nucleotide.molecule.getAtom(in_nucleotide.linker_star_atom_idx).remove();
            if (in_nucleotide.linker_star_atom_idx < five_prime_index) { five_prime_index--; }
            if (in_nucleotide.linker_star_atom_idx < three_prime_index) { three_prime_index--; }
            in_nucleotide.molecule.getAtom(in_nucleotide.three_prime_star_atom_idx).remove();
            if (in_nucleotide.three_prime_star_atom_idx < three_prime_index) { three_prime_index--; }
            if (in_nucleotide.three_prime_star_atom_idx < five_prime_index) { five_prime_index--; }
            return new Tuple<IndigoObject, Tuple<int, int>>(in_nucleotide.molecule, new Tuple<int, int>(five_prime_index, three_prime_index));
        }
        */
        
        //YP SRS-394. Rewriting this one below, keeping this version alive
        //Rewriting because a nucleoside mod doesn't have P, thus doesn't have 5' connector
        public static Tuple<IndigoObject, Tuple<int, int>> GetNucleosideConnectors_old(Nucleoside in_nucleoside)
        {

            int five_prime_index = 0;
            int three_prime_index = 0;

            IndigoObject five_prime_atom = null;
            IndigoObject three_prime_atom = null;
            foreach (IndigoObject neighbor in in_nucleoside.molecule.getAtom(in_nucleoside.five_prime_star_atom_idx).iterateNeighbors())
            {
                five_prime_index = neighbor.index();
                break;
            }
            foreach (IndigoObject neighbor in in_nucleoside.molecule.getAtom(in_nucleoside.three_prime_star_atom_idx).iterateNeighbors())
            {
                three_prime_index = neighbor.index();
                break;
            }

            in_nucleoside.molecule.getAtom(in_nucleoside.five_prime_star_atom_idx).remove();
            if (in_nucleoside.five_prime_star_atom_idx < five_prime_index) { five_prime_index--; }
            if (in_nucleoside.five_prime_star_atom_idx < three_prime_index) { three_prime_index--; }
            in_nucleoside.molecule.getAtom(in_nucleoside.three_prime_star_atom_idx).remove();
            if (in_nucleoside.three_prime_star_atom_idx < three_prime_index) { three_prime_index--; }
            if (in_nucleoside.three_prime_star_atom_idx < five_prime_index) { five_prime_index--; }
            return new Tuple<IndigoObject, Tuple<int, int>>(in_nucleoside.molecule, new Tuple<int, int>(five_prime_index, three_prime_index));
        }

        public static Tuple<IndigoObject, Tuple<int, int>> GetNucleosideConnectors(Nucleoside in_nucleoside)
        {

            int five_prime_index = -1;
            int three_prime_index = -1;

            IndigoObject five_prime_atom = null;
            IndigoObject three_prime_atom = null;
            foreach (IndigoObject neighbor in in_nucleoside.molecule.getAtom(in_nucleoside.five_prime_star_atom_idx).iterateNeighbors())
            {
                five_prime_index = neighbor.index();
                break;
            }
            foreach (IndigoObject neighbor in in_nucleoside.molecule.getAtom(in_nucleoside.three_prime_star_atom_idx).iterateNeighbors())
            {
                three_prime_index = neighbor.index();
                break;
            }

            in_nucleoside.molecule.getAtom(in_nucleoside.five_prime_star_atom_idx).remove();
            if (in_nucleoside.five_prime_star_atom_idx < five_prime_index) { five_prime_index--; }
            if (in_nucleoside.five_prime_star_atom_idx < three_prime_index) { three_prime_index--; }
            in_nucleoside.molecule.getAtom(in_nucleoside.three_prime_star_atom_idx).remove();
            if (in_nucleoside.three_prime_star_atom_idx < three_prime_index) { three_prime_index--; }
            if (in_nucleoside.three_prime_star_atom_idx < five_prime_index) { five_prime_index--; }

            //YP SRS-394. This is a nucleoside, so no linker is present
            //If linker is absent then P is also absent, and only P can be first (5') connector, so need to keep it -1 (0 based index) to generate nullFlavor in SPL
            return new Tuple<IndigoObject, Tuple<int, int>>(in_nucleoside.molecule, new Tuple<int, int>(-1, three_prime_index));
        }

        public static List<SdfRecord> asChemical_orig(this NucleicAcid na, PolymerBaseReadingState state, ConvertOptions opt)
        {
            int MAX_ATOMS = 999;


            List<List<PNucleotide>> subunits = new List<List<PNucleotide>>();

            //Decompse into sequence
            na.Subunits.Select(su => su.Sequence)
                            .ForAll(sq => {
                                List<PNucleotide> nuclist = new List<PNucleotide>();
                                sq.ToString().ForEachWithIndex((nuc, i) => {
                                    PNucleotide pnuc = PNucleotide.from(nuc + "", state);
                                    nuclist.Add(pnuc);
                                });
                                subunits.Add(nuclist);
                            });
            //First, you need to get the sequence

            na.Links.ForEachWithIndex((l, i) => {
                PNucleotide pnuc1 = null;

                l.Sites.ForEach(s => {
                    if (pnuc1 == null)
                    {
                        PNucleotide paa = subunits[s.Subunit.Ordinal - 1][s.Position];

                        paa.Fragment = l.Linker;
                        //protein.Subunits[s.Subunit.Ordinal - 1].Sequence.ToString();
                        //System.Console.WriteLine(paa.sym + "\t" + paa.Fragment.Molecule.SMILES);
                        pnuc1 = paa;
                    }
                    else
                    {
                        subunits[s.Subunit.Ordinal - 1][s.Position] = pnuc1;
                    }

                });
            });

            int nonStrModCount = na.NAModifications.Where(m => !(m is NAStructuralModificationGroup))
                                                    .Count();

            if (nonStrModCount != 0)
            {
                throw new Exception("Cannot convert nucleic acid to molecule. There are " + nonStrModCount + " non structural modifications.");
            }

            na.NAModifications.Where(m => m is NAStructuralModificationGroup)
                                 .Select(m => (NAStructuralModificationGroup)m)
                                 .ForEachWithIndex((m, i) => {

                                     //Need to do something with amounts here
                                     //m.Amount.
                                     NAStructuralModification sm = m.Modification;

                                     if (sm.ModificationType == "MOIETY")
                                     {
                                         PNucleotide pnuc = PNucleotide.from("X", state);
                                         pnuc.Fragment = sm.Fragment;
                                         pnuc.isMoiety = true;
                                         subunits[0].Add(pnuc);

                                     }
                                     else if (sm.ModificationType == "AMINO ACID SUBSTITUTION" || sm.ModificationType == "AMINO ACID SUBSTITUION"
                                          || sm.ModificationType == null)
                                     {
                                         if (m.NucleotideSites.Count <= 0)
                                         {
                                             throw new Exception("Non-moiety modifications must have at least one site to be convertable to chemicals.");
                                         }
                                         m.NucleotideSites.ForEachWithIndex((s, j) => {
                                             PNucleotide pn = subunits[s.Subunit.Ordinal - 1][s.Position];
                                             pn.Fragment = sm.Fragment;
                                         });
                                     }
                                 });


            //YP moved to inside subunits loop
            //IndigoObject mol = getIndigo().loadMolecule("");
            List<SdfRecord> SdfRecords = new List<SdfRecord>();
            try
            {

                List<PNucleotide> stagedLinkers = new List<PNucleotide>();

                subunits.ForEachWithIndex((su, i) => {
                    IndigoObject mol = getIndigo().loadMolecule("");
                    int currentHead = 0;
                    //su.Reverse();
                    int max = su.Count - 1;



                    su.ForEachWithIndex((paa, j) => {
                        if (paa.Fragment == null)
                        {
                            throw new Exception("Unspecified structure for:" + paa.sym);
                        }
                        if (paa.Fragment != null)
                        {


                            //System.Console.WriteLine(smio);

                            //****************
                            //START ADD MOLECULE
                            //TODO: STEREO & charge & isotope

                            int offset = mol.countAtoms();

                            if (!paa.alreadyAdded() && paa.isLinker())
                            {
                                stagedLinkers.Add(paa);
                            }

                            bool addedMol = paa.mergeMolecule(mol);

                            Tuple<int, int> position = paa.getPosition(offset);

                            //System.Console.WriteLine(paa.sym);
                            //System.Console.WriteLine(position);

                            if (!paa.isMoiety)
                            {
                                if (position.Item2 > 0)
                                {
                                    // position = Tuple.Create(position.Item1 - 1, position.Item2 - 1);

                                    var atomIndex1 = position.Item2 - 1;
                                    var atomIndex2 = position.Item1 - 1;
                                    IndigoObject matom = null;
                                    IndigoObject matom2 = null;
                                    string msym = null;
                                    string msym2 = null;
                                    if (atomIndex1 >= 0)
                                    {
                                        matom = mol.getAtom(atomIndex1);
                                        msym = matom.symbol();
                                    }
                                    if (atomIndex2 >= 0)
                                    {
                                        matom2 = mol.getAtom(atomIndex2);
                                        msym2 = matom2.symbol();
                                    }


                                    //string smi = mol.smiles();

                                    //System.Console.WriteLine(smi);
                                    //String smol = paa.getMolfile();
                                    // System.Console.WriteLine(smol);
                                    //Remove the -OH
                                    if (j > 0 && "P".Equals(matom.symbol()))
                                    {
                                        int remIndex = MAX_ATOMS;
                                        List<IndigoObject> bonds = new List<IndigoObject>();

                                        foreach (IndigoObject n in matom.iterateNeighbors())
                                        {
                                            IndigoObject bond = n.bond();
                                            if (n.symbol().Equals("O") && bond.bondOrder() == 1)
                                            {
                                                remIndex = n.index();
                                                bond.remove();
                                                n.remove();
                                                break;
                                            }
                                        }
                                        if (remIndex < MAX_ATOMS)
                                        {
                                            paa.markRemoved(remIndex);
                                            //adjust the position
                                            if (remIndex < position.Item1)
                                            {
                                                position = Tuple.Create(position.Item1 - 1, position.Item2);
                                            }

                                            if (remIndex < position.Item2)
                                            {
                                                position = Tuple.Create(position.Item1, position.Item2 - 1);
                                            }

                                            if (remIndex < currentHead)
                                            {
                                                currentHead--;
                                            }
                                            stagedLinkers.ForEach(palink => {
                                                palink.markRemoved(remIndex);
                                            });
                                        }


                                        //Clean (I don't know why we need this)
                                        mol = getIndigo().loadMolecule(mol.molfile());

                                    }
                                    else
                                    {
                                        //System.Console.WriteLine("Something is wrong with connector:" + msym);
                                    }
                                    if (j == 0 && "P".Equals(matom.symbol()))
                                    {
                                        List<int> p_bond_indices = new List<int> { };
                                        List<int> p_neighbor_indices = new List<int> { };
                                        foreach (IndigoObject n in matom.iterateNeighbors())
                                        {
                                            //IndigoObject bond = n.bond();
                                            p_bond_indices.Add(n.bond().index());
                                            p_neighbor_indices.Add(n.index());
                                        }
                                        mol.removeBonds(p_bond_indices);
                                        mol.removeAtoms(p_neighbor_indices);
                                        matom.remove();
                                    }
                                }
                                if (mol.countAtoms() > MAX_ATOMS)
                                {
                                    throw new Exception("Nucleic Acid is too large to be interpretted as chemical");
                                }

                                if (currentHead != 0)
                                {
                                    mol.getAtom(currentHead)
                                       .addBond(mol.getAtom(position.Item2 - 1), 1);
                                }
                                currentHead = position.Item1 - 1;
                            }


                            paa.incrementAdded(offset);


                            //var smi = mol.smiles();
                            //System.Console.WriteLine(smi);
                        }
                    });
                    if (mol.countAtoms() <= 0) throw new Exception("No atoms found in nucleic acid");

                    //YP moved up into subunits loop
                    //if (mol.countAtoms() <= 0) throw new Exception("No atoms found in nucleic acid");

                    //TP: 2017-11-06
                    //********************
                    // Stereocenter detection
                    // Indigo does not have many tools for this, so we look at the explicit
                    // centers, and then randomly assign chirality to all sp3 carbons
                    // and re-evalute the count.
                    //
                    // This is not used yet, but will be used for epimeric and other complex 
                    // stereo 


                    //mol2.stereo
                    int explicitStereo = mol.countStereocenters();
                    IndigoObject mol2 = getIndigo().loadMolecule(mol.smiles());
                    //System.Console.WriteLine(mol.smiles());


                    foreach (IndigoObject atom in mol2.iterateAtoms())
                    {
                        try
                        {
                            if (atom.stereocenterType() == 0) continue;
                            atom.iterateNeighbors();
                            List<IndigoObject> natoms = new List<IndigoObject>();
                            int horder = 1;

                            foreach (IndigoObject nei in atom.iterateNeighbors())
                            {
                                natoms.Add(nei);
                                int bo = nei.bond().bondOrder();
                                if (bo > horder)
                                {
                                    horder = bo;
                                }
                            }
                            if (natoms.Count >= 3 && atom.symbol().Equals("C") && horder == 1)
                            {
                                atom.addStereocenter(Indigo.AND, natoms[0].index(), natoms[1].index(), natoms[2].index());
                            }
                        }
                        catch (Exception e)
                        {
                            //System.Console.WriteLine(e.Message);
                        }
                    }
                    int possibleStereo = mol2.countStereocenters();
                    //System.Console.WriteLine("GOT:" + possibleStereo);

                    if (possibleStereo != explicitStereo)
                    {
                        //System.Console.WriteLine(mol.smiles() + "\t" + possibleStereo + "/" + explicitStereo);
                        //System.Console.WriteLine(mol2.molfile() + "\t" + possibleStereo + "/" + explicitStereo);
                        //This means it's not absolute stereo
                    }

                    //Indigo processes very slowly with large molecules
                    if (opt == null || opt.Coord)
                    {
                        if (mol.countAtoms() <= MAX_ATOMS)
                        {
                            //string yp_smiles=mol.smiles();
                            //System.IO.File.WriteAllText(@"yp_smiles.txt", yp_smiles);
                            try
                            {
                                mol.layout();

                                String mfile = mol.molfile();

                                List<String> lines = mfile.SplitOnNewLines();
                                int atomcount = int.Parse(lines[3].Substring(0, 3).Trim());

                                List<Tuple<int, int>> stereoBonds = lines.Skip(atomcount + 3)
                                     .Filter(l => !l.Substring(6, 3).Trim().Equals("0"))
                                     .Select(l => Tuple.Create(int.Parse(l.Substring(0, 3).Trim()), int.Parse(l.Substring(3, 3).Trim())))
                                     .ToList();



                            }
                            //YP
                            //To get around indigo failing to generate 2D coordinates via .layout for some molecules
                            catch (Exception e)
                            {
                                IndigoObject tmp_mol = _indigo.loadMolecule(mol.smiles());
                                tmp_mol.layout();
                                foreach (IndigoObject prop in mol.iterateProperties())
                                {
                                    tmp_mol.setProperty(prop.name(), prop.rawData());
                                }
                                mol = tmp_mol;
                            }
                            mol = cleanMoleculeStereo(mol);
                        }
                    }
                    SdfRecord sdfmol = SdfRecord.FromString(mol.molfile());
                    sdfmol.AddField("UNII", na.UNII);
                    sdfmol.AddField("SUBSTANCE_ID", na.UNII);
                    sdfmol.AddField("STRUCTURE_ID", na.UNII);
                    sdfmol.AddField("SUBUNIT", (SdfRecords.Count + 1).ToString());
                    SdfRecords.Add(sdfmol);

                });
                return SdfRecords;
            }
            catch (Exception e)
            {
                System.Console.WriteLine(e.Message);
                System.Console.WriteLine(e.StackTrace);
            }

            return null;



        }
        //
        /// <summary>
        /// Test if a given polygon (defined by a list of "outer path" 2D points) contains
        /// the supplied point. This is done by taking the scalar "rejection" of 
        /// the supplied point onto each successive edge of the polygon, and testing
        /// if the "rejections" share a sign.
        /// </summary>
        /// <remarks>
        /// <code>
        /// 1------2
        /// |     /
        /// | P  /
        /// |   /
        /// |  /
        /// | /
        /// |/
        /// 3
        /// 
        /// </code>
        /// 
        /// Put another way, point P is inside the triangle because it is "below"
        /// the line defined by 1->2 as well as 2->3 and 3->1 (if you were to rotate
        /// the triangle to make the tested line "horizontal" before each test).
        /// 
        /// The vector "rejection" of vector a onto b is defined as the vector
        /// difference between a and the projection of a onto b. The scalar form of 
        /// this is just the signed distance from the b line to that rejection. Conveniently,
        /// this is the same as the scalar "projection" of a onto a vector orthogonal to
        /// b. By convention, here, the vector used for projection is just b rotated by
        /// 90 degrees (counter-clockwise). This means that a positive "rejection" implies
        /// that a would be "swept" up by a 180-degree counter-clockwise sweep of "b". A
        /// negative value means that a 180-degree clockwise sweep of "b" would hit "a".
        /// 
        /// Since a projection is proportional to the dot product, and we only care about
        /// the sign, we can get the rejection sign by using the idea of a "rejectionDotProduct":
        /// 
        /// r=(x1*y2-y1*x2)
        /// 
        /// If all successive pairs of points in the polygon have rejections to the test
        /// point that are of the same sign, then we know the test point is "inside" the
        /// polygon. 
        /// 
        /// Note that this test only works if the provided points are non-intersecting
        /// when traversed in the order given.
        /// </remarks>
        /// <param name="polygon"></param>
        /// <param name="center"></param>
        /// <returns></returns>
        public static Boolean IsInsidePoly(List<Tuple<double, double>> polygon, Tuple<double, double> center)
        {

            int lastSign = 0;
            for (int i = 0; i <= polygon.Count; i++)
            {
                Tuple<double, double> p1 = polygon[(i + 1) % polygon.Count];
                Tuple<double, double> p2 = polygon[(i) % polygon.Count];
                double proj = RejectionDotProduct(p1, p2, center);
                int csign = Math.Sign(proj);
                if (proj == 0.0)
                {
                    continue;
                }
                if (lastSign != 0)
                {
                    if (lastSign != csign)
                    {
                        return false;
                    }
                }
                lastSign = csign;
            }
            return true;
        }

        private static List<Tuple<int, int>> GetBondsStereo(IndigoObject mol)
        {
            List<Tuple<int, int>> returnlist = new List<Tuple<int, int>>();
            foreach (IndigoObject bond in mol.iterateBonds())
            {
                returnlist.Add(Tuple.Create(bond.index(), bond.bondStereo()));
            }
            return returnlist;
        }


        private static double RejectionDotProduct(Tuple<double, double> p1, Tuple<double, double> p2, Tuple<double, double> c)
        {
            return RejectionDotProduct(Tuple.Create(p2.Item1 - p1.Item1, p2.Item2 - p1.Item2), Tuple.Create(c.Item1 - p1.Item1, c.Item2 - p1.Item2));
        }
        private static double RejectionDotProduct(Tuple<double, double> p1, Tuple<double, double> c)
        {
            return p1.Item1 * c.Item2 - c.Item1 * p1.Item2;
        }
        //Cleans the stereo on the molecule to be compatible with InChI when
        //the angles are too close to co-linear on a stereocenter
        public static IndigoObject cleanMoleculeStereo(IndigoObject mol)
        {

            String mfile = mol.molfile();


            List<String> lines = mfile.Replace("\r", "").Split('\n').ToList();
            int atomcount = int.Parse(lines[3].Substring(0, 3).Trim());
            int bondcount = int.Parse(lines[3].Substring(3, 3).Trim());

            List<Tuple<int, int, int>> stereoBonds = lines.Skip(atomcount + 4)
                 .Filter(l => l.Length > 12)
                 .Filter(l => !l.StartsWith("M"))
                 .Filter(l => l.Substring(9, 3).Trim().Equals("1") || l.Substring(9, 3).Trim().Equals("6"))
                 .Select(l => Tuple.Create(int.Parse(l.Substring(0, 3).Trim()),
                                           int.Parse(l.Substring(3, 3).Trim()),
                                           int.Parse(l.Substring(9, 3).Trim())
                                           ))
                 .ToList();

            Dictionary<String, Tuple<int, int, int>> toChangeBond = new Dictionary<String, Tuple<int, int, int>>();


            stereoBonds.ForEach(t => {
                IndigoObject at = mol.getAtom(t.Item1 - 1);
                String asym = at.symbol();
                float[] xyz1 = at.xyz();

                List<Tuple<IndigoObject, double[]>> angles = new List<Tuple<IndigoObject, double[]>>();

                List<Tuple<double, double>> polygon = new List<Tuple<double, double>>();

                foreach (IndigoObject n in at.iterateNeighbors())
                {
                    int ind = n.index();
                    float[] xyz2 = n.xyz();
                    double[] d = new double[] { xyz2[0] - xyz1[0], xyz2[1] - xyz1[1] };
                    polygon.Add(Tuple.Create(d[0], d[1]));
                    if (ind == t.Item2 - 1) continue;

                    angles.Add(Tuple.Create(n, d));
                }
                if (angles.Count() != 2) return;
                double v1 = Math.Sqrt(Math.Pow(angles[0].Item2[0], 2) + Math.Pow(angles[0].Item2[1], 2));
                double v2 = Math.Sqrt(Math.Pow(angles[1].Item2[0], 2) + Math.Pow(angles[1].Item2[1], 2));
                double cos = (angles[0].Item2[0] * angles[1].Item2[0] + angles[0].Item2[1] * angles[1].Item2[1]) / (v1 * v2);
                var ang = Math.Acos(cos) * 180 / Math.PI;
                Boolean shouldInvert = !IsInsidePoly(polygon, Tuple.Create(0.0, 0.0));
                if (ang > 172)
                {
                    //too shallow of an angle for the stereo bond
                    //Need to change the owner of the bond
                    IndigoObject tatom = angles[0].Item1;
                    if (tatom.bond().bondStereo() != 0)
                    {
                        tatom = angles[1].Item1;
                    }
                    if (tatom.bond().bondStereo() != 0)
                    {
                        System.Console.WriteLine("WARNING: Can't change wedge on stereocenter with so many other wedges/dashes present!");
                        return;
                    }
                    int ntype = t.Item3;
                    if (shouldInvert)
                    {
                        if (ntype == 6) ntype = 1;
                        else if (ntype == 1) ntype = 6;
                    }
                    Tuple<int, int, int> r = Tuple.Create(t.Item1, tatom.index() + 1, ntype);
                    //bondsToAddStereoTo.Add(r);
                    toChangeBond.Add(r.Item2 + "_" + r.Item1, r);
                    toChangeBond.Add(r.Item1 + "_" + r.Item2, r);
                    toChangeBond.Add(t.Item1 + "_" + t.Item2, Tuple.Create(t.Item1, t.Item2, 0));
                }
            });

            var li = 0;
            List<String> nmolfile = lines.Select(l => {
                if (li >= atomcount + 4 && li < atomcount + 4 + bondcount)
                {
                    //abond
                    Tuple<int, int, int> asBond = Tuple.Create(int.Parse(l.Substring(0, 3).Trim()),
                                                               int.Parse(l.Substring(3, 3).Trim()),
                                                               int.Parse(l.Substring(9, 3).Trim()));
                    String bondName = asBond.Item1 + "_" + asBond.Item2;
                    if (toChangeBond.ContainsKey(bondName))
                    {
                        Tuple<int, int, int> asBond2;
                        toChangeBond.TryGetValue(bondName, out asBond2);
                        String fpart = ("" + asBond2.Item1).PadLeft(3) + ("" + asBond2.Item2).PadLeft(3);
                        l = fpart + l.Substring(6, 3) + (asBond2.Item3 + "").PadLeft(3) + l.Substring(12);
                    }
                }
                li++;
                return l;
            }).ToList();
            String nmol = String.Join("\n", nmolfile);
            return getIndigo().loadMolecule(nmol);
        }

        public static bool IsRingAtom(IndigoObject atom)
        {
            foreach (IndigoObject nei in atom.iterateNeighbors())
            {
                if (nei.bond().topology() == Indigo.RING)
                {
                    return true;
                }
            }
            return false;
        }
        //YP given the two atoms and molecule, return the type of stereo of the bond between them and whether the source atom goes first in the bond definition
        public static Tuple<int, bool> RememberStereo(IndigoObject mol, int source_atom_index, int destination_atom_index)
        {
            String mfile = mol.molfile();
            bool source_first = false;
            int bond_type = 0;

            List<String> lines = mfile.Replace("\r", "").Split('\n').ToList();
            int atomcount = int.Parse(lines[3].Substring(0, 3).Trim());
            int bondcount = int.Parse(lines[3].Substring(3, 3).Trim());

            List<Tuple<int, int, int>> stereoBonds = lines.Skip(atomcount + 4)
                 .Filter(l => l.Length > 12)
                 .Filter(l => !l.StartsWith("M"))
                 .Filter(l => l.Substring(9, 3).Trim().Equals("1") || l.Substring(9, 3).Trim().Equals("6"))
                 .Select(l => Tuple.Create(int.Parse(l.Substring(0, 3).Trim()),
                                           int.Parse(l.Substring(3, 3).Trim()),
                                           int.Parse(l.Substring(9, 3).Trim())
                                           ))
                 .ToList();

            //figure out what the stereo is between the atom with index source_atom_index and its parent ring atom

            var li = 0;
            foreach (String l in lines)
            {
                if (li >= atomcount + 4 && li < atomcount + 4 + bondcount)
                {
                    //abond
                    Tuple<int, int, int> asBond = Tuple.Create(int.Parse(l.Substring(0, 3).Trim()),
                                                               int.Parse(l.Substring(3, 3).Trim()),
                                                               int.Parse(l.Substring(9, 3).Trim()));
                    String bondName = asBond.Item1 + "_" + asBond.Item2;

                    string fpart = "";
                    if (asBond.Item1 == source_atom_index && asBond.Item2 == destination_atom_index)
                    {

                        source_first = true;
                        bond_type = asBond.Item3;
                        break;

                    }
                    else if (asBond.Item1 == destination_atom_index && asBond.Item2 == source_atom_index)
                    {
                        source_first = false;
                        bond_type = asBond.Item3;
                        break;
                    }
                }
                li++;
            }
            return new Tuple<int, bool>(bond_type, source_first);
        }

        public static IndigoObject InvertAllStereo(IndigoObject mol)
        {
            String mfile = mol.molfile();

            List<String> lines = mfile.Replace("\r", "").Split('\n').ToList();
            int atomcount = int.Parse(lines[3].Substring(0, 3).Trim());
            int bondcount = int.Parse(lines[3].Substring(3, 3).Trim());

            List<Tuple<int, int, int>> stereoBonds = lines.Skip(atomcount + 4)
                 .Filter(l => l.Length > 12)
                 .Filter(l => !l.StartsWith("M"))
                 .Filter(l => l.Substring(9, 3).Trim().Equals("1") || l.Substring(9, 3).Trim().Equals("6"))
                 .Select(l => Tuple.Create(int.Parse(l.Substring(0, 3).Trim()),
                                           int.Parse(l.Substring(3, 3).Trim()),
                                           int.Parse(l.Substring(9, 3).Trim())
                                           ))
                 .ToList();

            var li = 0;
            List<String> nmolfile = lines.Select(l =>
            {
                if (li >= atomcount + 4 && li < atomcount + 4 + bondcount)
                {
                    //abond
                    Tuple<int, int, int> asBond = Tuple.Create(int.Parse(l.Substring(0, 3).Trim()),
                                                               int.Parse(l.Substring(3, 3).Trim()),
                                                               int.Parse(l.Substring(9, 3).Trim()));
                    String bondName = asBond.Item1 + "_" + asBond.Item2;

                    string fpart = "";

                    fpart = ("" + asBond.Item1).PadLeft(3) + ("" + asBond.Item2).PadLeft(3);
                    l = fpart + l.Substring(6, 3) + (InvertMolfileStereoType(asBond.Item3) + "").PadLeft(3) + l.Substring(12);


                }
                li++;
                return l;
            }).ToList();
            String nmol = String.Join("\n", nmolfile);
            return getIndigo().loadQueryMolecule(nmol);
        }

        public static IndigoObject RestoreStereoBond(IndigoObject mol, int source_atom_index, int destination_atom_index, bool source_first, int stereo_type, bool invert_stereo = false)
        {
            String mfile = mol.molfile();

            if (invert_stereo)
            {
                if (stereo_type == 6) { stereo_type = 1; } else if (stereo_type == 1) { stereo_type = 6; }
            }

            List<String> lines = mfile.Replace("\r", "").Split('\n').ToList();
            int atomcount = int.Parse(lines[3].Substring(0, 3).Trim());
            int bondcount = int.Parse(lines[3].Substring(3, 3).Trim());

            List<Tuple<int, int, int>> stereoBonds = lines.Skip(atomcount + 4)
                 .Filter(l => l.Length > 12)
                 .Filter(l => !l.StartsWith("M"))
                 .Filter(l => l.Substring(9, 3).Trim().Equals("1") || l.Substring(9, 3).Trim().Equals("6"))
                 .Select(l => Tuple.Create(int.Parse(l.Substring(0, 3).Trim()),
                                           int.Parse(l.Substring(3, 3).Trim()),
                                           int.Parse(l.Substring(9, 3).Trim())
                                           ))
                 .ToList();

            var li = 0;
            List<String> nmolfile = lines.Select(l => {
                if (li >= atomcount + 4 && li < atomcount + 4 + bondcount)
                {
                    //abond
                    Tuple<int, int, int> asBond = Tuple.Create(int.Parse(l.Substring(0, 3).Trim()),
                                                               int.Parse(l.Substring(3, 3).Trim()),
                                                               int.Parse(l.Substring(9, 3).Trim()));
                    String bondName = asBond.Item1 + "_" + asBond.Item2;

                    string fpart = "";
                    if (asBond.Item1 == source_atom_index && asBond.Item2 == destination_atom_index)
                    {
                        fpart = ("" + asBond.Item1).PadLeft(3) + ("" + asBond.Item2).PadLeft(3);
                        if (source_first)
                        {

                            l = fpart + l.Substring(6, 3) + (stereo_type + "").PadLeft(3) + l.Substring(12);
                        }
                        else
                        {
                            l = fpart + l.Substring(6, 3) + (InvertMolfileStereoType(stereo_type) + "").PadLeft(3) + l.Substring(12);
                        }

                    }
                    else if (asBond.Item1 == destination_atom_index && asBond.Item2 == source_atom_index)
                    {
                        fpart = ("" + asBond.Item1).PadLeft(3) + ("" + asBond.Item2).PadLeft(3);

                        if (source_first)
                        {
                            l = fpart + l.Substring(6, 3) + (InvertMolfileStereoType(stereo_type) + "").PadLeft(3) + l.Substring(12);
                        }
                        else
                        {
                            l = fpart + l.Substring(6, 3) + (stereo_type + "").PadLeft(3) + l.Substring(12);
                        }

                    }
                    else
                    {
                        fpart = ("" + asBond.Item1).PadLeft(3) + ("" + asBond.Item2).PadLeft(3);
                        l = fpart + l.Substring(6, 3) + (asBond.Item3 + "").PadLeft(3) + l.Substring(12);
                    }

                }
                li++;
                return l;
            }).ToList();
            String nmol = String.Join("\n", nmolfile);
            return getIndigo().loadQueryMolecule(nmol);
        }

        public static int InvertMolfileStereoType(int input_stereo)
        {
            if (input_stereo == 1)
            {
                return 6;
            }
            else if (input_stereo == 6)
            {
                return 1;
            }
            else
            {
                return input_stereo;
            }
        }

        public static Tuple<int, int, int, IndigoObject> PreserveLinkBondStereo(IndigoObject mol, int star_atom_index)
        //public static IndigoObject PreserveLinkBondStereo(IndigoObject mol, int star_atom_index)
        {
            foreach (IndigoObject nei in mol.getAtom(star_atom_index).iterateNeighbors())
            {
                if ((nei.bond().bondStereo() == Indigo.UP || nei.bond().bondStereo() == Indigo.UP) && IsRingAtom(nei))
                {
                    return MoveStereoToRing(mol, star_atom_index);
                }
            }
            return Tuple.Create(-1, -1, -1, mol);
        }

        //YP this is to move stereo from off ring bond to a non-stereo ring bond of at the same stereocenter
        //the purpose of this is to preserve the stereo of a star atom when it is deleted upon connection with another fragment
        //return both the new molecule and the indices of the in-ring atoms that the moved bond will connect
        public static Tuple<int, int, int, IndigoObject> MoveStereoToRing(IndigoObject mol, int off_ring_atom_index)
        //public static IndigoObject MoveStereoToRing(IndigoObject mol, int off_ring_atom_index)
        {
            //the plan is:
            //1. find the suitable bond to move stereo to
            //  suitable is a non-stereo single bond between the parent of atom with index off_ring_atom_index and an in-ring atom neighboring it
            //2. change its stereo in molfile to the stereo of the bond between atom with index off_ring_atom_index and its parent
            //3. in the molfile change the stereo of the bond between atom with index off_ring_atom_index and its parent to 0
            String mfile = mol.molfile();


            List<String> lines = mfile.Replace("\r", "").Split('\n').ToList();
            int atomcount = int.Parse(lines[3].Substring(0, 3).Trim());
            int bondcount = int.Parse(lines[3].Substring(3, 3).Trim());

            List<Tuple<int, int, int>> stereoBonds = lines.Skip(atomcount + 4)
                 .Filter(l => l.Length > 12)
                 .Filter(l => l.Substring(9, 3).Trim().Equals("1") || l.Substring(9, 3).Trim().Equals("6"))
                 .Select(l => Tuple.Create(int.Parse(l.Substring(0, 3).Trim()),
                                           int.Parse(l.Substring(3, 3).Trim()),
                                           int.Parse(l.Substring(9, 3).Trim())
                                           ))
                 .ToList();

            //figure out what the stereo is between the atom with index off_ring_atom_index and its parent ring atom
            int parent_atom_index = -1;
            int parent_neighbor_index = -1;
            int bond_to_parent_stereo = -1;
            foreach (IndigoObject parent_atom in mol.getAtom(off_ring_atom_index).iterateNeighbors())
            {
                parent_atom_index = parent_atom.index() + 1;
                //bond_to_parent_stereo = parent_atom.bond().bondStereo();
                //now let's iterate over parents' neighbors and see where we can "move" the stereo bond
                foreach (IndigoObject parent_neighbor in parent_atom.iterateNeighbors())
                {
                    if (parent_neighbor.bond().topology() == Indigo.RING && parent_neighbor.bond().bondOrder() == 1)
                    {
                        parent_neighbor_index = parent_neighbor.index() + 1;
                        break;
                    }
                }
                break;
            }
            //increase by one the atom index since the indices are 1 based in the molfile with which the code below deals
            off_ring_atom_index = off_ring_atom_index + 1;

            //figure out the order in which the atom(off_ring_atom_index) and its parent ring atom are listed in their bond definition
            bool parent_first = false;
            foreach (Tuple<int, int, int> bond_tuple in stereoBonds)
            {
                if (bond_tuple.Item1 == parent_atom_index && bond_tuple.Item2 == off_ring_atom_index)
                {
                    parent_first = true;
                    bond_to_parent_stereo = bond_tuple.Item3;
                }
                if (bond_tuple.Item1 == off_ring_atom_index && bond_tuple.Item2 == parent_atom_index)
                {
                    parent_first = false;
                    bond_to_parent_stereo = bond_tuple.Item3;
                }
            }
            var li = 0;
            List<String> nmolfile = lines.Select(l => {
                if (li >= atomcount + 4 && li < atomcount + 4 + bondcount)
                {
                    //abond
                    Tuple<int, int, int> asBond = Tuple.Create(int.Parse(l.Substring(0, 3).Trim()),
                                                               int.Parse(l.Substring(3, 3).Trim()),
                                                               int.Parse(l.Substring(9, 3).Trim()));
                    String bondName = asBond.Item1 + "_" + asBond.Item2;

                    string fpart = "";
                    if ((asBond.Item1 == parent_atom_index && asBond.Item2 == parent_neighbor_index) || (asBond.Item1 == parent_neighbor_index && asBond.Item2 == parent_atom_index))
                    {

                        if (parent_first)
                        {
                            fpart = ("" + parent_atom_index).PadLeft(3) + ("" + parent_neighbor_index).PadLeft(3);
                        }
                        else
                        {
                            fpart = ("" + parent_neighbor_index).PadLeft(3) + ("" + parent_atom_index).PadLeft(3);
                        }
                        l = fpart + l.Substring(6, 3) + (bond_to_parent_stereo + "").PadLeft(3) + l.Substring(12);
                    }
                    else if ((asBond.Item1 == parent_atom_index && asBond.Item2 == off_ring_atom_index) || (asBond.Item1 == off_ring_atom_index && asBond.Item2 == parent_atom_index))
                    {
                        if (parent_first)
                        {
                            fpart = ("" + parent_atom_index).PadLeft(3) + ("" + off_ring_atom_index).PadLeft(3);
                        }
                        else
                        {
                            fpart = ("" + off_ring_atom_index).PadLeft(3) + ("" + parent_atom_index).PadLeft(3);
                        }
                        l = fpart + l.Substring(6, 3) + (0 + "").PadLeft(3) + l.Substring(12);
                    }
                    else
                    {
                        fpart = ("" + asBond.Item1).PadLeft(3) + ("" + asBond.Item2).PadLeft(3);
                        l = fpart + l.Substring(6, 3) + (asBond.Item3 + "").PadLeft(3) + l.Substring(12);
                    }

                }
                li++;
                return l;
            }).ToList();
            String nmol = String.Join("\n", nmolfile);
            return Tuple.Create(parent_atom_index, parent_neighbor_index, bond_to_parent_stereo, getIndigo().loadQueryMolecule(nmol));
            //return getIndigo().loadQueryMolecule(nmol);
        }

        /*public static IndigoObject RestoreStereoBond(IndigoObject mol, int atom1_index, int atom2_index, int stereo)
        {
            String mfile = mol.molfile();


            List<String> lines = mfile.Replace("\r", "").Split('\n').ToList();
            int atomcount = int.Parse(lines[3].Substring(0, 3).Trim());
            int bondcount = int.Parse(lines[3].Substring(3, 3).Trim());

            List<Tuple<int, int, int>> stereoBonds = lines.Skip(atomcount + 4)
                 .Filter(l => l.Length > 12)
                 .Filter(l => l.Substring(9, 3).Trim().Equals("1") || l.Substring(9, 3).Trim().Equals("6"))
                 .Select(l => Tuple.Create(int.Parse(l.Substring(0, 3).Trim()),
                                           int.Parse(l.Substring(3, 3).Trim()),
                                           int.Parse(l.Substring(9, 3).Trim())
                                           ))
                 .ToList();
            
            var li = 0;
            List<String> nmolfile = lines.Select(l => {
                if (li >= atomcount + 4 && li < atomcount + 4 + bondcount)
                {
                    //abond
                    Tuple<int, int, int> asBond = Tuple.Create(int.Parse(l.Substring(0, 3).Trim()),
                                                               int.Parse(l.Substring(3, 3).Trim()),
                                                               int.Parse(l.Substring(9, 3).Trim()));
                    String bondName = asBond.Item1 + "_" + asBond.Item2;

                    string fpart = "";
                    if ((asBond.Item1 == atom1_index && asBond.Item2 == atom2_index) || (asBond.Item1 == atom2_index && asBond.Item2 == atom1_index))
                    {
                        fpart = ("" + asBond.Item1).PadLeft(3) + ("" + asBond.Item2).PadLeft(3);
                        l = fpart + l.Substring(6, 3) + (stereo + "").PadLeft(3) + l.Substring(12);
                    }
                    else
                    {
                        fpart = ("" + asBond.Item1).PadLeft(3) + ("" + asBond.Item2).PadLeft(3);
                        l = fpart + l.Substring(6, 3) + (asBond.Item3 + "").PadLeft(3) + l.Substring(12);
                    }

                }
                li++;
                return l;
            }).ToList();
            String nmol = String.Join("\n", nmolfile);
            return getIndigo().loadQueryMolecule(nmol);
        }
        */

        //YP this is to move stereo from inside ring bond to a non-stereo off ring bond of at the same stereocenter
        public static IndigoObject MoveStereoFromRingBond(IndigoObject mol, int ring_stereo_bond_index, List<int> parent_atoms_indices)
        {
            //the plan is:
            //1. find the suitable bond to move stereo to
            //  suitable is a non-stereo single bond between the parent of atom with index off_ring_atom_index and an off-ring atom neighboring it
            //2. change its stereo in molfile to the stereo of the bond with index ring_stereo_bond_index 
            //3. in the molfile change the stereo of the bond bond with index ring_stereo_bond_index to 0
            String mfile = mol.molfile();

            List<String> lines = mfile.Replace("\r", "").Split('\n').ToList();
            int atomcount = int.Parse(lines[3].Substring(0, 3).Trim());
            int bondcount = int.Parse(lines[3].Substring(3, 3).Trim());

            List<Tuple<int, int, int>> stereoBonds = lines.Skip(atomcount + 4)
                 .Filter(l => l.Length > 12)
                 .Filter(l => l.Substring(9, 3).Trim().Equals("1") || l.Substring(9, 3).Trim().Equals("6"))
                 .Select(l => Tuple.Create(int.Parse(l.Substring(0, 3).Trim()),
                                           int.Parse(l.Substring(3, 3).Trim()),
                                           int.Parse(l.Substring(9, 3).Trim())
                                           ))
                 .ToList();

            //ring_stereo_bond_stereo is the stereo of the in-ring bond to be moved
            int ring_stereo_bond_stereo = -1;
            //int ring_stereo_bond_stereo = mol.getBond(ring_stereo_bond_index).bondStereo();
            //find suitable bond to move stereo to
            int suitable_off_ring_bond_index = -1;
            //shared_parent_atom_index is an index of the parent atom that is shared between the in-ring bond from which stereo is to be moved
            //and the off-ring bond to which stereo is to be moved
            int shared_parent_atom_index = -1;
            //non_shared_parent_atom_index is the index of the other parent atom of the off-ring bond.
            int non_shared_parent_atom_index = -1;

            foreach (int parent_atom_index in parent_atoms_indices)
            {
                IndigoObject parent_atom = mol.getAtom(parent_atom_index);

                foreach (IndigoObject parent_atom_neighbor in parent_atom.iterateNeighbors())
                {
                    if ((parent_atom_neighbor.bond().topology() == Indigo.CHAIN || parent_atom_neighbor.bond().topology() == 0) && parent_atom_neighbor.bond().bondOrder() == 1)
                    {
                        suitable_off_ring_bond_index = parent_atom_neighbor.bond().index();
                        non_shared_parent_atom_index = parent_atom_neighbor.index();
                        shared_parent_atom_index = parent_atom.index();
                        break;
                    }
                }
                if (suitable_off_ring_bond_index != -1) { break; }
            }
            if (suitable_off_ring_bond_index == -1)
            {
                return mol;
            }
            //figure out the order in which the the atoms connected by the in-ring bond are listed in their bond definition
            //as well as the stereo of the bond
            bool shared_atom_first = false;
            foreach (Tuple<int, int, int> bond_tuple in stereoBonds)
            {
                if (parent_atoms_indices.Contains(bond_tuple.Item1 - 1) && parent_atoms_indices.Contains(bond_tuple.Item2 - 1) && bond_tuple.Item1 == shared_parent_atom_index + 1)
                //if (parent_atoms_indices.Contains(bond_tuple.Item1) && parent_atoms_indices.Contains(bond_tuple.Item2) && bond_tuple.Item1 == shared_parent_atom_index + 1)
                {
                    shared_atom_first = true;
                    ring_stereo_bond_stereo = bond_tuple.Item3;
                }
                if (parent_atoms_indices.Contains(bond_tuple.Item1 - 1) && parent_atoms_indices.Contains(bond_tuple.Item2 - 1) && bond_tuple.Item2 == shared_parent_atom_index + 1)
                //if (parent_atoms_indices.Contains(bond_tuple.Item1) && parent_atoms_indices.Contains(bond_tuple.Item2) && bond_tuple.Item2 == shared_parent_atom_index + 1)
                {
                    shared_atom_first = false;
                    ring_stereo_bond_stereo = bond_tuple.Item3;
                }
            }
            var li = 0;
            List<String> nmolfile = lines.Select(l => {
                if (li >= atomcount + 4 && li < atomcount + 4 + bondcount)
                {
                    //abond
                    Tuple<int, int, int> asBond = Tuple.Create(int.Parse(l.Substring(0, 3).Trim()),
                                                               int.Parse(l.Substring(3, 3).Trim()),
                                                               int.Parse(l.Substring(9, 3).Trim()));
                    String bondName = asBond.Item1 + "_" + asBond.Item2;

                    string fpart = "";
                    if ((asBond.Item1 - 1 == shared_parent_atom_index && asBond.Item2 - 1 == non_shared_parent_atom_index) || (asBond.Item1 - 1 == non_shared_parent_atom_index && asBond.Item2 - 1 == shared_parent_atom_index))
                    {

                        if (shared_atom_first)
                        {
                            fpart = ("" + (shared_parent_atom_index + 1)).PadLeft(3) + ("" + (non_shared_parent_atom_index + 1)).PadLeft(3);
                        }
                        else
                        {
                            fpart = ("" + (non_shared_parent_atom_index + 1)).PadLeft(3) + ("" + (shared_parent_atom_index + 1)).PadLeft(3);
                        }
                        l = fpart + l.Substring(6, 3) + (ring_stereo_bond_stereo + "").PadLeft(3) + l.Substring(12);
                    }
                    else if (parent_atoms_indices.Contains(asBond.Item1 - 1) && parent_atoms_indices.Contains(asBond.Item2 - 1))
                    {
                        fpart = ("" + asBond.Item1).PadLeft(3) + ("" + asBond.Item2).PadLeft(3);
                        l = fpart + l.Substring(6, 3) + (0 + "").PadLeft(3) + l.Substring(12);
                    }
                    else
                    {
                        fpart = ("" + asBond.Item1).PadLeft(3) + ("" + asBond.Item2).PadLeft(3);
                        l = fpart + l.Substring(6, 3) + (asBond.Item3 + "").PadLeft(3) + l.Substring(12);
                    }

                }
                li++;
                return l;
            }).ToList();
            String nmol = String.Join("\n", nmolfile);
            return getIndigo().loadQueryMolecule(nmol);
        }


        public static bool IsRingStereoBondMovable(IndigoObject mol, int ring_stereo_bond_index, List<int> parent_atoms_indices)
        {
            foreach (int parent_atom_index in parent_atoms_indices)
            {
                IndigoObject parent_atom = mol.getAtom(parent_atom_index);
                foreach (IndigoObject parent_atom_neighbor in parent_atom.iterateNeighbors())
                {
                    if ((parent_atom_neighbor.bond().topology() == Indigo.CHAIN || parent_atom_neighbor.bond().topology() == 0) && parent_atom_neighbor.bond().bondOrder() == 1)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        public static IndigoObject MoveStereoFromRings_old(IndigoObject mol)
        {
            IndigoObject check_mol = mol;
            List<int> bond_atoms = new List<int>();
            bool check_mol_changed = false;
            while (MovableRingStereoBondsExist(check_mol))
            {
                check_mol_changed = false;
                foreach (IndigoObject atom in check_mol.iterateAtoms())
                {
                    foreach (IndigoObject nei in atom.iterateNeighbors())
                    {
                        if (nei.bond().topology() == Indigo.RING && (nei.bond().bondStereo() == Indigo.UP || nei.bond().bondStereo() == Indigo.DOWN))
                        {
                            bond_atoms.Add(atom.index());
                            bond_atoms.Add(nei.index());
                            check_mol = MoveStereoFromRingBond(check_mol, nei.bond().index(), bond_atoms);
                            bond_atoms.Clear();
                            check_mol_changed = true;
                            break;
                        }
                    }
                    //if (check_mol_changed) { break; }
                }
            }
            return check_mol;
        }

        public static IndigoObject MoveStereoFromRings(IndigoObject mol)
        {
            IndigoObject check_mol = mol;
            List<int> bond_atoms = new List<int>();
            bool check_mol_changed = true;
            List<int> checked_bond_indices = new List<int>();
            //check_mol_changed = false;

            //bool all_bonds_moved = false;
            while (check_mol_changed)
            {
                check_mol_changed = false;
                foreach (IndigoObject atom in check_mol.iterateAtoms())
                {
                    foreach (IndigoObject nei in atom.iterateNeighbors())
                    {
                        bond_atoms.Clear();
                        bond_atoms.Add(atom.index());
                        bond_atoms.Add(nei.index());

                        if (nei.bond().topology() == Indigo.RING && (nei.bond().bondStereo() == Indigo.UP || nei.bond().bondStereo() == Indigo.DOWN) && !checked_bond_indices.Contains(nei.bond().index()))
                        {
                            checked_bond_indices.Add(nei.bond().index());
                            if (IsRingStereoBondMovable(mol, nei.bond().index(), bond_atoms))
                            {
                                //bond_atoms.Add(atom.index());
                                //bond_atoms.Add(nei.index());
                                check_mol = MoveStereoFromRingBond(check_mol, nei.bond().index(), bond_atoms);
                                bond_atoms.Clear();
                                check_mol_changed = true;
                                break;
                            }
                        }
                    }
                    if (check_mol_changed) { break; }
                    //all_bonds_moved = true;
                }
            }

            return check_mol;
        }
        public static IndigoObject MoveStereoFromRings_v2(IndigoObject mol)
        {
            IndigoObject check_mol = mol;
            List<int> bond_atoms = new List<int>();
            bool check_mol_changed = false;
            List<int> checked_bond_indices = new List<int>();
            //check_mol_changed = false;
            foreach (IndigoObject atom in check_mol.iterateAtoms())
            {
                foreach (IndigoObject nei in atom.iterateNeighbors())
                {
                    bond_atoms.Clear();
                    bond_atoms.Add(atom.index());
                    bond_atoms.Add(nei.index());

                    if (nei.bond().topology() == Indigo.RING && (nei.bond().bondStereo() == Indigo.UP || nei.bond().bondStereo() == Indigo.DOWN) && !checked_bond_indices.Contains(nei.bond().index()))
                    {
                        checked_bond_indices.Add(nei.bond().index());
                        if (IsRingStereoBondMovable(mol, nei.bond().index(), bond_atoms))
                        {
                            //bond_atoms.Add(atom.index());
                            //bond_atoms.Add(nei.index());
                            check_mol = MoveStereoFromRingBond(check_mol, nei.bond().index(), bond_atoms);
                            bond_atoms.Clear();
                            //check_mol_changed = true;
                            break;
                        }  
                    }
                }
                //if (check_mol_changed) { break; }
            }
            
            return check_mol;
        }

        public static bool MovableRingStereoBondsExist(IndigoObject mol)
        {
            List<IndigoObject> stereo_bond_atoms = new List<IndigoObject>();

            foreach (IndigoObject atom in mol.iterateAtoms())
            {

                foreach (IndigoObject nei in atom.iterateNeighbors())
                {
                    if (nei.bond().topology() == Indigo.RING && (nei.bond().bondStereo() == Indigo.UP || nei.bond().bondStereo() == Indigo.DOWN))
                    {
                        //found stereo bond
                        stereo_bond_atoms.Clear();
                        stereo_bond_atoms.Add(atom);
                        stereo_bond_atoms.Add(nei);

                        foreach (IndigoObject parent_atom in stereo_bond_atoms)
                        {
                            foreach (IndigoObject parent_atom_neighbor in parent_atom.iterateNeighbors())
                            {
                                if ((parent_atom_neighbor.bond().topology() == Indigo.CHAIN || parent_atom_neighbor.bond().topology() == 0) && parent_atom_neighbor.bond().bondOrder() == 1)
                                {
                                    return true;
                                }
                            }
                        }
                    }
                }
            }
            return false;
        }

        public static bool RingStereoBondsExist(IndigoObject mol)
        {
            foreach (IndigoObject bond in mol.iterateBonds())
            {
                if (bond.topology() == Indigo.RING && (bond.bondStereo() == Indigo.UP || bond.bondStereo() == Indigo.DOWN))
                {
                    return true;
                }
            }
            return false;
        }

        public static NAFragment FragmentResolve(string key, PolymerBaseReadingState state)
        {
            NAFragment f = null;
            string term = key.ToLower();
            if (state.NAFragmentsCache.ContainsKey(term))
            {
                f = state.NAFragmentsCache[term];
                state.CachedFragment = true;
            }
            else
            {
                f = PolymerBaseExtensions.NAFragmentFactory.Resolve(key);
                if (f != null)
                {
                    f = f.Clone();
                    state.NAFragmentsCache.Add(term, f);
                }
            }
            return f;
        }

        private static Indigo _indigo;

        public static Indigo getIndigo()
        {
            if (_indigo == null) _indigo = new Indigo();
            return _indigo;
        }

        private static void SiteAdjustNAModificationAmount(NAStructuralModificationGroup g)
        {
            NAStructuralModification mod = g.Modification;
            if (g.NucleotideSites.Count() != 0)
            {

                if (mod.Amount.Low != null)
                {
                    mod.Amount.Low = Math.Round(mod.Amount.Low.GetValueOrDefault() / g.NucleotideSites.Count(), 2);
                }
                if (mod.Amount.High != null)
                {
                    mod.Amount.High = Math.Round(mod.Amount.High.GetValueOrDefault() / g.NucleotideSites.Count(), 2);
                }
                if (mod.Amount.Center != null)
                {
                    mod.Amount.Center = Math.Round(mod.Amount.Center.GetValueOrDefault() / g.NucleotideSites.Count(), 2);
                    //mod.Amount.AmountType = AmountType.UncertainZero;
                }
                if (mod.Amount.Numerator != null)
                {
                    mod.Amount.Numerator = Math.Round(mod.Amount.Numerator.GetValueOrDefault() / g.NucleotideSites.Count(), 2);
                }
            }
        }

        private static void ApplyAmountRules(NAStructuralModificationGroup g)
        {
            AmountRules ar = new AmountRules(g.Modification.Extent + "###" + g.Amount.SrsAmountType + "###" + g.Amount.ExtentAmountUnits);
            if (ar.Id != null)
            {
                if (ar.DivideBy100) g.Modification.Amount.DivideBy100();
                g.Modification.Amount.Unit = ar.SPLUnits;
                if (ar.SiteAdjust) SiteAdjustNAModificationAmount(g);
            }
            else
            {
                //YP SRS-374
                //for now do site adjustment for everything even if it's not found in amount rules
                SiteAdjustNAModificationAmount(g);
            }
        }
    }
}
