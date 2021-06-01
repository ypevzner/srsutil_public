using System.Xml.Linq;

namespace FDA.SRS.ObjectModel
{
	public static class xmlns
	{
		private static XNamespace _spl = "urn:hl7-org:v3";
		public static XNamespace spl
		{
			get { return _spl; }
		}

		private static XNamespace _xsi = "http://www.w3.org/2001/XMLSchema-instance";
		public static XNamespace xsi
		{
			get { return _xsi; }
		}

		private static string _splSchemaLocation = "urn:hl7-org:v3 https://www.accessdata.fda.gov/spl/schema/spl.xsd";
		public static string splSchemaLocation
		{
			get { return _splSchemaLocation; }
		}
	}
}
