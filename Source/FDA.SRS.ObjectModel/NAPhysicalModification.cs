using System.Xml.Linq;

namespace FDA.SRS.ObjectModel
{
	public class NAPhysicalModification : NAModification
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

		public NAPhysicalModification(SplObject rootObject)
			: base(rootObject, "physical-modification")
		{

		}
	}
}
