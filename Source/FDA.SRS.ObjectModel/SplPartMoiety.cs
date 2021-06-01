using FDA.SRS.Utils;
using System;
using System.Xml.Linq;

namespace FDA.SRS.ObjectModel
{
	public class SplPartMoiety : SplObject
	{

		public SplPartMoiety(SplObject rootObject, string id)
			: base(rootObject, id)
		{
			
		}

		public override XElement SPL
		{
			get
			{
				XElement xPartMoiety = new XElement(xmlns.spl + "partMoiety");

				if ( !SplOptions.ExportOptions.Features.Has("no-id-extension-root") )
					xPartMoiety.Add(new XElement(xmlns.spl + "id", new XAttribute("extension", Id ?? ""), new XAttribute("root", RootObject.DocId)));

				xPartMoiety.Add(new XElement(xmlns.spl + "code", new XAttribute("code", Code ?? ""), new XAttribute("codeSystem", CodeSystem ?? RootObject.DocId.ToString()), new XAttribute("displayName", DisplayName ?? "")));

				if ( !String.IsNullOrEmpty(Name) )
					xPartMoiety.Add(new XElement(xmlns.spl + "name", Name));

				return xPartMoiety;
			}
		}
	}
}
