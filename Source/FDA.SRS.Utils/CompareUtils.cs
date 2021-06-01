using Microsoft.XmlDiffPatch;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace FDA.SRS.Utils
{
	public class CompareUtils
    {
        private static string[] _redFlags = { "@code" };

        public static XDocument SplDiff(string splFile, string refSplFile, bool fullDiff)
        {
            XmlDiff xmlDiff = new XmlDiff();
            xmlDiff.Algorithm = XmlDiffAlgorithm.Precise;

			using ( TempFile t = new TempFile() ) {
				using ( XmlWriter diffgramWriter = new XmlTextWriter(t.FullPath, Encoding.UTF8) ) {
					if ( !xmlDiff.Compare(refSplFile, splFile, false, diffgramWriter) && fullDiff )
						return null;
				}

				string diffSpl = File.ReadAllText(t.FullPath);
				foreach ( string flag in _redFlags ) {
					if ( diffSpl.Contains(flag) )
						return XDocument.Parse(diffSpl);
				}

				return null;
			}
        }
    }
}
