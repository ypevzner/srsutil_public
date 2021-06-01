using System.Xml.Linq;

namespace FDA.SRS.ObjectModel
{
    public class SplHash : SplObject
    {
        private string _hash;

        public SplHash(string hash)
            : base(null, "hash")
        {
            _hash = hash;
        }

        public override XElement SPL
        {
            get
            {
                return new XElement(xmlns.spl + "asEquivalentSubstance",
                    new XElement(xmlns.spl + "definingSubstance",
                        new XElement(xmlns.spl + "code",
                            new XAttribute("code", _hash ?? ""),
                            new XAttribute("codeSystem", CodeSystem ?? "")
                        )
                    )
                );
            }
        }
    }
}
