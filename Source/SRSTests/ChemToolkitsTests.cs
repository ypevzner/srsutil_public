using com.epam.indigo;
using FDA.SRS.Processing;
using FDA.SRS.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Serilog;
using System;
using System.IO;
using System.Linq;
using System.Text;

using NewMolecule=FDA.SRS.Utils.SDFUtil.NewMolecule;

namespace UnitTestProject
{
	[TestClass]
	[DeploymentItem(@"..\..\Resources\Chemicals\")]
	[DeploymentItem(@"..\..\Resources\DLL\indigo.dll")]
	[DeploymentItem(@"..\..\Resources\DLL\indigo-inchi.dll")]
	public class ChemToolkitsTests
	{
		[TestMethod]
		public void ExtractSpecificSGroupsTest()
		{
			Converter.SdfExtractSdf(
				new ImportOptions {
					InputFile = CmdLineUtils.ParsePathWithEncodingPrefix("00HPQ7674K.sdf"),
				},
				new ConvertOptions {
					SGroupTypes = SGroupType.GEN | SGroupType.DAT
				},
				new ExportOptions {
					OutDir = "out",
				});

			Assert.AreEqual(0, Directory.GetFiles("out").Count());

			Converter.SdfExtractSdf(
				new ImportOptions {
					InputFile = CmdLineUtils.ParsePathWithEncodingPrefix("025P9I542Y.sdf"),
				},
				new ConvertOptions {
					SGroupTypes = SGroupType.GEN | SGroupType.DAT
				},
				new ExportOptions {
					OutDir = "out",
				});

			Assert.AreEqual(1, Directory.GetFiles("out").Count());
		}

		[TestMethod]
		public void SGroups_UseIndigoSGroups()
		{
			using ( Indigo indigo = new Indigo() ) {
				foreach ( IndigoObject mol in indigo.iterateSDFile("J8QFJ59TK6.sdf") ) {
					foreach ( IndigoObject dsg in mol.iterateGenericSGroups() ) {
						System.Console.WriteLine("data sgroup " + dsg.index());
						foreach ( IndigoObject atom in dsg.iterateAtoms() )
							System.Console.WriteLine("  atom " + atom.index());
					}
					foreach ( IndigoObject dsg in mol.iterateDataSGroups() ) {
						System.Console.WriteLine("data sgroup " + dsg.index());
						foreach ( IndigoObject atom in dsg.iterateAtoms() )
							System.Console.WriteLine("  atom " + atom.index());
					}
				}

				foreach ( IndigoObject mol in indigo.iterateSDFile("6E17K3343P.sdf") ) {
					foreach ( IndigoObject dsg in mol.iterateGenericSGroups() ) {
						System.Console.WriteLine("data sgroup " + dsg.index());
						foreach ( IndigoObject atom in dsg.iterateAtoms() )
							System.Console.WriteLine("  atom " + atom.index());
					}
					foreach ( IndigoObject dsg in mol.iterateDataSGroups() ) {
						System.Console.WriteLine("data sgroup " + dsg.index());
						foreach ( IndigoObject atom in dsg.iterateAtoms() )
							System.Console.WriteLine("  atom " + atom.index());
					}
				}
			}

			// Assert.Fail("Fail by design");
		}
        [TestMethod]
        public void CanonicalizedMoleculeShouldHaveSameAtomMaps() {
            String mol1 = @"
  -ISIS-  06101519152D

 14 13  0  0  1  0  0  0  0  0999 V2000
    9.0500   -3.4875    0.0000 C   0  0  0  0  0  0  0  0  0  0  0  0
    3.2875   -3.9917    0.0000 C   0  0  0  0  0  0  0  0  0  0  0  0
    4.1417   -3.5042    0.0000 C   0  0  2  0  0  0  0  0  0  0  0  0
    8.2500   -3.9292    0.0000 C   0  0  2  0  0  0  0  0  0  0  0  0
    9.0500   -2.6167    0.0000 O   0  0  0  0  0  0  0  0  0  0  0  0
    3.2875   -4.8875    0.0000 O   0  0  0  0  0  0  0  0  0  0  0  0
    7.4000   -3.4875    0.0000 C   0  0  0  0  0  0  0  0  0  0  0  0
    4.9667   -3.9625    0.0000 C   0  0  0  0  0  0  0  0  0  0  0  0
    4.1417   -2.5625    0.0000 N   0  0  0  0  0  0  0  0  0  0  0  0
    8.2500   -4.8875    0.0000 N   0  0  0  0  0  0  0  0  0  0  0  0
    6.5917   -3.9625    0.0000 S   0  0  0  0  0  0  0  0  0  0  0  0
    9.8750   -3.9292    0.0000 O   0  0  0  0  0  0  0  0  0  0  0  0
    5.7917   -3.4875    0.0000 S   0  0  0  0  0  0  0  0  0  0  0  0
    2.5042   -3.5042    0.0000 O   0  0  0  0  0  0  0  0  0  0  0  0
  2  3  1  0  0  0  0
  3  8  1  0  0  0  0
  4  1  1  0  0  0  0
  5  1  2  0  0  0  0
  6  2  2  0  0  0  0
  7  4  1  0  0  0  0
  8 13  1  0  0  0  0
  3  9  1  1  0  0  0
  4 10  1  1  0  0  0
 11  7  1  0  0  0  0
 13 11  1  0  0  0  0
 12  1  1  0  0  0  0
 14  2  1  0  0  0  0
M  END";
            NewMolecule nm = new NewMolecule(mol1);
            int[] nums = nm.CanonicalNumbers();

            //this one should be canonical now
            NewMolecule nm2= nm.ReorderCanonically();            
            String mol2=nm2.Mol;

            int[] sameNumbers = nm2.CanonicalNumbers();
            NewMolecule doubleCanon = nm2.ReorderCanonically();

            String mol3 = doubleCanon.Mol;

            for (int i = 0; i < 14; i++) {
                Assert.AreEqual(i, sameNumbers[i]);
            }
            
        }


        [TestMethod]
		public void SGroups_UseIndigoSGroups2()
		{
			using ( Indigo indigo = new Indigo() )
			using ( IndigoObject mol = indigo.loadMoleculeFromFile("490D9F069T.original.mol") ) {
				int n = mol.countGenericSGroups();
				Log.Information("countGenericSGroups: {n}", n);
				n = mol.countSuperatoms();
				Log.Information("countSuperatoms: {n}", n);
				n = mol.countRepeatingUnits();
				Log.Information("countRepeatingUnits: {n}", n);
				n = mol.countMultipleGroups();
				Log.Information("countMultipleGroups: {n}", n);
				n = mol.countDataSGroups();
				Log.Information("countDataSGroups: {n}", n);

				foreach ( IndigoObject sg in mol.iterateGenericSGroups() ) {
					Log.Information("genericSGroup({n}): {sgroup_info}", sg.index(), getSGroupInfo(sg));
				}
				foreach ( IndigoObject sg in mol.iterateDataSGroups() ) {
					Log.Information("dataSGroup({n}): {sgroup_info}", sg.index(), getSGroupInfo(sg));
				}
				foreach ( IndigoObject sg in mol.iterateMultipleGroups() ) {
					Log.Information("multipleSGroup({n}): {sgroup_info}", sg.index(), getSGroupInfo(sg));
				}
			}

			// Assert.Fail("Fail by design");
		}

		private static string getSGroupInfo(IndigoObject sg)
		{
			StringBuilder sb = new StringBuilder();
			try {
				sb.Append(String.Format("getSGroupClass(): {0}; ", sg.getSGroupClass()));
			}
			catch {
			}
			try {
				sb.Append(String.Format("getSGroupDisplayOption(): {0}; ", sg.getSGroupDisplayOption()));
			}
			catch {
			}
			try {
				sb.Append(String.Format("getSGroupIndex(): {0}; ", sg.getSGroupIndex()));
			}
			catch {
			}
			try {
				sb.Append(String.Format("getSGroupMultiplier(): {0}; ", sg.getSGroupMultiplier()));
			}
			catch {
			}
			try {
				sb.Append(String.Format("getSGroupName(): {0}; ", sg.getSGroupName()));
			}
			catch {
			}
			try {
				sb.Append(String.Format("getSGroupNumCrossBonds(): {0}; ", sg.getSGroupNumCrossBonds()));
			}
			catch {
			}
			try {
				sb.Append(String.Format("getSGroupType(): {0}; ", sg.getSGroupType()));
			}
			catch {
			}

			sb.Append("atoms: " + String.Join(", ", sg.iterateAtoms().Cast<IndigoObject>().Select(a => a.index().ToString())));

			return sb.ToString();
		}
	}
}
