using FDA.SRS.Processing;
using FDA.SRS.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Linq;

namespace UnitTestProject
{
	[TestClass]
	[DeploymentItem(@"..\..\Resources\Chemicals\")]
	public class ExtractTests
	{
		private static readonly string _outDirPrefix = "Extract_";

		[TestMethod]
		public void ExtractSpecificSGroupsTest()
		{
			string file = "00HPQ7674K.sdf";
			string dir = _outDirPrefix + Path.GetFileNameWithoutExtension(file);

			Converter.SdfExtractSdf(
				new ImportOptions {
					InputFile = file,
				},
				new ConvertOptions {
					SGroupTypes = SGroupType.GEN | SGroupType.DAT
				},
				new ExportOptions {
					OutDir = dir,
				});

			Assert.AreEqual(0, Directory.GetFiles(dir).Count());

			file = "025P9I542Y.sdf";
			dir = _outDirPrefix + Path.GetFileNameWithoutExtension(file);

			Converter.SdfExtractSdf(
				new ImportOptions {
					InputFile = file,
				},
				new ConvertOptions {
					SGroupTypes = SGroupType.GEN | SGroupType.DAT
				},
				new ExportOptions {
					OutDir = dir,
				});

			Assert.AreEqual(1, Directory.GetFiles(dir).Count());
		}
	}
}
