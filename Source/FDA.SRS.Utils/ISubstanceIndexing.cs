using System;

namespace FDA.SRS.Utils
{
	public interface ISubstanceIndexing
	{
		/// <summary>
		/// Create new entry in in-memory copy of eList
		/// </summary>
		/// <param name="unii"></param>
		/// <param name="primaryName"></param>
		/// <returns></returns>
		SubstanceInfo CreateNew(string unii, string primaryName);

		/// <summary>
		/// Check if substance hash code is already registered in eList
		/// </summary>
		/// <param name="hash"></param>
		/// <returns></returns>
		bool Exists(Guid hash);

		/// <summary>
		/// Check if substance UNII (and hash code) is already registered in eList
		/// </summary>
		/// <param name="unii"></param>
		/// <param name="hash"></param>
		/// <returns></returns>
		bool Exists(string unii, Guid? hash = default(Guid?));

		/// <summary>
		/// Get substance information from eList by UNII
		/// </summary>
		/// <param name="unii"></param>
		/// <returns></returns>
		SubstanceInfo GetExisting(string unii);

		/// <summary>
		/// Get substance information from eList by hash code
		/// </summary>
		/// <param name="hash"></param>
		/// <returns></returns>
		SubstanceInfo GetExisting(Guid hash);
	}
}
