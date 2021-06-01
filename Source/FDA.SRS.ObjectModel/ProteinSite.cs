using System;
using System.Xml.Linq;

namespace FDA.SRS.ObjectModel
{
	public class ProteinSite : SplObject, IUniquelyIdentifiable
	{
		/// <summary>
		/// AA sequence
		/// </summary>
		public Subunit Subunit { get; set; }

		/// <summary>
		/// Position on Subunit
		/// </summary>
		public int Position { get; set; }

		// TODO: Not sure ProxPosition belongs here
		//begin YB: position on proximal moiety
		/// <summary>
		/// Reference to a connector on a Linker which is attached to this site. This is redundant and is used to cover Fragment's connectors renumbering.
		/// </summary>
		public Fragment.Connector ConnectorRef { get; set; }
		//end YB: position on proximal moiety
        

		public ProteinSite(SplObject rootObject, string type)
			: base(rootObject, type)
		{

		}

		/// <summary>
		/// AA symbol that corresponds to this Site
		/// </summary>
		public char Letter
		{
			get
			{
				return Subunit.Sequence.ToString()[Position];
			}
		}

		public override string ToString()
		{
			return String.Format("{0}_{1}", Subunit, Position);
		}

        /// <summary>
		/// 0-indexed
		/// </summary>
        public Tuple<int,int> ToTuple() {
            return Tuple.Create(Subunit.Ordinal-1,Position);
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
				if ( ConnectorRef != null && DisplayName.ToUpper() != "MONOMER DELETION SITE")
					bond.Add(new XElement(xmlns.spl + "positionNumber", new XAttribute("value", ConnectorRef.Id + 1)));
				//end YB:position on proximal moiety 

				bond.Add(
					new XElement(xmlns.spl + "positionNumber", new XAttribute("value", Position + 1)),
					new XElement(xmlns.spl + "distalMoiety",
						new XElement(xmlns.spl + "id", new XAttribute("extension", Subunit.Id), new XAttribute("root", RootObject.DocId))));

				return bond;
			}
		}
	}
}
