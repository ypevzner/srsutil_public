
//Instructions for adding Indigo and other DLLs to srsutil when building for the first time
//The following steps need to be performed from Visual Studio for the following projects that are part of the solution
//FDA.SRS.ObjectModel, FDA.SRS.Processing, FDA.SRS.Services, FDA.SRS.Utils, SRSTests
//1.Expand the project in Solution Explorer
//2.Expand References under the project
//3.If Indigo.Net is present among references, Right-Click it and select "Remove"
//4.Right-Click on the References and select "Add Reference" (or Right-Click on project name, select "Add" then select "Reference.."). Reference Manager window will pop up for that project
//5.In the Reference Manager window click "Browse..." and navigate to <local path>\srsutil\Source\srsutil\Resources\Indigo_DLL
//6.There select Indigo.Net.dll file
//Repeat for all the projects mentioned above
//7.Copy all files located in the <local path>\<local path>\srsutil\Source\srsutil\Resources\DLL and paste them into Debug and/or Release directory depending on how you're building the solution
//8.Build the solution

using FDA.SRS.Database;
using FDA.SRS.Processing;
using FDA.SRS.Utils;
using Serilog;
using System;
using System.Configuration.Install;
using System.Linq;

namespace SRS
{
	partial class Program
	{
		private static void Usage(int exitCode)
		{
			Console.Out.WriteLine(Resources.Resources.usage);

            Environment.Exit(exitCode);
		}

		static void Main(string[] args)
		{
			InstallContext context = new InstallContext(null, args);
			if ( args.Length == 0 )
				Usage(1);
			Init();

			switch ( (context.Parameters["command"] ?? String.Empty).ToLower() ) {
				// Read SDF file and extract MOLs and XML into a set of directories based on each record's root element
				case "sdf-to-dirs":
					Converter.Sdf2Dirs(
						new ImportOptions {
							InputFile = CmdLineUtils.ParsePathWithEncodingPrefix(context.Parameters["if"]),
							InputFileEncoding = CmdLineUtils.ParseEncodingPrefix(context.Parameters["if"]),
							SdfMapping = CmdLineUtils.ParseMapping(context.Parameters["mapping"]),
							NoAutoFix = context.IsParameterTrue("no-auto-fix"),
						},
						new ConvertOptions {
							GenerateMode = (GenerateMode)Enum.Parse(typeof(GenerateMode), context.Parameters["mode"] ?? "Default", true),
						},
						new ExportOptions {
							OutDir = context.Parameters["odir"] ?? "out"
						});
					break;

				case "sdf-extract-xml":
					Converter.SdfExtractXmlSnippets(
						new ImportOptions {
							InputFile = CmdLineUtils.ParsePathWithEncodingPrefix(context.Parameters["if"]),
							InputFileEncoding = CmdLineUtils.ParseEncodingPrefix(context.Parameters["if"]),
							SdfMapping = CmdLineUtils.ParseMapping(context.Parameters["mapping"]),
							XPath = context.Parameters["xpath"],
							NoAutoFix = context.IsParameterTrue("no-auto-fix"),
						},
						new ConvertOptions {
							Unique = context.IsParameterTrue("unique"),
						},
						new ExportOptions {
							OutputFile = context.Parameters["of"],
						});
					break;
				case "sdf-extract-sdf":
					Converter.SdfExtractSdf(
						new ImportOptions {
							InputFile = CmdLineUtils.ParsePathWithEncodingPrefix(context.Parameters["if"]),
							InputFileEncoding = CmdLineUtils.ParseEncodingPrefix(context.Parameters["if"]),
							XPath = context.Parameters["xpath"],
							SdfMapping = CmdLineUtils.ParseMapping(context.Parameters["mapping"]),
							Uniis = (context.Parameters["unii"] ?? "").Split(new char[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList(),
							NoAutoFix = context.IsParameterTrue("no-auto-fix"),
						},
						new ConvertOptions {
							Filters = CmdLineUtils.ParseFilters((context.Parameters["filters"] ?? "").Split(new char[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList()),
							SGroupTypes = (context.Parameters["sgroup-type"] ?? "ANY")
								.Split(',')
								.Select(s => (SGroupType)Enum.Parse(typeof(SGroupType), s, true))
								.Aggregate((flags, f) => flags |= f)
						},
						new ExportOptions {
							OutDir = context.Parameters["odir"] ?? "out",
							GenerateImage = context.IsParameterTrue("image") || !String.IsNullOrEmpty(context.Parameters["image"]),
							ImageOptions = String.IsNullOrEmpty(context.Parameters["image"]) ? null : CmdLineUtils.ParseImageOptions((context.Parameters["image"] ?? "").Split(new char[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList()),
							MolFile = context.IsParameterTrue("mol"),
							Clean = context.IsParameterTrue("clean"),
							Separate = context.IsParameterTrue("separate"),
						});
					break;
                //canonicalize sdf
                case "sdf-process-sdf":
                    Converter.SdfProcessSdf(
                        new ImportOptions
                        {
                            InputFile = CmdLineUtils.ParsePathWithEncodingPrefix(context.Parameters["if"]),
                            InputFileEncoding = CmdLineUtils.ParseEncodingPrefix(context.Parameters["if"]),
                            XPath = context.Parameters["xpath"],
                            SdfMapping = CmdLineUtils.ParseMapping(context.Parameters["mapping"]),
                            Uniis = (context.Parameters["unii"] ?? "").Split(new char[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList(),
                            NoAutoFix = context.IsParameterTrue("no-auto-fix"),
                        },
                        new ConvertOptions
                        {
                            Filters = CmdLineUtils.ParseFilters((context.Parameters["filters"] ?? "").Split(new char[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList()),
                            SGroupTypes = (context.Parameters["sgroup-type"] ?? "ANY")
                                .Split(',')
                                .Select(s => (SGroupType)Enum.Parse(typeof(SGroupType), s, true))
                                .Aggregate((flags, f) => flags |= f)
                        },
                        new ExportOptions
                        {
                            OutDir = context.Parameters["odir"] ?? "out",
                            GenerateImage = context.IsParameterTrue("image") || !String.IsNullOrEmpty(context.Parameters["image"]),
                            ImageOptions = String.IsNullOrEmpty(context.Parameters["image"]) ? null : CmdLineUtils.ParseImageOptions((context.Parameters["image"] ?? "").Split(new char[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList()),
                            MolFile = context.IsParameterTrue("mol"),
                            Clean = context.IsParameterTrue("clean"),
                            Separate = context.IsParameterTrue("separate"),
                            Canonicalize = context.IsParameterTrue("canonicalize")
                        });
                    break;
                //generate nucleic acid components sdf from JSON
                case "create-na-components":
                    Converter.GenerateNAComponentsMolfile(
                        new ImportOptions
                        {
                            InputFile = CmdLineUtils.ParsePathWithEncodingPrefix(context.Parameters["if"]),
                            InputFileEncoding = CmdLineUtils.ParseEncodingPrefix(context.Parameters["if"]),
                            XPath = context.Parameters["xpath"],
                            SdfMapping = CmdLineUtils.ParseMapping(context.Parameters["mapping"]),
                            Uniis = (context.Parameters["unii"] ?? "").Split(new char[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList(),
                            NoAutoFix = context.IsParameterTrue("no-auto-fix"),
                        },
                        new ConvertOptions
                        {
                            Filters = CmdLineUtils.ParseFilters((context.Parameters["filters"] ?? "").Split(new char[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList()),
                            SGroupTypes = (context.Parameters["sgroup-type"] ?? "ANY")
                                .Split(',')
                                .Select(s => (SGroupType)Enum.Parse(typeof(SGroupType), s, true))
                                .Aggregate((flags, f) => flags |= f)
                        },
                        new NAComponentsExportOptions
                        {
                            ComponentsOutputFile = context.Parameters["components-output-file"] ?? "na_components.sdf",
							DictionaryOutputFile = context.Parameters["dictionary-output-file"] ?? "na_components_dictionary.txt",
                        });
                    break;
                case "sdf-sort-by-xml":
					Converter.SdfSortByXmlKey(
						new ImportOptions {
							InputFile = CmdLineUtils.ParsePathWithEncodingPrefix(context.Parameters["if"]),
							InputFileEncoding = CmdLineUtils.ParseEncodingPrefix(context.Parameters["if"]),
							SdfMapping = CmdLineUtils.ParseMapping(context.Parameters["mapping"]),
							XPath = context.Parameters["xpath"],
							XSort = context.Parameters["xsort"] ?? ".",
							NoAutoFix = context.IsParameterTrue("no-auto-fix"),
						},
						new ExportOptions {
							OutDir = context.Parameters["odir"] ?? "out",
							Clean = context.IsParameterTrue("clean"),
							OutputFile = context.Parameters["of"],
							XPrint = context.Parameters["xprint"] ?? context.Parameters["xsort"] ?? ".",
						});
					break;

				// MOL to SPL
				case "mol2spl":
					Converter.Mol2Spl(new ImportOptions {
						InputFile = CmdLineUtils.ParsePathWithEncodingPrefix(context.Parameters["if"]),
						InputFileEncoding = CmdLineUtils.ParseEncodingPrefix(context.Parameters["if"]),
					},
					new ConvertOptions {
						GenerateMode = (GenerateMode)Enum.Parse(typeof(GenerateMode), context.Parameters["mode"] ?? "Default", true),
						FullDiff = context.IsParameterTrue("full-diff"),
						Validate = context.IsParameterTrue("validate"),
					},
					new ExportOptions {
						OutDir = context.Parameters["odir"],
						UNIIFile = context.IsParameterTrue("UNIIFile"),
						MolFile = context.IsParameterTrue("mol"),
						NoDocIdFile = context.IsParameterTrue("NoDocIdFile"),
						NoErrFile = context.IsParameterTrue("NoErrFile"),
						NoSplErr = context.IsParameterTrue("NoSplErr"),
						DocId = context.Parameters["DocId"] == null ? (Guid?)null : Guid.Parse(context.Parameters["DocId"]),
						SetId = context.Parameters["SetId"] == null ? (Guid?)null : Guid.Parse(context.Parameters["SetId"]),
						Clean = context.IsParameterTrue("clean"),
					});
					break;

				// Substances to SPL
				case "str2spl":
					Converter.Str2Spl(new ImportOptions {
						InputFile = CmdLineUtils.ParsePathWithEncodingPrefix(context.Parameters["if"]),
						InputFileEncoding = CmdLineUtils.ParseEncodingPrefix(context.Parameters["if"]),
						SdfMapping = CmdLineUtils.ParseMapping(context.Parameters["mapping"]),
						Uniis = (context.Parameters["unii"] ?? "").Split(new char[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList(),
						NoAutoFix = context.IsParameterTrue("no-auto-fix"),
					},
					new ConvertOptions {
						GenerateMode = (GenerateMode)Enum.Parse(typeof(GenerateMode), context.Parameters["mode"] ?? "Default", true),
						FullDiff = context.IsParameterTrue("full-diff"),
						Validate = context.IsParameterTrue("validate"),
						CheckRefs = context.IsParameterTrue("check-refs"),
					},
					new ExportOptions {
						OutDir = context.Parameters["odir"],
						UNIIFile = context.IsParameterTrue("UNIIFile"),
						NoDocIdFile = context.IsParameterTrue("NoDocIdFile"),
						NoErrFile = context.IsParameterTrue("NoErrFile"),
						NoSplErr = context.IsParameterTrue("NoSplErr"),
						SdfFile = context.IsParameterTrue("SDFFile"),
						SrsFile = context.IsParameterTrue("SRSFile"),
						DocId = context.Parameters["DocId"] == null ? (Guid?)null : Guid.Parse(context.Parameters["DocId"]),
						SetId = context.Parameters["SetId"] == null ? (Guid?)null : Guid.Parse(context.Parameters["SetId"]),
						Clean = context.IsParameterTrue("clean"),
					});
					break;

				// Mixtures to SPL
				case "mix2spl":
					Converter.Mix2Spl(new ImportOptions {
						InputFile = CmdLineUtils.ParsePathWithEncodingPrefix(context.Parameters["if"]),
						InputFileEncoding = CmdLineUtils.ParseEncodingPrefix(context.Parameters["if"]),
						SdfMapping = CmdLineUtils.ParseMapping(context.Parameters["mapping"]),
						Uniis = (context.Parameters["unii"] ?? "").Split(new char[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList(),
						NoAutoFix = context.IsParameterTrue("no-auto-fix"),
					},
					new ConvertOptions {
						GenerateMode = (GenerateMode)Enum.Parse(typeof(GenerateMode), context.Parameters["mode"] ?? "Default", true),
						FullDiff = context.IsParameterTrue("full-diff"),
						Validate = context.IsParameterTrue("validate"),
						CheckRefs = context.IsParameterTrue("check-refs"),
					},
					new ExportOptions {
						OutDir = context.Parameters["odir"],
						UNIIFile = context.IsParameterTrue("UNIIFile"),
						NoDocIdFile = context.IsParameterTrue("NoDocIdFile"),
						NoErrFile = context.IsParameterTrue("NoErrFile"),
						NoSplErr = context.IsParameterTrue("NoSplErr"),
						SdfFile = context.IsParameterTrue("SDFFile"),
						SrsFile = context.IsParameterTrue("SRSFile"),
						DocId = context.Parameters["DocId"] == null ? (Guid?)null : Guid.Parse(context.Parameters["DocId"]),
						SetId = context.Parameters["SetId"] == null ? (Guid?)null : Guid.Parse(context.Parameters["SetId"]),
						Clean = context.IsParameterTrue("clean"),
					});
					break;
                //mixture spl to spl conversion (hashcode regeneration)
                case "mixspl2spl":
                    Converter.MixSpl2Spl(new ImportOptions
                    {
                        InputFile = CmdLineUtils.ParsePathWithEncodingPrefix(context.Parameters["if"]),
                        InputFileEncoding = CmdLineUtils.ParseEncodingPrefix(context.Parameters["if"]),
                        SdfMapping = CmdLineUtils.ParseMapping(context.Parameters["mapping"]),
                        Uniis = (context.Parameters["unii"] ?? "").Split(new char[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList(),
                        NoAutoFix = context.IsParameterTrue("no-auto-fix"),
                    },
                    new ConvertOptions
                    {
                        GenerateMode = (GenerateMode)Enum.Parse(typeof(GenerateMode), context.Parameters["mode"] ?? "Default", true),
                        FullDiff = context.IsParameterTrue("full-diff"),
                        Validate = context.IsParameterTrue("validate"),
                        CheckRefs = context.IsParameterTrue("check-refs"),
                    },
                    new ExportOptions
                    {
                        OutDir = context.Parameters["odir"],
                        UNIIFile = context.IsParameterTrue("UNIIFile"),
                        NoDocIdFile = context.IsParameterTrue("NoDocIdFile"),
                        NoErrFile = context.IsParameterTrue("NoErrFile"),
                        NoSplErr = context.IsParameterTrue("NoSplErr"),
                        SdfFile = context.IsParameterTrue("SDFFile"),
                        SrsFile = context.IsParameterTrue("SRSFile"),
                        DocId = context.Parameters["DocId"] == null ? (Guid?)null : Guid.Parse(context.Parameters["DocId"]),
                        SetId = context.Parameters["SetId"] == null ? (Guid?)null : Guid.Parse(context.Parameters["SetId"]),
                        Clean = context.IsParameterTrue("clean"),
                    });
                    break;
                // Structurally diverse to SPL
                case "sd2spl":
					Converter.SD2Spl(new ImportOptions {
						InputFile = CmdLineUtils.ParsePathWithEncodingPrefix(context.Parameters["if"]),
						InputFileEncoding = CmdLineUtils.ParseEncodingPrefix(context.Parameters["if"]),
						SdfMapping = CmdLineUtils.ParseMapping(context.Parameters["mapping"]),
						Uniis = (context.Parameters["unii"] ?? "").Split(new char[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList(),
						TermsFiles = (context.Parameters["TermsFiles"] ?? "").Split(new char[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList(),
						NoAutoFix = context.IsParameterTrue("no-auto-fix"),
					},
					new ConvertOptions {
						GenerateMode = (GenerateMode)Enum.Parse(typeof(GenerateMode), context.Parameters["mode"] ?? "Default", true),
						Strict = context.IsParameterTrue("strict"),
						Validate = context.IsParameterTrue("validate"),
						Unique = context.IsParameterTrue("unique"),
						CheckRefs = context.IsParameterTrue("check-refs"),
					},
					new ExportOptions {
						OutDir = context.Parameters["odir"],
						UNIIFile = context.IsParameterTrue("UNIIFile"),
						NoDocIdFile = context.IsParameterTrue("NoDocIdFile"),
						NoErrFile = context.IsParameterTrue("NoErrFile"),
						NoSplErr = context.IsParameterTrue("NoSplErr"),
						SdfFile = context.IsParameterTrue("SDFFile"),
						SrsFile = context.IsParameterTrue("SRSFile"),
						DocId = context.Parameters["DocId"] == null ? (Guid?)null : Guid.Parse(context.Parameters["DocId"]),
						SetId = context.Parameters["SetId"] == null ? (Guid?)null : Guid.Parse(context.Parameters["SetId"]),
						Clean = context.IsParameterTrue("clean"),
						Features = SplFeature.FromList((context.Parameters["exp-features"] ?? "").Split(new char[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim())),
					});
					break;

				// Proteins to SPL
				case "prot2spl":
					Converter.Prot2Spl(new ImportOptions {
						InputFile = CmdLineUtils.ParsePathWithEncodingPrefix(context.Parameters["if"]),
						InputFileEncoding = CmdLineUtils.ParseEncodingPrefix(context.Parameters["if"]),
						SdfMapping = CmdLineUtils.ParseMapping(context.Parameters["mapping"]),
						Uniis = (context.Parameters["unii"] ?? "").Split(new char[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList(),
						NoAutoFix = context.IsParameterTrue("no-auto-fix"),
						ProteinType = (context.Parameters["protein-filter"] ?? "Any")
							.Split(',')
							.Select(s => (ProteinFeatures)Enum.Parse(typeof(ProteinFeatures), s, true))
							.Aggregate((flags, f) => flags |= f),
						Features = SplFeature.FromList((context.Parameters["imp-features"] ?? "").Split(new char[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim())),
					},
					new ConvertOptions {
						GenerateMode = (GenerateMode)Enum.Parse(typeof(GenerateMode), context.Parameters["mode"] ?? "Default", true),
						Strict = context.IsParameterTrue("strict"),
						Validate = context.IsParameterTrue("validate"),
						Unique = context.IsParameterTrue("unique"),
						CheckRefs = context.IsParameterTrue("check-refs"),
						Features = SplFeature.FromList((context.Parameters["cvt-features"] ?? "").Split(new char[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim())),
                        Coord = context.IsParameterTrue("2D"),
                    },
					new ExportOptions {
						OutDir = context.Parameters["odir"],
						UNIIFile = context.IsParameterTrue("UNIIFile"),
						NoDocIdFile = context.IsParameterTrue("NoDocIdFile"),
						NoErrFile = context.IsParameterTrue("NoErrFile"),
						NoSplErr = context.IsParameterTrue("NoSplErr"),
						SdfFile = context.IsParameterTrue("SDFFile"),
						SrsFile = context.IsParameterTrue("SRSFile"),
						DocId = context.Parameters["DocId"] == null ? (Guid?)null : Guid.Parse(context.Parameters["DocId"]),
						SetId = context.Parameters["SetId"] == null ? (Guid?)null : Guid.Parse(context.Parameters["SetId"]),
						Clean = context.IsParameterTrue("clean"),
						Features = SplFeature.FromList((context.Parameters["exp-features"] ?? "").Split(new char[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim())),
					});
					break;
                // Mixtures to SPL
                case "polymer2spl":
                    Converter.Polymer2Spl(new ImportOptions
                    {
                        InputFile = CmdLineUtils.ParsePathWithEncodingPrefix(context.Parameters["if"]),
                        InputFileEncoding = CmdLineUtils.ParseEncodingPrefix(context.Parameters["if"]),
                        SdfMapping = CmdLineUtils.ParseMapping(context.Parameters["mapping"]),
                        Uniis = (context.Parameters["unii"] ?? "").Split(new char[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList(),
                        NoAutoFix = context.IsParameterTrue("no-auto-fix"),
                    },
                    new ConvertOptions
                    {
                        GenerateMode = (GenerateMode)Enum.Parse(typeof(GenerateMode), context.Parameters["mode"] ?? "Default", true),
                        FullDiff = context.IsParameterTrue("full-diff"),
                        Validate = context.IsParameterTrue("validate"),
                        CheckRefs = context.IsParameterTrue("check-refs"),
                    },
                    new ExportOptions
                    {
                        OutDir = context.Parameters["odir"],
                        UNIIFile = context.IsParameterTrue("UNIIFile"),
                        NoDocIdFile = context.IsParameterTrue("NoDocIdFile"),
                        NoErrFile = context.IsParameterTrue("NoErrFile"),
                        NoSplErr = context.IsParameterTrue("NoSplErr"),
                        SdfFile = context.IsParameterTrue("SDFFile"),
                        SrsFile = context.IsParameterTrue("SRSFile"),
                        DocId = context.Parameters["DocId"] == null ? (Guid?)null : Guid.Parse(context.Parameters["DocId"]),
                        SetId = context.Parameters["SetId"] == null ? (Guid?)null : Guid.Parse(context.Parameters["SetId"]),
                        Clean = context.IsParameterTrue("clean"),
                    });
                    break;
				//Protein-PolymerConjugates
				case "protplmr2spl":
					Converter.ProtPlmr2Spl(new ImportOptions
					{
						InputFileProt = CmdLineUtils.ParsePathWithEncodingPrefix(context.Parameters["ifprot"]),
						InputFileProtEncoding = CmdLineUtils.ParseEncodingPrefix(context.Parameters["ifprot"]),
						InputFilePlmr = CmdLineUtils.ParsePathWithEncodingPrefix(context.Parameters["ifplmr"]),
						InputFilePlmrEncoding = CmdLineUtils.ParseEncodingPrefix(context.Parameters["ifplmr"]),
						SdfMapping = CmdLineUtils.ParseMapping(context.Parameters["mapping"]),
						Uniis = (context.Parameters["unii"] ?? "").Split(new char[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList(),
						NoAutoFix = context.IsParameterTrue("no-auto-fix"),
					},
					new ConvertOptions
					{
						GenerateMode = (GenerateMode)Enum.Parse(typeof(GenerateMode), context.Parameters["mode"] ?? "Default", true),
						FullDiff = context.IsParameterTrue("full-diff"),
						Validate = context.IsParameterTrue("validate"),
						CheckRefs = context.IsParameterTrue("check-refs"),
					},
					new ExportOptions
					{
						OutDir = context.Parameters["odir"],
						UNIIFile = context.IsParameterTrue("UNIIFile"),
						NoDocIdFile = context.IsParameterTrue("NoDocIdFile"),
						NoErrFile = context.IsParameterTrue("NoErrFile"),
						NoSplErr = context.IsParameterTrue("NoSplErr"),
						SdfFile = context.IsParameterTrue("SDFFile"),
						SrsFile = context.IsParameterTrue("SRSFile"),
						DocId = context.Parameters["DocId"] == null ? (Guid?)null : Guid.Parse(context.Parameters["DocId"]),
						SetId = context.Parameters["SetId"] == null ? (Guid?)null : Guid.Parse(context.Parameters["SetId"]),
						Clean = context.IsParameterTrue("clean"),
					});
					break;
				case "protspl2spl":
                    Converter.ProtSpl2Spl(new ImportOptions
                    {
                        InputFile = CmdLineUtils.ParsePathWithEncodingPrefix(context.Parameters["if"]),
                        InputFileEncoding = CmdLineUtils.ParseEncodingPrefix(context.Parameters["if"]),
                        SdfMapping = CmdLineUtils.ParseMapping(context.Parameters["mapping"]),
                        Uniis = (context.Parameters["unii"] ?? "").Split(new char[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList(),
                        NoAutoFix = context.IsParameterTrue("no-auto-fix"),
                        ProteinType = (context.Parameters["protein-filter"] ?? "Any")
                            .Split(',')
                            .Select(s => (ProteinFeatures)Enum.Parse(typeof(ProteinFeatures), s, true))
                            .Aggregate((flags, f) => flags |= f),
                        Features = SplFeature.FromList((context.Parameters["imp-features"] ?? "").Split(new char[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim())),
                    },
                    new ConvertOptions
                    {
                        GenerateMode = (GenerateMode)Enum.Parse(typeof(GenerateMode), context.Parameters["mode"] ?? "Default", true),
                        Strict = context.IsParameterTrue("strict"),
                        Validate = context.IsParameterTrue("validate"),
                        Unique = context.IsParameterTrue("unique"),
                        CheckRefs = context.IsParameterTrue("check-refs"),
                        Features = SplFeature.FromList((context.Parameters["cvt-features"] ?? "").Split(new char[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim())),
                        Coord = context.IsParameterTrue("2D"),
                    },
                    new ExportOptions
                    {
                        OutDir = context.Parameters["odir"],
                        UNIIFile = context.IsParameterTrue("UNIIFile"),
                        NoDocIdFile = context.IsParameterTrue("NoDocIdFile"),
                        NoErrFile = context.IsParameterTrue("NoErrFile"),
                        NoSplErr = context.IsParameterTrue("NoSplErr"),
                        SdfFile = context.IsParameterTrue("SDFFile"),
                        SrsFile = context.IsParameterTrue("SRSFile"),
                        DocId = context.Parameters["DocId"] == null ? (Guid?)null : Guid.Parse(context.Parameters["DocId"]),
                        SetId = context.Parameters["SetId"] == null ? (Guid?)null : Guid.Parse(context.Parameters["SetId"]),
                        Clean = context.IsParameterTrue("clean"),
                        Features = SplFeature.FromList((context.Parameters["exp-features"] ?? "").Split(new char[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim())),
                    });
                    break;
                case "na2spl":
                    Converter.Na2Spl(new ImportOptions
                    {
                        InputFile = CmdLineUtils.ParsePathWithEncodingPrefix(context.Parameters["if"]),
                        InputFileEncoding = CmdLineUtils.ParseEncodingPrefix(context.Parameters["if"]),
                        SdfMapping = CmdLineUtils.ParseMapping(context.Parameters["mapping"]),
                        Uniis = (context.Parameters["unii"] ?? "").Split(new char[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList(),
                        NoAutoFix = context.IsParameterTrue("no-auto-fix"),
                        ProteinType = (context.Parameters["protein-filter"] ?? "Any")
                            .Split(',')
                            .Select(s => (ProteinFeatures)Enum.Parse(typeof(ProteinFeatures), s, true))
                            .Aggregate((flags, f) => flags |= f),
                        Features = SplFeature.FromList((context.Parameters["imp-features"] ?? "").Split(new char[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim())),
                    },
                    new ConvertOptions
                    {
                        GenerateMode = (GenerateMode)Enum.Parse(typeof(GenerateMode), context.Parameters["mode"] ?? "Default", true),
                        Strict = context.IsParameterTrue("strict"),
                        Validate = context.IsParameterTrue("validate"),
                        Unique = context.IsParameterTrue("unique"),
                        CheckRefs = context.IsParameterTrue("check-refs"),
                        Features = SplFeature.FromList((context.Parameters["cvt-features"] ?? "").Split(new char[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim())),
                        Coord = context.IsParameterTrue("2D"),
                        //Coord = true,
                    },
                    new ExportOptions
                    {
                        OutDir = context.Parameters["odir"],
                        UNIIFile = context.IsParameterTrue("UNIIFile"),
                        NoDocIdFile = context.IsParameterTrue("NoDocIdFile"),
                        NoErrFile = context.IsParameterTrue("NoErrFile"),
                        NoSplErr = context.IsParameterTrue("NoSplErr"),
                        SdfFile = context.IsParameterTrue("SDFFile"),
                        SrsFile = context.IsParameterTrue("SRSFile"),
                        DocId = context.Parameters["DocId"] == null ? (Guid?)null : Guid.Parse(context.Parameters["DocId"]),
                        SetId = context.Parameters["SetId"] == null ? (Guid?)null : Guid.Parse(context.Parameters["SetId"]),
                        Clean = context.IsParameterTrue("clean"),
                        Features = SplFeature.FromList((context.Parameters["exp-features"] ?? "").Split(new char[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim())),
                    });
                    break;
                //Structurally Diverse SPL to SPL, regenerating hashcode if original SPL was changed
                case "sdspl2spl":
                    Converter.SDSpl2Spl(new ImportOptions
                    {
                        InputFile = CmdLineUtils.ParsePathWithEncodingPrefix(context.Parameters["if"]),
                        InputFileEncoding = CmdLineUtils.ParseEncodingPrefix(context.Parameters["if"]),
                        SdfMapping = CmdLineUtils.ParseMapping(context.Parameters["mapping"]),
                        Uniis = (context.Parameters["unii"] ?? "").Split(new char[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList(),
                        NoAutoFix = context.IsParameterTrue("no-auto-fix"),
                    },
                    new ConvertOptions
                    {
                        GenerateMode = (GenerateMode)Enum.Parse(typeof(GenerateMode), context.Parameters["mode"] ?? "Default", true),
                        FullDiff = context.IsParameterTrue("full-diff"),
                        Validate = context.IsParameterTrue("validate"),
                        CheckRefs = context.IsParameterTrue("check-refs"),
                    },
                    new ExportOptions
                    {
                        OutDir = context.Parameters["odir"],
                        UNIIFile = context.IsParameterTrue("UNIIFile"),
                        NoDocIdFile = context.IsParameterTrue("NoDocIdFile"),
                        NoErrFile = context.IsParameterTrue("NoErrFile"),
                        NoSplErr = context.IsParameterTrue("NoSplErr"),
                        SdfFile = context.IsParameterTrue("SDFFile"),
                        SrsFile = context.IsParameterTrue("SRSFile"),
                        DocId = context.Parameters["DocId"] == null ? (Guid?)null : Guid.Parse(context.Parameters["DocId"]),
                        SetId = context.Parameters["SetId"] == null ? (Guid?)null : Guid.Parse(context.Parameters["SetId"]),
                        Clean = context.IsParameterTrue("clean"),
                    });
                    break;

                // Convert NCBI, ITIS, USDA databases into large dictionary files
                case "ncbi2terms":
					TaxonomyUtils.Ncbi2Terms(new ImportOptions {
						InputFile = CmdLineUtils.ParsePathWithEncodingPrefix(context.Parameters["if"]),
						InputFileEncoding = CmdLineUtils.ParseEncodingPrefix(context.Parameters["if"]),
					},
					new ExportOptions {
						OutputFile = CmdLineUtils.ParsePathWithEncodingPrefix(context.Parameters["of"]),
						OutputFileEncoding = CmdLineUtils.ParseEncodingPrefix(context.Parameters["of"]),
					},
					new TaxonomyOptions {
						SimplifiedAuthorityReference = context.IsParameterTrue("simplified")
					});
					break;
				case "itis2terms":
					TaxonomyUtils.Itis2Terms(new ImportOptions {
						InputFile = CmdLineUtils.ParsePathWithEncodingPrefix(context.Parameters["if"]),
						InputFileEncoding = CmdLineUtils.ParseEncodingPrefix(context.Parameters["if"]),
					},
					new ExportOptions {
						OutputFile = CmdLineUtils.ParsePathWithEncodingPrefix(context.Parameters["of"]),
						OutputFileEncoding = CmdLineUtils.ParseEncodingPrefix(context.Parameters["of"]),
					},
					new TaxonomyOptions {
						SimplifiedAuthorityReference = context.IsParameterTrue("simplified")
					});
					break;
				case "usda2terms":
					TaxonomyUtils.Usda2Terms(new ImportOptions {
						InputFile = CmdLineUtils.ParsePathWithEncodingPrefix(context.Parameters["if"]),
						InputFileEncoding = CmdLineUtils.ParseEncodingPrefix(context.Parameters["if"]),
					},
					new ExportOptions {
						OutputFile = CmdLineUtils.ParsePathWithEncodingPrefix(context.Parameters["of"]),
						OutputFileEncoding = CmdLineUtils.ParseEncodingPrefix(context.Parameters["of"]),
					},
					new TaxonomyOptions {
						SimplifiedAuthorityReference = context.IsParameterTrue("simplified")
					});
					break;
				case "kew2terms":
					TaxonomyUtils.Kew2Terms(new ImportOptions {
						InputFile = CmdLineUtils.ParsePathWithEncodingPrefix(context.Parameters["if"]),
						InputFileEncoding = CmdLineUtils.ParseEncodingPrefix(context.Parameters["if"]),
					},
					new ExportOptions {
						OutputFile = CmdLineUtils.ParsePathWithEncodingPrefix(context.Parameters["of"]),
						OutputFileEncoding = CmdLineUtils.ParseEncodingPrefix(context.Parameters["of"]),
					},
					new TaxonomyOptions {
						SimplifiedAuthorityReference = context.IsParameterTrue("simplified")
					});
					break;
				case "merge-terms":
					TaxonomyUtils.MergeTerms(new ImportOptions {
						TermsFiles = (context.Parameters["TermsFiles"] ?? "").Split(new char[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList(),
					},
					new ExportOptions {
						OutputFile = CmdLineUtils.ParsePathWithEncodingPrefix(context.Parameters["of"]),
						OutputFileEncoding = CmdLineUtils.ParseEncodingPrefix(context.Parameters["of"]),
					});
					break;
				case "update-terms":
                    String failOnMissing=context.Parameters["fail-on-missing"] ?? "true";
                    bool failMissing = true;
                    if (failOnMissing.ToLower().Equals("false")) {
                        failMissing = false;
                    }

                    try {
                            TaxonomyUtils.UpdateTerms(new ExportOptions {
						    OutputFile = CmdLineUtils.ParsePathWithEncodingPrefix(context.Parameters["of"]),
						    OutputFileEncoding = CmdLineUtils.ParseEncodingPrefix(context.Parameters["of"]),
					    },
                            new TaxonomyOptions {
                                SimplifiedAuthorityReference = context.IsParameterTrue("simplified"),
                                FailOnMissing = failMissing
                            });
                    }catch(Exception ex) {
                       Log.Logger.Error("Unable to process Taxonomy datasource", ex);
                    }
					break;

				// Tools commands
				case "unzip-dailymed":
					SplUtils.UnzipDailymed(new ImportOptions {
						InputFile = context.Parameters["if"],
					},
					new ExportOptions {
						OutDir = context.Parameters["odir"] ?? "out",
					});
					break;

				case "index-spl":
					SplUtils.IndexSpl(new ImportOptions {
						InputFile = context.Parameters["if"],
						Name = context.Parameters["name"]
					},
					new ExportOptions {
						OutputFile = context.Parameters["of"],
					});
					break;
				case "export-spl":
					SplUtils.ExportSpl(new ImportOptions {
						InputFile = context.Parameters["if"],
						Name = context.Parameters["name"]
					},
					new ExportOptions {
						OutDir = context.Parameters["odir"] ?? "out",
						ExpType = (context.Parameters["exp-type"] ?? "Ok")
							.Split(',')
							.Select(s => (ExportOptions.ExportType)Enum.Parse(typeof(ExportOptions.ExportType), s, true))
							.Aggregate((flags, f) => flags |= f)
					});
					break;

				// Database commands
				case "load-spl":
					Importer.ImportDailymedSpl(new ImportOptions {
						InputFile = context.Parameters["if"],
						Name = context.Parameters["name"]
					});
					break;
				case "load-srs":
					Importer.ImportSrsSdf(new ImportOptions {
						InputFile = context.Parameters["if"],
						Name = context.Parameters["name"],
						NoAutoFix = context.IsParameterTrue("no-auto-fix"),
					});
					break;

                case "report-add-inchi-key":
                    EListReportTranslator.Translate(context.Parameters["if"], context.Parameters["of"]);
                    break;

				// TODO: figure out if we need it
				// Old stuff
				case "import-xml":
					Importer.import_xml(context.Parameters["if"], context.Parameters["oc"]);
					break;
				case "import-sdf":
					Importer.import_sdf(context.Parameters["if"], (Importer.CollisionsHandling)Enum.Parse(typeof(Importer.CollisionsHandling), context.Parameters["collisions"] ?? "All", true),
						CmdLineUtils.ParseMapping(context.Parameters["mapping"]));
					break;
				case "export-sdf":
					Exporter.export_sdf(context.Parameters["of"], CmdLineUtils.ParseSet(context.Parameters["substance_id"]), CmdLineUtils.ParseSet(context.Parameters["sdf_id"]));
					break;
				case "export-xml":
					Exporter.export_xml(context.Parameters["of"], (Exporter.ExportWhat)Enum.Parse(typeof(Exporter.ExportWhat), context.Parameters["export"] ?? "All", true),
						CmdLineUtils.ParseSet(context.Parameters["substance_id"]), context.Parameters["file"]);
					break;
				case "export-images":
					Exporter.export_images(CmdLineUtils.ParseSet(context.Parameters["xml_id"]), context.Parameters["prefix"] ?? "rec",
						int.Parse(context.Parameters["width"] ?? "200"), int.Parse(context.Parameters["height"] ?? "200"));
					break;

				default:
					Usage(1);
					break;
			}

		}

		private static void Init()
		{
			Log.Logger = new LoggerConfiguration()
				.WriteTo.LiterateConsole(Serilog.Events.LogEventLevel.Debug)
				.WriteTo.RollingFile("SRS-{Date}.log", Serilog.Events.LogEventLevel.Verbose, "{Timestamp:HH:mm:ss.fff} [{Level}] {Message}{NewLine}{Exception}")
				.CreateLogger();
		}
	}
}
