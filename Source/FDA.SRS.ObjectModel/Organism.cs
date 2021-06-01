namespace FDA.SRS.ObjectModel
{
	public class Organism
	{
		public string Kingdom { get; set; }
		public string Phylum { get; set; }
		public string Class { get; set; }
		public string Order { get; set; }
		public string Family { get; set; }
		
		private string _genus;
		public string Genus
		{
			get { return _genus; }
			set
			{
				_genus = value;
                if (_genus != null)
                    _genus = char.ToUpper(_genus[0]) + _genus.Substring(1).ToLower();
                    
            }
		}

		private string _species;
		public string Species
		{
			get { return _species; }
			set
			{
				_species = value;
				if ( _species != null )
                    //Ticket 397 Donot change capitalization
                    //_species = char.ToUpper(_species[0]) + _species.Substring(1).ToLower();
                    _species = _species[0] + _species.Substring(1);
            }
        }

		public string IntraspecificType { get; set; }
		public string IntraspecificDescription { get; set; }
		
	}
}
