using FDA.SRS.ObjectModel;
using FDA.SRS.Processing;
using FDA.SRS.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using System.Xml.XPath;

namespace UnitTestProject
{
	[TestClass]
	public class MiscTests
	{
		[TestMethod]
		public void RegExWithLineBreaks()
		{
			string s1 = @"</STRUCTURAL_REPEAT_UNIT_AMOUNT
_TYPE>";

			string s2 = @"</STRUCTURAL_REPEAT_UNIT_AMOUNT_TYPE>";

			string _s1 = Regex.Replace(s1, @"</?.*?\r?\n.*?>", m => {
				string s = m.Value
					.Replace("\r", "")
					.Replace("\n", "");
				return s;
			});
			Assert.AreEqual(_s1, s2);
		}

		[TestMethod]
		public void CorrectAuthorsTest()
		{
			string[] authors_raw = { @"(A.GRAY) G.L. NESOM", @"(WILLD. EX ROEM. & SCHULT.) ZUCC.", @"(MEILE ET AL.) MASCO ET AL." };
			string[] authors_correct = { @"(A.Gray) G.L. Nesom", @"(Willd. ex Roem. & Schult.) Zucc.", @"(Meile et al.) Masco et al." };

			for ( int i = 0; i < authors_raw.Length; i++ ) {
				string a = authors_raw[i].CasifyAuthors();
				Assert.AreEqual(a, authors_correct[i]);
			}
		}

        [TestMethod]
        public void NullAverageAmountSPLShouldHaveNoNumeratorValue() {
            Amount amt = new Amount();
            amt.High = 10;
            amt.Low = 1;

            XElement xml = amt.SPL;

            String rawXML = xml.ToString();

            Assert.IsTrue(rawXML.Split('\n').Filter(l=>l.Contains("<numerator"))
                                            .Filter(l=>l.Contains("value"))
                                            .ToHashSet()
                                            .Count == 0);

        }


        [TestMethod]
        public void NullLowAmountSPLShouldHaveNoLowValue() {
            Amount amt = new Amount();
            amt.High = 10;

            XElement xml = amt.SPL;

            String rawXML = xml.ToString();

            Assert.IsTrue(rawXML.Split('\n').Filter(l => l.Contains("low"))
                                            .ToHashSet()
                                            .Count == 0);

        }
    }
}
