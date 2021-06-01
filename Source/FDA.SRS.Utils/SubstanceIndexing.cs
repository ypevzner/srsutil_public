using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Media;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace FDA.SRS.Utils
{
	/// <summary>
	/// UNII|Primary name|Link|SetId|Version Number|Submission Time
	/// </summary>
	public class SubstanceIndexing : ISubstanceIndexing
	{
		private Dictionary<string, SubstanceInfo> _uniiInfos = new Dictionary<string, SubstanceInfo>();
		private Dictionary<Guid, SubstanceInfo> _hashInfos = new Dictionary<Guid, SubstanceInfo>();
		private string _file;

        private bool isCache = false;



        private static string getCacheFile(string fetch) {
            string today =DateTime.Today.ToString();
            return "cache_substindex" + (fetch+today).GetMD5String() + ".dat";
        }

        // Uses cached version of file if made on same day
		public SubstanceIndexing(string file = null)
        {
            string cachename = getCacheFile(file);
            if (File.Exists(cachename)) {
                isCache = true;
                _file = cachename;
                StringReader reader = new StringReader(File.ReadAllText(cachename));
                string header =reader.ReadLine();
                _read(reader);
            } else {
                _file = file;
                _read(file);
            }
			
		}

		public static TextReader _openTextUri(Uri uri)
		{
			WebRequest request = WebRequest.Create(uri);
			if ( uri.Scheme != "file" )
				request.UseDefaultCredentials = true;

			return new StreamReader(request.GetResponse().GetResponseStream());
		}

		private void _read(string uri)
		{

			TextReader reader = null;
			try {
				if ( !String.IsNullOrEmpty(uri) ) {
					if ( !Regex.IsMatch(uri, @"^[a-z]{3,5}://") )
						uri = "file:///" + uri;
					Uri t;
					if ( Uri.TryCreate(uri, UriKind.RelativeOrAbsolute, out t) ) {
						try {
							reader = _openTextUri(t);
						}
						catch(Exception e) {

							reader = null;
						}
					}
				}

				if ( reader == null ) {
					Trace.TraceWarning("Cannot access '{0}' - falling back to an internal version of SubstanceIndexing.dat", uri);
					reader = new StringReader(Resources.SubstanceIndexing);
				}
				
				string line = reader.ReadLine();  // Titles line
				if ( String.IsNullOrWhiteSpace(line) || !line.StartsWith("UNII|") ) {
					Trace.TraceWarning("Unexpected format of SubstanceIndexing.dat - falling back to internal version");
                    reader = new StringReader(Resources.SubstanceIndexing);
                    reader.ReadLine();
                } else {
                    //prepare cache file
                    if (!this.isCache) {
                        string cacheFile = getCacheFile(_file);

                        using (StreamWriter writer = new StreamWriter(cacheFile)) {
                            writer.WriteLine(line);
                            while ((line = reader.ReadLine()) != null) {
                                writer.WriteLine(line);
                            }
                        }
                        reader = new StringReader(File.ReadAllText(cacheFile));
                        reader.ReadLine();
                    }
                }
				
				_read(reader);
			}
			finally {
				reader.Close();
			}
		}

		private void _read(TextReader reader)
		{
			string line;
			while ( (line = reader.ReadLine()) != null ) {
                
				string[] parts = line.Split('|').Select(s => s.Trim()).ToArray();
				if ( parts.Length != 8 )
					throw new FormatException(String.Format("Invalid line: {0}", line));

				if ( _uniiInfos.ContainsKey(parts[0]) )
					Trace.TraceWarning("'{0}' was already added from SubstanceIndexing", parts[0]);
				else {
					var si = new SubstanceInfo {
						UNII = String.IsNullOrWhiteSpace(parts[0]) ? null : parts[0],
						PrimaryName = String.IsNullOrWhiteSpace(parts[1]) ? null : parts[1],
						Hash = Guid.Parse(parts[2]),
						Link = Guid.Parse(parts[3]),
						SetId = Guid.Parse(parts[4]),
						VersionNumber = int.Parse(parts[5]),
						SubmissionTime = DateTime.ParseExact(parts[6], "yyyyMMddHHmmss.FFF", null),    // e.g. 20130603204239.505
						Citation = String.IsNullOrWhiteSpace(parts[7]) ? null : System.Net.WebUtility.HtmlDecode(parts[7])
					};
                    if (!_uniiInfos.ContainsKey(si.UNII)) {
                        _uniiInfos.Add(si.UNII, si);
                    } else {
                        SystemSounds.Beep.Play();
                        throw new FatalException("Duplicate UNII:" + si.UNII);
                    }


                    if (!_hashInfos.ContainsKey(si.Hash)) {
                        _hashInfos.Add(si.Hash, si);
                    } else {
                        throw new FatalException("Duplicate HASH: UNII1=" + _hashInfos[si.Hash].UNII + 
                                  ", UNII2=" + si.UNII + ", hash=" + si.Hash);
                    }
                }
			}
		}

		private void _write(string file, List<SubstanceInfo> infos)
		{
			using ( StreamWriter writer = new StreamWriter(file) )
				_write(writer, infos);
		}

		private void _write(StreamWriter writer, List<SubstanceInfo> infos)
		{
			writer.WriteLine("UNII|Primary name|Link|SetId|Version Number|Submission Time");
			writer.WriteLine("UNII|Primary Name|Hash Code|Link|SetId|Version Number|Load Time|Citation");
			
			infos.ForEach(info => writer.WriteLine("{0}|{1}|{2}|{3}|{4}|{5}|{6}|{7}",
				info.UNII,
				info.PrimaryName,
				info.Hash,
				info.Link,
				info.SetId,
				info.VersionNumber,
				info.SubmissionTime.ToString("yyyyMMddHHmmss.FFF"),
				info.Citation)
			);
		}

		public SubstanceInfo CreateNew(string unii, string primaryName)
		{
			SubstanceInfo info = SubstanceInfo.New(unii, primaryName);
			_uniiInfos.Add(unii, info);
			return info;
		}

		public bool Exists(string unii, Guid? hash = null)
		{
			return _uniiInfos.ContainsKey(unii) && (hash == null || _uniiInfos[unii].Hash == hash);
		}

		public bool Exists(Guid hash)
		{
			return _hashInfos.ContainsKey(hash);
		}

		public SubstanceInfo GetExisting(string unii)
		{
			return _uniiInfos[unii];
		}

		public SubstanceInfo GetExisting(Guid hash)
		{
			return _hashInfos[hash];
		}
	}
}
