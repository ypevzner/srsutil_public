using System;
using System.Collections.Generic;

using System.Diagnostics;
using System.Linq;

using System.IO;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;

namespace FDA.SRS.Utils
{
	public static class MiscUtils 
	{
        public static string RemoveMolfileBrackets(string mol)
        {
            string returnvalue = "";
            List<string> molfile_lines = mol.Split('\n').ToList();
            foreach (string molfile_line in molfile_lines)
            {
                if (molfile_line.Trim() == "")
                {
                    returnvalue = returnvalue + molfile_line + "\n";
                }
                else if (((!molfile_line.Substring(0,2).Equals("V ")) & (!molfile_line.Substring(0, 2).Equals("M "))) | (molfile_line.Substring(0, 6).Equals("M  CHG")) | (molfile_line.Trim().Substring(molfile_line.Trim().Length - 3, 3).Equals("END")))
                {
                    returnvalue = returnvalue + molfile_line + "\n";
                }

            }
            return returnvalue.Substring(0,returnvalue.Length-1);
        }

        private static MD5 _md5 ;

        static MiscUtils()
        {
            MiscUtils._md5 = MD5.Create();
        }
        /*
        public static string GetMD5String(this string str)
        {
            return MiscUtils.BytesToX2String(str.GetMD5Hash());
        }
        */
        public static string BytesToX2String(byte[] hash)
        {
            StringBuilder stringBuilder = new StringBuilder();
            for (int i = 0; i < (int)hash.Length; i++)
            {
                stringBuilder.Append(hash[i].ToString("x2"));
            }
            return stringBuilder.ToString();
        }
/*
        public static byte[] GetMD5Hash(this string str)
        {
            byte[] numArray;
            lock (MiscUtils._md5)
            {
                numArray = MiscUtils._md5.ComputeHash(Encoding.UTF8.GetBytes(str));
            }
            return numArray;
        }
*/
        public static IEnumerable<T> ForAll<T>(this IEnumerable<T> enumeration, Action<T> action)
        {
            foreach (T t in enumeration)
            {
                action(t);
            }
            return enumeration;
        }

        public static bool EqualsNoCase(this string thisstr, string anotherstr)
        {
            bool flag;
            if ((thisstr != null ? true : anotherstr != null))
            {
                flag = (thisstr == null ? anotherstr.Equals(thisstr, StringComparison.InvariantCultureIgnoreCase) : thisstr.Equals(anotherstr, StringComparison.InvariantCultureIgnoreCase));
            }
            else
            {
                flag = true;
            }
            return flag;
        }

        public static List<string> SplitOnNewLines(this string s)
        {
            string newLine = "\r";
            if (s.Contains(Environment.NewLine))
            {
                newLine = Environment.NewLine;
            }
            else if (s.Contains("\n"))
            {
                newLine = "\n";
            }
            return s.Split(new string[] { newLine }, StringSplitOptions.None).ToList<string>();
        }


    }
}
