using System;
using System.Xml.Linq;

namespace FDA.SRS.ObjectModel
{
	// TODO: check where code is used and rewrite using SplCodedElement
	public class SplCodedItem : SplObject
	{
		private string _element;
		public SplCodedItem(string element, string id, string code = null, string displayName = null, string name = null)
			: base(null, id)
		{
			_element = element;
			if ( code != null )
				Code = code;
			if ( displayName != null )
				DisplayName = displayName;
			if ( name != null )
				Name = name;
		}

		public override XElement SPL
		{
			get
			{
				XElement xel = new XElement(xmlns.spl + _element,
					new XElement(xmlns.spl + "code", new XAttribute("code", Code), new XAttribute("codeSystem", CodeSystem))
				);

				if ( !String.IsNullOrEmpty(Name) )
					xel.Add(new XElement(xmlns.spl + "name", Name));

				return xel;
			}
		}
	}
}
