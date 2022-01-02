using ICSharpCode.SharpZipLib.GZip;
//using MoleculeObjects;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace FDA.SRS.Utils
{
    public class PolymerUnit {
        SdfRecord sdr;

        public PolymerUnit(SdfRecord sd) {
            this.sdr = sd;
        }

        /// <summary>
        /// Gets the modified InChI for SRUs. This is distinct from the
        /// typical InChI for a moleucle in that it can handle connection points.        /// 
        /// </summary>
        /// <returns>
        /// The modified inchi
        /// </returns>
        public String getUnitInChI() {
            return sdr.GetFieldValue("Computed_B_InChI");
        }

        /// <summary>
        /// Gets the modified InChIKey for SRUs. This is distinct from the
        /// typical InChIKey for a moleucle in that it can handle connection points.        /// 
        /// </summary>
        /// <returns>
        /// The modified inchi
        /// </returns>
        public String getUnitInChIKey()
        {
            return sdr.GetFieldValue("Computed_B_InChIKey");
        }

        public int[] getFragmentIds()
        {
            String fragment_ids = sdr.GetFieldValue("FRAGMENT_IDS");
            return fragment_ids.Split('\n').Map(s => s.Replace("\r", "").Trim())
                               .Map(s => int.Parse(s))
                               .Cast<int>()
                               .OrderBy(item => item)
                               .ToArray();
        }
        /// <summary>
        /// Returns the 0-indexed atom numbers where
        /// the given unit can connect to other units.
        /// </summary>
        /// <returns>
        /// int array of 0-indexed connecting atoms 
        /// </returns>
        public int[] getConnectingAtoms(int fragment_id = 0) {
            if (fragment_id!=0)
            {
                String cat = sdr.GetFieldValue("FRAGMENT_CONNECTIVITY");
                if (cat == null || cat == "") return new int[] { };
                return cat.Split('\n').Map(s => s.Replace("\r", "").Trim())
                                .Filter(s => s.Split(' ')[0] == fragment_id.ToString())
                                .Map(s=>s.Split(' ')[1])
                                .Map(s=>int.Parse(s))
                                .ToArray();
            }
            else
            {
                String cat = sdr.GetFieldValue("CONNECTING_ATOMS");
                if (cat == null) return new int[] { };
                return cat.Split('\n').Map(s => s.Replace("\r", "").Trim())
                               .Map(s => int.Parse(s))
                               .Cast<int>()
                               .OrderBy(item => item)
                               .ToArray();
            }
            
        }

        public int[] getConnectedFragmentIDs(int fragment_id)
        {
            
            String cat = sdr.GetFieldValue("FRAGMENT_CONNECTIVITY");
            if (cat == null || cat =="") return new int[] { };
            return cat.Split('\n').Map(s => s.Replace("\r", "").Trim())
                            .Filter(s => s.Split(' ')[0] == fragment_id.ToString())
                            .Map(s => s.Split(' ')[2])
                            .Map(s => int.Parse(s))
                            .ToArray();
        }

        public int[] geConnectivityConnectingFragment(int connected_fragment_id)
        {

            String cat = sdr.GetFieldValue("FRAGMENT_CONNECTIVITY");
            if (cat == null || cat == "") return new int[] { };
            string matching_connectivity_string = cat.Split('\n').Map(s => s.Replace("\r", "").Trim())
                            .Filter(s => s.Split(' ')[2] == connected_fragment_id.ToString()).ToArray()[0];

            return matching_connectivity_string.Split(' ').ToArray().Select(int.Parse).ToArray();
        }

        public int[] getAllConnectedFragmentIDs()
        {

            String cat = sdr.GetFieldValue("FRAGMENT_CONNECTIVITY");
            if (cat == null || cat == "") return new int[] { };
            return cat.Split('\n').Map(s => s.Replace("\r", "").Trim())
                            .Map(s => s.Split(' ')[2])
                            .Map(s => int.Parse(s))
                            .ToArray();
        }

        public int getConnectedFragmentID(int connecting_atom_index)
        {

            String cat = sdr.GetFieldValue("FRAGMENT_CONNECTIVITY");
            if (cat == null || cat =="") return 0;
            return cat.Split('\n').Map(s => s.Replace("\r", "").Trim())
                            .Filter(s => s.Split(' ')[1] == connecting_atom_index.ToString())
                            .Map(s => s.Split(' ')[2])
                            .Map(s => int.Parse(s))
                            .ToArray()[0];
        }

        public int[] getConnectingAtomIDs()
        {

            String cat = sdr.GetFieldValue("FRAGMENT_CONNECTIVITY");
            if (cat == null || cat =="") return new int[] { };
            return cat.Split('\n').Map(s => s.Replace("\r", "").Trim())
                            .Map(s => s.Split(' ')[1])
                            .Map(s => int.Parse(s))
                            .ToArray();
        }

        public int[] getConnectingAtomsHead(int fragment_id = 0)
        {
            /*
             * if (fragment_id != 0)
            {
                String cat = sdr.GetFieldValue("FRAGMENT_CONNECTIVITY");
                if (cat == null) return new int[] { };
                return cat.Split('\n').Map(s => s.Replace("\r", "").Trim())
                                .Filter(s => s.Split(' ')[0] == fragment_id.ToString())
                                .Map(s => s.Split(' ')[1])
                                .Map(s => int.Parse(s))
                                .ToArray();
            }
            else
            {*/
            String cat = sdr.GetFieldValue("CONNECTING_ATOMS_HEAD");
            if (cat == null) return new int[] { };
            return cat.Split('\n').Map(s => s.Replace("\r", "").Trim())
                            .Map(s => int.Parse(s))
                            .Cast<int>()
                            .ToArray();
            //}
        }
        public int[] getConnectingAtomsTail(int fragment_id = 0)
        {
            /*
             * if (fragment_id != 0)
            {
                String cat = sdr.GetFieldValue("FRAGMENT_CONNECTIVITY");
                if (cat == null) return new int[] { };
                return cat.Split('\n').Map(s => s.Replace("\r", "").Trim())
                                .Filter(s => s.Split(' ')[0] == fragment_id.ToString())
                                .Map(s => s.Split(' ')[1])
                                .Map(s => int.Parse(s))
                                .ToArray();
            }
            else
            {*/

            String cat = sdr.GetFieldValue("CONNECTING_ATOMS_TAIL");
            if (cat == null) return new int[] { };
            return cat.Split('\n').Map(s => s.Replace("\r", "").Trim())
                            .Map(s => int.Parse(s))
                            .Cast<int>()
                            .ToArray();
            //}
        }


        /// <summary>
        /// Returns the molfile of the unit
        /// </summary>
        /// <returns>
        /// String representation of the molfile.
        /// </returns>
        public String getMol() {
            return sdr.Mol;
        }

        public SDFUtil.IMolecule getMolecule() {
            return sdr.Molecule;
        }

        /// <summary>
        /// Returns the fragment type of the PolymerUnit. The most common returned strings are the following:
        /// <para>"linear sru" -- A repeating unit which is linear</para>
        /// <para>"head end group" -- A non-repeating unit that connects to the "head"</para>
        /// <para>"tail end group" -- A non-repeating unit that connects to the "tail"</para>
        /// <para>"disconnected" -- A disconnected fragment</para>
        /// </summary>
        /// <returns>
        /// string representing fragment type
        /// </returns>
        public String getFragmentType() {
            return sdr.GetFieldValue("FRAGMENT_TYPE");
        }

   
        public SdfRecord getSdfRecord()
        {
            return sdr;
        }
        public void setSdfRecord(SdfRecord new_sdf_record)
        {
            sdr = new_sdf_record;
        }
        /// <summary>
        /// Returns the polymer label of the PolymerUnit. The is a sequential index starting from 1 that is assigned to disjointed polymer chains
        /// </summary>
        /// <returns>
        /// string representing numeric chain index
        /// </returns>
        public String getPolymerLabel()
        {
            return sdr.GetFieldValue("POLYMER_LABEL");
        }

        public String getConnectivity()
        {
            return sdr.GetFieldValue("FRAGMENT_CONNECTIVITY");
        }

        /// <summary>
        /// Returns the set of string labels (if any) that were specified for the unit. Will return an empty list if there are none.
        /// </summary>
        /// <returns>
        /// List of strings, typically single letters
        /// </returns>
        public List<String> getLabels() {
            String lab= sdr.GetFieldValue("SRU_LABELS");
            if (lab == null) return new List<String>();
            return new List<String>(lab.Split('\n').Map(s => s.Replace("\r", "").Trim()));
        }

        public String getError()
        {
            return (sdr.GetFieldValue("ERROR") ?? "");
        }
        /// <summary>
        /// Returns the 0-indexed Map of atoms which appear
        /// in this molecule to their corresponding canonical
        /// index. For example, getCanonicalAtoms()[2] will return
        /// the 0-indexed canonical rank of atom 3. 
        /// 
        /// It's important to realize that the atoms in the molecule
        /// are not already canonicalized, so this method must be used
        /// for proper mapping to canonical numbers.
        /// </summary>
        /// <returns>
        /// int map from given atoms to canonical atoms. Returns null if there are no
        /// atoms to return.
        /// </returns>
        public int[] getCanonicalAtoms() {
            String lab = sdr.GetFieldValue("INPUT_TO_CANONICAL_ANUMS");
            if (lab == null) return null;
            return lab.Split('\n').Map(s => s.Replace("\r", "").Trim())
                                  .Map(s=>s.Split(' '))
                                  .Map(t=>Tuple.Create(int.Parse(t[0])-1, int.Parse(t[1])-1))
                                  .OrderBy(t=>t.Item1)
                                  .Map(t=>t.Item2)
                                  .ToArray();
        }

    }

	public class PolymerParser
	{
        private String Options = "";

        private String srs2MolPath=null;

        public PolymerParser(String path, String options="") {
            this.srs2MolPath = path;
            this.Options = options;
        }

        private static PolymerParser _instance = null;

        public static PolymerParser instance(String options = "") {
            //YP comment this out as this doesn't allow specifying options per substance but rather for the entire run
            //need to be able to specify options per substance (e.g. --branched)
            //if (_instance != null) return _instance;

            //YP use the relative path from debug folder in debug mode as the appsettings directive doesn't give a findable path
            //YP as of 09/20/2020 use of relative path shouldn't be needed 
            //as polymer.exe is expected to be in the srsutil/Resources directory 
            //and automatically copied to current directory during build
            String s =ConfigurationManager.AppSettings["srs2mol.polymer.path"];
            //String s = ("..\\..\\..\\..\\Data\\srs2mol\\polymer.exe");
            _instance = new PolymerParser(s, options);
            return _instance;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="mol"></param>
        /// <returns></returns>
        public SdfReader decomposeToReader(String mol) {
            String[] lines = mol.Split('\n');

            int atomCount = int.Parse(lines[3].Substring(0, 3).Trim());
            int bondCount = int.Parse(lines[3].Substring(3, 3).Trim());

            

            Boolean possiblyShifted = true;
            Boolean mustBeShifted = false;

            Dictionary<String, HashSet<int>> crossBonds = new Dictionary<string, HashSet<int>>();
            Dictionary<String, HashSet<int>> sgAtoms = new Dictionary<string, HashSet<int>>();
            HashSet<int>[] bonds= new HashSet<int>[bondCount+1];

            int c = 0;



            // This is a terrible fix for a terrible problem that JSDraw
            // has when producing molfiles.
            //
            // Here's the issue: It turns out that jsdraw shifts the cross
            // bonds for SRUs by the atom number. So, when looking at an SRU,
            // you will accidentally get an index for a bond that's far too high
            // (offset by the number of atoms). To detect this, we recognize a few
            // criteria:
            //
            // 1. A shift is POSSIBLE if all cross bond indexes are greater than the
            //    number of atoms. Otherwise it's not possible.
            // 2. A shift is REQUIRED if all cross bond indexes are greater than the
            //    number of bonds, because the cross bonds don't make sense as stated then.
            // 3. In a case where a shift is POSSIBLE, but not REQUIRED, we realize
            //    that any crossbond in an SRU must represent a bond that has exactly
            //    1 atom INSIDE the SRU group, and 1 atom OUTSIDE the SRU group. If we find
            //    that this is not the case, then we know the verbatim state is wrong,
            //    so we default to the shifted case. It's technically possible that a state
            //    here could be "satisfied" but still have a shift ... but that requires
            //    a very unlikely edge case.
            //
            // We generate both the verbatim molfile, and the shifted version, and 
            // choose which one to use based on the above criteria.
            //
            //
            String modMol=lines.Select(l => {
                c++;
                String line1 = l;
                if (c > 3 + atomCount + 1 && c< 3+atomCount+bondCount+1) {
                    int bindex = c - atomCount - 3-1;

                    bonds[bindex] = new int[]{
                            int.Parse(l.Substring(0,3).Trim()),
                            int.Parse(l.Substring(3,3).Trim()) }.ToHashSet();


                }
                if (l.Contains("M  SAL")) {
                    String sg = l.Substring(7, 3);
                    if (!sgAtoms.ContainsKey(sg.Trim())) {
                        sgAtoms[sg.Trim()] = new HashSet<int>();
                    }
                    Regex.Replace(l.Substring(13).Trim(), "[ ][ ]*", " ")
                                  .Split(' ')
                                  .ForEachWithIndex((s,i) => {
                                      sgAtoms[sg.Trim()].Add(int.Parse(s));
                                  });

                } else if (l.Contains("M  SBL")) {
                    String sg = l.Substring(7, 3);
                    if (!crossBonds.ContainsKey(sg.Trim())) {
                        crossBonds[sg.Trim()] = new HashSet<int>();
                    }
                    String n= l.Substring(0, 13) +
                        Regex.Replace(l.Substring(13).Trim(), "[ ][ ]*", " ")
                                  .Split(' ')
                                  .Filter(s => {
                                      crossBonds[sg.Trim()].Add(int.Parse(s));
                                      return true;
                                  })
                                  .Select(s => int.Parse(s))
                                  .Filter(s => {
                                      //You can't have a bond index beyond the number of 
                                      //bonds!
                                      if (s > bondCount) mustBeShifted = true;
                                      return true;
                                  })
                                  .Select(s => s - atomCount)
                                  .Filter(s => {
                                      if (s < 1) possiblyShifted = false;
                                      return true;
                                  })
                                  .Select(b => (b + "").PadLeft(4))
                                  .JoinToString("");
                    return n;
                }
                return l;
            }).JoinToString("\n");

            bool verbatimBondsOkay = true;
            //If it's possible that the bonds are shifted by the atom count,
            //look deeper to see if it's consistent
            if (!mustBeShifted && possiblyShifted) {
                try {
                    //look at each cross bond and see if exactly 1 
                    //of the atoms found there are in the sgroup
                    verbatimBondsOkay = crossBonds.All((k) => {
                        String sg = k.Key;
                        HashSet<int> inside = sgAtoms[sg];
                        HashSet<int> v = k.Value;

                        bool correct = v.Select(i => bonds[i].Filter(ai => inside.Contains(ai)).Count())
                         .All(num => {
                             return num == 1;
                         });
                        return correct;
                    });
                } catch (Exception ex) {
                    verbatimBondsOkay = false;
                }
            }



            if (possiblyShifted) {
                if (mustBeShifted) {
                    return extractParts(modMol, srs2MolPath);
                } else {
                    if (verbatimBondsOkay) {
                        return extractParts(mol, srs2MolPath);
                    } else {
                        return extractParts(modMol, srs2MolPath);
                    }
                }
            } else {
                //Nothing satisifies this problem
                if (mustBeShifted) {
                    throw new Exception("The supplied cross bonds for the SRUs do not exist, and can't be corrected");
                }
                //Not possible to be shifted, so just trust verbatim
                return extractParts(mol, srs2MolPath);
            }
        }

        public IEnumerable<PolymerUnit> decompose(String mol) {
            List<PolymerUnit> units = new List<PolymerUnit>();
            using(SdfReader red = this.decomposeToReader(mol)) {
                foreach (SdfRecord sdf in red.Records) {
                    units.Add(new PolymerUnit(sdf));
                }
            }
            return units;
        }

        private static void LaunchCommandLineApp(String path, String args) {
            //* Create your Process
            Process process = new Process();
            process.StartInfo.FileName = path;
            process.StartInfo.Arguments = args;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            //process.StartInfo.ErrorDialog = false;
            //process.StartInfo.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
            //* Set your output and error (asynchronous) handlers
            process.OutputDataReceived += new DataReceivedEventHandler(OutputHandler);
            process.ErrorDataReceived += new DataReceivedEventHandler(OutputHandler);
            //* Start process and handlers
            using (new ErrorModeContext(ErrorModes.FailCriticalErrors | ErrorModes.NoGpFaultErrorBox))
            {
                // start child process
                // and wait for it to finish
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                process.WaitForExit();
            }

           
        }
        //YP this is needed to suppress the "polymer.exe has stopped working" dialogue box
        public class ErrorModeContext : IDisposable
        {
            private readonly int _oldMode;

            public ErrorModeContext(ErrorModes mode)
            {
                _oldMode = SetErrorMode((int)mode);
            }

            ~ErrorModeContext()
            {
                Dispose(false);
            }

            private void Dispose(bool disposing)
            {
                SetErrorMode(_oldMode);
            }

            public void Dispose()
            {
                Dispose(true);
                GC.SuppressFinalize(this);
            }

            [DllImport("kernel32.dll")]
            private static extern int SetErrorMode(int newMode);
        }

        [Flags]
        public enum ErrorModes
        {
            Default = 0x0,
            FailCriticalErrors = 0x1,
            NoGpFaultErrorBox = 0x2, //this is the line that suppresses the error dialog box
            NoAlignmentFaultExcept = 0x4,
            NoOpenFileErrorBox = 0x8000,
        }
        //YP End of dialog box supression code

        private static void OutputHandler(object sendingProcess, DataReceivedEventArgs outLine) {
            //* Do your stuff with the output (write to console/log/StringBuilder)
            Console.WriteLine(outLine.Data);
        }

        private SdfReader extractParts(String mol, String path1) {
            //save temp file
            String fname=Guid.NewGuid().ToString();
            string myTempFile = Path.Combine(Path.GetTempPath(), fname +".mol");
            string myOutFile = Path.Combine(Path.GetTempPath(), fname +".out.sdf");

            using (StreamWriter sw = new StreamWriter(myTempFile)) {
                sw.Write(mol);
            }
            LaunchCommandLineApp(path1, "--connection "+  this.Options + " " + myTempFile + " " + myOutFile );
            return new SdfReader(myOutFile, Encoding.UTF8);
        }
    }
}
