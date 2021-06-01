using CsvHelper;
using FDA.SRS.Database;
using FDA.SRS.Processing.Properties;
using FDA.SRS.Utils;
using ICSharpCode.SharpZipLib.Zip;
using Serilog;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.OleDb;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace FDA.SRS.Processing
{
	public static partial class TaxonomyUtils
	{

		#region NCBI

		public static void Ncbi2Terms(ImportOptions impOpt, ExportOptions expOpt, TaxonomyOptions taxonomyOptions)
		{
			if ( String.IsNullOrEmpty(expOpt.OutputFile) )
				expOpt.OutputFile = Settings.Default.ncbi_taxonomy_filename;

			if ( String.IsNullOrEmpty(impOpt.InputFile) )
				impOpt.InputFile = PrepareNcbiTaxonomyFile();

			using ( StreamReader r = new StreamReader(impOpt.InputFile) )
			using ( StreamWriter w = new StreamWriter(expOpt.OutputFile) ) {
				string line = r.ReadLine();
				while ( line != null ) {
					var parts = Regex.Split(line, @"\s*\|\s*");
					if ( String.Equals(parts[3], "authority", StringComparison.OrdinalIgnoreCase) )
						w.WriteLine(parts[1].PrepareTaxonomyReference(taxonomyOptions));
					line = r.ReadLine();
				}
			}
		}

		public static string PrepareNcbiTaxonomyFile()
		{
			var downloadUrl = ConfigurationManager.AppSettings["ncbi.download_url"] ?? Settings.Default.ncbi_download_url;
			string localZip = DownloadUtils.Download(new Uri(downloadUrl), ConfigurationManager.AppSettings["ncbi.cached_file"] ?? Settings.Default.ncbi_cached_file,true);

			Console.Out.Write("Extracting... ");

			string ncbiNamesFile = Settings.Default.ncbi_names_file;
			using ( var zip = new ZipFile(localZip) ) {
				var fileIndex = zip.FindEntry(ncbiNamesFile, true);
				if ( fileIndex < 0 )
					throw new FileNotFoundException($"{ncbiNamesFile} not found in NCBI taxonomy archive {downloadUrl}");

				var localNcbiNamesFile = Path.Combine(Path.GetTempPath(), ncbiNamesFile);
				using ( var sr = new StreamReader(zip.GetInputStream(fileIndex)) )
				using ( var sw = new StreamWriter(localNcbiNamesFile) ) {
					sw.Write(sr.ReadToEnd());
				}

				Console.Out.WriteLine(" done.");
				return localNcbiNamesFile;
			}
		}

		#endregion

		#region ITIS

		public static void Itis2Terms(ImportOptions impOpt, ExportOptions expOpt, TaxonomyOptions taxonomyOptions)
		{
			if ( String.IsNullOrEmpty(expOpt.OutputFile) )
				expOpt.OutputFile = Settings.Default.itis_taxonomy_filename;

			if ( String.IsNullOrEmpty(impOpt.InputFile) )
				impOpt.InputFile = PrepareItisTaxonomySqliteFile();
			
			using ( ItisDatabase db = new ItisDatabase(impOpt.InputFile, impOpt.InputFileEncoding?.HeaderName) )
			using ( StreamWriter w = new StreamWriter(expOpt.OutputFile) ) {
				foreach ( var r in db.References ) {
					w.WriteLine(r.PrepareTaxonomyReference(taxonomyOptions));
				}
			}
		}

		public static string PrepareItisTaxonomySqliteFile()
		{
			var downloadUrl = ConfigurationManager.AppSettings["itis.download_url"] ?? Settings.Default.itis_download_url;
			string localZip = DownloadUtils.Download(new Uri(downloadUrl), ConfigurationManager.AppSettings["itis.cached_file"] ?? Settings.Default.itis_cached_file,true);

			Console.Out.Write("Extracting... ");

			var itisDatabaseFile = Settings.Default.itis_database_file;
			using ( var zip = new ZipFile(localZip) ) {
				ZipEntry foundEnt = null;
				foreach ( ZipEntry ze in zip ) {
					if ( ze.IsFile && String.Equals(Path.GetFileName(ze.Name), itisDatabaseFile, StringComparison.InvariantCultureIgnoreCase) ) {
						foundEnt = ze;
						break;
					}
				}
				if ( foundEnt == null )
					throw new FileNotFoundException($"{itisDatabaseFile} not found in ITIS taxonomy archive");

				var localItisDatabaseFile = Path.Combine(Path.GetTempPath(), itisDatabaseFile);
				using ( var ins = zip.GetInputStream(foundEnt) )
				using ( var sw = new StreamWriter(localItisDatabaseFile) ) {
					ins.CopyTo(sw.BaseStream);
				}

				Console.Out.WriteLine("done.");
				return localItisDatabaseFile;
			}
		}

		#endregion

		#region USDB

		public static void Usda2Terms(ImportOptions impOpt, ExportOptions expOpt, TaxonomyOptions taxonomyOptions)
		{
			if ( String.IsNullOrEmpty(expOpt.OutputFile) )
				expOpt.OutputFile = Settings.Default.usda_taxonomy_filename;

			if ( String.IsNullOrEmpty(impOpt.InputFile) )
				impOpt.InputFile = PrepareUsdaTaxonomyFile();

			using ( StreamReader r = new StreamReader(impOpt.InputFile) )
			using ( var csv = new CsvReader(r) )
			using ( StreamWriter w = new StreamWriter(expOpt.OutputFile) ) {
				while ( csv.Read() ) {
					StringBuilder term = new StringBuilder();
					term.AppendFormat("{0} {1}", csv.GetField<string>("TAXON"), csv.GetField<string>("TAXAUTHOR"));
					if ( !taxonomyOptions.SimplifiedAuthorityReference ) {
						int? year = csv.GetField<string>("PROTOLOGUE").ExtractProtologueYear();
						if ( year != null )
							term.AppendFormat(", {0}", year);
					}
					w.WriteLine(term.ToString().PrepareTaxonomyReference(taxonomyOptions));
				}
			}
		}

		public static string PrepareUsdaTaxonomyFile()
		{
			var downloadUrl = ConfigurationManager.AppSettings["usda.download_url"] ?? Settings.Default.usda_download_url;
			var usdaCachedFile = ConfigurationManager.AppSettings["usda.cached_file"] ?? Settings.Default.usda_cached_file;
			string localZip = DownloadUtils.Download(new Uri(downloadUrl), usdaCachedFile,true);

			Console.Out.Write("Extracting... ");

			var usdaSpeciesFile = Settings.Default.usda_species_file;
			using ( var zip = new ZipFile(localZip) ) {
				var fileIndex = zip.FindEntry(usdaSpeciesFile, true);
				if ( fileIndex < 0 )
					throw new FileNotFoundException($"{usdaSpeciesFile} not found in NCBI taxonomy archive {downloadUrl}");

				var localUsdaSpeciesFile = Path.Combine(Path.GetTempPath(), usdaSpeciesFile);
				using ( var ins = zip.GetInputStream(fileIndex) )
				using ( var sw = new StreamWriter(localUsdaSpeciesFile) ) {
					ins.CopyTo(sw.BaseStream);
				}

				Console.Out.WriteLine("done.");
                String npath = Path.Combine(Path.GetDirectoryName(usdaCachedFile), "species.csv");

                DataTable dt=GetYourData(localUsdaSpeciesFile);
                writeCSV(dt, npath);
                
                return npath;
			}
		}


        public static void writeCSV(DataTable dt, String outfile) {
            StringBuilder sb = new StringBuilder();

            IEnumerable<string> columnNames = dt.Columns.Cast<DataColumn>().
                                              Select(column => column.ColumnName);
            sb.AppendLine(string.Join(",", columnNames));

            foreach (DataRow row in dt.Rows) {
                IEnumerable<string> fields = row.ItemArray.Select(field => {

                    String f= field.ToString();
                    if(!Regex.IsMatch(f,"^[a-zA-Z 0-9.()]*$")){
                        return "\"" + f.Replace("\"", "\"\"") + "\"";
                    }
                    return f;
                    });
                sb.AppendLine(string.Join(",", fields));
            }

            File.WriteAllText(outfile, sb.ToString());
        }

        public static DataTable GetYourData(String file) {
        
            DBFReader d = new DBFReader(file, Encoding.ASCII);

            return d.ReadToDataTable();
            /*
            DataTable YourResultSet = new DataTable();
            OleDbConnection yourConnectionHandler = new OleDbConnection(
                @"Provider=VFPOLEDB.1;Data Source=C:\Users\PC1\Documents\Visual FoxPro Projects\");

            // if including the full dbc (database container) reference, just tack that on
            //      OleDbConnection yourConnectionHandler = new OleDbConnection(
            //          "Provider=VFPOLEDB.1;Data Source=C:\\SomePath\\NameOfYour.dbc;" );


            // Open the connection, and if open successfully, you can try to query it
            yourConnectionHandler.Open();

            if (yourConnectionHandler.State == ConnectionState.Open) {
                string mySQL = "select * from CLIENTS";  // dbf table name

                OleDbCommand MyQuery = new OleDbCommand(mySQL, yourConnectionHandler);
                OleDbDataAdapter DA = new OleDbDataAdapter(MyQuery);

                DA.Fill(YourResultSet);

                yourConnectionHandler.Close();
            }

            return YourResultSet;
            */
        }

        #endregion

        #region Kew Gardens

        public static void Kew2Terms(ImportOptions impOpt, ExportOptions expOpt, TaxonomyOptions taxonomyOptions)
		{
			if ( String.IsNullOrEmpty(expOpt.OutputFile) )
				expOpt.OutputFile = Settings.Default.kew_taxonomy_filename;

			if ( String.IsNullOrEmpty(impOpt.InputFile) )
				impOpt.InputFile = PrepareKewTaxonomyFile();

			using ( StreamReader r = new StreamReader(impOpt.InputFile) )
			using ( var csv = new CsvReader(r) )
			using ( StreamWriter w = new StreamWriter(expOpt.OutputFile) ) {
				while ( csv.Read() ) {
					StringBuilder term = new StringBuilder();
					term.AppendFormat("{0} {1} {2}", csv.GetField<string>("genus"), csv.GetField<string>("species"), csv.GetField<string>("author"));
					w.WriteLine(term.ToString().PrepareTaxonomyReference(taxonomyOptions));
				}
			}
		}

		public static string PrepareKewTaxonomyFile()
		{
			return ConfigurationManager.AppSettings["kew.cached_file"] ?? Settings.Default.kew_cached_file;
		}

		#endregion

		public static void MergeTerms(ImportOptions impOpt, ExportOptions expOpt)
		{
			var terms = new HashSet<string>();
          //  Regex rx = new Regex(@"^[a-z][a-z]", RegexOptions.Compiled);
            foreach ( string file in impOpt.TermsFiles ) {
				using ( StreamReader r = new StreamReader(CmdLineUtils.ParsePathWithEncodingPrefix(file), CmdLineUtils.ParseEncodingPrefix(file)) ) {
					while ( true ) {
						string line = r.ReadLine();
						if ( line == null )
							break;
                        String term = line.Trim();
                        /*
                        if (rx.IsMatch(term)) {
                            String ending=term.Substring(1);
                            term = term.Substring(0, 1).ToUpper() + ending;
                        }
                        */
                        terms.Add(term);
					}
				}
			}

			using ( StreamWriter w = new StreamWriter(expOpt.OutputFile, false, expOpt.OutputFileEncoding) ) {
				terms
					.OrderBy(t => t)
                    .Distinct()
                    .ToList()
					.ForEach(t => {
						if ( !String.IsNullOrWhiteSpace(t) )
							w.WriteLine(t);
					});
			}
		}

		public static void UpdateTerms(ExportOptions expOpt, TaxonomyOptions taxonomyOptions)
		{
			ImportOptions impOpt = new ImportOptions { TermsFiles = new List<string>() };

            
            try {
                var ncbiOptions = new ExportOptions { };
                Ncbi2Terms(new ImportOptions(), ncbiOptions, taxonomyOptions);
                impOpt.TermsFiles.Add(ncbiOptions.OutputFile);
            }catch(Exception e) {
                if (taxonomyOptions.FailOnMissing) {
                    throw e;
                }
                Log.Logger.Warning("Taxonomy datasource: NCBI could not be processed", e);
            }
            
            
            try {
                var itisOptions = new ExportOptions { };
			    Itis2Terms(new ImportOptions(), itisOptions, taxonomyOptions);
			    impOpt.TermsFiles.Add(itisOptions.OutputFile);
            } catch (Exception e) {
                if (taxonomyOptions.FailOnMissing) {
                    throw e;
                }
                Log.Logger.Warning("Taxonomy datasource: ITIS could not be processed", e);
            }
            
            try {
                var usdaOptions = new ExportOptions { };
			    Usda2Terms(new ImportOptions(), usdaOptions, taxonomyOptions);
			    impOpt.TermsFiles.Add(usdaOptions.OutputFile);
            } catch (Exception e) {
                if (taxonomyOptions.FailOnMissing) {
                    throw e;
                }
                Log.Logger.Warning("Taxonomy datasource: USDA could not be processed", e);
            }
            
            try {
                var kewOptions = new ExportOptions { };
                Kew2Terms(new ImportOptions(), kewOptions, taxonomyOptions);
                impOpt.TermsFiles.Add(kewOptions.OutputFile);
            }catch(Exception e) {
                if (taxonomyOptions.FailOnMissing) {
                    throw e;
                }
                Log.Logger.Warning("Taxonomy datasource: kew could not be processed", e);
            }
			MergeTerms(impOpt, expOpt);
		}
	}
}
