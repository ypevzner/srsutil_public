using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Xml.Linq;
using System.Xml.Schema;

namespace FDA.SRS.ObjectModel
{
	public class SplDocument : SplObject
	{
		private SplSection _section;

		public override string Id { get { return DocId.ToString(); } }

		public int Version { get; set; }

		public SplDocument(ISubjectsProvider subjectsProvider)
			: base(null, "document")
		{
			_section = new SplSection(this, subjectsProvider);

			Version = 1;
			
			Counters.Reset();

		}

		private List<SplError> _splErrors;
		public void AddError(string code, string message)
		{
			if ( _splErrors == null )
				_splErrors = new List<SplError>();

			_splErrors.Add(new SplError(code, message));
		}

		public override XElement SPL {
			get {
				XDocument xdoc = new XDocument(
					new XElement(xmlns.spl + "document",
						new XAttribute(XNamespace.Xmlns + "xsi", xmlns.xsi),
						new XAttribute(xmlns.xsi + "schemaLocation", xmlns.splSchemaLocation),
						new XElement(xmlns.spl + "id", new XAttribute("root", DocId)),
						new XElement(xmlns.spl + "code", new XAttribute("code", Code ?? ""), new XAttribute("codeSystem", CodeSystem ?? ""), new XAttribute("displayName", DisplayName ?? "")),
						new XElement(xmlns.spl + "title"),
						new XElement(xmlns.spl + "effectiveTime", new XAttribute("value", DateTime.Now.ToString("yyyyMMdd"))),
						new XElement(xmlns.spl + "setId", new XAttribute("root", SetId)),
						new XElement(xmlns.spl + "versionNumber", new XAttribute("value", Version)),
						new XElement(xmlns.spl + "author",
							new XElement(xmlns.spl + "assignedEntity",
								new XElement(xmlns.spl + "representedOrganization",
									new XElement(xmlns.spl + "id", new XAttribute("extension", "927645523"), new XAttribute("root", "1.3.6.1.4.1.519.1")),
									new XElement(xmlns.spl + "name", "Food and Drug Administration")
								)
							)
						),
						new XElement(xmlns.spl + "component",
							new XElement(xmlns.spl + "structuredBody",
								new XElement(xmlns.spl + "component", _section.SPL)
							)
						)
					)
				);

				if ( _splErrors != null && _splErrors.Any() )
					_splErrors.ForEach(e => xdoc.Root.Add(e.SPL));

				return xdoc.Root;
			}
		}

		public XDocument GetXml(bool validate, ValidationEventHandler handler = null)
		{
			var xml =
				new XDocument(
					new XDeclaration("1.0", "utf-8", "yes"),
					new XProcessingInstruction("xml-stylesheet", "type=\"text/xsl\" href=\"https://www.accessdata.fda.gov/spl/stylesheet/spl.xsl\""),
					SPL);

			if ( validate )
				xml.ValidateSpl(handler);

			return xml;
		}
	}

	public static class SplDocumentExtensions
	{
		private static XmlSchemaSet _xsd;
		private static object _lock = new object();

		public static void ValidateSpl(this XDocument xml, ValidationEventHandler handler = null)
		{
            //Added for Ticket 328 
            System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12;
            //End
            lock ( _lock ) {
				if ( _xsd == null ) {
					_xsd = new XmlSchemaSet();
					_xsd.Add("urn:hl7-org:v3", "https://www.accessdata.fda.gov/spl/schema/spl.xsd");
				}
			}

			if ( handler == null )
				handler = (o, a) => { Trace.TraceError("{0}: {1}", o, a.Message); };

			xml.Validate(_xsd, handler);
		}
	}
}
