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
    public class AmountRules
    {

        // id fields
        public virtual string Id { get; set; }

        // code fields
        public string SPLUnits { get; set; }
        public bool DivideBy100 { get; set; }
        public bool SiteAdjust { get; set; }
        public bool Fail { get; set; }

        
        public AmountRules()
        {

        }

        public AmountRules(string id)
        {
           
            id = id.ToUpper();
            var config = SplAmountRulesSection.Get("SPL.AmountRules");
            if (!String.IsNullOrWhiteSpace(id) && config.Codes.ContainsKey(id))
            {
                AmountRulesElement el = config.Codes[id];
                SPLUnits = el.SPLUnits;
                Id = id;
                SPLUnits = el.SPLUnits;
				DivideBy100 = el.DivideBy100;
				SiteAdjust = el.SiteAdjust;
				Fail = el.Fail;   
            }
        
            
        }
    }
}
