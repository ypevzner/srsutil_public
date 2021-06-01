using System;
using System.Collections.Generic;
using System.IO;
using com.epam.indigo;
using FDA.SRS.Utils;
using System.Text;
using System.Text.RegularExpressions;
using System.Linq;


//namespace FDA.SRS.Molecules
namespace FDA.SRS.Utils
{
	public static class MoleculeExtensions
	{
        private static Regex _reStext;

        static MoleculeExtensions()
        {
            MoleculeExtensions._reStext = new Regex("^(A|V|G|M)\\s+");
        }

        public static bool HasSGroup(this SdfRecord sdf, SGroupType sGroupType)
		{
			if ( sGroupType == SGroupType.ANY )
				return true;

			using ( Indigo indigo = new Indigo() )
			using ( IndigoObject mol = indigo.loadMolecule(sdf.Mol) ) {
				if ( (sGroupType & SGroupType.GEN) == SGroupType.GEN && mol.countGenericSGroups() > 0 ||
					 (sGroupType & SGroupType.SUP) == SGroupType.SUP && mol.countSuperatoms() > 0 ||
					 (sGroupType & SGroupType.SRU) == SGroupType.SRU && mol.countRepeatingUnits() > 0 ||
					 (sGroupType & SGroupType.MUL) == SGroupType.MUL && mol.countMultipleGroups() > 0 ||
					 (sGroupType & SGroupType.DAT) == SGroupType.DAT && mol.countDataSGroups() > 0 ||
					 (sGroupType == SGroupType.NON && mol.countGenericSGroups() == 0 && mol.countSuperatoms() == 0 && mol.countRepeatingUnits() == 0 && mol.countMultipleGroups() == 0 && mol.countDataSGroups() == 0) )
					return true;
			}

			return false;
		}

        public static bool IsCorrectMol(string mol)
        {
            bool flag;
            if (!string.IsNullOrWhiteSpace(mol))
            {
                using (StringReader stringReader = new StringReader(mol))
                {
                    if ((stringReader.ReadLine() == null || stringReader.ReadLine() == null ? false : stringReader.ReadLine() != null))
                    {
                        string str = stringReader.ReadLine();
                        if (str != null)
                        {
                            int num = int.Parse(str.Substring(0, 3));
                            if (num != 0)
                            {
                                int num1 = int.Parse(str.Substring(3, 3));
                                int num2 = 0;
                                while (num2 < num)
                                {
                                    if (stringReader.ReadLine() != null)
                                    {
                                        num2++;
                                    }
                                    else
                                    {
                                        flag = false;
                                        return flag;
                                    }
                                }
                                int num3 = 0;
                                while (num3 < num1)
                                {
                                    if (stringReader.ReadLine() != null)
                                    {
                                        num3++;
                                    }
                                    else
                                    {
                                        flag = false;
                                        return flag;
                                    }
                                }
                                while (true)
                                {
                                    string str1 = stringReader.ReadLine();
                                    string str2 = str1;
                                    if (str1 == null)
                                    {
                                        break;
                                    }
                                    if (!MoleculeExtensions._reStext.IsMatch(str2))
                                    {
                                        flag = false;
                                        return flag;
                                    }
                                }
                            }
                            else
                            {
                                flag = false;
                                return flag;
                            }
                        }
                        else
                        {
                            flag = false;
                            return flag;
                        }
                    }
                    else
                    {
                        flag = false;
                        return flag;
                    }
                }
                flag = true;
            }
            else
            {
                flag = false;
            }
            return flag;
        }
        public static bool IsCorrectSdf(string sdf)
        {
            bool flag;
            if (!string.IsNullOrWhiteSpace(sdf))
            {
                Match match = (new Regex("^>\\s+<", RegexOptions.Multiline)).Match(sdf);
                if (match.Success)
                {
                    string str = sdf.Substring(0, match.Index);
                    if ((string.IsNullOrWhiteSpace(str) ? false : !MoleculeExtensions.IsCorrectMol(str)))
                    {
                        flag = false;
                        return flag;
                    }
                }
                flag = (sdf.TrimEnd(new char[0]).EndsWith("$$$$") ? true : false);
            }
            else
            {
                flag = false;
            }
            return flag;
        }

        public static int[] CanonicalNumbers(this SDFUtil.NewMolecule mol)
        {
            int[] numArray;
            Func<int, bool> func = null;
            if ((mol == null || string.IsNullOrWhiteSpace(mol.Mol) ? false : !string.IsNullOrWhiteSpace(mol.InChIAuxInfo)))
            {
                Match match = Regex.Match(mol.InChIAuxInfo, "/N:([0-9,;]+)");
                if (match.Success)
                {
                    int[] array = (
                        from s in match.Groups[1].Value.Split(new char[] { ',', ';' })
                        select int.Parse(s)).ToArray<int>();
                    int[] numArray1 = new int[mol.AtomsCount];
                    for (int i = 0; i < (int)array.Length; i++)
                    {
                        numArray1[array[i] - 1] = i;
                    }
                    List<int> nums = new List<int>();
                    for (int j = (int)array.Length; j < (int)numArray1.Length; j++)
                    {
                        IEnumerable<int> nums1 = Enumerable.Range(1, mol.AtomsCount);
                        Func<int, bool> func1 = func;
                        if (func1 == null)
                        {
                            Func<int, bool> func2 = (int a) => (array.Contains<int>(a) ? false : !nums.Contains(a));
                            Func<int, bool> func3 = func2;
                            func = func2;
                            func1 = func3;
                        }
                        int num = nums1.Where<int>(func1).First<int>();
                        numArray1[num - 1] = j;
                        nums.Add(num);
                    }
                    numArray = numArray1;
                }
                else
                {
                    numArray = null;
                }
            }
            else
            {
                numArray = null;
            }
            return numArray;
        }

        public static SDFUtil.NewMolecule ReorderCanonically(this SDFUtil.NewMolecule mol)
        {
            SDFUtil.NewMolecule newMolecule;
            string m;
            int num = 0;
            int num1;
            bool length;
            if ((mol == null || string.IsNullOrWhiteSpace(mol.Mol) ? false : !string.IsNullOrWhiteSpace(mol.InChIAuxInfo)))
            {
                StringBuilder stringBuilder = new StringBuilder(mol.Mol.Length);
                using (StringReader stringReader = new StringReader(mol.Mol))
                {
                    stringBuilder.AppendLine(stringReader.ReadLine());
                    stringBuilder.AppendLine(stringReader.ReadLine());
                    stringBuilder.AppendLine(stringReader.ReadLine());
                    string str = stringReader.ReadLine();
                    stringBuilder.AppendLine(str);
                    int num2 = int.Parse(str.Substring(0, 3));
                    int num3 = int.Parse(str.Substring(3, 3));
                    string[] strArrays = new string[num2];
                    for (int i = 0; i < num2; i++)
                    {
                        strArrays[i] = stringReader.ReadLine().TrimEnd(new char[0]);
                    }
                    int[] numArray = mol.CanonicalNumbers();
                    string[] strArrays1 = new string[num2];
                    for (int j = 0; j < num2; j++)
                    {
                        strArrays1[numArray[j]] = strArrays[j];
                    }
                    for (int k = 0; k < num2; k++)
                    {
                        stringBuilder.AppendLine(strArrays1[k]);
                    }
                    for (int l = 0; l < num3; l++)
                    {
                        m = stringReader.ReadLine();
                        int[] numArray1 = new int[] { numArray[int.Parse(m.Substring(0, 3)) - 1] + 1, numArray[int.Parse(m.Substring(3, 3)) - 1] + 1 };
                        stringBuilder.AppendFormat("{0,3}{1,3}", numArray1[0], numArray1[1]);
                        stringBuilder.AppendLine(m.Substring(6));
                    }
                    for (m = stringReader.ReadLine(); m != null; m = stringReader.ReadLine())
                    {
                        if (Regex.IsMatch(m, "M\\s+"))
                        {
                            string[] strArrays2 = m.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                            if ((int)strArrays2.Length >= 5)
                            {
                                if (!(new string[] { "CHG", "RAD", "ISO", "RBC", "SUB", "UNS", "RGP" }).Contains<string>(strArrays2[1]) || !int.TryParse(strArrays2[2], out num))
                                {
                                    goto Label1;
                                }
                                length = (int)strArrays2.Length == 3 + num * 2;
                                goto Label0;
                            }
                            Label1:
                            length = false;
                            Label0:
                            if (length)
                            {
                                StringBuilder stringBuilder1 = new StringBuilder(string.Format("M  {0}{1,3}", strArrays2[1], strArrays2[2]));
                                for (int n = 0; n < num; n++)
                                {
                                    if (!int.TryParse(strArrays2[3 + n * 2], out num1))
                                    {
                                        stringBuilder1.AppendFormat(" {0, 3} {1, 3}", strArrays2[3 + n * 2], strArrays2[3 + n * 2 + 1]);
                                    }
                                    else
                                    {
                                        stringBuilder1.AppendFormat(" {0, 3} {1, 3}", numArray[num1 - 1] + 1, strArrays2[3 + n * 2 + 1]);
                                    }
                                }
                                m = stringBuilder1.ToString();
                            }
                        }
                        stringBuilder.AppendLine(m);
                    }
                }
                newMolecule = new SDFUtil.NewMolecule(stringBuilder.ToString());
            }
            else
            {
                newMolecule = null;
            }
            return newMolecule;
        }

        public static string FixMolSpaces(this string mol)
        {
            string str;
            if (!string.IsNullOrWhiteSpace(mol))
            {
                StringBuilder stringBuilder = new StringBuilder(mol.Length);
                using (StringReader stringReader = new StringReader(mol))
                {
                    string p = stringReader.ReadLine();
                    if (p != null)
                    {
                        stringBuilder.AppendLine(p);
                        p = stringReader.ReadLine();
                        if (p != null)
                        {
                            stringBuilder.AppendLine(p);
                            p = stringReader.ReadLine();
                            if (p != null)
                            {
                                stringBuilder.AppendLine(p);
                                string str1 = stringReader.ReadLine();
                                if (p != null)
                                {
                                    Match match = Regex.Match(str1, "(.{3})(.{3})(.*?)999 V2000");
                                    if (match.Success)
                                    {
                                        int num = int.Parse(str1.Substring(0, 3));
                                        int num1 = int.Parse(str1.Substring(3, 3));
                                        stringBuilder.AppendFormat("{0,3}{1,3}", num, num1);
                                        List<string> list = match.Groups[3].Value.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries).ToList<string>();
                                        for (int i = 0; i < 8 - list.Count<string>(); i++)
                                        {
                                            list.Add("0");
                                        }
                                        list.ForEach((string s) => stringBuilder.AppendFormat("{0,3}", s.Trim()));
                                        stringBuilder.AppendLine("999 V2000");
                                        for (int j = 0; j < num; j++)
                                        {
                                            p = stringReader.ReadLine();
                                            List<string> value = p.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries).ToList<string>();
                                            if ((value[2].Last<char>() == '.' ? false : !char.IsDigit(value[2].Last<char>())))
                                            {
                                                match = Regex.Match(value[2], "([0-9.]*)(\\S+)");
                                                if (match.Success)
                                                {
                                                    value[2] = match.Groups[1].Value;
                                                    value.Insert(3, match.Groups[2].Value);
                                                }
                                            }
                                            stringBuilder.AppendFormat("{0,10:f}{1,10:f}{2,10:f}", float.Parse(value[0]), float.Parse(value[1]), float.Parse(value[2]));
                                            stringBuilder.AppendFormat(" {0,-3}{1,2}", value[3], value[4]);
                                            for (int k = 0; k < 16 - value.Count; k++)
                                            {
                                                value.Add("0");
                                            }
                                            for (int l = 5; l < value.Count; l++)
                                            {
                                                stringBuilder.AppendFormat("{0,3}", value[l]);
                                            }
                                            stringBuilder.AppendLine();
                                        }
                                        for (int m = 0; m < num1; m++)
                                        {
                                            p = stringReader.ReadLine();
                                            List<string> strs = p.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries).ToList<string>();
                                            for (int n = 0; n < 7 - strs.Count; n++)
                                            {
                                                strs.Add("0");
                                            }
                                            for (int o = 0; o < strs.Count; o++)
                                            {
                                                stringBuilder.AppendFormat("{0, 3}", strs[o]);
                                            }
                                            stringBuilder.AppendLine();
                                        }
                                        for (p = stringReader.ReadLine(); p != null; p = stringReader.ReadLine())
                                        {
                                            if (Regex.IsMatch(p, "M\\s+END\\s*$"))
                                            {
                                                p = "M  END";
                                            }
                                            stringBuilder.AppendLine(p);
                                        }
                                    }
                                    else
                                    {
                                        str = null;
                                        return str;
                                    }
                                }
                                else
                                {
                                    str = null;
                                    return str;
                                }
                            }
                            else
                            {
                                str = null;
                                return str;
                            }
                        }
                        else
                        {
                            str = null;
                            return str;
                        }
                    }
                    else
                    {
                        str = null;
                        return str;
                    }
                }
                str = stringBuilder.ToString();
            }
            else
            {
                str = null;
            }
            return str;
        }
        public static string MolReplaceProgramName(this string mol, string name, bool preserveRemainder)
        {
            string str;
            if (!string.IsNullOrWhiteSpace(mol))
            {
                if (name.Length < 8)
                {
                    name = name.PadRight(8, ' ');
                }
                else if (name.Length > 8)
                {
                    name = name.Substring(0, 8);
                }
                StringBuilder stringBuilder = new StringBuilder(mol.Length);
                using (StringReader stringReader = new StringReader(mol))
                {
                    stringBuilder.AppendLine(stringReader.ReadLine());
                    string str1 = stringReader.ReadLine();
                    str1 = string.Concat(str1.Substring(0, 2), name, (preserveRemainder ? str1.Substring(10) : ""));
                    stringBuilder.AppendLine(str1);
                    stringBuilder.Append(stringReader.ReadToEnd());
                }
                str = stringBuilder.ToString();
            }
            else
            {
                str = null;
            }
            return str;
        }

        public static string MolReplaceProgramName(this string mol, string name)
        {
            return mol.MolReplaceProgramName(name, true);
        }

    }
}
