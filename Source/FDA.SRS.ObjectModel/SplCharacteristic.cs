using System;
using System.Linq;
using System.Xml.Linq;

namespace FDA.SRS.ObjectModel
{
	class SplCharacteristic : SplObject
	{
		public string Value { get; set; }

		public SplCharacteristic(string id, string value)
			: base(null, id)
		{
			Value = value;
		}

		public override XElement SPL
		{
			get
			{
				var xSubjectOf = new XElement(xmlns.spl + "subjectOf",
					new XElement(xmlns.spl + "characteristic",
						new XElement(xmlns.spl + "code",
							new XAttribute("code", Code ?? ""),
							new XAttribute("codeSystem", CodeSystem ?? ""),
							new XAttribute("displayName", DisplayName ?? "")
						),
						new XElement(xmlns.spl + "value",
							( Value ?? "" ).Contains('\n') ? new XCData(Value ?? "") : new XText(Value ?? "")
						)
					)
				);

				if ( !String.IsNullOrEmpty(ValueType) )
					xSubjectOf
						.Element(xmlns.spl + "characteristic")
						.Element(xmlns.spl + "value")
						.Add(new XAttribute(xmlns.xsi + "type", ValueType));

				if ( !String.IsNullOrEmpty(MediaType) )
					xSubjectOf
						.Element(xmlns.spl + "characteristic")
						.Element(xmlns.spl + "value")
						.Add(new XAttribute("mediaType", MediaType));

				return xSubjectOf;
			}
		}
	}
}
