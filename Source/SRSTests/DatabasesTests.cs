using FDA.SRS.Database;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTestProject
{
	[TestClass]
	public class DatabasesTests
	{
		[TestMethod]
		public void CreateNCBIDatabaseTest()
		{
			NcbiDatabase db = new NcbiDatabase() { Database = "NCBI.sqlite", DownloadUrl = "ftp://ftp.ncbi.nih.gov/pub/taxonomy/taxdmp.zip", LocalPath = "taxdmp.zip" };
			// db.CreateDatabase(true, );
		}
	}
}
