using com.epam.indigo;
using FDA.SRS.ObjectModel;
using FDA.SRS.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;


namespace FDA.SRS.Processing
{
	public static class Standardize
	{
		public static Stereomers processMolecule(Indigo indigo, IndigoObject mol, string unii = null, bool isV2000=false)
		{
			// Create stereo groups three
			Dictionary<StereoType, int> maxGroups = Enum.GetValues(typeof(StereoType)).Cast<StereoType>().ToDictionary(st => st, st => 0);
			Dictionary<StereoType, Dictionary<int, List<int>>> stereoTree = new Dictionary<StereoType, Dictionary<int, List<int>>>();
            //YP SRS-412
            //don't perform stereomers enumeration if not molfile is V2000
            if (!isV2000) 
            {
			    foreach ( IndigoObject atom in mol.iterateStereocenters() ) {
				    StereoType stereoType = (StereoType)atom.stereocenterType();
				    Dictionary<int, List<int>> stereoTypeCntnr;
				    if ( stereoTree.ContainsKey(stereoType) )
					    stereoTypeCntnr = stereoTree[stereoType];
				    else {
					    stereoTypeCntnr = new Dictionary<int, List<int>>();
					    stereoTree.Add(stereoType, stereoTypeCntnr);
				    }

				    List<int> stereoGroupAtoms;
				    int stereoGroup = atom.stereocenterGroup(); // implemented in old Indigo
				    // int stereoGroup = AlteredIndigoMethods.stereocenterGroup(atom);
				    if ( stereoTypeCntnr.ContainsKey(stereoGroup) )
					    stereoGroupAtoms = stereoTypeCntnr[stereoGroup];
				    else {
					    stereoGroupAtoms = new List<int>();
					    stereoTypeCntnr.Add(stereoGroup, stereoGroupAtoms);
				    }

				    if ( stereoGroup > maxGroups[stereoType] )
					    maxGroups[stereoType] = stereoGroup;

				    stereoGroupAtoms.Add(atom.index());
			    }
            }
            foreach ( var type in stereoTree ) {
				TraceUtils.WriteUNIITrace(TraceEventType.Information, unii, null,
					"{0}: {1}", type.Key, String.Join(", ", type.Value.Select(g => "G" + g.Key + "(" + String.Join(", ", g.Value) + ")")));
			}

			Stereomers stereomers = new Stereomers(indigo, mol.countAtoms());

			if ( stereoTree.Count == 0 || stereoTree.Count == 1 && stereoTree.First().Key == StereoType.Abs ) {
				if ( stereoTree.Count != 0 )
					stereomers.InitMask(stereoTree.First().Value.First().Value);
				stereomers.Add(new StereoConfiguration(indigo, mol));
			}
			else {
				foreach ( var type in stereoTree ) {
					if ( type.Key != StereoType.Abs ) {
						if ( type.Value.Count == 1 ) {
							stereomers.InitMask(type.Value.First().Value);
							processGroup(stereomers, new StereoConfiguration(indigo, mol), type.Value.First().Value);
						}
						else {
							foreach ( var g in type.Value )
								stereomers.InitMask(g.Value);
							processMixedGroups(stereomers, new StereoConfiguration(indigo, mol), type.Value);
						}
					}
				}
			}

			return stereomers;
		}

		public static void processGroup(Stereomers stereomers, StereoConfiguration conf, List<int> group)
		{
			stereomers.Add(conf);

			// Make a cast and save current molecule
			StereoConfiguration c2 = conf.Clone();

			// Invert all atoms in a group
			foreach ( int a in group ) {
				IndigoObject atom = c2.Molecule.getAtom(a);
				atom.invertStereo();
				c2.Invert(a);
			}
			c2.Molecule.markStereobonds();

			stereomers.Add(c2);
		}

		public static void processMixedGroups(Stereomers stereomers, StereoConfiguration conf, Dictionary<int, List<int>> groups)
		{
			if ( groups.Count == 0 )
				return;

			Dictionary<int, List<int>> groupsReduced = new Dictionary<int, List<int>>(groups);
			groupsReduced.Remove(groups.First().Key);

			// Make a cast and save current molecule
			StereoConfiguration c1 = conf.Clone();

			// Drill down with current molecule
			processMixedGroups(stereomers, c1, groupsReduced);

			// Invert all atoms in a group
			StereoConfiguration c2 = conf.Clone();
			foreach ( int a in groups.First().Value ) {
				IndigoObject atom = c2.Molecule.getAtom(a);
				atom.invertStereo();
				c2.Invert(a);
			}
			c2.Molecule.markStereobonds();

			// Drill down with inverted molecule
			processMixedGroups(stereomers, c2, groupsReduced);

			// IndigoObject o3 = mergeShifted(o1, o2);

			// Write only leafs of the tree to avoid duplication
			if ( groupsReduced.Count == 0 ) {
				stereomers.Add(c1);
				stereomers.Add(c2);
			}
		}

		public static IndigoObject mergeShifted(IndigoObject o1, IndigoObject o2)
		{
			IndigoObject o3 = o1.clone();
			float xmin = float.MaxValue, xmax = float.MinValue;
			float ymin = float.MaxValue, ymax = float.MinValue;
			o3.iterateAtoms().Cast<IndigoObject>().Select(a => a.xyz()).ToList().ForEach(xyz => {
				if ( xyz[0] > xmax )
					xmax = xyz[0];
				if ( xyz[0] < xmin )
					xmin = xyz[0];
				if ( xyz[1] > ymax )
					ymax = xyz[0];
				if ( xyz[1] < ymin )
					ymin = xyz[0];
			});

			o2.iterateAtoms().Cast<IndigoObject>().ToList().ForEach(a => {
				float[] xyz = a.xyz();
				a.setXYZ(xyz[0] + (xmax - xmin) * 1.2f, xyz[1], xyz[2]);
			});

			o3.merge(o2);
			return o3;
		}

		public static void Moietize(this Substance subst)
		{
			IEnumerable<Moiety> ml;
			using ( var indigo = new Indigo() ) {
				try {
                    //indigo.setOption("treat-stereo-as", "any");
                    IndigoInchi indigoInChI = new IndigoInchi(indigo);  
                    using ( IndigoObject mol = indigo.loadMolecule(subst.Sdf.Mol) ) {
						Stereomers s = Standardize.processMolecule(indigo, mol, subst.UNII, isV2000: subst.Sdf.Mol.Contains("V2000"));
						ml = s.ToMoieties(subst.SpecialStereo, subst.UNII).ToList();
						if ( ml.Where(m => m.Molecule != null && m.Molecule.Ends != null && m.Molecule.Ends.Count > 0).Any() )
							throw new SrsException("invalid_mol", "Fragments as moieties are not supported");
					}
				}
				catch ( Exception ex ) {
					if ( ex is SrsException )
						throw;
					throw new SrsException("invalid_mol", ex.Message, ex);
				}
			}

			subst.Moieties = ml.Distinct(new MoietyEqualityComparer()).ToList();
			foreach ( var m in subst.Moieties ) {
				m.MixtureCount = subst.Moieties.Count();
			}
		}

        public static void MoietizeRepresentative(this Substance subst)
        {
            IEnumerable<Moiety> ml;
            using (var indigo = new Indigo())
            {
                try
                {
                    using (IndigoObject mol = indigo.loadMolecule(subst.Sdf.Mol))
                    {
                        Stereomers s = Standardize.processMolecule(indigo, mol, subst.UNII, isV2000: subst.Sdf.Mol.Contains("V2000"));
                        ml = s.ToMoieties(subst.SpecialStereo, subst.UNII).ToList();
                        if (ml.Where(m => m.Molecule != null && m.Molecule.Ends != null && m.Molecule.Ends.Count > 0).Any())
                            throw new SrsException("invalid_mol", "Fragments as moieties are not supported");
                    }
                }
                catch (Exception ex)
                {
                    if (ex is SrsException)
                        throw;
                    throw new SrsException("invalid_mol", ex.Message, ex);
                }
            }

            //YP if only one stereoisomer is possible for the molecule than make it the parent
            if (ml.Distinct(new MoietyEqualityComparer()).ToList().Count() == 1)
            {
                subst.Moieties = ml.Distinct(new MoietyEqualityComparer()).ToList();
                foreach (Moiety parentMoiety in subst.Moieties)
                {
                    parentMoiety.RepresentativeStructure = true;
                }
            }
            //YP else make the stereoisomers the children of the parent moiety
            else
            {
                foreach (Moiety parentMoiety in subst.Moieties)
                {
                    //parentMoiety.Submoieties = ml.Distinct(new MoietyEqualityComparer()).ToList();
                    subst.Moieties = ml.Distinct(new MoietyEqualityComparer()).ToList();
                    //parentMoiety.ParentMixtureCount = parentMoiety.Submoieties.Count();
                    //foreach (var m in parentMoiety.Submoieties)
                    foreach (var m in subst.Moieties)
                    {
                        //m.MixtureCount = parentMoiety.Submoieties.Count();
                        m.RepresentativeStructure = true;
                    }
                }
            }
        }


        //YP Issue 5
        public static List<PolymerUnit> getDirectlyConnectedSRUs(PolymerUnit in_plmrunit, List<PolymerUnit> plmrunits)
        {
            List <PolymerUnit> returnlist = new List<PolymerUnit>();
            foreach (int in_fragment_id in in_plmrunit.getFragmentIds())
            {
                foreach (PolymerUnit plmrunit in plmrunits)
                {

                    if (plmrunit.getAllConnectedFragmentIDs().Contains(in_fragment_id) && plmrunit.getFragmentType() == "linear sru")
                    {
                        returnlist.Add(plmrunit);
                    }
                }

            }
        
            return returnlist;
        }

        public static int getMaxFragmentId(List<PolymerUnit> plmrunits)
        {
            int max_id = 0;
            foreach (PolymerUnit plmrunit in plmrunits)
            {
                foreach (int frag_id in plmrunit.getFragmentIds())
                {
                    if (frag_id>max_id)
                    {
                        max_id = frag_id;
                    }
                }
            }
            return max_id;
        }


        public static void Moietize(this Plmr plmr)
        {
            Chain chain = null;
            String polymer_tool_options = "";

            List<SRU> non_sru_frags = new List<SRU>();
            int chain_ordinal =0;
            if (plmr.plmr_geometry == "BRANCHED") { polymer_tool_options = "--branched"; }
            List<PolymerUnit> polymer_units = PolymerParser.instance(polymer_tool_options).decompose(plmr.Sdf.Mol).ToList();
            List<PolymerUnit> blank_connectors_units = new List<PolymerUnit>();
            int polymer_index = 0;
            //YP Issue 5
            //If two SRUs are connected directly, need to create a blank *-* connector that connects them
            //if (plmr.plmr_geometry == "BLOCK")
            //{
            string blank_connector_mol = @"
ACCLDraw10012110282D

2  1  0  0  0  0  0  0  0  0999 V2000
10.9063 - 4.7188    0.0000 * 0  0  0  0  0  0  0  0  0  0  0  0
13.0625 - 4.6250    0.0000 * 0  0  0  0  0  0  0  0  0  0  0  0
1  2  1  0  0  0  0
M END
";
            string connector_connectivity = "";
            int next_frag_id = getMaxFragmentId(polymer_units) + 1;
            List<string> already_connected_frag_ids = new List<string>();
            string connector_fragment_ids = "";
            if (plmr.plmr_geometry != "BLOCK")
            {

                chain_ordinal++;
                chain = new Chain(plmr.RootObject) { Code = "C48803", CodeSystem = "2.16.840.1.113883.3.26.1.1", DisplayName = "POLYMER", Ordinal = chain_ordinal, head_present = false, tail_present = false };
                //SRU new_sru = new SRU(plmrUnit, plmr.RootObject, fragment_id: fragment_id) { parent_chain = chain, SRULabels = (plmrUnit.getLabels().Count() > 0 ? plmrUnit.getLabels() : null), UndefinedAmount = true, Mol = plmrUnit.getMol() };
                //chain.sru_fragment_id = fragment_id;
                plmr.Subunits.Add(chain);
                polymer_index++;

            }
            foreach (PolymerUnit plmrUnit in polymer_units)
            {
                if (plmrUnit.getFragmentType() == "linear sru" || plmrUnit.getFragmentType() == "branched sru")
                {
                    List<string> unit_connectivity = (plmrUnit.getConnectivity()?.Split('\n').ToList() ?? new List<string>());
                    //YP Issue 5. If there is no connectivity then these SRUS can't be directly connected, so no need to create fake connector
                    if (unit_connectivity.Count != 0)
                    { 
                        string augmented_fragment_connectivity = "";
                        foreach (string unit_connection in unit_connectivity)
                        {
                            List<string> unit_connection_data = unit_connection.TrimEnd().Split(' ').ToList();
                            if (get_polymer_unit_by_frag_id(Int32.Parse(unit_connection_data[2]), polymer_units).getFragmentType() == "linear sru" || get_polymer_unit_by_frag_id(Int32.Parse(unit_connection_data[2]), polymer_units).getFragmentType() == "branched_sru")
                            {
                                bool connectivity_added = false;
                                if (!already_connected_frag_ids.Contains(unit_connection_data[0]))
                                {
                                    connector_connectivity = connector_connectivity + next_frag_id.ToString() + " " + 1 + " " + unit_connection_data[0] + " " + unit_connection_data[1] + "\r\n";
                                    //augmented_fragment_connectivity = augmented_fragment_connectivity + unit_connection_data[0] + " " + unit_connection_data[1] + " " + next_frag_id.ToString() + " " + 1 + "\r\n";
                                    already_connected_frag_ids.Add(unit_connection_data[0]);
                                    connectivity_added = true;
                                }
                                //augmented_fragment_connectivity = augmented_fragment_connectivity + unit_connection_data[0] + " " + unit_connection_data[1] + " " + next_frag_id.ToString() + " " + 1 + "\r\n";
                                if (!already_connected_frag_ids.Contains(unit_connection_data[2]))
                                {
                                    connector_connectivity = connector_connectivity + next_frag_id.ToString() + " " + 2 + " " + unit_connection_data[2] + " " + unit_connection_data[3] + "\r\n";
                                    //augmented_fragment_connectivity = augmented_fragment_connectivity + unit_connection_data[0] + " " + unit_connection_data[1] + " " + next_frag_id.ToString() + " " + 1;
                                    already_connected_frag_ids.Add(unit_connection_data[2]);
                                    connectivity_added = true;
                                }
                                //augmented_fragment_connectivity = augmented_fragment_connectivity + unit_connection_data[0] + " " + unit_connection_data[1] + " " + next_frag_id.ToString() + " " + 1;
                                if (connectivity_added)
                                {
                                    connector_fragment_ids = connector_fragment_ids + next_frag_id.ToString() + "\r\n";
                                    next_frag_id++;
                                }

                            }
                            else
                            {
                                augmented_fragment_connectivity = augmented_fragment_connectivity + unit_connection.TrimEnd() + "\r\n";
                            }

                        }

                        connector_connectivity = connector_connectivity.TrimEnd();
                        //YP Issue 5. Reset fragment connectivity for the sru to remove lines directly connecting other srus
                        augmented_fragment_connectivity = augmented_fragment_connectivity.TrimEnd();
                        SdfRecord augmented_sdf_record = new SdfRecord();
                        augmented_sdf_record.Mol = plmrUnit.getSdfRecord().Mol;
                        foreach (KeyValuePair<string, List<string>> property_value in plmrUnit.getSdfRecord().Properties)
                        {
                            if (property_value.Key == "FRAGMENT_CONNECTIVITY")
                            {
                                augmented_sdf_record.AddField(property_value.Key, augmented_fragment_connectivity);
                            }
                            else
                            {
                                augmented_sdf_record.AddField(property_value.Key, property_value.Value[0]);
                            }
                        }
                        plmrUnit.setSdfRecord(augmented_sdf_record);
                    }
                }
                  
            }
            //YP Issue 5 create blank connector molecule
            connector_fragment_ids = connector_fragment_ids.TrimEnd();
            SdfRecord blank_connector_sdf_record = new SdfRecord();
            blank_connector_sdf_record.Mol = blank_connector_mol;
            blank_connector_sdf_record.AddField("FRAGMENT_CONNECTIVITY", connector_connectivity);
            blank_connector_sdf_record.AddField("FRAGMENT_IDS", connector_fragment_ids);
            blank_connector_sdf_record.AddField("FRAGMENT_TYPE", "connection");
            blank_connector_sdf_record.AddField("Computed_B_InChI", "InChI=1B/Zz2/c1-2");
            blank_connector_sdf_record.AddField("Computed_B_InChIKey", "HZJIKGMPBQCCIS-UHFFFAOYBA-N");

            PolymerUnit blank_connector_sru_unit = new PolymerUnit(blank_connector_sdf_record);
            blank_connectors_units.Add(blank_connector_sru_unit);
            //}
            //YP Issue 5. The If statement  is a hacky way of fixing a symptom of creation of improper connecting units for cases when therea re no heads/tails and no SRUs connect directly
            //See example: 51L8Z461QZ
            if (connector_fragment_ids != "")
            {
                polymer_units.AddRange(blank_connectors_units);
            }
            //YP Issue 5 fill in the fragment_connectivity for SRUs that connect to newly created blank connections
            foreach (PolymerUnit sru_unit in polymer_units.Where(x => x.getFragmentType() == "linear sru" || x.getFragmentType() == "branched sru").ToList())
            {
                string augmented_fragment_connectivity = ""; 
                foreach (int sru_fragment_id in sru_unit.getFragmentIds())
                {
                    foreach (PolymerUnit connection_unit in polymer_units.Where(x => (x.getFragmentType() == "connection")).ToList())
                    {
                        if (connection_unit.getAllConnectedFragmentIDs().ToList().Contains(sru_fragment_id) && !sru_unit.getAllConnectedFragmentIDs().ToList().Any(connection_unit.getFragmentIds().ToList().Contains))
                        {
                            int[] connection_sru_connectivity = connection_unit.geConnectivityConnectingFragment(sru_fragment_id);
                            augmented_fragment_connectivity = augmented_fragment_connectivity + sru_fragment_id + " " + connection_sru_connectivity[3] + " " + connection_sru_connectivity[0] + " " + connection_sru_connectivity[1] + "\r\n";
                        }
                    }
                }

                augmented_fragment_connectivity = augmented_fragment_connectivity.TrimEnd();
                if (augmented_fragment_connectivity != "")
                {

                    SdfRecord augmented_sdf_record = new SdfRecord();
                    augmented_sdf_record.Mol = sru_unit.getSdfRecord().Mol;
                    foreach (KeyValuePair<string, List<string>> property_value in sru_unit.getSdfRecord().Properties)
                    {
                        if (property_value.Key == "FRAGMENT_CONNECTIVITY")
                        {
                            augmented_sdf_record.AddField(property_value.Key, property_value.Value[0] + "\r\n" + augmented_fragment_connectivity);
                        }
                        else
                        {
                            augmented_sdf_record.AddField(property_value.Key, property_value.Value[0]);
                        }
                    }
                    sru_unit.setSdfRecord(augmented_sdf_record);
                }
            }


            //YP Issue 5. Now proceed with regular processing
            foreach (PolymerUnit plmrUnit in polymer_units)
            {
                int connector_ref_id = 0;
                String plmr_error = plmrUnit.getError();
                if (plmr_error != "")
                {
                    throw new SrsException("invalid_mol", plmr_error);
                }
                if (plmrUnit.getLabels().Count == 0 && plmrUnit.getFragmentType() == "disconnected")
                {
                    throw new SrsException("invalid_mol", "Unlabeled disconnected fragment " + plmrUnit.getMolecule().SMILES + " encountered in a polymer. This is not allowed.");
                }
                //int polymer_index = Int32.Parse(plmrUnit.getPolymerLabel());
                if (plmrUnit.getFragmentType() == "linear sru" || plmrUnit.getFragmentType() == "branched sru")
                {
                    
                    polymer_index = Int32.Parse(plmrUnit.getPolymerLabel());
                    //need to create sru for each fragment_id but all represented by single fragment

                    SRU new_sru = new SRU(plmrUnit, plmr.RootObject) { SRULabels = (plmrUnit.getLabels().Count() > 0 ? plmrUnit.getLabels() : null), UndefinedAmount = true, Mol = plmrUnit.getMol() };
                    plmr.SRUs.Add(new_sru);
                    if (plmr.plmr_geometry == "BLOCK")
                    {
                        foreach (int fragment_id in plmrUnit.getFragmentIds())
                        {
                            chain_ordinal++;
                            chain = new Chain(plmr.RootObject) { Code = "C48803", CodeSystem = "2.16.840.1.113883.3.26.1.1", DisplayName = "POLYMER", Ordinal = chain_ordinal, head_present = false, tail_present = false };
                            //SRU new_sru = new SRU(plmrUnit, plmr.RootObject, fragment_id: fragment_id) { parent_chain = chain, SRULabels = (plmrUnit.getLabels().Count() > 0 ? plmrUnit.getLabels() : null), UndefinedAmount = true, Mol = plmrUnit.getMol() };
                            chain.SRUs.Add(new_sru);
                            chain.sru_fragment_id = fragment_id;
                            plmr.Subunits.Add(chain);
                            polymer_index++;
                        }
                    }
                }
                else //non-sru fragment
                {
                    
                    int sru_connection_position = 0;
                    var g = new PlmrStructuralModificationGroup(plmr.RootObject);
                    g.Modification = new PlmrStructuralModification(plmr.RootObject);

                    //need to create mod for each chain all referencing same fragment

                    g.Modification.Fragment = new SRU(plmrUnit, plmr.RootObject) { SRULabels = (plmrUnit.getLabels().Count() > 0 ? plmrUnit.getLabels() : null), UndefinedAmount = true, Mol = plmrUnit.getMol(), fragment_ids = plmrUnit.getFragmentIds() }; ;
                    g.Modification.Fragment.parent_chains = get_chains_by_frag_ids(plmrUnit.getAllConnectedFragmentIDs(), plmr);
                    g.Modification.Amount = new Amount(1);
                    g.Amount = g.Modification.Amount;

                    foreach (int connecting_atom_index in plmrUnit.getConnectingAtomIDs().Distinct())
                    {
                        if(plmr.plmr_geometry != "BLOCK")
                        {
                            //YP Issue 5. Assuming non-block polymers will only have one chain (subunit)
                            g.Modification.Fragment.connected_chains.Add(new Tuple<int, Chain>(connecting_atom_index, plmr.Subunits[0]));
                        }
                        else
                        {
                            g.Modification.Fragment.connected_chains.Add(new Tuple<int, Chain>(connecting_atom_index, get_chain_by_frag_id(plmrUnit.getConnectedFragmentID(connecting_atom_index), plmr)));
                        }
                        
                    }
                    
                    
                    
                    //foreach (Chain parent_chain in get_chains_by_frag_ids(g.Modification.Fragment.connected_fragment_ids, plmr))
                    foreach (int fragment_id in plmrUnit.getFragmentIds())
                    {

                        connector_ref_id = 0;
                        foreach (int parent_chain_fragment_id in (plmrUnit.getConnectedFragmentIDs(fragment_id)))
                        {
                            SRU.Connector end_group_frag_connector = new SRU.Connector();

                            Chain parent_chain = get_chain_by_frag_id(parent_chain_fragment_id, plmr);
                            if (parent_chain == null)
                            {
                                continue;
                            }
                            //YP Issue 5. if this unit connects to the sru at sru's head connecting atom
                            if (get_polymer_unit_by_frag_id(parent_chain_fragment_id,polymer_units).getConnectingAtomsHead().ToList().Contains(plmrUnit.geConnectivityConnectingFragment(parent_chain_fragment_id)[3]))
                            {
                                //parent_chain.head_present = true;
                                parent_chain.connected_frag_id_at_head = fragment_id;
                                sru_connection_position = 1;
                                end_group_frag_connector.Snip = new Tuple<int, int>(1, 0);
                            }
                            //YP Issue 5. if this unit connects to the sru at sru's tail connecting atom
                            else if (get_polymer_unit_by_frag_id(parent_chain_fragment_id, polymer_units).getConnectingAtomsTail().ToList().Contains(plmrUnit.geConnectivityConnectingFragment(parent_chain_fragment_id)[3]))
                            {
                                //parent_chain.tail_present = true;
                                parent_chain.connected_frag_id_at_tail = fragment_id;
                                sru_connection_position = -1;
                                end_group_frag_connector.Snip = new Tuple<int, int>(1, 0);
                            }

                            PlmrSite site = new PlmrSite(plmr.RootObject, "Structural Repeat Unit Substitution Site");
                            site.Position = sru_connection_position;
                            end_group_frag_connector.Id = connector_ref_id;
                            site.ConnectorRef = end_group_frag_connector;
                            if (g.Modification.Fragment.Type != "Disconnected")
                            {
                                site.Chain = parent_chain;
                            }
                            site.Code = "C132923";
                            site.CodeSystem = "2.16.840.1.113883.3.26.1.1";
                            site.DisplayName = "Structural Repeat Unit Substitution Site";
                            g.PolymerSites.Add(site);
                            connector_ref_id++;
                        }
                    }
                    
                    plmr.Modifications.Add(g);
                }
            }

            if (plmr.plmr_geometry != "BLOCK")
            {
                
                 plmr.Subunits[0].SRUs.AddRange(plmr.SRUs);

            }
            //need to run this after all is done in order to have all the information about which SRUs have what attached to their head/tail ends
            foreach (PlmrStructuralModificationGroup mod in plmr.Modifications)
            {
                mod.Modification.Fragment.PopulateSRUConnections();
            }
            /*
           

            //create mods out of non-sru fragments
            foreach (SRU non_sru_frag in non_sru_frags)
            {
                int sru_connection_position = 0;
                var g = new PlmrStructuralModificationGroup(plmr.RootObject);
                g.Modification = new PlmrStructuralModification(plmr.RootObject);

                //need to get fragment ids of fragments it connects to via fragment_connectivity
                non_sru_frag.parent_chains = get_chains_by_frag_ids(non_sru_frag.connected_fragment_ids, plmr);

                //need to create mod for each chain all referencing same fragment
                foreach (Chain parent_chain in non_sru_frag.parent_chains)
                {
                    g.Modification.Fragment = non_sru_frag;
                    g.Modification.Amount = new Amount(1);
                    g.Amount = g.Modification.Amount;
                    PlmrSite site = new PlmrSite(plmr.RootObject, "Structural Repeat Unit Substitution Site");
                    SRU.Connector end_group_frag_connector = new SRU.Connector();

                    if (non_sru_frag.Type == "Head end")
                    {
                        parent_chain.head_present = true;
                        //positionNumber1 = 0;
                        sru_connection_position = 1;
                        end_group_frag_connector.Snip = new Tuple<int, int>(1, 0);

                    }
                    else if (non_sru_frag.Type == "Tail end")
                    {
                        parent_chain.tail_present = true;
                        //positionNumber1 = 0;
                        sru_connection_position = -1;
                        //positionNumber2 = -1;
                        end_group_frag_connector.Snip = new Tuple<int, int>(1, 0);
                    }
                    else if (non_sru_frag.Type == "F")
                    {
                        //positionNumber1 = 0;
                        sru_connection_position = 1;
                        //positionNumber2 = -1;
                        end_group_frag_connector.Snip = new Tuple<int, int>(1, 0);
                    }
                    else if (non_sru_frag.Type == "Disconnected")
                    {
                        //positionNumber1 = 0;
                        sru_connection_position = 0;
                        //positionNumber2 = -1;
                        end_group_frag_connector.Snip = new Tuple<int, int>(1, 0);
                    }
                    site.Position = sru_connection_position;
                    site.ConnectorRef = end_group_frag_connector;
                    if (non_sru_frag.Type != "Disconnected")
                    {
                        site.Chain = parent_chain;
                    }
                    site.Code = "C132923";
                    site.CodeSystem = "2.16.840.1.113883.3.26.1.1";
                    site.DisplayName = "Structural Repeat Unit Substitution Site";
                    plmr.Modifications.Add(g);
                    g.PolymerSites.Add(site);
                }
                
            }
            */

        }

        public static List<Chain> get_chains_by_frag_ids(int[] fragment_ids, Plmr plmr)
        {
            List<Chain> returnvalue = new List<Chain>();
            foreach (Chain chain in plmr.Subunits)
            {
                foreach (int fragment_id in fragment_ids)
                {
                    if (chain.sru_fragment_id == fragment_id)
                    {
                        returnvalue.Add(chain);
                    }
                }
            }
            return returnvalue;
        }
        public static PolymerUnit get_polymer_unit_by_frag_id(int fragment_id, List<PolymerUnit> plmrunits)
        {
            foreach (PolymerUnit plmrunit in plmrunits)
            {
                if (plmrunit.getFragmentIds().Contains(fragment_id))
                {
                    return plmrunit;
                }
            }
            return null;
        }

        public static Chain get_chain_by_frag_id(int fragment_id, Plmr plmr)
        {
            Chain returnvalue = null;
            foreach (Chain chain in plmr.Subunits)
            {
                
                if (chain.sru_fragment_id == fragment_id)
                {
                    returnvalue = chain;
                }
                
            }
            return returnvalue;
        }
        public static void Moietize_old(this Plmr plmr)
        {
            //plmr.Units = PolymerParser.instance().decompose(plmr.Sdf.Mol);
            //plmr.SRUs = plmr.CreateSRUs();
            List<SRU> linear_srus = new List<SRU>();
            //List<Chain> chains = new List<Chain>();
            //Chain chain = new Chain(plmr.RootObject);
            //chain.Code = "C48803";
            //chain.CodeSystem = "2.16.840.1.113883.3.26.1.1";
            //chain.DisplayName = "POLYMER";
            //string end_group_type = "";
            //int positionNumber1 = 0;
            //int positionNumber2 = 0;
            
            int sru_connection_position = 0;
            String polymer_index = "0";
            Chain chain = null;
            String polymer_tool_options = "";
            int num_subunits = 0;
            int manual_chain_index = 1;
            bool linear_block_copolymer = false;
            bool linear_random_copolymer = false;
            if (plmr.plmr_class == "COPOLYMER" && plmr.plmr_subclass == "RANDOM" && plmr.plmr_geometry == "LINEAR") { linear_random_copolymer = true; }
            if (plmr.plmr_class == "COPOLYMER" && plmr.plmr_subclass == "BLOCK" && plmr.plmr_geometry == "LINEAR") { linear_block_copolymer = true; }
            if (plmr.plmr_geometry=="BRANCHED") { polymer_tool_options = "--branched"; }
            foreach (PolymerUnit plmrUnit in PolymerParser.instance(polymer_tool_options).decompose(plmr.Sdf.Mol))
            {
                String plmr_error = plmrUnit.getError();
                if (plmr_error != "")
                {
                    throw new SrsException("invalid_mol", plmr_error);
                }
                if (plmrUnit.getLabels().Count == 0 && plmrUnit.getFragmentType() == "disconnected")
                {
                    throw new SrsException("invalid_mol", "Unlabeled disconnected fragment " + plmrUnit.getMolecule().SMILES + " encountered in a polymer. This is not allowed.");
                }
                if (!linear_block_copolymer && !linear_random_copolymer)
                {
                    if (plmrUnit.getPolymerLabel() != polymer_index && plmrUnit.getFragmentType() != "disconnected")
                    {
                        if (polymer_index != "0")
                        {
                            plmr.Subunits.Add(chain);
                            num_subunits++;
                        }
                        polymer_index = plmrUnit.getPolymerLabel();
                        chain = new Chain(plmr.RootObject) { Code = "C48803", CodeSystem = "2.16.840.1.113883.3.26.1.1", DisplayName = "POLYMER", Ordinal = Int32.Parse(polymer_index), head_present = false, tail_present = false };
                        linear_srus = new List<SRU>();
                    }
                } else if (linear_random_copolymer && manual_chain_index==1)
                {
                    chain = new Chain(plmr.RootObject) { Code = "C48803", CodeSystem = "2.16.840.1.113883.3.26.1.1", DisplayName = "POLYMER", Ordinal = manual_chain_index, head_present = false, tail_present = false };
                    linear_srus = new List<SRU>();
                }

                if (plmrUnit.getFragmentType() == "linear sru" || plmrUnit.getFragmentType() == "branched sru")
                {
                    if (linear_block_copolymer && manual_chain_index != Int32.Parse(polymer_index))
                    {
                        if (polymer_index != "0")
                        {
                            plmr.Subunits.Add(chain);
                            num_subunits++;
                        }
                        polymer_index = manual_chain_index.ToString();
                        chain = new Chain(plmr.RootObject) { Code = "C48803", CodeSystem = "2.16.840.1.113883.3.26.1.1", DisplayName = "POLYMER", Ordinal = Int32.Parse(polymer_index), head_present = false, tail_present = false };
                        linear_srus = new List<SRU>();
                    }
                    //YP assuming here that the SRU_LABELS is intended to store just one label
                    if (plmr.plmr_subclass == "BLOCK")
                    {
                        foreach (int fragment_id in plmrUnit.getFragmentIds())
                        {
                            SRU linear_sru = new SRU(plmrUnit, plmr.RootObject, fragment_id: fragment_id) { parent_chain = chain, SRULabels = (plmrUnit.getLabels().Count() > 0 ? plmrUnit.getLabels() : null) };
                            linear_sru.UndefinedAmount = true;
                            //YP I don't know why the above line removes M CHG lines but the next line will hopefully restore it (SRS-400)
                            linear_sru.Mol = plmrUnit.getMol();
                            manual_chain_index++;
                            linear_srus.Add(linear_sru);
                            if (!linear_random_copolymer)
                            {
                                chain.SRUs = linear_srus;
                            }
                        }
                        
                    }
                    else
                    {
                        SRU linear_sru = new SRU(plmrUnit, plmr.RootObject) { parent_chain = chain, SRULabels = (plmrUnit.getLabels().Count() > 0 ? plmrUnit.getLabels() : null) };
                        linear_sru.UndefinedAmount = true;
                        //YP I don't know why the above line removes M CHG lines but the next line will hopefully restore it (SRS-400)
                        linear_sru.Mol = plmrUnit.getMol();
                        manual_chain_index++;
                        linear_srus.Add(linear_sru);
                        if (!linear_random_copolymer)
                        {
                            chain.SRUs = linear_srus;
                        }
                    }
                    
                    

                }
                else
                {
                    var g = new PlmrStructuralModificationGroup(plmr.RootObject);
                    g.Modification = new PlmrStructuralModification(plmr.RootObject);
                    SRU end_group_frag = new SRU(plmrUnit, plmr.RootObject) {parent_chain=chain, SRULabels = (plmrUnit.getLabels().Count() > 0 ? plmrUnit.getLabels() : null) };

                    //end_group_frag.Code = end_group_type;
                    g.Modification.Fragment = end_group_frag;
                    g.Modification.Amount = new Amount(1);
                    g.Amount = g.Modification.Amount;
                    PlmrSite site = new PlmrSite(plmr.RootObject, "Structural Repeat Unit Substitution Site");
                    SRU.Connector end_group_frag_connector = new SRU.Connector();

                    if (plmrUnit.getFragmentType() == "head end group")
                    {
                        chain.head_present = true;
                        //positionNumber1 = 0;
                        sru_connection_position = 1;
                        end_group_frag_connector.Snip = new Tuple<int, int>(1, 0);

                    }
                    else if (plmrUnit.getFragmentType() == "tail end group")
                    {
                        chain.tail_present = true;
                        //positionNumber1 = 0;
                        sru_connection_position = -1;
                        //positionNumber2 = -1;
                        end_group_frag_connector.Snip = new Tuple<int, int>(1, 0);
                    }
                    else if (plmrUnit.getFragmentType() == "connection")
                    {
                        //positionNumber1 = 0;
                        sru_connection_position = 1;
                        //positionNumber2 = -1;
                        end_group_frag_connector.Snip = new Tuple<int, int>(1, 0);
                    }
                    else if (plmrUnit.getFragmentType() == "disconnected")
                    {
                        //positionNumber1 = 0;
                        sru_connection_position = 0;
                        //positionNumber2 = -1;
                        end_group_frag_connector.Snip = new Tuple<int, int>(1, 0);
                    }
                    site.Position = sru_connection_position;
                    site.ConnectorRef = end_group_frag_connector;
                    if (plmrUnit.getFragmentType() != "disconnected")
                    {
                        site.Chain = chain;
                    }
                    site.Code = "C132923";
                    site.CodeSystem = "2.16.840.1.113883.3.26.1.1";
                    site.DisplayName = "Structural Repeat Unit Substitution Site";
                    plmr.Modifications.Add(g);
                    g.PolymerSites.Add(site);
                } 
            }
            if (linear_random_copolymer) { chain.SRUs = linear_srus; }
            plmr.Subunits.Add(chain);
            num_subunits++;

            

        }
    }
}
