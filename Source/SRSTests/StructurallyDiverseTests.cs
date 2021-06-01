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

    //A lot of these tests will need to be rewritten to use the JSON
	[TestClass]
	[DeploymentItem(@"..\..\Resources\registry.sdf")]
	[DeploymentItem(@"..\..\Resources\StructurallyDiverse\")]
	public class StructurallyDiverseTests
	{
		private static readonly string _outDirPrefix = "SD_";


        /// <summary>
        /// Test a record to make sure it gets the approximate same
        /// SPL output as expected for the input SDF. If res is set to true,
        /// it will enforce that the returned SPL is consistent, if res is
        /// false, it will ensure that it "correctly" fails.
        /// </summary>
        /// <param name="basename"></param>
        /// <param name="res"></param>
		private static void testRecordConversion(string basename, bool res)
		{
			string sdfFile = String.Format("{0}.sdf", basename);
			string srsFile = String.Format("{0}.srs.xml", basename);
			string splFile = String.Format("{0}.spl.xml", basename);
			string splDiffFile = String.Format("{0}.spl.diff.xml", basename);
			string dir = _outDirPrefix + basename;

			var impOpt = new ImportOptions {
				InputFile = sdfFile,

			};
			var opt = new ConvertOptions {
				Validate = true
			};
			var expOpt = new ExportOptions {
				OutDir = dir,
				UNIIFile = true,
				SrsFile = true,
				DocId = Guid.Parse("bead5ed2-f72d-45f7-a146-62ad261b022e"),
				SetId = Guid.Parse("5ac71a9e-6af2-4152-973d-79e81947cade"),
			};
			Converter.SD2Spl(impOpt, opt, expOpt);

			// Cover the case of dash in a file name
			string unii = basename.Split('-').FirstOrDefault();

			if ( !res ) {
				string errSrsFile = Path.Combine(expOpt.OutDir, Converter.ERR_DIR, unii + ".xml");
				Assert.IsTrue(File.Exists(errSrsFile));
			}
			else {
				string outSplFile = Path.Combine(expOpt.OutDir, Converter.OTHER_DIR, unii + ".xml");
				Assert.IsTrue(File.Exists(outSplFile), "{0} does not exist - likely in errors", outSplFile);

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
					xNodes.Cast<XmlNode>()
					.Any(x => x.Attributes["match"].Value != "@root" && x.Attributes["match"].Value != "@value"),
					String.Format("SPL files {0} and {1} are different", splFile, outSplFile));
			}
		}

		[TestMethod]
		[DeploymentItem(@"..\..\Resources\StructurallyDiverse\diverse_elist_update_1.sdf")]
		public void GenerateModeTests()
		{
			string file = "diverse_elist_update_1.sdf";
			string dir = _outDirPrefix + Path.GetFileNameWithoutExtension(file);

			// Default mode
			Converter.SD2Spl(
				new ImportOptions {
					InputFile = CmdLineUtils.ParsePathWithEncodingPrefix(file),
				},
				new ConvertOptions {
					GenerateMode = GenerateMode.Default
				},
				new ExportOptions {
					OutDir = dir,
				});

			var files = Directory.GetFiles(Path.Combine(dir, Converter.OUT_DIR), "*.xml", SearchOption.AllDirectories);
			Assert.AreEqual(5, files.Count());

			// Delete directories to clean the scene
			Directory.Delete(dir, true);

			// NewUnii mode
			Converter.SD2Spl(
				new ImportOptions {
					InputFile = CmdLineUtils.ParsePathWithEncodingPrefix(file),
				},
				new ConvertOptions {
					GenerateMode = GenerateMode.NewUnii
				},
				new ExportOptions {
					OutDir = dir,
				});

			files = Directory.GetFiles(Path.Combine(dir, Converter.OUT_DIR), "*.xml", SearchOption.AllDirectories);
			Assert.AreEqual(0, files.Count());

			// Delete directories to clean the scene
			Directory.Delete(dir, true);

			// NewHash mode
			Converter.SD2Spl(
				new ImportOptions {
					InputFile = CmdLineUtils.ParsePathWithEncodingPrefix(file),
				},
				new ConvertOptions {
					GenerateMode = GenerateMode.NewHash
				},
				new ExportOptions {
					OutDir = dir,
				});

			files = Directory.GetFiles(Path.Combine(dir, Converter.OUT_DIR), "*.xml", SearchOption.AllDirectories);
			Assert.AreEqual(0, files.Count());
		}

		[TestMethod]
		public void SD_Conversion_2B9XP242O4()
		{
          //  string basename = "2B9XP242O4";
          //  testRecordConversion(basename, false);

            string file = "2B9XP242O4.sdf";
			string dir = _outDirPrefix + Path.GetFileNameWithoutExtension(file);
			string docId = "bead5ed2-f72d-45f7-a146-62ad261b022e";
			Converter.SD2Spl(
				new ImportOptions { InputFile = file },
				new ConvertOptions { },
				new ExportOptions {
					OutDir = dir,
					DocId = Guid.Parse(docId),
				});

			string splFile = Path.Combine(dir, Converter.OUT_DIR, docId, "a", docId + ".xml");
			XDocument xdoc = XDocument.Load(splFile);
			Assert.AreEqual("addd4ad9-5884-4e94-bb27-31ce5a0ad09b", xdoc.SplSetId().ToString());
			Assert.AreEqual(3, xdoc.SplVersion());
            
            Assert.AreEqual("Prostanthera rotundifolia R. Br.", xdoc.SplBibRef());

            Assert.IsTrue(JsonTests.cleanedContains(xdoc.ToString(), @" <subject>
            <identifiedSubstance>
              <id extension=""2B9XP242O4"" root=""2.16.840.1.113883.4.9"" />
              <identifiedSubstance>
                <code code=""2B9XP242O4"" codeSystem=""2.16.840.1.113883.4.9"" />
                <asEquivalentSubstance>
                  <definingSubstance>
                    <code code=""a4af087a-90e0-1217-45f5-d4643d91b6cd"" codeSystem=""2.16.840.1.113883.3.2705"" />
                  </definingSubstance>
                </asEquivalentSubstance>
              </identifiedSubstance>
              <subjectOf>
                <document>
                  <bibliographicDesignationText>Prostanthera rotundifolia R. Br.</bibliographicDesignationText>
                </document>
              </subjectOf>
            </identifiedSubstance>
          </subject>"));
          
        }

		/////////////////////////////////////////////////////////////////////////////////
		// Whole organisms

		[TestMethod]
		public void SD_Conversion_04W636S1V3()
		{
			string basename = "04W636S1V3";
			testRecordConversion(basename, true);
		}

        //Added for Ticket:312 Structurally Diverse
        [TestMethod]
        public void SD_Conversion_BY2F2908HQ()
        {
            string basename = "BY2F2908HQ";
            testRecordConversion(basename, true);
        }

        //Deprecated test
        //[TestMethod]
        public void SD_Conversion_04W636S1V3_GSRS()
		{
			string basename = "04W636S1V3-GSRS";
			testRecordConversion(basename, true);
		}

		[TestMethod]
		public void SD_Conversion_00338G7O3S()
		{
			string basename = "00338G7O3S";
			testRecordConversion(basename, false);
		}

		[TestMethod]
		public void SD_Conversion_02FHK59X1U()
		{
			string basename = "02FHK59X1U";
			testRecordConversion(basename, false);
		}

		[TestMethod]
		public void SD_Conversion_5F25FW4LG7()
		{
			string basename = "5F25FW4LG7";
			testRecordConversion(basename, false);
		}

		[TestMethod]
		public void SD_Conversion_8R7L0KTA8M()
		{
			string basename = "8R7L0KTA8M";
			testRecordConversion(basename, true);
		}

		[TestMethod]
		public void SD_Conversion_NFT1835HUN()
		{
			string basename = "NFT1835HUN";
			testRecordConversion(basename, true);
		}

		[TestMethod]
		public void SD_Conversion_ZSH041A5VN()
		{
			string basename = "ZSH041A5VN";
			testRecordConversion(basename, false);
		}

		/////////////////////////////////////////////////////////////////////////////////
		// Viruses

		[TestMethod]
		public void SD_Conversion_08MVT1AQQ9()
		{
			string basename = "08MVT1AQQ9";
			testRecordConversion(basename, true);
		}

		[TestMethod]
		public void SD_Conversion_22Q2CO717B()
		{
			string basename = "22Q2CO717B";
			testRecordConversion(basename, false);
		}

        //Added for Ticket 371
        [TestMethod]
        public void SD_Conversion_NY7PTD1862()
        {
            string basename = "NY7PTD1862";
            testRecordConversion(basename, false);
        }

        [TestMethod]
        public void SD_Conversion_X7R0M59LQ6()
        {
            string basename = "X7R0M59LQ6";
            testRecordConversion(basename, false);
        }


        //Virus whole test (ignored for now, expected to fail, and doesn't)
        //[TestMethod]
        public void SD_Conversion_93O6C9VO2C()
		{
			string basename = "93O6C9VO2C";
			testRecordConversion(basename, false);
		}

		[TestMethod]
		public void SD_Conversion_5307V7XW8I()
		{
			string basename = "5307V7XW8I";
			testRecordConversion(basename, false);
		}


        //This is a plant part with a fraction and a
        //structure that is also representative
        //For reasons I don't understand, it was set to pass
        //but really shouldn't have been since it is representative
        //
		[TestMethod]
		public void SD_Conversion_V5B97ZYP2F()
		{
			string basename = "V5B97ZYP2F";
			testRecordConversion(basename, false);
		}

		/////////////////////////////////////////////////////////////////////////////////
		// SD with parents

        //plant part (ignored for now)
		[TestMethod]
		public void SD_Conversion_9871T0PD5P()
		{
			string basename = "9871T0PD5P";
			testRecordConversion(basename, true);
		}

        //plant part (ignored for now)
        [TestMethod]
		public void SD_Conversion_B423VGH5S9()
		{
			string basename = "B423VGH5S9";
			testRecordConversion(basename, true);
		}

	}
}
