namespace FDA.SRS.ObjectModel
{
	public class ProteinProperty : IUniquelyIdentifiable
	{
		public string PropertyType { get; internal set; }
		public string PropertyValue { get; internal set; }
		public Amount Amount { get; internal set; }

		public string UID
		{
			get {
				return
					( PropertyType ?? "" ) + "_" +
					( PropertyValue ?? "" ) + "_" +
					( Amount.UID ?? "" );
			}
		}
		
	}
}
