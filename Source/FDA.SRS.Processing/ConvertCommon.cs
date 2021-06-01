using FDA.SRS.Database;
using FDA.SRS.ObjectModel;
using FDA.SRS.Utils;
using Serilog;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using System.Xml;

namespace FDA.SRS.Processing
{
    public static partial class Converter
    {
        public const string ERR_DIR = "err";
        public const string ERRORS_DIR = "errors";
        public const string OUT_DIR = "out";
        public const string OTHER_DIR = "other";
        public const string DIFF_DIR = "diff";

        public enum OutputFileType
        {
            /// <summary>
            /// GUID-named SPL
            /// </summary>
            Spl,

            /// <summary>
            /// UNII-named SPL
            /// </summary>
            UniiSpl,

            /// <summary>
            /// SDF
            /// </summary>
            Sdf,

            /// <summary>
            /// SRS XML
            /// </summary>
            SrsXml,

            /// <summary>
            /// MOL
            /// </summary>
            Mol,

            /// <summary>
            /// MOLs combined into SDF
            /// </summary>
            MolsSdf,

            /// <summary>
            /// Erroneous SPL named by GUID and written in respective hisrarchical directory
            /// </summary>
            ErrSpl,

            /// <summary>
            /// Erroneous UNII-named SPL
            /// </summary>
            ErrUniiSpl,

            /// <summary>
            /// Erroneous SDF
            /// </summary>
            ErrSdf,

            /// <summary>
            /// Erroneous SRS XML
            /// </summary>
            ErrSrsXml,

            /// <summary>
            /// Improvised XML with detailed info about error
            /// </summary>
            ErrXml,
        };

        private const string NEW_UNII = "_NEW_UNII_";

        private static bool isRecordOfInterest(ImportOptions impOpt, ConvertOptions opt, OperationalParameters pars, string unii)
        {
            return
                (impOpt.Uniis == null || impOpt.Uniis.Count() == 0 || impOpt.Uniis.Contains(unii))
                &&
                (opt.GenerateMode != GenerateMode.NewUnii || !ReferenceDatabases.Indexes.Exists(unii));
        }

        /// <summary>
        /// Do common preparational tasks
        /// </summary>
        public static OperationalParameters PrepareConversion(this ImportOptions impOpt, ExportOptions expOpt)
        {
            OperationalParameters pars = new OperationalParameters();
           
                // Output file - if *.sqlite then it's SQLite database
                if (!String.IsNullOrEmpty(expOpt.OutputFile) && Path.GetExtension(expOpt.OutputFile) == ".sqlite")
            {
                pars.SplsDb = new SplsDatabase(expOpt.OutputFile);
                if (!String.IsNullOrEmpty(impOpt.Name))
                    pars.SetId = pars.SplsDb.AddSet(impOpt.Name);
            }
            else if (String.IsNullOrEmpty(expOpt.OutDir)) // Otherwise put a default out dir there
                expOpt.OutDir = "out";

            // Output directory
            if (!String.IsNullOrEmpty(expOpt.OutDir))
            {
                if (Directory.Exists(expOpt.OutDir) && expOpt.Clean)
                    Directory.Delete(expOpt.OutDir, true);

                if (!Directory.Exists(expOpt.OutDir))
                    Directory.CreateDirectory(expOpt.OutDir);

                if (!Directory.Exists(Path.Combine(expOpt.OutDir, OUT_DIR)))
                    Directory.CreateDirectory(Path.Combine(expOpt.OutDir, OUT_DIR));

                if (!Directory.Exists(Path.Combine(expOpt.OutDir, ERR_DIR)))
                    Directory.CreateDirectory(Path.Combine(expOpt.OutDir, ERR_DIR));

                if (!Directory.Exists(Path.Combine(expOpt.OutDir, ERRORS_DIR)))
                    Directory.CreateDirectory(Path.Combine(expOpt.OutDir, ERRORS_DIR));

                if (!Directory.Exists(Path.Combine(expOpt.OutDir, OTHER_DIR)))
                    Directory.CreateDirectory(Path.Combine(expOpt.OutDir, OTHER_DIR));
            }

            // Prepare CSV log file
            if (!String.IsNullOrEmpty(expOpt.LogPath))
            {
                foreach (TraceListener tl in Trace.Listeners)
                {
                    if (tl is DelimitedListTraceListener)
                        Trace.Listeners.Remove(tl);
                }

                DelimitedListTraceListener l = new DelimitedListTraceListener(expOpt.LogPath);
                l.Delimiter = ",";
                Trace.Listeners.Add(l);
            }

            // Figure out input
            
            if (Directory.Exists(impOpt.InputFile))
                pars.InputFiles = FileUtils.ListFiles(impOpt.InputFile, true, "\\.sdf$|\\.mol$");
            else if (File.Exists(impOpt.InputFile))
                pars.InputFiles = new List<string> { impOpt.InputFile };
            else            
                return null;  //Added for Ticket 390  
                //throw new FileNotFoundException(impOpt.InputFile);

            return pars;
        }

        private static XDocument diffSpl(XDocument xdoc, string unii, string sdf, ConvertOptions opt, ExportOptions expOpt, OperationalParameters pars)
        {
            XDocument xDiff = null;

            var xSpl = ReferenceDatabases.RefSplsDb.GetDocByUnii(unii);
            if (xSpl == null)
            {

                TraceUtils.WriteUNIITrace(TraceEventType.Warning, unii, null, "Cannot find reference UNII={0}", unii);
            }
            else
            {
                string diffDir = Path.Combine(expOpt.OutDir, DIFF_DIR);
                if (!Directory.Exists(diffDir))
                    Directory.CreateDirectory(diffDir);

                using (TempFile tOrig = new TempFile())
                using (TempFile tNew = new TempFile())
                {
                    File.WriteAllText(tOrig.FullPath, xSpl.Spl());
                    File.WriteAllText(tNew.FullPath, xdoc.ToString());

                    xDiff = CompareUtils.SplDiff(tNew.FullPath, tOrig.FullPath, opt.FullDiff);
                    if (xDiff != null)
                    {
                        if (!String.IsNullOrEmpty(diffDir))
                        {
                            xDiff.Save(Path.Combine(diffDir, unii + "-diff.xml"));
                            File.Copy(tNew.FullPath, Path.Combine(diffDir, unii + ".xml"));
                            File.Copy(tOrig.FullPath, Path.Combine(diffDir, unii + "-orig.xml"));
                            File.WriteAllText(Path.Combine(diffDir, unii + ".sdf"), sdf);
                        }
                        TraceUtils.WriteUNIITrace(TraceEventType.Warning, unii, null, "Different SPL for UNII={0}", unii);
                    }
                }
            }

            return xDiff;
        }

        private static void writeResultFiles(ConvertOptions opt, ExportOptions expOpt, OperationalParameters pars, IEnumerable<SdfRecord> sdf, SplDocument splDoc, List<Tuple<OutputFileType, string>> result)
        {
            XDocument xSpl = splDoc.GetXml(opt.Validate); // Materialize the whole SPL XML here
            Guid hash = xSpl.SplHash();

            string unii = sdf.First()?.GetFieldValue("MIX_UNII");

            if (unii == null)
            {
                unii = sdf.First()?.GetFieldValue("UNII");
            }

            if (opt.GenerateMode != GenerateMode.NewHash || !ReferenceDatabases.Indexes.Exists(unii, hash))
            {
                if (unii == NEW_UNII && ReferenceDatabases.Indexes.Exists(hash))
                    unii = ReferenceDatabases.Indexes.GetExisting(hash).UNII;

                if (!expOpt.NoDocIdFile)
                {
                    string dir = Path.Combine(expOpt.OutDir, OUT_DIR, splDoc.DocId.ToString(), "a");
                    if (!Directory.Exists(dir))
                        Directory.CreateDirectory(dir);

                    string file = Path.Combine(dir, splDoc.DocId + ".xml");
                    xSpl.Save(file);
                    lock (result)
                        result.Add(Tuple.Create(OutputFileType.Spl, file));
                }

                if (expOpt.UNIIFile || expOpt.NoDocIdFile)
                {
                    string file = Path.Combine(expOpt.OutDir, OTHER_DIR, unii + ".xml");
                    xSpl.Save(file);
                    lock (result)
                        result.Add(Tuple.Create(OutputFileType.UniiSpl, file));
                }

                if (expOpt.SdfFile)
                {
                    string file = Path.Combine(expOpt.OutDir, OTHER_DIR, unii + ".sdf");
                    File.WriteAllText(file, sdf.ToString());
                    lock (result)
                        result.Add(Tuple.Create(OutputFileType.Sdf, file));
                }

                // Extract all MOLs from SPL
                if (expOpt.MolFile)
                {
                    List<string> mols = splDoc.GetXml(false).SplMols().ToList();
                    for (int i = 0; i < mols.Count(); i++)
                    {
                        string file = Path.Combine(expOpt.OutDir, OTHER_DIR, String.Format("{0}-{1}.mol", unii, i));
                        File.WriteAllText(file, mols[i]);
                        lock (result)
                            result.Add(Tuple.Create(OutputFileType.Mol, file));
                    }

                    if (expOpt.MolsAsSdf)
                    {
                        string file = Path.Combine(expOpt.OutDir, OTHER_DIR, String.Format("{0}.mol.sdf", unii));
                        using (StreamWriter w = new StreamWriter(file))
                        {
                            for (int i = 0; i < mols.Count(); i++)
                            {
                                SdfRecord rec = new SdfRecord(mols[i], null);
                                w.Write(rec.ToString());
                            }
                        }
                        lock (result)
                            result.Add(Tuple.Create(OutputFileType.MolsSdf, file));
                    }
                }

                string descr = sdf.First()?.GetConcatXmlFields("DESC_PART");
                if (!String.IsNullOrWhiteSpace(descr) && expOpt.SrsFile)
                {
                    string file = Path.Combine(expOpt.OutDir, OTHER_DIR, unii + ".srs.xml");
                    File.WriteAllText(file, descr);
                    lock (result)
                        result.Add(Tuple.Create(OutputFileType.SrsXml, file));
                }

                // Compare with reference if any
                XDocument xDiff = null;
                if (ReferenceDatabases.RefSplsDb != null)
                    xDiff = diffSpl(xSpl, unii, sdf.ToString(), opt, expOpt, pars);

                // Save in SQLite database if any
                if (pars.SplsDb != null)
                    pars.SplsDb.AddDoc(pars.SetId, unii, xSpl, xDiff, expOpt.SdfFile ? sdf.ToString() : null, expOpt.SrsFile ? descr : null, null);
            }
        }



        private static void writeErrorFiles(OperationalParameters pars, ExportOptions expOpt, IEnumerable<SdfRecord> sdf, SplDocument splDoc, Exception ex, List<Tuple<OutputFileType, string>> result)
        {

            string unii = sdf.First()?.GetFieldValue("UNII");

            Log.Logger.Error("Processing record (UNII={0})...", unii);

            // Add fake GUID as it's fake SPL anyway
            if (splDoc.SetId == null)
                splDoc.SetId = Guid.NewGuid();

            if (!expOpt.NoSplErr)
            {
                if (ex is SrsException)
                    splDoc.AddError((ex as SrsException).Category, ex.Message);
                else
                    splDoc.AddError("general_error", ex.Message);

                // Materialize the whole SPL XML here without validation as SPL errors won't validate against schema
                var xml = splDoc.GetXml(false);

                // Write GUID-named SPL with error message(s)
                if (!expOpt.NoDocIdFile)
                {
                    string errDir = Path.Combine(expOpt.OutDir, ERR_DIR, splDoc.DocId.ToString(), "a");
                    if (!Directory.Exists(errDir))
                        Directory.CreateDirectory(errDir);
                    string file = Path.Combine(errDir, splDoc.DocId + ".xml");
                    xml.Save(file);
                    lock (result)
                        result.Add(Tuple.Create(OutputFileType.ErrSpl, file));
                }

                // Write UNII-named SPL with error message(s)
                if (expOpt.UNIIFile || expOpt.NoDocIdFile)
                {
                    string file = Path.Combine(expOpt.OutDir, ERR_DIR, unii + ".xml");
                    xml.Save(file);
                    lock (result)
                        result.Add(Tuple.Create(OutputFileType.ErrUniiSpl, file));
                }
            }

            // Write various error-related files into errors subdirectory
            string dir = Path.Combine(expOpt.OutDir, ERRORS_DIR, ex.GetType().Name);

            // Additional directories structure for SRSExceptions
            if (ex is SrsException)
                dir = Path.Combine(dir, (ex as SrsException).Category);

            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            if (expOpt.SdfFile)
            {
                string file = Path.Combine(dir, unii + ".sdf");
                File.WriteAllText(file, sdf.ToString());
                lock (result)
                    result.Add(Tuple.Create(OutputFileType.ErrSdf, file));
            }

            string descr = sdf.First()?.GetConcatXmlFields("DESC_PART");
            if (!String.IsNullOrWhiteSpace(descr) && expOpt.SrsFile)
            {
                string file = Path.Combine(dir, unii + ".srs.xml");
                File.WriteAllText(file, descr);
                lock (result)
                    result.Add(Tuple.Create(OutputFileType.ErrSrsXml, file));
            }

            XDocument xErr = new XDocument(new XElement("root",
                    new XElement("exception", new XCData(ex.ToString())),
                    new XElement("srs-xml", new XCData(descr))));

            if (!expOpt.NoErrFile)
            {
                string file = Path.Combine(dir, unii + ".err.xml");
                xErr.Save(file);
                lock (result)
                    result.Add(Tuple.Create(OutputFileType.ErrXml, file));
            }

            if (pars.SplsDb != null)
                pars.SplsDb.AddDoc(pars.SetId, unii, null, null, sdf?.ToString(), descr, xErr.ToString());
        }

        private static void writeSPL2SPLErrorFiles(OperationalParameters pars, ExportOptions expOpt, string unii, System.Xml.XmlDocument inSPL, SplDocument splDoc, Exception ex, List<Tuple<OutputFileType, string>> result)
        {
            //YP writeSPL2SPLErrorFiles, based on the writeErrorFiles but with some modifications to make it suitable for SPL2SPL conversion

            //YP SD file is not used in spl2spl, passing unii in as an argument
            //string unii = sdf.First()?.GetFieldValue("UNII");

            Log.Logger.Error("Processing record (UNII={0})...", unii);

            // Add fake GUID as it's fake SPL anyway
            if (splDoc.SetId == null)
                splDoc.SetId = Guid.NewGuid();

            if (!expOpt.NoSplErr)
            {
                if (ex is SrsException)
                    splDoc.AddError((ex as SrsException).Category, ex.Message);
                else
                    splDoc.AddError("general_error", ex.Message);

                // Materialize the whole SPL XML here without validation as SPL errors won't validate against schema
                var xml = splDoc.GetXml(false);

                // Write GUID-named SPL with error message(s)
                if (!expOpt.NoDocIdFile)
                {
                    string errDir = Path.Combine(expOpt.OutDir, ERR_DIR, splDoc.DocId.ToString(), "a");
                    if (!Directory.Exists(errDir))
                        Directory.CreateDirectory(errDir);
                    string file = Path.Combine(errDir, splDoc.DocId + ".xml");
                    xml.Save(file);
                    lock (result)
                        result.Add(Tuple.Create(OutputFileType.ErrSpl, file));
                }

                // Write UNII-named SPL with error message(s)
                if (expOpt.UNIIFile || expOpt.NoDocIdFile)
                {
                    string file = Path.Combine(expOpt.OutDir, ERR_DIR, unii + ".xml");
                    xml.Save(file);
                    lock (result)
                        result.Add(Tuple.Create(OutputFileType.ErrUniiSpl, file));
                }
            }

            // Write various error-related files into errors subdirectory
            string dir = Path.Combine(expOpt.OutDir, ERRORS_DIR, ex.GetType().Name);

            // Additional directories structure for SRSExceptions
            if (ex is SrsException)
                dir = Path.Combine(dir, (ex as SrsException).Category);

            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            /* YP SD files is not used in spl2spl
            if (expOpt.SdfFile)
            {
                string file = Path.Combine(dir, unii + ".sdf");
                File.WriteAllText(file, sdf.ToString());
                lock (result)
                    result.Add(Tuple.Create(OutputFileType.ErrSdf, file));
            }
            */

            /* YP Again, no SD file and no descr used in spl2spl
            string descr = sdf.First()?.GetConcatXmlFields("DESC_PART");
            if (!String.IsNullOrWhiteSpace(descr) && expOpt.SrsFile)
            {
                string file = Path.Combine(dir, unii + ".srs.xml");
                File.WriteAllText(file, descr);
                lock (result)
                    result.Add(Tuple.Create(OutputFileType.ErrSrsXml, file));
            }
            */

            XDocument xErr = new XDocument(new XElement("root",
                    new XElement("exception", new XCData(ex.ToString())),
                    new XElement("srs-xml", new XCData(inSPL.OuterXml))));

            if (!expOpt.NoErrFile)
            {
                string file = Path.Combine(dir, unii + ".err.xml");
                xErr.Save(file);
                lock (result)
                    result.Add(Tuple.Create(OutputFileType.ErrXml, file));
            }

            //YP passing nulls in place of sdf and descr since they are not available in spl to spl conversion
            if (pars.SplsDb != null)
                pars.SplsDb.AddDoc(pars.SetId, unii, null, null, null, null, xErr.ToString());
        }

        //Added for Ticket 390                   
        private static void writeError_FileNotFound(OperationalParameters pars, ExportOptions expOpt, IEnumerable<SdfRecord> sdf, SplDocument splDoc1, Exception ex, List<Tuple<OutputFileType, string>> result)
        {

            string unii = "Filename"; 
            
            // Write various error-related files into errors subdirectory
            string dir = Path.Combine(expOpt.OutDir, ERRORS_DIR, ex.GetType().Name);

            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            if (expOpt.SdfFile)
            {
                string file = Path.Combine(dir, unii + ".sdf");
                File.WriteAllText(file, sdf.ToString());
                lock (result)
                    result.Add(Tuple.Create(OutputFileType.ErrSdf, file));
            }

            string descr = "File Not Found";
            
            XDocument xErr = new XDocument(new XElement("root",
                    new XElement("exception", new XCData(ex.ToString())),
                    new XElement("srs-xml", new XCData(descr))));

            if (!expOpt.NoErrFile)
            {
                string file = Path.Combine(dir, unii + ".err.xml");
                xErr.Save(file);
                lock (result)
                    result.Add(Tuple.Create(OutputFileType.ErrXml, file));
            }
        }



    }

}
