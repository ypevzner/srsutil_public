using FDA.SRS.Processing;
using FDA.SRS.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace UnitTestProject
{
    [TestClass]
    //TP: I don't know why, but the active directory here
    //seems to be srs\Source\SRSTests\obj\Debug or something close
    //We need to do some funky traversal to make sure the right things
    //get copied
    [DeploymentItem(@"..\..\Resources\Polymers\")]
    [DeploymentItem(@"..\..\..\..\Data\srs2mol\", @"Data\srs2mol")]
    [DeploymentItem(@"polymer.exe")]

    public partial class PolymerTests {
        public static String SIMPLE_LINEAR = @"
  Symyx   04271719122D 1   1.00000     0.00000     0

 18 18  0     0  0            999 V2000
    2.1458    0.6875    0.0000 O   0  0  0  0  0  0           0  0  0
    3.1708    1.1041    0.0000 C   0  0  0  0  0  0           0  0  0
    3.8833    0.6916    0.0000 C   0  0  0  0  0  0           0  0  0
    4.5958    1.1083    0.0000 O   0  0  0  0  0  0           0  0  0
    5.6667    0.6958    0.0000 C   0  0  0  0  0  0           0  0  0
    5.6682   -0.1357    0.0000 C   0  0  0  0  0  0           0  0  0
    6.3831   -0.5486    0.0000 C   0  0  0  0  0  0           0  0  0
    7.0995   -0.1352    0.0000 C   0  0  0  0  0  0           0  0  0
    7.0966    0.6953    0.0000 C   0  0  0  0  0  0           0  0  0
    6.3813    1.1044    0.0000 C   0  0  0  0  0  0           0  0  0
    7.8083   -0.5459    0.0000 C   0  0  3  0  0  0           0  0  0
    8.5208   -0.1292    0.0000 C   0  0  0  0  0  0           0  0  0
    9.2333   -0.5417    0.0000 C   0  0  3  0  0  0           0  0  0
    9.9458   -0.1250    0.0000 C   0  0  0  0  0  0           0  0  0
    9.2292   -1.3667    0.0000 C   0  0  0  0  0  0           0  0  0
    9.9458   -0.9542    0.0000 C   0  0  0  0  0  0           0  0  0
    7.3917   -1.2584    0.0000 C   0  0  0  0  0  0           0  0  0
    8.2167   -1.2584    0.0000 C   0  0  0  0  0  0           0  0  0
  4  5  1  0     0  0
  9 10  2  0     0  0
 10  5  1  0     0  0
  2  3  1  0     0  0
  8 11  1  0     0  0
  5  6  2  0     0  0
 11 12  1  0     0  0
  1  2  1  0     0  0
 12 13  1  0     0  0
  6  7  1  0     0  0
 13 14  1  0     0  0
  3  4  1  0     0  0
 13 15  1  0     0  0
  7  8  2  0     0  0
 13 16  1  0     0  0
 11 17  1  0     0  0
  8  9  1  0     0  0
 11 18  1  0     0  0
M  STY  1   1 SRU
M  SLB  1   1   1
M  SCN  1   1 HT
M  SAL   1  3   2   3   4
M  SBL   1  2   1   8
M  SMT   1 A
M  SDI   1  4    2.7600    0.5100    2.7600    1.3100
M  SDI   1  4    5.2100    1.3600    5.2100    0.5100
M  END";

        /// <summary>
        /// This test confirms that a polymer gets decomposed into 3 components,
        /// and each has the expected InChI
        /// </summary>
        [TestMethod]
        public void CanDecomposeSimpleLinearPolymer() {
           
            PolymerParser pp = PolymerParser.instance();

            string[] expectedInChI = new string[] { "InChI=1B/C2H4O/c1-2-3-1/h1-2H2/z101-1-3(1,2,1,3,2,3)",
                                               "InChI=1S/H2O/h1H2",
                                               "InChI=1S/C14H22/c1-13(2,3)11-14(4,5)12-9-7-6-8-10-12/h6-10H,11H2,1-5H3" };
            int i = 0;
            using (SdfReader r = pp.decomposeToReader(SIMPLE_LINEAR)) {
                foreach (SdfRecord sdf in r.Records) {
                    String s = sdf.ToString();
                    Console.WriteLine(s);

                    //this is the current location where the standard inchi is found
                    //This has been updated in the past, and may need to be in the future
                    //as well
                    String ss = sdf.GetFieldValue("Computed_B_InChI");

                    Assert.AreEqual(expectedInChI[i], ss);
                    i++;
                }
            }
            Assert.AreEqual(3, i, "Expected 3 components, found " + i);
        }

        /// <summary>
        /// Confirms that a polymer decomposition results in the right SRU
        /// based on inchi, and has the correct atoms mapped for the connections
        /// </summary>
        [TestMethod]
        public void CanDecomposeSimpleLinearPolymerToUnits() {
            PolymerParser pp = PolymerParser.instance();

            List<PolymerUnit> units=new List<PolymerUnit>(pp.decompose(SIMPLE_LINEAR));
            Assert.AreEqual("InChI=1I/C2H4OX2/c4-2-1-3-5/h1-2H2", units[0].getUnitInChI());
            int[] connecting = units[0].getConnectingAtoms();
            Assert.AreEqual(new int[] {2, 0}.JoinToString(";"), connecting.JoinToString(";"));
            Assert.AreEqual("A", units[0].getLabels()[0]);

            //check that the canonical order is as expected.
            //this had previously been 0,1,2,3,4 but recently changed to be 1,0,2,3,4 but I don't
            //know why. 
            //YP 09/2020
            //this is deprecated as of 09/13/2020 as the latest version of polymer.exe produces atoms that are already in a canonical order
            //Assert.AreEqual(new int[] {1,0,2,3,4}.JoinToString(";"), units[0].getCanonicalAtoms().JoinToString(";"));

        }
        
        
        [TestMethod]
        public void CanReadBasicPolymerRecordAndParse() {
            String file = "I274II9A1F.sdf";
            using (SdfReader r = new SdfReader(file)) {
                SdfRecord sdf = r.Records.First();
                String s=sdf.ToString();
                JObject job = sdf.GetGSRSJson();
                String mfile=job.SelectToken("..polymer.idealizedStructure.molfile").ToString();
                
                Console.WriteLine(mfile);
                IEnumerable<PolymerUnit> units=PolymerParser.instance()
                                                     .decompose(mfile);

                Assert.AreEqual(3,units.Count());
            }
        }
    }

}
