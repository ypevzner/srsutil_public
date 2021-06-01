using System.Collections.Generic;
using System.Configuration;
using System.Xml;

namespace FDA.SRS.Utils
{
	public class SrsPartsSection : IConfigurationSectionHandler
	{
		private static SrsPartsSection _configSection;
		public static SrsPartsSection Get(string section)
		{
			if ( _configSection == null )
				_configSection = ConfigurationManager.GetSection(section) as SrsPartsSection;
			return _configSection;
		}

		private List<SrsPartElement> _parts;
		public IEnumerable<SrsPartElement> Parts
		{
			get
			{
				return _parts;
			}
		}

		public object Create(object parent, object configContext, XmlNode section)
		{
			_parts = new List<SrsPartElement>();

			foreach ( XmlNode child in section.ChildNodes ) {
				if ( XmlNodeType.Element == child.NodeType ) {
					_parts.Add(new SrsPartElement {
						CodeId = child.Attributes["code-id"].Value,
						Part = child.Attributes["part"] != null ? child.Attributes["part"].Value : null,
						PartLocation = child.Attributes["part-location"] != null ? child.Attributes["part-location"].Value : null,
					});
				}
			}

			return this;
		}
	}

	public class SrsPartElement : ConfigurationElement
	{
		public string Part { get; set; }

		public string PartLocation { get; set; }

		public string CodeId { get; set; }

	}
}
