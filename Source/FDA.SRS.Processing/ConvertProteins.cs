using FDA.SRS.ObjectModel;
using FDA.SRS.Utils;
using Newtonsoft.Json.Linq;
using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Xml.Linq;
using System.Xml.XPath;
using System.Text;
using System.Xml;

namespace FDA.SRS.Processing
{
    public static partial class Converter
    {
        private static bool isProteinOfInterest(ImportOptions impOpt, Protein protein)
        {
            return
                impOpt.ProteinType == ProteinFeatures.Any ||
                (protein.ProteinType & ProteinFeatures.All) == impOpt.ProteinType;
        }

        private static XElement RemoveAllNamespaces(XElement xmlDocument)
        {
            if (!xmlDocument.HasElements)
            {
                XElement xElement = new XElement(xmlDocument.Name.LocalName);
                xElement.Value = xmlDocument.Value;

                foreach (XAttribute attribute in xmlDocument.Attributes())
                    xElement.Add(attribute);

                return xElement;
            }
            return new XElement(xmlDocument.Name.LocalName, xmlDocument.Elements().Select(el => RemoveAllNamespaces(el)));
        }

        private static String proteinChemFile = "protein_molecules.sdf";

        private static void deleteProteinLog()
        {
            string file = "hashlog.log";
            if (File.Exists(file))
            {
                File.Delete(file);
            }
            string pmfile = proteinChemFile;
            if (File.Exists(pmfile))
            {
                File.Delete(pmfile);
            }
        }

        private static void deleteProteinSpl2SplLog()
        {
            string file = "spl2splhashlog.log";
            if (File.Exists(file))
            {
                File.Delete(file);
            }
        }

        private static void writeProteinSpl2SplLog(string log_text)
        {
            string file = "spl2splhashlog.log";
            File.AppendAllLines(file, new string[] { log_text });
        }

        private static void writeProteinLog(string unii, SdfRecord imol, string uid, string type)
        {

            string smiles = "null";
            string inchikey = "null";
            if (imol != null)
            {

                SDFUtil.IMolecule mol = new SDFUtil.NewMolecule(imol.Mol);
                smiles = mol.SMILES;
                inchikey = mol.InChIKey;
                string pmfile = proteinChemFile;
                File.AppendAllText(pmfile, imol.ToString());
            }

            string file = "hashlog.log";
            File.AppendAllLines(file, new string[] { unii + "\t" + inchikey + "\t" + smiles + "\t" + uid + "\t" + type });
        }

        public static void logProteinHash(Protein protein, SdfRecord asChem, string readType)
        {
            //************************************************************
            // The following is for logging information regarding the hash

            string seqOnlyUID = String.Join("|", protein.Subunits.Select(su => su.Sequence.UID)).GetMD5String();
            string mwOnlyUID = Optional<MolecularWeight>.ofNullable(protein.MolecularWeight)
                                               .map(mw => mw.UID.GetMD5String())
                                               .orElse("null");
            string linksOnlyUID = String.Join("|", protein.Links.Select(l => l.UID)).GetMD5String();
            string fragmentUID = String.Join("|", protein.Fragments.Select(f => f.UID)).GetMD5String();
            string glycosylationUID = String.Join("|", protein.Glycosylations.Select(s => s.UID)).GetMD5String();
            string modificationUID = String.Join("|", protein.Modifications.Select(s => s.DefiningParts)).GetMD5String();
            string wholeProteinUID = protein.UID;


            writeProteinLog(protein.UNII,
                asChem,
                    seqOnlyUID
            // + "|" + mwOnlyUID 
            + "|" + linksOnlyUID
            + "|" + fragmentUID
            + "|" + glycosylationUID
            + "|" + modificationUID
            + "|" + wholeProteinUID,
                    readType);
            //************************************************************

        }

        /// <summary>
        /// Protein SDF to SPL
        /// </summary>
        /// <param name="opt"></param>
        public static List<Tuple<OutputFileType, string>> Prot2Spl(ImportOptions impOpt, ConvertOptions opt, ExportOptions expOpt)
        {
            List<Tuple<OutputFileType, string>> result = new List<Tuple<OutputFileType, string>>();

            SplOptions.ImportOptions = impOpt;
            SplOptions.ConvertOptions = opt;
            SplOptions.ExportOptions = expOpt;
            OperationalParameters pars = impOpt.PrepareConversion(expOpt);

            Log.Logger.Information("Converting mixtures using {@ImportOptions}, {@ConvertOptions}, {@ExportOptions}, {@OperationalParameters}", impOpt, opt, expOpt, pars);
            TraceUtils.WriteUNIITrace(TraceEventType.Information, null, null, "Processing file {0} with output into directory {1}...", impOpt.InputFile, expOpt.OutDir);

            proteinChemFile = Directory.GetParent(impOpt.InputFile).FullName + "/protein_molecules.sdf";
            deleteProteinLog();

            int nErrors = 0, nRecords = 0;





            using (SdfReader r = new SdfReader(impOpt.InputFile, impOpt.InputFileEncoding) { FieldsMap = impOpt.SdfMapping })
            {
                foreach (SdfRecord sdf in r.Records)
                {
                    string unii = sdf.GetFieldValue("UNII").Trim();

                    if (String.IsNullOrEmpty(unii))
                    {
                        if (opt.GenerateMode == GenerateMode.NewSubstance)
                            unii = "_NEW_UNII_";
                        else
                            throw new SrsException("mandatory_field_missing", "UNII is missing and GenerateMode != NewSubstance");
                    }

                    /*
                    if(!unii.Equals("5Y7IGH3V3Q")) {
                        continue;
                    }
                    */

                    if (isRecordOfInterest(impOpt, opt, pars, unii))
                    {

                        string[] readTypes = new string[] {
                           // "SDF",
                           // "JSON"                            
                           impOpt.ProteinSourceType
                        };
                        //TODO: Abstract this
                        //string readType = "SDF";
                        foreach (string readType in readTypes)
                        {
                            Protein protein = new Protein { Sdf = sdf, UNII = unii };
                            var info = pars.GetSubstanceInfo(unii);
                            SplDocument splDoc = new SplDocument(protein) { DocId = expOpt.DocId ?? Guid.NewGuid(), SetId = info.SetId, Version = info.VersionNumber };

                            try
                            {
                                sdf.ValidateSrsSdf(impOpt);
                                //If reading from JSON
                                if (readType.Equals("JSON"))
                                {
                                    String json1 = sdf.GetFieldValue("GSRS_JSON");

                                    JObject json;

                                    try
                                    {
                                        json = JObject.Parse(json1);
                                    }
                                    catch (Exception e)
                                    {
                                        throw new Exception("Record is missing GSRS_JSON property needed for parsing.");
                                    }


                                    Optional<JToken>.ofNullable(json.SelectToken("definitionLevel"))
                                                    .map(u => u.ToString())
                                                    .ifPresent(level => {
                                                        if (level.EqualsNoCase("incomplete"))
                                                        {
                                                            throw new Exception("Record is incomplete");
                                                        }
                                                        else if (level.EqualsNoCase("representative"))
                                                        {
                                                            throw new Exception("Record is representative");
                                                        }
                                                    });


                                    Optional<JToken>.ofNullable(json.SelectToken("definitionType"))
                                                    .map(u => u.ToString())
                                                    .ifPresent(level => {
                                                        if (level.EqualsNoCase("alternative"))
                                                        {
                                                            throw new Exception("Record is an alternative definition");
                                                        }
                                                    });

                                    protein.readFromJson(json, splDoc);

                                    if (isProteinOfInterest(impOpt, protein))
                                    {
                                        if (protein.Subunits.Count <= 0)
                                        {
                                            throw new Exception("Protein has no subunits");
                                        }
                                        SdfRecord imol = null;
                                        try
                                        {
                                            imol = protein.asChemical(new PolymerBaseReadingState { RootObject = splDoc }, opt);
                                            System.Console.WriteLine("Got as chem");
                                        }
                                        catch (Exception e)
                                        {
                                            throw new Exception(e.Message + " " + "Error encountered when attempting to convert protein to chemical. Skipping protein");
                                        }
                                        logProteinHash(protein, imol, readType);
                                        if (imol == null)
                                        {
                                            // Create SPL document and write result files
                                            writeResultFiles(opt, expOpt, pars, new List<SdfRecord> { sdf }, splDoc, result);

                                        }
                                        else
                                        {
                                            throw new Exception("Protein can be interpretted as chemical");
                                        }

                                    }
                                }
                                else
                                {
                                    XDocument descXml = sdf.GetDescXml("DESC_PART", SrsDomain.Protein, impOpt);
                                    if (descXml == null)
                                    {
                                        TraceUtils.WriteUNIITrace(TraceEventType.Warning, unii, null, "SRS XML description is not found in record with UNII {0} - skipping the record...", unii);
                                        continue;
                                    }

                                    if (impOpt.Features.Has("clear-srs-xml-ns"))
                                        descXml = XDocument.Parse(RemoveAllNamespaces(descXml.Root).ToString());

                                    var xPolymer = descXml.XPathSelectElement("//POLYMER");
                                    if (xPolymer != null && xPolymer.Value.Trim('0', ' ', ';').Trim().Length > 1)
                                    {
                                        if (!impOpt.Features.Has("ignore-empty-polymer") || !String.IsNullOrWhiteSpace(xPolymer.Value))
                                            throw new SrsException("invalid_srs_xml", "Record contains POLYMER description");
                                    }

                                    var xProteins = descXml.XPathSelectElements("//PROTEIN");
                                    if (xProteins.Count() > 1)
                                        throw new SrsException("invalid_srs_xml", "Record contains multiple PROTEIN descriptions");
                                    if (xProteins.Count() == 0)
                                        throw new SrsException("invalid_srs_xml", "Record does not contain PROTEIN description");

                                    // Read from SRS XML and compose the structure of SPL document 
                                    Exception readProteinException = null;
                                    try
                                    {
                                        protein.ReadProtein(xProteins.First(), splDoc, impOpt, opt);
                                    }
                                    catch (Exception ex)
                                    {
                                        readProteinException = ex;
                                    }

                                    if (isProteinOfInterest(impOpt, protein))
                                    {
                                        // Only log filtered proteins
                                        if (readProteinException != null)
                                            throw readProteinException;

                                        // Create SPL document and write result files
                                        writeResultFiles(opt, expOpt, pars, new List<SdfRecord> { sdf }, splDoc, result);
                                    }

                                }



                            }
                            catch (Exception ex)
                            {
                                if (ex.InnerException is FatalException)
                                {
                                    throw ex.InnerException;
                                }
                                Interlocked.Increment(ref nErrors);

                                writeProteinLog(unii, null, "ERROR", readType + "\t" + ex.ToString().GetMD5String());
                                
                                try
                                {
                                    writeErrorFiles(pars, expOpt, new List<SdfRecord> { sdf }, splDoc, ex, result);
                                }
                                catch (Exception e)
                                {
                                    TraceUtils.WriteUNIITrace(TraceEventType.Error, unii, null, "Error exporting errored record {0}", e.ToString());
                                    
                                }
                            }

                        }

                        // Update processed records stats
                        Interlocked.Increment(ref nRecords);
                        if (nRecords % 100 == 0)
                            Console.Out.Write(".");
                        if (nRecords % 1000 == 0)
                            Console.Out.Write(nRecords);
                    }
                    //throw new SrsException("mandatory_field_missing", "UNII is missing and GenerateMode != NewSubstance");

                }
            }

            TraceUtils.WriteUNIITrace(TraceEventType.Information, null, null, "Processed file '{0}': {1} errors out of {2} records", impOpt.InputFile, nErrors, nRecords);
            Log.Logger.Information("Result {@Result}", result);

            return result;
        }

        public static List<Tuple<OutputFileType, string>> ProtSpl2Spl(ImportOptions impOpt, ConvertOptions opt, ExportOptions expOpt)
        {
            List<Tuple<OutputFileType, string>> result = new List<Tuple<OutputFileType, string>>();

            SplOptions.ImportOptions = impOpt;
            SplOptions.ConvertOptions = opt;
            SplOptions.ExportOptions = expOpt;
            OperationalParameters pars = impOpt.PrepareConversion(expOpt);

            Log.Logger.Information("Converting mixtures using {@ImportOptions}, {@ConvertOptions}, {@ExportOptions}, {@OperationalParameters}", impOpt, opt, expOpt, pars);
            TraceUtils.WriteUNIITrace(TraceEventType.Information, null, null, "Processing file {0} with output into directory {1}...", impOpt.InputFile, expOpt.OutDir);

            proteinChemFile = Directory.GetParent(impOpt.InputFile).FullName + "/protein_molecules.sdf";
            deleteProteinSpl2SplLog();
            writeProteinSpl2SplLog("Begin protein spl2spl log");
            writeProteinSpl2SplLog("");
            int nErrors = 0, nRecords = 0;

            //YP code
            XmlDocument Spl_in = new XmlDocument();
            Spl_in.Load(impOpt.InputFile);
            XmlElement root = Spl_in.DocumentElement;
            XmlNodeList moiety_elements = Spl_in.GetElementsByTagName("moiety");
            List<XmlNode> subunit_moieties = new List<XmlNode>();

            //YP get unii
            string unii = null;
            string codeSystem = "2.16.840.1.113883.4.9";
            XmlNodeList code_elements = Spl_in.GetElementsByTagName("code");
            foreach (XmlNode code_element in code_elements)
            {
                //YP identify <identifiedSubstance> node that should contain moieties
                if (code_element.ParentNode.Name == "identifiedSubstance")
                {
                    unii = code_element.Attributes["code"].Value;
                    break;
                }
            }
            writeProteinSpl2SplLog("Begin protein spl2spl log for UNII " + unii);
            writeProteinSpl2SplLog("");

            Protein protein = new Protein { Sdf = null, UNII = unii };
            Guid DocID = Guid.Parse(root.GetElementsByTagName("id")[0].Attributes["root"].Value);
            Guid SetID = Guid.Parse(root.GetElementsByTagName("setId")[0].Attributes["root"].Value);
            int versionNuber = int.Parse(root.GetElementsByTagName("versionNumber")[0].Attributes["value"].Value);

            SplDocument splDoc = new SplDocument(protein) { DocId = expOpt.DocId ?? DocID, SetId = SetID, Version = versionNuber };
            writeProteinSpl2SplLog("Looking for subunit moieties in SPL");
            writeProteinSpl2SplLog("###############################");
            foreach (XmlNode moiety_element in moiety_elements)
            {
                XmlNode parent = moiety_element.ParentNode;
                if (parent.Name == "identifiedSubstance")
                {
                    if (get_SPLmoiety_displayName(moiety_element) == "PROTEIN SUBUNIT")
                    {
                        subunit_moieties.Add(moiety_element);
                        writeProteinSpl2SplLog("-----------------------");
                        writeProteinSpl2SplLog("Found subunit moiety:");
                        writeProteinSpl2SplLog(moiety_element.OuterXml);
                    }
                }
            }
            writeProteinSpl2SplLog("###############################");
            writeProteinSpl2SplLog("");
            writeProteinSpl2SplLog("Creating subunit objects");

            foreach (XmlNode subunit_moiety in subunit_moieties)
            {
                writeProteinSpl2SplLog("-----------------------");
                writeProteinSpl2SplLog("Creating subunit with sequence " + get_SPLmoiety_subunit_sequence(subunit_moiety));
                protein.Subunits.Add(new Subunit(splDoc) { Sequence = new Sequence(splDoc, get_SPLmoiety_subunit_sequence(subunit_moiety)) });
                writeProteinSpl2SplLog("Subunit object added to protein object");
            }
            writeProteinSpl2SplLog("###############################");
            writeProteinSpl2SplLog("");
            writeProteinSpl2SplLog("Original subunit order is");
            foreach (Subunit subunit in protein.Subunits)
            {
                writeProteinSpl2SplLog(subunit.Id + " with sequence " + subunit.Sequence.ToString());
            }
            writeProteinSpl2SplLog("");
            writeProteinSpl2SplLog("");
            writeProteinSpl2SplLog("Looking for Cysteine disulfide link moieties in SPL");
            writeProteinSpl2SplLog("###############################");

            XmlNodeList partmoiety_elements = Spl_in.GetElementsByTagName("partMoiety");
            foreach (XmlNode partmoiety_element in partmoiety_elements)
            {
                if (get_SPLmoiety_displayName(partmoiety_element) == "Cysteine disulfide")
                {
                    writeProteinSpl2SplLog("-----------------------");
                    writeProteinSpl2SplLog("Found Cystene disulfide link moiety:");
                    writeProteinSpl2SplLog(partmoiety_element.OuterXml);
                    List<Tuple<int, int>> tlist = new List<Tuple<int, int>>();
                    List<XmlNode> bond_nodes = get_child_nodes(partmoiety_element, "bond");

                    XmlNode distalMoiety1 = get_child_nodes(bond_nodes[0], "distalMoiety")[0];
                    XmlNode distalMoiety2 = get_child_nodes(bond_nodes[1], "distalMoiety")[0];
                    writeProteinSpl2SplLog("distalMoiety1:");
                    writeProteinSpl2SplLog(distalMoiety1.OuterXml);
                    writeProteinSpl2SplLog("");
                    writeProteinSpl2SplLog("distalMoiety2:");
                    writeProteinSpl2SplLog(distalMoiety2.OuterXml);
                    tlist.Add(new Tuple<int, int>(int.Parse(get_child_nodes(distalMoiety1, "id")[0].Attributes["extension"].Value.Replace("SU", "")) - 1, int.Parse(get_child_nodes(bond_nodes[0], "positionNumber")[1].Attributes["value"].Value) - 1));
                    tlist.Add(new Tuple<int, int>(int.Parse(get_child_nodes(distalMoiety2, "id")[0].Attributes["extension"].Value.Replace("SU", "")) - 1, int.Parse(get_child_nodes(bond_nodes[1], "positionNumber")[1].Attributes["value"].Value) - 1));
                    Link li = PolymerBaseExtensions.FragmentFactory.CreateLink(protein, tlist, "cys-cys", splDoc);
                    writeProteinSpl2SplLog("");
                    writeProteinSpl2SplLog("Based on the above XML a Link was created with the following original order link sites:");
                    foreach (ProteinSite linksite in li.Sites)
                    {
                        writeProteinSpl2SplLog("Site subunit: " + linksite.Subunit.ToString() + ", site position: " + linksite.Position.ToString());
                    }
                    /*
                     * foreach (Tuple<int,int> tup in tlist)
                    {
                        writeProteinSpl2SplLog(tup.ToString());
                    }
                    */
                    writeProteinSpl2SplLog("");
                    writeProteinSpl2SplLog("... and the following cys-cys linker as described by its UID:");
                    writeProteinSpl2SplLog(li.Linker.UID);
                    writeProteinSpl2SplLog("");
                    writeProteinSpl2SplLog("The UID of the above link is:");
                    writeProteinSpl2SplLog(li.UID);
                    writeProteinSpl2SplLog("");
                    writeProteinSpl2SplLog("This link has been added to the protein object's Links list");
                    protein.Links.Add(li);
                }
            }

            writeProteinSpl2SplLog("");
            writeProteinSpl2SplLog("Protein links are currently not ordered when generating links portion of the protein hashcode and are used in their original order:");
            foreach (Link link in protein.Links)
            {
                writeProteinSpl2SplLog(link.UID);
            }


            writeProteinSpl2SplLog("###############################");
            writeProteinSpl2SplLog("");
            writeProteinSpl2SplLog("Putting together subunit portion of the protein hashcode");
            writeProteinSpl2SplLog("");
            writeProteinSpl2SplLog("Reordering Subunits based on their sequences: protein.Subunits.OrderBy(s => s.Sequence.ToString())");
            writeProteinSpl2SplLog("");
            writeProteinSpl2SplLog("Reordered Subunit Sequences");
            //protein.Subunits.OrderBy(s => s.Sequence.ToString()).Select(s => writeProteinSpl2SplLog(s.Id + " with sequence " + s.Sequence.ToString())
            writeProteinSpl2SplLog(String.Join(Environment.NewLine, protein.Subunits.OrderBy(s => s.Sequence.ToString()).Select(s => s.Id + " with sequence " + s.Sequence.ToString())));
            writeProteinSpl2SplLog("-----------------------");
            writeProteinSpl2SplLog("");
            writeProteinSpl2SplLog("Reordered Subunit UIDs");
            writeProteinSpl2SplLog(String.Join(Environment.NewLine, protein.Subunits.OrderBy(s => s.Sequence.ToString()).Select(s => s.Id + " with UID " + s.UID)));

            writeProteinSpl2SplLog("###############################");
            writeProteinSpl2SplLog("");
            writeProteinSpl2SplLog("Final subunit portion of the hashcode (based on sequence based re-ordering of subunits):");
            writeProteinSpl2SplLog(String.Join("|", protein.Subunits.OrderBy(s => s.Sequence.ToString()).Select(s => s.UID)));
            writeProteinSpl2SplLog("###############################");
            writeProteinSpl2SplLog("");
            writeProteinSpl2SplLog("Final links portion of the protein hashcode (based on original order subunits):");
            writeProteinSpl2SplLog(String.Join("|", protein.Links.Select(s => s.UID)));

            string hashcode = protein.UID;
            return result;
        }

        private static string get_SPLmoiety_subunit_sequence(XmlNode moiety_xml)
        {
            foreach (XmlNode subjectof_node in get_child_nodes(moiety_xml, "subjectOf"))
            {
                foreach (XmlNode characteristic_node in get_child_nodes(subjectof_node, "characteristic"))
                {
                    XmlNode value_node = get_child_nodes(characteristic_node, "value")[0];
                    if (value_node.Attributes["mediaType"].Value == "application/x-aa-seq")
                    {
                        return value_node.InnerText;
                    }
                }
            }
            return null;
        }

        private static string get_SPLmoiety_displayName(XmlNode moiety_xml)
        {
            try
            {
                return get_child_nodes(moiety_xml, "code")[0].Attributes["displayName"].Value;
            }
            catch
            {
                return "";
            }
        }
    }
}
