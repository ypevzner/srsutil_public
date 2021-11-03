using FDA.SRS.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Xml.Linq;
using System.Linq;
namespace FDA.SRS.ObjectModel
{
	public class PlmrStructuralModificationGroup : PlmrModification
	{

        /// <summary>
        /// Subunits sites to be modified
        /// </summary>
        public List<PlmrSite> PolymerSites { get; set; } = new List<PlmrSite>();

		/// <summary>
		/// Structural modification described in one or another way
		/// </summary>
		public PlmrStructuralModification Modification { get; set; }
		
		public override string UID
		{
			get
			{
				return
					String.Join("|", PolymerSites) +
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
				foreach (int fragment_id in Modification.Fragment.fragment_ids)
				{
					//foreach (int connected_fragment_id in Modification.Fragment.plmr_unit.getConnectedFragmentIDs(fragment_id))
					//{
					var xMoiety =
						new XElement(xmlns.spl + "moiety",
							// new XComment("StructuralModificationGroup"),
							new XElement(xmlns.spl + "code", new XAttribute("code", Code ?? ""), new XAttribute("codeSystem", CodeSystem ?? ""), new XAttribute("displayName", DisplayName ?? ""))
						);

					//if ( String.Equals(Modification.Amount.SrsAmountType, "PROBABILITY", StringComparison.InvariantCultureIgnoreCase) )
					xMoiety.Add(Modification.Amount.SPL);

					var xPartMoiety = new XElement(xmlns.spl + "partMoiety");

					if (!SplOptions.ExportOptions.Features.Has("no-id-extension-root"))
						xPartMoiety.Add(new XElement(xmlns.spl + "id", new XAttribute("extension", Id ?? ""), new XAttribute("root", RootObject.DocId)));

					xPartMoiety.Add(
							new XElement(xmlns.spl + "code", new XAttribute("code", Modification.Fragment.Id ?? ""), new XAttribute("codeSystem", RootObject.DocId == null ? "" : RootObject.DocId.ToString())));

					foreach (var s in PolymerSites)
					{
						if (Modification.Fragment.plmr_unit.getConnectedFragmentIDs(fragment_id).Contains(s.Chain.sru_fragment_id))
						//if (Modification.Fragment.parent_chains.Contains(s.Chain))
						{ 
							xPartMoiety.Add(s.SPL);
						}
					};
					xMoiety.Add(xPartMoiety);
					yield return xMoiety;
					//}
				}
			}
		}

		public PlmrStructuralModificationGroup(SplObject rootObject)
			: base(rootObject, "STRUCTURAL MODIFICATION")
		{

		}
	}

	public class PlmrStructuralModification : PlmrModification
	{
		public string UNII { get; set; }
		public string InChI { get; set; }
		public string Mol { get; set; }

		public SRU Fragment { get; set; }

		public override string ToString()
		{
			return String.Format("<{0}> {1}", UNII, Fragment);
		}

		public override string UID
		{
			get { return Fragment.UID.GetMD5String() + Amount.UID; }
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

		public PlmrStructuralModification(SplObject rootObject)
			: base(rootObject, "STRUCTURAL MODIFICATION")
		{

		}
	}
}
