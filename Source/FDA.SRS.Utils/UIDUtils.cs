using System;

namespace FDA.SRS.Utils
{
	public static class UIDUtils
	{
		public static string FormatAsGuid(this string md5hash)
		{
			if ( md5hash.Length != 32 )
				throw new ArgumentOutOfRangeException("md5hash", "String must be 32 characters in length");

			return md5hash
				.Insert(20, "-")
				.Insert(16, "-")
				.Insert(12, "-")
				.Insert(8, "-");
		}
	}
}
