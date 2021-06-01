using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace FDA.SRS.Database.Models
{
	public class SplSet : InfoSet
	{
		[DatabaseGenerated(DatabaseGeneratedOption.Identity)]
		public int Id { get; set; }

		public DateTime? StartTime { get; set; }

		public DateTime? FinishTime { get; set; }
		
		public ICollection<SplDoc> SplDocs { get; set; }
	}
}
