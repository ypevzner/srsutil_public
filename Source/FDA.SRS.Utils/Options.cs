using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FDA.SRS.Utils
{
	public enum GenerateMode
	{
		/// <summary>
		/// Always generate a new SPL
		/// </summary>
		Default,

		/// <summary>
		/// Only generate SPL for new UNII
		/// </summary>
		NewUnii,

		/// <summary>
		/// Only generate SPL for new Hash
		/// </summary>
		NewHash,

		/// <summary>
		/// Do not fail on missing UNII or other otherwise mandatory fields
		/// </summary>
		NewSubstance,
	}

	[Flags]
	public enum SGroupType
	{
		NON = 0x00,
		GEN = 0x01,
		SUP = 0x02,
		MUL = 0x04,
		DAT = 0x08,
		SRU = 0x10,
		ANY = 0xFF
	}

	[Flags]
	public enum ProteinFeatures
	{
		Empty			= 0x00,
		Basic			= 0x01,
		Glycosilated	= 0x02,
		Linked			= 0x04,
		Modified		= 0x08,
		All				= 0x0F,
		Any				= 0xFF,
	}

    public enum NAFeatures
    {
        Empty = 0x00,
        Basic = 0x01,
        Glycosilated = 0x02,
        Linked = 0x04,
        Modified = 0x08,
        All = 0x0F,
        Any = 0xFF,
    }

    /// <summary>
    /// SPL features, some may be experimental
    /// </summary>
    public class SplFeature
	{
		private static SplFeature[] _features = new [] {
			new SplFeature { Name = "include-protein-mass", Description = "Include protein mass into SPL" },
			new SplFeature { Name = "clear-srs-xml-ns", Description = "Clear all namespaces from SRS XML elements" },
			new SplFeature { Name = "ignore-empty-polymer", Description = "Ignore presence of an empty POLYMER descriptor in PROTEIN rather than throw an exception" },
			new SplFeature { Name = "allow-lossy-v3000-v2000", Description = "Allow information-loss v2000 -> v3000 conversion (e.g. when modification is an epimer)" },
			new SplFeature { Name = "separate-sequence-definition", Description = "Place sequences definition in SPL separately from subunits" },
			new SplFeature { Name = "no-id-extension-root", Description = "Do not include additional <id extension='...' root='...'> into SPL" },
			new SplFeature { Name = "debug-ignore-description", Description = "Skip description parsing and pretend it's missing" },
		};
		public string Name { get; set; }
		public string Description { get; set; }

		public static IEnumerable<SplFeature> FromList(params string[] features)
		{
			return FromList(features.ToList());
		}

		public static IEnumerable<SplFeature> FromList(IEnumerable<string> features)
		{
			List<SplFeature> fs = new List<SplFeature>();
			foreach ( var s in features ) {
				SplFeature f = _features.Find(s);
				if ( f != null )
					fs.Add(f);
				else
					throw new SrsException("configuration", String.Format("Unknown feature {0}", s));
			}
			return fs;
		}
	};

	public static class SplFeatureExtensions
	{
		public static bool Has(this IEnumerable<SplFeature> features, string feature)
		{
			return features.Any(f => f.Name == feature);
		}

		public static SplFeature Find(this IEnumerable<SplFeature> features, string feature)
		{
			return features.Where(f => f.Name == feature).FirstOrDefault();
		}
	}

	public class Options
	{
		public IEnumerable<SplFeature> Features { get; set; } = new List<SplFeature>();
	}

	public class ImportOptions : Options
	{
		public string Name { get; set; }

		public string InputFile { get; set; }

		public Encoding InputFileEncoding { get; set; }

		public string InputFileProt { get; set; }
		public Encoding InputFileProtEncoding { get; set; }
		public string InputFilePlmr { get; set; }
		public Encoding InputFilePlmrEncoding { get; set; }

		public IDictionary<string, string> SdfMapping { get; set; } = new Dictionary<string, string>();

		public IList<string> TermsFiles { get; set; } = new List<string>();

		public string XPath { get; set; }
		public string XSort { get; set; }

		/// <summary>
		/// Use this UNII where necessary. In case of SDF file conversion only record with this UNII will be converted and other will just be skipped.
		/// </summary>
		public IEnumerable<string> Uniis { get; set; } = new List<string>();

		public bool NoAutoFix { get; set; } = false;

		public ProteinFeatures ProteinType { get; set; } = ProteinFeatures.Any;

        public string ProteinSourceType { get; set; } = "JSON";

        public string NASourceType { get; set; } = "JSON";

        public NAFeatures NAType { get; set; } = NAFeatures.Any;

    }

	public class ConvertOptions : Options
	{
		public GenerateMode GenerateMode { get; set; } = GenerateMode.Default;

		public enum FltPred { Exists, NotExists, Equals, NotEquals, Like, NotLike }

		/// <summary>
		/// Normally is associated with a logic of not producing duplicates
		/// </summary>
		public bool Unique { get; set; }

		public bool FullDiff { get; set; }

		/// <summary>
		/// Check generated SPL against reference database of SPL files (e.g. Dailymed)
		/// </summary>
		public bool CheckRefs { get; set; }

		/// <summary>
		/// Validate what's possible
		/// </summary>
		public bool Validate { get; set; }

		/// <summary>
		/// More checks will run if strict conversion specified
		/// </summary>
		public bool Strict { get; set; }

        /// <summary>
		/// Generate 2D coordinates for chemicals
		/// </summary>
		public bool Coord { get; set; }

        public class Filter
		{
			public string Subject;
			public FltPred Predicate;
			public string Object;
		}

		public IEnumerable<Filter> Filters { get; set; } = new List<Filter>();

		public SGroupType SGroupTypes { get; set; }
	}

	public class ExportOptions : Options
	{
		public string OutputFile { get; set; }
		public Encoding OutputFileEncoding { get; set; }

		/// <summary>
		/// Output directory (default is "out")
		/// </summary>
		public string OutDir { get; set; }

		public bool Clean { get; set; }
                public bool Canonicalize { get; set; }
                /// <summary>
                /// Normally is associated with a logic of separating files into directories (probably based on file name) rather than dumping everything into the same place
                /// </summary>
                public bool Separate { get; set; }

		public string LogPath { get; set; }

		public string XPrint { get; set; }

		[Flags]
		public enum ExportType
		{
			None,
			Ok = 0x01,
			Diff = 0x02,
			Err = 0x04,
			All = Ok | Diff | Err,
		}

		public ExportType ExpType { get; set; }

		/// <summary>
		/// Use this DocId instead of auto-generate a random one
		/// </summary>
		public Guid? DocId { get; set; }

		/// <summary>
		/// Use this SetId instead of auto-generate a random one
		/// </summary>
		public Guid? SetId { get; set; }

		/// <summary>
		/// Output result SPL into a file named by UNII (<UNII>.spl.xml)
		/// </summary>
		public bool UNIIFile { get; set; }

		/// <summary>
		/// Do not generate GUID-named (after DocId) file
		/// </summary>
		public bool NoDocIdFile { get; set; }

		/// <summary>
		/// DO not generate *.err.xml file when severe error is found
		/// </summary>
		public bool NoErrFile { get; set; }

		/// <summary>
		/// DO not generate *.spl.xml file when error occurs. By default SPL error message is embedded into SPL.
		/// </summary>
		public bool NoSplErr { get; set; }

		/// <summary>
		/// Output each record's SDF file (unmodified - just as read)
		/// </summary>
		public bool SdfFile { get; set; }

		/// <summary>
		/// Output each record's SRS XML file (from DESCR_XML*)
		/// </summary>
		public bool SrsFile { get; set; }

		public bool MolFile { get; set; }

		public bool MolsAsSdf { get; set; }

		public bool GenerateImage { get; set; }
		public IDictionary<string, string> ImageOptions { get; set; } = new Dictionary<string, string>();
	}

	public class NAComponentsExportOptions : Options
	{
		public string ComponentsOutputFile { get; set; }
		public string DictionaryOutputFile { get; set; }

	}
	public class TaxonomyOptions
	{
		/// <summary>
		/// Strip out year from within authors
		/// </summary>
		public bool SimplifiedAuthorityReference { get; set; }
        public bool FailOnMissing { get; set; } = false;
    }
}
