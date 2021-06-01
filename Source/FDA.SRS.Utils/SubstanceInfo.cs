using System;

namespace FDA.SRS.Utils
{
	public class SubstanceInfo
	{
		public string UNII { get; set; }
		public string PrimaryName { get; set; }
		public Guid Hash { get; set; }
		public Guid Link { get; set; }
		public Guid SetId { get; set; }
		public int VersionNumber { get; set; }
		public DateTime SubmissionTime { get; set; }
		public string Citation { get; set; }

		public static SubstanceInfo New(string unii, string primaryName)
		{
			return new SubstanceInfo {
				UNII = unii,
				PrimaryName = primaryName,
				Link = Guid.NewGuid(),
				SetId = Guid.NewGuid(),
				VersionNumber = 1,
				SubmissionTime = DateTime.Now,
				Citation = null
			};
		}
	}
}
