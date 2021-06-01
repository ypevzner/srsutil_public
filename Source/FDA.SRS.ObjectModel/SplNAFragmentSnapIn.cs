using System;
using System.Xml.Linq;

namespace FDA.SRS.ObjectModel
{
	class SplNAFragmentSnapIn : SplObject
	{
		private Tuple<int, int> Positions { get; set; }

		public SplNAFragmentSnapIn(SplObject rootObject, Tuple<int, int> positions)
			: base(rootObject, "Nucleotide connection points")
		{
			Positions = positions;
			Id = rootObject.Id;
		}

		public override XElement SPL
		{
			get
			{
				return
					new XElement(xmlns.spl + "moiety",
						// new XComment("SplFragmentSnapIn"),
						new XElement(xmlns.spl + "code",
							new XAttribute("code", Code ?? ""),
							new XAttribute("codeSystem", CodeSystem ?? ""),
							new XAttribute("displayName", DisplayName ?? "")
						),
						new XElement(xmlns.spl + "positionNumber",
                            Positions.Item1 == 0 ?
								new XAttribute("nullFlavor", "NA") : 
								new XAttribute("value", Positions.Item1)
						),
						new XElement(xmlns.spl + "positionNumber",
                            Positions.Item2 == 0 ?
								new XAttribute("nullFlavor", "NA") :
								new XAttribute("value", Positions.Item2)
                        ),
					new XElement(xmlns.spl + "partMoiety")
				);
			}
		}
	}
}
