using FDA.SRS.Utils;
using GeorgeCloney;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Xml.Linq;
using IMolecule = FDA.SRS.Utils.SDFUtil.IMolecule;
using NewMolecule = FDA.SRS.Utils.SDFUtil.NewMolecule;

namespace FDA.SRS.ObjectModel
{
	// TODO: separate "fragment" and "fragment instance in SPL". Registry would return a "fragment" and SRS XML would do the other tweaks on a cloned "fragment instance"
	[Serializable]
	public class PlmrFragment : SplObject, IUniquelyIdentifiable
	{
		[Serializable]
		public class Connector
		{
			/// <summary>
			/// Sorted connector ordinal within a Fragment
			/// </summary>
			public int Id { get; set; }

			/// <summary>
			/// A pair of canonical (N, C) positions within a Fragment
			/// </summary>
			public Tuple<int, int> Snip { get; set; }

			public override bool Equals(object obj)
			{
				return Snip.Item1 == (obj as Connector).Snip.Item1 && Snip.Item2 == (obj as Connector).Snip.Item2;
			}

            public string[] Residues { get; set; } = null;


            /// <summary>
            /// Test a residue String to check if this connector
            /// can make sense for it
            /// </summary>
            public bool CanUseResidue(String r) {
                if (Residues == null || Residues.Length == 0) return true;
                return Residues.Contains(r);
            }

		}

		private string _id;

		/// <summary>
		/// Unique fragment ID in SPL
		/// </summary>
		public override string Id
		{
			set {
				lock ( this )
					_id = value;
			}
			get
			{
				lock ( this ) {
					if ( _id == null )
						_id = String.Format("F{0}", Interlocked.Increment(ref Counters.FragmentCounter));
					return _id;
				}
			}
		}

		public StructuralModification Parent { get; set; }

		/// <summary>
		/// UNII of the fragment
		/// </summary>
		public string UNII { get; set; }

		/// <summary>
		/// Other known identifiers of the fragment
		/// </summary>
		public IList<string> Synonyms { get; set; }

		private IMolecule _molecule;
		private int[] _cn;
		public IMolecule Molecule {
			get { return _molecule; }
			set {
				if ( value is NewMolecule ) {
					NewMolecule nm = value as NewMolecule;
					_cn = nm.CanonicalNumbers();
					_molecule = nm.ReorderCanonically();
				}
			}
		}

		public List<Connector> Connectors { get; set; } = new List<Connector>();

		public PlmrFragment()
		{

		}

		public PlmrFragment(SplObject rootObject)
			: base(rootObject, null)
		{
			
		}

		public PlmrFragment Clone()
		{
			PlmrFragment f = (PlmrFragment)MemberwiseClone();
			f.Connectors = Connectors.DeepClone();
			return f;
		}

		public void AddConnectorsPair(int n, int c, string unii)
		{
            AddConnectorsPair(n, c, null, unii);

        }

        public void AddConnectorsPair(int n, int c, string[] rset,string unii) {
            int c2 = c, n2 = n;
            if (Molecule as NewMolecule != null) {
                if (n != 0)
                    n2 = _cn[n - 1] + 1;
                if (c != 0)
                    c2 = _cn[c - 1] + 1;

                TraceUtils.WriteUNIITrace(TraceEventType.Warning, unii, null, "AddConnectorsPair: connectors transcoded to canonical numbers: ({0}, {1}) => ({2}, {3})", n, c, n2, c2);
            }

            AddConnectorsPairCanonical(n2, c2, rset, unii);
        }

        public void SortConnectors() {
            Connectors.Sort((p1,p2) => {
                int r1 = p1.Snip.Item1;
                int r2 = p2.Snip.Item1;
                if (r1 == 0) r1 = 9999;
                if (r2 == 0) r2 = 9999;

                int d = r1 - r2;
                if (d < 0) return -1;
                if (d > 0) return 1;
                return p1.Snip.Item2 - p2.Snip.Item2;
                
                /*
                 * N	C	Rank
NA	5	4
10	21	2
3	NA	1
20	11	3
NA	15	5
*/
            });
        }

        /// <summary>
        /// Adds connectivity based on canonical atoms instead of molfile atoms
        /// </summary>
        public void AddConnectorsPairCanonical(int n2, int c2, string[] rset, string unii) {
            var tt = new Tuple<int, int>(n2, c2);
            if (!Connectors.Where(cc => cc.Snip.Item1 == tt.Item1 && cc.Snip.Item2 == tt.Item2).Any())
                Connectors.Add(new Connector { Id = -1, Snip = tt, Residues = rset });
            else
                TraceUtils.WriteUNIITrace(TraceEventType.Warning, unii, null, "Attempt to add a dupplicate connectors pair ({0}) ignored", tt);
        }
        /// <summary>
        /// Adds connectivity based on canonical atoms instead of molfile atoms
        /// </summary>
        public void AddConnectorsPairCanonical(int n2, int c2, string unii) {
            AddConnectorsPairCanonical(n2, c2, null, unii);
        }

        /// <summary>
        /// Returns true if a Fragment does not have any connectors
        /// </summary>
        public bool IsMolecule { get { return Connectors.Count == 0; } }

		/// <summary>
		/// Returns true if a Fragment has only one connector and consequently is a modification embedded into AA sequence (replaces one of the letters)
		/// </summary>
		public bool IsModification { get { return Connectors.Count == 1; } }

		/// <summary>
		/// Returns true if a Fragment has multiple connectors and as such serves as a Linker between multiple sequence positions
		/// </summary>
		public bool IsLinker { get { return Connectors.Count > 1; } }

		private string FragmentType
		{
			get
			{
				string type = null;
				if ( IsMolecule )
					type = "Molecule";
				else if ( IsModification )
					type = "Modification";
				else if ( IsLinker )
					type = "Linker";
				return type;
			}
		}

		public override string ToString()
		{
			return String.Format("{0} <{1}> ({2})", FragmentType, UNII, String.Join(", ", Synonyms));
		}

		public string UID
		{
			get
			{
				StringBuilder sb = new StringBuilder(Molecule.InChIKey);

				if ( Connectors.Count > 0 )
					sb.AppendFormat("-{0}", String.Join("-", Connectors.OrderBy(c => c.Snip).Select(c => String.Format("{0}_{1}", c.Snip.Item1, c.Snip.Item2))));

				return sb.ToString();
			}
		}

		public override XElement SPL
		{
			get
			{
				XElement xIdentifiedSubstance =
					new XElement(xmlns.spl + "identifiedSubstance",
						new XElement(xmlns.spl + "code", new XAttribute("code", Id ?? ""), new XAttribute("codeSystem", RootObject.DocId)),
						new SplHash(UID.GetMD5String().FormatAsGuid()).SPL
					);

				if ( !String.IsNullOrEmpty(UNII) ) {
					xIdentifiedSubstance.Add(
						new XElement(xmlns.spl + "asSpecializedKind",
							new XElement(xmlns.spl + "generalizedMaterialKind",
								new XElement(xmlns.spl + "code", new XAttribute("code", UNII), new XAttribute("codeSystem", "2.16.840.1.113883.4.9"))
							)
						));
				}

				// See SRS-173 for explanation
				var xPartMoiety = new XElement(xmlns.spl + "partMoiety");
				if ( !String.IsNullOrEmpty(UNII) )
					xPartMoiety.Add(new XElement(xmlns.spl + "code", new XAttribute("code", UNII), new XAttribute("codeSystem", "2.16.840.1.113883.4.9")));

				xIdentifiedSubstance.Add(
					new XElement(xmlns.spl + "moiety",
						new Amount(1,"mol","mol").SPL, // TODO: For now in all fragments amount == "1/1", unit="1". See SRS-208 for more info.
						xPartMoiety,
						new SplCharacteristic("chemical-mol", Molecule.Mol.MolReplaceProgramName("-FDASRS-")).SPL,
						new SplCharacteristic("chemical-inchi", Molecule.InChI).SPL,
						new SplCharacteristic("chemical-inchikey", Molecule.InChIKey).SPL
					)
				);

				foreach ( var c in Connectors ) {	// .OrderBy(c => c.Snip) - order in other place
					xIdentifiedSubstance.Add(new SplFragmentSnapIn(RootObject, c.Snip) { Id = String.Format("{0}-{1}", Id, c.Id) }.SPL);
				}

				XElement xSubject =
					new XElement(xmlns.spl + "subject",
						new XElement(xmlns.spl + "identifiedSubstance",
							new XElement(xmlns.spl + "id", new XAttribute("extension", Id ?? ""), new XAttribute("root", RootObject.DocId)),
							xIdentifiedSubstance
						));

				return xSubject;
			}
		}
	}
}
