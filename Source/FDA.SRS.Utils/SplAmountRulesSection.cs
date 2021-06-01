using System;
using System.Collections.Generic;
using System.Configuration;
using System.Xml;

// http://msdn.microsoft.com/en-us/library/2tw134k3(v=vs.100).aspx
// http://msdn.microsoft.com/en-us/library/ms228056(v=vs.100).aspx

namespace FDA.SRS.Utils
{
	public class SplAmountRulesSection : IConfigurationSectionHandler
	{
		private static object _mutex = new object();
		private static SplAmountRulesSection _configSection;
		public static SplAmountRulesSection Get(string section)
		{
			lock ( _mutex ) {
				if ( _configSection == null )
					_configSection = ConfigurationManager.GetSection(section) as SplAmountRulesSection;
			}
			return _configSection;
		}

		private Dictionary<string, AmountRulesElement> _codes;
		public IDictionary<string, AmountRulesElement> Codes
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
			_codes = new Dictionary<string, AmountRulesElement>();

			_defaultCodeSystem = section.Attributes["defaultCodeSystem"] != null ? section.Attributes["defaultCodeSystem"].Value : null;
			_defaultValueType = section.Attributes["defaultValueType"] != null ? section.Attributes["defaultValueType"].Value : null;

			foreach ( XmlNode child in section.ChildNodes ) {
				if ( XmlNodeType.Element == child.NodeType ) {
					AmountRulesElement el = new AmountRulesElement
					{
						Id = child.Attributes["id"].Value,
						SPLUnits = child.Attributes["SPLUnits"] != null ? child.Attributes["SPLUnits"].Value : "mol",
						DivideBy100 = bool.Parse(child.Attributes["DivideBy100"] != null ? child.Attributes["DivideBy100"].Value : "false"),
						SiteAdjust = bool.Parse(child.Attributes["SiteAdjust"] != null ? child.Attributes["SiteAdjust"].Value : "false"),
						Fail = bool.Parse(child.Attributes["Fail"] != null ? child.Attributes["Fail"].Value : "false")
					};
                    if (!_codes.ContainsKey(el.Id))
                        _codes.Add(el.Id, el);
                  

                }
            }

			return this;
		}
	}

	public class AmountRulesElement : ConfigurationElement
	{
		/// <summary>
		/// Internal code to retrieve infor from config/resources
		/// </summary>
		public virtual string Id { get; set; }

		// code fields
		public string SPLUnits { get; set; }
		public bool DivideBy100 { get; set; }
		public bool SiteAdjust { get; set; }
		public bool Fail { get; set; }
	}
    
}
