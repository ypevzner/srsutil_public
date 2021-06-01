using FDA.SRS.ObjectModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using FDA.SRS.Utils;

namespace UnitTestProject
{
	[TestClass]
	[DeploymentItem(@"..\..\Resources\Chemicals\")]
	[DeploymentItem(@"..\..\Resources\Misc\")]
	[DeploymentItem(@"x64\", @"x64\")]
	[DeploymentItem(@"x86\", @"x86\")]
	public class CompoundsTests
	{
		[TestMethod]
		public void CompoundProperties()
		{
			string mol_aspirin = @"


 13 13  0  0000  0  0  0  0  0999 V2000
    2.3031   -1.9922    0.0000 O   0  0  0  0  0  0  0  0  0  0  0
    5.7579    0.0000    0.0000 O   0  0  0  0  0  0  0  0  0  0  0
    3.4547    0.0000    0.0000 O   0  0  0  0  0  0  0  0  0  0  0
    1.1516   -3.9902    0.0000 O   0  0  0  0  0  0  0  0  0  0  0
    3.4547   -2.6601    0.0000 C   0  0  0  0  0  0  0  0  0  0  0
    4.6063   -1.9922    0.0000 C   0  0  0  0  0  0  0  0  0  0  0
    3.4547   -3.9902    0.0000 C   0  0  0  0  0  0  0  0  0  0  0
    5.7579   -2.6601    0.0000 C   0  0  0  0  0  0  0  0  0  0  0
    4.6063   -4.6524    0.0000 C   0  0  0  0  0  0  0  0  0  0  0
    5.7579   -3.9902    0.0000 C   0  0  0  0  0  0  0  0  0  0  0
    4.6063   -0.6622    0.0000 C   0  0  0  0  0  0  0  0  0  0  0
    1.1516   -2.6601    0.0000 C   0  0  0  0  0  0  0  0  0  0  0
    0.0000   -1.9922    0.0000 C   0  0  0  0  0  0  0  0  0  0  0
  1  5  1  0
  1 12  1  0
  2 11  1  0
  3 11  2  0
  4 12  2  0
  5  6  1  0
  5  7  2  0
  6  8  2  0
  6 11  1  0
  7  9  1  0
  8 10  1  0
  9 10  2  0
 12 13  1  0
M  END
";
			Compound c = new Compound(mol_aspirin);
			Assert.IsNotNull(c.InChI);
			Assert.IsNotNull(c.InChIKey);
			Assert.IsNotNull(c.Mol);
			Assert.IsNotNull(c.SMILES);
		}

		// [TestMethod]
		public void CanonicalNumbers_OnAllOfChemicals()
		{
			Directory.CreateDirectory("diffs");
			string file = @"..\..\..\..\Data\public_chemicals_V3000_4_27_16.sdf";
			int nNonCan = 0, nNonEqual = 0, nExcept = 0;

			using ( SdfReader r = new SdfReader(file) ) {
				r.Records
					.AsParallel()
					.ForAll(sdf => {
						try {
							SDFUtil.NewMolecule orig = sdf.Molecule as SDFUtil.NewMolecule;
							if ( orig != null ) {
                                SDFUtil.NewMolecule canon = orig.ReorderCanonically();
								if ( canon == null ) {
									if ( !String.IsNullOrWhiteSpace(orig.Mol) ) {
										Interlocked.Increment(ref nNonCan);
										File.WriteAllText(Path.Combine("diffs", sdf.GetFieldValue("UNII") + "-noncanonicalizable.sdf"), sdf.ToString());
									}
								}
								else {
									// Assert.AreEqual(orig.InChI, canon.InChI, String.Format("{0}: {1} != {2}", sdf.GetFieldValue("UNII"), orig.InChI, canon.InChI));
									if ( orig.InChI != canon.InChI ) {
										Interlocked.Increment(ref nNonEqual);
										File.WriteAllText(Path.Combine("diffs", sdf.GetFieldValue("UNII") + ".sdf"), sdf.ToString());
										File.WriteAllText(Path.Combine("diffs", sdf.GetFieldValue("UNII") + "-1.mol"), orig.Mol);
										File.WriteAllText(Path.Combine("diffs", sdf.GetFieldValue("UNII") + "-2.mol"), canon.Mol);
									}
								}
							}
						}
						catch ( Exception ex ) {
							Interlocked.Increment(ref nExcept);
							sdf.AddField("Exception", ex.ToString());
							File.WriteAllText(Path.Combine("diffs", sdf.GetFieldValue("UNII") + "-exception.sdf"), sdf.ToString());
						}
				});
			}

			Assert.Fail();
		}
	}
}
