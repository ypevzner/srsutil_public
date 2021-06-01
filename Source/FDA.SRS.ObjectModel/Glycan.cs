using System;
using System.Xml.Linq;

namespace FDA.SRS.ObjectModel
{
	public class Glycan : SplObject, IUniquelyIdentifiable
	{
		public Glycan(SplObject rootObject, string type)
			: base(rootObject, type)
		{

		}
		public override XElement SPL {
			get {
				throw new NotImplementedException();
			}
		}

		public string UID {
			get {
				throw new NotImplementedException();
			}
		}
	}
}
