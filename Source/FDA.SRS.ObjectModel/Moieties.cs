using com.epam.indigo;
using FDA.SRS.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using System.Diagnostics;



namespace FDA.SRS.ObjectModel
{
    public enum MixtureType
    {
        None,
        OneOf,
        AnyOf,
        AllOf
    }

    public enum StereoType
    {
        None = 0,
        Either = Indigo.EITHER,
        Abs = Indigo.ABS,
        Or = Indigo.OR,
        And = Indigo.AND,
    }

    public static class MoietyHelpers
    {
        public static string UID(this IEnumerable<Moiety> mlist)
        {
            return
                mlist.Count() == 0 ?
                    null :
                    String.Join("|", mlist.OrderBy(m => m.UID).Select(m => m.UID))
                ?.GetMD5String();
        }
    }

    public class Moiety : SplObject
    {
        public SDFUtil.NewMolecule Molecule { get; set; }
        public string DecodedStereo { get; set; }
        public string SpecialStereo { get; set; }
        public bool UndefinedAmount { get; set; }
        public bool RepresentativeStructure { get; set; }
        public MixtureType ParentSubstanceMixType { get; set; }
        public List<Moiety> Submoieties { get; set; } = new List<Moiety>();
        public bool IsNonStoichiometric = false;
        //YP change to create null MoietyAmount with numerator null if MoietyAmount isn't set explicitly
        //public Amount MoietyAmount { get; set; } = new Amount(1, null, null);
        public Amount MoietyAmount { get; set; } = new Amount(null, null, null);


        //This isn't right. This says it's a mixture component if it has sub-elements
        //But it's only a mixture if its PARENT has sub elements

        public bool IsMixture
        {
            get
            {
                return MixtureCount > 1 || (Submoieties.Count > 1) || (RepresentativeStructure == true);
            }
        }

        public bool isParentUndefined
        {
            get; set;
        } = false;


        //This is a count of the CHILDREN mixture pieces
        public int MixtureCount { get; set; }

        //Gets the count of elements in parent mixture (or 1 if no parent)
        public int ParentMixtureCount { get; set; } = 1;

        //Is part of a mixture
        public bool MixtureComponent
        {
            get
            {
                return (ParentMixtureCount > 1 || RepresentativeStructure);
            }
        }

        public Moiety()
            : base(null, "")
        {
        }

        public string MoietyUNII
        {
            set;
            get;
        }

        public string Mol
        {
            get { return Molecule == null ? null : Molecule.Mol.Replace("-INDIGO-", "-FDASRS-"); }
        }

        public string InChI
        {
            get
            {
                if (Molecule != null)
                {
                    //     Console.WriteLine("Doing Inchi" + Molecule.Mol.GetMD5String());
                }

                return Molecule == null ? null : Molecule.InChI;

            }
        }

        public string Stereo
        {
            get
            {
                if (DecodedStereo == null)
                {
                    return SpecialStereo == null ? null : decodeStereoType(SpecialStereo);
                }
                else
                {
                    return DecodedStereo;
                }
            }
        }

        private Amount Amount
        {
            get
            {
                //TODO:
                //This is where the denominator sometimes gets set to 0
                //YP if MoietyAmount isn't set, thus numerator is null, then use existing logic
                //if MoietyAmount is set then return it as Amount
                //YP Issue 3
                if (MoietyAmount.Numerator != null || MoietyAmount.Center !=null || MoietyAmount.Low != null || MoietyAmount.High != null)
                //if (MoietyAmount.Numerator != null || MoietyAmount.Low != null || MoietyAmount.High != null)
                {
                    return MoietyAmount;
                }
                else
                {
                    
                    
                    Amount a = UndefinedAmount ? Amount.UncertainNonZero : new Amount(1);

                    if (RepresentativeStructure || ParentSubstanceMixType==MixtureType.AnyOf)
                    {
                        a = Amount.UncertainZero;
                    }
                    if (isParentUndefined)
                    {
                        a.Denominator = 1;
                    }
                    else
                    {
                        a.Denominator = ParentMixtureCount;
                    }
                    // All moieties are explicitly
                    // in mol/mol ratios for now
                    a.Unit = "mol";
                    a.DenominatorUnit = "mol";
                    return a;
                }
            }
        }

        public string UID
        {
            get
            {
                //YP comment out if standardtiation is not dezired
                //standardize();
                return (InChI + Stereo + Amount.UID + (Submoieties.Count == 0 ? "" : "[" + String.Join("|", Submoieties.OrderBy(m => m.UID).Select(m => m.UID)) + "]"));
            }
        }

        public void standardize()
        {
            if (IsMixture)
            {
                bool isUndefinedParent = false;
                foreach (Moiety m in Submoieties)
                {
                    if (m.UndefinedAmount)
                    {
                        isUndefinedParent = true;
                    }
                }
                foreach (Moiety m in Submoieties)
                {
                    //m.MixtureComponent = true;
                    m.ParentMixtureCount = Submoieties.Count;
                    m.isParentUndefined = isUndefinedParent;
                }
            }
        }

        public override string ToString()
        {
            return String.Format("{0} {1} [{2}]", InChI, Stereo, (Submoieties.Count == 0 ? "" : String.Join("; ", Submoieties.OrderBy(m => m.UID).Select(m => m.UID))));
        }

        //Special Stereo
        public static string decodeStereoType(string stereo)
        {
            if (String.IsNullOrWhiteSpace(stereo))
                return null;

            switch (stereo.Trim().ToUpper())
            {
                case "(+)":
                    return "C103202";
                case "(-)":
                    return "C103203";
                case "(+/-)":
                    return "C103204";
                case "SQUARE PLANAR 1":
                case "SP-4-1":
                case "SP-4-1 (TRANS)":
                    return "C103211";
                case "SQUARE PLANAR 2":
                case "SP-4-2":
                case "SQUARE PLANAR 4-2 (SP4-2) (not in CV)":
                case "SQUARE PLANAR 4-2 (SP4-2)":
                    return "C103212";
                case "SQUARE PLANAR 3":
                case "SP-4-3":
                    return "C103213";
                case "SQUARE PLANAR 4":
                case "SP-4-4":
                    return "C103214";
                case "TETRAHEDRAL":
                case "T-4":
                    return "C103215";
                case "OCTAHEDRAL 12":
                case "OC-6-12":
                case "TRANS-OCTAHEDRAL":
                    return "C103216";
                case "OCTAHEDRAL 22":
                case "OC-6-22":
                case "MER-OCTAHEDRAL":
                    return "C103217";
                case "OCTAHEDRAL 21":
                case "OC-6-21":
                case "CIS-OCTAHEDRAL":
                case "FAC-OCTAHEDRAL":
                    return "C103218";
                case "EPIMERIC":
                    return "C103209";
                case "CAHN-INGOLD-PRELOG PRIORITY SYSTEM":
                case "CIP SYSTEM":
                    return "C103219";
                case "AXIAL R":
                case "RA":
                case "P":
                case "DELTA":
                case "AXIAL, R":
                    return "C103220";
                case "AXIAL S":
                case "SA":
                case "M":
                case "LAMBDA":
                case "AXIAL, S":
                    return "C103221";
                default:
                    return null;
            }
        }

        private static string preferredTermStereoType(string stereo_code)
        {
            switch (stereo_code)
            {
                case "C103202":
                    return "Dextrorotatory";
                case "C103203":
                    return "Levorotatory";
                case "C103204":
                    return "Nonrotatory";
                case "C103211":
                    return "Square Planar 1";
                case "C103212":
                    return "Square Planar 2";
                case "C103213":
                    return "Square Planar 3";
                case "C103214":
                    return "Square Planar 4";
                case "C103209":
                    return "Epimeric";
                case "C103215":
                    return "Tetrahedral";
                case "C103216":
                    return "Octahedral 12";
                case "C103217":
                    return "Octahedral 22";
                case "C103218":
                    return "Octahedral 21";
                case "C103219":
                    return "Cahn-Ingold-Prelog Priority System";
                case "C103220":
                    return "Axial R";
                case "C103221":
                    return "Axial S";
                default:
                    return null;
            }
        }

        public override XElement SPL
        {
            get
            {

                XElement xMoiety = new XElement(xmlns.spl + "moiety");
                if (MixtureComponent && !IsNonStoichiometric)
                {
                    xMoiety.Add(
                        new XElement(xmlns.spl + "code",
                            new XAttribute("displayName", "mixture component"),
                            new XAttribute("codeSystem", "2.16.840.1.113883.3.26.1.1"),
                            new XAttribute("code", "C103243")));
                }

                xMoiety.Add(Amount.SPL);

                XElement xPartMoiety = new XElement(xmlns.spl + "partMoiety");
                xMoiety.Add(xPartMoiety);
                if (IsMixture)
                {
                    bool isUndefinedParent = false;
                    foreach (Moiety m in Submoieties)
                    {
                        if (m.UndefinedAmount)
                        {
                            isUndefinedParent = true;
                        }
                    }
                    foreach (Moiety m in Submoieties)
                    {
                        //m.MixtureComponent = true;
                        m.ParentMixtureCount = Submoieties.Count;
                        m.isParentUndefined = isUndefinedParent;
                        xPartMoiety.Add(m.SPL);

                    }
                }
                else if (!String.IsNullOrEmpty(MoietyUNII))
                {
                    xPartMoiety.Add(new XElement(xmlns.spl + "code", new XAttribute("code", MoietyUNII), new XAttribute("codeSystem", "2.16.840.1.113883.4.9")));
                }

                if (Molecule != null && Submoieties.Count == 0)
                {
                    if (String.IsNullOrEmpty(InChI))
                    {
                        File.WriteAllText("non-inchifiable.mol", Mol);
                        throw new SrsException("invalid_mol", "Non-InChI-fiable MOL: " + Mol);
                    }

                    xMoiety.Add(new SplCharacteristic("chemical-mol", Molecule.Mol.MolReplaceProgramName("-FDASRS-")).SPL);
                    xMoiety.Add(new SplCharacteristic("chemical-inchi", Molecule.InChI).SPL);
                    xMoiety.Add(new SplCharacteristic("chemical-inchikey", Molecule.InChIKey).SPL);

                    //Removed this from the MOEITY part, and added to the identified substance
                    //NOTE: this may be an issue for mixtures of mixtures so-to-speak
                    //
                    //TP: Readded based on YB's feedback
                    //(a little uncertain about the rule here)                   

                    //Only adds non-rotatory stereo info
                    XElement stereoXML = StereoSPL;


                    if (stereoXML != null)
                    {
                        // Special Stereo                
                        xMoiety.Add(stereoXML);
                    }

                }

                return xMoiety;
            }
        }

        public XElement StereoSPL
        {
            get
            {
                string stereoCode = decodeStereoType(SpecialStereo);
                if (!String.IsNullOrEmpty(stereoCode))
                {

                    if (stereoCode == "C103202" || stereoCode == "C103203" || stereoCode == "C103204")
                    {

                    }
                    else
                    {
                        // TODO: Turn into SplCharacteristic
                        return new XElement(xmlns.spl + "subjectOf",
                        new XElement(xmlns.spl + "characteristic",
                            new XElement(xmlns.spl + "code", new XAttribute("code", "C18188"), new XAttribute("codeSystem", "2.16.840.1.113883.3.26.1.1"), new XAttribute("displayName", "Stereochemistry Type")),
                            new XElement(xmlns.spl + "value", new XAttribute(xmlns.xsi + "type", "CV"), new XAttribute("codeSystem", "2.16.840.1.113883.3.26.1.1"), new XAttribute("code", stereoCode), new XAttribute("displayName", preferredTermStereoType(stereoCode)))));
                    }
                }
                return null;
            }
        }

        public XElement OpticalActivitySPL
        {
            get
            {
                string stereoCode = decodeStereoType(SpecialStereo);
                if (!String.IsNullOrEmpty(stereoCode))
                {
                    if (stereoCode == "C103202" || stereoCode == "C103203" || stereoCode == "C103204")
                    {
                        // TODO: Turn into SplCharacteristic
                        return new XElement(xmlns.spl + "subjectOf",
                            new XElement(xmlns.spl + "characteristic",
                                new XElement(xmlns.spl + "code", new XAttribute("code", "C103201"), new XAttribute("codeSystem", "2.16.840.1.113883.3.26.1.1"), new XAttribute("displayName", "Optical Activity")),
                                new XElement(xmlns.spl + "value", new XAttribute(xmlns.xsi + "type", "CV"), new XAttribute("codeSystem", "2.16.840.1.113883.3.26.1.1"), new XAttribute("code", stereoCode), new XAttribute("displayName", preferredTermStereoType(stereoCode)))));
                    }

                }
                return null;
            }
        }

        /*public XElement ToSPL(bool IsMixtureComponent, int count)
        {
            XElement xMoiety = new XElement(xmlns.spl + "moiety");
            if ( IsMixtureComponent ) {
                xMoiety.Add(
                    new XElement(xmlns.spl + "code",
                        new XAttribute("displayName", "mixture component"),
                        new XAttribute("codeSystem", "2.16.840.1.113883.3.26.1.1"),
                        new XAttribute("code", "C103243")));
            }

            xMoiety.Add(Amount.SPL);

            if ( IsMixtureComponent ) {
                XElement xPartMoiety = new XElement(xmlns.spl + "partMoiety");
                xMoiety.Add(xPartMoiety);
                foreach ( Moiety m in Submoieties ) {
                    xPartMoiety.Add(m.ToSPL(true, Submoieties.Count));
                }
            }
            else {
                XElement xPartMoiety = new XElement(xmlns.spl + "partMoiety",
                    new XElement(xmlns.spl + "code", new XAttribute("code", MoietyUNII), new XAttribute("codeSystem", "2.16.840.1.113883.4.9")));
                xMoiety.Add(xPartMoiety);
            }

            if ( Molecule != null && Submoieties.Count == 0 )
            {
                if ( String.IsNullOrEmpty(InChI) )
                    throw new SrsException("invalid_mol", "Non-InChI-fiable MOL: " + Mol);

                xMoiety.Add(new SplCharacteristic("chemical-mol", Molecule.Mol.MolReplaceProgramName("-FDASRS-")).SPL);
                xMoiety.Add(new SplCharacteristic("chemical-inchi", Molecule.InChI).SPL);
                xMoiety.Add(new SplCharacteristic("chemical-inchikey", Molecule.InChIKey).SPL);

                // Special Stereo                
                string stereoCode = decodeStereoType(SpecialStereo);
                if ( !String.IsNullOrEmpty(stereoCode) ) {
                    xMoiety.Add(
                        // TODO: Turn into SplCharacteristic
                        new XElement(xmlns.spl + "subjectOf",
                            new XElement(xmlns.spl + "characteristic",
                                stereoCode == "C103202" || stereoCode == "C103203" || stereoCode == "C103204" ?
                                    new XElement(xmlns.spl + "code", new XAttribute("code", "C103201"), new XAttribute("codeSystem", "2.16.840.1.113883.3.26.1.1"), new XAttribute("displayName", "Optical Activity")) :
                                    new XElement(xmlns.spl + "code", new XAttribute("code", "C18188"), new XAttribute("codeSystem", "2.16.840.1.113883.3.26.1.1"), new XAttribute("displayName", "Stereochemistry Type")),
                                new XElement(xmlns.spl + "value", new XAttribute(xmlns.xsi + "type", "CV"), new XAttribute("codeSystem", "2.16.840.1.113883.3.26.1.1"), new XAttribute("code", stereoCode), new XAttribute("displayName", preferredTermStereoType(stereoCode))))));
                }
            }

            return xMoiety;
        }*/
    }

    public class MoietyEqualityComparer : IEqualityComparer<Moiety>
    {
        public bool Equals(Moiety m1, Moiety m2)
        {
            return m1.UID == m2.UID;
        }

        public int GetHashCode(Moiety m)
        {
            return m.UID.GetHashCode();
        }
    }
}
