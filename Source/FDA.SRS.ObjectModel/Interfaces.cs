using System.Collections.Generic;
using System.Xml.Linq;

namespace FDA.SRS.ObjectModel
{
	public interface IUniquelyIdentifiable
	{
		string UID { get; }
	}

	public interface ISplable
	{
		XElement SPL { get; }
	}

	public interface ISubjectsProvider
	{
		IEnumerable<XElement> Subjects { get; }
	}
}
