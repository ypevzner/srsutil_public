using FDA.SRS.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Xml.Linq;

namespace FDA.SRS.ObjectModel
{
	public class StructuralModificationGroup : ProteinModification
	{
		/// <summary>
		/// Residue to be modified (e.g. cysteine, etc). This is redundant and ResidueSites property is a preferred way to specify modifications.
		/// </summary>
		public string Residue { get; set; }

        /// <summary>
        /// Subunits sites to be modified
        /// </summary>
        public List<ProteinSite> ResidueSites { get; set; } = new List<ProteinSite>();

		/// <summary>
		/// Structural modification described in one or another way
		/// </summary>
		public StructuralModification Modification { get; set; }
		
		public override string UID
		{
			get
			{
				return
					( String.IsNullOrWhiteSpace(Residue) ? "" : Residue ) +
					String.Join("|", ResidueSites) +
					Modification.UID;
			}
		}

        public override string DefiningParts {
            get {
                return (
                    UID
                );
            }
        }

        public override XElement SPL
		{
			get { throw new NotImplementedException(); }
		}

		private string _id;
		public override string Id
		{
			set { _id = value; }
			get
			{
				if ( _id != null )
					return _id;

				return String.Format("M{0}", Interlocked.Increment(ref Counters.ModificationCounter));
			}
		}

		public IEnumerable<XElement> SPL2
		{
			get
			{
				foreach ( var s in ResidueSites ) {
					var xMoiety = 
						new XElement(xmlns.spl + "moiety",
							// new XComment("StructuralModificationGroup"),
							new XElement(xmlns.spl + "code", new XAttribute("code", Code ?? ""), new XAttribute("codeSystem", CodeSystem ?? ""), new XAttribute("displayName", DisplayName ?? ""))
						);

					//YP SRS-413 Removing "PROBABILITY" condition to ensure amount always shows up
					//if ( String.Equals(Modification.Amount.SrsAmountType, "PROBABILITY", StringComparison.InvariantCultureIgnoreCase) )
					xMoiety.Add(Modification.Amount.SPL);
                    
					var xPartMoiety = new XElement(xmlns.spl + "partMoiety");

					if ( !SplOptions.ExportOptions.Features.Has("no-id-extension-root") )
						xPartMoiety.Add(new XElement(xmlns.spl + "id", new XAttribute("extension", Id ?? ""), new XAttribute("root", RootObject.DocId)));

					if (Modification.ModificationType == "AMINO ACID REMOVAL")
					{
						xPartMoiety.Add(s.SPL);
					}
                    else
                    {
						xPartMoiety.Add(
						new XElement(xmlns.spl + "code", new XAttribute("code", Modification.Fragment.Id ?? ""), new XAttribute("codeSystem", RootObject.DocId == null ? "" : RootObject.DocId.ToString())),
						s.SPL);
					}
					xMoiety.Add(xPartMoiety);
					yield return xMoiety;
				};
			}
		}

		public StructuralModificationGroup(SplObject rootObject)
			: base(rootObject, "STRUCTURAL MODIFICATION")
		{

		}
	}

	public class StructuralModification : ProteinModification
	{
		public string UNII { get; set; }
		public string InChI { get; set; }
		public string Mol { get; set; }

		public string Extent { get; set; }
		public Fragment Fragment { get; set; }
        public bool isDeletion = false;

		public override string ToString()
		{
			return String.Format("<{0}> {1}", UNII, Fragment);
		}

		public override string UID
		{
			get { return Fragment.UID.GetMD5String()+Amount.UID; }
		}

		public override XElement SPL
		{
			get {
				XElement xIdentifiedSubstance =
                        new XElement(xmlns.spl + " identifiedSubstance",
						    new XElement(xmlns.spl + "id", new XAttribute("extension", Fragment.Id ?? ""), new XAttribute("root", RootObject.DocId)),
						    Fragment.SPL
                    );

				return new XElement(xmlns.spl + "subject", xIdentifiedSubstance);
			}
		}

		public StructuralModification(SplObject rootObject)
			: base(rootObject, "STRUCTURAL MODIFICATION")
		{

		}
	}
}
