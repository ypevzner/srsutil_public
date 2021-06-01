using FDA.SRS.Utils;

namespace FDA.SRS.ObjectModel
{
	public class Compound : IUniquelyIdentifiable
	{
        private SDFUtil.IMolecule _molecule;

        public string Mol
		{
			get { return _molecule == null ? null : _molecule.ToString(); }
		}

		public string InChI
		{
			get { return _molecule == null ? null : _molecule.InChI; }
		}

		public string InChIKey
		{
			get { return _molecule == null ? null : _molecule.InChIKey; }
		}

		public string SMILES
		{
			get { return _molecule == null ? null : _molecule.SMILES; }
		}

		public string UID
		{
			get { return InChIKey; }
		}

		public Compound(string mol)
		{
            _molecule = new SDFUtil.NewMolecule(mol).ReorderCanonically();
        }
	}
}
