using FDA.SRS.ObjectModel;
using FDA.SRS.Utils;
using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Xml.Linq;
using Newtonsoft.Json.Linq;

namespace FDA.SRS.Processing
{
    public static partial class Converter
    {
        public static List<Tuple<OutputFileType, string>> Polymer2Spl(ImportOptions impOpt, ConvertOptions opt, ExportOptions expOpt)
        {
            List<Tuple<OutputFileType, string>> result = new List<Tuple<OutputFileType, string>>();

            SplOptions.ImportOptions = impOpt;
            SplOptions.ConvertOptions = opt;
            SplOptions.ExportOptions = expOpt;
            OperationalParameters pars = impOpt.PrepareConversion(expOpt);

            foreach (string file in pars.InputFiles)
                Polymer2Spl(file, impOpt, opt, expOpt, pars, result);

            return result;
        }

        private static void Polymer2Spl(string file, ImportOptions impOpt, ConvertOptions opt, ExportOptions expOpt, OperationalParameters pars, List<Tuple<OutputFileType, string>> result)
        {
            Log.Logger.Information("Converting {File} using {@ImportOptions}, {@ConvertOptions}, {@ExportOptions}, {@OperationalParameters}", file, impOpt, opt, expOpt, pars);
            TraceUtils.WriteUNIITrace(TraceEventType.Information, null, null, "Processing file {0} with output into directory {1}...", file, expOpt.OutDir);


            string linear_polymer_file = Path.Combine("linear_polymers.sdf");

            string linear_homopolymer_file = Path.Combine("linear_homopolymer_polymers.sdf");
            string linear_na_homopolymer_file = Path.Combine("linear_na_homopolymer_polymers.sdf");
            string linear_block_homopolymer_file = Path.Combine("linear_block_homopolymer_polymers.sdf");
            string linear_substituted_homopolymer_file = Path.Combine("linear_substituted_homopolymer_polymers.sdf");
            string linear_double_stranded_homopolymer_file = Path.Combine("linear_double-stranded_homopolymer_polymers.sdf");
            //string linear_crosslinked_homopolymer = Path.Combine("linear_crosslinked_homopolymer_polymers.sdf");

            string linear_copolymer_file = Path.Combine("linear_copolymer_polymers.sdf");
            string linear_na_copolymer_file = Path.Combine("linear_na_copolymer_polymers.sdf");
            string linear_block_copolymer_file = Path.Combine("linear_block_copolymer_polymers.sdf");
            string linear_random_copolymer_file = Path.Combine("linear_random_copolymer_polymers.sdf");
            string linear_double_stranded_copolymer_file = Path.Combine("linear_double-stranded_copolymer_polymers.sdf");

            int nErrors = 0, nRecords = 0;
            Func<SdfRecord, int, string, string> conv = SrsSdfValidators.SubstanceSdfLineFilter;

            
            using (SdfReader r = new SdfReader(file, impOpt.InputFileEncoding) { FieldsMap = impOpt.SdfMapping, LineFilters = new[] { conv } })
            {
                r.Records
#if !DEBUG
					//.AsParallel()
#endif
                    .ForAll(sdf => {
                        try
                        {
                            //Counters.Reset();
                            string unii = sdf.GetFieldValue("UNII");
                            if (String.IsNullOrEmpty(unii))
                            {
                                if (opt.GenerateMode == GenerateMode.NewSubstance)
                                    unii = NEW_UNII;
                                else
                                    throw new SrsException("mandatory_field_missing", "UNII is missing and GenerateMode != NewSubstance");
                            }

                            if (!isRecordOfInterest(impOpt, opt, pars, unii))
                                Log.Logger.Debug("Skipping record (UNII={0})...", unii);
                            else
                            {
                                string substance_id = null;
                                try
                                {
                                    substance_id = sdf.GetFieldValue("SUBSTANCE_ID");
                                }
                                catch (Exception ex)
                                {
                                    if (opt.GenerateMode != GenerateMode.NewSubstance)
                                        throw new SrsException("mandatory_field_missing", String.Format("SUBSTANCE_ID field is not available: {0}", ex.Message), ex);
                                }

                                Log.Logger.Debug("Processing record (UNII={0})...", unii);
                                sdf.ValidateSrsSdf(impOpt);
                                JObject job = sdf.GetGSRSJson();

                                Polymer polymer = new Polymer { Sdf = sdf, UNII = unii };
                                String mfile = job.SelectToken("..polymer.idealizedStructure.molfile").ToString();

                                // YP "manual" specification of the molfile input
                                //mfile = File.ReadAllText(@"YZC5LZ8BUB_2.mol");
                                // YP Code to extract linear homopolymers
                                /*int num_structural_units = job.SelectToken("..polymer.structuralUnits").Count();
                                String structural_unit_type = "";
                                if (num_structural_units == 1)
                                {
                                    structural_unit_type = job.SelectToken("..polymer.structuralUnits").First["type"].ToString();
                                    if (structural_unit_type == "HEAD-TAIL")
                                    {
                                        
                                        File.AppendAllText(@"output-polymer-2018-10-08_homolinear.json",",\n" + job.ToString());
                                        rec_count += 1;
                                        if (rec_count > 3) return;
                                    }
                                }
                                */
                                SdfRecord plmr_sdf = new SdfRecord();
                                plmr_sdf.Mol = mfile;
                                Plmr plmr = new Plmr { UNII = unii, Sdf = plmr_sdf };
                                
                                plmr.plmr_geometry = job.SelectToken("..polymer.classification.polymerGeometry").ToString().ToUpper();
                                plmr.plmr_subclass = job.SelectToken("..polymer.classification.polymerSubclass").ToList().Count > 0 ? job.SelectToken("..polymer.classification.polymerSubclass").ToList()[0].ToString().ToUpper() : "";
                                plmr.plmr_class = job.SelectToken("..polymer.classification.polymerClass").ToString().ToUpper();


                                // YP writing out polymers to separate files based on their classification
                                /*
                                if (polymer_geometry.ToUpper() == "LINEAR")
                                {
                                    File.AppendAllText(linear_polymer_file, sdf.ToString());
                                    if (polymer_class.ToUpper() == "HOMOPOLYMER")
                                    {
                                        File.AppendAllText(linear_homopolymer_file, sdf.ToString());
                                        switch (polymer_subclass.ToUpper())
                                        {
                                            case "":
                                                File.AppendAllText(linear_na_homopolymer_file, sdf.ToString());
                                                break;
                                            case "BLOCK":
                                                File.AppendAllText(linear_block_homopolymer_file, sdf.ToString());
                                                break;
                                            case "DOUBLE-STRANDED":
                                                File.AppendAllText(linear_double_stranded_homopolymer_file, sdf.ToString());
                                                break;
                                            case "SUBSTITUTED":
                                                File.AppendAllText(linear_substituted_homopolymer_file, sdf.ToString());
                                                break;
                                        }
                                    }
                                    else if (polymer_class.ToUpper() == "COPOLYMER")
                                    {
                                        File.AppendAllText(linear_copolymer_file, sdf.ToString());
                                        switch (polymer_subclass.ToUpper())
                                        {
                                            case "":
                                                File.AppendAllText(linear_na_copolymer_file, sdf.ToString());
                                                break;
                                            case "BLOCK":
                                                File.AppendAllText(linear_block_copolymer_file, sdf.ToString());
                                                break;
                                            case "DOUBLE-STRANDED":
                                                File.AppendAllText(linear_double_stranded_copolymer_file, sdf.ToString());
                                                break;
                                            case "RANDOM":
                                                File.AppendAllText(linear_random_copolymer_file, sdf.ToString());
                                                break;
                                        }
                                    }
                                }


                                return;
                                */
                                //SplDocument splDoc = new SplDocument(plmr) { DocId = expOpt.DocId ?? Guid.NewGuid(), SetId = new Guid("fa5e21ca-4ba4-42ed-a000-23e5acb8374f"), Version = 1 };
                                //Protein protein = new Protein { Sdf = sdf, UNII = unii };
                                var info = pars.GetSubstanceInfo(unii);
                                SplDocument splDoc = new SplDocument(plmr) { DocId = expOpt.DocId ?? Guid.NewGuid(), SetId = info.SetId, Version = info.VersionNumber };
                                //{ DocId = expOpt.DocId ?? Guid.NewGuid(), SetId = info.SetId, Version = info.VersionNumber }
                                PolymerBaseReadingState state = new PolymerBaseReadingState { RootObject = splDoc };

                                try
                                {
                                    // Primitive validation
                                    
                                    // Validate with CVSP
                                    //YP doesn't seem to be relevant to polymers
                                    //if (opt.Validate)
                                    //    sdf.ValidateWithCvsp(unii);

                                    // Get name and stereo if any
                                    //YP doesn't seem to be relevant to polymers
                                    //subst.Preprocess(sdf.GetDescXml("DESC_PART", SrsDomain.Substance, impOpt));

                                    // Find if this chemical is already in registry and complete SplDocument info
                                    //var info = pars.GetSubstanceInfo(unii, "test linear polymer");
                                    //splDoc.SetId = info.SetId;
                                    //splDoc.Version = info.VersionNumber;

                                    if (plmr.plmr_subclass == "BLOCK")
                                    {
                                        throw new SrsException("invalid_mol", "Currently Block polymers are not handled");
                                    }

                                    // Additional MOL checks
                                    if (sdf.HasSGroup(SGroupType.DAT))
                                        throw new SrsException("invalid_mol", "DAT SGroups are not supported");

                                    Optional<JToken>.ofNullable(job.SelectToken("properties"))
                                    .map(t => t.AsJEnumerable()
                                             .Filter(v => v.SelectToken("name").ToString().Contains("MOL_WEIGHT"))
                                             .First())
                                    .ifPresent(jmw => {
                                        //TODO: This is really due to improper import of old SRS records into
                                        //GSRS. This should remain supported, but be updated in the future to
                                        //use parameters of the property.

                                        string mwname = jmw.SelectToken("name").ToString();

                                        MolecularWeight mw = new MolecularWeight();
                                        mw.Amount = PolymerBaseExtensions.ReadAmountJson(plmr, jmw.SelectToken("value"), state);


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
                                        SRSReadingUtils.readSingleElement(mwcolon, "MOLECULAR_WEIGHT_TYPE", ValidatedValues.MWTypes.Keys.Contains, v => mw.WeightType = v, plmr.UNII);
                                        SRSReadingUtils.readSingleElement(mwparan, "MOLECULAR_WEIGHT_METHOD", ValidatedValues.MWMethods.Keys.Contains, v => mw.WeightMethod = v, plmr.UNII);

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
                                            plmr.MolecularWeight = mw;
                                        }
                                    });

                                    // Moietize - the main processing routine
                                    plmr.RootObject = splDoc;
                                    plmr.Moietize();
                                    // YP maybe this is where the amounts stuff will go


                                    //List<JToken> structural_units = null;
                                    //JToken structural_units = job.SelectToken("..polymer.structuralUnits");
                                    bool valid_amount_formula = false;
                                    bool non_numeric_amount_found = false;
                                    float common_first_number = -999; 
                                    int structural_units_count = 0;
                                    double? srus_mw_total = 0;
                                    Optional<JToken>.ofNullable(job.SelectToken("..polymer.structuralUnits"))
                                        .ifPresent(units => {
                                            structural_units_count = units.Count();
                                            units.AsJEnumerable()
                                            .Map(n =>
                                            {
                                                string sru_label = n.SelectToken("label").ToString();
                                                foreach (Chain subunit in plmr.Subunits)
                                                {
                                                    if (subunit.SRUs != null)
                                                    {
                                                        foreach (SRU sru in subunit.SRUs)
                                                        {
                                                            if (sru.SRULabels.Contains(sru_label))
                                                            {
                                                                SRSReadingUtils.readJsonElement(n, "amount.type", ValidatedValues.AmountTypes.Keys.Contains, v => sru.Amount.SrsAmountType = v, plmr.UNII);
                                                                if (n.SelectToken("..amount..lowLimit") != null || n.SelectToken("..amount..highLimit") != null || n.SelectToken("..amount..average") != null)
                                                                {
                                                                    if (n.SelectToken("..amount..lowLimit") != null)
                                                                    {
                                                                        sru.Amount.AmountType = AmountType.Statistical;
                                                                        sru.Amount.Low = Convert.ToDouble(n.SelectToken("..amount..lowLimit"));

                                                                    }
                                                                    if (n.SelectToken("..amount..highLimit") != null)
                                                                    {
                                                                        sru.Amount.AmountType = AmountType.Statistical;
                                                                        sru.Amount.High = Convert.ToDouble(n.SelectToken("..amount..highLimit"));
                                                                    }
                                                                    if (n.SelectToken("..amount..average") != null)
                                                                    {
                                                                        sru.Amount.AmountType = AmountType.Statistical;
                                                                        sru.Amount.Center = Convert.ToDouble(n.SelectToken("..amount..average"));
                                                                    }
                                                                }
                                                                else if (n.SelectToken("..amount..nonNumericValue") != null)
                                                                {
                                                                    non_numeric_amount_found = true;
                                                                    string amount_formula = Convert.ToString(n.SelectToken("..amount..nonNumericValue"));
                                                                    float first_number;
                                                                    valid_amount_formula = float.TryParse(amount_formula.Replace("[", "").Substring(0, amount_formula.Replace("[", "").IndexOf("-")), out first_number);
                                                                    if (common_first_number !=-999 && first_number != common_first_number)
                                                                    {
                                                                        throw new SrsException("mandatory_field_missing", "Inconsistent value in the non-numeric amount formula");
                                                                        
                                                                    }
                                                                    common_first_number = first_number;
                                                                }
                                                                else if (n.SelectToken("..amount") != null)
                                                                {
                                                                    throw new SrsException("mandatory_field_missing", "SRU Amount is present but specified incorrectly. Expecting lowLimit, highLimit, average or nonNumericValue.");
                                                                }
                                                                
                                                                //YP SRS-400 calculate degree of polymerization amounts for all SRUs


                                                                //sru.Amount.AdjustAmount();
                                                                if (sru.Amount.SrsAmountType == "MOLE PERCENT")
                                                                {
                                                                    if (sru.Amount.Center != null)
                                                                    {
                                                                        srus_mw_total += sru.Amount.Center * sru.MolecularWeight;
                                                                    }
                                                                    if (sru.Amount.Low != null)
                                                                    {
                                                                        srus_mw_total += sru.Amount.Low * sru.MolecularWeight;
                                                                    }
                                                                    if (sru.Amount.High != null)
                                                                    {
                                                                        srus_mw_total += sru.Amount.High * sru.MolecularWeight;
                                                                    }
                                                                }





                                                            }
                                                        }
                                                    }
                                                }
                                    
                                                return sru_label;
                                            });
                                        });

                                    //YP SRS-400
                                    
                                    plmr.polimerization_factor = plmr.MolecularWeight.Amount.Numerator / srus_mw_total;
                                    foreach (Chain chn in plmr.Subunits)
                                    {
                                        foreach (SRU chn_sru in chn.SRUs)
                                        {
                                            if (chn_sru.Amount.SrsAmountType == "MOLE PERCENT")
                                            {
                                                if (chn_sru.Amount.Center != null)
                                                {
                                                    chn_sru.Amount.Center = Math.Round((double)chn_sru.Amount.Center * (double)plmr.polimerization_factor, 0);
                                                }
                                                if (chn_sru.Amount.Low != null)
                                                {
                                                    chn_sru.Amount.Low = Math.Round((double)chn_sru.Amount.Low * (double)plmr.polimerization_factor, 0);
                                                }
                                                if (chn_sru.Amount.High != null)
                                                {
                                                    chn_sru.Amount.High = Math.Round((double)chn_sru.Amount.High * (double)plmr.polimerization_factor, 0);
                                                }
                                            }
                                        }
                                    }


                                    if (non_numeric_amount_found && valid_amount_formula)
                                    {
                                        Optional<JToken>.ofNullable(job.SelectToken("..polymer.monomers"))
                                            .ifPresent(monomers => {
                                                monomers.AsJEnumerable()
                                                .Map(monomer =>
                                                {
                                                    double monomer_amount = Convert.ToDouble(monomer.SelectToken("..amount.average").ToString());
                                                    foreach (Chain subunit in plmr.Subunits)
                                                    {
                                                        foreach (SRU sru in subunit.SRUs)
                                                        {
                                                            sru.Amount.AmountType = AmountType.Statistical;
                                                            sru.Amount.Center = Math.Round((monomer_amount / structural_units_count),1) ;
                                                        }
                                                    }
                                                    return monomer_amount;
                                                });
                                            });
                                    }
                                    else if (non_numeric_amount_found && !valid_amount_formula)
                                    {
                                        throw new SrsException("mandatory_field_missing", "Polymer amount is an uninterpretable non-numeric value");
                                    }

                                    foreach (Chain subunit in plmr.Subunits)
                                    {
                                        foreach (SRU sru in subunit.SRUs)
                                        {

                                        }
                                    }
                                    
                                    
                                    // Create SPL document and write result files
                                    writeResultFiles(opt, expOpt, pars, new List<SdfRecord> { sdf }, splDoc, result);
                                }
                                catch (Exception ex)
                                {
                                    if (ex is FatalException) throw ex;
                                    Interlocked.Increment(ref nErrors);
                                    writeErrorFiles(pars, expOpt, new List<SdfRecord> { sdf }, splDoc, ex, result);
                                }

                                // Update processed records stats
                                Interlocked.Increment(ref nRecords);
                                if (nRecords % 100 == 0)
                                    Console.Out.Write(".");
                                if (nRecords % 1000 == 0)
                                    Console.Out.Write(nRecords);
                            }
                        }
                        catch (Exception ex)
                        {
                            if (ex is FatalException) throw ex;
                            Log.Logger.Error(ex.Message);
                            Log.Logger.Error("Skipping record ... ");
                        }
                    });
            }

            TraceUtils.WriteUNIITrace(TraceEventType.Information, null, null, "Processed file '{0}': {1} errors out of {2} records", impOpt.InputFile, nErrors, nRecords);
            Log.Logger.Information("Result {@Result}", result);
        }
    }
}
