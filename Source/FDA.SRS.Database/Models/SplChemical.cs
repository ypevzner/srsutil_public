using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FDA.SRS.Database.Models
{
	public class SplChemical
	{
		[Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
		public int Id { get; set; }

		[MaxLength(10)]
		public string UNII { get; set; }

		[Required]
		public string Mol { get; set; }

		[Required]
		public string InChI { get; set; }

		[Required, MaxLength(27)]
		public string InChIKey { get; set; }

		public ICollection<SplDocChemical> DocChemicals { get; set; }
	}

	public class SplChemicalComparer : IEqualityComparer<SplChemical>
	{
		public bool Equals(SplChemical x, SplChemical y)
		{
			return x.InChIKey == y.InChIKey;
		}

		public int GetHashCode(SplChemical obj)
		{
			return obj.InChIKey.GetHashCode();
		}
	}

	public class SplDocChemical
	{
		[Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
		public int Id { get; set; }

		public int DocId { get; set; }
		public SplDoc Doc { get; set; }

		public int ChemicalId { get; set; }
		public SplChemical Chemical { get; set; }
	}
}
