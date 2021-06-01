using com.epam.indigo;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace FDA.SRS.Utils
{
    public class SdfRecord : SDFUtil.MoleculeRecord
    {
        public override int DataCount
        {
            get
            {
                return 0;
            }
        }

        public IEnumerable<string> this[string name]
        {
            get
            {
                IEnumerable<string> strs;
                IEnumerable<string> value;
                if (this.m_Properties != null)
                {
                    KeyValuePair<string, List<string>> keyValuePair = this.m_Properties.FirstOrDefault<KeyValuePair<string, List<string>>>((KeyValuePair<string, List<string>> kv) => string.Compare(kv.Key, name, true) == 0);
                    if (keyValuePair.Equals(new KeyValuePair<string, List<string>>()))
                    {
                        value = null;
                    }
                    else
                    {
                        value = keyValuePair.Value;
                    }
                    strs = value;
                }
                else
                {
                    strs = null;
                }
                return strs;
            }
        }

        public IEnumerable<string> this[Regex regex]
        {
            get
            {
                IEnumerable<string> strs;
                IEnumerable<string> strs1;
                Func<KeyValuePair<string, List<string>>, bool> func = null;
                if (this.m_Properties != null)
                {
                    List<string> strs2 = new List<string>();
                    Dictionary<string, List<string>> mProperties = this.m_Properties;
                    Func<KeyValuePair<string, List<string>>, bool> func1 = func;
                    if (func1 == null)
                    {
                        Func<KeyValuePair<string, List<string>>, bool> func2 = (KeyValuePair<string, List<string>> kvp) => regex.IsMatch(kvp.Key);
                        Func<KeyValuePair<string, List<string>>, bool> func3 = func2;
                        func = func2;
                        func1 = func3;
                    }
                    foreach (List<string> strs3 in mProperties.Where<KeyValuePair<string, List<string>>>(func1).Select<KeyValuePair<string, List<string>>, List<string>>((KeyValuePair<string, List<string>> kvp) => kvp.Value))
                    {
                        strs2.AddRange(strs3);
                    }
                    if (strs2.Count > 0)
                    {
                        strs1 = strs2;
                    }
                    else
                    {
                        strs1 = null;
                    }
                    strs = strs1;
                }
                else
                {
                    strs = null;
                }
                return strs;
            }
        }

        public string Mol
        {
            get
            {
                string mol;
                if (base.Molecule == null)
                {
                    mol = null;
                }
                else
                {
                    mol = base.Molecule.Mol;
                }
                return mol;
            }
            set
            {
                base.Molecule = new SDFUtil.NewMolecule(value);
            }
        }

        public SdfRecord()
        {
        }

        public SdfRecord(string mol, IEnumerable<Tuple<string, string>> data)
        {
            this.Mol = mol;
            if (data != null)
            {
                data.ForAll<Tuple<string, string>>((Tuple<string, string> p) => this.AddField(p.Item1, p.Item2));
            }
        }

        public SdfRecord AddField(string name, string value)
        {
            if (this.m_Properties == null)
            {
                this.m_Properties = new Dictionary<string, List<string>>();
            }
            if (!this.m_Properties.ContainsKey(name))
            {
                this.m_Properties.Add(name, new List<string>());
            }
            this.m_Properties[name].Add(value);
            return this;
        }

        public SdfRecord AddFieldValues(string name, IEnumerable<string> values)
        {
            foreach (string value in values)
            {
                this.AddField(name, value);
            }
            return this;
        }

        public static SdfRecord FromString(string sdf_or_mol)
        {
            SdfRecord sdfRecord;
            if (!string.IsNullOrEmpty(sdf_or_mol))
            {
                using (StringReader stringReader = new StringReader(sdf_or_mol))
                {
                    using (SdfReader sdfReader = new SdfReader(stringReader))
                    {
                        SdfRecord sdfRecord1 = sdfReader.ReadSDFRecord();
                        if (sdfReader.ReadSDFRecord() != null)
                        {
                            throw new Exception("String contains more than one SDF records");
                        }
                        sdfRecord = sdfRecord1;
                    }
                }
            }
            else
            {
                sdfRecord = null;
            }
            return sdfRecord;
        }

        public string GetFieldValue(string name)
        {
            string str;
            if (this[name] == null)
            {
                str = null;
            }
            else
            {
                str = string.Join(Environment.NewLine, this[name]);
            }
            return str;
        }

        public bool HasField(string name)
        {
            return this[name] != null;
        }

        private static void hashSdf(IndigoObject sdf, out byte[] hash, out byte[] str_hash, out byte[] data_hash)
        {
            MD5 mD5 = MD5.Create();
            str_hash = mD5.ComputeHash(Encoding.UTF8.GetBytes(sdf.molfile().TrimEnd(new char[0])));
            data_hash = mD5.ComputeHash(Encoding.UTF8.GetBytes(string.Join("",
                from IndigoObject p in sdf.iterateProperties()
                select string.Concat(p.name().Trim(), p.rawData().Trim()) into p
                orderby p
                select p)));
            hash = mD5.ComputeHash(str_hash.Concat<byte>(data_hash).ToArray<byte>());
        }

        public static bool IsCorrect(string sdf)
        {
            return MoleculeExtensions.IsCorrectSdf(sdf);
        }

        public override string ToString()
        {
            StringBuilder stringBuilder = new StringBuilder((string.IsNullOrEmpty(this.Mol) ? "" : this.Mol.Replace("\n$$$$", "")));
            if (this.m_Properties != null)
            {
                foreach (string key in this.m_Properties.Keys)
                {
                    if (this.m_Properties[key].Count > 0)
                    {
                        stringBuilder.AppendFormat("> <{0}>\n", key);
                        foreach (string item in this.m_Properties[key])
                        {
                            if (!string.IsNullOrEmpty(item))
                            {
                                stringBuilder.AppendLine(item);
                            }
                        }
                        stringBuilder.AppendLine();
                    }
                }
            }
            stringBuilder.AppendLine("$$$$");
            return stringBuilder.ToString();
        }
    }
}