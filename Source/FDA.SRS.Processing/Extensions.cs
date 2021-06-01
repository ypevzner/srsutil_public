using FDA.SRS.Utils;

namespace FDA.SRS.Processing
{
	public static class ConvertOptionsExtensions
	{
		public static SubstanceInfo GetSubstanceInfo(this OperationalParameters pars, string unii, string primaryname = null)
		{
			SubstanceInfo info;
			
				if (!ReferenceDatabases.Indexes.Exists(unii))
					info = ReferenceDatabases.Indexes.CreateNew(unii, primaryname);
				else {
					info = ReferenceDatabases.Indexes.GetExisting(unii);
					info.VersionNumber++;
				}
			
			return info;
		}
	}
}
