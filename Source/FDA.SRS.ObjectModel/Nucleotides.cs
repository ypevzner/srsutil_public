using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FDA.SRS.Utils;

namespace FDA.SRS.ObjectModel
{
	public static class Nucleotides
	{
		private static Dictionary<char, string> _seq2name = new Dictionary<char, string> {
			{ 'A', "Adenine" },
			{ 'C', "Cytosine" },
			{ 'G', "Guanine" },
			{ 'T', "Thymine" },
			{ 'U', "Uracil" },
            { 'a', "Adenine" },
            { 'c', "Cytosine" },
            { 'g', "Guanine" },
            { 't', "Thymine" },
            { 'u', "Uracil" },
            { 'X', "non-standard"}
        };

		static public IEnumerable<char> Nucleotide_Letters { get { return _seq2name.Keys; } }

		static public IEnumerable<string> Nucleotide_Names { get { return _seq2name.Values; } }

		private static Dictionary<char, string> _seq2mol = new Dictionary<char, string>();

		static Nucleotides()
		{
			using ( StringReader sr = new StringReader(Resources.NA)  )
			using ( SdfReader sdf = new SdfReader(sr) ) {
				foreach (Utils.SdfRecord r in sdf.Records ) {
					_seq2mol.Add(r["NA"].First().First(), r.Mol);
				}
			}
		}

		public static string GetNameByLetter(char letter)
		{
			string name;
			return _seq2name.TryGetValue(letter, out name) ? name : null;
		}

		public static string GetMolByLetter(char letter)
		{
			string mol;
			return _seq2mol.TryGetValue(letter, out mol) ? mol : null;
		}

		private static Dictionary<char, Compound> _compoundsCache = new Dictionary<char, Compound>();

		public static Compound GetCompoundByLetter(char letter)
		{
			lock ( _compoundsCache ) {
				if ( _compoundsCache.ContainsKey(letter) )
					return _compoundsCache[letter];

				Compound c = new Compound(GetMolByLetter(letter));
				_compoundsCache.Add(letter, c);
				return c;
			}
		}

		// TODO: adapt for possible variation in names
		private class AAEqualityComparer : IEqualityComparer<string>
		{
			public bool Equals(string x, string y)
			{
				return String.Compare(x, y, true) == 0;
			}

			public int GetHashCode(string obj)
			{
				return obj.ToLower().GetHashCode();
			}
		}

		public static bool IsValidAminoAcidLetter(char letter)
		{
			return _seq2name.Keys.Contains(letter);
		}

		public static bool IsValidAminoAcidName(string name)
		{
			return _seq2name.Values.Contains(name, new AAEqualityComparer());
		}
	}
}
