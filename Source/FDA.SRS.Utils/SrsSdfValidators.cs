using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace FDA.SRS.Utils
{
	public static class SrsSdfValidators
	{
		private static List<Regex> loadExpressions(string section)
		{

            Debug.WriteLine("TEST");

			NameValueCollection expressions = (NameValueCollection)ConfigurationManager.GetSection(section);
			return expressions == null ?
				new List<Regex>() :
				expressions
					.Keys
					.Cast<string>()
					.Select(k => new Regex(expressions[k], RegexOptions.IgnoreCase))
					.ToList();
		}

		private static Dictionary<string, List<Regex>> loadExpressionLists(string section)
		{


            Debug.WriteLine("TEST2:" + section);

            NameValueCollection expressions = (NameValueCollection)ConfigurationManager.GetSection(section);
			return expressions == null ?
				new Dictionary<string, List<Regex>>() :
				expressions
					.Keys
					.Cast<string>()
					.ToDictionary(
						k => k,
						k => expressions[k]
							.Split(new char[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
							.Select(s => new Regex(s.Trim(), RegexOptions.IgnoreCase))
							.ToList()
					);
		}

        private static Dictionary<string, List<String>> loadXPathLists(string section) {


            NameValueCollection expressions = (NameValueCollection)ConfigurationManager.GetSection(section);
            return expressions == null ?
                new Dictionary<string, List<String>>() :
                expressions
                    .Keys
                    .Cast<string>()
                    .ToDictionary(
                        k => k,
                        k => expressions[k]
                            .Split(new char[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                            .Select(s => s)
                            .ToList()
                    );
        }

        private static List<Regex> loadExpressions(string section, string format)
		{
			NameValueCollection expressions = (NameValueCollection)ConfigurationManager.GetSection(section);
			return expressions == null ?
				new List<Regex>() :
				expressions
					.Keys
					.Cast<string>()
					.Select(k => new Regex(String.Format(format, k, expressions[k]), RegexOptions.IgnoreCase))
					.ToList();
		}

		public static Dictionary<string, List<Regex>> RootDescriptionTags = loadExpressionLists("Validation/DescriptionRoot");
        public static Dictionary<string, List<String>> BadDescriptionTags = loadXPathLists("Validation/BadDescriptionXPaths");
        private static List<Regex> _badComments = loadExpressions("Validation/BadComments");
		private static List<Regex> _skipSdataStrings = loadExpressions("Validation/SkipSdata", @"^M\s+{0}\s+{1}");
		private static List<Regex> _badSdataStrings = loadExpressions("Validation/BadSdata", @"^M\s+{0}\s+{1}");
		private static List<Regex> _badMixType = loadExpressions("Validation/BadMixtureType");

		public static void ValidateSrsSdf(this SdfRecord sdf, ImportOptions impOpt)
		{
			// Test info if any accumulated wrt properties
			string srs_errors = sdf.GetFieldValue("srs-errors");
			if ( !String.IsNullOrEmpty(srs_errors) )
				throw new SrsException("fields-validation", srs_errors);

			// Comments field checks
			string comment = sdf.GetFieldValue("COMMENTS");
			if ( !String.IsNullOrEmpty(comment) ) {
				_badComments.ForEach(re => {
					if ( re.IsMatch(comment) )
						throw new SrsException("fields-validation", String.Format("Invalid ({0}) string in description: {1}", re.ToString(), comment));
				});
			}
		}

		public static XDocument ValidateMixtureSdf(this SdfRecord sdf, ImportOptions impOpt)
		{
			// Common validations
			sdf.ValidateSrsSdf(impOpt);

			// Mixture type checks
			string mixtype = sdf.GetFieldValue("MIXTURE_TYPE");
			if ( !String.IsNullOrEmpty(mixtype) ) {
				_badMixType.ForEach(re => {
					if ( re.IsMatch(mixtype) )
						throw new SrsException("fields-validation", String.Format("Invalid ({0}) string in description: {1}", re.ToString(), mixtype));
				});
			}

            XDocument getMix =sdf.GetDescXml("MIX_DESC_PART", SrsDomain.Mixture, impOpt);
            // Get and check Mixture description field
            return sdf.GetDescXml("DESC_PART", SrsDomain.Substance, impOpt);
        }

		public static readonly Func<SdfRecord, int, string, string> SubstanceSdfLineFilter = (sdf, n, l) => {
			foreach ( var re in _skipSdataStrings )
				if ( re.IsMatch(l) )
					return null;

			foreach ( var re in _badSdataStrings )
				if ( re.IsMatch(l) )
					sdf.AddField("srs-errors", String.Format("Invalid ({0}) properties string: {1}", re.ToString(), l));

			return l;
		};
	}
}
