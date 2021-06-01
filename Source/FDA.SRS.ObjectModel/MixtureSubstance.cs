using FDA.SRS.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace FDA.SRS.ObjectModel
{
	public enum OptAct
	{
		levorotatory,
		dextrorotatory,
		nonrotatory
	}

	public class MixtureSubstance : SplObject
	{
		public MixtureType MixType { set; get; }

		public List<Moiety> Moieties { set; get; } = new List<Moiety>();

		public string SubstanceId { set; get; }

		public string UNII { set; get; }

		public string NamesXml { set; get; }

		public string PrimaryName { set; get; }

		public OptAct OptActivity { set; get; }

        //Added for Ticket 349 : MIX_DESC_PART1
        public string RefApprovalId { set; get; }
        public string RefPName { set; get; }
        public Boolean SourceMaterialExists = false;

        public MixtureSubstance(SplObject rootObject)
            : base(rootObject, null)
        {
            MixType = MixtureType.None;
            OptActivity = OptAct.nonrotatory;
        }

        //Added for Ticket 349
        public void MixSubstanceSPLCode(String id)
		{
            var config = SplCodesSection.Get("SPL.Codes");
            if (!String.IsNullOrWhiteSpace(id) && config.Codes.ContainsKey(id))
            {
                SplCodeElement el = config.Codes[id];
                Code = el.Code;
                CodeSystem = el.CodeSystem;
                DisplayName = el.DisplayName;
                Id = id;
            }
        }
        ////

        /// <param name="substance"></param>

		public void AddMoiety(MixtureSubstance substance)
		{
			if ( substance.Moieties.Count() == 0 )
				return;
			if ( substance.Moieties.Count() == 1 ) {
				substance.Moieties.First().UndefinedAmount = true;
                substance.Moieties.First().ParentSubstanceMixType = this.MixType;
                this.Moieties.Add(substance.Moieties.First());
			}
			else {
				if ( substance.Moieties.First().UndefinedAmount == true )
					this.Moieties.AddRange(substance.Moieties);
				else
					this.Moieties.Add(new Moiety() { UndefinedAmount = true, Submoieties = substance.Moieties, ParentSubstanceMixType = this.MixType });
			}
		}

        public void AddMoiety(MixtureSubstance substance,bool is_representative_component)
        {
            if (substance.Moieties.Count() == 0)
                return;
            if (substance.Moieties.Count() == 1)
            {
                substance.Moieties.First().UndefinedAmount = true;
                substance.Moieties.First().ParentSubstanceMixType = this.MixType;
                if (is_representative_component)
                {
                    substance.Moieties.First().RepresentativeStructure = true;
                }
                this.Moieties.Add(substance.Moieties.First());
                
            }
            else
            {
                if (substance.Moieties.First().UndefinedAmount == true)
                    this.Moieties.AddRange(substance.Moieties);
                else
                    this.Moieties.Add(new Moiety() { UndefinedAmount = true, Submoieties = substance.Moieties, RepresentativeStructure = is_representative_component, ParentSubstanceMixType = this.MixType });
            }
        }

        public override XElement SPL
		{
			get { throw new NotImplementedException(); }
		}
	}
}
