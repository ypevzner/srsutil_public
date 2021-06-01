using System;
using System.Xml.Linq;

namespace FDA.SRS.ObjectModel
{
	public class SplReferenceDocument : SplObject, IUniquelyIdentifiable
	{
		public string Title { get; set; }
		public string Bibliographic { get; set; }

		public SplReferenceDocument(SplObject rootObject)
			: base(rootObject, null)
		{

		}

		public override XElement SPL
		{
			get
			{
				return
					new XElement(xmlns.spl + "subjectOf",
						new XElement(xmlns.spl + "document",
							new XElement(xmlns.spl + "id",
								new XAttribute("extension", Id ?? ""),
								new XAttribute("root", RootObject.Id)
							),
							new XElement(xmlns.spl + "title", Title ?? ""),
							new XElement(xmlns.spl + "bibliographicDesignationText", Bibliographic ?? "")
						)
					);
			}
		}

		public string UID
		{
			get { throw new NotImplementedException(); }
		}
	}
}
