using FDA.SRS.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Xml.Linq;

namespace UnitTestProject
{
	[TestClass]
	[DeploymentItem(@"..\..\Resources\Spl\")]
	public class SplTests
	{
		[TestMethod]
		public void SplExtensionsTest()
		{

            XDocument xSpl = XDocument.Load("spl.xml");
			Guid predefined = Guid.Parse("12345678-8198-de45-02b8-6332e7720dca");

			// Hash
			Guid hash = xSpl.SplHash();
			Assert.AreEqual(Guid.Parse("2d2e01e9-8198-de45-02b8-6332e7720dca"), hash);

			xSpl.SplHash(predefined);
			hash = xSpl.SplHash();
			Assert.AreEqual(predefined, hash);

			// setId
			Guid setId = xSpl.SplSetId();
			Assert.AreEqual(Guid.Parse("b81a90bc-912f-4046-906c-c24ae9358dfc"), setId);

			xSpl.SplSetId(predefined);
			setId = xSpl.SplSetId();
			Assert.AreEqual(predefined, setId);

			// docId
			Guid docId = xSpl.SplDocId();
			Assert.AreEqual(Guid.Parse("12f85f46-acba-4679-9340-f4441e04d5ab"), docId);

			xSpl.SplDocId(predefined);
			docId = xSpl.SplDocId();
			Assert.AreEqual(predefined, docId);

			// UNII
			string unii = xSpl.SplUNII();
			Assert.AreEqual("490D9F069T", unii);

			xSpl.SplUNII("01234ABCDE");
			unii = xSpl.SplUNII();
			Assert.AreEqual("01234ABCDE", unii);
		}

		[TestMethod]
		[DeploymentItem(@"..\..\Resources\StructurallyDiverse\ZSH041A5VN.spl.xml")]
		public void SplExtensionsTests()
		{
			var xdoc = XDocument.Load("ZSH041A5VN.spl.xml");
			Assert.AreEqual(Guid.Parse("bead5ed2-f72d-45f7-a146-62ad261b022e"), xdoc.SplDocId());
			Assert.AreEqual(1, xdoc.SplVersion());
			Assert.AreEqual("ZSH041A5VN", xdoc.SplUNII());
			Assert.AreEqual(Guid.Parse("f0175a3d-4e57-fda1-3b0e-76acecf80b99"), xdoc.SplHash());
		}
	}
}
