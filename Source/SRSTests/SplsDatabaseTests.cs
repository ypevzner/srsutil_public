using FDA.SRS.Database;
using FDA.SRS.Database.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Xml.Linq;

namespace UnitTestProject
{
	[TestClass]
	[DeploymentItem(@"x86\", @"x86\")]
	[DeploymentItem(@"x64\", @"x64\")]
	public class SplDatabaseTests
	{
		[TestMethod]
		public void GzipUngzip()
		{
			SQLiteSplDoc doc = new SQLiteSplDoc();
			string s1 = @"</STRUCTURAL_REPEAT_UNIT_AMOUNT_TYPE>";
			doc.Err(s1);
			Assert.AreEqual(s1, doc.Err());
		}

		[TestMethod]
		[DeploymentItem(@"..\..\Resources\Misc\S1O619E29M.xml")]
		public void SplsDatabaseWriteRead()
		{
			SplsDatabase db = new SplsDatabase("spls.sqlite");
			var xdoc = XDocument.Load("S1O619E29M.xml");
			int id = db.AddDoc(null, null, xdoc, null, null, null, null);
			var doc = db.GetDoc(id);
			Assert.IsNotNull(doc);

			XDocument docXml = doc.SplXml();
			Assert.IsNotNull(docXml);

			// File.WriteAllText("S1O619E29M.db.xml", docXml.ToString());
			Assert.AreEqual(xdoc.ToString(), docXml.ToString());
		}
	}
}
