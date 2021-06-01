using com.epam.indigo;
using FDA.SRS.ObjectModel;
using FDA.SRS.Processing;
using FDA.SRS.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

using NewMolecule = FDA.SRS.Utils.SDFUtil.NewMolecule;
using IMolecule = FDA.SRS.Utils.SDFUtil.IMolecule;

namespace UnitTestProject
{
	static class SplOptActExtensions
	{
		/*
		<subjectOf>
		  <characteristic>
			<code code="C103201" codeSystem="2.16.840.1.113883.3.26.1.1" displayName="Optical Activity" />
			<value xsi:type="CV" code="C103202" codeSystem="2.16.840.1.113883.3.26.1.1" displayName="Dextrorotatory" />
		  </characteristic>
		</subjectOf>
		*/
		public static string SplOptActCode(this XDocument xdoc)
		{
			return ((xdoc
				.Descendants(XName.Get("code", "urn:hl7-org:v3"))
				.Where(e => e.Attribute("displayName")?.Value == "Optical Activity")
				.FirstOrDefault()
				?.NextNode) as XElement)
				?.Attribute("code")
				?.Value;
		}
	}

	[TestClass]
	[DeploymentItem(@"..\..\Resources\DLL\indigo.dll")]
	[DeploymentItem(@"..\..\Resources\DLL\indigo-inchi.dll")]
	[DeploymentItem(@"..\..\Resources\Chemicals\")]
	[DeploymentItem(@"x64\", @"x64\")]
	[DeploymentItem(@"x86\", @"x86\")]
	public class ChemicalsTests
	{
		private static readonly string _outDirPrefix = "Chemicals_";

		public TestContext TestContext { get; set; }
        
        [TestMethod]
		public void Chemicals_Standardization()
		{
			using ( SdfReader r = new SdfReader("0035H8M4YL.sdf") ) {
				SdfRecord sdf = r.Records.First();
				using ( var indigo = new Indigo() )
				using ( IndigoObject mol = indigo.loadMolecule(sdf.Mol) ) {
					Stereomers s = Standardize.processMolecule(indigo, mol, sdf.GetFieldValue("UNII"), isV2000: sdf.Mol.Contains("V2000"));
					IEnumerable<Moiety> ml = s.ToMoieties("MIXED", null).ToList();
					StringBuilder sb = new StringBuilder();
					foreach ( var m in ml ) {
						sb.AppendLine(m.ToString());
					}

					Assert.AreEqual(2, ml.Count());
					ml = ml.Distinct(new MoietyEqualityComparer()).ToList();
					Assert.AreEqual(2, ml.Count());
				}
			}
		}

		[TestMethod]
		public void Chemicals_RemovePolymerSGroup()
		{
			string file = "J8QFJ59TK6.sdf";
			using ( SdfReader r = new SdfReader(file) { LineFilters = new[] { SrsSdfValidators.SubstanceSdfLineFilter } } ) {
				SdfRecord sdf = r.Records.First();
				File.WriteAllText(Path.Combine(Path.GetDirectoryName(file), Path.GetFileNameWithoutExtension(file) + "-modified2.sdf"), sdf.ToString());
			}

			file = "6E17K3343P.sdf";
			using ( SdfReader r = new SdfReader(file) { LineFilters = new[] { SrsSdfValidators.SubstanceSdfLineFilter } } ) {
				SdfRecord sdf = r.Records.First();
				File.WriteAllText(Path.Combine(Path.GetDirectoryName(file), Path.GetFileNameWithoutExtension(file) + "-modified2.sdf"), sdf.ToString());
			}
		}

		[TestMethod]
		public void Chemicals_SRS_115()
		{
			string file = "J8QFJ59TK6.sdf";
			string dir = _outDirPrefix + Path.GetFileNameWithoutExtension(file);

			Converter.Str2Spl(
				new ImportOptions {
					InputFile = CmdLineUtils.ParsePathWithEncodingPrefix(file),
				},
				new ConvertOptions { },
				new ExportOptions {
					OutDir = dir
				});

			Assert.AreEqual(1, Directory.GetFiles(Path.Combine(dir, Converter.OUT_DIR), "*.xml", SearchOption.AllDirectories).Count());
		}
        
        [TestMethod]
        public void Chemicals_AXIAL_STEREO() {
            string file = "ZW0P1P8U1D.sdf";
            string dir = _outDirPrefix + Path.GetFileNameWithoutExtension(file);

            Converter.Str2Spl(
                new ImportOptions {
                    InputFile = CmdLineUtils.ParsePathWithEncodingPrefix(file),
                },
                new ConvertOptions { },
                new ExportOptions {
                    OutDir = dir
                });
            
            Assert.AreEqual(1, Directory.GetFiles(Path.Combine(dir, Converter.OUT_DIR), "*.xml", SearchOption.AllDirectories).Count());

            Directory.GetFiles(Path.Combine(dir, Converter.OUT_DIR), "*.xml", SearchOption.AllDirectories).ForEachWithIndex((f, i) => {
                System.Console.WriteLine(f);
                StringBuilder sb = new StringBuilder();

                using (StreamReader sr = new StreamReader(f)) {
                    while (sr.Peek() >= 0) {
                        sb.Append(sr.ReadLine() + "\n");
                    }
                }
                Assert.IsTrue(sb.ToString().Contains("Axial S"));

            });
            
            
        }

        [TestMethod]
        public void Polygon_Origin_Should_Be_Inside_Simple_Triangle() {
            List<Tuple<double, double>> polygon = new List<Tuple<double, double>>();
            polygon.Add(Tuple.Create(Math.Cos(Math.PI * 2 / 3.0), Math.Sin(Math.PI * 2 / 3.0)));
            polygon.Add(Tuple.Create(Math.Cos(2*Math.PI * 2 / 3.0), Math.Sin(2*Math.PI * 2 / 3.0)));
            polygon.Add(Tuple.Create(Math.Cos(3 * Math.PI * 2 / 3.0), Math.Sin(3 * Math.PI * 2 / 3.0)));

            Assert.IsTrue(ProteinExtensions.IsInsidePoly(polygon,Tuple.Create(0.0,0.0)));
        }
        [TestMethod]
        public void Polygon_Origin_Should_Be_Outside_Simple_Triangle() {
            List<Tuple<double, double>> polygon = new List<Tuple<double, double>>();
            polygon.Add(Tuple.Create(Math.Cos(Math.PI * 2 / 3.0), Math.Sin(Math.PI * 2 / 3.0)));
            polygon.Add(Tuple.Create(Math.Cos(2 * Math.PI * 2 / 3.0), Math.Sin(2 * Math.PI * 2 / 3.0)));
            polygon.Add(Tuple.Create(Math.Cos(3 * Math.PI * 2 / 3.0), Math.Sin(3 * Math.PI * 2 / 3.0)));

            Assert.IsFalse(ProteinExtensions.IsInsidePoly(polygon, Tuple.Create(2.0, 0.0)));
        }
        [TestMethod]
        public void Polygon_Vertex_Should_Be_Inside_Simple_Triangle() {
            List<Tuple<double, double>> polygon = new List<Tuple<double, double>>();
            polygon.Add(Tuple.Create(Math.Cos(Math.PI * 2 / 3.0), Math.Sin(Math.PI * 2 / 3.0)));
            polygon.Add(Tuple.Create(Math.Cos(2 * Math.PI * 2 / 3.0), Math.Sin(2 * Math.PI * 2 / 3.0)));
            polygon.Add(Tuple.Create(Math.Cos(3 * Math.PI * 2 / 3.0), Math.Sin(3 * Math.PI * 2 / 3.0)));

            Assert.IsTrue(ProteinExtensions.IsInsidePoly(polygon, polygon[0]));
        }

        [TestMethod]
        public void Chemical_ShallowStereoCenterWithCenterInPolygonShouldChangeWedgeLocationKeepType() {
            String molfile = @"[NO NAME]


  4  3  0  0  1  0            999 V2000
  -31.2584   74.7933    0.0000 C   0  0  0  0  0  0  0  0  0  0  0  0
  -32.8575   74.7117    0.0000 N   0  0  0  0  0  0  0  0  0  0  0  0
  -29.5450   74.6954    0.0000 C   0  0  0  0  0  0  0  0  0  0  0  0
  -31.2584   76.3933    0.0000 S   0  0  0  0  0  0  0  0  0  0  0  0
  1  2  1  0  0  0  0
  1  3  1  0  0  0  0
  1  4  1  1  0  0  0
M  END";
            Indigo indigo = new Indigo();
            IndigoObject mol = indigo.loadMolecule(molfile);
            mol = ProteinExtensions.cleanMoleculeStereo(mol);
            String nmolfile = mol.molfile();

            Assert.IsTrue(nmolfile.Contains("  1  4  1  0  0  0  0"));
            Assert.IsTrue(nmolfile.Contains("  1  2  1  1  0  0  0"));
        }

        [TestMethod]
        public void Chemical_ShallowStereoCenterWithCenterOutsidePolygonShouldChangeWedgeLocationAndType() {
            String molfile = @"[NO NAME]


  4  3  0  0  1  0            999 V2000
  -31.2584   74.7933    0.0000 C   0  0  0  0  0  0  0  0  0  0  0  0
  -32.8575   74.7117    0.0000 N   0  0  0  0  0  0  0  0  0  0  0  0
  -29.5730   75.0290    0.0000 C   0  0  0  0  0  0  0  0  0  0  0  0
  -31.2584   76.3933    0.0000 S   0  0  0  0  0  0  0  0  0  0  0  0
  1  2  1  0  0  0  0
  1  3  1  0  0  0  0
  1  4  1  1  0  0  0
M  END";
            Indigo indigo = new Indigo();
            IndigoObject mol = indigo.loadMolecule(molfile);
            mol = ProteinExtensions.cleanMoleculeStereo(mol);
            String nmolfile = mol.molfile();

            Assert.IsTrue(nmolfile.Contains("  1  4  1  0  0  0  0"));
            Assert.IsTrue(nmolfile.Contains("  1  2  1  6  0  0  0"));
        }

        [TestMethod]
        public void Chemicals_OPTICAL_ACTIVITY() {
            string file = "JN94J856WH.sdf";
            string dir = _outDirPrefix + Path.GetFileNameWithoutExtension(file);

            Converter.Str2Spl(
                new ImportOptions {
                    InputFile = CmdLineUtils.ParsePathWithEncodingPrefix(file),
                },
                new ConvertOptions { },
                new ExportOptions {
                    OutDir = dir
                });

            Assert.AreEqual(1, Directory.GetFiles(Path.Combine(dir, Converter.OUT_DIR), "*.xml", SearchOption.AllDirectories).Count());

            string spl=getXMLRaw(dir);
            
            System.Console.WriteLine(spl);
            Assert.IsTrue(spl.Contains("Dextrorotatory"));


        }


        private static string getXMLRaw(string dir) {
            string path =Directory.GetFiles(Path.Combine(dir, Converter.OUT_DIR), "*.xml", SearchOption.AllDirectories)
                     .First();
            
                StringBuilder sb = new StringBuilder();

                using (StreamReader sr = new StreamReader(path)) {
                    while (sr.Peek() >= 0) {
                        sb.Append(sr.ReadLine() + "\n");

                    }
                }
            return sb.ToString();
        }

        [TestMethod]
		public void Chemicals_UniiGenerateMode()
		{
			string file = "J8QFJ59TK6.sdf";
			string dir = _outDirPrefix + Path.GetFileNameWithoutExtension(file);

			// NewUnii mode
			Converter.Str2Spl(
				new ImportOptions {
					InputFile = CmdLineUtils.ParsePathWithEncodingPrefix(file),
				},
				new ConvertOptions {
					GenerateMode = GenerateMode.NewUnii
				},
				new ExportOptions {
					OutDir = dir + "_NewUnii"
				});

			Assert.AreEqual(0, Directory.GetFiles(Path.Combine(dir + "_NewUnii", Converter.OUT_DIR), "*.xml", SearchOption.AllDirectories).Count());

			// Default mode
			Converter.Str2Spl(
				new ImportOptions {
					InputFile = CmdLineUtils.ParsePathWithEncodingPrefix(file),
				},
				new ConvertOptions {
					GenerateMode = GenerateMode.Default
				},
				new ExportOptions {
					OutDir = dir + "_Default"
				});

			Assert.AreEqual(1, Directory.GetFiles(Path.Combine(dir + "_Default", Converter.OUT_DIR), "*.xml", SearchOption.AllDirectories).Count());

			// NewHash mode
			Converter.Str2Spl(
				new ImportOptions {
					InputFile = CmdLineUtils.ParsePathWithEncodingPrefix(file),
				},
				new ConvertOptions {
					GenerateMode = GenerateMode.NewHash
				},
				new ExportOptions {
					OutDir = dir + "_NewHash"
				});

			Assert.AreEqual(0, Directory.GetFiles(Path.Combine(dir + "_NewHash", Converter.OUT_DIR), "*.xml", SearchOption.AllDirectories).Count());

			// NewHash mode on changed file
			file = "J8QFJ59TK6-modified.sdf";
			dir = _outDirPrefix + Path.GetFileNameWithoutExtension(file);
			Converter.Str2Spl(
				new ImportOptions {
					InputFile = CmdLineUtils.ParsePathWithEncodingPrefix(file),
				},
				new ConvertOptions {
					GenerateMode = GenerateMode.NewHash
				},
				new ExportOptions {
					OutDir = dir + "_NewHash"
				});

			Assert.AreEqual(1, Directory.GetFiles(Path.Combine(dir + "_NewHash", Converter.OUT_DIR), "*.xml", SearchOption.AllDirectories).Count());
		}

		[TestMethod]
		public void Chemicals_N71N0J89ZH()
		{
			string file = "N71N0J89ZH.sdf";
			string dir = _outDirPrefix + Path.GetFileNameWithoutExtension(file);

			// Default mode
			Converter.Str2Spl(
				new ImportOptions {
					InputFile = CmdLineUtils.ParsePathWithEncodingPrefix(file),
				},
				new ConvertOptions { },
				new ExportOptions {
					OutDir = dir
				});

			Assert.AreEqual(1, Directory.GetFiles(Path.Combine(dir, Converter.OUT_DIR), "*.xml", SearchOption.AllDirectories).Count());
		}

		[TestMethod]
		public void Chemicals_4B136GSG9M()
		{
			string file = "4B136GSG9M.sdf";
			string dir = _outDirPrefix + Path.GetFileNameWithoutExtension(file);

			// Default mode
			Converter.Str2Spl(
				new ImportOptions {
					InputFile = CmdLineUtils.ParsePathWithEncodingPrefix(file),
				},
				new ConvertOptions { },
				new ExportOptions {
					OutDir = dir
				});

			Assert.AreEqual(1, Directory.GetFiles(Path.Combine(dir, Converter.OUT_DIR), "*.xml", SearchOption.AllDirectories).Count());
		}

		[TestMethod]
		public void Chemicals_490D9F069T()
		{
			string file = "490D9F069T.sdf";
			string dir = _outDirPrefix + Path.GetFileNameWithoutExtension(file);

			// Default mode
			var r = Converter.Str2Spl(
				new ImportOptions {
					InputFile = CmdLineUtils.ParsePathWithEncodingPrefix(file),
					Features = SplFeature.FromList("debug-ignore-description")
				},
				new ConvertOptions { },
				new ExportOptions {
					OutDir = dir
				});

			Assert.AreEqual(2, r.Count, "Unexpected number of error files generated");
			Assert.IsFalse(r.Any(f => f.Item1 == Converter.OutputFileType.Spl), "SPL file produced while should not");
		}

		/// <summary>
		/// SRS-125
        /// TP: This test was looking for a DIFF folder, implying it's seen this substance before
        /// but I don't see how that could be the case, unless there was also an expected
        /// old hash set that this is meant to be using. Since it's difficult to have in
        /// a test that relies on a static snapshot of an old report, it's been changed just
        /// to confirm that it works in general, not that it's different from an old version.
        /// 
		/// </summary>
		//[TestMethod]
		public void Chemicals_FP3LLW01BL()
		{
			string file = "FP3LLW01BL.sdf";
			string dir = _outDirPrefix + Path.GetFileNameWithoutExtension(file);

			// Default mode
			Converter.Str2Spl(
				new ImportOptions {
					InputFile = CmdLineUtils.ParsePathWithEncodingPrefix(file),
				},
				new ConvertOptions {
					Validate = true,
					CheckRefs = true,
				},
				new ExportOptions {
					OutDir = dir
				});

			Assert.AreEqual(1, Directory.GetFiles(Path.Combine(dir, Converter.OUT_DIR), "*.xml", SearchOption.AllDirectories).Count());
			//TP: Commented out to remove diff check
            //Assert.IsTrue(Directory.GetFiles(Path.Combine(dir, Converter.DIFF_DIR)).Count() > 0);
		}

		[TestMethod]
		public void Chemicals_DFN9D6Y7HT()
		{
			string file = "DFN9D6Y7HT.sdf";
			string dir = _outDirPrefix + Path.GetFileNameWithoutExtension(file);

			// Default mode
			Converter.Str2Spl(
				new ImportOptions {
					InputFile = CmdLineUtils.ParsePathWithEncodingPrefix(file),
				},
				new ConvertOptions {
					CheckRefs = true,
				},
				new ExportOptions {
					OutDir = dir
				});

			Assert.AreEqual(1, Directory.GetFiles(Path.Combine(dir, Converter.OUT_DIR), "*.xml", SearchOption.AllDirectories).Count());
            if (Directory.Exists(Path.Combine(dir, Converter.DIFF_DIR))) {
                Assert.AreEqual(0, Directory.GetFiles(Path.Combine(dir, Converter.DIFF_DIR)).Count());
            }
		}

		[TestMethod]
		public void Chemicals_no_UNII()
		{
			string file = "no-UNII.sdf";
			string dir = _outDirPrefix + Path.GetFileNameWithoutExtension(file);

			Converter.Str2Spl(
				new ImportOptions { InputFile = file },
				new ConvertOptions {
					GenerateMode = GenerateMode.NewSubstance
				},
				new ExportOptions {
					OutDir = dir
				});

			Assert.AreEqual(1, Directory.GetFiles(Path.Combine(dir, Converter.OUT_DIR), "*.xml", SearchOption.AllDirectories).Count());

			Directory.Delete(dir, true);

			file = "no-UNII.mol";
			Directory.CreateDirectory("indir");
			File.Move(file, Path.Combine("indir", file));
			file = Path.Combine("indir", file);

			Converter.Str2Spl(
				new ImportOptions { InputFile = "indir" },
				new ConvertOptions {
					GenerateMode = GenerateMode.NewSubstance
				},
				new ExportOptions {
					OutDir = dir
				});

			Assert.AreEqual(1, Directory.GetFiles(Path.Combine(dir, Converter.OUT_DIR), "*.xml", SearchOption.AllDirectories).Count());
		}

		/// <summary>
		/// Tests for SRS-122 Missing opt act
		/// </summary>
		[TestMethod]
		public void Chemicals_OptAct()
		{
			string file = "3CWS3B755Y.sdf";
			string dir = _outDirPrefix + Path.GetFileNameWithoutExtension(file);

			// Default mode
			Converter.Str2Spl(
				new ImportOptions {
					InputFile = CmdLineUtils.ParsePathWithEncodingPrefix(file),
				},
				new ConvertOptions {
					CheckRefs = true,
				},
				new ExportOptions {
					OutDir = dir
				});

			Assert.AreEqual(1, Directory.GetFiles(Path.Combine(dir, Converter.OUT_DIR), "*.xml", SearchOption.AllDirectories).Count());
            if (Directory.Exists(Path.Combine(dir, Converter.DIFF_DIR))) {
                Assert.AreEqual(0, Directory.GetFiles(Path.Combine(dir, Converter.DIFF_DIR)).Count());
            }

			var xdoc = XDocument.Load(Directory.GetFiles(Path.Combine(dir, Converter.OUT_DIR), "*.xml", SearchOption.AllDirectories).First());
			Assert.AreEqual("C103203", xdoc.SplOptActCode());

			file = "2NJ66SLA5C.sdf";
			dir = _outDirPrefix + Path.GetFileNameWithoutExtension(file);

			// Default mode
			Converter.Str2Spl(
				new ImportOptions {
					InputFile = CmdLineUtils.ParsePathWithEncodingPrefix(file),
				},
				new ConvertOptions {
					CheckRefs = true,
				},
				new ExportOptions {
					OutDir = dir
				});

			Assert.AreEqual(1, Directory.GetFiles(Path.Combine(dir, Converter.OUT_DIR), "*.xml", SearchOption.AllDirectories).Count());
            if (Directory.Exists(Path.Combine(dir, Converter.DIFF_DIR))) {
                Assert.AreEqual(0, Directory.GetFiles(Path.Combine(dir, Converter.DIFF_DIR)).Count());
            }

			xdoc = XDocument.Load(Directory.GetFiles(Path.Combine(dir, Converter.OUT_DIR), "*.xml", SearchOption.AllDirectories).First());
			Assert.AreEqual("C103202", xdoc.SplOptActCode());
		}

		/// <summary>
		/// SRS-126 Issue with "data in identifying description"
		/// </summary>
		[TestMethod]
		public void Chemicals_DataInIdentifyingDescription()
		{
			string file = "SDI4U20V1J.sdf";
			string dir = _outDirPrefix + Path.GetFileNameWithoutExtension(file);

			// Default mode
			Converter.Str2Spl(
				new ImportOptions {
					InputFile = CmdLineUtils.ParsePathWithEncodingPrefix(file),
				},
				new ConvertOptions {
					CheckRefs = true,
				},
				new ExportOptions {
					OutDir = dir
				});

			Assert.AreEqual(1, Directory.GetFiles(Path.Combine(dir, Converter.ERRORS_DIR, @"SRSException\fields-validation")).Count());

			file = "00HPQ7674K.sdf";
			dir = _outDirPrefix + Path.GetFileNameWithoutExtension(file);

			// Default mode
			Converter.Str2Spl(
				new ImportOptions {
					InputFile = CmdLineUtils.ParsePathWithEncodingPrefix(file),
				},
				new ConvertOptions {
					CheckRefs = true,
				},
				new ExportOptions {
					OutDir = dir
				});

			Assert.AreEqual(1, Directory.GetFiles(Path.Combine(dir, Converter.OUT_DIR), "*.xml", SearchOption.AllDirectories).Count());
            if (Directory.Exists(Path.Combine(dir, Converter.DIFF_DIR))) {
                Assert.AreEqual(0, Directory.GetFiles(Path.Combine(dir, Converter.DIFF_DIR)).Count());
            }
		}

		[TestMethod]
		public void Chemicals_FixSrsXml()
		{
			string file = "4B136GSG9M.sdf";
			string dir = _outDirPrefix + Path.GetFileNameWithoutExtension(file) + "_FixSrsXml";

			Converter.Str2Spl(
				new ImportOptions {
					InputFile = CmdLineUtils.ParsePathWithEncodingPrefix(file),
					NoAutoFix = true
				},
				new ConvertOptions {
					CheckRefs = true,
				},
				new ExportOptions {
					OutDir = dir
				});

			Assert.AreEqual(0, Directory.GetFiles(Path.Combine(dir, Converter.OUT_DIR), "*.xml", SearchOption.AllDirectories).Count());
			Assert.AreEqual(1, Directory.GetFiles(Path.Combine(dir, Converter.ERRORS_DIR, @"SRSException\invalid_srs_xml")).Count());

			Converter.Str2Spl(
				new ImportOptions {
					InputFile = CmdLineUtils.ParsePathWithEncodingPrefix(file),
					NoAutoFix = false
				},
				new ConvertOptions {
					CheckRefs = true,
				},
				new ExportOptions {
					OutDir = dir
				});

			Assert.AreEqual(1, Directory.GetFiles(Path.Combine(dir, Converter.OUT_DIR), "*.xml", SearchOption.AllDirectories).Count());
		}

		[TestMethod]
		public void Chemicals_6VKW7R97ZU()
        {
           
            string file = "6VKW7R97ZU.sdf";
			string dir = _outDirPrefix + Path.GetFileNameWithoutExtension(file);
            TestContext.WriteLine(dir);
            Converter.Str2Spl(
				new ImportOptions {
					InputFile = CmdLineUtils.ParsePathWithEncodingPrefix(file),
					NoAutoFix = true
				},
				new ConvertOptions {
					CheckRefs = true,
				},
				new ExportOptions {
					OutDir = dir
				});

			Assert.AreEqual(0, Directory.GetFiles(Path.Combine(dir, Converter.OUT_DIR), "*.xml", SearchOption.AllDirectories).Count());
			Assert.AreEqual(1, Directory.GetFiles(Path.Combine(dir, Converter.ERRORS_DIR, @"SRSException\invalid_srs_xml")).Count());
		}

		[TestMethod]
		public void Chemicals_E3V9B5ZAEO()
		{
			string file = "E3V9B5ZAEO.sdf";
			string dir = _outDirPrefix + Path.GetFileNameWithoutExtension(file);

			Converter.Str2Spl(
				new ImportOptions {
					InputFile = CmdLineUtils.ParsePathWithEncodingPrefix(file),
					NoAutoFix = true
				},
				new ConvertOptions { },
				new ExportOptions {
					OutDir = dir
				});

			Assert.AreEqual(1, Directory.GetFiles(Path.Combine(dir, Converter.ERRORS_DIR, @"SrsException\invalid_mol")).Count());
		}

		[TestMethod]
		public void Chemicals_6P52D0J76B()
		{
			string file = "6P52D0J76B.sdf";
			string dir = _outDirPrefix + Path.GetFileNameWithoutExtension(file);

			Converter.Str2Spl(
				new ImportOptions {
					InputFile = CmdLineUtils.ParsePathWithEncodingPrefix(file),
					NoAutoFix = true
				},
				new ConvertOptions { },
				new ExportOptions {
					OutDir = dir
				});

			Assert.AreEqual(1, Directory.GetFiles(Path.Combine(dir, Converter.OUT_DIR), "*.xml", SearchOption.AllDirectories).Count());
            string spl = getXMLRaw(dir);
            System.Console.WriteLine(spl);

        }


        [TestMethod]
        public void Chemicals_EPIMERIC() {
            string file = "COE40KKG35.sdf";
            string dir = _outDirPrefix + Path.GetFileNameWithoutExtension(file);

            Converter.Str2Spl(
                new ImportOptions {
                    InputFile = CmdLineUtils.ParsePathWithEncodingPrefix(file),
                    NoAutoFix = true
                },
                new ConvertOptions { },
                new ExportOptions {
                    OutDir = dir
                });

            Assert.AreEqual(1, Directory.GetFiles(Path.Combine(dir, Converter.OUT_DIR), "*.xml", SearchOption.AllDirectories).Count());

            string spl = getXMLRaw(dir);
            System.Console.WriteLine(spl);
            Assert.IsTrue(spl.ToUpper().Contains("EPIMERIC"));

        }

        [TestMethod]
		public void Chemicals_example1()
		{
			string file = "example1.mol";
			string dir = _outDirPrefix + Path.GetFileNameWithoutExtension(file);

			var files = Converter.Mol2Spl(
				new ImportOptions {
					InputFile = CmdLineUtils.ParsePathWithEncodingPrefix(file),
				},
				new ConvertOptions { },
				new ExportOptions {
					OutDir = dir,
					UNIIFile = true,
					NoDocIdFile = true,
					MolFile = true,
					MolsAsSdf = true,
				});

			Assert.AreEqual(1, files.Where(f => f.Item1 == Converter.OutputFileType.UniiSpl).Count());
			//YP 
			//Changing this to expect 1 count instead of 2 stereomers due to change from SRS-412
			//Assert.AreEqual(2, files.Where(f => f.Item1 == Converter.OutputFileType.Mol).Count());
			Assert.AreEqual(1, files.Where(f => f.Item1 == Converter.OutputFileType.Mol).Count());
			Assert.AreEqual(1, files.Where(f => f.Item1 == Converter.OutputFileType.MolsSdf).Count());
			TestContext.AddResultFile(files.Where(f => f.Item1 == Converter.OutputFileType.UniiSpl).First().Item2);
		}

		[TestMethod]
		public void Chemicals_example2()
		{
			string file = "example2.mol";
			string dir = _outDirPrefix + Path.GetFileNameWithoutExtension(file);

			var files = Converter.Mol2Spl(
				new ImportOptions {
					InputFile = CmdLineUtils.ParsePathWithEncodingPrefix(file),
				},
				new ConvertOptions { },
				new ExportOptions {
					OutDir = dir,
					UNIIFile = true,
					NoDocIdFile = true,
					MolFile = true,
					MolsAsSdf = true,
				});

			Assert.AreEqual(1, files.Where(f => f.Item1 == Converter.OutputFileType.UniiSpl).Count());
			Assert.AreEqual(4, files.Where(f => f.Item1 == Converter.OutputFileType.Mol).Count());
			Assert.AreEqual(1, files.Where(f => f.Item1 == Converter.OutputFileType.MolsSdf).Count());
			TestContext.AddResultFile(files.Where(f => f.Item1 == Converter.OutputFileType.UniiSpl).First().Item2);
		}

		[TestMethod]
		public void Chemicals_example3()
		{
			string file = "example3.mol";
			string dir = _outDirPrefix + Path.GetFileNameWithoutExtension(file);

			var files = Converter.Mol2Spl(
				new ImportOptions {
					InputFile = CmdLineUtils.ParsePathWithEncodingPrefix(file),
				},
				new ConvertOptions { },
				new ExportOptions {
					OutDir = dir,
					UNIIFile = true,
					NoDocIdFile = true,
					MolFile = true,
					MolsAsSdf = true,
				});

			Assert.AreEqual(1, files.Where(f => f.Item1 == Converter.OutputFileType.UniiSpl).Count());
			Assert.AreEqual(128, files.Where(f => f.Item1 == Converter.OutputFileType.Mol).Count());
			Assert.AreEqual(1, files.Where(f => f.Item1 == Converter.OutputFileType.MolsSdf).Count());
			TestContext.AddResultFile(files.Where(f => f.Item1 == Converter.OutputFileType.UniiSpl).First().Item2);
		}

        [TestMethod]
        public void reorderAtomsOnMolfileWithSRUsChargesAndIsotopeShouldChangeMolfileAndBeReversable() {
            String molfile = @"
   JSDraw212161817452D

  3  2  0  0  0  0              0 V2000
   14.6640   -8.1380    0.0000 O   0  0  0  0  0  0  0  0  0  0  0  0
   16.0150   -7.3580    0.0000 C   0  3  0  0  0  0  0  0  0  0  0  0
   17.3660   -8.1380    0.0000 N   0  0  0  0  0  0  0  0  0  0  0  0
  1  2  1  0  0  0  0
  2  3  1  0  0  0  0
M  ISO  1   1  16
M  STY  1   1 SRU
M  SAL   1  1   2
M  SBL   1  2   1   2
M  SMT   1 n
M  SDI   1  4   15.2360   -8.3980   15.2360   -6.7860
M  SDI   1  4   16.6400   -6.7860   16.6400   -8.3980
M  CHG  1   2   1
M  END";
            NewMolecule nm = new NewMolecule(molfile);
            NewMolecule shuffled = nm.reorderBasedOn(new int[] { 2, 0, 1 });
            Assert.AreNotEqual(molfile, shuffled.Mol);
            Assert.IsTrue(shuffled.Mol.Split('\n')[4].Contains("C"));
            String nmol = shuffled.Mol;

            //shuffle
            NewMolecule unshuffled = shuffled.reorderBasedOn(new int[] { 1, 2, 0 });//unshuffle
            Assert.AreEqual(molfile, unshuffled.Mol);

        }

    }
}
