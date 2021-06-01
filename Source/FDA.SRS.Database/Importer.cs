using FDA.SRS.Database.Models;
using FDA.SRS.Utils;
using ICSharpCode.SharpZipLib.Zip;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;

namespace FDA.SRS.Database
{
	public static class Importer
	{
		public static void ImportDailymedSpl(ImportOptions options)
		{
			using ( SrsDbContext db = new SrsDbContext() ) {
#if DEBUG
				db.LogToConsole(LogLevel.Debug);
#endif

				SplSet splSet = new SplSet();
				splSet.Name = options.Name;
				splSet.StartTime = DateTime.Now;
				splSet.SplDocs = new List<SplDoc>();
				db.SplSets.Add(splSet);
				db.SaveChanges();

				HashSet<string> dbInChIKeys = new HashSet<string>(db.SplChemicals.Select(c => c.InChIKey));

				int counter = 0;
				using ( ZipFile zip = new ZipFile(options.InputFile) ) {
					string tmp = Path.GetTempFileName();
					try {
						foreach ( ZipEntry ent in zip.Cast<ZipEntry>().Where(e => e.IsFile && e.Name.EndsWith(".zip", StringComparison.InvariantCultureIgnoreCase)) ) {
							using ( StreamWriter sw = new StreamWriter(tmp) ) {
								zip.GetInputStream(ent).CopyTo(sw.BaseStream);
								sw.Flush();
							}
							using ( ZipFile zip2 = new ZipFile(tmp) ) {
								foreach ( ZipEntry ent2 in zip2 ) {
									MemoryStream ms = new MemoryStream((int)ent2.Size);
									zip2.GetInputStream(ent2).CopyTo(ms);
									string spl = Encoding.UTF8.GetString(ms.ToArray());

									// FIX: Trick to replace hardcoded UTF-8 with real .NET string UTF-16
									spl = spl.Replace("<?xml version=\"1.0\" encoding=\"UTF-8\"?>", "<?xml version=\"1.0\" encoding=\"UTF-16\"?>");

									XDocument xdoc = XDocument.Parse(spl);
									SplDoc splDoc = new SplDoc {
										DocId = xdoc.SplDocId(),
										Version = xdoc.SplVersion(),
										UNII = xdoc.SplUNII(),
										Spl = spl
									};
									
									splSet.SplDocs.Add(splDoc);
									db.SplDocs.Add(splDoc);
									db.SaveChanges();

									List<SplChemical> splChemicals = ExctractSplChemicals(xdoc).ToList();
									List<SplChemical> newChemicals = splChemicals.Where(c => !dbInChIKeys.Contains(c.InChIKey)).Distinct(new SplChemicalComparer()).ToList();
									db.SplChemicals.AddRange(newChemicals);
									db.SaveChanges();

									newChemicals.ForEach(c => dbInChIKeys.Add(c.InChIKey));

									splChemicals.ForEach(c => {
										if ( c.Id == 0 )
											c.Id = db.SplChemicals.Where(cc => c.InChIKey == cc.InChIKey).Select(cc => cc.Id).Single();
									});

									var dc = splChemicals.Select(c => new SplDocChemical { Doc = splDoc, Chemical = c }).ToList();
									db.SplDocChemicals.AddRange(dc);
									db.SaveChanges();
								}
								Console.Out.Write(".");
								if ( ++counter % 1000 == 0 )
									Console.Out.Write(counter);
							}
						}
					}
					finally {
						if ( File.Exists(tmp) )
							File.Delete(tmp);
					}
				}

				splSet.FinishTime = DateTime.Now;
				db.SaveChanges();
			}
		}

		private static IEnumerable<SplChemical> ExctractSplChemicals(XDocument xdoc)
		{
			var nsm = new XmlNamespaceManager(new NameTable());
			nsm.AddNamespace("s", "urn:hl7-org:v3");
			var ms = xdoc.XPathSelectElements("//s:moiety[s:subjectOf/s:characteristic/s:value]", nsm);
			foreach ( var m in ms ) {
				var mol = m.XPathSelectElement("s:subjectOf/s:characteristic/s:value[@mediaType='application/x-mdl-molfile']", nsm)?.Value;
				var inchi = m.XPathSelectElement("s:subjectOf/s:characteristic/s:value[@mediaType='application/x-inchi']", nsm)?.Value;
				var inchikey = m.XPathSelectElement("s:subjectOf/s:characteristic/s:value[@mediaType='application/x-inchi-key']", nsm)?.Value;
				yield return new SplChemical { Mol = mol, InChI = inchi, InChIKey = inchikey };
			}
		}

		public static void import_xml(string dir, string table)
		{
			using ( SqlConnection conn = new SqlConnection(ConfigurationManager.ConnectionStrings["SRSConnectionString"].ConnectionString) ) {
				conn.Open();
				using ( SqlCommand cmd = new SqlCommand(String.Format("insert into {0} values (@spl_xml)", table), conn) ) {
					cmd.Parameters.Add("spl_xml", System.Data.SqlDbType.Xml);
					foreach ( string f in Directory.GetFiles(dir, "*.xml") ) {
						cmd.Parameters["spl_xml"].Value = XDocument.Load(f).ToString();
						cmd.ExecuteScalar();
					}
				}
			}
		}

		public enum CollisionsHandling { All, Update, Skip }

		private static int registerFile(string ifile, SqlConnection conn)
		{
			string name = Path.GetFileName(ifile);
			DateTime date_modified = File.GetLastWriteTime(ifile);
			int? fil_id = conn.ExecuteScalar<int?>("select fil_id from import_files where name = @name and date_modified = @date_modified", new { name = name, date_modified = date_modified });
			if ( fil_id == null )
				fil_id = conn.ExecuteScalar<int>("insert into import_files (name, date_modified) values (@name, @date_modified) select SCOPE_IDENTITY()", new { name = name, date_modified = date_modified });
			return (int)fil_id;
		}

		public static void import_sdf(string ifile, CollisionsHandling eCollisionsHandling, IDictionary<string, string> sdfMap)
		{
			using ( StreamWriter err = new StreamWriter(Path.Combine(Path.GetDirectoryName(ifile), Path.GetFileNameWithoutExtension(ifile) + "-err.sdf")) )
			using ( StreamWriter dup = new StreamWriter(Path.Combine(Path.GetDirectoryName(ifile), Path.GetFileNameWithoutExtension(ifile) + "-dup.sdf")) )
			using ( SqlConnection conn = new SqlConnection(ConfigurationManager.ConnectionStrings["SRSConnectionString"].ConnectionString) ) {
				SdfReader r = new SdfReader(ifile);
				r.FieldsMap = sdfMap;

				int fil_id = registerFile(ifile, conn);

				foreach ( SdfRecord sdf in r.Records ) {
					processSdfRecord(eCollisionsHandling, conn, fil_id, sdf, err, dup);
				}
			}
		}

		class XmlCorrector
		{
			public Regex MessageParser;
			public Regex ErroneousExpression;
			public Regex FixedExpression;
		}
		static XmlCorrector[] s_correctors = {
			new XmlCorrector { MessageParser = new Regex("The following elements are not closed: (.*?)."), ErroneousExpression = null, FixedExpression = null }
		};

		private static XElement getFieldsXml(SdfRecord sdf)
		{
			Dictionary<string, string> warnings = new Dictionary<string, string>();
			XElement fields = new XElement("fields");
			foreach ( var p in sdf.Properties ) {
				XElement field = new XElement("field", new XAttribute("name", p.Key));
				foreach ( var v in p.Value ) {
					XElement x = null;
					if ( v.Contains("<") && v.Contains(">") ) {
						for ( int iCorrector = 0; iCorrector < s_correctors.Length; iCorrector++ ) {
							try {
								x = XElement.Parse(v);
							}
							catch ( Exception ex ) {
								warnings.Add("Warning", String.Format("XML: {0}", ex.Message));
							}
						}
					}

					if ( x == null )
						field.Add(v);
					else
						field.Add(new XAttribute("media-type", "text/xml"), x);
				}
				fields.Add(field);
			}

			warnings.ToList().ForEach(d => sdf.AddField(d.Key, d.Value));

			return fields;
		}

		private static void processSdfRecord(CollisionsHandling eCollisionsHandling, SqlConnection conn, int? fil_id, SdfRecord sdf, TextWriter err, TextWriter dup)
		{
			try {
				int? sdf_id_by_hash = conn.ExecuteScalar<int?>("select sdf_id from sdfs where hash = @hash", new { hash = sdf.Hash });
				if ( sdf_id_by_hash != null ) { // We've already imported EXACTLY this record
					Console.Out.Write(" ");
					dup.Write(sdf.ToString());
					return;
				}

				string substance_id = sdf.GetFieldValue("SUBSTANCE_ID");
				int? sdf_id_by_sub_id = conn.ExecuteScalar<int?>("select sdf_id from sdfs where substance_id = @substance_id", new { substance_id = substance_id });

				if ( sdf_id_by_sub_id == null || eCollisionsHandling == CollisionsHandling.All ) {
					conn.ExecuteCommand(@"
								insert into sdfs (mol, substance_id, structure_id, mixture_id, unii, [hash], str_hash, data_hash, cdbregno, fda_id, smiles, inchi, inchi_key, fields, fil_id)
								values (@mol, @substance_id, @structure_id, @mixture_id, @unii, @hash, @str_hash, @data_hash, @cdbregno, @fda_id, @smiles, @inchi, @inchi_key, @fields, @fil_id)",
						new {
							mol = sdf.Molecule,
							substance_id = substance_id,
							structure_id = String.IsNullOrEmpty(sdf.GetFieldValue("STRUCTURE_ID")) ? null : sdf.GetFieldValue("STRUCTURE_ID"),
							mixture_id = String.IsNullOrEmpty(sdf.GetFieldValue("MIX_SUBSTANCE_ID")) ? null : sdf.GetFieldValue("MIX_SUBSTANCE_ID"),
							unii = sdf.GetFieldValue("UNII"),
							hash = sdf.Hash,
							str_hash = sdf.Molecule.StructureHash,
							data_hash = sdf.DataHash,
							cdbregno = sdf.GetFieldValue("CDBREGNO"),
							fda_id = sdf.GetFieldValue("FDA_ID"),
							smiles = sdf.Molecule.SMILES,
							inchi = sdf.Molecule.InChI,
							inchi_key = sdf.Molecule.InChIKey,
							fields = getFieldsXml(sdf).ToString(),
							fil_id = fil_id
						});
					Console.Out.Write("+");
				}
				else if ( eCollisionsHandling == CollisionsHandling.Update ) {
					conn.ExecuteCommand(@"
								update sdfs set mol = @mol, structure_id = @structure_id, mixture_id = @mixture_id, unii = @unii, [hash] = @hash, str_hash = @str_hash, data_hash = @data_hash,
									cdbregno = @cdbregno, fda_id = @fda_id, smiles = @smiles, inchi = @inchi, inchi_key = @inchi_key, fields = @fields, fil_id = @fil_id
								where substance_id = @substance_id",
						new {
							mol = sdf.Molecule,
							substance_id = substance_id,
							structure_id = String.IsNullOrEmpty(sdf.GetFieldValue("STRUCTURE_ID")) ? null : sdf.GetFieldValue("STRUCTURE_ID"),
							mixture_id = String.IsNullOrEmpty(sdf.GetFieldValue("MIX_SUBSTANCE_ID")) ? null : sdf.GetFieldValue("MIX_SUBSTANCE_ID"),
							unii = sdf.GetFieldValue("UNII"),
							hash = sdf.Hash,
							str_hash = sdf.Molecule.StructureHash,
							data_hash = sdf.DataHash,
							cdbregno = sdf.GetFieldValue("CDBREGNO"),
							fda_id = sdf.GetFieldValue("FDA_ID"),
							smiles = sdf.Molecule.SMILES,
							inchi = sdf.Molecule.InChI,
							inchi_key = sdf.Molecule.InChIKey,
							fields = getFieldsXml(sdf).ToString(),
							fil_id = fil_id
						});
					Console.Out.Write("u");
				}
				else {  // Skip
					Console.Out.Write(".");
				}
			}
			catch ( Exception ex ) {
				Console.Out.Write("E");
				sdf.AddField("Error", ex.Message);
			}

			if ( sdf.HasField("Error") )
				err.Write(sdf.ToString());
		}

		public static void ImportSrsSdf(ImportOptions impOpt)
		{
			using ( SrsDbContext db = new SrsDbContext() ) {
#if DEBUG
				db.LogToConsole(LogLevel.Debug);
#endif
				SrsSet srsSet = new SrsSet();
				srsSet.Name = impOpt.Name;
				srsSet.FileName = Path.GetFileName(impOpt.InputFile);
				var fi = new FileInfo(impOpt.InputFile);
				srsSet.ImportDate = DateTime.Now;
				srsSet.ModificationDate = fi.LastWriteTime;
				srsSet.Records = new List<SrsRecord>();
				db.SrsSets.Add(srsSet);
				db.SaveChanges();

				int counter = 0;
				using ( SdfReader sdf = new SdfReader(impOpt.InputFile) ) {
					foreach ( var rec in sdf.Records ) {
						StringBuilder errors = new StringBuilder();
						XDocument descXml = null;
						string desc = null;
						try {
							descXml = rec.GetDescXml("DESC_PART", SrsDomain.Any, impOpt);
						}
						catch ( Exception ex ) {
							desc = rec.GetConcatXmlFields("DESC_PART");
							errors.AppendLine(ex.Message);
						}

						SrsRecord srsRec = new SrsRecord {
							UNII = rec.GetFieldValue("UNII"),
							SubstanceId = rec.GetFieldValue("SUBSTANCE_ID"),
							Sdf = rec.ToString(),
							InChIKey = rec.Molecule?.InChIKey,
							Desc = desc,
							DescXml = descXml?.ToString(),
							Ref = rec.GetConcatXmlFields("REF_INFO_PART"),
							Comments = rec.GetFieldValue("COMMENTS"),
							Errors = errors.ToString()
						};

						srsSet.Records.Add(srsRec);
						db.SrsRecords.Add(srsRec);
						db.SaveChanges();

						Console.Out.Write(".");
						if ( ++counter % 1000 == 0 )
							Console.Out.Write(counter);
					}
				}

				db.SaveChanges();
			}
		}
	}
}
