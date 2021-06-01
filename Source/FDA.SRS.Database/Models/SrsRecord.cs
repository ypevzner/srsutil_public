using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FDA.SRS.Database.Models
{
	public class SrsRecord
	{
		[Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
		public int Id { get; set; }

		[Required]
		[MaxLength(37)]
		public string SubstanceId { get; set; }

		[MaxLength(10)]
		public string UNII { get; set; }

		public string Sdf { get; set; }

		[MaxLength(27)]
		public string InChIKey { get; set; }

		public string Desc { get; set; }

		public string DescXml { get; set; }

		public string Ref { get; set; }

		public string Errors { get; set; }

		public string Comments { get; set; }
	}
}
