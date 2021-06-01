using System;
using System.Xml.Linq;
using System.Collections.Generic;

namespace FDA.SRS.ObjectModel
{
    class SplNAMultiFragmentSnapIn : SplObject
	{
		private List<NAFragment.ComponentConnector> ComponentPositions { get; set; }
        private List<NAFragment.Connector> Positions { get; set; }

        public SplNAMultiFragmentSnapIn(SplObject rootObject, List<NAFragment.ComponentConnector> positions)
			: base(rootObject, "Nucleotide connection points")
		{
			ComponentPositions = positions;
			Id = rootObject.Id;
		}
        public SplNAMultiFragmentSnapIn(SplObject rootObject, List<NAFragment.Connector> positions)
            : base(rootObject, "Nucleotide connection points")
        {
            Positions = positions;
            Id = rootObject.Id;
        }

        public override XElement SPL
		{
			get
			{
                List<XElement> connectors = new List<XElement>();

				//return new XElement(xmlns.spl + "moiety",connectors);
				//YP SRS-394
				//foreach (NAFragment.Connector c in Positions)
				foreach (NAFragment.ComponentConnector c in ComponentPositions)
				{
                    connectors.Add(new XElement(xmlns.spl + "positionNumber",
                                c.Snip.Item1 == 0 ?
                                    new XAttribute("nullFlavor", "NA") :
                                    new XAttribute("value", c.Snip.Item1)
                                    )
                                   );
                }
				return
					new XElement(xmlns.spl + "moiety",
						// new XComment("SplFragmentSnapIn"),
						new XElement(xmlns.spl + "code",
							new XAttribute("code", Code ?? ""),
							new XAttribute("codeSystem", CodeSystem ?? ""),
							new XAttribute("displayName", DisplayName ?? "")
						),
						connectors,
					new XElement(xmlns.spl + "partMoiety")
				);
			
            }
            
		}
	}
}
