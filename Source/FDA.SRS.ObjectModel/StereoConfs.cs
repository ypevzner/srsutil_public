using com.epam.indigo;
using FDA.SRS.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace FDA.SRS.ObjectModel
{
	static class BitArrayHelpers
	{
		public static string Signature(this BitArray bits)
		{
			StringBuilder sb = new StringBuilder();
			foreach ( bool b in bits )
				sb.Append(b ? "1" : "0");
			return sb.ToString();
		}
	}

	public class StereoConfiguration
	{
		private Indigo _indigo;
		private IndigoObject _molecule;
		private BitArray _stereoBits;

		public IndigoObject Molecule { get { return _molecule; } }

		public StereoConfiguration(Indigo indigo, IndigoObject molecule)
		{
			_indigo = indigo;
			_molecule = molecule.clone();
			_stereoBits = new BitArray(_molecule.countAtoms());
		}

		public StereoConfiguration(StereoConfiguration config)
		{
			_indigo = config._indigo;
			_molecule = config._molecule.clone();
			_stereoBits = config._stereoBits.Clone() as BitArray;
		}

		public StereoConfiguration Clone()
		{
			return new StereoConfiguration(this);
		}

		public void Invert(int index)
		{
			_stereoBits.Set(index, !_stereoBits[index]);
		}

		public string Signature()
		{
			return _stereoBits.Signature();
		}

		public string InverseSignature(BitArray mask)
		{
			return _stereoBits.Xor(mask).Signature();
		}

		public void SaveMol(string dir = null)
		{
			if ( !String.IsNullOrEmpty(dir) && !Directory.Exists(dir) )
				Directory.CreateDirectory(dir);
			Molecule.saveMolfile(Path.Combine(dir ?? "", Signature() + ".mol"));
		}

		public override string ToString()
		{
			IndigoInchi ii = new IndigoInchi(_indigo);
			return String.Format("{0}: {1}", Signature(), ii.getInchi(_molecule));
		}
	}

	// TODO: rewrite the way it works with Moieties
	public class Stereomers
	{
		private Indigo _indigo;
		private BitArray _mask;
		private Dictionary<string, StereoConfiguration> _isomers;

		public Stereomers(Indigo indigo, int count)
		{
			_indigo = indigo;
			_indigo.setOption("molfile-saving-mode", "2000");
			_mask = new BitArray(count);
			_isomers = new Dictionary<string, StereoConfiguration>();
		}

		public void InitMask(IEnumerable<int> centers)
		{
			foreach ( int index in centers )
				_mask.Set(index, true);
		}

		public void Add(StereoConfiguration conf)
		{
			_isomers.Add(conf.Signature(), conf);
		}

		public IEnumerable<Moiety> ToMoieties(string specialStereo, string unii)
		{
			if ( _isomers.Count == 1 ) {
				return new List<Moiety> {
					new Moiety() {
						Molecule = new SDFUtil.NewMolecule(_isomers.First().Value.Molecule.molfile()),
						SpecialStereo = specialStereo,
						MoietyUNII = unii
					}
				};
			}
			else if ( _isomers.Count == 2 ) {
				if ( _indigo.IsMeso(_isomers.First().Value.Molecule, _isomers.Last().Value.Molecule) ) {
					return new List<Moiety> {
						new Moiety() {
							Molecule = new SDFUtil.NewMolecule(_isomers.First().Value.Molecule.molfile()),
							MoietyUNII = unii
						}
				   };
				}
				else {
					// TODO: Anything more complex than single moiety or mesomer cannot have the same UNII
					return new List<Moiety> {
						new Moiety() {
							Molecule = new SDFUtil.NewMolecule(_isomers.First().Value.Molecule.molfile())
							// MoietyUNII = unii
						},
						new Moiety() {
							Molecule = new SDFUtil.NewMolecule(_isomers.Last().Value.Molecule.molfile())
							// MoietyUNII = unii
						},
					};
				}
			}
			else if ( _isomers.Count > 2 ) {
				var ents = GetEnantiomers().ToList();
				return ents.Select(
					p => p.Item2 == null ?
						new Moiety() {
							UndefinedAmount = _isomers.Count > 2,
							MoietyAmount = new Amount(null, null, null),
							Molecule = new SDFUtil.NewMolecule(p.Item1)
							// MoietyUNII = unii
						}
					:
						new Moiety() {
							UndefinedAmount = _isomers.Count > 2,
							MoietyAmount = new Amount(null, null, null),
							Submoieties = new List<Moiety> {
									new Moiety() {
										Molecule = new SDFUtil.NewMolecule(p.Item1)
										// MoietyUNII = unii
									},
									new Moiety() {
										Molecule = new SDFUtil.NewMolecule(p.Item2)
										// MoietyUNII = unii
									},
							}
						});
			}

			return null;
		}

        public IEnumerable<Moiety> ToMoieties(string specialStereo, string unii, bool is_representative_component)
        {
            if (_isomers.Count == 1)
            {
                return new List<Moiety> {
                    new Moiety() {
                        Molecule = new SDFUtil.NewMolecule(_isomers.First().Value.Molecule.molfile()),
                        SpecialStereo = specialStereo,
                        MoietyUNII = unii
                    }
                };
            }
            else if (_isomers.Count == 2)
            {
                if (_indigo.IsMeso(_isomers.First().Value.Molecule, _isomers.Last().Value.Molecule))
                {
                    return new List<Moiety> {
                        new Moiety() {
                            Molecule = new SDFUtil.NewMolecule(_isomers.First().Value.Molecule.molfile()),
                            MoietyUNII = unii
                        }
                   };
                }
                else
                {
                    // TODO: Anything more complex than single moiety or mesomer cannot have the same UNII
                    return new List<Moiety> {
                        new Moiety() {
                            Molecule = new SDFUtil.NewMolecule(_isomers.First().Value.Molecule.molfile())
							// MoietyUNII = unii
						},
                        new Moiety() {
                            Molecule = new SDFUtil.NewMolecule(_isomers.Last().Value.Molecule.molfile())
							// MoietyUNII = unii
						},
                    };
                }
            }
            else if (_isomers.Count > 2)
            {
                var ents = GetEnantiomers().ToList();
                return ents.Select(
                    p => p.Item2 == null ?
                        new Moiety()
                        {
                            UndefinedAmount = _isomers.Count > 2,
                            MoietyAmount = new Amount(null, null, null),
                            Molecule = new SDFUtil.NewMolecule(p.Item1),
                            RepresentativeStructure = is_representative_component
                            // MoietyUNII = unii
                        }
                    :
                        new Moiety()
                        {
                            UndefinedAmount = _isomers.Count > 2,
                            MoietyAmount = new Amount(null, null, null),
                            RepresentativeStructure = is_representative_component,
                            Submoieties = new List<Moiety> {
                                    new Moiety() {
                                        Molecule = new SDFUtil.NewMolecule(p.Item1)
										// MoietyUNII = unii
									},
                                    new Moiety() {
                                        Molecule = new SDFUtil.NewMolecule(p.Item2)
										// MoietyUNII = unii
									},
                            }
                        });
            }

            return null;
        }
        public IEnumerable<Tuple<string, string>> GetEnantiomers()
		{
			List<Tuple<string, string>> list = new List<Tuple<string, string>>();
			HashSet<string> added = new HashSet<string>();
			foreach ( var v in _isomers ) {
				if ( !added.Contains(v.Key) ) {
					StereoConfiguration c = v.Value;
					string peer = c.InverseSignature(_mask);
					if ( _indigo.IsMeso(c.Molecule, _isomers[peer].Molecule) )
						list.Add(new Tuple<string, string>(c.Molecule.molfile(), null));
					else
						list.Add(new Tuple<string, string>(c.Molecule.molfile(), _isomers[peer].Molecule.molfile()));
					added.Add(peer);
				}
			}
			return list;
		}

		public override string ToString()
		{
			return String.Join("; ", _isomers.Select(s => String.Format("{0}: {1}", s.Key, s.Value)));
		}
	}
}
