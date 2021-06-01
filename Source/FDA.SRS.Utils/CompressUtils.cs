using ICSharpCode.SharpZipLib.GZip;
using System.IO;

namespace FDA.SRS.Utils
{
	public static class CompressUtils
	{
		public static byte[] Gzip(this byte[] bytes, int level = 6)
		{
			if ( bytes == null )
				return null;

			using ( MemoryStream ms = new MemoryStream() )
			using ( GZipOutputStream zs = new GZipOutputStream(ms) ) {
				zs.SetLevel(level);
				zs.Write(bytes, 0, bytes.Length);
				zs.Flush();
				zs.Finish();
				return ms.ToArray();
			}
		}

		public static byte[] Ungzip(this byte[] bytes)
		{
			if ( bytes == null )
				return null;

			using ( MemoryStream ms = new MemoryStream(bytes) )
			using ( MemoryStream oms = new MemoryStream() )
			using ( GZipInputStream zs = new GZipInputStream(ms) ) {
				zs.CopyTo(oms);
				return oms.ToArray();
			}
		}
	}
}
