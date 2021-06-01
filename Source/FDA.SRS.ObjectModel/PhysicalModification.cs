using System.Xml.Linq;

namespace FDA.SRS.ObjectModel
{
	public class PhysicalModification : ProteinModification
	{
		public override string UID
		{
			get { return base.UID; }
		}

		public override XElement SPL
		{
			get {
				return null;
			}
		}

		public PhysicalModification(SplObject rootObject)
			: base(rootObject, "physical-modification")
		{

		}
	}
}
