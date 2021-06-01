using System;
using System.Xml.Linq;

namespace FDA.SRS.ObjectModel
{
	public class SplSection : SplObject
	{
		private ISubjectsProvider _subjectsProvider;

		public SplSection(SplObject rootObject, ISubjectsProvider subjectsProvider)
			: base(rootObject, "section")
		{
			_subjectsProvider = subjectsProvider;
		}

		public override XElement SPL
		{
			get
			{
				XElement xSection =
					new XElement(xmlns.spl + "section",
						new XElement(xmlns.spl + "id", new XAttribute("root", Guid.NewGuid().ToString())),
						new XElement(xmlns.spl + "code", new XAttribute("code", Code ?? ""), new XAttribute("codeSystem", CodeSystem ?? ""), new XAttribute("displayName", DisplayName ?? "")),
					// new XElement(xmlns.spl + "title"),
					// new XElement(xmlns.spl + "text"),
						new XElement(xmlns.spl + "effectiveTime", new XAttribute("value", DateTime.Now.ToString("yyyyMMdd"))));

				foreach ( var xSubject in _subjectsProvider.Subjects ) {
					if ( xSubject != null )
						xSection.Add(xSubject);
				}

				return xSection;
			}
		}
	}
}
