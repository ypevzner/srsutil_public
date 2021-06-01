using System.Xml.Linq;

namespace FDA.SRS.ObjectModel
{
	public class ProteinAttachment : SplObject, IUniquelyIdentifiable
	{
		public ProteinSite Site { get; private set; }
		public string AttachmentType { get; private set; }
		public Glycan Glycan { get; private set; }

		public ProteinAttachment(SplObject rootObject, string type, string element, Subunit su, int pos)
			: base(rootObject, type)
		{
			if ( pos < 0 || pos >= su.Sequence.Length )
				// TraceUtils.WriteUNIITrace(TraceEventType.Error, null, null, "Protein STRUCTURAL ATTACHMENT POINT problem - out of sequence range");
				throw new SrsException("seq_ref", "Protein STRUCTURAL ATTACHMENT POINT problem - out of sequence range");

			AttachmentType = element;
			Site = new ProteinSite(rootObject, "STRUCTURAL ATTACHMENT POINT") { Subunit = su, Position = pos };
		}

		public override XElement SPL
		{
			get
			{
				return
					new XElement(xmlns.spl + "bond",
						new XElement(xmlns.spl + "code", new XAttribute("code", Code ?? ""), new XAttribute("codeSystem", CodeSystem ?? ""), new XAttribute("displayName", DisplayName ?? "")),
						new XElement(xmlns.spl + "positionNumber", new XAttribute("value", Site.Position + 1)),
						new XElement(xmlns.spl + "distalMoiety",
							new XElement(xmlns.spl + "id", new XAttribute("extension", Site.Subunit.ToString()), new XAttribute("root", RootObject.Id))));
			}
		}

		public string UID
		{
			get { return Site.UID + "-" + AttachmentType; }
		}
	}
}
