using com.epam.indigo;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Configuration;
using System.Drawing;

namespace FDA.SRS.Utils
{

    public class SDFUtil
    {
        public enum CurationQuality : byte
        {
            AuthorityRejected,
            AutoRejected,
            CuratorRejected,
            Uncertaint,
            CuratorConfirmed,
            AutoApproved,
            AuthorityApproved
        }

        public class DataMeta
        {
            public string type;

            public string subtype;

            public CurationQuality quality;

            public DataMeta()
            {
            }
        }

        public class DataValue
        {
            public string Value;

            public DataMeta Meta;

            public DataValue()
            {
            }
        }

        public interface IMolecule
        {
            string InChI
            {
                get;
            }

            string InChIKey
            {
                get;
            }

            double? MolecularWeight
            {
                get;
            }

            bool IsEmpty
            {
                get;
            }

            string Mol
            {
                get;
                set;
            }

            string SMILES
            {
                get;
            }

            byte[] StructureHash
            {
                get;
            }

            byte[] Thumbnail
            {
                get;
            }
        }


        public class NewMolecule : IMolecule
        {
            private bool _allowV2000Downsize = false;

            private string _mol = null;

            private Dictionary<int, string> _ends;

            private bool _ends_calculated;

            private bool _inchi_info_calculated;

            private string _inchi;

            private string _inchi_key;

            private string _inchi_aux;

            private bool _mol_info_calculated;

            private bool _molecular_weight_calculated;

            private int _atoms_num;

            private bool _smiles_calculated;

            private string _smiles;

            private bool _thumbnail_calculated;

            private byte[] _thumbnail;

            private IList<NewMolecule> _components;

            private double? _molecular_weight;

            public bool AllowV2000Downsize
            {
                get
                {
                    return this._allowV2000Downsize;
                }
                set
                {
                    this._allowV2000Downsize = value;

                    this._inchi = null;
                    this._inchi_key = null;
                    this._inchi_aux = null;

                    this._inchi_info_calculated = false;
                    this._smiles_calculated = false;
                    this._thumbnail_calculated = false;
                    this._mol_info_calculated = false;
                }
            }

            public int AtomsCount
            {
                get
                {
                    int _atomsNum;
                    if (this.Mol != null)
                    {
                        if (!this._mol_info_calculated)
                        {
                            this.calculateMolInfo();
                        }
                        _atomsNum = this._atoms_num;
                    }
                    else
                    {
                        _atomsNum = 0;
                    }
                    return _atomsNum;
                }
            }

            public IEnumerable<NewMolecule> Components
            {
                get
                {
                    if (this._components == null)
                    {
                        this._components = new List<NewMolecule>();
                        using (Indigo indigo = new Indigo())
                        {
                            indigo.setOption("ignore-stereochemistry-errors", 1);
                            using (IndigoObject indigoObjects = indigo.loadMolecule(this.Mol))
                            {
                                if (indigoObjects != null)
                                {
                                    if (indigoObjects.countComponents() > 1)
                                    {
                                        foreach (IndigoObject indigoObjects1 in indigoObjects.iterateComponents())
                                        {
                                            this._components.Add(new NewMolecule(indigoObjects1.clone().molfile()));
                                        }
                                    }
                                }
                            }
                        }
                    }
                    return this._components;
                }
            }

            public Dictionary<int, string> Ends
            {
                get
                {
                    if (!this._ends_calculated)
                    {
                        using (Indigo indigo = new Indigo())
                        {
                            indigo.setOption("ignore-stereochemistry-errors", 1);
                            using (IndigoObject indigoObjects = indigo.loadMolecule(this.Mol))
                            {
                                this.findEnds(indigoObjects);
                            }
                        }
                    }
                    return this._ends;
                }
            }

            public string InChI
            {
                get
                {
                    string str;
                    if (this.Mol != null)
                    {
                        if (!this._inchi_info_calculated)
                        {
                            this.calculateInChI();
                        }
                        str = this._inchi;
                    }
                    else
                    {
                        str = null;
                    }
                    return str;
                }
            }

            public string InChIAuxInfo
            {
                get
                {
                    string str;
                    if (this.Mol != null)
                    {
                        if (!this._inchi_info_calculated)
                        {
                            this.calculateInChI();
                        }
                        str = this._inchi_aux;
                    }
                    else
                    {
                        str = null;
                    }
                    return str;
                }
            }

            public string InChIKey
            {
                get
                {
                    string str;
                    if (this.Mol != null)
                    {
                        if (!this._inchi_info_calculated)
                        {
                            this.calculateInChI();
                        }
                        str = this._inchi_key;
                    }
                    else
                    {
                        str = null;
                    }
                    return str;
                }
            }

            public double? MolecularWeight
            {
                get
                {
                    double? val;
                    if (this.Mol != null)
                    {
                        if (!this._molecular_weight_calculated)
                        {
                            this.calculateMolecularWeight();
                        }
                        val = this._molecular_weight;
                    }
                    else
                    {
                        val = null;
                    }
                    return val;
                }
            }

            public bool IsEmpty
            {
                get
                {
                    return string.IsNullOrEmpty(this.SMILES);
                }
            }

            public string Mol
            {
                get
                {
                    return this._mol;
                }
                set
                {
                    this._mol = value;
                    this._inchi = null;
                    this._inchi_key = null;
                    this._inchi_aux = null;
                    this._inchi_info_calculated = false;
                    this._smiles_calculated = false;
                    this._thumbnail_calculated = false;
                    this._mol_info_calculated = false;
                }
            }

            public string SMILES
            {
                get
                {
                    string str;
                    string str1;
                    if (this.Mol != null)
                    {
                        if (!this._smiles_calculated)
                        {
                            try
                            {
                                using (Indigo indigo = new Indigo())
                                {
                                    indigo.setOption("ignore-stereochemistry-errors", 1);
                                    using (IndigoObject indigoObjects = indigo.loadMolecule(this.Mol))
                                    {
                                        this._smiles = indigoObjects.smiles();
                                    }
                                }
                                if (!string.IsNullOrEmpty(this._smiles))
                                {
                                    int num = this._smiles.IndexOf('|');
                                    if (num >= 0)
                                    {
                                        this._smiles = this._smiles.Substring(0, num);
                                    }
                                }
                            }
                            catch (Exception exception)
                            {
                                //Trace.TraceWarning(exception.Message);
                            }
                            this._smiles_calculated = true;
                        }
                        if (string.IsNullOrEmpty(this._smiles))
                        {
                            str1 = null;
                        }
                        else
                        {
                            str1 = this._smiles;
                        }
                        str = str1;
                    }
                    else
                    {
                        str = null;
                    }
                    return str;
                }
            }

            public byte[] StructureHash
            {
                get
                {
                    byte[] mD5Hash;
                    if (this.InChIKey == null)
                    {
                        mD5Hash = null;
                    }
                    else
                    {
                        mD5Hash = this.InChIKey.GetMD5Hash();
                    }
                    return mD5Hash;
                }
            }

            public byte[] Thumbnail
            {
                get
                {
                    byte[] numArray;
                    if (this.Mol != null)
                    {
                        if (!this._thumbnail_calculated)
                        {
                            this._thumbnail = this.GetImage(int.Parse(ConfigurationManager.AppSettings["thumbnail-width"] ?? "200"), int.Parse(ConfigurationManager.AppSettings["thumbnail-height"] ?? "200"));
                            this._thumbnail_calculated = true;
                        }
                        numArray = this._thumbnail;
                    }
                    else
                    {
                        numArray = null;
                    }
                    return numArray;
                }
            }

            public NewMolecule()
            {
            }

            public NewMolecule(string mol)
            {
                this.Mol = mol;
            }

            private void calculateInChI()
            {
                using (Indigo indigo = new Indigo())
                {
                    indigo.setOption("ignore-stereochemistry-errors", 1);
                    
                    using (IndigoObject indigoObjects = indigo.loadMolecule(this.Mol))
                    {
                        this.findEnds(indigoObjects);

                        IndigoInchi indigoInChI = new IndigoInchi(indigo);
                        try
                        {
                            this._inchi = indigoInChI.getInchi(indigoObjects);
                        }
                        catch (IndigoException)
                        {
                            this._inchi = null;
                        }

                        if (this._inchi == null)
                        {
                            string str = indigoObjects.molfile();
                            if (this.AllowV2000Downsize && str.Contains("V3000"))
                            {
                                indigo.setOption("molfile-saving-mode", "2000");
                                try
                                {
                                    this._inchi = indigoInChI.getInchi(indigoObjects);
                                }
                                catch (IndigoException)
                                {
                                    this._inchi = null;
                                }
                            }
                        }

                        if (this._inchi != null)
                        {
                            this._inchi_key = indigoInChI.getInchiKey(this._inchi);
                            this._inchi_aux = indigoInChI.getAuxInfo();
                            this._mol = indigoObjects.molfile();
                        }
                    }
                }
                this._inchi_info_calculated = true;
            }

            private void calculateMolInfo()
            {
                if ((this.Mol == null ? false : !this._mol_info_calculated))
                {
                    try
                    {
                        using (Indigo indigo = new Indigo())
                        {
                            indigo.setOption("ignore-stereochemistry-errors", 1);
                            using (IndigoObject indigoObjects = indigo.loadMolecule(this.Mol))
                            {
                                this._atoms_num = indigoObjects.countAtoms();
                            }
                        }
                    }
                    catch (Exception exception)
                    {
                        //  Trace.TraceWarning(exception.Message);
                    }
                    this._mol_info_calculated = true;
                }
            }

            private void calculateMolecularWeight()
            {
                using (Indigo indigo = new Indigo())
                {

                    using (IndigoObject indigoObjects = indigo.loadMolecule(this.Mol))
                    {
                        this._molecular_weight = indigoObjects.molecularWeight();
                    }
                }
                this._mol_info_calculated = true;
            }

            private void findEnds(IndigoObject mol)
            {
                this._ends = null;
                for (int i = 0; i < mol.countAtoms(); i++)
                {
                    IndigoObject atom = mol.getAtom(i);
                    if (atom.symbol() == "*")
                    {
                        if (this._ends == null)
                        {
                            this._ends = new Dictionary<int, string>();
                        }
                        atom.resetAtom("H");
                        foreach (IndigoObject indigoObjects in atom.iterateNeighbors().Cast<IndigoObject>())
                        {
                            this._ends.Add(indigoObjects.index() - 1, indigoObjects.symbol());
                        }
                    }
                }
                this._ends_calculated = true;
            }

            public byte[] GetImage(int width, int height)
            {
                byte[] buffer;
                if (this.Mol != null)
                {
                    try
                    {
                        using (Indigo indigo = new Indigo())
                        {
                            indigo.setOption("ignore-stereochemistry-errors", 1);
                            using (IndigoObject indigoObjects = indigo.loadMolecule(this.Mol))
                            {
                                if (indigoObjects != null)
                                {
                                    IndigoRenderer indigoRenderer = new IndigoRenderer(indigo);
                                    indigo.setOption("render-output-format", "png");
                                    indigo.setOption("render-stereo-style", "ext");
                                    indigo.setOption("render-margins", 1, 1);
                                    indigo.setOption("render-image-size", width, height);
                                    buffer = indigoRenderer.renderToBuffer(indigoObjects);
                                }
                                else
                                {
                                    buffer = null;
                                }
                            }
                        }
                    }
                    catch (Exception exception)
                    {
                        //Trace.TraceWarning("{0}", new object[] { exception });
                        buffer = null;
                    }
                }
                else
                {
                    buffer = null;
                }
                return buffer;
            }
        }

        public abstract class MoleculeRecord : IMoleculeRecord
        {
            protected Dictionary<string, List<string>> m_Properties = new Dictionary<string, List<string>>();

            private bool _DataHash_calculated;

            private byte[] _DataHash;

            public abstract int DataCount
            {
                get;
            }

            public byte[] DataHash
            {
                get
                {
                    if (!this._DataHash_calculated)
                    {
                        StringBuilder stringBuilder = new StringBuilder();
                        foreach (string str in
                            from k in this.m_Properties.Keys
                            orderby k
                            select k)
                        {
                            stringBuilder.AppendLine(str);
                            foreach (string str1 in
                                from v in this.m_Properties[str]
                                where !string.IsNullOrWhiteSpace(v)
                                orderby v
                                select v)
                            {
                                stringBuilder.AppendLine(str1);
                            }
                        }
                        if (stringBuilder.Length > 0)
                        {
                            this._DataHash = stringBuilder.ToString().GetMD5Hash();
                        }
                        this._DataHash_calculated = true;
                    }
                    return this._DataHash;
                }
            }

            public byte[] Hash
            {
                get
                {
                    byte[] mD5Hash;
                    if ((this.Molecule == null || this.Molecule.StructureHash == null ? false : this.DataHash != null))
                    {
                        //mD5Hash = this.Molecule.StructureHash.Concat<byte>(this.DataHash).ToArray<byte>().
                            //.ToArray<byte>().GetMD5Hash();
                        mD5Hash = this.Molecule.StructureHash.Concat<byte>(this.DataHash).ToArray<byte>().GetMD5Hash();
                        //GALOG
                        //mD5Hash = this.Molecule.StructureHash.Concat<byte>(this._DataHash).ToArray<byte>();
                    }
                    else if (this.DataHash != null)
                    {
                        mD5Hash = this.DataHash;
                    }
                    else if ((this.Molecule == null ? true : this.Molecule.StructureHash == null))
                    {
                        mD5Hash = null;
                    }
                    else
                    {
                        mD5Hash = this.Molecule.StructureHash;
                    }
                    return mD5Hash;
                }
            }

            public IMolecule Molecule
            {
                get;
                set;
            }

            public Dictionary<string, List<string>> Properties
            {
                get
                {
                    return this.m_Properties;
                }
            }

            protected MoleculeRecord()
            {
            }

            public IEnumerable<DataValue> GetDataByName(string name)
            {
                throw new NotImplementedException();
            }

            public IEnumerable<DataValue> GetDataByOrdinal(int n)
            {
                throw new NotImplementedException();
            }

            public IEnumerable<DataValue> GetDataByType(string type, string subtype)
            {
                throw new NotImplementedException();
            }
        }

        public interface IMoleculeRecord
        {
            int DataCount
            {
                get;
            }

            byte[] DataHash
            {
                get;
            }

            byte[] Hash
            {
                get;
            }

            IMolecule Molecule
            {
                get;
                set;
            }

            IEnumerable<DataValue> GetDataByName(string name);

            IEnumerable<DataValue> GetDataByOrdinal(int n);

            IEnumerable<DataValue> GetDataByType(string type, string subtype);
        }

        public static float[] TranslateCoordinates(float[] xyz_in, float x_shift= 0, float y_shift = 0, float z_shift = 0)
        {
            float[] xyz_out = xyz_in;
            //for (int k = 0; k < xyz_in.GetLength(0); k++)
            int i = 0;
            foreach (float row in xyz_out)
            {
                xyz_out[i] = row;
                //xyz_out[k, 0] = xyz_in[k, 0] + x_shift;
                //xyz_out[k, 1] = xyz_in[k, 1] + y_shift;
                //xyz_out[k, 2] = xyz_in[k, 2] + z_shift;
                i++;
            }
            return xyz_in;
        }

        public static IndigoObject TranslateMolecule(Indigo indigo_obj, IndigoObject mol, float x_shift = 0, float y_shift = 0, float z_shift = 0)
        {
            String mfile = mol.molfile();

            List<String> lines = mfile.Replace("\r", "").Split('\n').ToList();
            int atomcount = int.Parse(lines[3].Substring(0, 3).Trim());
            int bondcount = int.Parse(lines[3].Substring(3, 3).Trim());

            var li = 0;
            List<String> nmolfile = lines.Select(l =>
            {
                if (li >= 4 && li < atomcount + 4)
                {

                    Tuple<float, float, float> coords = Tuple.Create(float.Parse(l.Substring(0, 10).Trim()),
                                                               float.Parse(l.Substring(10, 10).Trim()),
                                                               float.Parse(l.Substring(20, 10).Trim()));
                    string rest_of_the_line = l.Substring(30, l.Length - 30);
                    float new_x = (float)Math.Round(coords.Item1 + x_shift, 4);
                    float new_y = (float)Math.Round(coords.Item2 + y_shift, 4);
                    float new_z = (float)Math.Round(coords.Item3 + z_shift, 4);

                    l = new_x.ToString().PadLeft(10) + new_y.ToString().PadLeft(10) + new_z.ToString().PadLeft(10) + rest_of_the_line;

                }
                li++;
                return l;
            }).ToList();
            String nmol = String.Join("\n", nmolfile);
            return indigo_obj.loadQueryMolecule(nmol);
        }

        public static float GetMaxX(IndigoObject mol)
        {

            float max_x = -9999;
            foreach (IndigoObject atom in mol.iterateAtoms())
            {
                if (atom.xyz()[0] > max_x) { max_x = atom.xyz()[0]; }
            }
            
            return max_x;
        }

        public static PointF GetMaxXPoint(IndigoObject mol)
        {

            float max_x = -9999;
            PointF max_x_point = new PointF(-9999,-9999);
            foreach (IndigoObject atom in mol.iterateAtoms())
            {
                if (atom.xyz()[0] > max_x) 
                { 
                    max_x = atom.xyz()[0];
                    max_x_point.X= atom.xyz()[0];
                    max_x_point.Y = atom.xyz()[1];
                }
            }

            return max_x_point;
        }

        public static PointF GetMaxYPoint(IndigoObject mol)
        {

            float max_y = -9999;
            PointF max_y_point = new PointF(-9999, -9999);
            foreach (IndigoObject atom in mol.iterateAtoms())
            {
                if (atom.xyz()[1] > max_y)
                {
                    max_y = atom.xyz()[1];
                    max_y_point.X = atom.xyz()[0];
                    max_y_point.Y = atom.xyz()[1];
                }
            }

            return max_y_point;
        }

        public static PointF GetMinYPoint(IndigoObject mol)
        {

            float min_y = 9999;
            PointF min_y_point = new PointF(9999, 9999);
            foreach (IndigoObject atom in mol.iterateAtoms())
            {
                if (atom.xyz()[1] < min_y)
                {
                    min_y = atom.xyz()[1];
                    min_y_point.X = atom.xyz()[0];
                    min_y_point.Y = atom.xyz()[1];
                }
            }

            return min_y_point;
        }

        public static PointF GetMinXPoint(IndigoObject mol)
        {

            float min_x = 9999;
            PointF min_x_point = new PointF(9999, 9999);
            foreach (IndigoObject atom in mol.iterateAtoms())
            {
                if (atom.xyz()[0] < min_x)
                {
                    min_x = atom.xyz()[0];
                    min_x_point.X = atom.xyz()[0];
                    min_x_point.Y = atom.xyz()[1];
                }
            }

            return min_x_point;
        }

        public static float GetMinX(IndigoObject mol)
        {

            float min_x = 9999;
            foreach (IndigoObject atom in mol.iterateAtoms())
            {
                if (atom.xyz()[0] < min_x) { min_x = atom.xyz()[0]; }
            }

            return min_x;
        }

        public static float GetMinY(IndigoObject mol)
        {

            float min_y = 9999;
            foreach (IndigoObject atom in mol.iterateAtoms())
            {
                if (atom.xyz()[1] < min_y) { min_y = atom.xyz()[1]; }
            }

            return min_y;
        }

        public static PointF RotatePoint(PointF pointToRotate, PointF centerPoint, double angleInDegrees)
        {
            double angleInRadians = angleInDegrees * (Math.PI / 180);
            double cosTheta = Math.Cos(angleInRadians);
            double sinTheta = Math.Sin(angleInRadians);
            return new PointF
            {
                X =
                    (float)
                    (cosTheta * (pointToRotate.X - centerPoint.X) -
                    sinTheta * (pointToRotate.Y - centerPoint.Y) + centerPoint.X),
                Y =
                    (float)
                    (sinTheta * (pointToRotate.X - centerPoint.X) +
                    cosTheta * (pointToRotate.Y - centerPoint.Y) + centerPoint.Y)
            };
        }

        public static PointF GetCenterPoint(List<PointF> points_in)
        {
            List<Point> dots = new List<Point>();
            float totalX = 0, totalY = 0;
            foreach (PointF p in points_in)
            {
                totalX += p.X;
                totalY += p.Y;
            }
            float centerX = totalX / dots.Count;
            float centerY = totalY / dots.Count;

            return new PointF(centerX, centerY);
        }

        public static PointF Get2DMoleculeCenterPoint(IndigoObject mol)
        {
            float totalX = 0, totalY = 0;
            foreach (IndigoObject atom in mol.iterateAtoms())
            {
                totalX +=atom.xyz()[0];
                totalY +=atom.xyz()[1];

            }
            
            float centerX = totalX / mol.countAtoms();
            float centerY = totalY / mol.countAtoms();

            return new PointF(centerX, centerY);
        }

        public static IndigoObject Rotate2DMolecule(Indigo indigo_obj, IndigoObject mol, float degrees)
        {
            String mfile = mol.molfile();
            PointF molecule_center = Get2DMoleculeCenterPoint(mol);

            List<String> lines = mfile.Replace("\r", "").Split('\n').ToList();
            int atomcount = int.Parse(lines[3].Substring(0, 3).Trim());
            int bondcount = int.Parse(lines[3].Substring(3, 3).Trim());

            var li = 0;
            List<String> nmolfile = lines.Select(l =>
            {
                if (li >= 4 && li < atomcount + 4)
                {

                    Tuple<float, float, float> coords = Tuple.Create(float.Parse(l.Substring(0, 10).Trim()),
                                                               float.Parse(l.Substring(10, 10).Trim()),
                                                               float.Parse(l.Substring(20, 10).Trim()));
                    string rest_of_the_line = l.Substring(30, l.Length - 30);

                    PointF new_point = RotatePoint(new PointF(coords.Item1, coords.Item2), molecule_center, degrees);
                    float new_x = (float)Math.Round(new_point.X,4);
                    float new_y = (float)Math.Round(new_point.Y,4);
                    float new_z = coords.Item3; //not changing that, assuming this is 0 anyway for 2D molecules which is what is usually expected

                    l = new_x.ToString().PadLeft(10) + new_y.ToString().PadLeft(10) + new_z.ToString().PadLeft(10) + rest_of_the_line;

                }
                li++;
                return l;
            }).ToList();
            String nmol = String.Join("\n", nmolfile);
            return indigo_obj.loadQueryMolecule(nmol);
        }

    }
}