//using FDA.SRS.Molecules;
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
using System.Text.RegularExpressions;

namespace FDA.SRS.Processing
{
	public static partial class Converter
	{
		public static List<Tuple<OutputFileType, string>> Str2Spl(ImportOptions impOpt, ConvertOptions opt, ExportOptions expOpt)
		{
			List<Tuple<OutputFileType, string>> result = new List<Tuple<OutputFileType, string>>();

			SplOptions.ImportOptions = impOpt;
			SplOptions.ConvertOptions = opt;
			SplOptions.ExportOptions = expOpt;
			OperationalParameters pars = impOpt.PrepareConversion(expOpt);

			foreach ( string file in pars.InputFiles )
				Str2Spl(file, impOpt, opt, expOpt, pars, result);

			return result;
		}

		private static void Str2Spl(string file, ImportOptions impOpt, ConvertOptions opt, ExportOptions expOpt, OperationalParameters pars, List<Tuple<OutputFileType, string>> result)
		{
			Log.Logger.Information("Converting {File} using {@ImportOptions}, {@ConvertOptions}, {@ExportOptions}, {@OperationalParameters}", file, impOpt, opt, expOpt, pars);
			TraceUtils.WriteUNIITrace(TraceEventType.Information, null, null, "Processing file {0} with output into directory {1}...", file, expOpt.OutDir);



			int nErrors = 0, nRecords = 0;
            Func<SdfRecord, int, string, string>  conv=SrsSdfValidators.SubstanceSdfLineFilter;

            using ( SdfReader r = new SdfReader(file, impOpt.InputFileEncoding) { FieldsMap = impOpt.SdfMapping, LineFilters = new[] { conv} } ) {
				r.Records
#if !DEBUG
					.AsParallel()
#endif
					.ForAll(sdf => {
                        try
                        {
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

                                Substance subst = new Substance { UNII = unii, Sdf = sdf };
                                SplDocument splDoc = new SplDocument(subst) { DocId = expOpt.DocId ?? Guid.NewGuid() };

                                try
                                {
                                    string comment = sdf.GetFieldValue("COMMENTS");
                                    bool is_representative_structure = false;
                                    Regex re_repr_struct = new Regex("Representative", RegexOptions.IgnoreCase);
                                    if (!String.IsNullOrEmpty(comment))
                                    {
                                        if (re_repr_struct.IsMatch(comment))
                                        {
                                            is_representative_structure = true;
                                        }
                                    }
                                    // Primitive validation
                                    sdf.ValidateSrsSdf(impOpt);

                                    // Validate with CVSP
                                    if (opt.Validate)
                                        sdf.ValidateWithCvsp(unii);

                                    // Get name and stereo if any
                                    subst.Preprocess(sdf.GetDescXml("DESC_PART", SrsDomain.Substance, impOpt));

                                    // Find if this chemical is already in registry and complete SplDocument info
                                    var info = pars.GetSubstanceInfo(unii, subst.PrimaryName);
                                    splDoc.SetId = info.SetId;
                                    splDoc.Version = info.VersionNumber;

                                    // Additional MOL checks
                                    //GALOG
                                    //if (sdf.GetType().Equals(SGroupType.DAT))
                                        if (sdf.HasSGroup(SGroupType.DAT))
                                        throw new SrsException("invalid_mol", "DAT SGroups are not supported");

                                    // Moietize - the main processing routine
                                    

                                    //YP setting representative structure to true so that this can be reflected in the moiety's amount
                                    if (is_representative_structure)
                                    {
                                        //create parent mixture                                        
                                        subst.Moieties = new List<Moiety>
                                        {
                                            new Moiety() {
                                                MoietyUNII = unii,
                                                RepresentativeStructure =true                                                
                                            }
                                        };
                                        subst.MoietizeRepresentative();
                                        /*foreach (Moiety mty in subst.Moieties)
                                        {
                                            mty.RepresentativeStructure = true;
                                        }*/
                                    } else
                                    {
                                        //treat as usual
                                        subst.Moietize();
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
                        }catch(Exception ex){
                            if (ex is FatalException) throw ex;
                            Log.Logger.Error(ex.Message);
                            Log.Logger.Error("Skipping record ... ");
                        }
					});
			}

			TraceUtils.WriteUNIITrace(TraceEventType.Information, null, null, "Processed file '{0}': {1} errors out of {2} records", impOpt.InputFile, nErrors, nRecords);
			Log.Logger.Information("Result {@Result}", result);
		}

		public static List<Tuple<OutputFileType, string>> Mol2Spl(ImportOptions impOpt, ConvertOptions opt, ExportOptions expOpt)
		{
			List<Tuple<OutputFileType, string>> result = new List<Tuple<OutputFileType, string>>();
			OperationalParameters pars = impOpt.PrepareConversion(expOpt);

			Log.Logger.Information("Converting {File} using {@ImportOptions}, {@ConvertOptions}, {@ExportOptions}, {@OperationalParameters}", impOpt.InputFile, impOpt, opt, expOpt, pars);
			TraceUtils.WriteUNIITrace(TraceEventType.Information, null, null, "Processing file {0} with output into directory {1}...", impOpt.InputFile, expOpt.OutDir);

			using ( SdfReader r = new SdfReader(impOpt.InputFile, impOpt.InputFileEncoding) { FieldsMap = impOpt.SdfMapping, LineFilters = new[] { SrsSdfValidators.SubstanceSdfLineFilter } } ) {
				SdfRecord sdf = r.Records.First();	// This is MOL file - there is only one record

				string unii = Path.GetFileNameWithoutExtension(impOpt.InputFile);
				sdf.AddField("UNII", unii);

				Substance subst = new Substance { UNII = unii, Sdf = sdf };
				SplDocument splDoc = new SplDocument(subst) { DocId = expOpt.DocId ?? Guid.NewGuid() };

				try {
					// Validate with CVSP
					if ( opt.Validate )
						sdf.ValidateWithCvsp(unii);

					// Find if this chemical is already in registry and complete SplDocument info
					var info = pars.GetSubstanceInfo(unii, subst.PrimaryName);
					splDoc.SetId = info.SetId;
					splDoc.Version = info.VersionNumber;

                    // Additional MOL checks
                    //GALOG
                    //if (sdf.GetType().Equals(SGroupType.DAT))
                        if ( sdf.HasSGroup(SGroupType.DAT) )
						throw new SrsException("invalid_mol", "DAT SGroups are not supported");

					// Moietize - the main processing routine
					subst.Moietize();

					// Create SPL document and write result files
					writeResultFiles(opt, expOpt, pars, new List<SdfRecord> { sdf }, splDoc, result);
				}
				catch ( Exception ex ) {
                    if (ex is FatalException)throw ex;
                    writeErrorFiles(pars, expOpt, new List<SdfRecord> { sdf }, splDoc, ex, result);
				}
			}

			TraceUtils.WriteUNIITrace(TraceEventType.Information, null, null, "Processed file '{0}'", impOpt.InputFile);
			Log.Logger.Information("Result {@Result}", result);

			return result;
		}

		private static void ValidateWithCvsp(this SdfRecord sdf, string unii)
		{
			string acidRules = Path.GetTempFileName() + ".xml";
			string validationRules = Path.GetTempFileName() + ".xml";
			try {
				File.WriteAllText(acidRules, Resources.acidgroups);
				File.WriteAllText(validationRules, Resources.ValidationRules);
				CvspValidation.Validate(sdf, unii, validationRules, acidRules);
			}
			finally {
				File.Delete(acidRules);
				File.Delete(validationRules);
			}
		}
	}
}
