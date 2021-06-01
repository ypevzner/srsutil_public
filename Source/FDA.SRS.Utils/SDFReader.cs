using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;

namespace FDA.SRS.Utils
{
    public class SdfReader : IDisposable
    {
        public static Func<string, IEnumerable<string>> DefaultSplitter;

        private Stream m_Stream;

        private TextReader m_Reader;

        private IDictionary<string, string> m_FieldsMap;

        private Dictionary<string, Func<string, IEnumerable<string>>> m_Splitters;

        private int _counter;

        private Regex _rxRecEnd = new Regex("^\\$\\$\\$\\$\\s*$");

        private Regex _rxMEnd = new Regex("^M\\s+END");

        private Regex _rxPropName = new Regex("^>[^<]*<([^/][^>]+)>");

        private SdfReader.SdfEnumerable _enumerable;

        public int Counter
        {
            get
            {
                return this._counter;
            }
        }

        public IDictionary<string, string> FieldsMap
        {
            get
            {
                return this.m_FieldsMap;
            }
            set
            {
                this.m_FieldsMap = value;
            }
        }

        public ICollection<Func<SdfRecord, int, string, string>> LineFilters
        {
            get;
            set;
        }

        public IEnumerable<SdfRecord> Records
        {
            get
            {
                SdfReader.SdfEnumerable sdfEnumerables = this._enumerable;
                if (sdfEnumerables == null)
                {
                    SdfReader.SdfEnumerable sdfEnumerables1 = new SdfReader.SdfEnumerable(this);
                    SdfReader.SdfEnumerable sdfEnumerables2 = sdfEnumerables1;
                    this._enumerable = sdfEnumerables1;
                    sdfEnumerables = sdfEnumerables2;
                }
                return sdfEnumerables;
            }
        }

        public Dictionary<string, Func<string, IEnumerable<string>>> Splitters
        {
            get
            {
                return this.m_Splitters;
            }
            set
            {
                this.m_Splitters = value;
            }
        }

        static SdfReader()
        {
            SdfReader.DefaultSplitter = (string s) =>
                from _s in s.Split(new char[] { '\n' }, StringSplitOptions.RemoveEmptyEntries)
                select _s.Trim();
        }

        public SdfReader(Stream s) : this(s, null)
        {
        }

        public SdfReader(Stream s, Encoding enc)
        {
            this.m_Stream = s;
            if (!s.CanRead)
            {
                throw new Exception("Could not read the given SDF stream!");
            }
            this.m_Reader = (enc != null ? new StreamReader(s, enc) : new StreamReader(s, Encoding.GetEncoding(1252)));
        }

        public SdfReader(TextReader r)
        {
            this.m_Reader = r;
        }

        public SdfReader(string filename) : this(filename, null)
        {
        }

        public SdfReader(string filename, Encoding enc) : this(new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read), enc ?? Encoding.GetEncoding(1252))
        {
        }

        private void addSdfField(SdfRecord record, string key, string value)
        {
            string str = key;
            if (this.m_FieldsMap != null)
            {
                str = this.mapPropName(str);
            }
            if (this.m_Splitters != null)
            {
                KeyValuePair<string, Func<string, IEnumerable<string>>> keyValuePair = this.m_Splitters.FirstOrDefault<KeyValuePair<string, Func<string, IEnumerable<string>>>>((KeyValuePair<string, Func<string, IEnumerable<string>>> kv) => string.Compare(kv.Key, str, true) == 0);
                if (!keyValuePair.Equals(new KeyValuePair<string, Func<string, IEnumerable<string>>>()))
                {
                    record.AddFieldValues(str, keyValuePair.Value(value));
                }
                else
                {
                    record.AddField(str, value);
                }
            }
            else
            {
                record.AddField(str, value);
            }
        }

        public void Dispose()
        {
            if (this.m_Stream != null)
            {
                this.m_Stream.Close();
            }
            this.m_Reader.Close();
        }

        private string mapPropName(string propname)
        {
            foreach (string key in this.m_FieldsMap.Keys)
            {
                if (Regex.IsMatch(propname, key, RegexOptions.IgnoreCase))
                {
                    propname = this.m_FieldsMap[key];
                }
            }
            return propname;
        }

        private SdfRecord readSDFRecord(IEnumerable<Func<SdfRecord, int, string, string>> lineFilters)
        {
            SdfRecord sdfRecord;
            SdfRecord str = new SdfRecord();
            StringBuilder stringBuilder = new StringBuilder();
            string value = null;
            bool flag = false;
            string str1 = this.m_Reader.ReadLine();
            if (str1 != null)
            {
                int num = 0;
                while (str1 != null)
                {
                    if (lineFilters != null)
                    {
                        bool flag1 = false;
                        foreach (Func<SdfRecord, int, string, string> lineFilter in lineFilters)
                        {
                            int num1 = num;
                            num = num1 + 1;
                            str1 = lineFilter(str, num1, str1);
                            if (str1 == null)
                            {
                                str1 = this.m_Reader.ReadLine();
                                flag1 = true;
                                break;
                            }
                        }
                        if (flag1)
                        {
                            continue;
                        }
                    }
                    if ((flag ? true : !this._rxMEnd.IsMatch(str1)))
                    {
                        Match match = this._rxPropName.Match(str1);
                        if (match.Success)
                        {
                            if (!flag)
                            {
                                str.Mol = stringBuilder.ToString();
                                flag = true;
                            }
                            if (value != null)
                            {
                                this.addSdfField(str, value, stringBuilder.ToString().Trim());
                            }
                            value = match.Groups[1].Value;
                            stringBuilder.Clear();
                        }
                        else if (!this._rxRecEnd.IsMatch(str1))
                        {
                            stringBuilder.AppendLine(str1);
                        }
                        else
                        {
                            if (!flag)
                            {
                                str.Mol = stringBuilder.ToString();
                            }
                            else if (value != null)
                            {
                                this.addSdfField(str, value, stringBuilder.ToString().Trim());
                                value = null;
                            }
                            break;
                        }
                    }
                    else
                    {
                        stringBuilder.AppendLine(str1);
                        str.Mol = stringBuilder.ToString();
                        flag = true;
                        stringBuilder.Clear();
                    }
                    str1 = this.m_Reader.ReadLine();
                }
                if (value != null)
                {
                    this.addSdfField(str, value, stringBuilder.ToString().Trim());
                }
                if ((str.Molecule != null ? true : str.Properties.Count != 0))
                {
                    this._counter++;
                    sdfRecord = str;
                }
                else
                {
                    sdfRecord = null;
                }
            }
            else
            {
                sdfRecord = null;
            }
            return sdfRecord;
        }

        public SdfRecord ReadSDFRecord()
        {
            return this.readSDFRecord(this.LineFilters);
        }

        public enum Options
        {
            SplitMultilineValues
        }

        private class SdfEnumerable : IEnumerable<SdfRecord>, IEnumerable
        {
            private SdfReader _reader;

            private SdfReader.SdfEnumerator _enumerator;

            public SdfEnumerable(SdfReader reader)
            {
                this._reader = reader;
            }

            private IEnumerator<SdfRecord> getEnumerator()
            {
                if ((this._enumerator == null ? false : this._reader._counter != 0))
                {
                    throw new InvalidOperationException("Cannot create new enumerator on a reader in non-initial state");
                }
                SdfReader.SdfEnumerator sdfEnumerator = this._enumerator;
                if (sdfEnumerator == null)
                {
                    SdfReader.SdfEnumerator sdfEnumerator1 = new SdfReader.SdfEnumerator(this._reader);
                    SdfReader.SdfEnumerator sdfEnumerator2 = sdfEnumerator1;
                    this._enumerator = sdfEnumerator1;
                    sdfEnumerator = sdfEnumerator2;
                }
                return sdfEnumerator;
            }

            public IEnumerator<SdfRecord> GetEnumerator()
            {
                return this.getEnumerator();
            }

            IEnumerator System.Collections.IEnumerable.GetEnumerator()
            {
                return this.getEnumerator();
            }
        }

        private class SdfEnumerator : IEnumerator<SdfRecord>, IDisposable, IEnumerator
        {
            private SdfReader _reader;

            private SdfRecord _record;

            public SdfRecord Current
            {
                get
                {
                    return this._record;
                }
            }

            object System.Collections.IEnumerator.Current
            {
                get
                {
                    return this._record;
                }
            }

            internal SdfEnumerator(SdfReader r)
            {
                this._reader = r;
            }

            public void Dispose()
            {
            }

            public bool MoveNext()
            {
                bool flag;
                lock (this._reader)
                {
                    this._record = this._reader.ReadSDFRecord();
                    flag = this._record != null;
                }
                return flag;
            }

            public void Reset()
            {
                throw new InvalidOperationException();
            }
        }
    }
}
