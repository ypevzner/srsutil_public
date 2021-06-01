using System.Linq;
using System.Xml.Linq;

namespace FDA.SRS.ObjectModel
{
	public class SplError : SplObject
	{
		/*
		<subjectOf>
			<action>
				<code code="bad-name-error" codeSystem="1.2.3"/>
				<text>Metanephrops rubellus (Moreira, 1903)</text>
			</action>
		</subjectOf>
		 * */

		public SplError(string code, string message)
			: base(null, "error")
		{
			Code = code;
			Name = message;
		}

		public override XElement SPL
		{
			get {
				var xSubjectOf = new XElement(xmlns.spl + "subjectOf",
					new XElement(xmlns.spl + "action",
						new XElement(xmlns.spl + "code",
							new XAttribute("code", Code ?? ""),
							new XAttribute("codeSystem", CodeSystem ?? "")
						),
						new XElement(xmlns.spl + "text", ( Name ?? "" ).Contains('\n') ? new XCData(Name ?? "") : new XText(Name ?? "")
						)
					)
				);
				return xSubjectOf;
			}
		}
	}
}
