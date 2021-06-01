using System.ComponentModel.DataAnnotations;

namespace FDA.SRS.Database.Models
{
	public class InfoSet
	{
		[MaxLength(100)]
		public string Name { get; set; }

		public string Description { get; set; }
	}
}
