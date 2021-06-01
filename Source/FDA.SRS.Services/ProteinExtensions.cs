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
using System.Text.RegularExpressions;
using System.Xml.Linq;
using System.Xml.XPath;
using System.Threading.Tasks;

namespace FDA.SRS.Processing
{
    public static class ProteinExtensions
    {
        //xProtein is the xml document
        public static Protein ReadProtein(this Protein protein, XElement xProtein, SplObject rootObject, ImportOptions impOpt, ConvertOptions opt)
        {
            PolymerBaseReadingState state = new PolymerBaseReadingState { RootObject = rootObject };

            SRSReadingUtils.readElement(xProtein, "SEQUENCE_TYPE", st => ValidatedValues.SequenceTypes.Keys.Contains(st), st => protein.SeqType = st, protein.UNII);

            // Subunits
            int nDeclSubunits = 0;
            SRSReadingUtils.readElement(xProtein, "NUMBER_OF_SUBUNITS", n => int.TryParse(n, out nDeclSubunits), n => nDeclSubunits = int.Parse(n), protein.UNII);

            IEnumerable<XElement> xels = xProtein.XPathSelectElements("SUBUNIT_GROUP");
            if (nDeclSubunits != xels.Count())
                TraceUtils.WriteUNIITrace(TraceEventType.Warning, protein.UNII, null, "Declared NUMBER_OF_SUBUNITS does not match found one: {0} != {1}", nDeclSubunits, xels.Count());

            foreach (XElement x in xels)
            {
                Subunit su = protein.ReadSubunit(x, state);
                protein.Subunits.Add(su);
                //begin YB
                if (su.Sequence.ToString().Any(c => String.IsNullOrEmpty(AminoAcids.GetNameByLetter(c))))
                    TraceUtils.ReportError("seq_ref", protein.UNII, "Unknown letter(s) used in sequence");
                //end YB
            }

            // Disulfide links
            xProtein
                .XPathSelectElements("DISULFIDE_LINKAGE")
                .ForAll(x => {
                    if (!String.IsNullOrWhiteSpace(x.Value))
                    {
                        protein
                            .Links
                            .AddRange(
                                Helpers.SequenceLinks(x.Value)
                                .Select(tt => PolymerBaseExtensions.FragmentFactory.CreateLink(protein, tt, "cys-cys", rootObject))
                            );
                    }
                });

            // Check if any disulfide links were referenced and resolve/add cystein in such case
            if (protein.Links.Any(l => l.LinkType == "cys-cys" && l.Linker == null))
                throw new SrsException("fragment", "Disulfide link (cys-cys) cannot be resolved - make sure you have proper registry.sdf");

            // Other links. Constructed based on the same principle as disulfide links.
            xProtein
                .XPathSelectElements("OTHER_LINKAGE")
                .ForAll(x => {
                    if (!String.IsNullOrWhiteSpace(x.Value))
                    {
                        var ol = protein.ReadOtherLinkage(x);
                        if (!String.IsNullOrEmpty(ol.Item1))
                        {
                            protein
                                .Links
                                .AddRange(
                                    Helpers.SequenceLinks(ol.Item1)
                                    .Select(tt => PolymerBaseExtensions.FragmentFactory.CreateLink(protein, tt, ol.Item2, rootObject))
                                );
                        }
                    }
                });

            // Glycosylation
            protein.ReadGlycosylation(xProtein.XPathSelectElement("GLYCOSYLATION"), state);

            if (protein.Glycosylations.Count() == 0)
                protein.ReadGlycosylation(xProtein.XPathSelectElement("GLYCOSYLATION_GROUP"), state);

            // Physical modifications
            xProtein
                .XPathSelectElements("MODIFICATION_GROUP/PHYSICAL_MODIFICATION_GROUP")
                .ForAll(x => {
                    if (!String.IsNullOrWhiteSpace(x.Value))
                    {
                        var g = protein.ReadPhysicalModificationGroup(x, state);
                        if (g != null)
                            protein.Modifications.Add(g);
                    }
                });

            // Agent modifications

            xProtein
               .XPathSelectElements("MODIFICATION_GROUP/AGENT_MODIFICATION_GROUP")
               .ForAll(x => {
                   if (!String.IsNullOrWhiteSpace(x.Value))
                   {
                       var g = protein.ReadAgentModification(x, state);
                       if (g != null)
                           protein.Modifications.Add(g);
                   }
               });


            // Structural modifications
            xProtein
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
                        var g = protein.ReadStructuralModificationGroup(x, state);
                        protein.ProcessStructuralModificationGroup(g, state);
                    }
                });

            // In strict mode check that if MOL exists in SDF record then it was used to resolve at least one fragment
            if (opt.Strict && !state.ExternalMolUsed && MoleculeExtensions.IsCorrectMol(protein.Sdf.Mol))
                throw new SrsException("external_mol", "External MOL exists but wasn't referenced in any of fragments in SRS record");

            // Check that all links have associated fragments and all connection points are defined for all fragments
            foreach (var link in protein.Links.Where(l => !l.isDisulfideType()))
            {
                if (link.Linker == null)
                    throw new SrsException("link", "Link from OTHER_LINKAGE has no fragment associated/resolved");
                if (link.Linker.Connectors.Count != link.Sites.Count)
                    throw new SrsException("link", "Not all link sites from OTHER_LINKAGE have connection points for fragment defined");
                if (!link.IsCompletelyDefined)
                    throw new SrsException("link", "Link from OTHER_LINKAGE is not completely defined");

                // and register Fragment in an enclosing container (protein)
                protein.RegisterFragment(link.Linker);
            }

            // Molecular Weight
            var xMW = xProtein.XPathSelectElement("MOLECULAR_WEIGHT");
            if (xMW != null && !String.IsNullOrEmpty(xMW.Value))
                protein.MolecularWeight = protein.ReadMolecularWeight(xMW, state);

            protein.Canonicalize();

            return protein;
        }



        public static void ProcessStructuralModificationGroup(this Protein protein, StructuralModificationGroup g, PolymerBaseReadingState state)
        {
            if (g != null)
            {
                bool groupUsedInLinks = false;

                //Okay, the idea here is that a structural modification is _for_ a linkage
                //if (and only if) all sites specified in that structural modification are the same
                //as the sites specified in the linkage. At least that's how it SHOULD work.

                HashSet<String> uniqueModSitesList = g.ResidueSites.Select(s => s.UID).OrderBy(u => u).ToHashSet();

                String uniqueModSites = String.Join(";", uniqueModSitesList);

                // may have to change this for d-cys
                // Iterate through non cys-cys links (e.g. <OTHER_LINKAGE><SITE>1_125-1_275-1_368</SITE><LINKAGE_TYPE/></OTHER_LINKAGE>)
                foreach (var link in protein.Links.Where(l => !l.isDisulfideType()))
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
                        foreach (var modSite in g.ResidueSites)
                        {
                            if (linkSite.UID == modSite.UID)
                            {
                                if (g.Modification.Fragment.Connectors.Count != 1 && !state.CachedFragment)
                                    throw new SrsException("link", "Two and only two connectors (C, N) per fragment associated with OTHER_LINK(AGE) is allowed");
                                else if (link.Linker == null)
                                    // This is the first time we hit the fragment associated with previously identified OTHER_LINK(AGE)
                                    link.Linker = g.Modification.Fragment;
                                //else if ( link.Linker.UID == g.Modification.Fragment.UID )  // ???
                                //	throw new SRSException("link", "Same fragment (molecule + connectors) cannot be used twice in the same link");
                                else if (link.Linker.Molecule.InChIKey != g.Modification.Fragment.Molecule.InChIKey)
                                    throw new SrsException("linkFragment", "Different structural moiety (molecule) is not allowed in the same OTHER_LINK(AGE)");
                                else
                                { // Keep adding connectors to a previously identified Linker
                                    Fragment.Connector con = g.Modification.Fragment.Connectors.First();

                                    if (!link.Linker.Connectors.Contains(con))
                                    {
                                        link.Linker.Connectors.Add(con);
                                    }
                                }

                                // Link protein site with fragment's connector and update connector id

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
                    protein.Modifications.Add(g);
                    //if (g.ModificationType!= "AMINO ACID REMOVAL")
                    //{
                    protein.RegisterFragment(g.Modification.Fragment);  // ...and add Fragment if not added yet
                                                                        //}

                }
            }
        }

        /// <summary>
        /// This method simply adds a blank PhysicalModification to the front of the modification
        /// list of a protein if no PhysicalModification exists there already. This is not a good idea,
        /// in general, but will help the protein have a compatible hash with the legacy reader which sometimes
        /// respected null physical modifications and would add them to the modifications list. This method
        /// should never be called except for in debugging cases.
        /// </summary>
        /// <param name="protein"></param>
        public static void LegacyCanonicalize(this Protein protein)
        {
            int c = protein.Modifications
                .Where(m => m is PhysicalModification)
                .Count();

            //This is a terrible idea to try to sometimes have backwards
            //compatibility.
            if (c == 0)
            {
                PhysicalModification m = new PhysicalModification(protein.Subunits[0].RootObject);
                List<ProteinModification> omods = protein.Modifications;
                protein.Modifications = new List<ProteinModification>();
                protein.Modifications.Add(m);
                protein.Modifications.AddRange(omods);
            }
        }

        public static void Canonicalize(this Protein protein)
        {
            // Sort connectors in canonical order and assigned id as an ordinal in canonical order
            foreach (var f in protein.Fragments)
            {
                //YP changed to custom sorting function in order to consider 0 atom index as a high number rather than lowf
                //YP never mind, commented out SortConnectors since Yulia is entering correct order manually
                //f.SortConnectors();

                f.Connectors = f.Connectors.OrderBy(c => c.Snip).ToList();
                f.Connectors.ForEachWithIndex((c, i) => c.Id = i);
            }

            //YP overwriting the above assignment of Ids to connectors per SRS-337, Yulia enters things in pre-defined order that needs to be kept
            //regardless of the order of connectors in registry
            foreach (Link link in protein.Links)
            {
                link.Sites.ForEachWithIndex((s, i) => {
                    if (s.ConnectorRef != null)
                    {
                        s.ConnectorRef.Id = i;
                    }
                });
            }

            //Ticket 271: Commented for Agent Mdification
            //Remove all nonsense structural mods
            /*
                        protein.Modifications.RemoveAll(m => {
                            if (m is StructuralModificationGroup) {
                                return ((StructuralModificationGroup)m).Modification == null;
                            }
                            return false;
                        });
            */

            protein.Modifications
                   .RemoveAll(m => m.DefiningParts.Equals(""));



            //addGlycosylations(protein, type, xGlyc, "N_GLYCOSYLATION", "N", state);
            //addGlycosylations(protein, type, xGlyc, "O_GLYCOSYLATION", "O", state);
            //addGlycosylations(protein, type, xGlyc, "C_GLYCOSYLATION", "C", state);
            //N first, then O, then C
            //Then sort by subunit
            //Then by position

            List<string> glycPrefOrder = new string[] { "N", "O", "C" }.ToList();

            protein.Glycosylations.Sort((a, b) => {
                int o = a.GlycosylationType.CompareTo(b.GlycosylationType);
                if (o != 0) return o;

                o = glycPrefOrder.IndexOf(a.Attachment.AttachmentType) - glycPrefOrder.IndexOf(b.Attachment.AttachmentType);
                if (o != 0) return o;

                o = a.Attachment.Site.Subunit.Ordinal - b.Attachment.Site.Subunit.Ordinal;
                if (o != 0) return o;

                o = a.Attachment.Site.Position - b.Attachment.Site.Position;
                if (o != 0) return o;

                return a.UID.CompareTo(b.UID);
            });

            //remove empty sequences
            protein.Subunits.RemoveAll(su => {
                if (su.Sequence == null) return true;
                return su.Sequence.ToString().Trim().Equals("");
            });

            Dictionary<string, HashSet<string>> fragPosMap = new Dictionary<string, HashSet<string>>();
            Dictionary<string, Fragment> fragMap = new Dictionary<string, Fragment>();



            protein.Modifications
                .Where(m => m is StructuralModificationGroup)
                .Select(m => (StructuralModificationGroup)m)
                .ForAll(m => {
                    Fragment f = m.Modification.Fragment;
                    HashSet<string> pos;
                    pos = fragPosMap.TryGetValue(f.Id, out pos) ? pos : null;
                    if (pos == null)
                    {
                        pos = new HashSet<string>();
                        fragPosMap.Add(f.Id, pos);
                        fragMap.Add(f.Id, f);
                    }

                    m.ResidueSites.ForEach(s => {

                        if (s.Position == 0)
                        {
                            pos.Add("N");
                        }
                        else if (s.Position == s.Subunit.Sequence.Length - 1)
                        {
                            pos.Add("C");
                        }
                        else
                        {
                            pos.Add("M");
                        }
                    });
                });

            fragPosMap.AsEnumerable().ForAll(kv => {
                Fragment frag = fragMap[kv.Key];
                if (kv.Value.Count() == 1)
                {

                    if (frag.Connectors.Count() == 1)
                    {
                        Fragment.Connector con = frag.Connectors.First();
                        //C-term only
                        if (kv.Value.Contains("C"))
                        {
                            con.Snip = Tuple.Create(con.Snip.Item1, 0);
                        }
                        else if (kv.Value.Contains("N"))
                        {
                            con.Snip = Tuple.Create(0, con.Snip.Item2);
                        }
                    }
                }

            });

            //Cannonicalize modification order
            protein.Modifications = protein.Modifications.OrderBy(m => m.UID).ToList();

            /*Commented for Ticket 271:Agent Modification
              //For now, throw an excpetion if there are any agent modifications
              protein.Modifications
                  .Where(m => m is AgentModification)
                  .ForAll(m => {
                      throw new Exception("Non-empty agent modifications not supported at this time!");
                  });
             */
            //For now, throw an excpetion if there are any physical modifications
            protein.Modifications
                .Where(m => m is PhysicalModification)
                .ForAll(m => {
                    throw new Exception("Non-empty physical modifications not supported at this time!");
                });

        }

        public static void RegisterFragment(this Protein protein, Fragment fragment)
        {
            if (!protein.Fragments.Any(f => f.UID == fragment.UID))
                protein.Fragments.Add(fragment);
        }

        /*
		 * <SITE>3_99-4_99</SITE>
		 * <LINKAGE_TYPE>DIAMIDE </LINKAGE_TYPE>
		 */
        public static Tuple<string, string> ReadOtherLinkage(this Protein protein, XElement xOtherLinkage)
        {
            string site = null, linkageType = null;
            SRSReadingUtils.readElement(xOtherLinkage, "SITE", null, v => site = v.Trim(), protein.UNII);
            SRSReadingUtils.readElement(xOtherLinkage, "LINKAGE_TYPE", null, v => linkageType = v.Trim(), protein.UNII);
            return new Tuple<string, string>(site, linkageType);
        }

        //expects 0-indexed site for both residue and subunit index
        public static char residueAt(this Protein protein, Tuple<int, int> site)
        {
            return protein.Subunits.Where(su => su.Ordinal == (site.Item1 + 1))
                            .Select(su => su.Sequence.ToString()[site.Item2])
                            .First();
        }

        /*
		 * Get the sites for a specific residue (e.g. "C")
		 */
        public static List<Tuple<Subunit, int>> SitesMatchingResidue(this Protein protein, char res)
        {
            List<Tuple<Subunit, int>> sites = new List<Tuple<Subunit, int>>();

            protein.Subunits.ForEach(sub => {

                sub.Sequence.SitesMatchingResidue(res)
                .Map(i => Tuple.Create(sub, i))
                .ForAll(t => sites.Add(t));
            });
            return sites;

        }

        public static StructuralModificationGroup ReadStructuralModificationGroup(this Protein protein, XElement xStrModGroup, PolymerBaseReadingState state)
        {
            StructuralModificationGroup g = new StructuralModificationGroup(state.RootObject);

            // Check if modification group is not empty
            XElement xel = xStrModGroup.XPathSelectElement("RESIDUE_SITE");
            if (xel == null || String.IsNullOrWhiteSpace(xel.Value))
                TraceUtils.ReportError("mandatory_field_missing", protein.UNII, "Residue sites are not specified - skipping the rest of modification");

            // Try to read fragment
            xel = xStrModGroup.XPathSelectElement("MOLECULAR_FRAGMENT_MOIETY");
            if (xel != null)
            {
                StructuralModification m = new StructuralModification(state.RootObject);
                g.Modification = protein.ReadStructuralModification(m, xel, state);
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
                SRSReadingUtils.readElement(xStrModGroup, "AMOUNT_TYPE", ValidatedValues.AmountTypes.Keys.Contains, v => g.Modification.Amount.SrsAmountType = v, protein.UNII);
            }

            // Read protein sites
            xel = xStrModGroup.XPathSelectElement("RESIDUE_SITE");
            g.ResidueSites =
                Helpers.SequencePositions(xel.Value)
                .Select(t => {
                    if (t.Item1 == -1)
                    {
                        if (protein.Subunits.Count != 1)
                            throw new SrsException("subunit_ref", string.Format("Non-recoveranle reference to non-existing subunit: {0}", xel.Value));

                        t = new Tuple<int, int>(0, t.Item2);
                        TraceUtils.WriteUNIITrace(TraceEventType.Warning, protein.UNII, null, "Reference corrected as the only unit is available");
                    }
                    if (t.Item1 >= protein.Subunits.Count)
                        throw new SrsException("subunit_ref", String.Format("Reference to non-existing subunit: {0}", t));
                    if (t.Item2 >= protein.Subunits[t.Item1].Sequence.Length)
                        throw new SrsException("subunit_ref", String.Format("Reference to non-existing position: {0}", t));

                    //begin YB: added position on proximal moiety (fragment)
                    return new ProteinSite(state.RootObject, "AMINO ACID SUBSTITUTION POINT") { Subunit = protein.Subunits[t.Item1], Position = t.Item2, ConnectorRef = g.Modification.Fragment.Connectors.First() };
                    //end YB: added position on proximal moiety (fragment)
                }).ToList();

            SRSReadingUtils.readElement(xStrModGroup, "RESIDUE_MODIFIED", aa => AminoAcids.IsValidAminoAcidName(aa), v => g.Residue = v, protein.UNII);

            if (!String.IsNullOrEmpty(g.Residue) && g.ResidueSites.Any(s => !String.Equals(AminoAcids.GetNameByLetter(s.Letter), g.Residue, StringComparison.InvariantCultureIgnoreCase)))
                TraceUtils.WriteUNIITrace(TraceEventType.Error, protein.UNII, null, "Residue {0} does not match all positions", g.Residue);

            if (String.IsNullOrEmpty(g.Residue) && g.ResidueSites.Count == 1)
            {
                ProteinSite site = g.ResidueSites.First();
                g.Residue = AminoAcids.GetNameByLetter(site.Letter);
                TraceUtils.WriteUNIITrace(TraceEventType.Information, protein.UNII, null, "Residue restored from position: {0} => {1} => {2}", site, site.Letter, g.Residue);
            }

            return g;
        }

        public static char ResolveAAName(String res)
        {
            Dictionary<string, string> aadict = @"ALANINE	A
CYSTEINE	C
ASPARTATE	D
GLUTAMATE	E
PHENYLALANINE	F
GLYCINE	G
HISTIDINE	H
ISOLEUCINE	I
LYSINE	K
LEUCINE	L
METHIONINE	M
ASPARAGINE	N
PROLINE	P
GLUTAMINE	Q
ARGININE	R
SERINE	S
THREONINE	T
VALINE	V
TRYPTOPHAN	W
TYROSINE	Y
ALA	A
CYS	C
ASP	D
GLU	E
PHE	F
GLY	G
HIS	H
ILE	I
LYS	K
LEU	L
MET	M
ASN	N
PRO	P
GLN	Q
ARG	R
SER	S
THR	T
VAL	V
TRP	W
TYR	Y
A	A
C	C
D	D
E	E
F	F
G	G
H	H
I	I
K	K
L	L
M	M
N	N
P	P
Q	Q
R	R
S	S
T	T
V	V
W	W
Y	Y".Replace("\r", "")
               .Split('\n')
               .ToDictionary(l => l.Split('\t')[0], l => l.Split('\t')[1]);

            string aaSingleLetter = aadict.TryGetValue(res.ToUpper(), out aaSingleLetter) ? aaSingleLetter : null;

            if (aaSingleLetter == null) return '0';
            return aaSingleLetter.ToCharArray()[0];

        }


        public static StructuralModificationGroup ReadStructuralModificationJson(this Protein protein, JToken jStrModGroup, PolymerBaseReadingState state)
        {
            StructuralModificationGroup g = new StructuralModificationGroup(state.RootObject);

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


            SRSReadingUtils.readJsonElement(jStrModGroup, "residueModified", ex => true, v => residue = v, protein.UNII);


            SRSReadingUtils.readJsonElement(jStrModGroup, "structuralModificationType", tp => true, v => modType = v, protein.UNII);


            //YP for SRS-363
            g.ModificationType = modType;



            // Check if modification group is not empty
            JToken jsites = jStrModGroup.SelectToken("sites");
            if ((jsites == null || jsites.Count() == 0) && residue == null)
                TraceUtils.ReportError("mandatory_field_missing", protein.UNII, "Residue sites are not specified, and no AA specified - skipping the rest of modification");

            List<Tuple<Subunit, int>> csites = new List<Tuple<Subunit, int>>();



            if ((jsites == null || jsites.Count() == 0))
            {
                if (!modType.Equals("MOIETY"))
                {
                    TraceUtils.WriteUNIITrace(TraceEventType.Warning, protein.UNII, null, "Residue sites are not specified, but AA specified, calculating sites.");
                    char res1Let = ResolveAAName(residue);

                    if (res1Let == '0')
                    {
                        TraceUtils.WriteUNIITrace(TraceEventType.Error, protein.UNII, null, "Unknown Amino Acid designator: {0}", residue);
                    }

                    csites = protein.SitesMatchingResidue(res1Let);

                    if (csites.Count <= 0)
                    {
                        TraceUtils.WriteUNIITrace(TraceEventType.Error, protein.UNII, null, "No matching residue sites for supplied residue: {0}", res1Let);
                    }
                }
            }
            else
            {
                csites = ProteinExtensions.fromJsonSites(jsites)
                            .Map(t => ProteinExtensions.toSiteTuple(t, protein))
                            .ToList();
            }







            String extent = "COMPLETE";
            SRSReadingUtils.readJsonElement(jStrModGroup, "extent", ex => true, v => extent = v, protein.UNII);

            // Try to read fragment
            JToken jel = jStrModGroup.SelectToken("molecularFragment");
            if (jel != null)
            {
                StructuralModification m = new StructuralModification(state.RootObject);



                g.Modification = protein.ReadStructuralModificationJson(m, jStrModGroup, state);
                g.Modification.Extent = extent;

                if (g.ModificationType == "AMINO ACID REMOVAL")
                    g.Modification.Fragment.isDeletion = true;

                //YP SRS-413 make sure that if extent is "COMPLETE" then the amount is 1/1 mol even if it's a deletion. Adding extent check to the conditional
                //if (g.ModificationType == "AMINO ACID REMOVAL" && g.Modification.Amount.isDefaultNumerator)
                if (g.ModificationType == "AMINO ACID REMOVAL" && g.Modification.Amount.isDefaultNumerator && extent != "COMPLETE")
                {
                    g.Modification.Amount.AmountType = AmountType.UncertainZero;
                }

                g.Amount = g.Modification.Amount;


                /*
                 * if (g.Modification.isDeletion)
                {
                    g.Modification.Fragment = null;
                }
                */

                if (g.Modification == null)
                    throw new SrsException("fragment", String.Format("Cannot read fragment: {0}", jStrModGroup));

                if (g.Modification.Fragment == null)
                    throw new SrsException("fragment", String.Format("Cannot resolve fragment: {0}", jStrModGroup));
                if (g.Modification.Fragment.IsMolecule && g.Modification.ModificationType != "MOIETY")
                    throw new SrsException("fragment", String.Format("Cannot define fragment connection points: {0}", jStrModGroup));
                if (g.Modification.Fragment.IsLinker && !state.CachedFragment)
                    // Normally a fragment can only be defined with one pair of connectors, but in OTHER_LINKAGE sitiation can be more complicated by repeatedly adding one pair per STRUCTURE_MODIFICATION
                    throw new SrsException("fragment", String.Format("Fragment is defined with multiple connection points: {0}", jStrModGroup));


                SRSReadingUtils.readJsonElement(jStrModGroup, "extentAmount.type", ValidatedValues.AmountTypes.Keys.Contains, v => g.Modification.Amount.SrsAmountType = v, protein.UNII);

                SRSReadingUtils.readJsonElement(jStrModGroup, "extentAmount.units", ValidatedValues.ExtentAmountUnits.Keys.Contains, v => g.Modification.Amount.ExtentAmountUnits = v, protein.UNII);

            }

            g.ResidueSites = csites
                             .Map(s => {
                                 if (g.Modification == null) throw new SrsException("modification", String.Format("There is no specific modification, even though there are specified sites for the modification : {0}", jStrModGroup));
                                 Fragment f = g.Modification.Fragment;
                                 if (f == null) throw new SrsException("connectors", String.Format("There is no fragment associated with modification, even though there are sites specified: {0}", jStrModGroup));
                                 if (f.Connectors == null) throw new SrsException("connectors", String.Format("There is no connectors associated with fragment for modification, even though there are sites specified: {0}", jStrModGroup));
                                 if (f.Connectors.Count == 0) throw new SrsException("connectors", String.Format("There is no connectors associated with fragment for modification, even though there are sites specified: {0}", jStrModGroup));
                                 return new ProteinSite(state.RootObject, g.ModificationType == "AMINO ACID REMOVAL" ? "MONOMER DELETION SITE" : "AMINO ACID SUBSTITUTION POINT") { Subunit = s.Item1, Position = s.Item2, ConnectorRef = f.Connectors.First() };
                             })
                             .ToList();


            /*
            if (g.Modification.Amount.SrsAmountType == "MOLE PERCENT")
            {
                //g.Modification.Amount.DivideBy100();
                //ApplyAmountRules(g);
            }
            else
            {
                SiteAdjustModificationAmount(g);
            }
            */

            g.Modification.Amount.isExtentComplete = extent == "COMPLETE";
            ApplyAmountRules(g);
            //I know this has just been done above, but need to make sure that after adjustment amounts are in sync
            g.Amount = g.Modification.Amount;


            bool isComplete = false;

            if (extent == "COMPLETE")
            {
                isComplete = true;
            }
            else if (extent == "PARTIAL")
            {
                g.Modification.Amount.isExtentPartial = true;
                isComplete = false;
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

            if (g.ResidueSites.Count == 0)
            {

            }


            //Calculate probabilities
            if (!isComplete)
            {

                if (g.ResidueSites.Count == 0)
                {
                    TraceUtils.WriteUNIITrace(TraceEventType.Error, protein.UNII, null, "Structural Modification has non-complete amount and specifies no sites");
                }

                //YP commenting this out per SRS-372 denominator is usually 1 mol as the amounts are usually per one mol
                //the adjustment for the number of sites is performed on the numerator portion and is done by the SiteAdjustModificationAmount routine
                //g.Modification.Amount.Denominator = g.ResidueSites.Count;

                g.Modification.Amount.SrsAmountType = "PROBABILITY";

                if (g.Modification.Amount.Numerator > g.Modification.Amount.Denominator)
                {
                    TraceUtils.WriteUNIITrace(TraceEventType.Error, protein.UNII, null, "Structural Modification amount numerator {0} greater than the denominator {1}", g.Modification.Amount.Numerator, g.Modification.Amount.Denominator);
                }
                if (extent == "PARTIAL")
                {

                    if (g.ModificationType != "MOIETY")
                    {
                        //only do this if extent amount is missing from JSON
                        if (jStrModGroup.SelectToken("extentAmount") == null)
                        {
                            g.Modification.Amount.AmountType = AmountType.UncertainZero;
                        }
                    }
                    //Per SRS-361
                    //g.Modification.Amount.Denominator
                    /*YP moved this logic to Amount.SPL
                     * if (g.Modification.Amount.isDefaultDenominator)
                    {
                        g.Modification.Amount.AmountType = AmountType.UncertainZero;
                        g.Modification.Amount.Low = 0;
                        g.Modification.Amount.High = 1;
                        g.Modification.Amount.Denominator = 1;
                    }
                    */

                }

            }



            SRSReadingUtils.readJsonElement(jStrModGroup, "residueModified", aa => AminoAcids.IsValidAminoAcidName(aa), v => g.Residue = v, protein.UNII);

            if (!String.IsNullOrEmpty(g.Residue) && g.ResidueSites.Any(s => !String.Equals(AminoAcids.GetNameByLetter(s.Letter), g.Residue, StringComparison.InvariantCultureIgnoreCase)))
                TraceUtils.WriteUNIITrace(TraceEventType.Error, protein.UNII, null, "Residue {0} does not match all positions", g.Residue);

            if (String.IsNullOrEmpty(g.Residue) && g.ResidueSites.Count == 1)
            {
                ProteinSite site = g.ResidueSites.First();
                g.Residue = AminoAcids.GetNameByLetter(site.Letter);
                TraceUtils.WriteUNIITrace(TraceEventType.Information, protein.UNII, null, "Residue restored from position: {0} => {1} => {2}", site, site.Letter, g.Residue);
            }

            return g;
        }

        public static void ReadGlycosylation(this Protein protein, XElement xGlyc, PolymerBaseReadingState state)
        {
            protein.Glycosylations = new List<Glycosylation>();

            if (xGlyc != null && !String.IsNullOrWhiteSpace(xGlyc.Value))
            {
                string type = null;
                SRSReadingUtils.readElement(xGlyc, "GLYCOSYLATION_TYPE", null, v => type = v, protein.UNII);
                if (String.IsNullOrEmpty(type))
                    type = "MAMMALIAN";

                addGlycosylations(protein, type, xGlyc, "N_GLYCOSYLATION", "N", state);
                addGlycosylations(protein, type, xGlyc, "O_GLYCOSYLATION", "O", state);
                addGlycosylations(protein, type, xGlyc, "C_GLYCOSYLATION", "C", state);
                if (type == null)
                    throw new SrsException("mandatory_field_missing", string.Format("Unspecified glycosylation type: {0}", xGlyc));

                if (!ValidatedValues.GlycosylationTypes.Keys.Contains(type))
                    throw new SrsException("unknown_value", string.Format("Unknown glycosylation type: {0}", type));
            }
        }

        private static void addGlycosylations(Protein protein, string type, XElement xGlyc, string xPath, string element, PolymerBaseReadingState state)
        {
            XElement xel = xGlyc.XPathSelectElement(xPath);
            if (xel != null && !String.IsNullOrEmpty(xel.Value))
            {
                foreach (var t in Helpers.SequencePositions(xel.Value))
                {
                    if (t.Item1 >= protein.Subunits.Count)
                        throw new SrsException("subunit_ref", string.Format("Reference to non-existing subunit: {0}", xel.Value));
                    if (t.Item2 >= protein.Subunits[t.Item1].Sequence.Length)
                        throw new SrsException("subunit_ref", string.Format("Reference to non-existing position: {0}", xel.Value));

                    Glycosylation g = new Glycosylation(state.RootObject, type);
                    g.Attachment = new ProteinAttachment(state.RootObject, "STRUCTURAL ATTACHMENT POINT", element, protein.Subunits[t.Item1], t.Item2);
                    protein.Glycosylations.Add(g);
                }
            }
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

        public static Tuple<Subunit, int> toSiteTuple(Tuple<int, int> t, Protein protein)
        {
            if (t.Item1 >= protein.Subunits.Count)
                throw new SrsException("subunit_ref", string.Format("Reference to non-existing subunit: {0}", t.ToString()));
            if (t.Item2 >= protein.Subunits[t.Item1].Sequence.Length)
                throw new SrsException("subunit_ref", string.Format("Reference to non-existing position: {0}", t.ToString()));
            return new Tuple<Subunit, int>(protein.Subunits[t.Item1], t.Item2);
        }

        public static void addGlycosylationsDirect(Protein protein, string type, List<Tuple<int, int>> slist, string gtype, PolymerBaseReadingState state)
        {
            foreach (var t in slist)
            {
                Tuple<Subunit, int> site = toSiteTuple(t, protein);
                Glycosylation g = new Glycosylation(state.RootObject, type);
                g.Attachment = new ProteinAttachment(state.RootObject, "STRUCTURAL ATTACHMENT POINT", gtype, site.Item1, site.Item2);
                protein.Glycosylations.Add(g);
            }
        }

        public static Subunit ReadSubunit(this Protein protein, XElement xSubunit, PolymerBaseReadingState state)
        {
            Subunit su = new Subunit(state.RootObject);
            if (su.Id == null)
                TraceUtils.WriteUNIITrace(TraceEventType.Error, protein.UNII, null, "Element <SUBUNIT> is not found or not interpreted: {0}", xSubunit);

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
            Sequence _seq = new Sequence(su.RootObject, seq);
            Sequence sequence = protein.Sequences.Find(s => s.UID == _seq.UID);
            if (sequence == null)
            {
                sequence = _seq;
                protein.Sequences.Add(sequence);
            }
            su.Sequence = sequence;

            if (len != su.Sequence.Length)
                TraceUtils.WriteUNIITrace(TraceEventType.Warning, protein.UNII, null, "The declared length of the sequence does not match");
            return su;
        }

        public static Subunit MakeSubunit(this Protein protein, string seq, PolymerBaseReadingState state)
        {
            Subunit su = new Subunit(state.RootObject);

            Sequence _seq = new Sequence(su.RootObject, seq);
            Sequence sequence = protein.Sequences.Find(s => s.UID == _seq.UID);
            if (sequence == null)
            {
                sequence = _seq;
                protein.Sequences.Add(sequence);
            }
            su.Sequence = sequence;
            return su;
        }


        /// <summary>
        /// Just turns all wedge bonds to dashes, and all dashes to wedges.
        /// This should really be possible with indogo's invertStereo method, 
        /// but that method appears to do nothing.
        /// </summary>
        /// <param name="mol"></param>
        /// <returns></returns>
        public static String invertMolfileStereo(String mol)
        {
            String s = Regex.Replace(mol, @"([\n\r][ 0-9]{3}[ 0-9]{3}[ ][ ]1[ ])[ ]6", "$1 !!!!!!");
            s = Regex.Replace(s, @"([\n\r][ 0-9]{3}[ 0-9]{3}[ ][ ]1[ ])[ ]1", "$1 6");
            s = Regex.Replace(s, @"!!!!!!", "1");
            return s;
        }

        public static bool OtherLinksExist(this Protein protein)
        {
            bool returnvalue = false;
            List<string> other_links = new List<string> { "cys-cys", "cysD-cysD", "cysD-cysL", "cysD-cysL" };

            foreach (Link link in protein.Links)
            {
                //if (link.LinkType=="Other") { return true;  }
                if (!other_links.Contains(link.LinkType)) { return true; }

            }

            return returnvalue;
        }

        public static Protein readFromJson(this Protein protein, JObject o, SplDocument splDoc)
        {
            return readFromJson(protein, o, splDoc, true, true);
        }

        public static Protein readFromJson(this Protein protein, JObject o, SplDocument splDoc, Boolean canonicalize)
        {
            return readFromJson(protein, o, splDoc, canonicalize, true);
        }

        //Parse a protein from a given JSON object
        public static Protein readFromJson(this Protein protein, JObject o, SplDocument splDoc, Boolean canonicalize, Boolean validateLinks)
        {


            Optional<JToken>.ofNullable(o.SelectToken("approvalID"))
                            .map(u => u.ToString())
                            .ifPresent(unii => {
                                protein.UNII = unii;
                            });

            List<JToken> acme = o.SelectTokens("$..protein..sequence").ToList();
            JToken jseqType = o.SelectToken("$..protein..sequenceType");

            string seqType = null;

            if (jseqType != null)
            {
                seqType = jseqType.ToString();
            }

            PolymerBaseReadingState state = new PolymerBaseReadingState { RootObject = splDoc };

            SRSReadingUtils.readJsonElement(o, "$..protein..sequenceType", st => ValidatedValues.SequenceTypes.Keys.Contains(st), st => protein.SeqType = st, protein.UNII);



            //Reference connector sites
            List<Tuple<Predicate<Fragment>, List<Tuple<int, int>>>> fragRefCon = null;
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
                                SRSReadingUtils.readJsonElement(n, "$..note", (n2) => true, v => note = v, protein.UNII);
                                return note.ToUpper().Trim();
                            })
                            .Filter(n => n.StartsWith("FRAGMENT CONNECTORS<"))
                            .Select(n => {
                                String[] splitted = n.Split(':');
                                String ident = splitted[0].Split('<')[1].Split('>')[0];
                                Predicate<Fragment> pred = (frag) => {
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
                                    TraceUtils.WriteUNIITrace(TraceEventType.Warning, protein.UNII, null, "Odd connection points - only first 0, 2, 4, etc will be used");

                                List<Tuple<int, int>> cList = new List<Tuple<int, int>>();

                                for (int i = 0; i < conns.Count; i += 2)
                                {
                                    cList.Add(Tuple.Create(conns[i], conns[i + 1]));
                                }
                                return Tuple.Create(pred, cList);
                            })
                            .ToList();

                    });
            protein.setFragmentConnectorsData(fragRefCon);


            //Agent modifications


            Optional<JToken>.ofNullable(o.SelectToken("$..agentModifications"))
                    .ifPresent(mods => {
                        mods.AsJEnumerable()
                            .Map(am => {
                                AgentModification m = new AgentModification(state.RootObject);

                                //Commented and added for Ticket 271
                                AgentModification.SeqCount = 1;
                                //SRSReadingUtils.readJsonElement(am, "$..agentModificationRole", ValidatedValues.AgentModificationRoles.Keys.Contains, v => m.Role = v, protein.UNII);
                                //SRSReadingUtils.readJsonElement(am, "$..agentModificationType", ValidatedValues.AgentModificationTypes.Keys.Contains, v => m.ModificationType = v, protein.UNII);
                                SRSReadingUtils.readJsonElement(am, "$..agentModificationRole", null, v => m.Role = v, protein.UNII);
                                SRSReadingUtils.readJsonElement(am, "$..agentModificationType", null, v => m.ModificationType = v, protein.UNII);

                                //added on 9/20/18 adding process
                                SRSReadingUtils.readJsonElement(am, "$..agentModificationProcess", null, v => m.Process = v, protein.UNII);

                                SRSReadingUtils.readJsonElement(am, "$..refPname", null, v => m.Agent = v, protein.UNII);
                                SRSReadingUtils.readJsonElement(am, "$..approvalID", null, v => m.AgentId = v, protein.UNII);
                                //Added for Ticket 271
                                //String AgentSubLinkID;
                                SRSReadingUtils.readJsonElement(am, "$..agentSubstance.linkingID", null, v => m.AgentSubLinkID = v, protein.UNII);

                                //String RelationTypeLinkID;
                                SRSReadingUtils.readJsonElement(o, "$..relationships[?(@.type == 'STARTING MATERIAL->INGREDIENT')].relatedSubstance.linkingID", null, v => m.RelationTypeLinkID = v, protein.UNII);

                                SRSReadingUtils.readJsonElement(o, "$..relationships[?(@.type == 'STARTING MATERIAL->INGREDIENT')].relatedSubstance.approvalID", null, v => m.RelationTypeApprovalID = v, protein.UNII);

                                SubstanceIndexing ind = new SubstanceIndexing(ConfigurationManager.AppSettings["SubstanceIndexingDat"]);
                                // UNII|Primary Name|Hash Code|Link|SetId|Version Number|Load Time|Citation
                                // 5I9835JO3M||2841ba24-3dc4-3d7d-0743-e9c87e86e1d5|1c7a69fd-d641-48cb-8ad4-ba84025f2906|141b28a9-4ac2-4c1a-986c-7fc160bb5446|1|20140505122508|
                                if (ind != null)
                                {
                                    try
                                    {
                                        //What exactly is this meant to do? It seems strange to look this up for the PROTEIN UNII,
                                        //Is this a mistake?
                                        var ent = ind.GetExisting(m.AgentId);
                                        //var ent = ind.GetExisting("5I9835JO3M");
                                        if (ent != null)
                                            m.AgentHashCode = ent.Hash.ToString();
                                    }
                                    catch (Exception e2)
                                    {
                                        //Should this actually throw an exception further?
                                        throw new SrsException("mandatory_field_missing", "Cannot find the hash in the index for:" + protein.UNII);
                                    }
                                }


                                if (String.IsNullOrEmpty(m.RelationTypeLinkID) || String.IsNullOrEmpty(m.RelationTypeApprovalID))
                                    throw new SrsException("mandatory_field_missing", "Type in Relationships is not STARTING MATERIAL INGREDIENT/no approvalID available for Agent Modification");

                                if (String.IsNullOrEmpty(m.AgentId))
                                    throw new SrsException("mandatory_field_missing", "Approval Id doesnot exist for Agent Modification");

                                //modified on 09/20/18 adding process
                                AgentTerms at = new AgentTerms(m.Process + "###" + m.Role + "###" + m.ModificationType);

                                if (at.Id == null)//modified on 09/20/18 adding process
                                    throw new SrsException("mandatory_field_missing", "Role/Type/Process don't exist for Agent Modification");

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

                                //Added for Ticket 355
                                AgentTerms defaultCodeSystem = new AgentTerms("DefaultCodeSystem");
                                m.CodeSystem = defaultCodeSystem.CodeSystem;


                                ////End of 271////

                                m.Amount = PolymerBaseExtensions.ReadAmountJson(protein, am.SelectToken("amount"), state);
                                protein.Modifications.Add(m);
                                return m;
                            });
                    });

            //IEnumerable<JToken> jsubs = o.SelectTokens("$..protein..subunits..sequence");
            IEnumerable<JToken> jsubs = o.SelectTokens("$..protein..subunits");

            foreach (JToken subunit_token in jsubs.Children())
            {
                JToken x = subunit_token.SelectToken("sequence");
                JToken seq = x;
                Subunit su = protein.MakeSubunit(seq.ToString(), state);

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
                protein.Subunits.Add(su);

                //begin YB
                if (su.Sequence.ToString().Any(c => String.IsNullOrEmpty(AminoAcids.GetNameByLetter(c))))
                    TraceUtils.ReportError("seq_ref", protein.UNII, "Unknown letter(s) used in sequence");
                //end YB
            }

            /*
            foreach (JToken x in jsubs)
            {
                JToken seq = x;
                Subunit su = protein.MakeSubunit(seq.ToString(), state);
                protein.Subunits.Add(su);

                //begin YB
                if (su.Sequence.ToString().Any(c => String.IsNullOrEmpty(AminoAcids.GetNameByLetter(c))))
                    TraceUtils.ReportError("seq_ref", protein.UNII, "Unknown letter(s) used in sequence");
                //end YB
            }
            */
            // Disulfide links
            o.SelectTokens("$..disulfideLinks..sites")
                .ForEachWithIndex((x, h) => {
                    List<Tuple<int, int>> tlist = new List<Tuple<int, int>>();

                    x.ForEachWithIndex((l, i) => {
                        tlist.Add(ProteinExtensions.fromJsonSite(l));
                    });
                    String type = "cys-cys";
                    bool switchConnectors = false;
                    bool switchSites = false;

                    try
                    {
                        String linked = tlist.Select(site => protein.residueAt(site))
                                           .JoinToString("");


                        if (linked.Equals("cc"))
                        {
                            type = "cysD-cysD";
                        }
                        else if (linked.Equals("cC"))
                        {
                            type = "cysD-cysL";
                            switchConnectors = true;
                            switchSites = false;
                        }
                        else if (linked.Equals("Cc"))
                        {
                            type = "cysD-cysL";
                            switchSites = true;
                        }
                    }
                    catch (Exception e)
                    {
                        //likely non-valid sites specified, don't fail here, let it fail
                        //in the next check
                    }

                    //Link li = PolymerBaseExtensions.FragmentFactory.CreateLink(protein, tlist, "48TCX9A1VT".ToLower(), splDoc);



                    Link li = PolymerBaseExtensions.FragmentFactory.CreateLink(protein, tlist, type, splDoc);
                    if (switchConnectors)
                    {
                        var t = li.Sites[1];
                        li.Sites[1] = li.Sites[0];
                        li.Sites[0] = t;
                    }
                    if (switchSites)
                    {
                        var t = li.Sites[1].ConnectorRef;
                        li.Sites[1].ConnectorRef = li.Sites[0].ConnectorRef;
                        li.Sites[0].ConnectorRef = t;
                    }
                    protein.Links
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

                Link ll = PolymerBaseExtensions.FragmentFactory.CreateLink(protein, tlist, ltype, splDoc);

                protein.Links.Add(ll);
            });

            JToken jglyType = o.SelectToken("$..protein..glycosylationType");
            String glyType = "MAMMALIAN";
            if (jglyType != null)
            {
                glyType = jglyType.ToString();
            }
            if (!ValidatedValues.GlycosylationTypes.Keys.Contains(glyType))
                throw new SrsException("unknown_value", string.Format("Unknown glycosylation type: {0}", glyType));
            glyType = ValidatedValues.GlycosylationTypes.Filter(kv => kv.Key.Equals(glyType))
                                              .Select(kv => kv.Value)
                                              .First();

            JToken jGlyc = o.SelectToken("$..protein.glycosylation");

            if (jGlyc != null)
            {
                List<Tuple<int, int>> olist = ProteinExtensions.fromJsonSites(jGlyc.SelectToken("$..OGlycosylationSites"));
                List<Tuple<int, int>> nlist = ProteinExtensions.fromJsonSites(jGlyc.SelectToken("$..NGlycosylationSites"));
                List<Tuple<int, int>> clist = ProteinExtensions.fromJsonSites(jGlyc.SelectToken("$..CGlycosylationSites"));

                ProteinExtensions.addGlycosylationsDirect(protein, glyType, olist, "O", state);
                ProteinExtensions.addGlycosylationsDirect(protein, glyType, nlist, "N", state);
                ProteinExtensions.addGlycosylationsDirect(protein, glyType, clist, "C", state);
            }

            Func<string, Boolean> roleValidator = ValidatedValues.StructureModificationTypes.Keys.Contains;

            //Reference connector sites
            List<List<Tuple<int, int>>> refCon = null;
            Optional<JToken>.ofNullable(o.SelectToken("$..notes"))
                    .ifPresent(notes => {
                        refCon = notes.AsJEnumerable()
                            .Map(n => {
                                String note = null;
                                SRSReadingUtils.readJsonElement(n, "$..note", (n2) => true, v => note = v, protein.UNII);
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
            protein.setConnectorsData(refCon);

            //YP SRS-358
            if (refCon.Count == 0 && OtherLinksExist(protein))
            {
                throw new SrsException("mandatory_field_missing", "CONNECTORS not explicitly specified");
            }

            //TODO: really fix this
            // Physical modifications
            // Note that the only thing that they have is the ROLE currently
            // Parameters are NOT currently used in SRSUtil
            o.SelectTokens("$..physicalModificationRole")
                            .Map(r => r.ToString())
                            .ForEachWithIndex((r, i) => {
                                PhysicalModification m = new PhysicalModification(state.RootObject);
                                //I don't think this makes sense ... 
                                SRSReadingUtils.readSingleElement(r, "PHYSICAL_MODIFICATION_ROLE", roleValidator, v => m.Role = v, protein.UNII);
                                protein.Modifications.Add(m);
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
                    mw.Amount = PolymerBaseExtensions.ReadAmountJson(protein, jmw.SelectToken("value"), state);


                    string mwcolon = Optional<JToken>.ofNullable(jmw.SelectToken("value.type"))
                                                    .map(v => v.ToString())
                                                    .orElse(null);




                    string mwparan = null;

                                //read from property value
                                if (mwcolon == null || !mwcolon.Contains("("))
                    {
                        string[] split = mwname.Split(new char[] { ':' }, 2);
                        if (split.Length > 1)
                        {
                            string[] split2 = split[1].Split(new char[] { '(' }, 2);
                            string mwcolon1 = split2[0].Trim();
                            if (split2.Length > 1)
                            {
                                mwparan = split2[1].Trim().Replace(")", "");
                                mwcolon = split2[0].Trim();
                            }
                        }
                        if (mwparan == null)
                        {
                            string[] split2 = mwname.Split(new char[] { '(' }, 2);
                            if (split2.Length > 1)
                            {
                                mwparan = split2[1].Replace(")", "").Trim();
                            }
                        }
                    }

                                //mw.WeightType = mwcolon;
                                //mw.WeightMethod = mwparan;
                                SRSReadingUtils.readSingleElement(mwcolon, "MOLECULAR_WEIGHT_TYPE", ValidatedValues.MWTypes.Keys.Contains, v => mw.WeightType = v, protein.UNII);
                    SRSReadingUtils.readSingleElement(mwparan, "MOLECULAR_WEIGHT_METHOD", ValidatedValues.MWMethods.Keys.Contains, v => mw.WeightMethod = v, protein.UNII);

                    if (mw.WeightMethod != null
                       || mw.WeightType != null
                       || mw.Amount.Low != null
                       || mw.Amount.High != null
                       || mw.Amount.Numerator != null
                       || mw.Amount.NonNumericValue != null)
                    {


                        if (mw.Amount.Unit == null || mw.Amount.Unit.Equals("mol"))
                        {
                            mw.Amount.Unit = "DA";
                        }
                                    //YP SRS-372 commenting this as the denominator unit should always be "mol" per Yulia
                                    //if (mw.Amount.DenominatorUnit == null || mw.Amount.DenominatorUnit.Equals("mol")) {
                                    //    mw.Amount.DenominatorUnit = "1";
                                    //}
                                    protein.MolecularWeight = mw;
                    }
                });
            Optional<JToken>.ofNullable(o.SelectToken("$..structuralModifications"))
                    .ifPresent(mods => {
                        mods.AsJEnumerable()
                            .ForEachWithIndex((sm, i) => {
                                var g = protein.ReadStructuralModificationJson(sm, state);
                                protein.ProcessStructuralModificationGroup(g, state);
                            });
                    });

            // Check that all links have associated fragments and all connection points are defined for all fragments
            if (validateLinks)
            {
                foreach (var link in protein.Links.Where(l => !l.isDisulfideType()))
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

                    protein.reorderProteinSites(link.Sites)
                          .ForEachWithIndex((s, i) => {
                              s.ConnectorRef = link.Linker.Connectors[i];
                          });
                    //YP
                    //bool needsReorder = true;

                    bool needsReorder = false;

                    //YP adding this per SRS-337 in order to order link sites based on the order specified in notes CONNECTORS rather than order in otherLinks
                    link.Sites = protein.reorderProteinSites(link.Sites);

                    link.Sites.ForEachWithIndex((s, i) => {
                        if (!s.ConnectorRef.CanUseResidue(s.Letter + ""))
                        {
                            needsReorder = true;
                            TraceUtils.WriteUNIITrace(TraceEventType.Warning, protein.UNII, null, "Connector {0} can not use residue {1}. Will reshuffle", i, s.Letter);
                        }
                    });


                    if (needsReorder)
                    {

                        IList<String> rlist = link.Sites.Select(s => s.Letter + "").ToList();

                        List<Fragment.Connector> oldCons = link.Linker.Connectors.Select(c => c).ToList();
                        List<Fragment.Connector> newCons = new List<Fragment.Connector>();

                        for (int i = 0; i < rlist.Count; i++)
                        {
                            Fragment.Connector fcon = oldCons[0];
                            IList<Fragment.Connector> fcons = oldCons.Filter(o1 => o1.CanUseResidue(rlist[i])).ToList();
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
                    protein.RegisterFragment(link.Linker);
                }
            }

            if (canonicalize)
            {
                protein.Canonicalize();
            }


            CrossCheckAmounts(protein);

            if (protein.isSingleMoietySubstance)
            {
                protein.Subunits[0].isTheOnlyMoiety = true;
                protein.Subunits[0].parent_unii = protein.UNII;
            }

            PopulateDerivationProcessModel(protein);

            return protein;

        }

        public static void PopulateDerivationProcessModel(Protein protein)
        {
            Protein.DerivationProcess derivation_process = new Protein.DerivationProcess();
            derivation_process.DisplayName = "Modification";
            derivation_process.code = "C25572";
            derivation_process.codeSystem = "2.16.840.1.113883.3.26.1.1";

            Boolean AgentFlag = true, AgentExists = false, AgentBipass = false;

            //Added to avoid other than Agent Modification
            foreach (var s in protein.Modifications)
            {
                if (s is AgentModification)
                {
                    AgentBipass = true;
                    break;
                }
                continue;

            }
            ////

            foreach (var s in protein.Modifications)
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

                        derivation_process.interactors.Add(
                            new Protein.DerivationProcess.Interactor
                            {
                                typeCode = "CSM",
                                unii = ((AgentModification)s).RelationTypeApprovalID,
                                //YP SRS-390
                                //do we need "asEquivalentSubstance" code (AgentHashCode) here?
                                //it appears "name" isn't present in interactor for protein DP
                                //so let's use AgentHashCode instead
                                AgentHashCode = ((AgentModification)s).AgentHashCode,
                                codeSystem = "2.16.840.1.113883.4.9"
                            }
                        );

                    }
                    AgentFlag = false;

                    int seq_count = 1;
                    //why is this a loop, it looks like there's only one xComponent that's returned by SPL2
                    foreach (var x in ((AgentModification)s).SPL2)
                    {

                        Protein.DerivationProcess component_dp = new Protein.DerivationProcess();
                        component_dp.DisplayName = ((AgentModification)s).AgentDisplayName;
                        component_dp.code = ((AgentModification)s).AgentCode;
                        component_dp.codeSystem = ((AgentModification)s).CodeSystem;
                        component_dp.interactors.Add(
                            new Protein.DerivationProcess.Interactor
                            {
                                typeCode = "CSM",
                                unii = ((AgentModification)s).AgentId,
                                AgentHashCode = ((AgentModification)s).AgentHashCode,
                                codeSystem = "2.16.840.1.113883.4.9"
                            });
                        derivation_process.components.Add(new Tuple<Protein.DerivationProcess, int>(component_dp, seq_count));
                        ++seq_count;
                    }

                }

            }

            if (AgentExists == true)
                protein.derivation_process = derivation_process;

        }

        public class PAminoAcid
        {
            public string sym;
            public Fragment Fragment;

            private int addCount = 0;
            private int offset = 0;
            private List<int> removed = new List<int>();

            public Boolean isMoiety = false;
            private bool _isModified = false;

            private string _mol;

            Dictionary<string, Fragment.Connector> cons = new Dictionary<string, Fragment.Connector>();





            public static PAminoAcid from(String s, PolymerBaseReadingState state)
            {
                PAminoAcid pac = new PAminoAcid { sym = s };

                Dictionary<string, string> aadict = @"A	OF5P57N2ZX
R	94ZLA3W45F
N	5Z33R5TKO7
D	30KYC7MIAI
C	K848JZ4886
E	3KX376GY7L
Q	0RH81L854J
G	TE7660XO1C
H	4QD397987E
I	04Y7590D77
L	GMW67QNF9C
K	K3Z4F929H6
M	AE28F7PNPL
F	47E5O17Y3R
P	9DLQ4CIU6V
S	452VLY9402
T	2ZD004190S
W	8DUH1N11BX
Y	42HK56048U
V	HG18B9YRS7".Replace("\r", "")
               .Split('\n')
               .ToDictionary(l => l.Split('\t')[0], l => l.Split('\t')[1]);
                string aaunii = aadict.TryGetValue(s.ToUpper(), out aaunii) ? aaunii : null;

                if (aaunii != null)
                {
                    pac.Fragment = FragmentResolve(aaunii, state);

                    if (s.Equals(s.ToLower()))
                    {
                        if (pac.Fragment != null)
                        {
                            SDFUtil.IMolecule m = pac.Fragment.Molecule;
                            String mm = invertMolfileStereo(m.Mol);
                            pac._mol = mm;
                        }
                    }
                }


                return pac;
            }

            public bool isModified()
            {
                return _isModified;
            }

            public PAminoAcid setLinkerConnectionForSite(Tuple<int, int> site, Fragment.Connector fc)
            {
                cons.Add(site.Item1 + "_" + site.Item2, fc);
                return this;
            }

            public PAminoAcid setModified(bool m)
            {
                this._isModified = m;
                return this;
            }

            private string getBestMolfile()
            {
                if (_mol != null && !this.isModified()) return _mol;
                return Fragment.Molecule.Mol;
            }

            public string getMolfile()
            {
                if (!this.alreadyAdded())
                {
                    return getBestMolfile();
                }
                else
                {
                    Indigo ind = new Indigo();
                    IndigoObject io = ind.loadMolecule(getBestMolfile());

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

            public Tuple<int, int> getPosition(int poffset, Tuple<int, int> site)
            {
                if (this.alreadyAdded())
                {
                    poffset = offset;
                }
                Fragment.Connector fc = cons.TryGetValue(site.Item1 + "_" + site.Item2, out fc) ? fc : null;
                Tuple<int, int> tup1;
                if (fc != null)
                {
                    tup1 = fc.Snip;
                }
                else
                {
                    tup1 = Fragment.Connectors[this.addCount].Snip;
                }

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


        /**
         * Creates an IMolecule object from the provided protein, by building it up AA by AA.
         * 
         * TODO:
         *  1. Invert stereo on lower case letters     [done]
         *  2. Fail on agent / physical mods           [done]
         *  3. Fail on undefined structural mod extent [???]
         *  4. Fail on glycosylation                   [done]
         *  5. Fail on very large molecules            [done]
         *  6. Deal with complex stereochemistry       [needs guidance]
         */
        //YP
        //ConvertOptions opt parameter is a bit hacky but required to pass along the command line arguments that govern
        //the converstion/output format such as generation of 2D coordinates
        public static SdfRecord asChemical(this Protein protein, PolymerBaseReadingState state, ConvertOptions opt)
        {
            int MAX_ATOMS = 999;
            int MAX_BONDS = 999;

            //throw error if layout generation takes longer than this (in milliseconds)
            int layout_timeout = 1200000;

            //Fail if glycosylated
            if (protein.Glycosylations.Count > 0) return null;


            List<List<PAminoAcid>> subunits = new List<List<PAminoAcid>>();
            /*
                1. Create molecules for each sequence, keeping track of any non-standard amino acid (using L/D amino acids based on capitalization), 
                   and putting a placeholder for the non-standard AAs
                2. Look at disulfide bridges, adding links between each cysteine marked
                3. Look at structural modifications, and for all that are marked as "complete" and have defined sites, NOT associated with the "other links", find the appropriate substitution from the registry (or embedded molfile), and replace that residue with the replacement.
                4. Look at other links. Find the associated structural modifications, and use the connectors to replace the 2 amino acids at once. The first site specified will have the primary connectors, and the second will have the next alternative connectors. Substitute the whole molecule at both sites.
            */

            //Decompse into sequence
            protein.Subunits.Select(su => su.Sequence)
                            .ForAll(sq => {
                                List<PAminoAcid> aalist = new List<PAminoAcid>();
                                sq.ToString().ForEachWithIndex((aa, i) => {
                                    PAminoAcid paa = PAminoAcid.from(aa + "", state);
                                    aalist.Add(paa);
                                });
                                subunits.Add(aalist);
                            });
            //First, you need to get the sequence

            protein.Links.ForEachWithIndex((l, i) => {
                PAminoAcid paa1 = null;

                l.Sites.ForEach(s => {
                    if (paa1 == null)
                    {
                        PAminoAcid paa = subunits[s.Subunit.Ordinal - 1][s.Position];

                        paa.Fragment = l.Linker;
                        paa.setModified(true);

                        //protein.Subunits[s.Subunit.Ordinal - 1].Sequence.ToString();
                        //System.Console.WriteLine(paa.sym + "\t" + paa.Fragment.Molecule.SMILES);
                        paa1 = paa;
                    }
                    else
                    {
                        subunits[s.Subunit.Ordinal - 1][s.Position] = paa1;
                    }
                    paa1.setLinkerConnectionForSite(s.ToTuple(), s.ConnectorRef);
                });
            });

            int nonStrModCount = protein.Modifications.Where(m => !(m is StructuralModificationGroup))
                                                    .Count();

            if (nonStrModCount != 0)
            {
                throw new Exception("Cannot convert protein to molecule. There are " + nonStrModCount + " non structural modifications.");
            }

            protein.Modifications.Where(m => m is StructuralModificationGroup)
                                 .Select(m => (StructuralModificationGroup)m)
                                 .ForEachWithIndex((m, i) => {

                                     //Need to do something with amounts here
                                     //m.Amount.
                                     StructuralModification sm = m.Modification;

                                     if (sm.ModificationType == "MOIETY")
                                     {
                                         PAminoAcid paa = PAminoAcid.from("X", state);
                                         paa.Fragment = sm.Fragment;
                                         paa.isMoiety = true;

                                         //YP SRS-363
                                         if (sm.Amount.AmountType == AmountType.Exact && ((m.Modification.Amount.Numerator % 1) == 0))
                                         {
                                             for (int k = 0; k < m.Modification.Amount.Numerator; k += 1)
                                                 subunits[0].Add(paa);
                                         }
                                         else
                                         {
                                             throw new Exception("MOIETY Modification is encountered with invalid amount.");
                                         }

                                         //YP SRS-407
                                         //} else if (sm.ModificationType == "AMINO ACID SUBSTITUTION" || sm.ModificationType == "AMINO ACID SUBSTITUION"
                                         //       || sm.ModificationType == null) {
                                     }
                                     else if (ValidatedValues.StructureModificationTypes.Keys.Contains(sm.ModificationType))
                                     {
                                         if (m.ResidueSites.Count <= 0)
                                         {
                                             throw new Exception("Non-moiety modifications must have at least one site to be convertable to chemicals.");
                                         }
                                         m.ResidueSites.ForEachWithIndex((s, j) => {
                                             PAminoAcid paa = subunits[s.Subunit.Ordinal - 1][s.Position];
                                             paa.Fragment = sm.Fragment;
                                             paa.setModified(true);
                                         });
                                     }
                                 });



            IndigoObject mol = getIndigo().loadMolecule("");
            try
            {

                List<PAminoAcid> stagedLinkers = new List<PAminoAcid>();

                subunits.ForEachWithIndex((su, i) => {
                    int currentHead = 0;
                    su.Reverse();
                    int max = su.Count - 1;




                    su.ForEachWithIndex((paa, j) => {
                        if (paa.Fragment == null)
                        {
                            throw new Exception("Unspecified structure for:" + paa.sym + " for UNII: " + protein.UNII);
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

                            Tuple<int, int> site = Tuple.Create(i, su.Count() - j - 1);

                            //YP moved this line to inside isMoiety check as this line throws an error for MOIETY mods
                            //Tuple<int, int> position = paa.getPosition(offset,site);

                            //System.Console.WriteLine(paa.sym);
                            //System.Console.WriteLine(position);

                            if (!paa.isMoiety)
                            {
                                Tuple<int, int> position = paa.getPosition(offset, site);
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
                                    if (j > 0 && "C".Equals(matom.symbol()))
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
                                }
                                if (mol.countAtoms() > MAX_ATOMS || mol.countBonds() > MAX_BONDS)
                                {
                                    throw new Exception("Protein is too large to be interpretted as chemical");
                                }

                                if (currentHead != 0)
                                {
                                    mol.getAtom(currentHead)
                                       .addBond(mol.getAtom(position.Item2 - 1), 1);
                                }
                                currentHead = position.Item1 - 1;
                                paa.incrementAdded(offset);

                            }

                            //YP SRS-363 move this into the if statement below to allow for addition of multiple MOIETY modification structures
                            //paa.incrementAdded(offset);


                        }
                    });
                });

                if (mol.countAtoms() <= 0) throw new Exception("No atoms found in protein");

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
                    if (mol.countAtoms() <= MAX_ATOMS && mol.countBonds() <= MAX_BONDS)
                    {
                        //string yp_smiles=mol.smiles();
                        //System.IO.File.WriteAllText(@"yp_smiles.txt", yp_smiles);
                        try
                        {

                            //YP these two lines can speed up layout calculation, but need indigo 1.3.0 beta for that
                            //_indigo.setOption("smart-layout", "true");
                            //_indigo.setOption("layout-max-iterations", 10);

                            //if (!Task.Run(() => mol.layout()).Wait(10000)) 
                            if (!Task.Run(() => mol.layout()).Wait(layout_timeout))
                            {
                                throw new TimeoutException("2D Coordinates took too long to generate.");
                            }
                            //mol.layout();

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
                            if (e.Message == "2D Coordinates took too long to generate.")
                            {

                                //System.Console.WriteLine(e.Message);
                                //System.Console.WriteLine("Protein will be skipped");

                                /* YP uncomment the below if still wish to generate molfile
                                System.Console.WriteLine("Molfile will be generated without calculated 2D coordinates");
                                SdfRecord sdfmol1 = SdfRecord.FromString(mol.molfile());
                                sdfmol1.AddField("UNII", protein.UNII);
                                sdfmol1.AddField("SUBSTANCE_ID", protein.UNII);
                                sdfmol1.AddField("STRUCTURE_ID", protein.UNII);
                                return sdfmol1;
                                */
                                throw e;
                            }
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
                sdfmol.AddField("UNII", protein.UNII);
                sdfmol.AddField("SUBSTANCE_ID", protein.UNII);
                sdfmol.AddField("STRUCTURE_ID", protein.UNII);

                return sdfmol;
            }
            catch (Exception e)
            {
                System.Console.WriteLine(e.Message);
                System.Console.WriteLine(e.StackTrace);
                if (e.Message == "2D Coordinates took too long to generate.")
                    //System.Console.WriteLine(e.Message);
                    //System.Console.WriteLine("Protein will be skipped");
                    throw e;
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

        public static Fragment FragmentResolve(string key, PolymerBaseReadingState state)
        {
            Fragment f = null;
            string term = key.ToLower();
            if (state.FragmentsCache.ContainsKey(term))
            {
                f = state.FragmentsCache[term];
                state.CachedFragment = true;
            }
            else
            {
                f = PolymerBaseExtensions.FragmentFactory.Resolve(key);
                if (f != null)
                {
                    f = f.Clone();
                    state.FragmentsCache.Add(term, f);
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

        //SRS-372
        //The idea here is to make sure that if multiple modifications/links are found at a site, the amounts should reflect that 
        //For example if two modifications exist at a site they can't be both 1mol but should be (1mol-sum of other amounts at this site)
        //Also there can't be an exact and an uncertain amounts at the same site.
        //See SRS-372 for examples and further explanation
        private static void CrossCheckAmounts(this Protein protein)
        {
            //first addressing conflict exact/uncertain amounts
            //if there is a modification with uncertain amounts then links at that site should also be set to uncertain
            foreach (Link lnk in protein.Links)
            {
                double? sum_of_mod_amounts = 0;
                double? sum_of_low_mod_amounts = 0;
                double? sum_of_high_mod_amounts = 0;
                double? sum_of_avg_mod_amounts = 0;
                double? max_high_mod_amount = 0;
                double? max_avg_mod_amount = 0;
                double? max_mod_amount = 0;
                double? min_low_mod_amount = 999999;
                double? min_mod_amount = 999999;
                bool need_to_adjust_link_amount = false;
                //Amount UncertainAmount = null;
                AmountType uncertain_amount_type = AmountType.Statistical; //can't null it so setting Statistical as an arbitrary default
                bool mod_with_uncertain_amount_found_at_the_location_of_this_link = false; //long, but self-explanatory
                foreach (ProteinSite link_site in lnk.Sites)
                {
                    foreach (var s in protein.Modifications)
                    {
                        if (s is StructuralModificationGroup)
                        {
                            foreach (ProteinSite mod_site in ((StructuralModificationGroup)s).ResidueSites)
                            {
                                //check if modification site is found among this link's sites
                                //if (lnk.Sites.All(o => ((StructuralModificationGroup)s).ResidueSites.Any(w => w.UID != null && w.UID == o.UID)))
                                //{
                                if (link_site.UID == mod_site.UID)
                                {
                                    lnk.Amount.AmountType = s.Amount.AmountType;

                                    if (new[] { AmountType.UncertainZero, AmountType.UncertainNonZero, AmountType.Statistical }.Contains(s.Amount.AmountType))
                                    {

                                        //lnk.Amount = s.Amount;
                                        //lnk.Amount.Low = 0;
                                        //lnk.Amount.High = 1;
                                        mod_with_uncertain_amount_found_at_the_location_of_this_link = true;
                                        uncertain_amount_type = s.Amount.AmountType;
                                        sum_of_low_mod_amounts += s.Amount.Low;
                                        sum_of_high_mod_amounts += s.Amount.High;
                                        sum_of_avg_mod_amounts += s.Amount.Center;
                                        if (s.Amount.High.GetValueOrDefault() > max_mod_amount)
                                        {
                                            max_high_mod_amount = s.Amount.High.GetValueOrDefault();
                                        }
                                        if (s.Amount.Center.GetValueOrDefault() > max_mod_amount)
                                        {
                                            max_avg_mod_amount = s.Amount.Center.GetValueOrDefault();
                                        }
                                        if (s.Amount.Low.GetValueOrDefault() < min_mod_amount)
                                        {
                                            min_low_mod_amount = s.Amount.Low.GetValueOrDefault();
                                        }
                                        need_to_adjust_link_amount = true;

                                    }
                                    //else if (new[] { AmountType.Exact, AmountType.Statistical }.Contains(s.Amount.AmountType))
                                    else if (s.Amount.AmountType == AmountType.Exact)
                                    {
                                        sum_of_mod_amounts += s.Amount.Numerator;
                                        need_to_adjust_link_amount = true;
                                    }
                                    //else if (s.Amount.AmountType == AmountType.Statistical)
                                    //{
                                    //    sum_of_mod_amounts += s.Amount.Center;
                                    //    need_to_adjust_link_amount = true;
                                    //}
                                    if (s.Amount.Numerator.GetValueOrDefault() > max_mod_amount)
                                    {
                                        max_mod_amount = s.Amount.Numerator.GetValueOrDefault();
                                    }
                                    if (s.Amount.Numerator.GetValueOrDefault() < min_mod_amount)
                                    {
                                        min_mod_amount = s.Amount.Numerator.GetValueOrDefault();
                                    }
                                }
                                if (max_mod_amount + sum_of_mod_amounts > 1)
                                {
                                    throw new Exception("Sum of modifications amounts at site " + mod_site.Id + " exceeds 1");
                                }
                            }

                        }

                    }
                }
                if (mod_with_uncertain_amount_found_at_the_location_of_this_link)
                {
                    lnk.Amount.AmountType = uncertain_amount_type;
                    lnk.Amount.Low = 0;
                    lnk.Amount.High = 1;
                }
                lnk.Amount.AdjustAmount();
                if (need_to_adjust_link_amount)
                {

                    if (lnk.LinkType == "cys-cys")
                    {
                        //for disulfides adjust according to other modifications at participating sites
                        if (new[] { AmountType.UncertainZero, AmountType.UncertainNonZero, AmountType.Statistical }.Contains(lnk.Amount.AmountType))
                        {
                            lnk.Amount.Low = 1 - max_high_mod_amount;
                            lnk.Amount.High = 1 - min_low_mod_amount;
                        }
                        //else if (lnk.Amount.AmountType == AmountType.Statistical)
                        //{
                        //    lnk.Amount.Center = 1 - max_mod_amount;
                        //}
                        else if (lnk.Amount.AmountType == AmountType.Exact)
                        {
                            lnk.Amount.Numerator = 1 - max_mod_amount;
                        }
                    }
                    else
                    {
                        if (max_mod_amount + sum_of_mod_amounts + Math.Max(lnk.Amount.Center.GetValueOrDefault(), Math.Max(lnk.Amount.High.GetValueOrDefault(), lnk.Amount.Numerator.GetValueOrDefault())) > 1)
                        {
                            throw new Exception("Sum of modifications amounts exceeds 1");
                        }
                    }
                }
            }
        }


        //SRS-372
        //Adjust numerical amount quantities to the number of sites
        //if modification amount is low=1, high=2, or center=5  
        //these quantities need to be adjusted by dividing them by number of sites of this modification
        private static void SiteAdjustModificationAmount(StructuralModificationGroup g)
        {
            StructuralModification mod = g.Modification;
            if (g.ResidueSites.Count() != 0)
            {

                if (mod.Amount.Low != null)
                {
                    mod.Amount.Low = Math.Round(mod.Amount.Low.GetValueOrDefault() / g.ResidueSites.Count(), 2);
                }
                if (mod.Amount.High != null)
                {
                    mod.Amount.High = Math.Round(mod.Amount.High.GetValueOrDefault() / g.ResidueSites.Count(), 2);
                }
                if (mod.Amount.Center != null)
                {
                    mod.Amount.Center = Math.Round(mod.Amount.Center.GetValueOrDefault() / g.ResidueSites.Count(), 2);
                    //mod.Amount.AmountType = AmountType.UncertainZero;
                }
                if (mod.Amount.Numerator != null)
                {
                    mod.Amount.Numerator = Math.Round(mod.Amount.Numerator.GetValueOrDefault() / g.ResidueSites.Count(), 2);
                }
            }
        }

        private static void ApplyAmountRules(StructuralModificationGroup g)
        {
            AmountRules ar = new AmountRules(g.Modification.Extent + "###" + g.Amount.SrsAmountType + "###" + g.Amount.ExtentAmountUnits);
            if (ar.Id != null)
            {
                if (ar.DivideBy100) g.Modification.Amount.DivideBy100();
                g.Modification.Amount.Unit = ar.SPLUnits;
                if (ar.SiteAdjust) SiteAdjustModificationAmount(g);
            }
            else
            {
                //YP SRS-374
                //for now do site adjustment for everything even if it's not found in amount rules
                //YP SRS-413 do not adjust if extent is complete
                if (!g.Modification.Amount.isExtentComplete)
                {
                    SiteAdjustModificationAmount(g);
                }
            }
        }
    }
}
