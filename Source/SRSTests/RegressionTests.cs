using FDA.SRS.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;

namespace UnitTestProject
{
	[TestClass]
	public class RegressionTests
	{
		[TestMethod]
		[DeploymentItem(@"..\..\..\FDA.SRS.Utils\Resources\SubstanceIndexing.dat")]
		public void SubstanceIndexing()
		{
			SubstanceIndexing ind = new SubstanceIndexing(Path.Combine(Environment.CurrentDirectory, "SubstanceIndexing.dat"));
			// UNII|Primary Name|Hash Code|Link|SetId|Version Number|Load Time|Citation
			// KI8GVV5BFB||20e9e844-1e3c-9506-d65d-bc7add731f84|90ab518e-b944-4e9e-8c66-f5e6d0bdecd8|4b1461c4-fbef-4dcd-9758-21f971e17dcf|1|20160204124041|Scorpaena cardinalis Solander &amp; Richardson
			var ent = ind.GetExisting("KI8GVV5BFB");
			Assert.IsNotNull(ent);
			Assert.IsNull(ent.PrimaryName);
			Assert.AreEqual(Guid.Parse("2b0dc0b2-10c2-08b3-ecfa-4ef49e849195"), ent.Hash);
			Assert.AreEqual("Scorpaena cardinalis Solander and Richardson, 1842", ent.Citation);
		}
		
	}
}
