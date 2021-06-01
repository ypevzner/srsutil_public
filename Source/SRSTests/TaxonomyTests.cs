using FDA.SRS.Database;
using FDA.SRS.Processing;
using FDA.SRS.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace UnitTestProject
{
	[TestClass]
	[DeploymentItem(@"x64\", @"x64\")]
	[DeploymentItem(@"x86\", @"x86\")]
    [DeploymentItem(@"..\..\Resources\KewGardens.csv")]
	[DeploymentItem("runtimes", "runtimes")]
	public class TaxonomyTests
	{
		// [TestMethod]
		public void NcbiTaxonomyLocalDownload()
		{
			var names_dmp = TaxonomyUtils.PrepareNcbiTaxonomyFile();
			Assert.IsTrue(File.Exists(names_dmp));
			Assert.IsTrue(new FileInfo(names_dmp).Length > 0);
		}


		// [TestMethod]
		public void NcbiTaxonomyDownload()
		{
			var file = TaxonomyUtils.PrepareNcbiTaxonomyFile();
			Assert.IsTrue(File.Exists(file));
			Assert.IsTrue(new FileInfo(file).Length > 0);
		}

		// [TestMethod]
		public void ItisTaxonomyDownload()
		{
			var file = TaxonomyUtils.PrepareItisTaxonomySqliteFile();
			Assert.IsTrue(File.Exists(file));
			Assert.IsTrue(new FileInfo(file).Length > 0);
		}

		[TestMethod]
		public void ITIS_CheckDatabaseContent()
		{
			ItisDatabase db = new ItisDatabase(@"..\..\..\..\Data\ITIS.sqlite");
			Assert.IsTrue(db.ValidateName("caseya guttata"));
			Assert.IsTrue(db.ValidateName("amphinaias asperata archeri"));

			Assert.IsTrue(db.ValidateReference("Aeromonas salmonicida (Lehmann and Neumann, 1896) Griffin et al., 1953"));
			Assert.IsTrue(db.ValidateReference("Aeromonas salmonicida salmonicida (Lehmann and Neumann, 1896) Schubert, 1967"));
			Assert.IsTrue(db.ValidateReference("Thiobacillus denitrificans (ex Beijerinck, 1904) Kelly and Harrison, 1989"));

			Assert.IsTrue(db.ValidateAuthor("Duellman and Dixon, 1959"));
			Assert.IsTrue(db.ValidateAuthor("Valenciennes in Humboldt & Bonpland, 1827"));
		}

		// We can't work with DBF files
		// [TestMethod]
		public void USDA_CheckDatabaseContent()
		{
			UsdaDatabase db = new UsdaDatabase(@"..\..\..\..\Data\species.dbf");
			Assert.IsTrue(db.ValidateName("caseya guttata"));
			Assert.IsTrue(db.ValidateName("amphinaias asperata archeri"));

			Assert.IsTrue(db.ValidateAuthor("duellman and dixon"));
			Assert.IsTrue(db.ValidateAuthor("valenciennes in humboldt & bonpland"));

			Assert.IsTrue(db.ValidateReference("Aeromonas salmonicida (Lehmann and Neumann) Griffin et al."));
			Assert.IsTrue(db.ValidateReference("Thiobacillus denitrificans (ex Beijerinck) Kelly and Harrison"));
		}

        [TestMethod]
        public void UpdateTaxonomyTest() {
            ExportOptions expOp = new ExportOptions {
                OutputFile = CmdLineUtils.ParsePathWithEncodingPrefix("outputall.txt"),
                OutputFileEncoding = Encoding.UTF8,
            };
            TaxonomyUtils.UpdateTerms(expOp,
                    new TaxonomyOptions {
                        SimplifiedAuthorityReference = true,
                        FailOnMissing=true
                    });
            int count = 0;
            using (StreamReader r = new StreamReader(expOp.OutputFile)) {
                while (r.ReadLine()!=null) {
                    count++;
                }
            }
            Assert.IsTrue(count > 1000000);

        }

        [TestMethod]
        public void UpdateTaxonomyKewOnlyTest() {
            ExportOptions expOp = new ExportOptions {
                OutputFile = CmdLineUtils.ParsePathWithEncodingPrefix("outputall.txt"),
                OutputFileEncoding = Encoding.UTF8,
            };

            ImportOptions impOpt = new ImportOptions { TermsFiles = new List<string>() };
            
            try {
                var kewOptions = new ExportOptions { };
                TaxonomyUtils.Kew2Terms(new ImportOptions(), kewOptions, new TaxonomyOptions {
                    SimplifiedAuthorityReference = true,
                    FailOnMissing = true
                });
                impOpt.TermsFiles.Add(kewOptions.OutputFile);
            } catch (Exception e) {
                throw e;
            }
            TaxonomyUtils.MergeTerms(impOpt, expOp);
            
            int count = 0;
            using (StreamReader r = new StreamReader(expOp.OutputFile)) {
                while (r.ReadLine() != null) {
                    count++;
                }
            }
            Assert.IsTrue(count > 10000);

        }

        [TestMethod]
		public void PrepareTaxonomyReference()
		{
			Assert.AreEqual("Enterobacter aerogenes Hormaeche and Edwards", "Enterobacter aerogenes Hormaeche and Edwards, 1986".PrepareTaxonomyReference(new TaxonomyOptions { SimplifiedAuthorityReference = true }));
			Assert.AreEqual("Enterobacter aerogenes (Hormaeche and Edwards), Edwards", "Enterobacter aerogenes (Hormaeche and Edwards, 1985), Edwards, 1986".PrepareTaxonomyReference(new TaxonomyOptions { SimplifiedAuthorityReference = true }));
			Assert.AreEqual("Enterobacter aerogenes (Hormaeche, Edwards), Edwards", "Enterobacter aerogenes (Hormaeche, 1294, Edwards, 1985), Edwards, 1986".PrepareTaxonomyReference(new TaxonomyOptions { SimplifiedAuthorityReference = true }));
			Assert.AreEqual("Enterobacter aerogenes (Hormaeche, Edwards), Edwards", "Enterobacter aerogenes (Hormaeche 1294, Edwards 1985), Edwards, 1986".PrepareTaxonomyReference(new TaxonomyOptions { SimplifiedAuthorityReference = true }));

			Assert.AreEqual("Enterobacter aerogenes Hormaeche and Edwards, 1986", "Enterobacter aerogenes Hormaeche and Edwards, 1986".PrepareTaxonomyReference(new TaxonomyOptions { SimplifiedAuthorityReference = false }));
			Assert.AreEqual("Enterobacter aerogenes (Hormaeche and Edwards, 1985), Edwards, 1986", "Enterobacter aerogenes (Hormaeche and Edwards, 1985), Edwards, 1986".PrepareTaxonomyReference(new TaxonomyOptions { SimplifiedAuthorityReference = false }));
			Assert.AreEqual("Enterobacter aerogenes (Hormaeche, 1294, Edwards, 1985), Edwards, 1986", "Enterobacter aerogenes (Hormaeche, 1294, Edwards, 1985), Edwards, 1986".PrepareTaxonomyReference(new TaxonomyOptions { SimplifiedAuthorityReference = false }));
			Assert.AreEqual("Enterobacter aerogenes (Hormaeche 1294, Edwards 1985), Edwards, 1986", "Enterobacter aerogenes (Hormaeche 1294, Edwards 1985), Edwards, 1986".PrepareTaxonomyReference(new TaxonomyOptions { SimplifiedAuthorityReference = false }));
        }

        //Added for Ticket 265 : Incorrect capitalization of names

        [TestMethod]
        public void AuthorStandardize() {
            String ss= "(L.) Dc.".CasifyAuthors();
            Assert.AreEqual("(L.) DC.", ss);

            ss = "(L.F.) Willd.".CasifyAuthors();
            Assert.AreEqual("(L.f.) Willd.", ss);
        }


		[TestMethod]
		public void ExtractProtologueYear()
		{
			Assert.AreEqual(1857, "Bull. Cl. Phys.-Math. Acad. Imp. Sci. Saint-P&eacute;tersbourg 15:128, 143.  1857 (\"1856\")".ExtractProtologueYear());
			Assert.AreEqual(1913, "Notes Roy. Bot. Gard. Edinburgh 8:103.  1913, nom. rej. prop.".ExtractProtologueYear());
			Assert.AreEqual(1852, "Pl. wright. 1:89.  1852 (Smithsonian Contr. Knowl. 3, Art. 5); 2:75.  1853 (Smithsonian Contr. Knowl. 5, Art. 6)".ExtractProtologueYear());
			Assert.AreEqual(1964, "Sida 1:295.  1964".ExtractProtologueYear());

			Assert.IsNull("Sida 1:295.".ExtractProtologueYear());
		}
	}
}
