using FDA.SRS.Utils;
using GeorgeCloney;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Xml.Linq;
using System.Text.RegularExpressions;

using IMolecule=FDA.SRS.Utils.SDFUtil.IMolecule;
using NewMolecule = FDA.SRS.Utils.SDFUtil.NewMolecule;

namespace FDA.SRS.ObjectModel
{
	// TODO: separate "fragment" and "fragment instance in SPL". Registry would return a "fragment" and SRS XML would do the other tweaks on a cloned "fragment instance"
	[Serializable]
	public class NAFragment : SplObject, IUniquelyIdentifiable
	{

		[Serializable]
		public class Connector
		{
			/// <summary>
			/// Sorted connector ordinal within a Fragment
			/// </summary>
			public int Id { get; set; }

			/// <summary>
			/// YP connection atom, connection type
			/// </summary>
			public Tuple<int, int> Snip { get; set; }

			public override bool Equals(object obj)
			{
				return Snip.Item1 == (obj as Connector).Snip.Item1 && Snip.Item2 == (obj as Connector).Snip.Item2;
			}

            public string[] Nucleotides { get; set; } = null;


            /// <summary>
            /// Test a residue String to check if this connector
            /// can make sense for it
            /// </summary>
            public bool CanUseNucleotide(String r) {
                if (Nucleotides == null || Nucleotides.Length == 0) return true;
                return Nucleotides.Contains(r);
            }

            //YP SRS-394
            static public implicit operator Connector(ComponentConnector cc)
            {
                return new Connector { Snip =new Tuple<int,int> (cc.Snip.Item1,0), Id = cc.Id, Nucleotides=cc.Nucleotides};
            }

        }

        [Serializable]
        public class ComponentConnector
        {
            /// <summary>
            /// Sorted connector ordinal within a Fragment
            /// </summary>
            public int Id { get; set; }

            /// <summary>
            /// YP connection atom, connection type
            /// </summary>
            public Tuple<int, string> Snip { get; set; }

            public override bool Equals(object obj)
            {
                return Snip.Item1 == (obj as ComponentConnector).Snip.Item1 && Snip.Item2 == (obj as ComponentConnector).Snip.Item2;
            }

            public string[] Nucleotides { get; set; } = null;


            /// <summary>
            /// Test a residue String to check if this connector
            /// can make sense for it
            /// </summary>
            public bool CanUseNucleotide(String r) {
                if (Nucleotides == null || Nucleotides.Length == 0) return true;
                return Nucleotides.Contains(r);
            }

            

        }

        private string _id;

        public bool isDeletion = false;

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

		public NAStructuralModification Parent { get; set; }

		/// <summary>
		/// UNII of the fragment
		/// </summary>
		public string UNII { get; set; }

		/// <summary>
		/// Other known identifiers of the fragment
		/// </summary>
		public IList<string> Synonyms { get; set; }

		private IMolecule _molecule;
        //public IMolecule Molecule;
        private int[] _cn;


        //YP had to comment out this block and replace private _molecule with public Molecule 
        //because the setter was throwing a vague error for cases with connection atoms
        //public IMolecule Molecule {
        //get { return _molecule; }
        //set {
        //_molecule = value as IMolecule;
        //if ( value is NewMolecule ) {
        //NewMolecule nm = value as NewMolecule;
        //_molecule = value;
        //_cn = nm.CanonicalNumbers();
        //_molecule = nm.ReorderCanonically();
        //YP if connection atoms are used, the above will not result in null, below check for that and just use nm to set _molecule
        /*if (_molecule == null)
        {
            _molecule = nm;
        }
        */
        //}
        //}
        //}
        //YP had to comment out this block and replace private _molecule with public Molecule 
        //because the setter was throwing a vague error for cases with connection atoms
        
        public IMolecule Molecule {
            get { return _molecule; }
            set {
                _molecule = value as IMolecule;
                if ( value is NewMolecule ) {
                    NewMolecule nm = value as NewMolecule;
                    //_molecule = value;
                    if (!nm.Mol.Contains("R#"))
                    {
                        _cn = nm.CanonicalNumbers();
                        _molecule = nm.ReorderCanonically();
                    }
                    
                    //YP if connection atoms are used, the above will not result in null, below check for that and just use nm to set _molecule
                    //if (_molecule == null)
                    //{
                    //    _molecule = nm;
                    //}
        
                }
            }
        }

        public int[] getCanonicalAtoms()
        {
            return _cn;
        }

        public List<Connector> Connectors { get; set; } = new List<Connector>();
        public List<ComponentConnector> ComponentConnectors { get; set; } = new List<ComponentConnector>();

        public NAFragment()
		{

		}

		public NAFragment(SplObject rootObject)
			: base(rootObject, null)
		{
			
		}

		public NAFragment Clone()
		{
			NAFragment f = (NAFragment)MemberwiseClone();
			f.Connectors = Connectors.DeepClone();
            f.ComponentConnectors = ComponentConnectors.DeepClone();
			return f;
		}

        /// <summary>
		/// Returns atom index of a star atom by its type (e.g. "_R1")
		/// </summary>
        public int getStarAtomIndex(string connector_type)
        {
            int returnvalue = -1;
            foreach (ComponentConnector connector in ComponentConnectors)
            {
                if (connector.Snip.Item2 == connector_type)
                {
                    returnvalue = connector.Snip.Item1;
                    break;
                }
            }
            return returnvalue;
        }

        public int getConnectingAtomIndex(string connector_type)
        {
            //
            int returnvalue = -1;
            foreach (ComponentConnector connector in ComponentConnectors)
            {
                if (connector.Snip.Item2 == connector_type)
                {
                    returnvalue = connector.Snip.Item1;
                    break;
                }
            }
            return returnvalue;
        }

        public void AddConnectorsPair(int n, int c, string unii)
        {
            AddConnectorsPair(n, c, null, unii);

        }

        public void AddConnectorsPair(int n, int c, string[] rset, string unii)
        {
            int c2 = c, n2 = n;
            
            var tt = new Tuple<int, int>(n2, c2);
            if (!Connectors.Where(cc => cc.Snip.Item1 == tt.Item1 && cc.Snip.Item2 == tt.Item2).Any())
                Connectors.Add(new Connector { Id = -1, Snip = tt, Nucleotides = rset });
            else
                TraceUtils.WriteUNIITrace(TraceEventType.Warning, unii, null, "Attempt to add a dupplicate connectors pair ({0}) ignored", tt);
        }

        public void AddConnectors(List<int> atom_indices, List<string> connection_types)
        {
            AddConnectors(atom_indices, connection_types, null, "");

        }

        public void AddConnectors(List<int> atom_indices, List<string> connection_types, string unii)
		{
            AddConnectors(atom_indices, connection_types, null, unii);

        }

        public void AddConnectors(List<int> atom_indices, List<string> connection_types, string[] rset,string unii) {

            //TraceUtils.WriteUNIITrace(TraceEventType.Warning, unii, null, "AddConnectors: connectors transcoded to canonical numbers: ({0}, {1}) => ({2}, {3})", n, c, n2, c2);
            
            //List<String> atom_indices_list = Regex.Replace(atom_indices, @"\s", ";").Split(';').ToList();
            //List<String> connection_types_list = Regex.Replace(connection_types, @"\s", ";").Split(';').ToList();

            for (int i = 0; i < atom_indices.Count; i++)
            {
                if (atom_indices[i] == 0)
                {

                }
                //if (atom_indices[i] != 0)
                //{
                    Tuple<int, string> new_snip = new Tuple<int, string>(atom_indices[i], (connection_types.Count()!=0?connection_types[i]:"0"));
                    //YP commenting this check out for now because component connectrs aren't viewed pairwise
                    //will likely have to come back to this later if this presents a problem with duplicate connectors
                    //if (!ComponentConnectors.Where(cc => cc.Snip.Item1 == atom_indices[i] && cc.Snip.Item2 == (connection_types.Count() < i?"0": connection_types[i])).Any())
                    //{
                        ComponentConnectors.Add(new ComponentConnector()
                        {
                            Snip = new_snip
                        });
                    //}
                    //else
                    //{
                    //    TraceUtils.WriteUNIITrace(TraceEventType.Warning, unii, null, "Attempt to add a dupplicate connectors pair ({0}) ignored", new_snip);
                    //}
               //}
            }
            
        }

        /// <summary>
        /// Returns true if a Fragment does not have any connectors
        /// </summary>
        public bool IsMolecule { get { return (Connectors.Count == 0 && ComponentConnectors.Count == 0); } }

		/// <summary>
		/// Returns true if a Fragment has only one connector and consequently is a modification embedded into NA sequence (replaces one of the letters)
		/// </summary>
		public bool IsModification { get { return Connectors.Count == 1; } }

		/// <summary>
        /// YP this needs to be modified for NAs
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

                if (ComponentConnectors.Count > 0)
                {
                    xIdentifiedSubstance.Add(new SplNAMultiFragmentSnapIn(RootObject, ComponentConnectors).SPL);
                } else
                {
                    foreach (var c in Connectors)
                    {   // .OrderBy(c => c.Snip) - order in other place
                        xIdentifiedSubstance.Add(new SplNAFragmentSnapIn(RootObject, c.Snip) { Id = String.Format("{0}-{1}", Id, c.Id) }.SPL);
                    }
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
