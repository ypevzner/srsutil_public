using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FDA.SRS.Utils;
using System.Xml.Linq;


namespace FDA.SRS.Utils
{
    //Class Added for Ticket 312:Structurally Diverse
    public class PlmrTerms
    {

        // id fields
        public virtual string Id { get; set; }
        
        public StructurallyDiverseTerms RootObject { get; set; }

        public Guid? DocId { get; set; }
        public Guid? SetId { get; set; }

        // code fields
        public string Code { get; set; }
        public string CodeSystem { get; set; }
        public virtual string DisplayName { get; set; }

        public PlmrTerms()
        {

        }

        public bool isReal() {
            return Code != null;
        }

        public PlmrTerms(string id)
        {
           
            id = id.ToUpper();
            var config = SplCodesSection.Get("SPL.Codes");
            if (!String.IsNullOrWhiteSpace(id) && config.Codes.ContainsKey(id))
            {
                SplCodeElement el = config.Codes[id];
                Code = el.Code;
                CodeSystem = el.CodeSystem;
                DisplayName = el.DisplayName;
            }
        
            
        }
    }
}
