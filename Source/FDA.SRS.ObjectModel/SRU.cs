using com.epam.indigo;
using FDA.SRS.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using System.Diagnostics;
using System.Threading;



namespace FDA.SRS.ObjectModel
{
    public static class SRUHelpers
    {
        public static string UID(this IEnumerable<SRU> srulist)
        {
            return
                srulist.Count() == 0 ?
                    null :
                    String.Join("|", srulist.OrderBy(sru => sru.UID).Select(sru => sru.UID))
                ?.GetMD5String();
        }
    }

    public class SRU : SplObject, IUniquelyIdentifiable
    {
        [Serializable]
        public class Connector
        {
            /// <summary>
            /// Sorted connector ordinal within a Fragment
            /// </summary>
            public int Id { get; set; }

            /// <summary>
            /// SRU, Head end or Tail end
            /// </summary>
            public int Type { get; set; }
            public int Subtype { get; set; }

            /// <summary>
            /// A pair of canonical (N, C) positions within a Fragment
            /// </summary>
            public Tuple<int, int> Snip { get; set; }

            public override bool Equals(object obj)
            {
                return Snip.Item1 == (obj as Connector).Snip.Item1 && Snip.Item2 == (obj as Connector).Snip.Item2;
            }

        }

        private string Type;

        private string Subtype;
        public List<string> SRULabels { get; set;}
        public Chain parent_chain { get; set; }
        public int Ordinal { get; set; }
        private int _counter_value;
        public Amount Amount { get; set; }

        public int Fragment_Id { get; set; }
        public double? MolecularWeight { get; set; }
        /// <summary>
        /// Unique fragment ID in SPL
        /// </summary>
        /// 
        /*
        public override string Id
        {
            set
            {
                lock (this)
                    Type = value;
            }
            get
            {
                lock (this)
                {
                    if (Type == null)
                        Type = String.Format("F{0}", Interlocked.Increment(ref Counters.SRUCounter));
                    return Type;
                }
            }
        }
        */

        public override string Id
        {
            set
            {
                lock (this)
                    Type = value;
            }
            get
            {
                lock (this)
                {
                    if (Type == "SRU") {
                        _counter_value = Counters.SRUCounter;
                    } else if (Type == "Head end")
                    {
                        _counter_value = Counters.HeadEndCounter;
                    } else if (Type == "Tail end")
                    {
                        _counter_value = Counters.TailEndCounter;
                    } else if (Type == "F")
                    {
                        _counter_value = Counters.FragmentCounter;
                    } else
                    {
                        _counter_value = 1;
                    }
                    return _counter_value > 1 ? String.Format("{0}{1}", Type.Replace(" ","_"), Ordinal) : String.Format("{0}", Type.Replace(" ", "_"));
                }
            }
        }
        public List<Connector> Connectors { get; set; } = new List<Connector>();
        public List<int> Connecting_atoms = new List<int>();
        public List<int> Connecting_atoms_head = new List<int>();
        public List<int> Connecting_atoms_tail = new List<int>();
        public string InChI;
        public string InChIKey;
        //public NewMolecule CanonicalizedMolecule { get; set; }
        public string Mol;
        //YP polymers 2020
        //this is deprecated as of 09/13/2020 as the latest version of polymer.exe produces atoms that are already in a canonical order
        //string CanonicalizedMol;
        public PolymerUnit plmr_unit { get; set; }
        public SplObject RootObject { get; set; }
        private SDFUtil.IMolecule _molecule;
        private SDFUtil.IMolecule _canonicalized_molecule;
        public int[] _cn;
        public bool UndefinedAmount { get; 
            set; }



        public SRU(string mol)
        {
            this.Mol = mol;
            this._molecule = new SDFUtil.NewMolecule(mol);
        }

        public SRU(PolymerUnit punit, SplObject rootObj, int fragment_id=0)
            : base(rootObj, "SRU")
        {
            this.plmr_unit = punit;
            this.Mol = punit.getMol();
            this.MolecularWeight = punit.getMolecule().MolecularWeight;
            Type = "";
            Subtype = "";
            Fragment_Id = fragment_id;
            //this.Molecule = new NewMolecule(this.Mol);
            //this.CanonicalizedMolecule = new NewMolecule(this.Mol);

            //YP polymers 2020
            //this is deprecated as of 09/13/2020 as the latest version of polymer.exe produces atoms that are already in a canonical order
            //setCanonicalAtoms();

            //if (plmr_unit.getFragmentType() != "linear sru")
            //{
            //YP polymers 2020
            //this is deprecated as of 09/13/2020 as the latest version of polymer.exe produces atoms that are already in a canonical order
            //this.setCanonicalizedMolecule();

            //}
            foreach (int atom_index in punit.getConnectingAtoms(fragment_id: Fragment_Id))
            {
                this.Connecting_atoms.Add(atom_index);
            }

            foreach (int atom_index in punit.getConnectingAtomsHead(fragment_id: Fragment_Id))
            {
                this.Connecting_atoms_head.Add(atom_index);
            }

            foreach (int atom_index in punit.getConnectingAtomsTail(fragment_id: Fragment_Id))
            {
                this.Connecting_atoms_tail.Add(atom_index);
            }

            this.RootObject = rootObj;

            if (punit.getFragmentType()== "branched sru") { Subtype = "BRANCHED";  } else { Subtype = "LINEAR"; }
           

            if (punit.getFragmentType() == "head end group")
            {
                Type = "Head end";
                Ordinal = Interlocked.Increment(ref Counters.HeadEndCounter);
                _counter_value = Counters.HeadEndCounter;

            } else if (punit.getFragmentType() == "tail end group")
            {
                Type = "Tail end";
                Ordinal = Interlocked.Increment(ref Counters.TailEndCounter);
                _counter_value = Counters.TailEndCounter;

            } else if (punit.getFragmentType() == "linear sru")
            {
                Type = "SRU";
                Ordinal = Interlocked.Increment(ref Counters.SRUCounter);
                _counter_value = Counters.SRUCounter;

            } else if (punit.getFragmentType() == "branched sru")
            {
                Type = "SRU";
                Ordinal = Interlocked.Increment(ref Counters.SRUCounter);
                _counter_value = Counters.SRUCounter;

            } else if (punit.getFragmentType() == "connection")
            {
                Type = "F";
                Ordinal = Interlocked.Increment(ref Counters.FragmentCounter);
                _counter_value = Counters.FragmentCounter;
            } else if (punit.getFragmentType() == "disconnected")
            {
                Type = "Disconnected";
                Ordinal = Interlocked.Increment(ref Counters.FragmentCounter);
                _counter_value = Counters.FragmentCounter;
            }


            //DisplayName = punit.getFragmentType();
            InChI = punit.getUnitInChI();
            InChIKey = punit.getUnitInChIKey();
            //Amount = new Amount();
            Amount = new Amount();
            Amount.AmountType = AmountType.UncertainNonZero;
            //InChIKey = "Not yet available";
        }

        public override string ToString()
        {
            return InChI;
        }
        /*
         * private Amount Amount
        {
            get
            {
                Amount a = UndefinedAmount ? Amount.UncertainNonZero : new Amount(1);
                // All moieties are explicitly
                // in mol/mol ratios for now
                a.Unit = "mol";
                a.DenominatorUnit = "mol";
                return a;
            }
        }
        */

        //YP polymers 2020
        //this is deprecated as of 09/13/2020 as the latest version of polymer.exe produces atoms that are already in a canonical order
        /*
        private void setCanonicalizedMolecule()
        {
            SDFUtil.NewMolecule nm = new SDFUtil.NewMolecule(this.Mol);      
            //_canonicalized_molecule = nm.ReorderCanonically();
            _canonicalized_molecule = SrsSdfUtils.reorderBasedOn(nm, _cn);
            this.CanonicalizedMol = _canonicalized_molecule.Mol;

        }
        */

        //YP polymers 2020
        //this is deprecated as of 09/13/2020 as the latest version of polymer.exe produces atoms that are already in a canonical order
        /*
        public void setCanonicalAtoms()
        {
            _cn = plmr_unit.getCanonicalAtoms();
        }
        */
        /*
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
        */
        public string UID
        {
            get
            {
                return (InChI + "_" + Amount.UID);
            }
        }

        public string UNII
        {
            set;
            get;
        }


        public XElement SPL2
        {
            get
            {

                XElement xMoiety = new XElement(xmlns.spl + "moiety");

                //xMoiety.Add(Amount.SPL);

                XElement xPartMoiety = new XElement(xmlns.spl + "partMoiety");

                //xPartMoiety.Add(new XElement(xmlns.spl + "code", new XAttribute("code", "SRU"), new XAttribute("codeSystem", "2.16.840.1.113883.4.9"), new XAttribute("displayName", "SRU")));
                xPartMoiety.Add(new XElement(xmlns.spl + "code", new XAttribute("code", Id ?? ""), new XAttribute("codeSystem", RootObject.DocId), new XAttribute("displayName", Id ?? "")));
                xMoiety.Add(new XElement(xmlns.spl + "code", new XAttribute("code", Code ?? ""), new XAttribute("codeSystem", CodeSystem ?? ""), new XAttribute("displayName", DisplayName ?? "")),
                        Amount.SPL, xPartMoiety);

                return xMoiety;
            }
        }

        public override XElement SPL
        {
            get
            {

                XElement xIdentifiedSubstance =
                    new XElement(xmlns.spl + "identifiedSubstance",
                        new XElement(xmlns.spl + "code", new XAttribute("code", Id ?? ""), new XAttribute("codeSystem", RootObject.DocId)),
                        new SplHash(UID.GetMD5String().FormatAsGuid()).SPL
                    );

                if (!String.IsNullOrEmpty(UNII))
                {
                    xIdentifiedSubstance.Add(
                        new XElement(xmlns.spl + "asSpecializedKind",
                            new XElement(xmlns.spl + "generalizedMaterialKind",
                                new XElement(xmlns.spl + "code", new XAttribute("code", UNII), new XAttribute("codeSystem", "2.16.840.1.113883.4.9"))
                            )
                        ));
                }

                // See SRS-173 for explanation
                //YP
                var xPartMoiety = new XElement(xmlns.spl + "partMoiety");
                if (!String.IsNullOrEmpty(UNII))
                    xPartMoiety.Add(new XElement(xmlns.spl + "code", new XAttribute("code", UNII), new XAttribute("codeSystem", "2.16.840.1.113883.4.9")));

                this.Mol = MiscUtils.RemoveMolfileBrackets(this.Mol);
                //this.CanonicalizedMol = MiscUtils.RemoveMolfileBrackets(this.CanonicalizedMol);
                xIdentifiedSubstance.Add(
                    new XElement(xmlns.spl + "moiety",
                        new Amount(1, "mol", "mol").SPL, // TODO: For now in all fragments amount == "1/1", unit="1". See SRS-208 for more info.
                        xPartMoiety,
                        //new SplCharacteristic("chemical-mol", (Id=="SRU") ? Mol.MolReplaceProgramName("-FDASRS-") : CanonicalizedMol.MolReplaceProgramName("-FDASRS-")).SPL,
                        new SplCharacteristic("chemical-mol", Mol.MolReplaceProgramName("-FDASRS-")).SPL,
                        new SplCharacteristic("chemical-inchi", InChI).SPL,
                        new SplCharacteristic("chemical-inchikey", InChIKey).SPL
                    )
                );

                PlmrTerms connection_point_terms = new PlmrTerms(Subtype + "#####" + "CONNECTION POINTS");

                // if connection points not canonicalized use _cn[i - 1] + 1 instead of i

                if ((Type == "Head end") || (Type == "F" && parent_chain.tail_present))
                {
                    foreach (int i in Connecting_atoms)
                    {
                        List<XElement> XConnectors = new List<XElement>();
                        XConnectors.Add(
                        new XElement(xmlns.spl + "positionNumber", new XAttribute("nullFlavor", "NA"))
                        );
                        XConnectors.Add(
                            new XElement(xmlns.spl + "positionNumber", new XAttribute("value", i))
                            );
                        XElement XConnectorsMoiety = new XElement(xmlns.spl + "moiety",
                       // new XComment("SplFragmentSnapIn"),
                       new XElement(xmlns.spl + "code",
                            new XAttribute("code", connection_point_terms.Code),
                            new XAttribute("codeSystem", connection_point_terms.CodeSystem),
                            new XAttribute("displayName", connection_point_terms.DisplayName)
                       ));
                        XConnectorsMoiety.Add(XConnectors);
                        XConnectorsMoiety.Add(xPartMoiety);
                        xIdentifiedSubstance.Add(XConnectorsMoiety);
                    }
                }
                
                //YP to ensure there's always two positionNumbers, if only one connection point, add second positionNumber="N/A"
                if ((Type == "Tail end") || (Type=="F" && parent_chain.head_present) )
                {
                    foreach (int i in Connecting_atoms)
                    {
                        List<XElement> XConnectors = new List<XElement>();
                        XConnectors.Add(
                            new XElement(xmlns.spl + "positionNumber", new XAttribute("value", i))
                            );
                        XConnectors.Add(
                        new XElement(xmlns.spl + "positionNumber", new XAttribute("nullFlavor", "NA"))
                        );
                        XElement XConnectorsMoiety = new XElement(xmlns.spl + "moiety",
                       // new XComment("SplFragmentSnapIn"),
                       new XElement(xmlns.spl + "code",
                            new XAttribute("code", connection_point_terms.Code),
                            new XAttribute("codeSystem", connection_point_terms.CodeSystem),
                            new XAttribute("displayName", connection_point_terms.DisplayName)
                       ));
                        XConnectorsMoiety.Add(XConnectors);
                        XConnectorsMoiety.Add(xPartMoiety);
                        xIdentifiedSubstance.Add(XConnectorsMoiety);
                    }
                    
                }
                if ((Type == "SRU"))
                {
                    List<XElement> XConnectors = new List<XElement>();
                    foreach (int i in Connecting_atoms_head)
                    {
                        XConnectors.Add(
                            new XElement(xmlns.spl + "positionNumber", new XAttribute("value", i))
                            );
                    }
                    foreach (int i in Connecting_atoms_tail)
                    {
                        XConnectors.Add(
                            new XElement(xmlns.spl + "positionNumber", new XAttribute("value", i))
                            );
                    }

                    
                    foreach (int i in Connecting_atoms)
                    {
                        if (!Connecting_atoms_head.Contains(i) && !Connecting_atoms_tail.Contains(i))
                        {
                            XConnectors.Add(
                                new XElement(xmlns.spl + "positionNumber", new XAttribute("value", i))
                                );
                        }
                    }
                    
                    
                    XElement XConnectorsMoiety = new XElement(xmlns.spl + "moiety",
                      // new XComment("SplFragmentSnapIn"),
                      new XElement(xmlns.spl + "code",
                            new XAttribute("code", connection_point_terms.Code),
                            new XAttribute("codeSystem", connection_point_terms.CodeSystem),
                            new XAttribute("displayName", connection_point_terms.DisplayName)
                      ));
                    XConnectorsMoiety.Add(XConnectors);
                    XConnectorsMoiety.Add(xPartMoiety);
                    xIdentifiedSubstance.Add(XConnectorsMoiety);

                }

                //xIdentifiedSubstance.Add(
                //    new XElement(xmlns.spl + "partMoiety")
                //    );
                

                XElement xSubject =
                    new XElement(xmlns.spl + "subject",
                        new XElement(xmlns.spl + "identifiedSubstance",
                            new XElement(xmlns.spl + "id", new XAttribute("extension", Id ?? ""), new XAttribute("root", RootObject.DocId)),
                            xIdentifiedSubstance
                        ));

                return xSubject;
            }
        }

    }
}
