using FDA.SRS.ObjectModel;
using FDA.SRS.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;

namespace UnitTestProject
{
	[TestClass]
	[DeploymentItem(@"registry.sdf")]
    [DeploymentItem(@"Fragments.sdf")]
    public class ResolverTests
	{
		[TestMethod]
		public void RegistryLoadTest()
		{
			FragmentFactory factory = new FragmentFactory();

			// Resolved from name
			var r = factory.Resolve("cysteine disulfide");
			Assert.IsNotNull(r);
			Assert.IsNotNull(r.Molecule);
			Assert.IsTrue(r.IsLinker);

			// Cannot be resolved from name and synonyms are not mentioned in config
			r = factory.Resolve("cys-cys");
			Assert.IsNotNull(r);

			r = factory.Resolve("SZB83O1W42");
			Assert.IsNotNull(r);
			Assert.IsNotNull(r.Molecule);
			Assert.IsTrue(r.IsModification);
		}

        //This test is broken, and appears to be due to the canonicalization
        //algorithm not working as expected. This will have some side effects elsewhere
        //as well
		[TestMethod]
		public void CysCysCanonicalNumsTest()
		{
			FragmentFactory factory = new FragmentFactory();

			// registry.sdf
			// Resolved from name
			var r = factory.Resolve("cysteine disulfide");
			Assert.IsNotNull(r);
			Assert.IsNotNull(r.Molecule);
			Assert.IsTrue(r.IsLinker);
            Fragment.Connector c1 = r.Connectors.First();
            Fragment.Connector c2 = r.Connectors.Last();
            
            Assert.AreEqual(8, c1.Snip.Item1);
            Assert.AreEqual(6, c1.Snip.Item2);
            Assert.AreEqual(7, c2.Snip.Item1);
            Assert.AreEqual(5, c2.Snip.Item2);
			
		}
	}
}
