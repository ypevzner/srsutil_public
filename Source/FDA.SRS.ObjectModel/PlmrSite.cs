using System;
using System.Xml.Linq;

namespace FDA.SRS.ObjectModel
{
	public class PlmrSite : SplObject, IUniquelyIdentifiable
	{
		/// <summary>
		/// AA sequence
		/// </summary>
		public Chain Chain { get; set; }

		/// <summary>
		/// Position on Subunit
		/// </summary>
		public int Position { get; set; }

		// TODO: Not sure ProxPosition belongs here
		//begin YB: position on proximal moiety
		/// <summary>
		/// Reference to a connector on a Linker which is attached to this site. This is redundant and is used to cover Fragment's connectors renumbering.
		/// </summary>
		//public Fragment.Connector ConnectorRef { get; set; }
        public SRU.Connector ConnectorRef { get; set; }
        //end YB: position on proximal moiety


        public PlmrSite(SplObject rootObject, string type)
			: base(rootObject, type)
		{

		}

		/// <summary>
		/// AA symbol that corresponds to this Site
		/// </summary>

		public override string ToString()
		{
			return String.Format("{0}_{1}", Chain, Position);
		}

        /// <summary>
		/// 0-indexed
		/// </summary>
        public Tuple<int,int> ToTuple() {
            return Tuple.Create(Chain.Ordinal-1,Position);
        }

		public string UID
		{
			get { return ToString(); }
		}

		public override XElement SPL
		{
			get
			{
				var bond =
					new XElement(xmlns.spl + "bond",
						new XElement(xmlns.spl + "code", new XAttribute("code", Code ?? ""), new XAttribute("codeSystem", CodeSystem ?? ""), new XAttribute("displayName", DisplayName ?? "")));

				//begin YB:position on proximal moiety 
				if ( ConnectorRef != null )
					bond.Add(new XElement(xmlns.spl + "positionNumber", new XAttribute("value", ConnectorRef.Id + 1)));
                //end YB:position on proximal moiety 


                bond.Add(
                    new XElement(xmlns.spl + "positionNumber", new XAttribute("value", (Position == 0 ? "UNK" : Position.ToString()))));
                if (Chain != null) {
                    bond.Add(new XElement(xmlns.spl + "distalMoiety",
                            new XElement(xmlns.spl + "id", new XAttribute("extension", Chain.Id), new XAttribute("root", RootObject.DocId))));
                }
				return bond;
			}
		}
	}
}
