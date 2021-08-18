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
        private static bool isNAOfInterest(ImportOptions impOpt, NucleicAcid na)
        {
            return
                impOpt.NAType == NAFeatures.Any ||
                (na.Type & NAFeatures.All) == impOpt.NAType;
        }


        private static String NAChemFile = "nucleic_acid_molecules.sdf";

        private static void deleteNALog()
        {
            string file = "hashlog.log";
            if (File.Exists(file))
            {
                File.Delete(file);
            }
            string pmfile = NAChemFile;
            if (File.Exists(pmfile))
            {
                File.Delete(pmfile);
            }
        }
        private static void writeNALog(string unii, SdfRecord imol, string uid, string type)
        {

            string smiles = "null";
            string inchikey = "null";
            if (imol != null)
            {

                SDFUtil.IMolecule mol = new SDFUtil.NewMolecule(imol.Mol);
                smiles = mol.SMILES;
                inchikey = mol.InChIKey;
                string pmfile = NAChemFile;
                File.AppendAllText(pmfile, imol.ToString());
            }

            string file = "hashlog.log";
            File.AppendAllLines(file, new string[] { unii + "\t" + inchikey + "\t" + smiles + "\t" + uid + "\t" + type });
        }

        public static void logNAHash(NucleicAcid na, SdfRecord asChem, string readType)
        {
            //************************************************************
            // The following is for logging information regarding the hash

            string seqOnlyUID = String.Join("|", na.Subunits.Select(su => su.SugarSensitiveSequence.UID)).GetMD5String();
            string mwOnlyUID = Optional<MolecularWeight>.ofNullable(na.MolecularWeight)
                                               .map(mw => mw.UID.GetMD5String())
                                               .orElse("null");
            string linksOnlyUID = String.Join("|", na.Links.Select(l => l.UID)).GetMD5String();
            string fragmentUID = String.Join("|", na.Fragments.Select(f => f.UID)).GetMD5String();
            string modificationUID = String.Join("|", na.Modifications.Select(s => s.DefiningParts)).GetMD5String();
            string wholeNAUID = na.UID;


            writeNALog(na.UNII,
                asChem,
                    seqOnlyUID
            // + "|" + mwOnlyUID 
            + "|" + linksOnlyUID
            + "|" + fragmentUID
            + "|" + modificationUID
            + "|" + wholeNAUID,
                    readType);
            //************************************************************

        }

        /// <summary>
        /// Nucleic Acid SDF to SPL
        /// </summary>
        /// <param name="opt"></param>
        public static List<Tuple<OutputFileType, string>> Na2Spl(ImportOptions impOpt, ConvertOptions opt, ExportOptions expOpt)
        {
            List<Tuple<OutputFileType, string>> result = new List<Tuple<OutputFileType, string>>();

            SplOptions.ImportOptions = impOpt;
            SplOptions.ConvertOptions = opt;
            SplOptions.ExportOptions = expOpt;
            OperationalParameters pars = impOpt.PrepareConversion(expOpt);

            Log.Logger.Information("Converting mixtures using {@ImportOptions}, {@ConvertOptions}, {@ExportOptions}, {@OperationalParameters}", impOpt, opt, expOpt, pars);
            TraceUtils.WriteUNIITrace(TraceEventType.Information, null, null, "Processing file {0} with output into directory {1}...", impOpt.InputFile, expOpt.OutDir);

            NAChemFile = Directory.GetParent(impOpt.InputFile).FullName + "/nucleic_acid_molecules.sdf";
            deleteNALog();

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
                           impOpt.NASourceType
                        };
                        //TODO: Abstract this
                        //string readType = "SDF";
                        foreach (string readType in readTypes)
                        {
                            NucleicAcid na = new NucleicAcid { Sdf = sdf, UNII = unii };
                            var info = pars.GetSubstanceInfo(unii);
                            SplDocument splDoc = new SplDocument(na) { DocId = expOpt.DocId ?? Guid.NewGuid(), SetId = info.SetId, Version = info.VersionNumber };

                            try { 
                            //if (1 == 1) { 
                            
                                sdf.ValidateSrsSdf(impOpt);
                                //If reading from JSON
                                if (readType.Equals("JSON"))
                                {
                                    JObject json = JObject.Parse(sdf.GetFieldValue("GSRS_JSON"));


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

                                    na.readFromJson(json, splDoc);

                                    if (isNAOfInterest(impOpt, na))
                                    {
                                        if (na.Subunits.Count <= 0)
                                        {
                                            throw new Exception("Nucleic acid has no subunits");
                                        }
                                        //List<SdfRecord> imols = new List<SdfRecord>();
                                        SdfRecord imol = null;
                                        try { 
                                        //if (1==1){
                                            imol = na.asChemical(new PolymerBaseReadingState { RootObject = splDoc }, opt);
                                            System.Console.WriteLine("Got as chem");
                                            
                                        }

                                        catch (Exception e)
                                        {
                                            if (e.Message != "Nucleic Acid is too large to be interpretted as chemical")
                                            {
                                                throw e;
                                            }
                                            else
                                            {
                                                
                                            }
                                            //Trouble converting to chemical
                                        }
                                        
                                        //for (int i = 0; i <= imols.Count; i++)
                                        //{
                                        logNAHash(na, imol, readType);
                                        //}
                                        if (imol==null)
                                        {
                                            // Create SPL document and write result files
                                            writeResultFiles(opt, expOpt, pars, new List<SdfRecord> { sdf }, splDoc, result);

                                        }
                                        else
                                        {
                                            throw new Exception("Nucleic acid can be interpretted as chemical");
                                        }

                                    }
                                }
                                else
                                {
                                    XDocument descXml = sdf.GetDescXml("DESC_PART", SrsDomain.NucleicAcid, impOpt);
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

                                    var xNAs = descXml.XPathSelectElements("//NUCLEIC_ACID");
                                    if (xNAs.Count() > 1)
                                        throw new SrsException("invalid_srs_xml", "Record contains multiple NUCLEIC_ACID descriptions");
                                    if (xNAs.Count() == 0)
                                        throw new SrsException("invalid_srs_xml", "Record does not contain NUCLEIC_ACID description");

                                    // Read from SRS XML and compose the structure of SPL document 
                                    Exception readNAException = null;
                                    try
                                    {
                                        na.ReadNA(xNAs.First(), splDoc, impOpt, opt);
                                    }
                                    catch (Exception ex)
                                    {
                                        readNAException = ex;
                                    }

                                    if (isNAOfInterest(impOpt, na))
                                    {
                                        // Only log filtered nucleic acids
                                        if (readNAException != null)
                                            throw readNAException;

                                        // Create SPL document and write result files
                                        writeResultFiles(opt, expOpt, pars, new List<SdfRecord> { sdf }, splDoc, result);
                                    }

                                }



                            }
                            
                            catch (Exception ex)
                            {
                                if (ex is FatalException)
                                {
                                    throw ex;
                                }
                                Interlocked.Increment(ref nErrors);

                                writeNALog(unii, null, "ERROR", readType + "\t" + ex.ToString().GetMD5String());
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

        public static List<Tuple<OutputFileType, string>> NASpl2Spl(ImportOptions impOpt, ConvertOptions opt, ExportOptions expOpt)
        {
            List<Tuple<OutputFileType, string>> result = new List<Tuple<OutputFileType, string>>();

            SplOptions.ImportOptions = impOpt;
            SplOptions.ConvertOptions = opt;
            SplOptions.ExportOptions = expOpt;
            OperationalParameters pars = impOpt.PrepareConversion(expOpt);

            Log.Logger.Information("Converting mixtures using {@ImportOptions}, {@ConvertOptions}, {@ExportOptions}, {@OperationalParameters}", impOpt, opt, expOpt, pars);
            TraceUtils.WriteUNIITrace(TraceEventType.Information, null, null, "Processing file {0} with output into directory {1}...", impOpt.InputFile, expOpt.OutDir);

            NAChemFile = Directory.GetParent(impOpt.InputFile).FullName + "/nucleic_acid_molecules.sdf";
            deleteNALog();

            int nErrors = 0, nRecords = 0;

            //YP code
            XmlDocument Spl_in = new XmlDocument();
            Spl_in.Load(impOpt.InputFile);
            XmlElement root = Spl_in.DocumentElement;
            XmlNodeList moiety_elements = Spl_in.GetElementsByTagName("moiety");
            List<XmlNode> subunit_moieties = new List<XmlNode>();

            //YP get unii
            string unii = null;
            XmlNodeList code_elements = Spl_in.GetElementsByTagName("code");
            foreach (XmlNode code_element in code_elements)
            {
                //YP identify <identifiedSubstance> node that should contain moieties
                if (code_element.ParentNode.Name == "identifiedSubstance")
                {
                    unii = code_element.Attributes["code"].Value;
                }
            }

            NucleicAcid na = new NucleicAcid { Sdf = null, UNII = unii };
            Guid DocID = Guid.Parse(root.GetElementsByTagName("id")[0].Attributes["root"].Value);
            Guid SetID = Guid.Parse(root.GetElementsByTagName("setId")[0].Attributes["root"].Value);
            int versionNuber = int.Parse(root.GetElementsByTagName("versionNumber")[0].Attributes["value"].Value);

            SplDocument splDoc = new SplDocument(na) { DocId = expOpt.DocId ?? DocID, SetId = SetID, Version = versionNuber };

            foreach (XmlNode moiety_element in moiety_elements)
            {
                XmlNode parent = moiety_element.ParentNode;
                if (parent.Name == "identifiedSubstance")
                {
                    if (get_SPLmoiety_displayName(moiety_element) == "NUCLEIC ACID SUBUNIT")
                    {
                        subunit_moieties.Add(moiety_element);
                    }
                }
            }

            foreach (XmlNode subunit_moiety in subunit_moieties)
            {
                na.Subunits.Add(new NASubunit(splDoc) { Sequence = new NASequence(splDoc, get_SPLmoiety_subunit_sequence(subunit_moiety)) });
            }


            XmlNodeList partmoiety_elements = Spl_in.GetElementsByTagName("partMoiety");
            foreach (XmlNode partmoiety_element in partmoiety_elements)
            {
                if (get_SPLmoiety_displayName(partmoiety_element) == "Cysteine disulfide")
                {
                    List<Tuple<int, int>> tlist = new List<Tuple<int, int>>();
                    List<XmlNode> bond_nodes = get_child_nodes(partmoiety_element, "bond");
                    XmlNode distalMoiety1 = get_child_nodes(bond_nodes[0], "distalMoiety")[0];
                    XmlNode distalMoiety2 = get_child_nodes(bond_nodes[1], "distalMoiety")[0];
                    tlist.Add(new Tuple<int, int>(int.Parse(get_child_nodes(distalMoiety1, "id")[0].Attributes["extension"].Value.Replace("SU", "")) - 1, int.Parse(get_child_nodes(bond_nodes[0], "positionNumber")[1].Attributes["value"].Value) - 1));
                    tlist.Add(new Tuple<int, int>(int.Parse(get_child_nodes(distalMoiety2, "id")[0].Attributes["extension"].Value.Replace("SU", "")) - 1, int.Parse(get_child_nodes(bond_nodes[1], "positionNumber")[1].Attributes["value"].Value) - 1));
                    NALink li = PolymerBaseExtensions.NAFragmentFactory.CreateLink(na, tlist, "cys-cys", splDoc);
                    na.Links.Add(li);
                }
            }

            string hashcode = na.UID;
            return result;
        }

        
    }
}
