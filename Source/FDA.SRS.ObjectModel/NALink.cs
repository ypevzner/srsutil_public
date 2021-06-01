using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Xml.Linq;

namespace FDA.SRS.ObjectModel
{
	/// <summary>
	/// This class represents protein link in general (each end attaching to/replacing one amino-acid sequence) and can be multi-center.
	/// </summary>
	public class NALink : SplObject, IUniquelyIdentifiable
	{
		private string _id;

        public bool IsImplicit = false;

		/// <summary>
		/// Link ID (in a form of "Mn" where n is a SPL-wide 0-based numbers succession)
		/// </summary>
		public override string Id
		{
			set {
				lock (this)
					_id = value;
			}
			get
			{
				lock ( this ) {
					if ( _id == null )
						_id = String.Format("M{0}", Interlocked.Increment(ref Counters.ModificationCounter));
					return _id;
				}
			}
		}

		/// <summary>
		/// Chemical (name) which is used as a Linker
		/// </summary>
		public string LinkType { get; set; }

        //per SRS-357
        public Amount Amount { get; set; }

        /// <summary>
        /// Protein sites where the Link is connected
        /// </summary>
        public List<NASite> Sites { get; set; }

		/// <summary>
		/// A fragment that constitutes the link
		/// </summary>
		public NAFragment Linker { get; set; }

        public int NextConnector { get; set; } = 0;

        public void IncrementConnector() {
            NextConnector++;
        }


        /// <summary>
        /// Returns true if Link is completely defined. E.g. cys-cys links are completely defiend at the time of creation while other links are defined in a piecewise manner across SRS XML.
        /// </summary>
        public bool IsCompletelyDefined
		{
			get {
				// TODO: add more rigorous validation
				return Linker != null && Sites.Count == Linker.Connectors.Count && Sites.All(s => s.ConnectorRef != null);
			}
		}

		public NALink(SplObject rootObject)
			: base(rootObject, "STRUCTURAL MODIFICATION")
		{
			Sites = new List<NASite>();
		}

		public string UID
		{
			get
			{
				return Linker == null ? "<cannot be generated>" : String.Join("-", Sites.OrderBy(s => s.ToString()).Select(s => s.UID)) + "-" + Linker.UID + Amount.UID;
			}
		}

		public override XElement SPL
		{
			get
			{
				if ( Linker == null )
					return new SplError("unknown_linker", String.Format("LinkerType: {0}", LinkType)).SPL;
				else {
					// TODO: insert LinkType instead of cys-cys, etc
					var xPartMoeity = new SplPartMoiety(RootObject, LinkType) { Id = Id, Code = Linker.Id, DisplayName = LinkType == "cys-cys" ? "Cysteine disulfide" : "Other link" }.SPL;

					foreach ( var s in Sites )
						xPartMoeity.Add(s.SPL);

					return
						new XElement(xmlns.spl + "moiety",
							new XElement(xmlns.spl + "code", new XAttribute("code", Code ?? ""), new XAttribute("codeSystem", CodeSystem ?? ""), new XAttribute("displayName", DisplayName ?? "")),
							xPartMoeity
						);
				}
			}
		}
	}
}
