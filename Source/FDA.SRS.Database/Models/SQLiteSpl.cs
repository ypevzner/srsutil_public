using SQLite;
using System;

namespace FDA.SRS.Database.Models
{
	public class SQLiteSplSet : ISplSet
	{
		[PrimaryKey, AutoIncrement]
		public int Id { get; set; }

		public DateTime SetDate { get; set; }

		public string Name { get; set; }
	}

	public class SQLiteSplDoc : ISplDoc
	{
		[PrimaryKey, AutoIncrement]
		public int Id { get; set; }

		[Indexed]
		public int? SetId { get; set; }

		[Indexed]
		public Guid? DocId { get; set; }

		public int? Version { get; set; }

		[Indexed, SQLite.MaxLength(10)]
		public string UNII { get; set; }

		public byte[] Spl { get; set; }

		public byte[] SplDiff { get; set; }

		public byte[] Srs { get; set; }

		public byte[] Sdf { get; set; }

		public byte[] Err { get; set; }

	}
}
