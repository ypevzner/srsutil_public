using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FDA.SRS.Utils;
using System.Xml.Linq;


namespace FDA.SRS.Utils
{
    //Class Added for Ticket 271:Agent Modification
    public class AgentTerms
    {

        // id fields
        public virtual string Id { get; set; }
        
        public AgentTerms RootObject { get; set; }

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

        //public abstract XElement SPL { get; }

        public AgentTerms()
        {

        }

        public AgentTerms(string id)
        {
            //RootObject = rootObject;

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
                Id = id;
            }
        }
    }
}
