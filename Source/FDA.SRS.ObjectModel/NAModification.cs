using FDA.SRS.Utils;

namespace FDA.SRS.ObjectModel
{
	public abstract class NAModification : SplObject, IUniquelyIdentifiable
	{
		public string ModificationType { get; set; }
		public string Role { get; set; }
		public Amount Amount { get; set; }

		public NAModification(SplObject rootObject, string type)
			: base(rootObject, type)
		{

		}

		public virtual string UID
		{
			get {
                return (
                    ModificationType +
                    Role +
                    Amount.UID
                ).GetMD5String();
            }
        }
        public virtual string DefiningParts {
            get {
                string amount = "";
                if (Amount != null) {
                    amount = Amount.UID;
                }
                return (
                    ModificationType +
                    Role +
                    amount
                );
            }
        }
    }
}
