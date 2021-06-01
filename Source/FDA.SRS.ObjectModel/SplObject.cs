using FDA.SRS.Utils;
using System;
using System.Xml.Linq;

namespace FDA.SRS.ObjectModel
{
	[Serializable]
	public abstract class SplObject : ISplable
	{
		// id fields
		public virtual string Id { get; set; }
		
		public SplObject RootObject { get; set; }

		public Guid? DocId { get; set; }
		public Guid? SetId { get; set; }

		// code fields
		public string Code { get; set; }
		public string CodeSystem { get; set; }
		public virtual string DisplayName { get; set; }

		// name field
		public string Name { get; set; }

		public string MediaType { get; set; }

		public string ValueType { get; set; }

		public abstract XElement SPL { get; }

		public SplObject()
		{

		}

		public SplObject(SplObject rootObject, string id)
		{
			RootObject = rootObject;

                var config = SplCodesSection.Get("SPL.Codes");
                if (!String.IsNullOrWhiteSpace(id) && config.Codes.ContainsKey(id))
                {
                    SplCodeElement el = config.Codes[id];
                    Code = el.Code;
                    CodeSystem = el.CodeSystem;
                    DisplayName = el.DisplayName;
                    Name = el.Name;
                    MediaType = el.MediaType;
                    ValueType = el.ValueType;
                }
          
		}

        











}
}
