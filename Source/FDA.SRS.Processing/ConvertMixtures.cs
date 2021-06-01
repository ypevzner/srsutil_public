using FDA.SRS.ObjectModel;
using FDA.SRS.Utils;
using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Xml.Linq;
using System.Xml.XPath;
using System.Xml;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace FDA.SRS.Processing
{
    public static partial class Converter
    {
        public static List<Tuple<OutputFileType, string>> Mix2Spl(ImportOptions impOpt, ConvertOptions opt, ExportOptions expOpt)
        {
            List<Tuple<OutputFileType, string>> result = new List<Tuple<OutputFileType, string>>();

            SplOptions.ImportOptions = impOpt;
            SplOptions.ConvertOptions = opt;
            SplOptions.ExportOptions = expOpt;
            OperationalParameters pars = impOpt.PrepareConversion(expOpt);

            Log.Logger.Information("Converting mixtures using {@ImportOptions}, {@ConvertOptions}, {@ExportOptions}, {@OperationalParameters}", impOpt, opt, expOpt, pars);
            TraceUtils.WriteUNIITrace(TraceEventType.Information, null, null, "Processing file {0} with output into directory {1}...", impOpt.InputFile, expOpt.OutDir);

            int nErrors = 0, nRecords = 0;
            using (SdfReader r = new SdfReader(impOpt.InputFile, impOpt.InputFileEncoding) { FieldsMap = impOpt.SdfMapping, LineFilters = new[] { SrsSdfValidators.SubstanceSdfLineFilter } })
            {
                var mixtures = r.Records
                    .Where(s => s.HasField("MIX_SUBSTANCE_ID"))
                    .GroupBy(s => s.GetFieldValue("MIX_SUBSTANCE_ID"));
                mixtures
#if !DEBUG
					.AsParallel()
#endif
                    .ForAll(mix => {
                        bool is_representative_mixture = false;
                        Regex re_repr_struct = new Regex("Representative", RegexOptions.IgnoreCase);

                        //YP. The purpose of the for loop below is solely to see if any of the MIX_COMMENTS cotain "Representative"
                        //It can be commented out and replaced with the checking of only the First element of mixture if an assumption
                        //can be made that all of the MIX_COMMENTS will contain "Representative" for a representative mixture
                        foreach (SdfRecord sdf in mix)
                        {
                            string mix_comment = sdf.GetFieldValue("MIX_COMMENTS");
                            if (!String.IsNullOrEmpty(mix_comment))
                            {
                                if (re_repr_struct.IsMatch(mix_comment))
                                {
                                    is_representative_mixture = true;
                                }
                            }
                        }

                        //YP the below block should be used if an assumption
                        //can be made that all of the MIX_COMMENTS will contain "Representative" for a representative mixture
                        //thus checking only the first component will suffice
                        /*
                        string mix_comment = mix.First().GetFieldValue("MIX_COMMENTS");
                        if (!String.IsNullOrEmpty(mix_comment))
                        {
                            if (re_repr_struct.IsMatch(mix_comment))
                            {
                                is_representative_mixture = true;
                            }
                        }
                        */
                        string unii = mix.First().GetFieldValue("MIX_UNII");
                        if (String.IsNullOrEmpty(unii))
                        {
                            if (opt.GenerateMode == GenerateMode.NewSubstance)
                                unii = NEW_UNII;
                            else
                                throw new SrsException("mandatory_field_missing", "UNII is missing and GenerateMode != NewSubstance");
                        }

                        if (isRecordOfInterest(impOpt, opt, pars, unii))
                        {
                            var info = pars.GetSubstanceInfo(unii);
                            Mixture mixture = new Mixture { UNII = unii };
                            SplDocument splDoc = new SplDocument(mixture) { DocId = expOpt.DocId ?? Guid.NewGuid(), SetId = info.SetId, Version = info.VersionNumber };

                            try
                            {
                                MixtureSubstance mixSubst = new MixtureSubstance(splDoc.RootObject);
                                foreach (SdfRecord sdf in mix)
                                {
                                    string component_comment = sdf.GetFieldValue("COMMENTS");
                                    bool is_representative_component = false;
                                    if (!String.IsNullOrEmpty(component_comment))
                                    {
                                        if (re_repr_struct.IsMatch(component_comment))
                                        {
                                            is_representative_component = true;
                                        }
                                    }
                                    MixtureSubstance substance = sdf.ToSubstance(impOpt, opt, is_representative_component);
                                    if (substance.Moieties.Where(m => m.Molecule != null && m.Molecule.Ends != null && m.Molecule.Ends.Count > 0).Any())
                                        throw new SrsException("invalid_mol", "Fragments as moieties are not supported");

                                    //YP setting representative structure to true so that this can be reflected in the moiety's amount
                                    //this doesn't look to be correct so commenting out for now
                                    /*
                                    if (is_representative_mixture)
                                    {
                                        foreach (Moiety mty in substance.Moieties)
                                        {
                                            mty.RepresentativeStructure = true;
                                        }
                                    }
                                    */
                                    if (is_representative_mixture) { is_representative_component = true; }

                                    if (String.IsNullOrEmpty(mixSubst.SubstanceId))
                                    {
                                        mixSubst.SubstanceId = substance.SubstanceId;
                                        mixSubst.MixType = substance.MixType;
                                        mixSubst.UNII = substance.UNII;
                                        mixSubst.NamesXml = substance.NamesXml;
                                        mixSubst.PrimaryName = substance.PrimaryName;
                                    }
                                    //YP passing representative component indicator to set only the component moiety (not the submoieties) to representative
                                    mixSubst.AddMoiety(substance, is_representative_component);

                                    //Added for Ticket 349

                                    XDocument getMix = sdf.GetDescXml("MIX_DESC_PART", SrsDomain.Mixture, impOpt);
                                    
                                    if (getMix != null)
                                    {
                                        XElement ref_Approval_Id = getMix.XPathSelectElement("/SOURCE_MATERIAL/REF_APPROVAL_ID");
                                        if (ref_Approval_Id != null && !String.IsNullOrWhiteSpace(ref_Approval_Id.Value))
                                        {
                                            mixSubst.SourceMaterialExists = true;
                                            mixSubst.RefApprovalId = ref_Approval_Id.Value;
                                        }
                                        XElement ref_Pname = getMix.XPathSelectElement("/SOURCE_MATERIAL/REF_PNAME");
                                        if (ref_Pname != null && !String.IsNullOrWhiteSpace(ref_Pname.Value))
                                        {
                                            mixSubst.RefPName = ref_Pname.Value;
                                        }
                                    }
                                    
                                    mixSubst.MixSubstanceSPLCode("Mixture");

                                    //////

                                }

                                mixture.MixtureSubstance = mixSubst;
                                PopulateDerivationProcessModel(mixture);

                                // Create SPL document and write result files
                                writeResultFiles(opt, expOpt, pars, mix, splDoc, result);
                            }
                            catch (Exception ex)
                            {
                                if (ex is FatalException) throw ex;
                                Interlocked.Increment(ref nErrors);
                                writeErrorFiles(pars, expOpt, mix, splDoc, ex, result);
                            }

                            // Update processed records stats
                            Interlocked.Increment(ref nRecords);
                            if (nRecords % 100 == 0)
                                Console.Out.Write(".");
                            if (nRecords % 1000 == 0)
                                Console.Out.Write(nRecords);
                        }
                    });
            }

            TraceUtils.WriteUNIITrace(TraceEventType.Information, null, null, "Processed file '{0}': {1} errors out of {2} records", impOpt.InputFile, nErrors, nRecords);
            Log.Logger.Information("Result {@Result}", result);

            return result;
        }

        public static void PopulateDerivationProcessModel(Mixture mixture)
        {
            if (mixture.MixtureSubstance != null && mixture.MixtureSubstance.SourceMaterialExists == true)
            {
                
                mixture.derivation_process = new Mixture.DerivationProcess();
                mixture.derivation_process.DisplayName = "Modification";
                mixture.derivation_process.code = "C25572";
                mixture.derivation_process.codeSystem = "2.16.840.1.113883.3.26.1.1";
                mixture.derivation_process.interactors.Add(
                    new Mixture.DerivationProcess.Interactor
                    {
                        typeCode = "CSM",
                        unii = mixture.MixtureSubstance.RefApprovalId ?? "",
                        name = mixture.MixtureSubstance.RefPName ?? "",
                        codeSystem = "2.16.840.1.113883.4.9"
                    }
                );

                Mixture.DerivationProcess component_dp = new Mixture.DerivationProcess();
                component_dp.DisplayName = mixture.MixtureSubstance.DisplayName ?? "";
                component_dp.codeSystem = mixture.MixtureSubstance.CodeSystem ?? "";
                component_dp.code = mixture.MixtureSubstance.Code ?? "";
                mixture.derivation_process.components.Add(new Tuple<Mixture.DerivationProcess, int>(component_dp, 1));

            }
        }

        public static List<Tuple<OutputFileType, string>> MixSpl2Spl(ImportOptions impOpt, ConvertOptions opt, ExportOptions expOpt)
        {
            List<Moiety> Moieties_list = new List<Moiety>();
            List<Moiety> distinct_moieties = new List<Moiety>();
            IEnumerable<Moiety> Moieties;
            List<Tuple<OutputFileType, string>> result = new List<Tuple<OutputFileType, string>>();

            SplOptions.ImportOptions = impOpt;
            SplOptions.ConvertOptions = opt;
            SplOptions.ExportOptions = expOpt;
            OperationalParameters pars = impOpt.PrepareConversion(expOpt);

            Log.Logger.Information("Converting mixtures using {@ImportOptions}, {@ConvertOptions}, {@ExportOptions}, {@OperationalParameters}", impOpt, opt, expOpt, pars);
            TraceUtils.WriteUNIITrace(TraceEventType.Information, null, null, "Processing file {0} with output into directory {1}...", impOpt.InputFile, expOpt.OutDir);

            int nErrors = 0, nRecords = 0;

            //YP code
            XmlDocument Spl_in = new XmlDocument();
            Spl_in.Load(impOpt.InputFile);
            XmlElement root = Spl_in.DocumentElement;
            XmlNodeList moiety_elements = Spl_in.GetElementsByTagName("moiety");

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

            //Guid DocId = Guid.NewGuid();
            var info = pars.GetSubstanceInfo(unii);
            Mixture mixture = new Mixture { UNII = unii };
            SplDocument splDoc = new SplDocument(mixture) { DocId = expOpt.DocId ?? Guid.NewGuid(), SetId = info.SetId, Version = info.VersionNumber };


            //YP this block creates a list of new moiety objects from the moieties nodes of the input SPL
            try
            {
                foreach (XmlNode moiety_element in moiety_elements)
                {
                    XmlNode parent = moiety_element.ParentNode;
                    if (parent.Name == "identifiedSubstance")
                    {
                        Moieties_list.Add(MakeMoiety(moiety_element));
                    }
                }


                //YP this block removes existing hashcode element(s)
                XmlNodeList hash_elements = Spl_in.GetElementsByTagName("asEquivalentSubstance");
                while (hash_elements.Count > 0)
                {
                    hash_elements[0].ParentNode.RemoveChild(hash_elements[0]);
                    hash_elements = Spl_in.GetElementsByTagName("asEquivalentSubstance");
                }


                //YP this block removes existing moieties from SPL's identifiedSubstance
                while (moiety_elements.Count > 0)
                {
                    moiety_elements[0].ParentNode.RemoveChild(moiety_elements[0]);
                    moiety_elements = Spl_in.GetElementsByTagName("moiety");
                }

                distinct_moieties = Moieties_list.Distinct(new MoietyEqualityComparer()).ToList();
                //YP this block recalculates the hashcode based on the internal moieties representation
                string hashcode = String.Join("|", distinct_moieties.OrderBy(m => m.UID).Select(m => m.UID))
                    ?.GetMD5String();
                string formatted_hashcode = hashcode.FormatAsGuid();
                SplHash splhash = new SplHash(formatted_hashcode);


                //YP this block adds recalculated hashcode element,
                //regenerates moieties' SPLs from internal moieties representations and inserts them back into the SPL document

                //first let's update document id
                XmlNodeList id_elements = Spl_in.GetElementsByTagName("id");
                foreach (XmlNode id_element in id_elements)
                {
                    if (id_element.ParentNode.Name == "document")
                    {
                        id_element.Attributes["root"].Value = splDoc.DocId.ToString();
                        break;
                    }
                }

                XmlNodeList identified_substances = Spl_in.GetElementsByTagName("identifiedSubstance");
                foreach (XmlNode identified_substance in identified_substances)
                {
                    //YP identify <identifiedSubstance> node that should contain moieties
                    if (identified_substance.ParentNode.Name == "identifiedSubstance")
                    {
                        //YP add the hashcode containing element
                        identified_substance.InnerXml = identified_substance.InnerXml + splhash.SPL.ToString(SaveOptions.DisableFormatting);

                        foreach (Moiety distinct_moiety in distinct_moieties)
                        {
                            //YP append each of the moieties' SPL to the identifiedSubstance node
                            identified_substance.InnerXml = identified_substance.InnerXml + distinct_moiety.SPL.ToString(SaveOptions.DisableFormatting);
                        }
                    }
                }




                //YP this block updates SPL with the new hashcode
                //Spl_in.GetElementsByTagName("definingSubstance")[0]["code"].Attributes["code"].Value = formatted_hashcode;
                string dir = Path.Combine(expOpt.OutDir, OUT_DIR, splDoc.DocId.ToString(), "a");
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                string file = Path.Combine(dir, splDoc.DocId + ".xml");
                Spl_in.Save(file);

                //end YP code
                TraceUtils.WriteUNIITrace(TraceEventType.Information, null, null, "Processed file '{0}': {1} errors out of {2} records", impOpt.InputFile, nErrors, nRecords);
                Log.Logger.Information("Result {@Result}", result);

            }
            catch (Exception ex)
            {
                if (ex is FatalException) throw ex;
                Interlocked.Increment(ref nErrors);
                writeSPL2SPLErrorFiles(pars, expOpt, unii, Spl_in, splDoc, ex, result);
            }
            return result;
        }

        private static Moiety MakeMoiety(XmlNode moiety_xml)
        {
            string molfile = get_SPLmoiety_molfile(moiety_xml);
            string stereo = get_SPLmoiety_stereo(moiety_xml);
            string input_spl_inchi = get_SPLmoiety_inchi(moiety_xml);
            string input_spl_inchikey = get_SPLmoiety_inchikey(moiety_xml);
            double? amount_numerator = null;
            double amount_denominator = 1;
            string amount_denominator_unit = null;
            AmountType amount_numerator_type = AmountType.Statistical;
            double? amount_numerator_high = null;
            double? amount_numerator_low = null;
            string amount_numerator_unit = null;

            //bool? amount_numerator_high_inclusive = null;
            //bool? amount_numerator_low_inclusive = null;
            string structure_identifier_disagreement_message = null;
            SDFUtil.NewMolecule molfile_molecule = new SDFUtil.NewMolecule(molfile);
            if (molfile_molecule.InChI != input_spl_inchi)
            {
                structure_identifier_disagreement_message = "InChI " + input_spl_inchi + " does not represent the molfile structure";
            }
            else if (molfile_molecule.InChIKey != input_spl_inchikey)
            {
                structure_identifier_disagreement_message = "InChIKey " + input_spl_inchikey + " does not represent the molfile structure";
            }

            if (structure_identifier_disagreement_message != null)
            {
                //YP write error file
                throw new SrsException("molfile_inchi_mismatch", structure_identifier_disagreement_message);
            }


            get_SPLMoiety_AmountInfo(moiety_xml, out amount_numerator_type, out amount_numerator, out amount_numerator_unit, out amount_numerator_low, out amount_numerator_high, out amount_denominator, out amount_denominator_unit);


            //get_SPLMoiety_AmountNumeratorInfo(moiety_xml);

            //List<Moiety> submoieties = get_submoieties(moiety_xml);
            Amount m_amount = null;
            bool UndefinedAmt = true;
            if (amount_numerator == null)
            {
                UndefinedAmt = true;
                m_amount = new Amount(null, null, null);
                amount_numerator_low = null;
            }
            else
            {
                UndefinedAmt = false;
                m_amount = new Amount()
                {
                    Numerator = amount_numerator,
                    AmountType = amount_numerator_type,
                    Low = amount_numerator_low,
                    High = amount_numerator_high,
                    Unit = amount_numerator_unit,
                    Denominator = amount_denominator,
                    DenominatorUnit = amount_denominator_unit
                };
            }

            Moiety new_moiety = new Moiety()
            {
                //user innertext here maybe
                Molecule = new SDFUtil.NewMolecule(molfile),
                DecodedStereo = stereo,
                Submoieties = get_submoieties(moiety_xml),
                MoietyAmount = m_amount,
                UndefinedAmount = UndefinedAmt
            };


            return new_moiety;
        }

        private static List<Moiety> get_submoieties(XmlNode moiety_xml)
        {
            List<Moiety> return_submoieties = new List<Moiety>();
            XmlNode part_moiety_node = get_child_nodes(moiety_xml, "partMoiety")[0];
            foreach (XmlNode submoiety_node in get_child_nodes(part_moiety_node, "moiety"))
            {
                return_submoieties.Add(MakeMoiety(submoiety_node));
            }
            return return_submoieties;
        }

        private static void get_SPLMoiety_AmountInfo(
            XmlNode moiety_xml, out AmountType numerator_type,
            out double? amount_numerator, out string numerator_unit, out double? low,
            out double? high, out double amount_denominator, out string amount_denominator_unit)
        {
            bool? low_inclusive = null;
            bool? high_inclusive = null;
            string spl_numerator_type = null;
            XmlNode quantity_node = get_child_nodes(moiety_xml, "quantity")[0];
            XmlNode numerator_node = get_child_nodes(quantity_node, "numerator")[0];
            XmlNode denominator_node = get_child_nodes(quantity_node, "denominator")[0];
            string spl_numerator_unit = null;
            spl_numerator_unit = numerator_node.Attributes["unit"]?.Value;
            spl_numerator_type = numerator_node.Attributes["xsi:type"]?.Value;
            try
            {
                amount_numerator = Convert.ToDouble(numerator_node.Attributes["value"].Value);
            }
            catch
            {
                amount_numerator = null;
            }
            numerator_unit = numerator_node.Attributes["unit"]?.Value;
            amount_denominator = Convert.ToDouble(denominator_node.Attributes["value"]?.Value);
            amount_denominator_unit = denominator_node.Attributes["unit"]?.Value;
            //numerator_type = AmountType.Statistical;
            numerator_type = AmountType.Exact;

            try
            {
                XmlNode lowNode = get_child_nodes(numerator_node, "low")[0];
                low = Convert.ToDouble(lowNode.Attributes["value"]?.Value);
                //if (low == 0) { low = null; }
                low_inclusive = Convert.ToBoolean(lowNode.Attributes["inclusive"]?.Value);
            }
            catch
            {
                low = null;
                low_inclusive = null;
            }

            try
            {
                XmlNode highNode = get_child_nodes(numerator_node, "high")[0];
                high = Convert.ToDouble(highNode.Attributes["value"]?.Value);
                high_inclusive = Convert.ToBoolean(highNode.Attributes["inclusive"]?.Value);
            }
            catch
            {
                high = null;
                high_inclusive = null;
            }

            if (spl_numerator_type != null && spl_numerator_unit != null)
            {
                numerator_type = AmountType.Exact;
            }
            if (low != null && high != null)
            {
                numerator_type = AmountType.Statistical;
            }
            if (low != null && high == null && low_inclusive == false)
            {
                numerator_type = AmountType.UncertainNonZero;
            }
            if (low != null && high == null && low_inclusive == true)
            {
                numerator_type = AmountType.UncertainZero;
            }
        }




        private static string get_SPLmoiety_molfile(XmlNode moiety_xml)
        {
            foreach (XmlNode subjectof_node in get_child_nodes(moiety_xml, "subjectOf"))
            {
                foreach (XmlNode characteristic_node in get_child_nodes(subjectof_node, "characteristic"))
                {
                    XmlNode value_node = get_child_nodes(characteristic_node, "value")[0];
                    if (value_node.Attributes["mediaType"].Value == "application/x-mdl-molfile")
                    {
                        return value_node.InnerText;
                    }
                }
            }
            return null;
        }

        private static string get_SPLmoiety_inchi(XmlNode moiety_xml)
        {
            foreach (XmlNode subjectof_node in get_child_nodes(moiety_xml, "subjectOf"))
            {
                foreach (XmlNode characteristic_node in get_child_nodes(subjectof_node, "characteristic"))
                {
                    XmlNode value_node = get_child_nodes(characteristic_node, "value")[0];
                    if (value_node.Attributes["mediaType"].Value == "application/x-inchi")
                    {
                        return value_node.InnerText;
                    }
                }
            }
            return null;
        }

        private static string get_SPLmoiety_inchikey(XmlNode moiety_xml)
        {
            foreach (XmlNode subjectof_node in get_child_nodes(moiety_xml, "subjectOf"))
            {
                foreach (XmlNode characteristic_node in get_child_nodes(subjectof_node, "characteristic"))
                {
                    XmlNode value_node = get_child_nodes(characteristic_node, "value")[0];
                    if (value_node.Attributes["mediaType"].Value == "application/x-inchi-key")
                    {
                        return value_node.InnerText;
                    }
                }
            }
            return null;
        }

        private static string get_SPLmoiety_stereo(XmlNode moiety_xml)
        {
            foreach (XmlNode subjectof_node in get_child_nodes(moiety_xml, "subjectOf"))
            {
                foreach (XmlNode characteristic_node in get_child_nodes(subjectof_node, "characteristic"))
                {
                    XmlNode code_node = get_child_nodes(characteristic_node, "code")[0];
                    if (code_node.Attributes["displayName"].Value == "Stereochemistry Type")
                    {
                        XmlNode value_node = get_child_nodes(characteristic_node, "code")[0];
                        return value_node.Attributes["code"].Value;
                    }
                }
            }
            return null;
        }

        private static List<XmlNode> get_child_nodes(XmlNode parent_node, string child_node_name)
        {
            List<XmlNode> return_nodes = new List<XmlNode>();
            foreach (XmlNode child_node in parent_node.ChildNodes)
            {
                if (child_node.Name == child_node_name)
                {
                    return_nodes.Add(child_node);
                }
            }
            return return_nodes;
        }

        public static MixtureSubstance ToSubstance(this SdfRecord sdf, ImportOptions impOpt, ConvertOptions opt)
        {
            string _names = Substance.DecodeNames(sdf.GetFieldValue("SUBSTANCE_NAME"));
            MixtureSubstance substance = new MixtureSubstance(null)
            {
                SubstanceId = String.IsNullOrEmpty(sdf.GetFieldValue("MIX_SUBSTANCE_ID")) ? sdf.GetFieldValue("SUBSTANCE_ID") : sdf.GetFieldValue("MIX_SUBSTANCE_ID"),
                UNII = sdf.GetFieldValue("MIX_UNII"),
                MixType = decodeMixType(sdf.GetFieldValue("MIXTURE_TYPE")),
                NamesXml = _names,
                PrimaryName = Substance.DecodePrimaryName(_names)
            };
            TraceUtils.WriteUNIITrace(TraceEventType.Information, substance.UNII, null, "Processing record (SUBSTANCE_ID={0}, UNII={1})", substance.SubstanceId, substance.UNII);

            XDocument desc_xml = sdf.ValidateMixtureSdf(impOpt);

            string spl_special_stereo = null;
            if (desc_xml != null)
            {
                XElement xel = desc_xml.XPathSelectElement("/STEREOCHEMISTRY/TYPE");
                if (xel != null && !String.IsNullOrWhiteSpace(xel.Value))
                {
                    if (xel.Value.Trim().ToUpper() == "UNKNOWN")
                    {
                        ;
                    }
                    else
                        spl_special_stereo = xel.Value;
                }
            }

            sdf.Mol.ProcessMol((i, o) => {
                Stereomers s = Standardize.processMolecule(i, o,isV2000: sdf.Mol.Contains("V2000"));
                // TODO: Not good to assign moieties UNII this way...
                substance.Moieties = s.ToMoieties(spl_special_stereo, sdf["UNII"].FirstOrDefault()).Distinct(new MoietyEqualityComparer()).ToList();
            });

            return substance;
        }

        //YP overriding to be able to have a parameter to indicate if the component is representative
        public static MixtureSubstance ToSubstance(this SdfRecord sdf, ImportOptions impOpt, ConvertOptions opt, bool is_representative_component)
        {
            string _names = Substance.DecodeNames(sdf.GetFieldValue("SUBSTANCE_NAME"));
            MixtureSubstance substance = new MixtureSubstance(null)
            {
                SubstanceId = String.IsNullOrEmpty(sdf.GetFieldValue("MIX_SUBSTANCE_ID")) ? sdf.GetFieldValue("SUBSTANCE_ID") : sdf.GetFieldValue("MIX_SUBSTANCE_ID"),
                UNII = sdf.GetFieldValue("MIX_UNII"),
                MixType = decodeMixType(sdf.GetFieldValue("MIXTURE_TYPE")),
                NamesXml = _names,
                PrimaryName = Substance.DecodePrimaryName(_names)
            };
            TraceUtils.WriteUNIITrace(TraceEventType.Information, substance.UNII, null, "Processing record (SUBSTANCE_ID={0}, UNII={1})", substance.SubstanceId, substance.UNII);

            XDocument desc_xml = sdf.ValidateMixtureSdf(impOpt);

            string spl_special_stereo = null;
            if (desc_xml != null)
            {
                XElement xel = desc_xml.XPathSelectElement("/STEREOCHEMISTRY/TYPE");
                if (xel != null && !String.IsNullOrWhiteSpace(xel.Value))
                {
                    if (xel.Value.Trim().ToUpper() == "UNKNOWN")
                    {
                        ;
                    }
                    else
                        spl_special_stereo = xel.Value;
                }
            }

            sdf.Mol.ProcessMol((i, o) => {
                Stereomers s = Standardize.processMolecule(i, o, isV2000: sdf.Mol.Contains("V2000"));
                // TODO: Not good to assign moieties UNII this way...
                substance.Moieties = s.ToMoieties(spl_special_stereo, sdf["UNII"].FirstOrDefault(), is_representative_component).Distinct(new MoietyEqualityComparer()).ToList();
            });

            //YP SRS-310 Read amount JSON from MIXTURE_AMOUNT SDF tag for NON_STOICHIOMETRIC mixtures

            if (!String.IsNullOrEmpty(sdf.GetFieldValue("NON_STOICHIOMETRIC")))
            {
                if (!String.IsNullOrEmpty(sdf.GetFieldValue("MIXTURE_AMOUNT")))
                {
                    JObject o = JObject.Parse(sdf.GetFieldValue("MIXTURE_AMOUNT"));
                    Amount substance_amount = ReadAmountJson(substance.UNII, o);
                    if (substance_amount.isDefaultNumerator) {
                        //need to set this to low only
                        substance_amount.AmountType = AmountType.UncertainNonZero;
                        substance_amount.Low = 0;
                        //YP SRS-310, setting partial extent to true so that amount doesn't get adjusted to Statistical
                        substance_amount.isExtentPartial = true;
                        substance_amount.AdjustAmount();
                    }
                    MoietyCountAdjustAmount(substance_amount, substance.Moieties.Count());
                    foreach (Moiety moiety in substance.Moieties)
                    {
                        moiety.MoietyAmount = substance_amount;
                        
                    }

                }
                foreach (Moiety moiety in substance.Moieties)
                {
                    moiety.IsNonStoichiometric = true;

                }
            }
            return substance;
        }

        private static MixtureType decodeMixType(string type)
        {
            switch (type)
            {
                case "ALL":
                    return MixtureType.AllOf;
                case "ANY":
                    return MixtureType.AnyOf;
                case "ONE_OF":
                    return MixtureType.OneOf;
                default:
                    return MixtureType.None;
            }
        }

        private static void MoietyCountAdjustAmount(Amount amount, int moieties_count)
        {

            if (moieties_count != 0)
            {

                if (amount.Low != null)
                {
                    amount.Low = Math.Round(amount.Low.GetValueOrDefault() / moieties_count, 2);
                }
                if (amount.High != null)
                {
                    amount.High = Math.Round(amount.High.GetValueOrDefault() / moieties_count, 2);
                }
                if (amount.Center != null)
                {
                    amount.Center = Math.Round(amount.Center.GetValueOrDefault() / moieties_count, 2);
                    //amount.AmountType = AmountType.UncertainZero;
                }
                if (amount.Numerator != null)
                {
                    amount.Numerator = Math.Round(amount.Numerator.GetValueOrDefault() / moieties_count, 2);
                }
            }
        }

        public static Amount ReadAmountJson(String mixture_unii, JObject jAmount)
        {
            Amount a = new Amount { };  // See SRS-208 for (hopefully) explanation
            if (jAmount == null)
            {
                a.AdjustAmount();
                return a;
            }
            SRSReadingUtils.readJsonElement(jAmount, "type", ValidatedValues.AmountTypes.Keys.Contains, v => a.SrsAmountType = v, mixture_unii);
            SRSReadingUtils.readJsonElement(jAmount, "average", null, v => {
                var m = Regex.Match(v, @"(\d+)\s*/\s*(\d+)");
                if (m.Success)
                {
                    a.Numerator = double.Parse(m.Groups[1].Value);
                    a.Denominator = double.Parse(m.Groups[2].Value);
                    a.isDefaultDenominator = false;
                }
                else
                {
                    try { a.Center = double.Parse(v); }
                    catch (FormatException ex)
                    {
                        TraceUtils.ReportError("amount", mixture_unii, "Cannot parse <AMOUNT>: {0}", ex.Message);
                    }
                }
            }, mixture_unii);
            SRSReadingUtils.readJsonElement(jAmount, "low", null, v => a.Low = double.Parse(v), mixture_unii);
            SRSReadingUtils.readJsonElement(jAmount, "high", null, v => a.High = double.Parse(v), mixture_unii);


            if (a.Low == null)
            {
                SRSReadingUtils.readJsonElement(jAmount, "lowLimit", null, v => a.Low = double.Parse(v), mixture_unii);
            }
            if (a.High == null)
            {
                SRSReadingUtils.readJsonElement(jAmount, "highLimit", null, v => a.High = double.Parse(v), mixture_unii);
            }


            SRSReadingUtils.readJsonElement(jAmount, "nonNumericValue", null, v => a.NonNumericValue = v, mixture_unii);
            SRSReadingUtils.readJsonElement(jAmount, "units", ValidatedValues.Units.Keys.Contains, v => a.Unit = v, mixture_unii);
            a.AdjustAmount();

            //Note: This was changed to standardize units. This is not always a good idea,
            //since not all structural modifications have units like this.

            a.DenominatorUnit = "mol";
            return a;
        }
    }

    
}
