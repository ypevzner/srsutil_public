using System;
using System.Xml.Linq;

namespace FDA.SRS.ObjectModel
{
	public class SplNamedEntity : SplObject
	{
		public SplNamedEntity(SplObject rootObject)
			: base(rootObject, "SY")
		{

		}

		public string UID
		{
			get { throw new NotImplementedException(); }
		}

		public override XElement SPL
		{
			get { throw new NotImplementedException(); }
		}
	}
}
