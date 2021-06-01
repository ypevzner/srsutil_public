using FDA.SRS.Utils;
using System;
using System.Threading;
using System.Xml.Linq;

namespace FDA.SRS.ObjectModel
{
	public class Subunit : SplObject, IUniquelyIdentifiable
	{
		private string _id;
		public override string Id
		{
			set { _id = value; }
			get {
				if ( _id == null )
					_id = String.Format("SU{0}", Ordinal);
				return _id;
			}
		}

		public int Ordinal { get; set; }

		// Amino-acid sequence
		public Sequence Sequence { get; set; }

        //per SRS-357
        public Amount SubunitAmount { get; set; }

        //YP SRS-383
        //this would indicate if the subunit is the only moiety in this substance (no other subunits or links etc)
        //unii is needed to display in the <code> element of the SPL partMoiety if this is the only moiety
        public bool isTheOnlyMoiety = false;
        public string parent_unii = "";

        public Subunit(SplObject rootObject)
			: base(rootObject, "PROTEIN SUBUNIT")
		{
			Ordinal = Interlocked.Increment(ref Counters.SubunitCounter);
            SubunitAmount = new Amount(1, "mol", "mol");
        }

		public override string ToString()
		{
			return Id;
		}

		public string UID
		{
			get { return Sequence.ToString().GetMD5String() + SubunitAmount.UID; }
		}

		public override XElement SPL
		{
			get
			{
                //YP Commenting this part for proteins per Yulia... NAs still need this
                //var partMoiety =
                //    this.isTheOnlyMoiety ? new XElement(xmlns.spl + "partMoiety",
                //        new XElement(xmlns.spl + "code", new XAttribute("code", parent_unii ?? ""), new XAttribute("codeSystem", "2.16.840.1.113883.4.9")),
                //        new XElement(xmlns.spl + "id", new XAttribute("extension", Id), new XAttribute("root", RootObject.DocId))
                //        ) : new XElement(xmlns.spl + "partMoiety",
                //        new XElement(xmlns.spl + "id", new XAttribute("extension", Id), new XAttribute("root", RootObject.DocId))
                //        );

                var partMoiety =
                new XElement(xmlns.spl + "partMoiety",
                    new XElement(xmlns.spl + "id", new XAttribute("extension", Id), new XAttribute("root", RootObject.DocId))
                );

                if ( SplOptions.ExportOptions.Features.Has("separate-sequence-definition") )
					partMoiety.Add(new XElement(xmlns.spl + "code", new XAttribute("code", Sequence.Id ?? ""), new XAttribute("codeSystem", RootObject.DocId == null ? "" : RootObject.DocId.ToString())));

				var moiety =
					new XElement(xmlns.spl + "moiety",
						// new XComment("Subunit"),
						new XElement(xmlns.spl + "code", new XAttribute("code", Code ?? ""), new XAttribute("codeSystem", CodeSystem ?? ""), new XAttribute("displayName", DisplayName ?? "")),
						//new Amount(1, "mol","mol").SPL,
                        SubunitAmount.SPL,
						partMoiety
					);

				if ( !SplOptions.ExportOptions.Features.Has("separate-sequence-definition") )
					moiety.Add(Sequence.SequenceSpl);

				return moiety;
			}
		}
	}
}
