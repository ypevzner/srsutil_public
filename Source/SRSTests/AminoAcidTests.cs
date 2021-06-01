using System.Linq;
using FDA.SRS.ObjectModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTestProject
{
	[TestClass]
	public class AminoAcidTests
	{
		[TestMethod]
		public void CheckAllAminoAcids()
		{
			foreach ( char l in AminoAcids.AA_Letters.Where(c => char.IsUpper(c) && c != 'X') ) {
				var c = AminoAcids.GetCompoundByLetter(l);
				Assert.IsNotNull(c);
				Assert.IsNotNull(c.InChI);
			}
		}
	}
}
