using FDA.SRS.Utils;
using ICSharpCode.SharpZipLib.Zip;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Threading;
using System.Xml.Linq;

namespace FDA.SRS.Database
{
	public static class SplUtils
	{
		public static void UnzipDailymed(ImportOptions impOpt, ExportOptions expOpt)
		{
			GetDailyMedSpl(impOpt);

			if ( !Directory.Exists(expOpt.OutDir) )
				Directory.CreateDirectory(expOpt.OutDir);

			int counter = 0;
			IterateDailymedZip(impOpt.InputFile, (zip, ent) => {
				using ( var zis = zip.GetInputStream(ent) )
				using ( StreamWriter sw = new StreamWriter(Path.Combine(expOpt.OutDir, Path.GetFileName(ent.Name))) ) {
					zis.CopyTo(sw.BaseStream);
					sw.Flush();
				}

				Console.Out.Write(".");
				if ( ++counter % 1000 == 0 )
					Console.Out.Write(counter);
			});
		}

		private static void IterateDailymedZip(string file, Action<ZipFile, ZipEntry> onEntry)
		{
			using ( ZipFile zip = new ZipFile(file) ) {
				foreach ( ZipEntry ent in zip.Cast<ZipEntry>().Where(e => e.IsFile && e.Name.EndsWith(".zip", StringComparison.InvariantCultureIgnoreCase)) ) {
					using ( var ms = new MemoryStream((int)ent.Size) ) {
						zip.GetInputStream(ent).CopyTo(ms);
						ms.Flush();

						using ( ZipFile zip2 = new ZipFile(ms) ) {
							foreach ( ZipEntry ent2 in zip2 ) {
								onEntry(zip2, ent2);
							}
						}
					}
				}
			}
		}

		private static void IterateDailymedZip(string file, Action<XDocument> onEntry)
		{
			int counter = 0;
			IterateDailymedZip(file, (zip, ent) => {
				using ( var zis = zip.GetInputStream(ent) ) {
					onEntry(XDocument.Load(zis));
				}

				Console.Out.Write(".");
				if ( ++counter % 1000 == 0 )
					Console.Out.Write(counter);
			});
		}

		public static void IndexSpl(ImportOptions impOpt, ExportOptions expOpt)
		{
			GetDailyMedSpl(impOpt);

			string sqliteDb = null;
			if ( !String.IsNullOrEmpty(expOpt.OutputFile) )
				sqliteDb = expOpt.OutputFile;
			else if ( Directory.Exists(impOpt.InputFile) )
				sqliteDb = impOpt.InputFile.TrimEnd('\\') + ".sqlite";
			else if ( File.Exists(impOpt.InputFile) )
				sqliteDb = Path.Combine(Path.GetDirectoryName(impOpt.InputFile), Path.GetFileNameWithoutExtension(impOpt.InputFile) + ".sqlite");
			else
				throw new FileNotFoundException(impOpt.InputFile);

			using ( SplsDatabase db = new SplsDatabase(sqliteDb) ) {

				int setId = db.AddSet(impOpt.Name);

				int counter = 0;
				if ( Directory.Exists(impOpt.InputFile) ) {
					Directory.GetFiles(impOpt.InputFile, "*.xml", SearchOption.AllDirectories)
						.ToList()
						.ForEach(s => {
							XDocument xdoc = XDocument.Load(s);
							db.AddDoc(setId, null, xdoc, null, null, null, null);

							Console.Out.Write(".");
							if ( ++counter % 1000 == 0 )
								Console.Out.Write(counter);
						});
				}
				else {
					IterateDailymedZip(impOpt.InputFile, (xdoc) => {
						db.AddDoc(setId, null, xdoc, null, null, null, null);

						Console.Out.Write(".");
						if ( ++counter % 1000 == 0 )
							Console.Out.Write(counter);
					});
				}
			}
		}

		private static void GetDailyMedSpl(ImportOptions impOpt)
		{
			if ( String.IsNullOrEmpty(impOpt.InputFile) ) {
				string cachedFile = ConfigurationManager.AppSettings["dailymed.substances.cached_file"] ?? Path.Combine(Path.GetTempPath(), "substance_indexing_spl_files.zip");
				if ( File.Exists(cachedFile) )
					impOpt.InputFile = cachedFile;
				else {
					var downloadUrl = ConfigurationManager.AppSettings["dailymed.substances.download_url"];
					if ( !String.IsNullOrEmpty(downloadUrl) )
						impOpt.InputFile = DownloadUtils.Download(new Uri(downloadUrl), cachedFile,true);
					else
						throw new ArgumentException("Mandatory input file argument not supplied and no hint on how to download provided");
				}
			}
		}

		public static void ExportSpl(ImportOptions impOpt, ExportOptions expOpt)
		{
			using ( SplsDatabase db = new SplsDatabase(impOpt.InputFile) ) {

				int? setId = null;
				if ( !String.IsNullOrEmpty(impOpt.Name) )
					setId = db.GetSets().Select(id => db.GetSet(id)).Where(s => s.Name == impOpt.Name).First()?.Id;

				if ( !Directory.Exists(expOpt.OutDir) )
					Directory.CreateDirectory(expOpt.OutDir);

				int counter = 0;
				IEnumerable<int> docIds = db.GetDocs(setId).ToList();
				docIds
					.AsParallel()
					.ForAll(docId => {
						var doc = db.GetDoc(docId);
						if ( (expOpt.ExpType & ExportOptions.ExportType.All) == ExportOptions.ExportType.All ||
							 (expOpt.ExpType & ExportOptions.ExportType.Ok) == ExportOptions.ExportType.Ok && doc.Spl != null ||
							 (expOpt.ExpType & ExportOptions.ExportType.Err) == ExportOptions.ExportType.Err && doc.Err != null ||
							 (expOpt.ExpType & ExportOptions.ExportType.Diff) == ExportOptions.ExportType.Diff && doc.SplDiff != null ) {
							if ( doc.Spl != null )
								doc.SplXml().Save(Path.Combine(expOpt.OutDir, doc.SplXml().SplDocId().ToString() + ".xml"));
							if ( doc.SplDiff != null )
								doc.SplDiffXml().Save(Path.Combine(expOpt.OutDir, doc.SplXml().SplDocId().ToString() + "-diff.xml"));
							if ( doc.Sdf != null )
								File.WriteAllText(Path.Combine(expOpt.OutDir, doc.UNII + ".sdf"), doc.Sdf());
							if ( doc.Srs != null )
								File.WriteAllText(Path.Combine(expOpt.OutDir, doc.UNII + ".srs.xml"), doc.Srs());
							if ( doc.Err != null )
								File.WriteAllText(Path.Combine(expOpt.OutDir, doc.UNII + ".err.xml"), doc.Err());

							Console.Out.Write(".");
							Interlocked.Increment(ref counter);
							if ( counter % 1000 == 0 )
								Console.Out.Write(counter);
						}
					});
			}
		}
	}
}
