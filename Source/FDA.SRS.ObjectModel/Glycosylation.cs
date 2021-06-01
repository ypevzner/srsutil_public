using System;
using System.Threading;
using System.Xml.Linq;

namespace FDA.SRS.ObjectModel
{
	public class Glycosylation : SplObject, IUniquelyIdentifiable
	{
		private string _id;
		public override string Id
		{
			set { _id = value; }
			get
			{
				if ( _id == null )
					_id = String.Format("GLY{0}", Interlocked.Increment(ref Counters.GlycosylationCounter));
				return _id;
			}
		}

		// TODO: enum?
		public string GlycosylationType { get; set; }
		public ProteinAttachment Attachment { get; set; }
        //per SRS-361
        public Amount Amount
        {
            get;
            set;
        }
        public Glycosylation(SplObject rootObject, string type)
			: base(rootObject, "STRUCTURAL MODIFICATION")
		{
			GlycosylationType = type;
            Amount = new Amount();
            Amount.DenominatorUnit = "mol";
        }

		public string UID
		{
			get { return GlycosylationType + "-" + Attachment.UID + Amount.UID; }
		}

		public override XElement SPL
		{
			get
			{
				string id = String.Format("{0} TYPE GLYCAN", GlycosylationType);
				XElement xPartMoiety = new SplPartMoiety(RootObject, id) { Id = Id }.SPL;

				XElement xMoiety = new XElement(xmlns.spl + "moiety",
					new XElement(xmlns.spl + "code", new XAttribute("code", Code ?? ""), new XAttribute("codeSystem", CodeSystem ?? ""), new XAttribute("displayName", DisplayName ?? "")),
					Amount.SPL,
                    xPartMoiety
				);

				xPartMoiety.Add(Attachment.SPL);

				return xMoiety;
			}
		}
	}
}
