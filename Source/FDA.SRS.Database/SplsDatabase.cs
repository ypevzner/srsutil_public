using FDA.SRS.Database.Models;
using FDA.SRS.Utils;
using SQLite;
using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;

namespace FDA.SRS.Database
{
	public class SplsDatabase : ISplsDatabase
	{
		SQLiteConnection _db;

		public SplsDatabase(string file)
		{
            String fp= new FileInfo(file).Directory.FullName;

            if (!Directory.Exists(fp)) {
                throw new Exception("Directory of database:" + fp + " does not exist");
            }

			_db = new SQLiteConnection(file);
			_db.CreateTable<SQLiteSplSet>();
			_db.CreateTable<SQLiteSplDoc>();
		}

		public IEnumerable<int> GetSets()
		{
			return _db.Table<SQLiteSplSet>().Select(t => t.Id);
		}

		public ISplSet GetSet(int setId)
		{
			return _db.Table<SQLiteSplSet>()
				.Where(t => setId == t.Id)
				.FirstOrDefault();
		}

		public IEnumerable<int> GetDocs(int? setId)
		{
			return _db.Table<SQLiteSplDoc>()
				// .Where(t => setId == null || setId == t.SetId)
				.Select(t => t.Id);
		}

		public ISplDoc GetDoc(int docId)
		{
			return _db.Table<SQLiteSplDoc>()
				.Where(t => docId == t.Id)
				.FirstOrDefault();
		}

		public ISplDoc GetDocByUnii(string unii, int? setId)
		{
			return _db.Table<SQLiteSplDoc>()
				.Where(t => t.UNII == unii && (setId == null || t.SetId == setId))
				.FirstOrDefault();
		}

		public int AddSet(string name)
		{
			return _db.Insert(new SQLiteSplSet {
				SetDate = DateTime.Now,
				Name = name
			});
		}

		public int AddDoc(int? setId, string unii, XDocument spl, XDocument splDiff, string sdf, string srs, string err)
		{
			var doc = new SQLiteSplDoc {
				SetId = setId,
				DocId = spl?.SplDocId(),
				Version = spl?.SplVersion(),
				UNII = unii ?? spl?.SplUNII()
			};

			doc.SplXml(spl);
			doc.SplDiffXml(splDiff);
			doc.Sdf(sdf);
			doc.Srs(srs);
			doc.Err(err);

			return _db.Insert(doc);
		}

		public void Dispose()
		{
			_db.Close();
		}
	}

	public static class SplsDatabaseExtensions
	{
		
	}
}
