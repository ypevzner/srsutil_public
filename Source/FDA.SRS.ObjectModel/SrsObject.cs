using System.Collections.Generic;
using System.Xml.Linq;

namespace FDA.SRS.ObjectModel
{
	public abstract class SrsObject : IUniquelyIdentifiable, ISubjectsProvider
	{
		public string UNII { get; set; }

		public abstract string UID { get; }

		public abstract IEnumerable<XElement> Subjects { get; }
	}
}
