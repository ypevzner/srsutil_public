using FDA.SRS.Processing;
using FDA.SRS.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.XmlDiffPatch;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace UnitTestProject
{
    //This test class should be deprecated, as most tests
    //are from the old XML way, new tests are done via JSON
	//[TestClass]
	[DeploymentItem(@"..\..\Resources\registry.sdf")]
    [DeploymentItem(@"..\..\Resources\Fragments.sdf")]
    [DeploymentItem(@"..\..\Resources\Proteins\")]
	public class ProteinsTests
	{
		private static readonly string _outDirPrefix = "Proteins_";

		private static void testRecordConversion(string basename, bool result = true, ImportOptions importOptions = null, ConvertOptions convertOptions = null, ExportOptions exportOptions = null)
		{
			string splDiffFile = String.Format("{0}.spl.diff.xml", basename);
			string splFile, outSplFile;
			runProteinConversion(basename, result, out splFile, out outSplFile, importOptions, convertOptions, exportOptions);

			if ( result ) {
				var xmlDiff = new XmlDiff();
				using ( XmlTextWriter xw = new XmlTextWriter(splDiffFile, Encoding.UTF8) ) {
					bool diff = xmlDiff.Compare(splFile, outSplFile, false, xw);
					Assert.IsFalse(diff, "SPL files are same which is impossible");
				}

				var xmlDoc = new XmlDocument();
				xmlDoc.Load(splDiffFile);
				XmlNamespaceManager nsmgr = new XmlNamespaceManager(xmlDoc.NameTable);
				nsmgr.AddNamespace("xd", "http://schemas.microsoft.com/xmltools/2002/xmldiff");

				var xNodes = xmlDoc.DocumentElement.SelectNodes("//xd:change", nsmgr);

				Assert.IsFalse(
					xNodes
						.Cast<XmlNode>()
						.Any(x => x.Attributes["match"].Value != "@root" && x.Attributes["match"].Value != "@value" && !(x.FirstChild is XmlCDataSection)),
					String.Format("SPL files {0} and {1} are different", splFile, outSplFile));
			}
		}

		private static void runProteinConversion(string basename, bool result, out string splFile, out string outSplFile, ImportOptions importOptions, ConvertOptions convertOptions, ExportOptions exportOptions)
		{
			var docId = Guid.Parse("bead5ed2-f72d-45f7-a146-62ad261b022e");
			var setId = Guid.Parse("5ac71a9e-6af2-4152-973d-79e81947cade");

			string dir = _outDirPrefix + basename;

			var impOpt = importOptions ?? new ImportOptions {
				InputFile = String.Format("{0}.sdf", basename),
			};
			var opt = convertOptions ?? new ConvertOptions {
				Validate = true,
			};
			var expOpt = exportOptions ?? new ExportOptions {
				NoDocIdFile = true,
				OutDir = dir,
				UNIIFile = true,
				SrsFile = true,
				DocId = docId,
				SetId = setId,
			};
			Converter.Prot2Spl(impOpt, opt, expOpt);

			splFile = String.Format("{0}.xml", basename);
			outSplFile = Path.Combine(expOpt.OutDir, Converter.OTHER_DIR, splFile);

			if ( result )
				Assert.IsTrue(File.Exists(outSplFile), "SPL file '{0}' does not exist", outSplFile);
			else
				Assert.IsFalse(File.Exists(outSplFile), "SPL file '{0}' exists", outSplFile);
		}

        /////////////////////////////////////////////////////////////
       
        //Test added forTicket 271
         [TestMethod]
         public void Protein_Conversion_4E18KMU551()
         {
             string basename = "4E18KMU551";
             testRecordConversion(basename);
         }
        //End
        
        [TestMethod]
		public void Protein_Conversion_T03CDT3SDM()
		{
			string basename = "T03CDT3SDM";
			testRecordConversion(basename);
		}

		[TestMethod]
		[JiraIssue("SRS-153", "SRS-156")]
		public void Protein_Conversion_7IUT83FK6S()
		{
			string basename = "7IUT83FK6S";
			testRecordConversion(basename);
		}

		[TestMethod]
		public void Protein_Conversion_YN28Z5YZ73()
		{
			string basename = "YN28Z5YZ73";
			testRecordConversion(basename);
		}

		[TestMethod]
		public void Protein_Conversion_YCP1X6C60L()
		{
			string basename = "YCP1X6C60L";
			testRecordConversion(basename);
		}

		[TestMethod]
		public void Protein_Conversion_7XL5ISS668()
		{
			string basename = "7XL5ISS668";
			testRecordConversion(basename);
		}

		// [TestMethod]
		public void Protein_Conversion_VQ723R7O8R()
		{
			string basename = "VQ723R7O8R";
			testRecordConversion(basename);
		}
        //Passed 10/30/10
		[TestMethod]
		public void Protein_Conversion_2S9ZZM9Q9V()
		{
			string basename = "2S9ZZM9Q9V";
			testRecordConversion(basename);
		}

		[TestMethod]
		public void Protein_Conversion_27QNN5AS7N()
		{
			string basename = "27QNN5AS7N";
			testRecordConversion(basename);
		}

		// Probabilistic modifications (AMOUNT_TYPE = PROBABILITY)

		[TestMethod]
		public void Protein_Conversion_8E3HI6QQ9J()
		{
			string basename = "8E3HI6QQ9J";
			testRecordConversion(basename);
		}

		[TestMethod]
		public void Protein_Conversion_M9BYU8XDQ6()
		{
			string basename = "M9BYU8XDQ6";
			testRecordConversion(basename);
		}

		// OTHER_LINKAGE

		[TestMethod]
		public void Protein_Conversion_5Y7IGH3V3Q()
		{
			string basename = "5Y7IGH3V3Q";
			testRecordConversion(basename);
		}

		[TestMethod]
		public void Protein_Conversion_01UIN5OC49()
		{
			string basename = "01UIN5OC49";
			testRecordConversion(basename);
		}

        //Has an agent modification, needs some thought to make sure it's done right
		[TestMethod]
		public void Protein_Conversion_353O60Z7Q1()
		{
			string basename = "353O60Z7Q1";
			testRecordConversion(basename);
		}

		[TestMethod]
		[JiraIssue("SRS-152")]
		public void Protein_Conversion_0192074X0K()
		{
			string basename = "0192074X0K";
			testRecordConversion(basename);
		}

		[TestMethod]
		public void Protein_Conversion_Swap_Sequences()
		{
			Converter.Prot2Spl(
				new ImportOptions {
					InputFile = "YCP1X6C60L-simplified.sdf",
				},
				new ConvertOptions { },
				new ExportOptions {
					NoDocIdFile = true,
					OutDir = "out",
					UNIIFile = true,
				});

			string splFile = Path.Combine("out", Converter.OTHER_DIR, "YCP1X6C60L.xml");
			var hash1 = XDocument.Load(splFile).SplHash();

			File.Move(splFile, splFile + ".bak");

			Converter.Prot2Spl(
				new ImportOptions {
					InputFile = "YCP1X6C60L-simplified-seq-swapped.sdf",
				},
				new ConvertOptions { },
				new ExportOptions {
					NoDocIdFile = true,
					OutDir = "out",
					UNIIFile = true,
				});
			var hash2 = XDocument.Load(splFile).SplHash();

			Assert.AreEqual(hash1, hash2);
		}

		[TestMethod]
		public void Protein_Conversion_SimplePermutations()
		{
			Converter.Prot2Spl(
				new ImportOptions {
					InputFile = "aa-permutations.sdf",
				},
				new ConvertOptions { },
				new ExportOptions {
					NoDocIdFile = true,
					OutDir = @"out",
					UNIIFile = true,
				});

			var hash0 = XDocument.Load(Path.Combine("out", Converter.OTHER_DIR, "0AAAAAAAAA.xml")).SplHash();
			var hash1 = XDocument.Load(Path.Combine("out", Converter.OTHER_DIR, "1AAAAAAAAA.xml")).SplHash();
			var hash2 = XDocument.Load(Path.Combine("out", Converter.OTHER_DIR, "2AAAAAAAAA.xml")).SplHash();
			var hash3 = XDocument.Load(Path.Combine("out", Converter.OTHER_DIR, "3AAAAAAAAA.xml")).SplHash();
			var hash4 = XDocument.Load(Path.Combine("out", Converter.OTHER_DIR, "4AAAAAAAAA.xml")).SplHash();

			Assert.AreEqual(hash0, hash1);
			Assert.AreEqual(hash1, hash2);
			Assert.AreEqual(hash2, hash3);
			Assert.AreNotEqual(hash3, hash4);
		}

		[TestMethod]
		public void Protein_Conversion_mod_hash_inv()
		{
			Converter.Prot2Spl(
				new ImportOptions {
					InputFile = "mod-hash-inv.sdf",
				},
				new ConvertOptions { },
				new ExportOptions {
					NoDocIdFile = true,
					OutDir = @"out",
					UNIIFile = true,
				});

			var hash0 = XDocument.Load(Path.Combine("out", Converter.OTHER_DIR, "0BBBBBBBBB.xml")).SplHash();
			var hash1 = XDocument.Load(Path.Combine("out", Converter.OTHER_DIR, "1BBBBBBBBB.xml")).SplHash();

			Assert.AreEqual(hash0, hash1);
		}

		[TestMethod]
		public void Protein_Conversion_675VGV5J1D()
		{
			string basename = "675VGV5J1D";
			testRecordConversion(basename);
		}

		[TestMethod]
		public void Protein_Conversion_08AN7WA2G0_clear_srs_xml_ns()
		{
			string basename = "08AN7WA2G0";
			testRecordConversion(basename, importOptions: new ImportOptions {
				InputFile = String.Format("{0}.sdf", basename),
				Features = SplFeature.FromList("clear-srs-xml-ns"),
			});
		}

		[TestMethod]
		public void Protein_Conversion_562JQF15GN_ignore_empty_polymer()
		{
			string basename = "562JQF15GN";
			testRecordConversion(basename, importOptions: new ImportOptions {
				InputFile = String.Format("{0}.sdf", basename),
				Features = SplFeature.FromList("ignore-empty-polymer"),
			});
		}

		[TestMethod]
		public void Protein_Conversion_T13V17U431_v3000()
		{
			string basename = "T13V17U431";
			testRecordConversion(basename, false);

			testRecordConversion(basename, convertOptions: new ConvertOptions {
				Features = SplFeature.FromList("allow-lossy-v3000-v2000"),
			});
		}
	}
}
