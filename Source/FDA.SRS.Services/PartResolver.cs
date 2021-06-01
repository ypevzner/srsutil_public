using FDA.SRS.ObjectModel;

namespace FDA.SRS.Services
{
	public class PartResolver
	{
		public SplCodedItem Resolve(string part, string part_location)
		{
			string id = null;
			return new SplCodedItem(null, "identifiedSubstance", id);
		}
	}
}
