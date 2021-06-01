using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace FDA.SRS.Utils
{
	public static class CmdLineUtils
	{
		static public IEnumerable<int> ParseSet(string set)
		{
			if ( String.IsNullOrEmpty(set) )
				return null;

			return set
				.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
				.Select(s => s.Trim())
				.SelectMany(s => {
					if ( !s.Contains('-') )
						return new int[] { int.Parse(s) };
					else {
						string[] range = s.Split('-');
						int start = int.Parse(range[0]);
						int count = int.Parse(range[1]) - start + 1;
						return Enumerable.Range(start, count);
					}
				});
		}

		static public IDictionary<string, string> ParseMapping(string mapping)
		{
			if ( String.IsNullOrEmpty(mapping) )
				return null;
			return mapping
				.Split(new char[] { ',', ';' })
				.Select(s => s.Trim())
				.ToDictionary(s => s.Substring(0, s.IndexOf("=>")).Trim(), s => s.Substring(s.IndexOf("=>") + 2).Trim());
		}

		public static IEnumerable<ConvertOptions.Filter> ParseFilters(IEnumerable<string> flts)
		{
			var preds = new string[] { "==", "!=", "=~", "!~" };

			List<ConvertOptions.Filter> filters = new List<ConvertOptions.Filter>();
			flts.ToList().ForEach(f => {
				var ss = Regex.Split(f, "(==|!=|=~|!~)", RegexOptions.Compiled).Select(s => s.Trim()).ToList();
				if ( ss.Count() == 3 ) {
					if ( !preds.Contains(ss[1]) )
						throw new FormatException(f);
				}
				else if ( ss.Count() != 1 )
					throw new FormatException(f);

				if ( ss.Count() == 3 )
					filters.Add(new ConvertOptions.Filter { Subject = ss[0], Predicate = ss[1].ToFltPred(), Object = ss[2] });
				else if ( ss[0].StartsWith("!") )
					filters.Add(new ConvertOptions.Filter { Subject = ss[0].Substring(1).Trim(), Predicate = ConvertOptions.FltPred.NotExists });
				else
					filters.Add(new ConvertOptions.Filter { Subject = ss[0], Predicate = ConvertOptions.FltPred.Exists });

			});
			return filters;
		}

		static ConvertOptions.FltPred ToFltPred(this string pred)
		{
			switch ( pred ) {
				case "==":
					return ConvertOptions.FltPred.Equals;
				case "!=":
					return ConvertOptions.FltPred.NotEquals;
				case "=~":
					return ConvertOptions.FltPred.Like;
				case "!~":
					return ConvertOptions.FltPred.NotLike;
				default:
					throw new FormatException(String.Format("Unknown predicate: {0}", pred));
			}
		}

		public static Dictionary<string, string> ParseImageOptions(List<string> opts)
		{
			NameValueCollection _opts = (NameValueCollection)ConfigurationManager.GetSection("ImageOptions");
			Dictionary<string, string> options = _opts.Keys.Cast<string>().ToDictionary(k => k, k => _opts[k]);

			foreach ( var o in opts ) {
				var ss = o.Split('=').Select(s => s.Trim());
				if ( ss.Count() == 2 ) {
					string key = ss.First();
					string value = ss.Last();
					if ( options.ContainsKey(key) )
						options[key] = value;
					else
						options.Add(key, value);
				}
			}

			return options;
		}

		public static Encoding ParseEncodingPrefix(string s)
		{
			if ( s == null )
				return null;

			var encs = Encoding.GetEncodings().Select(e => e.Name);
			var ss = s.Split(':').ToList();
			if ( ss.Count() > 1 && encs.Contains(ss[0]) )
				return Encoding.GetEncoding(ss[0]);
			else
				return Encoding.UTF8;
		}

		public static string ParsePathWithEncodingPrefix(string s)
		{
			if ( s == null )
				return null;

			var encs = Encoding.GetEncodings().Select(e => e.Name);
			var ss = s.Split(':').ToList();
			if ( ss.Count() > 1 && encs.Contains(ss[0]) )
				return s.Substring(ss[0].Length + 1);
			else
				return s;
		}
	}
}
