using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FDA.SRS.Utils;
namespace FDA.SRS.ObjectModel
{
	public static class AminoAcids
	{
		private static Dictionary<char, string> _seq2name = new Dictionary<char, string> {
			{ 'A', "Alanine" },
			{ 'R', "Arginine" },
			{ 'N', "Asparagine" },
			{ 'D', "Aspartic acid" },
			{ 'C', "Cysteine" },
			{ 'Q', "Glutamine" },
			{ 'E', "Glutamic acid" },
			{ 'G', "Glycine" },
			{ 'H', "Histidine" },
			{ 'I', "Isoleucine" },
			{ 'L', "Leucine" },
			{ 'K', "Lysine" },
			{ 'M', "Methionine" },
			{ 'F', "Phenylalanine" },
			{ 'P', "Proline" },
			{ 'S', "Serine" },
			{ 'T', "Threonine" },
			{ 'W', "Tryptophan" },
			{ 'Y', "Tyrosine" },
			{ 'V', "Valine" },
            { 'a', "alanine" },
            { 'r', "arginine" },
            { 'n', "asparagine" },
            { 'd', "aspartic acid" },
            { 'c', "cysteine" },
            { 'q', "glutamine" },
            { 'e', "glutamic acid" },
            { 'h', "histidine" },
            { 'i', "isoleucine" },
            { 'l', "leucine" },
            { 'k', "lysine" },
            { 'm', "methionine" },
            { 'f', "phenylalanine" },
            { 'p', "proline" },
            { 's', "serine" },
            { 't', "threonine" },
            { 'w', "tryptophan" },
            { 'y', "tyrosine" },
            { 'v', "valine" },
            { 'X', "non-standard"}
        };

		static public IEnumerable<char> AA_Letters { get { return _seq2name.Keys; } }

		static public IEnumerable<string> AA_Names { get { return _seq2name.Values; } }

		private static Dictionary<char, string> _seq2mol = new Dictionary<char, string>();

		static AminoAcids()
		{
			using ( StringReader sr = new StringReader(Resources.AA)  )
			using ( SdfReader sdf = new SdfReader(sr) ) {
				foreach ( SdfRecord r in sdf.Records ) {
					_seq2mol.Add(r["AAL"].First().First(), r.Mol);
				}
			}
		}

		public static string GetNameByLetter(char letter)
		{
			string name;
            bool dacid = false;
            if ((letter + "").ToLower()[0] == letter) {
                dacid = true;
            }
			name=_seq2name.TryGetValue((letter+"").ToUpper()[0], out name) ? name : null;
            if(name!=null && dacid) {
                return "D-" + name;
            }
            return name;
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
