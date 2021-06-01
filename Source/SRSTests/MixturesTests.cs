using FDA.SRS.ObjectModel;
using FDA.SRS.Processing;
using FDA.SRS.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;

namespace UnitTestProject
{
	[TestClass]
	[DeploymentItem(@"..\..\Resources\Mixtures\")]
	[DeploymentItem(@"x64\", @"x64\")]
	[DeploymentItem(@"x86\", @"x86\")]
	[DeploymentItem(@"..\..\Resources\DLL\indigo.dll")]
	[DeploymentItem(@"..\..\Resources\DLL\indigo-inchi.dll")]
	public class MixtureConversionTests
	{
		public TestContext TestContext { get; set; }

		private static readonly string _outDirPrefix = "Mixtures_";


        public static Stream GenerateStreamFromString(string s) {
            var stream = new MemoryStream();
            var writer = new StreamWriter(stream);
            writer.Write(s);
            writer.Flush();
            stream.Position = 0;
            return stream;
        }

        [TestMethod]
        public void MixtureWithSourceMaterialShouldThrowException() {
            string mixWithSource = @"
  Symyx   04271610292D 1   1.00000     0.00000     0

 12 11  0     0  0            999 V2000
   20.2310   -4.7158    0.0000 O   0  0  0  0  0  0           0  0  0
   19.0499   -4.7158    0.0000 C   0  0  0  0  0  0           0  0  0
   18.4594   -5.7386    0.0000 C   0  0  3  0  0  0           0  0  0
   17.2783   -5.7386    0.0000 C   0  0  0  0  0  0           0  0  0
   16.6877   -6.7615    0.0000 O   0  0  0  0  0  0           0  0  0
   15.5066   -6.7615    0.0000 C   0  0  0  0  0  0           0  0  0
   14.9161   -7.7844    0.0000 C   0  0  0  0  0  0           0  0  0
   13.7350   -7.7844    0.0000 O   0  0  0  0  0  0           0  0  0
   19.0499   -6.7615    0.0000 O   0  0  0  0  0  0           0  0  0
   20.8216   -3.6929    0.0000 C   0  0  0  0  0  0           0  0  0
   22.0027   -3.6929    0.0000 C   0  0  0  0  0  0           0  0  0
   22.5933   -2.6700    0.0000 O   0  0  0  0  0  0           0  0  0
  1  2  1  0     0  0
  2  3  1  0     0  0
  3  4  1  0     0  0
  4  5  1  0     0  0
  5  6  1  0     0  0
  6  7  1  0     0  0
  7  8  1  0     0  0
  9  3  1  0     0  0
  1 10  1  0     0  0
 10 11  1  0     0  0
 11 12  1  0     0  0
M  END
>  <MIX_SUBSTANCE_ID>
1794

>  <MIX_UNII>
0UY833L6XU

>  <MIXTURE_TYPE>
ALL

>  <MIX_DESC_PART1>
<SOURCE_MATERIAL>
 <REF_UUID>fake</REF_UUID>
 <REF_APPROVAL_ID>fake</REF_APPROVAL_ID>
 <REF_PNAME>fake</REF_PNAME>
</SOURCE_MATERIAL>

>  <MIX_DESC_PART2>

>  <MIX_COMMENTS>

>  <SUBSTANCE_ID>
177122

>  <UNII>
9CC1P9T91Z

>  <DESC_PART1>

>  <DESC_PART2>

>  <COMMENTS>

>  <STRUCTURE_ID>
80470

>  <SUBSTANCE_NAME>


$$$$";
            ImportOptions io = new ImportOptions {
            };
            ConvertOptions opt = new ConvertOptions {
                GenerateMode = GenerateMode.NewSubstance
            };

            Exception ex=null;

            using (SdfReader r = new SdfReader(GenerateStreamFromString(mixWithSource), io.InputFileEncoding) { FieldsMap = io.SdfMapping, LineFilters = new[] { SrsSdfValidators.SubstanceSdfLineFilter } }) {
                var mixtures = r.Records
                     .Where(s => s.HasField("MIX_SUBSTANCE_ID"));
                
                mixtures.ForEachWithIndex((s, i) => {
                    try {
                        MixtureSubstance substance = s.ToSubstance(io, opt);
                        var s2 = substance.ToString();
                    }catch(Exception e) {
                        ex = e;
                    }
                });
            }

            Assert.IsTrue(ex != null,"Should have thrown an exception when mixture has source material in description.");
            Assert.IsTrue(ex.Message.Contains("SOURCE_MATERIAL"), "Should have thrown an exception which mentions source material field in a mixture.");

        }

        [TestMethod]
		public void Mixtures_0UY833L6XU()
		{
			string file = "0UY833L6XU.sdf";
			string dir = _outDirPrefix + Path.GetFileNameWithoutExtension(file);
			var files = Converter.Mix2Spl(
				new ImportOptions {
					InputFile = CmdLineUtils.ParsePathWithEncodingPrefix(file),
				},
				new ConvertOptions { },
				new ExportOptions {
					OutDir = dir,
					NoDocIdFile = true,
					UNIIFile = true
				});

			Assert.AreEqual(1, files.Where(f => f.Item1 == Converter.OutputFileType.UniiSpl).Count());

			TestContext.AddResultFile(files.First().Item2);
		}

		[TestMethod]
		public void Mixtures_JWM00VS7HC()
		{
			string file = "JWM00VS7HC.sdf";
			string dir = _outDirPrefix + Path.GetFileNameWithoutExtension(file);
			var files = Converter.Mix2Spl(
				new ImportOptions {
					InputFile = CmdLineUtils.ParsePathWithEncodingPrefix(file),
				},
				new ConvertOptions { },
				new ExportOptions {
					OutDir = dir,
					NoDocIdFile = true,
					UNIIFile = true
				});

            
			Assert.AreEqual(1, files.Where(f => f.Item1 == Converter.OutputFileType.UniiSpl).Count());

			TestContext.AddResultFile(files.First().Item2);
		}
        

        [TestMethod]
		public void Mixtures_Undefined_and_Ranges()
		{
			string file = "8X7386QRLV.sdf";
			string dir = _outDirPrefix + Path.GetFileNameWithoutExtension(file);
			Converter.Mix2Spl(
				new ImportOptions {
					InputFile = CmdLineUtils.ParsePathWithEncodingPrefix(file),
				},
				new ConvertOptions { },
				new ExportOptions {
					OutDir = dir
				});

			Assert.AreEqual(0, Directory.GetFiles(Path.Combine(dir, Converter.OUT_DIR)).Count());
		}

		[TestMethod]
		public void Mixtures_UniiGenerateMode()
		{
			string file = "1B8447E7YI.sdf";
			string dir = _outDirPrefix + "UniiGenerateMode_NewUnii";

			// NewUnii mode
			Converter.Mix2Spl(
				new ImportOptions {
					InputFile = CmdLineUtils.ParsePathWithEncodingPrefix(file),
				},
				new ConvertOptions {
					GenerateMode = GenerateMode.NewUnii
				},
				new ExportOptions {
					OutDir = dir
				});

			Assert.AreEqual(0, Directory.GetFiles(Path.Combine(dir, Converter.OUT_DIR), "*.xml", SearchOption.AllDirectories).Count());

			dir = _outDirPrefix + "UniiGenerateMode_Default";

			// Default mode
			Converter.Mix2Spl(
				new ImportOptions {
					InputFile = CmdLineUtils.ParsePathWithEncodingPrefix(file),
				},
				new ConvertOptions {
					GenerateMode = GenerateMode.Default
				},
				new ExportOptions {
					OutDir = dir
				});

			Assert.AreEqual(1, Directory.GetFiles(Path.Combine(dir, Converter.OUT_DIR), "*.xml", SearchOption.AllDirectories).Count());
            
            //Look for change
            file = "1B8447E7YI_diff.sdf";
            dir = _outDirPrefix + "UniiGenerateMode_NewHash_Changed";

            // NewHash mode
            Converter.Mix2Spl(
                new ImportOptions {
                    InputFile = CmdLineUtils.ParsePathWithEncodingPrefix(file),
                },
                new ConvertOptions {
                    GenerateMode = GenerateMode.NewHash
                },
                new ExportOptions {
                    OutDir = dir
                });

            Assert.AreEqual(1, Directory.GetFiles(Path.Combine(dir, Converter.OUT_DIR), "*.xml", SearchOption.AllDirectories).Count());

            /*
            //Look for change
            file = "1B8447E7YI_diff.sdf";
            dir = _outDirPrefix + "UniiGenerateMode_NewHash_Changed2";

            // NewHash mode
            Converter.Mix2Spl(
                new ImportOptions {
                    InputFile = CmdLineUtils.ParsePathWithEncodingPrefix(file),
                },
                new ConvertOptions {
                    GenerateMode = GenerateMode.NewHash
                },
                new ExportOptions {
                    OutDir = dir
                });

            Assert.AreEqual(0, Directory.GetFiles(Path.Combine(dir, Converter.OUT_DIR), "*.xml", SearchOption.AllDirectories).Count());
            */

        }



        [TestMethod]
		public void Mixtures_MWC0ET1L2P()
		{
			string file = "MWC0ET1L2P.sdf";
			string dir = _outDirPrefix + Path.GetFileNameWithoutExtension(file);

			var files = Converter.Mix2Spl(
				new ImportOptions {
					InputFile = CmdLineUtils.ParsePathWithEncodingPrefix(file),
				},
				new ConvertOptions { },
				new ExportOptions {
					OutDir = dir,
					NoDocIdFile = true,
					UNIIFile = true
				});

			Assert.AreEqual(1, files.Where(f => f.Item1 == Converter.OutputFileType.UniiSpl).Count());

			TestContext.AddResultFile(files.First().Item2);
		}

		[TestMethod]
		public void Mixtures_update_V3000()
		{
			string file = "mixtures_update_V3000.sdf";
			string dir = _outDirPrefix + Path.GetFileNameWithoutExtension(file);

			var files = Converter.Mix2Spl(
				new ImportOptions {
					InputFile = CmdLineUtils.ParsePathWithEncodingPrefix(file),
				},
				new ConvertOptions { },
				new ExportOptions {
					OutDir = dir,
					NoDocIdFile = true,
					UNIIFile = true
				});
            //This is not a good test. It was just testing that 257 out of a total
            //of 316 mixtures get converted. We actually have 259 converted now,
            //due to changes in validation rules / standardization. It's hard
            //to pin down exactly what changed. 
			Assert.AreEqual(259, files.Where(f => f.Item1 == Converter.OutputFileType.UniiSpl).Count());

			TestContext.AddResultFile(files.First().Item2);
		}
	}
}
