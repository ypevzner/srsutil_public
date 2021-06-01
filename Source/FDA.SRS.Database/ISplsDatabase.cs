using FDA.SRS.Utils;
using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Linq;

namespace FDA.SRS.Database
{
	public interface ISplSet
	{
		int Id { get; set; }
		string Name { get; set; }
		DateTime SetDate { get; set; }
	}

	public interface ISplDoc
	{
		int Id { get; set; }
		int? SetId { get; set; }
		string UNII { get; set; }
		Guid? DocId { get; set; }
		int? Version { get; set; }
		byte[] Spl { get; set; }
		byte[] SplDiff { get; set; }
		byte[] Sdf { get; set; }
		byte[] Srs { get; set; }
		byte[] Err { get; set; }
	}

	public interface ISplsDatabase : IDisposable
	{
		IEnumerable<int> GetSets();
		ISplSet GetSet(int id);
		int AddSet(string name);

		IEnumerable<int> GetDocs(int? setId);
		ISplDoc GetDoc(int id);
		ISplDoc GetDocByUnii(string unii, int? setId = null);
		int AddDoc(int? setId, string unii, XDocument spl, XDocument splDiff, string sdf, string srs, string err);
	}

	public static class ISplDocExtensions
	{
		public static XDocument SplXml(this ISplDoc doc)
		{
			return XDocument.Parse(doc.Spl());
		}

		public static void SplXml(this ISplDoc doc, XDocument xdoc)
		{
			doc.Spl(xdoc?.ToString());
		}

		public static XDocument SplDiffXml(this ISplDoc doc)
		{
			return XDocument.Parse(doc.SplDiff());
		}

		public static void SplDiffXml(this ISplDoc doc, XDocument xdoc)
		{
			doc.SplDiff(xdoc?.ToString());
		}

		public static string Spl(this ISplDoc doc)
		{
			return doc.Spl == null ? null : Encoding.UTF8.GetString(doc.Spl.Ungzip());
		}

		public static void Spl(this ISplDoc doc, string spl)
		{
			doc.Spl = spl == null ? null : Encoding.UTF8.GetBytes(spl).Gzip();
		}

		public static string SplDiff(this ISplDoc doc)
		{
			return doc.SplDiff == null ? null : Encoding.UTF8.GetString(doc.SplDiff.Ungzip());
		}

		public static void SplDiff(this ISplDoc doc, string spl)
		{
			doc.SplDiff = spl == null ? null : Encoding.UTF8.GetBytes(spl).Gzip();
		}

		public static string Srs(this ISplDoc doc)
		{
			return doc.Srs == null ? null : Encoding.UTF8.GetString(doc.Srs.Ungzip());
		}

		public static void Srs(this ISplDoc doc, string spl)
		{
			doc.Srs = spl == null ? null : Encoding.UTF8.GetBytes(spl).Gzip();
		}

		public static string Sdf(this ISplDoc doc)
		{
			return doc.Sdf == null ? null : Encoding.UTF8.GetString(doc.Sdf.Ungzip());
		}

		public static void Sdf(this ISplDoc doc, string spl)
		{
			doc.Sdf = spl == null ? null : Encoding.UTF8.GetBytes(spl).Gzip();
		}

		public static string Err(this ISplDoc doc)
		{
			return doc.Err == null ? null : Encoding.UTF8.GetString(doc.Err.Ungzip());
		}

		public static void Err(this ISplDoc doc, string spl)
		{
			doc.Err = spl == null ? null : Encoding.UTF8.GetBytes(spl).Gzip();
		}
	}
}
