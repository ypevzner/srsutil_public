using FDA.SRS.ObjectModel;
using FDA.SRS.Services;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;

namespace FDA.SRS.Processing
{
    public static class StructurallyDiverseExtensions
    {
        public static StructurallyDiverse ReadStructurallyDiverse(this StructurallyDiverse sd, XElement xSD)
        {
            XElement xSourceMaterial = xSD.XPathSelectElement("SOURCE_MATERIAL");
            if (xSourceMaterial != null && !String.IsNullOrWhiteSpace(xSourceMaterial.Value))
                sd.SourceMaterial = sd.ReadSourceMaterial(xSourceMaterial);
            else
                sd.SourceMaterial = sd.ReadGsrsSD(xSD);

            XElement xAgentModification = xSD.XPathSelectElement("MODIFICATION_GROUP/AGENT_MODIFICATION");
            if (xAgentModification != null && !String.IsNullOrWhiteSpace(xAgentModification.Value))
            {
                throw new SrsException("sd_modification_not_supported_yet", xAgentModification.ToString());
                // TODO: implement
                // sd.AgentModification = sd.ReadAgentModification(xAgentModification);
            }

            return sd;
        }

        public static ISDNameResolver _sdNameResolver = new SimpleSDNameResolver();

        public static SourceMaterial ReadSourceMaterial(this StructurallyDiverse sd, XElement xSourceMaterial)
        {
            SourceMaterial sm = new SourceMaterial();

            SRSReadingUtils.readElement(xSourceMaterial, "SOURCE_MATERIAL_CLASS", null, v => sm.MaterialClass = v, sd.UNII);
            SRSReadingUtils.readElement(xSourceMaterial, "SOURCE_MATERIAL_TYPE", null, v => sm.MaterialType = v, sd.UNII);
            SRSReadingUtils.readElement(xSourceMaterial, "SOURCE_MATERIAL_STATE", null, v => sm.MaterialState = v, sd.UNII);

            SRSReadingUtils.readElement(xSourceMaterial, "PARENT_SUBSTANCE_ID", null, v => sm.SubstanceId = v, sd.UNII);
            SRSReadingUtils.readElement(xSourceMaterial, "PARENT_SUBSTANCE_NAME", null, v => sm.SubstanceName = v, sd.UNII);

            XElement xPartGroup = xSourceMaterial.XPathSelectElement("PART_GROUP");
            if (xPartGroup != null && !String.IsNullOrWhiteSpace(xPartGroup.Value))
            {
                SRSReadingUtils.readElement(xPartGroup, "PART", null, v => sm.Part = v, sd.UNII);
                SRSReadingUtils.readElement(xPartGroup, "PART_LOCATION", null, v => sm.PartLocation = v, sd.UNII);
            }

            if (!String.Equals(sm.Part, "WHOLE", StringComparison.CurrentCultureIgnoreCase))
                throw new SrsException("sd_type_not_supported_yet", xSourceMaterial.ToString());

            XElement xFraction = xSourceMaterial.XPathSelectElement("FRACTION");
            if (xFraction != null && !String.IsNullOrWhiteSpace(xFraction.Value))
            {
                SRSReadingUtils.readElement(xFraction, "MATERIAL_TYPE", null, v => sm.FractionType = v, sd.UNII);
                SRSReadingUtils.readElement(xFraction, "FRACTION", null, v => sm.Fraction = v, sd.UNII);
            }

            XElement xOrganism = xSourceMaterial.XPathSelectElement("ORGANISM");
            if (xOrganism != null && !String.IsNullOrWhiteSpace(xOrganism.Value))
                sm.Organism = sd.ReadOrganism(xOrganism);

            if (sm.Organism == null ||
                (String.IsNullOrWhiteSpace(sm.Organism.Genus) || String.IsNullOrWhiteSpace(sm.Organism.Species)) &&
                (String.IsNullOrWhiteSpace(sm.Organism.IntraspecificType) || String.IsNullOrWhiteSpace(sm.Organism.IntraspecificDescription)))
                throw new SrsException("not_enough_information", xSourceMaterial.ToString());

            // TODO: recover resolver
            // sm.Name = _sdNameResolver.Resolve(sm).Name;

            return sm;
        }

        public static SourceMaterial ReadGsrsSD(this StructurallyDiverse sd, XElement xSD)
        {
            SourceMaterial sm = new SourceMaterial();

            // Read PART and make sure it exists
            SRSReadingUtils.readElement(xSD, "PART", null, v => sm.Part = v, sd.UNII);
            if (String.IsNullOrWhiteSpace(sm.Part) || !String.Equals(sm.Part, "WHOLE", StringComparison.CurrentCultureIgnoreCase))
                throw new SrsException("sd_type_not_supported_yet", xSD.ToString());

            // Read and validate organism
            sm.Organism = sd.ReadOrganism(xSD);
            if (sm.Organism == null ||
                (String.IsNullOrWhiteSpace(sm.Organism.Genus) || String.IsNullOrWhiteSpace(sm.Organism.Species)) &&
                (String.IsNullOrWhiteSpace(sm.Organism.IntraspecificType) || String.IsNullOrWhiteSpace(sm.Organism.IntraspecificDescription)))
                throw new SrsException("not_enough_information", xSD.ToString());

            // TODO: recover resolver
            // sm.Name = _sdNameResolver.Resolve(sm).Name;

            // Read authors from GSRS, override the ones from SDF
            SRSReadingUtils.readElement(xSD, "TAXON_AUTHOR", null, v => sd.Authors = parseAuthor(v), sd.UNII);

            return sm;
        }

        public static IOrganismResolver _organismResolver = new NullOrganismResolver();
        // public static ITISDatabase _db = new ITISDatabase(@"D:\PROJECTS\FDA\SRS\data\itisSqlite062915\ITIS.sqlite");

        public static Organism ReadOrganism(this StructurallyDiverse sd, XElement xOrganism)
        {
            Organism o = new Organism();

            SRSReadingUtils.readElement(xOrganism, "KINGDOM", null, v => o.Kingdom = v, sd.UNII);
            SRSReadingUtils.readElement(xOrganism, "PHYLUM", null, v => o.Phylum = v, sd.UNII);
            SRSReadingUtils.readElement(xOrganism, "CLASS", null, v => o.Class = v, sd.UNII);
            SRSReadingUtils.readElement(xOrganism, "ORDER", null, v => o.Order = v, sd.UNII);
            SRSReadingUtils.readElement(xOrganism, "FAMILY", null, v => o.Family = v, sd.UNII);
            SRSReadingUtils.readElement(xOrganism, "GENUS", null, v => o.Genus = v, sd.UNII);
            SRSReadingUtils.readElement(xOrganism, "SPECIES", null, v => o.Species = v, sd.UNII);

            SRSReadingUtils.readElement(xOrganism, "INFRASPECIFIC_TYPE", null, v => o.IntraspecificType = v, sd.UNII);
            SRSReadingUtils.readElement(xOrganism, "INFRASPECIFIC_DESCRIPTION", null, v => o.IntraspecificDescription = v, sd.UNII);

            return _organismResolver.Resolve(o).Organism;
        }

        public static Organism ReadOrganismJSON(this StructurallyDiverse sd, JToken jOrganism)
        {
            Organism o = new Organism();


            SRSReadingUtils.readJsonElement(jOrganism, "$..organismFamily", null, v => o.Family = v, sd.UNII);
            SRSReadingUtils.readJsonElement(jOrganism, "$..organismGenus", null, v => o.Genus = v, sd.UNII);
            SRSReadingUtils.readJsonElement(jOrganism, "$..organismSpecies", null, v => o.Species = v, sd.UNII);

            SRSReadingUtils.readJsonElement(jOrganism, "$..infraSpecificType", null, v => o.IntraspecificType = v, sd.UNII);
            SRSReadingUtils.readJsonElement(jOrganism, "$..infraSpecificName", null, v => o.IntraspecificDescription = v, sd.UNII);

            //this part isn't quite right, as it has side effects,
            //and you don't typically want those on a method like this,
            //but since this is always used to set organism info, it should
            //be okay for now
            SRSReadingUtils.readJsonElement(jOrganism, "$..organismAuthor", null, v => sd.Authors = parseAuthor(v), sd.UNII);

            
      
            

            if (o.Family == null && o.Species == null && o.IntraspecificDescription == null)
            {
                return null;
            }

            return _organismResolver.Resolve(o).Organism;
        }

        public static AgentModification ReadAgentModification(this StructurallyDiverse sd, XElement xAgentMod)
        {
            AgentModification m = new AgentModification(null);
            SRSReadingUtils.readElement(xAgentMod, "AGENT_MODIFICATION_TYPE", null, v => m.ModificationType = v, sd.UNII);
            SRSReadingUtils.readElement(xAgentMod, "ROLE", null, v => m.Role = v, sd.UNII);
            SRSReadingUtils.readElement(xAgentMod, "MODIFICATION_AGENT", null, v => m.Agent = v, sd.UNII);
            SRSReadingUtils.readElement(xAgentMod, "MODIFICATION_AGENT_ID", null, v => m.AgentId = v, sd.UNII);
            SRSReadingUtils.readElement(xAgentMod, "MODIFICATION_PROCESS", null, v => m.Process = v, sd.UNII);
            return m;
        }

        private static IAuthorResolver _authorResolver = new NullAuthorResolver();

        public static void ParseSdfRefInfo(this StructurallyDiverse sd, string refInfo)
        {
            // Dirty XML fixes to extract authors -->
            if (!refInfo.StartsWith("<"))
                refInfo = "<" + refInfo;
            if (!refInfo.EndsWith(">"))
                refInfo = refInfo + ">";
            refInfo = Regex.Replace(refInfo, "<TAXON_AUTHOR>$", "</TAXON_AUTHOR>");

            if (refInfo != "<UNDEFINED>")
            {
                try
                {
                    sd.Authors = _authorResolver.Resolve(XDocument.Parse(refInfo).Root.Value).Author.ToString();
                }
                catch (XmlException ex)
                {
                    if (ex.Message.Contains("There are multiple root elements"))
                    {
                        XDocument xdoc = XDocument.Parse("<ROOT>" + refInfo + "</ROOT>");
                        IEnumerable<XElement> xels = xdoc.Root.XPathSelectElements("TAXON_AUTHOR");
                        sd.Authors = String.Join(", ", xels.Select(x => parseAuthor(x.Value)).Where(x1 => x1 != null));
                    }
                }
            }
            // Dirty XML fixes to extract authors <--
        }

        private static string parseAuthor(string author)
        {
            String author1 = _authorResolver.Resolve(author).Author.ToString();
            if (author1 != null && author1.ToLower() == "none")
            {
                return null;
            }
            return author;
        }
    }
}
