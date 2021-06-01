using FDA.SRS.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Diagnostics;
using System.Xml.Linq;
using System.Xml.XPath;

namespace FDA.SRS.Processing
{
	public static class SRSReadingUtils
	{
		public static void readElement(XElement x, string xpath, Func<string, bool> validator, Action<string> setter, string unii)
		{
			XElement xel = x.XPathSelectElement(xpath);
			if ( xel != null && !String.IsNullOrWhiteSpace(xel.Value) ) {
				string v = xel.Value.Trim().ToUpper();
				if ( validator != null && !validator(v) )
					TraceUtils.WriteUNIITrace(TraceEventType.Warning, null, null, "Invalid {0}: {1}", xpath, v);
				else
					setter(v);
			}
		}
        public static void readJsonElement(JToken o, string jpath, Func<string, bool> validator, Action<string> setter, string unii){
            JToken tok = o.SelectToken(jpath);
            if (tok != null && !String.IsNullOrWhiteSpace(tok.ToString())){
                string v = tok.ToString().Trim().ToUpper();
                if (validator != null && !validator(v))
                {
                    TraceUtils.WriteUNIITrace(TraceEventType.Error, null, null, "Invalid {0}: {1}", jpath, v);
                    if (jpath == "structuralModificationType")
                    {
                        throw new SrsException("modification", "Invalid " + jpath + ": " + v);
                    }      
                }
                else
                    setter(v);
            }
        }
        public static void readSingleElement(string v, string apath, Func<string, bool> validator, Action<string> setter, string unii){
            if (v != null && !String.IsNullOrWhiteSpace(v)){
                v = v.Trim().ToUpper();
                if (validator != null && !validator(v))
                    TraceUtils.WriteUNIITrace(TraceEventType.Warning, null, null, "Invalid {0}: {1}", apath, v);
                else
                    setter(v);
            }
        }
    }
}
