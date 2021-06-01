using System;
using System.IO;
using System.Net;
using System.Threading;

namespace FDA.SRS.Utils
{
	public static class DownloadUtils
	{
        /// <summary>
        /// Downloads a file from a Uri to the local file stored at the supplied path and returns the download path. 
        /// If the file already exists, it will instead just return that file directory, unless it's set to 
        /// force the download with the last argument being "true".
        /// </summary>
        /// <param name="downloadUrl"></param>
        /// <param name="localZip"></param>
        /// <param name="force"></param>
        /// <returns></returns>
		public static string Download(Uri downloadUrl, string localZip, bool force)
		{
			Console.Out.WriteLine($"{downloadUrl} => {localZip}...");

			if (!force && File.Exists(localZip) && new FileInfo(localZip).Length > 0 )
				Console.Out.WriteLine("Local file found ({0}) - skipping...", localZip);
			else {
				Console.Out.Write("Downloading...     ");
                //localZip = "";
				if ( String.IsNullOrEmpty(localZip) )
					localZip = Path.Combine(Path.GetTempPath(), Path.GetFileName(downloadUrl.LocalPath));

				using ( WebClient web = new WebClient() ) {
					web.DownloadProgressChanged += (sender, e) => { Console.Out.Write("\b\b\b\b{0,3}%", e.ProgressPercentage); };
					web.DownloadFileAsync(downloadUrl, localZip);
					while ( web.IsBusy )
						Thread.Sleep(100);
				}

				Console.Out.WriteLine("\b\b\b\bdone. ");
			}

			return localZip;
		}
	}
}
