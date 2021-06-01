namespace FDA.SRS.ObjectModel
{
	public class MolecularWeight : IUniquelyIdentifiable
	{
		public string WeightType { get; set; }
		public string WeightMethod { get; set; }
		public Amount Amount { get; set; }

		public string UID
		{
			get { return ( WeightType ?? "" ) + "_" + ( WeightMethod ?? "" ) + "_" + ( Amount.UID ?? "" ); }
		}
	}
}
