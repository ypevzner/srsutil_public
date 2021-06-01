using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using System.Linq;
using System.Text.RegularExpressions;

namespace FDA.SRS.Utils
{
	public static class TaxonomyExtensions
	{
		public static string PrepareTaxonomyReference(this string reference, TaxonomyOptions taxonomyOptions)
		{
			if ( taxonomyOptions != null && taxonomyOptions.SimplifiedAuthorityReference )
				return Regex.Replace(reference, @",?\s*\d{4}\b\s*", "");

			return reference;
		}

		public static int? ExtractProtologueYear(this string protologue)
		{
			var m = Regex.Match(protologue, @"\d{4}");
			if ( m.Success ) {
				int year = int.Parse(m.Captures[0].Value);
				if ( year >= 1600 && year <= 2020 )
					return year;
			}

			return null;
		}

        //Added for Ticket 265 : Incorrect capitalization of names

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
        

        private static string[] _excp = { "ex", "and", "et", "al" };
        
        private static Dictionary<String, List<String>> _fixFormat = null;

        private static Dictionary<String, List<String>> fixFormat {
            get {
                if (_fixFormat != null) {
                    return _fixFormat;
                } else {
                    //Pulled from config file                   
                    _fixFormat= loadXPathLists("Validation/AuthorStandardizationDictionary");
                    
                    return _fixFormat;
                }
            }          
        }
        
        public static string CasifyAuthors(this string str)
		{
			return Regex.Replace(str, @"\b(\w+[.]*)+", m => {
                string s = m.Value;

                if (fixFormat.ContainsKey(s.ToLower())) {
                    String ff = fixFormat[s.ToLower()][0];
                    return ff;
                } else {
                    return Regex.Replace(s, @"\b\w+\b", m2 => {
                        string s2 = m2.Value;
                        if (!_excp.Contains(s2.ToLower()))
                            s2 = char.ToUpper(s2[0]) + s2.Substring(1).ToLower();
                        else
                            s2 = s2.ToLower();
                        return s2;
                    });
                }

            });
        }

        //////End
    }
}
