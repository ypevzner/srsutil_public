using System;
using System.Collections.Generic;
using System.Configuration;
using System.Xml;

// http://msdn.microsoft.com/en-us/library/2tw134k3(v=vs.100).aspx
// http://msdn.microsoft.com/en-us/library/ms228056(v=vs.100).aspx

namespace FDA.SRS.Utils
{
	public class SplCodesSection : IConfigurationSectionHandler
	{
		private static object _mutex = new object();
		private static SplCodesSection _configSection;
		public static SplCodesSection Get(string section)
		{
			lock ( _mutex ) {
				if ( _configSection == null )
					_configSection = ConfigurationManager.GetSection(section) as SplCodesSection;
			}
			return _configSection;
		}

		private Dictionary<string, SplCodeElement> _codes;
		public IDictionary<string, SplCodeElement> Codes
		{
			get
			{
				return _codes;
			}
		}

		private string _defaultCodeSystem;
		private string _defaultValueType;

		public object Create(object parent, object configContext, XmlNode section)
		{
			_codes = new Dictionary<string, SplCodeElement>();

			_defaultCodeSystem = section.Attributes["defaultCodeSystem"] != null ? section.Attributes["defaultCodeSystem"].Value : null;
			_defaultValueType = section.Attributes["defaultValueType"] != null ? section.Attributes["defaultValueType"].Value : null;

			foreach ( XmlNode child in section.ChildNodes ) {
				if ( XmlNodeType.Element == child.NodeType ) {
					SplCodeElement el = new SplCodeElement {
						Id = child.Attributes["id"].Value,
						Code = child.Attributes["code"] != null ? child.Attributes["code"].Value : null,
						CodeSystem = child.Attributes["codeSystem"] != null ? child.Attributes["codeSystem"].Value : _defaultCodeSystem,
						DisplayName = child.Attributes["displayName"] != null ? child.Attributes["displayName"].Value : null,
						Name = child.Attributes["name"] != null ? child.Attributes["name"].Value : null,
						MediaType = child.Attributes["mediaType"] != null ? child.Attributes["mediaType"].Value : null,
						ValueType = child.Attributes["valueType"] != null ? child.Attributes["valueType"].Value : _defaultValueType,
						Description = !String.IsNullOrWhiteSpace(child.InnerText) ? child.InnerText : null,
					};
                    if (!_codes.ContainsKey(el.Id))
                        _codes.Add(el.Id, el);
                  

                }
            }

			return this;
		}
	}

	public class SplCodeElement : ConfigurationElement
	{
		/// <summary>
		/// Internal code to retrieve infor from config/resources
		/// </summary>
		public string Id { get; set; }

		/// <summary>
		/// SPL code
		/// </summary>
		public string Code { get; set; }

		/// <summary>
		/// SPL code system
		/// </summary>
		public string CodeSystem { get; set; }

		/// <summary>
		/// Display name to be shown in SPL
		/// </summary>
		public string DisplayName { get; set; }

		/// <summary>
		/// Elaborate description of an entity
		/// </summary>
		public string Description { get; set; }

		/// <summary>
		/// In some cases a name to be used in SPL
		/// </summary>
		public string Name { get; set; }

		/// <summary>
		/// In characteristics - media type
		/// </summary>
		public string MediaType { get; set; }

		/// <summary>
		/// In characteristics - xsi:type
		/// https://www.corepointhealth.com/resource-center/hl7-resources/hl7-data-types
		/// </summary>
		public string ValueType { get; set; }
	}

}
