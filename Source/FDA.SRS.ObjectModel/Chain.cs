using FDA.SRS.Utils;
using System;
using System.Threading;
using System.Xml.Linq;
using System.Collections.Generic;
using System.Linq;

namespace FDA.SRS.ObjectModel
{
    //Polymer chain
    public class Chain : SplObject, IUniquelyIdentifiable
	{
		//public IEnumerable<SRU> SRUs { get; set; }

		public List<SRU> SRUs { get; set; } = new List<SRU>();


		private string _id;
		public override string Id
		{
			set { _id = value; }
			get {
				if ( _id == null )
					_id = String.Format("P{0}", Ordinal);
				return _id;
			}
		}

		public int Ordinal { get; set; }

		public int sru_fragment_id { get; set; }
        public bool head_present { get; set; }
        public bool tail_present { get; set; }

        public Chain(SplObject rootObject)
			: base(rootObject, "POLYMER")
		{
			Ordinal = Interlocked.Increment(ref Counters.SubunitCounter);
		}

		public override string ToString()
		{
			return Id;
		}

		public string UID
		{
			get { return String.Join("|", SRUs.OrderBy(s => s.ToString()).Select(s => s.UID)) ; }
		}

		public override XElement SPL
		{
			get
			{
				var partMoiety =
					new XElement(xmlns.spl + "partMoiety",
						new XElement(xmlns.spl + "id", new XAttribute("extension", Id), new XAttribute("root", RootObject.DocId))
					);
                if (SRUs != null)
                {
                    foreach (SRU sru in SRUs)
                    {
                        partMoiety.Add(sru.SPL2);

                    }
                }
                var moiety =
					new XElement(xmlns.spl + "moiety",
						// new XComment("Subunit"),
						new XElement(xmlns.spl + "code", new XAttribute("code", Code ?? ""), new XAttribute("codeSystem", CodeSystem ?? ""), new XAttribute("displayName", DisplayName ?? "")),
						new Amount(1, "mol","mol").SPL,
						partMoiety
					);

				return moiety;
			}
		}
	}
}
