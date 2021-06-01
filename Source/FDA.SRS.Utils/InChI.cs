using com.epam.indigo;


namespace FDA.SRS.Utils
{
    public static class InChI
    {
        public static string Clean(string mol)
        {
            string str;
            using (Indigo indigo = new Indigo())
            {
                using (IndigoObject indigoObjects = indigo.loadMolecule(mol))
                {
                    indigoObjects.layout();
                    str = indigoObjects.molfile();
                }
            }
            return str;
        }

        public static string InChIToMol(string inchi, bool clean = true)
        {
            var oindigo = new Indigo();
            var indigo = new IndigoInchi(oindigo);
            var molecule = indigo.loadMolecule(inchi);
            string str = molecule.molfile();
            return (clean ? Clean(str) : str);
        }

        public static string InChIToSMILES(string inchi)
        {
            var oindigo = new Indigo();
            var indigo = new IndigoInchi(oindigo);
            var molecule = indigo.loadMolecule(inchi);
            string str = molecule.smiles().Trim();
            return str;
        }
    }
}
