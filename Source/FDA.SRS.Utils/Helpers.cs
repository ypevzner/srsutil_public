using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace FDA.SRS.Utils
{
	public static class Helpers
	{
		// 1_368
		public static Tuple<int, int> SequencePosition(string s)
		{
			if ( Regex.Match(s, @"^\d+$").Success )
				s = String.Format("0_{0}", s);

			string[] su_ref = s.Split('_');
			if ( su_ref.Length != 2 )
				throw new FormatException(String.Format("Position reference malformed: {0}", s));

			// Not cool to substract 1 from subunit reference - should be done in a caller
			return new Tuple<int, int>(int.Parse(su_ref[0].Trim()) - 1, int.Parse(su_ref[1].Trim()) - 1);
		}

		// 1_368; 2_369
		public static IEnumerable<Tuple<int, int>> SequencePositions(string str)
		{
			try {
				return str
					.Trim(' ', ';', ',')
					.Split(new char[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries)
					.Select(s => s.Trim())
					.Select(s => SequencePosition(s));
			}
			catch ( FormatException ex ) {
				throw new SrsException("seq_ref", String.Format("Error parsing sequence positions: {0}", str), ex);
			}
		}

		// 1_32-1_45
		// 1_125-1_275-1_368
		public static IEnumerable<Tuple<int, int>> SequenceLink(string str)
		{
			return str
				.Trim(' ', ';', ',')
				.Split(new char[] { '-' }, StringSplitOptions.RemoveEmptyEntries)
				.Select(s => s.Trim())
				.Select(s => SequencePosition(s));
		}

		// <DISULFIDE_LINKAGE>1_32-1_45;1_146-1_156;1_150-1_174;1_254-1_265;1_473-1_530;1_571-1_596;</DISULFIDE_LINKAGE>
		// <OTHER_LINKAGE><SITE>1_125-1_275-1_368</SITE>
		public static IEnumerable<IEnumerable<Tuple<int, int>>> SequenceLinks(string str)
		{
			try {
				return str
					.Trim(' ', ';', ',')
					.Split(new char[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries)
					.Select(s => s.Trim())
					.Select(s => SequenceLink(s));
			}
			catch ( FormatException ex ) {
				throw new SrsException("seq_ref", String.Format("Error parsing sequence positions: {0}", str), ex);
			}
		}
	}
}
