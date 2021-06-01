using System.IO;
using System.Net;

namespace FDA.SRS.Database
{
	public abstract class DownloadableDatabase
	{
		public string Database { get; set; }

		public string DownloadUrl { get; set; }

		public string LocalPath { get; set; }

		public void Download()
		{
			WebRequest req = WebRequest.Create(DownloadUrl);
			WebResponse res = req.GetResponse();
			Stream webStream = res.GetResponseStream();
			Stream dbStream = File.Create(LocalPath);
			webStream.CopyTo(dbStream);
		}
	}
}
