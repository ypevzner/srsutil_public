using FDA.SRS.ObjectModel;
using FDA.SRS.Services;
using FDA.SRS.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace UnitTestProject
{
	[TestClass]
	public class FragmentsRegistryTests
	{
		[TestMethod]
		[DeploymentItem(@"registry.sdf")]
        [DeploymentItem(@"Fragments.sdf")]
        public void LoadRegistry()
		{
			FragmentsRegistry reg = new FragmentsRegistry();
			reg.Load();
			var f = reg.Resolve("VI4F0K069V");
			Assert.IsNotNull(f);
			Assert.AreEqual("VI4F0K069V", f.UNII);
			Assert.AreEqual("BZQFBWGGLXLEPQ-REOHCLBHSA-N", f.Molecule.InChIKey);
			Assert.AreEqual(1, f.Connectors.Count);
			Assert.AreEqual(4, f.Connectors.First().Snip.Item1);
			Assert.AreEqual(3, f.Connectors.First().Snip.Item2);
		}
	}
}
