using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using FDA.SRS.Utils;

namespace FDA.SRS.ObjectModel
{
	public class Polymer : PolymerBase
	{
		public string PolymerClass { get; set; }
		public string PolymerGeometry { get; set; }
		public string CopolymerType { get; set; }

		public override string UID
		{
			get
			{
				return (
					String.Join("|", Modifications.Select(s => s.UID)) + "_" +
					String.Join("|", Fragments.Select(s => s.UID))
				).GetMD5String();
			}
		}

		public override IEnumerable<XElement> Subjects
		{
			get
			{
				// Main complex entity - identifiedSubstance
				XElement xIdentifiedSubstance2 =
					new XElement(xmlns.spl + "identifiedSubstance",
						new XElement(xmlns.spl + "code", new XAttribute("code", UNII ?? ""), new XAttribute("codeSystem", "2.16.840.1.113883.4.9"))
							// new XElement(xmlns.spl + "name", "unknown")
					);

				// Top level subject containing the main entity
				XElement xSubject =
					new XElement(xmlns.spl + "subject",
						new XElement(xmlns.spl + "identifiedSubstance",
							new XElement(xmlns.spl + "id", new XAttribute("extension", UNII), new XAttribute("root", "2.16.840.1.113883.4.9")),
							xIdentifiedSubstance2,
							new SplCharacteristic("hash", UID).SPL
						));

				if ( MolecularWeight != null )
					xSubject.Descendants().First().Add(new SplCharacteristic("mass-from-formula", MolecularWeight.Amount.UID).SPL);

				// xIdentifiedSubstance2.Add(new XComment("modifications start"));
				foreach ( var s in Modifications ) {
					if ( s is StructuralModificationGroup ) {
						foreach ( var x in ((StructuralModificationGroup)s).SPL2 )
							xIdentifiedSubstance2.Add(x);
					}
				}
				// xIdentifiedSubstance2.Add(new XComment("modifications end"));

				yield return xSubject;

				// Independent entities used/referenced in the main identifiedSubstance
				foreach ( var s in Fragments )
					yield return s.SPL;
			}
		}
	}
}
