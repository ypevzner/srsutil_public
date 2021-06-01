using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using System.Linq;
using FDA.SRS.Utils;

namespace FDA.SRS.ObjectModel
{
	public static class ValidatedValues
	{
		public static Dictionary<string, string> SequenceTypes = new Dictionary<string,string>();
		public static Dictionary<string, string> GlycosylationTypes = new Dictionary<string,string>();
		public static Dictionary<string, string> AgentModificationTypes = new Dictionary<string,string>();
		public static Dictionary<string, string> AgentModificationRoles = new Dictionary<string,string>();
		public static Dictionary<string, string> AmountTypes = new Dictionary<string,string>();
		public static Dictionary<string, string> Units = new Dictionary<string,string>();
		public static Dictionary<string, string> ExtentAmountUnits = new Dictionary<string, string>();
		public static Dictionary<string, string> StructureModificationTypes = new Dictionary<string,string>();
        public static Dictionary<string, string> NAStructureModificationTypes = new Dictionary<string, string>();
        public static Dictionary<string, string> MWTypes = new Dictionary<string,string>();
		public static Dictionary<string, string> MWMethods = new Dictionary<string,string>();
		public static Dictionary<string, string> LinkageTypes = new Dictionary<string, string>();

		private static Dictionary<string, string> loadConfigSection(this Dictionary<string, string> dict, string section)
		{
			dict.Clear();

			NameValueCollection values = (NameValueCollection)ConfigurationManager.GetSection(section);
			if ( values != null )
				values.Keys
					.Cast<string>()
					.ForAll(k => dict.Add(k, values[k]));

			return dict;
				
		}

		static ValidatedValues()
		{
			SequenceTypes.loadConfigSection("Validation/SequenceTypes");
			GlycosylationTypes.loadConfigSection("Validation/GlycosylationTypes");
			AgentModificationTypes.loadConfigSection("Validation/AgentModificationTypes");
			AgentModificationRoles.loadConfigSection("Validation/AgentModificationRoles");
			AmountTypes.loadConfigSection("Validation/AmountTypes");
			Units.loadConfigSection("Validation/Units");
			ExtentAmountUnits.loadConfigSection("Validation/ExtentAmountUnits");
			StructureModificationTypes.loadConfigSection("Validation/StructureModificationTypes");
            NAStructureModificationTypes.loadConfigSection("Validation/NAStructureModificationTypes");
            MWTypes.loadConfigSection("Validation/MWTypes");
			MWMethods.loadConfigSection("Validation/MWMethods");
			LinkageTypes.loadConfigSection("Validation/LinkageTypes");
		}
	}
}
