using System;
using System.Linq;
using System.Text;
using System.IO;
using System.Security.Cryptography;

namespace FDA.SRS.Utils
{
    public static class HashUtils
    {
        private static MD5 _md5;

        static HashUtils()
        {
            HashUtils._md5 = MD5.Create();
        }

        public static string BytesToX2String(byte[] hash)
        {
            StringBuilder stringBuilder = new StringBuilder();
            for (int i = 0; i < (int)hash.Length; i++)
            {
                stringBuilder.Append(hash[i].ToString("x2"));
            }
            return stringBuilder.ToString();
        }

        public static byte[] GetFileMD5Hash(string file)
        {
            byte[] numArray;
            lock (HashUtils._md5)
            {
                using (FileStream fileStream = File.Open(file, FileMode.Open, FileAccess.Read))
                {
                    numArray = HashUtils._md5.ComputeHash(fileStream);
                }
            }
            return numArray;
        }

        public static byte[] GetMD5Hash(this string str)
        {
            byte[] numArray;
            lock (HashUtils._md5)
            {
                numArray = HashUtils._md5.ComputeHash(Encoding.UTF8.GetBytes(str));
            }
            return numArray;
        }

        public static byte[] GetMD5Hash(this byte[] buf)
        {
            byte[] numArray;
            lock (HashUtils._md5)
            {
                numArray = HashUtils._md5.ComputeHash(buf);
            }
            return numArray;
        }

        public static string GetMD5String(this string str)
        {
            return HashUtils.BytesToX2String(str.GetMD5Hash());
        }

        public static string GetMD5String(this byte[] buf)
        {
            return HashUtils.BytesToX2String(buf.GetMD5Hash());
        }

        public static int HashIntegers(params int[] args)
        {
            int num = 0;
            args.ForAll<int>((int i) => num ^= i);
            return num;
        }

        public static string Hex2String(byte[] data)
        {
            string str = string.Concat(Array.ConvertAll<byte, string>(data, (byte x) => x.ToString("X2")));
            return str;
        }

        public static byte[] String2Hex(string data)
        {
            byte[] array = (
                from x in Enumerable.Range(0, data.Length)
                where x % 2 == 0
                select Convert.ToByte(data.Substring(x, 2), 16)).ToArray<byte>();
            return array;
        }
    }
}

