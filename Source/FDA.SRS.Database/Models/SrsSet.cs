using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FDA.SRS.Database.Models
{
	public class SrsSet : InfoSet
	{
		[DatabaseGenerated(DatabaseGeneratedOption.Identity)]
		public int Id { get; set; }

		[MaxLength(300)]
		public string FileName { get; set; }

		public DateTime? ImportDate { get; set; }

		public DateTime? ModificationDate { get; set; }
		
		public ICollection<SrsRecord> Records { get; set; }
	}
}
