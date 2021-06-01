using FDA.SRS.Utils;
using System;
using System.Xml.Linq;
using System.Collections.Generic;

namespace FDA.SRS.ObjectModel
{
	public class AgentModification : ProteinModification
	{
		public string Agent { get; set; }
		public string AgentId { get; set; }
		public string Process { get; set; }
        //Added for Ticket 271 Agent Modification
        public string AgentSubLinkID { get; set; }
        public string RelationTypeLinkID { get; set; }
        public string RelationTypeApprovalID { get; set; }
        public static int SeqCount { get; set; } = 1;
        public string AgentCode { get; set; }
        public string AgentDisplayName { get; set; }
        public string AgentHashCode { get; set; }
        //End 271

        public SDFUtil.NewMolecule Fragment { get; set; }


		public override string UID
		{
			get
			{
				return (
					base.UID +
					( String.IsNullOrWhiteSpace(Agent) ? "" : Agent ) +
					( String.IsNullOrWhiteSpace(AgentId) ? "" : AgentId ) +
					( Fragment == null ? "" : Fragment.InChIKey ) +
					( Amount == null ? "" : Amount.UID )
					).GetMD5String();
			}
		}

        public override string DefiningParts {
            get {
                return (
                    base.DefiningParts +
                    (String.IsNullOrWhiteSpace(Agent) ? "" : Agent) +
                    (String.IsNullOrWhiteSpace(AgentId) ? "" : AgentId) +
                    (Fragment == null ? "" : Fragment.InChIKey) +
                    (Amount == null ? "" : Amount.UID)
                );
            }
        }

        public override XElement SPL
		{
			get
			{
				throw new NotImplementedException("AgentModification.SPL");
			}
		}

        //Added for Ticket 271:Agent Modification
        public IEnumerable<XElement> SPL1
        {
            get
            {
                //Added for Ticket 375:Add code to all top layer <derivationProcess> elements
               /* var xCode = //new XElement(xmlns.spl + "code",
                           new XElement(xmlns.spl + "code", new XAttribute("code", "C25572"),
                           new XAttribute("displayName", "Modification"),
                           new XAttribute("codeSystem", "2.16.840.1.113883.3.26.1.1"));
                 */          

                var xInteractorOuter = new XElement(xmlns.spl + "interactor",
                  new XAttribute("typeCode", "CSM"));
                var xIdentifiedSubstanceOuter = new XElement(xmlns.spl + "identifiedSubstance",
                    new XElement(xmlns.spl + "id", new XAttribute("extension", RelationTypeApprovalID ?? ""),
                    new XAttribute("root",  "2.16.840.1.113883.4.9")));
                var xIdentifiedSubstanceInternalOuter = new XElement(xmlns.spl + "identifiedSubstance",
                   new XElement(xmlns.spl + "code", new XAttribute("code", RelationTypeApprovalID ?? ""),
                   new XAttribute("codeSystem",  "2.16.840.1.113883.4.9")));
               // var xAsEquivalentSubstanceOuter = new XElement(xmlns.spl + "asEquivalentSubstance");
                var xDefiningSubstanceOuter = new XElement(xmlns.spl + "definingSubstance",
                 new XElement(xmlns.spl + "code", new XAttribute("code", AgentHashCode ?? ""),
                 new XAttribute("codeSystem", "2.16.840.1.113883.3.2705")));

                //xCode.Add(xInteractorOuter); //Ticket 375
                xInteractorOuter.Add(xIdentifiedSubstanceOuter);
                //xCode.Add(xIdentifiedSubstanceOuter);
                xIdentifiedSubstanceOuter.Add(xIdentifiedSubstanceInternalOuter);
               //xIdentifiedSubstanceInternalOuter.Add(xAsEquivalentSubstanceOuter);
                //xAsEquivalentSubstanceOuter.Add(xDefiningSubstanceOuter);
                //xIdentifiedSubstanceInternalOuter.Add(xDefiningSubstanceOuter);

                //Modified for Ticket 375
                yield return xInteractorOuter;
                //yield return xCode;
            }
        }
        //End of 271

        public IEnumerable<XElement> SPL2
        {
            get
            {
                var xComponent = new XElement(xmlns.spl + "component");
                var xSequenceNumber = new XElement(xmlns.spl + "sequenceNumber",
                    new XAttribute("value", SeqCount));
                var xDerivationProcess = new XElement(xmlns.spl + "derivationProcess",
                    new XElement(xmlns.spl + "code", new XAttribute("code", AgentCode ?? ""),
                    new XAttribute("codeSystem", CodeSystem ?? ""), //Modified for Ticket SRS-354,355
                    new XAttribute("displayName", AgentDisplayName ?? "")));
                var xInteractor = new XElement(xmlns.spl + "interactor",
                    new XAttribute("typeCode", "CSM"));
                var xIdentifiedSubstance = new XElement(xmlns.spl + "identifiedSubstance",
                    new XElement(xmlns.spl + "id", new XAttribute("extension", AgentId ?? ""),
                    new XAttribute("root", "2.16.840.1.113883.4.9")));
                var xIdentifiedSubstanceInternal = new XElement(xmlns.spl + "identifiedSubstance",
                    new XElement(xmlns.spl + "code", new XAttribute("code", AgentId ?? ""),
                    new XAttribute("codeSystem", "2.16.840.1.113883.4.9")));
                var xAsEquivalentSubstance = new XElement(xmlns.spl + "asEquivalentSubstance");
                var xDefiningSubstance = new XElement(xmlns.spl + "definingSubstance",
                   new XElement(xmlns.spl + "code", new XAttribute("code", AgentHashCode ?? ""),
                   new XAttribute("codeSystem", "2.16.840.1.113883.3.2705")));

                xComponent.Add(xSequenceNumber);
                xComponent.Add(xDerivationProcess);
                xDerivationProcess.Add(xInteractor);
                xInteractor.Add(xIdentifiedSubstance);
                xIdentifiedSubstance.Add(xIdentifiedSubstanceInternal);
                xIdentifiedSubstanceInternal.Add(xAsEquivalentSubstance);
                xAsEquivalentSubstance.Add(xDefiningSubstance);
                //xIdentifiedSubstanceInternal.Add(xDefiningSubstance);

                yield return xComponent;
            }
        }
        //END of 271

        public override string DisplayName	// TODO: collides with SplObject's DisplayName
		{
			get
			{
				return String.Join(" ",
					String.IsNullOrWhiteSpace(ModificationType) ? "" : ModificationType,
					String.IsNullOrWhiteSpace(Role) ? "" : Role,
					String.IsNullOrWhiteSpace(Process) ? "" : Process
					).Trim();
			}
		}

		public AgentModification(SplObject rootObject)
			: base(rootObject, "agent-modification")
		{

		}
	}
}
