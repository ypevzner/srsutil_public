using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Xml.Linq;

namespace FDA.SRS.Database.Models
{
	public class SplDoc
	{
		[Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
		public int Id { get; set; }

		public Guid DocId { get; set; }

		public int Version { get; set; }

		[Required, MaxLength(10)]
		public string UNII { get; set; }

		public string Spl { get; set; }

		[NotMapped]
		public XDocument SplXml {
			get { return XDocument.Parse(Spl); }
			set { Spl = value.ToString(); }
		}

		public string Srs { get; set; }
		
		public string Sdf { get; set; }

		public string Err { get; set; }

		public ICollection<SplDocChemical> DocChemicals { get; set; }
	}
}
