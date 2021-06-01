using System;
using System.Collections.Generic;

namespace FDA.SRS.ObjectModel
{
	public class SourceMaterial
	{
		public string MaterialClass { get; set; }
		public string MaterialType { get; set; }
		public string MaterialState { get; set; }

		public string SubstanceId { get; set; }
		public string SubstanceName { get; set; }

		public string Part { get; set; }
		public string PartLocation { get; set; }

		public string FractionType { get; set; }
		public string Fraction { get; set; }

		public Organism Organism { get; set; }

		public string Name { get; set; }

        //Added for Ticket 312 : Structurally Diverse
        public string ApprovalId { get; set; }
        public string linkingId { get; set; }
        public string refPname { get; set; }

        public string Step { get; set; }
        public string Code { get; set; }
        public string CodeSystem { get; set; }
        public string DispName { get; set; }
        public string InteractorDisplayName { get; set; }
        public string InteractorCodeSystem { get; set; }
        public string InteractorCode { get; set; }

        public string InfraspecificType { get; set; }
        public string InfraspecificDescription { get; set; }
        ////
        public string DevelopmentalStage { get; set; } //Added for Ticket 352

        private bool _hasParent = false;

        /// <summary>
        /// Returns true if the source material has a linked parent record,
        /// such as a whole organism record
        /// </summary>
        /// <returns></returns>
        public bool hasParent() {
            return _hasParent;
        }

        public void setHasParent(bool hasParent) {
            this._hasParent = hasParent;
        }

        private static HashSet<string> _exceptions = new HashSet<string> { "SEROTYPE", "SEROVAR", "SEROGROUP", "CULTIVAR", "STRAIN", "SUBSTRAIN" };

		public string Reference
		{
			get
			{
				if ( Organism == null )
					return null;

				List<string> parts = new List<string>();

				if ( MaterialType?.ToUpper() == "VIRUS" )
					parts.Add(Organism.Species);
				else {
					if ( Organism.Genus != null )
						parts.Add(Organism.Genus);
                    if (Organism.Species != null)
                        parts.Add(Organism.Species.ToLower()); 
                        
                }

                if (Organism.IntraspecificType != null && !_exceptions.Contains(Organism.IntraspecificType.ToUpper()))
                    parts.Add(Organism.IntraspecificDescription.ToLower());  
                    

                    return String.Join(" ", parts);
			}
		}

		public string DisplayName
		{
			get
			{
				return String.Join(" ", PartLocation, Part, FractionType, Fraction).Trim();
			}
		}
	}
}
