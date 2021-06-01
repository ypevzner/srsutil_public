using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace FDA.SRS.Utils
{
	public static class FileUtils
	{
		/// <summary>
		/// Enumerates files in directory
		/// </summary>
		/// <param name="dir">Directory to list files in</param>
		/// <param name="recursively">If true then enumeration is recursive</param>
		/// <param name="filter">Regular expression to filter file names</param>
		/// <returns>IEnumerable of found files</returns>
		public static IEnumerable<string> ListFiles(string dir, bool recursively = false, string filter = null)
		{
			Regex regex = null;
			if ( filter != null )
				regex = new Regex(filter, RegexOptions.IgnoreCase);

			List<string> files = new List<string>();

			_listDirFiles(regex, files, dir);

			if ( recursively ) {
				foreach ( string d in Directory.GetDirectories(dir) )
					files.AddRange(ListFiles(d, recursively, filter));
			}

			return files;
		}

		private static void _listDirFiles(Regex regex, List<string> files, string d)
		{
			foreach ( string f in Directory.GetFiles(d) ) {
				if ( regex == null || regex.IsMatch(Path.GetFileName(f)) )
					files.Add(f);
			}
		}
	}
}
