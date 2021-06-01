using FDA.SRS.ObjectModel;
using FDA.SRS.Services;
using FDA.SRS.Utils;
//using FDA.SRS.Utils.SDFUtils;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace FDA.SRS.Processing
{
	public class PolymerBaseReadingState
	{
		public SplObject RootObject;
		public bool ExternalMolUsed;
		public Dictionary<string, Fragment> FragmentsCache = new Dictionary<string, Fragment>();
        public Dictionary<string, NAFragment> NAFragmentsCache = new Dictionary<string, NAFragment>();
        public bool CachedFragment;
	}

	public static class PolymerBaseExtensions
	{
		private static FragmentFactory _fragmentFactory = new FragmentFactory();
		public static FragmentFactory FragmentFactory
		{
			get { return _fragmentFactory; }
		}

        private static NAFragmentFactory _nafragmentFactory = new NAFragmentFactory();
        public static NAFragmentFactory NAFragmentFactory
        {
            get { return _nafragmentFactory; }
        }
        /// <summary>
        /// Read Fragment definition from MOLECULAR_FRAGMENT_MOIETY element of STRUCTURAL_MODIFICATION_GROUP
        /// </summary>
        public static StructuralModification ReadStructuralModification(this PolymerBase polymer, StructuralModification m, XElement xStrMod, PolymerBaseReadingState state)
		{
			polymer.ReadProteinModification(m, xStrMod, state);
			SRSReadingUtils.readElement(xStrMod, "STRUCTURAL_MODIFICATION_TYPE", ValidatedValues.StructureModificationTypes.Keys.Contains, v => m.ModificationType = v, polymer.UNII);
			SRSReadingUtils.readElement(xStrMod, "MOLECULAR_FRAGMENT_NAME", null, v => m.Name = v, polymer.UNII);
			SRSReadingUtils.readElement(xStrMod, "MOLECULAR_FRAGMENT_ID", v => v.Length == 10, v => m.UNII = v, polymer.UNII);
			SRSReadingUtils.readElement(xStrMod, "MOLECULAR_FRAGMENT_INCHI", v => v.StartsWith("InChI=1"), v => m.InChI = v, polymer.UNII);

			state.CachedFragment = false;
			XElement xMOL = xStrMod.Element("MOLFILE");
			if ( xMOL != null ) {
				XAttribute xExt = xMOL.Attribute("external");
				if ( xExt != null && xExt.Value == "1" ) {
					if ( String.IsNullOrEmpty(polymer.Sdf.Mol) )
						throw new SrsException("external_mol", "External MOL record is referenced from SRS XML, but not present in the record");

					// If this is the second reference to external MOL - use previously initialized fragment as it may alrwady contain connectors
					if ( state.FragmentsCache.ContainsKey("[external]") ) {
						m.Fragment = state.FragmentsCache["[external]"];
						state.CachedFragment = true;
					}
					else {
						m.Mol = polymer.Sdf.Mol;
						validateStructuralModificationMol(m);
					}
					state.ExternalMolUsed = true;
				}
				else if (MoleculeExtensions.IsCorrectMol(xMOL.Value) ) {
					m.Mol = xMOL.Value;
					validateStructuralModificationMol(m);
				}
				else if ( String.IsNullOrWhiteSpace(m.Name) && String.IsNullOrWhiteSpace(m.UNII) ) {
					throw new SrsException("internal_mol", "Missing or incorrect internal MOL in SRS XML record");
				}
			}

			// Anything to Molecule resolution
			if ( m.Fragment == null && !String.IsNullOrEmpty(m.Mol) ) {
				m.Fragment = new Fragment(m.RootObject) { Molecule = new SDFUtil.NewMolecule(m.Mol) };
				if ( state.ExternalMolUsed )
					state.FragmentsCache.Add("[external]", m.Fragment);
				TraceUtils.WriteUNIITrace(TraceEventType.Information, polymer.UNII, null, "Fragment resolved from MOL");
			}

			if ( m.Fragment == null && !String.IsNullOrEmpty(m.UNII) ) {
				string term = m.UNII.ToLower();
				if ( state.FragmentsCache.ContainsKey(term) ) {
					m.Fragment = state.FragmentsCache[term];
					state.CachedFragment = true;
				}
				else {
					m.Fragment = FragmentFactory.Resolve(m.UNII, m.RootObject);
					if ( m.Fragment != null ) {
						m.Fragment = m.Fragment.Clone();
						state.FragmentsCache.Add(term, m.Fragment);
						TraceUtils.WriteUNIITrace(TraceEventType.Information, polymer.UNII, null, "Fragment resolved from ({0})", term);
					}
				}
			}

			if ( m.Fragment == null && !String.IsNullOrEmpty(m.Name) ) {
				string term = m.Name.ToLower();
				if ( state.FragmentsCache.ContainsKey(term) ) {
					m.Fragment = state.FragmentsCache[term];
					state.CachedFragment = true;
				}
				else {
					m.Fragment = FragmentFactory.Resolve(m.Name, m.RootObject);
					if ( m.Fragment != null ) {
						m.Fragment = m.Fragment.Clone();
						state.FragmentsCache.Add(term, m.Fragment);
						TraceUtils.WriteUNIITrace(TraceEventType.Information, polymer.UNII, null, "Fragment resolved from ({0})", term);
					}
				}
			}

			if ( m.Fragment == null )
				TraceUtils.WriteUNIITrace(TraceEventType.Error, polymer.UNII, null, "Fragment cannot be resolved");
			else {
				// Connection points - if specified in SRS XML - override those from resolver
				string conn = null;
				SRSReadingUtils.readElement(xStrMod, "FRAGMENT_CONNECTIVITY", null, v => conn = v, polymer.UNII);
				if ( String.IsNullOrWhiteSpace(conn) )	// Ad-hoc misspelling fix in SRS XML
					SRSReadingUtils.readElement(xStrMod, "FRAGMENT_CONNECTIVTY", null, v => conn = v, polymer.UNII);

				if ( String.IsNullOrWhiteSpace(conn) )
					TraceUtils.WriteUNIITrace(TraceEventType.Warning, polymer.UNII, null, "Connection points are not specified - using previously collected ones");
				else {
					IList<int> iConns = conn
						.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
						.Select(s => int.Parse(s.Trim()))
						.ToList();
					if ( iConns.Count % 2 != 0 )
						TraceUtils.WriteUNIITrace(TraceEventType.Warning, polymer.UNII, null, "Odd connection points - only first 0, 2, 4, etc will be used: {0}", conn);
					if ( iConns.Count < 2 )
						TraceUtils.WriteUNIITrace(TraceEventType.Warning, polymer.UNII, null, "Malformed connection points entry: {0}", conn);
					else {
						// For cached fragments we assume that it's a repetition to add connection points to the OTHER_LINKAGE fragment
						if ( !state.CachedFragment ) {
							// Override what otherwise might have been specified elsewhere
							m.Fragment.Connectors.Clear();
						}

						for ( int i = 0; i < iConns.Count; i += 2 )
							m.Fragment.AddConnectorsPair(iConns[i], iConns[i + 1], polymer.UNII);
					}
				}

				m.Fragment.Parent = m;
			}

			return m;
		}

        public static NAStructuralModification ReadNAStructuralModification(this PolymerBase polymer, NAStructuralModification m, XElement xStrMod, PolymerBaseReadingState state)
        {
            polymer.ReadNAModification(m, xStrMod, state);
            SRSReadingUtils.readElement(xStrMod, "STRUCTURAL_MODIFICATION_TYPE", ValidatedValues.NAStructureModificationTypes.Keys.Contains, v => m.ModificationType = v, polymer.UNII);
            SRSReadingUtils.readElement(xStrMod, "MOLECULAR_FRAGMENT_NAME", null, v => m.Name = v, polymer.UNII);
            SRSReadingUtils.readElement(xStrMod, "MOLECULAR_FRAGMENT_ID", v => v.Length == 10, v => m.UNII = v, polymer.UNII);
            SRSReadingUtils.readElement(xStrMod, "MOLECULAR_FRAGMENT_INCHI", v => v.StartsWith("InChI=1"), v => m.InChI = v, polymer.UNII);

            state.CachedFragment = false;
            XElement xMOL = xStrMod.Element("MOLFILE");
            if (xMOL != null)
            {
                XAttribute xExt = xMOL.Attribute("external");
                if (xExt != null && xExt.Value == "1")
                {
                    if (String.IsNullOrEmpty(polymer.Sdf.Mol))
                        throw new SrsException("external_mol", "External MOL record is referenced from SRS XML, but not present in the record");

                    // If this is the second reference to external MOL - use previously initialized fragment as it may alrwady contain connectors
                    if (state.FragmentsCache.ContainsKey("[external]"))
                    {
                        m.Fragment = state.NAFragmentsCache["[external]"];
                        state.CachedFragment = true;
                    }
                    else
                    {
                        m.Mol = polymer.Sdf.Mol;
                        validateNAStructuralModificationMol(m);
                    }
                    state.ExternalMolUsed = true;
                }
                else if (MoleculeExtensions.IsCorrectMol(xMOL.Value))
                {
                    m.Mol = xMOL.Value;
                    validateNAStructuralModificationMol(m);
                }
                else if (String.IsNullOrWhiteSpace(m.Name) && String.IsNullOrWhiteSpace(m.UNII))
                {
                    throw new SrsException("internal_mol", "Missing or incorrect internal MOL in SRS XML record");
                }
            }

            // Anything to Molecule resolution
            if (m.Fragment == null && !String.IsNullOrEmpty(m.Mol))
            {
                m.Fragment = new NAFragment(m.RootObject) { Molecule = new SDFUtil.NewMolecule(m.Mol) };
                if (state.ExternalMolUsed)
                    state.NAFragmentsCache.Add("[external]", m.Fragment);
                TraceUtils.WriteUNIITrace(TraceEventType.Information, polymer.UNII, null, "Fragment resolved from MOL");
            }

            if (m.Fragment == null && !String.IsNullOrEmpty(m.UNII))
            {
                string term = m.UNII.ToLower();
                if (state.FragmentsCache.ContainsKey(term))
                {
                    m.Fragment = state.NAFragmentsCache[term];
                    state.CachedFragment = true;
                }
                else
                {
                    m.Fragment = NAFragmentFactory.Resolve(m.UNII, m.RootObject);
                    if (m.Fragment != null)
                    {
                        m.Fragment = m.Fragment.Clone();
                        state.NAFragmentsCache.Add(term, m.Fragment);
                        TraceUtils.WriteUNIITrace(TraceEventType.Information, polymer.UNII, null, "Fragment resolved from ({0})", term);
                    }
                }
            }

            if (m.Fragment == null && !String.IsNullOrEmpty(m.Name))
            {
                string term = m.Name.ToLower();
                if (state.FragmentsCache.ContainsKey(term))
                {
                    m.Fragment = state.NAFragmentsCache[term];
                    state.CachedFragment = true;
                }
                else
                {
                    m.Fragment = NAFragmentFactory.Resolve(m.Name, m.RootObject);
                    if (m.Fragment != null)
                    {
                        m.Fragment = m.Fragment.Clone();
                        state.NAFragmentsCache.Add(term, m.Fragment);
                        TraceUtils.WriteUNIITrace(TraceEventType.Information, polymer.UNII, null, "Fragment resolved from ({0})", term);
                    }
                }
            }

            if (m.Fragment == null)
                TraceUtils.WriteUNIITrace(TraceEventType.Error, polymer.UNII, null, "Fragment cannot be resolved");
            else
            {
                // Connection points - if specified in SRS XML - override those from resolver
                string conn = null;
                SRSReadingUtils.readElement(xStrMod, "FRAGMENT_CONNECTIVITY", null, v => conn = v, polymer.UNII);
                if (String.IsNullOrWhiteSpace(conn))    // Ad-hoc misspelling fix in SRS XML
                    SRSReadingUtils.readElement(xStrMod, "FRAGMENT_CONNECTIVTY", null, v => conn = v, polymer.UNII);

                if (String.IsNullOrWhiteSpace(conn))
                    TraceUtils.WriteUNIITrace(TraceEventType.Warning, polymer.UNII, null, "Connection points are not specified - using previously collected ones");
                else
                {
                    IList<int> iConns = conn
                        .Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(s => int.Parse(s.Trim()))
                        .ToList();
                    if (iConns.Count % 2 != 0)
                        TraceUtils.WriteUNIITrace(TraceEventType.Warning, polymer.UNII, null, "Odd connection points - only first 0, 2, 4, etc will be used: {0}", conn);
                    if (iConns.Count < 2)
                        TraceUtils.WriteUNIITrace(TraceEventType.Warning, polymer.UNII, null, "Malformed connection points entry: {0}", conn);
                    else
                    {
                        // For cached fragments we assume that it's a repetition to add connection points to the OTHER_LINKAGE fragment
                        if (!state.CachedFragment)
                        {
                            // Override what otherwise might have been specified elsewhere
                            m.Fragment.Connectors.Clear();
                        }

                        for (int i = 0; i < iConns.Count; i += 2)
                            m.Fragment.AddConnectorsPair(iConns[i], iConns[i + 1], polymer.UNII);
                    }
                }

                m.Fragment.Parent = m;
            }

            return m;
        }

        /// <summary>
		/// Read Fragment definition from JSON elements of GSRS JSON
		/// </summary>
		public static StructuralModification ReadStructuralModificationJson(this PolymerBase polymer, StructuralModification m, JToken jStrMod, PolymerBaseReadingState state)
        {

            m.Amount=polymer.ReadAmountJson(jStrMod.SelectToken("extentAmount"), state);
            
            //structuralModificationType
            //TODO: There is almost no way this is right
            SRSReadingUtils.readJsonElement(jStrMod, "$..moleculareFragmentRole", ValidatedValues.StructureModificationTypes.Keys.Contains, v => m.Role = v, polymer.UNII);

            SRSReadingUtils.readJsonElement(jStrMod, "structuralModificationType", ValidatedValues.StructureModificationTypes.Keys.Contains, v => m.ModificationType = v, polymer.UNII);
            SRSReadingUtils.readJsonElement(jStrMod, "molecularFragment.refPname", null, v => m.Name = v, polymer.UNII);
            SRSReadingUtils.readJsonElement(jStrMod, "molecularFragment.approvalID", v => v.Length == 10, v => m.UNII = v, polymer.UNII);

            //TODO?
            //SRSReadingUtils.readElement(xStrMod, "MOLECULAR_FRAGMENT_INCHI", v => v.StartsWith("InChI=1"), v => m.InChI = v, polymer.UNII);

            state.CachedFragment = false;
            
            if (m.Fragment == null && !String.IsNullOrEmpty(m.UNII)){
                string term = m.UNII.ToLower();
                if (state.FragmentsCache.ContainsKey(term)){
                    m.Fragment = state.FragmentsCache[term];
                    state.CachedFragment = true;
                } else{
                    m.Fragment = FragmentFactory.Resolve(m.UNII, m.RootObject);
                    if (m.Fragment != null){
                        m.Fragment = m.Fragment.Clone();
                        state.FragmentsCache.Add(term, m.Fragment);
                        TraceUtils.WriteUNIITrace(TraceEventType.Information, polymer.UNII, null, "Fragment resolved from ({0})", term);
                    }
                }
            }

            if (m.Fragment == null && !String.IsNullOrEmpty(m.Name)){
                string term = m.Name.ToLower();
                if (state.FragmentsCache.ContainsKey(term)){
                    m.Fragment = state.FragmentsCache[term];
                    state.CachedFragment = true;
                }else{
                    m.Fragment = FragmentFactory.Resolve(m.Name, m.RootObject);
                    if (m.Fragment != null){
                        m.Fragment = m.Fragment.Clone();
                        state.FragmentsCache.Add(term, m.Fragment);
                        TraceUtils.WriteUNIITrace(TraceEventType.Information, polymer.UNII, null, "Fragment resolved from ({0})", term);
                    }
                }
            }

            if (m.Fragment == null) {
                TraceUtils.WriteUNIITrace(TraceEventType.Error, polymer.UNII, null, "Fragment cannot be resolved");
            } else {
                // Connection points - if specified in SRS XML - override those from resolver
                List<Tuple<int,int>> iConns = polymer._getFragmentConnectorsFor(m.Fragment);

                //TODO: Sometimes read connection points?
                //This will sometimes be necessary, just for knowing which end is which
                //1. Short term is to guess based on registry (done, imperfect)
                //2. Longer-term is to specify which sites go with each residue (won't work with assymetric forms)


                //Will always be true
                if (iConns == null) { 
                    TraceUtils.WriteUNIITrace(TraceEventType.Warning, polymer.UNII, null, "Connection points are not specified - using previously collected ones");
                    state.CachedFragment = true;
                } else{
                    // For cached fragments we assume that it's a repetition to add connection points to the OTHER_LINKAGE fragment
                    if (!state.CachedFragment){
                        // Override what otherwise might have been specified elsewhere
                        m.Fragment.Connectors.Clear();
                    }

                    for(int i = 0; i < iConns.Count; i++) {
                        m.Fragment.AddConnectorsPairCanonical(iConns[i].Item1, iConns[i].Item2, polymer.UNII);
                    }   
                }
                m.Fragment.Parent = m;
            }

            return m;
        }

        public static NAStructuralModification ReadNAStructuralModificationJson(this PolymerBase polymer, NAStructuralModification m, JToken jStrMod, PolymerBaseReadingState state)
        {

            m.Amount = polymer.ReadAmountJson(jStrMod.SelectToken("extentAmount"), state);

            //structuralModificationType
            //TODO: There is almost no way this is right
            SRSReadingUtils.readJsonElement(jStrMod, "$..moleculareFragmentRole", ValidatedValues.NAStructureModificationTypes.Keys.Contains, v => m.Role = v, polymer.UNII);

            SRSReadingUtils.readJsonElement(jStrMod, "structuralModificationType", ValidatedValues.NAStructureModificationTypes.Keys.Contains, v => m.ModificationType = v, polymer.UNII);
            SRSReadingUtils.readJsonElement(jStrMod, "molecularFragment.refPname", null, v => m.Name = v, polymer.UNII);
            SRSReadingUtils.readJsonElement(jStrMod, "molecularFragment.approvalID", v => v.Length == 10, v => m.UNII = v, polymer.UNII);

            //TODO?
            //SRSReadingUtils.readElement(xStrMod, "MOLECULAR_FRAGMENT_INCHI", v => v.StartsWith("InChI=1"), v => m.InChI = v, polymer.UNII);

            state.CachedFragment = false;


            if (m.Fragment == null && !String.IsNullOrEmpty(m.UNII))
            {
                string term = m.UNII.ToLower();
                if (state.FragmentsCache.ContainsKey(term))
                {
                    m.Fragment = state.NAFragmentsCache[term];
                    state.CachedFragment = true;
                }
                else
                {
                    m.Fragment = NAFragmentFactory.Resolve(m.UNII, m.RootObject);
                    if (m.Fragment != null)
                    {
                        m.Fragment = m.Fragment.Clone();
                        state.NAFragmentsCache.Add(term, m.Fragment);
                        TraceUtils.WriteUNIITrace(TraceEventType.Information, polymer.UNII, null, "Fragment resolved from ({0})", term);
                    }
                }
            }

            if (m.Fragment == null && !String.IsNullOrEmpty(m.Name))
            {
                string term = m.Name.ToLower();
                if (state.FragmentsCache.ContainsKey(term))
                {
                    m.Fragment = state.NAFragmentsCache[term];
                    state.CachedFragment = true;
                }
                else
                {
                    m.Fragment = NAFragmentFactory.Resolve(m.Name, m.RootObject);
                    if (m.Fragment != null)
                    {
                        m.Fragment = m.Fragment.Clone();
                        state.NAFragmentsCache.Add(term, m.Fragment);
                        TraceUtils.WriteUNIITrace(TraceEventType.Information, polymer.UNII, null, "Fragment resolved from ({0})", term);
                    }
                }
            }

            if (m.Fragment == null)
            {
                TraceUtils.WriteUNIITrace(TraceEventType.Error, polymer.UNII, null, "Fragment cannot be resolved");
            }
            else
            {
                // Connection points - if specified in SRS XML - override those from resolver
                List<Tuple<int, int>> iConns = polymer._getNAFragmentConnectorsFor(m.Fragment);

                //TODO: Sometimes read connection points?
                //This will sometimes be necessary, just for knowing which end is which
                //1. Short term is to guess based on registry (done, imperfect)
                //2. Longer-term is to specify which sites go with each residue (won't work with assymetric forms)


                //Will always be true
                if (iConns == null)
                {
                    TraceUtils.WriteUNIITrace(TraceEventType.Warning, polymer.UNII, null, "Connection points are not specified - using previously collected ones");
                    state.CachedFragment = true;
                }
                else
                {
                    // For cached fragments we assume that it's a repetition to add connection points to the OTHER_LINKAGE fragment
                    if (!state.CachedFragment)
                    {
                        // Override what otherwise might have been specified elsewhere
                        m.Fragment.Connectors.Clear();
                    }

                    for (int i = 0; i < iConns.Count; i++)
                    {
                        m.Fragment.AddConnectorsPair(iConns[i].Item1, iConns[i].Item2, polymer.UNII);
                    }
                }
                m.Fragment.Parent = m;
            }

            return m;
        }


        private static void validateStructuralModificationMol(StructuralModification m)
		{
			var nm = new SDFUtil.NewMolecule(m.Mol);
			if ( String.IsNullOrEmpty(nm.InChI) ) {
				if ( !nm.Mol.Contains("V3000") )
					nm.Mol = m.Mol.FixMolSpaces();
				else if ( !SplOptions.ConvertOptions.Features.Has("allow-lossy-v3000-v2000") )
					throw new SrsException("invalid_mol", "MOL is in V3000 format, no information-less conversion exists and 'allow-lossy-v3000-v2000' option is not provided");
				else
					nm.AllowV2000Downsize = true;	// Allow lossy conversion

				// Still cannot calculate InChI - something wrong
                //GALOG
				if ( String.IsNullOrEmpty(nm.InChI) )
					//throw new SrsException("internal_mol", "Incorrect internal MOL in SRS XML record", new Exception("", nm.Mol));
                    throw new SrsException("internal_mol", "Incorrect internal MOL in SRS XML record" );

                m.Mol = nm.Mol;
			}
		}

        private static void validateNAStructuralModificationMol(NAStructuralModification m)
        {
            var nm = new SDFUtil.NewMolecule(m.Mol);
            if (String.IsNullOrEmpty(nm.InChI))
            {
                if (!nm.Mol.Contains("V3000"))
                    nm.Mol = m.Mol.FixMolSpaces();
                else if (!SplOptions.ConvertOptions.Features.Has("allow-lossy-v3000-v2000"))
                    throw new SrsException("invalid_mol", "MOL is in V3000 format, no information-less conversion exists and 'allow-lossy-v3000-v2000' option is not provided");
                else
                    nm.AllowV2000Downsize = true;   // Allow lossy conversion

                // Still cannot calculate InChI - something wrong
                if (String.IsNullOrEmpty(nm.InChI))
                    throw new SrsException("internal_mol", "Incorrect internal MOL in SRS XML record: " + nm.Mol);

                m.Mol = nm.Mol;
            }
        }

        public static AgentModification ReadAgentModification(this PolymerBase polymer, XElement xAgentMod, PolymerBaseReadingState state)
		{
			AgentModification m = new AgentModification(state.RootObject);
			polymer.ReadProteinModification(m, xAgentMod, state);
			SRSReadingUtils.readElement(xAgentMod, "AGENT_MODIFICATION_TYPE", ValidatedValues.AgentModificationTypes.Keys.Contains, v => m.ModificationType = v, polymer.UNII);
			SRSReadingUtils.readElement(xAgentMod, "MODIFICATION_AGENT", null, v => m.Agent = v, polymer.UNII);
			SRSReadingUtils.readElement(xAgentMod, "MODIFICATION_AGENT_ID", null, v => m.AgentId = v, polymer.UNII);
			return m;
		}

		// TODO: don't like this partial tolerance...
		public static Amount ReadAmount(this PolymerBase polymer, XElement xAmount, PolymerBaseReadingState state)
		{
			Amount a = new Amount {  };	// See SRS-208 for (hopefully) explanation
			SRSReadingUtils.readElement(xAmount, "AMOUNT_TYPE", ValidatedValues.AmountTypes.Keys.Contains, v => a.SrsAmountType = v, polymer.UNII);
			SRSReadingUtils.readElement(xAmount, "AMOUNT/AVERAGE", null, v => {
				var m = Regex.Match(v, @"(\d+)\s*/\s*(\d+)");
				if ( m.Success ) {
					a.Numerator = double.Parse(m.Groups[1].Value);
					a.Denominator = double.Parse(m.Groups[2].Value);
				}
				else {
					try { a.Numerator = double.Parse(v); }
					catch ( FormatException ex ) {
						TraceUtils.ReportError("amount", polymer.UNII, "Cannot parse <AMOUNT>: {0}", ex.Message);
					}
				}
			}, polymer.UNII);
			SRSReadingUtils.readElement(xAmount, "AMOUNT/LOW_LIMIT", null, v => a.Low = double.Parse(v), polymer.UNII);
			SRSReadingUtils.readElement(xAmount, "AMOUNT/HIGH_LIMIT", null, v => a.High = double.Parse(v), polymer.UNII);
			SRSReadingUtils.readElement(xAmount, "AMOUNT/NON_NUMERIC_VALUE", null, v => a.NonNumericValue = v, polymer.UNII);
			SRSReadingUtils.readElement(xAmount, "AMOUNT/UNIT", ValidatedValues.Units.Keys.Contains, v => a.Unit = v, polymer.UNII);
			a.AdjustAmount();
			return a;
		}

        // TODO: don't like this partial tolerance...
        public static Amount ReadAmountJson(this PolymerBase polymer, JToken jAmount, PolymerBaseReadingState state){
            Amount a = new Amount { };  // See SRS-208 for (hopefully) explanation
            if(jAmount == null){
                a.AdjustAmount();
                return a;
            }
            SRSReadingUtils.readJsonElement(jAmount, "type", ValidatedValues.AmountTypes.Keys.Contains, v => a.SrsAmountType = v, polymer.UNII);
            SRSReadingUtils.readJsonElement(jAmount, "average", null, v => {
                var m = Regex.Match(v, @"(\d+)\s*/\s*(\d+)");
                if (m.Success)
                {
                    a.Numerator = double.Parse(m.Groups[1].Value);
                    a.Denominator = double.Parse(m.Groups[2].Value);
                    a.isDefaultDenominator = false;
                }
                else
                {
                    //YP SRS-372, populating Center here as well
                    //Julia asked to treat center as value (numerator), so commenting this out and only populating a.Numerator
                    //try { a.Numerator = a.Center = double.Parse(v);}
                    //YP SRS-372 setting amount to uncertain here per Julia's request during a call
                    //try { a.Numerator = double.Parse(v); a.AmountType = AmountType.UncertainZero; }
                    //YP SRS-372, Julia asked to revert the changes regarding treating center as numerator, so I'm commenting the change out, but keeping for records
                    //try { a.Center = double.Parse(v);}
                    //At last request per SRS-372 04/06/2020 if there is only average (no low and high) then don't use URG_PQ at all, basically amount is not uncertain if there is average
                    try { a.Numerator = double.Parse(v); }
                    catch (FormatException ex)
                    {
                        TraceUtils.ReportError("amount", polymer.UNII, "Cannot parse <AMOUNT>: {0}", ex.Message);
                    }
                }
            }, polymer.UNII);
            SRSReadingUtils.readJsonElement(jAmount, "low", null, v => a.Low = double.Parse(v), polymer.UNII);
            SRSReadingUtils.readJsonElement(jAmount, "high", null, v => a.High = double.Parse(v), polymer.UNII);

            
            if (a.Low == null){
                SRSReadingUtils.readJsonElement(jAmount, "lowLimit", null, v => a.Low = double.Parse(v), polymer.UNII);
            }
            if (a.High == null){
                SRSReadingUtils.readJsonElement(jAmount, "highLimit", null, v => a.High = double.Parse(v), polymer.UNII);
            }


            SRSReadingUtils.readJsonElement(jAmount, "nonNumericValue", null, v => a.NonNumericValue = v, polymer.UNII);
            SRSReadingUtils.readJsonElement(jAmount, "units", ValidatedValues.Units.Keys.Contains, v => a.Unit = v, polymer.UNII);
            a.AdjustAmount();

            //Note: This was changed to standardize units. This is not always a good idea,
            //since not all structural modifications have units like this.

            a.DenominatorUnit = "mol";
            return a;
        }

        public static MolecularWeight ReadMolecularWeight(this PolymerBase polymer, XElement xMW, PolymerBaseReadingState state){
			MolecularWeight mw = new MolecularWeight();
			SRSReadingUtils.readElement(xMW, "MOLECULAR_WEIGHT_TYPE", ValidatedValues.MWTypes.Keys.Contains, v => mw.WeightType = v, polymer.UNII);
			SRSReadingUtils.readElement(xMW, "MOLECULAR_WEIGHT_METHOD", ValidatedValues.MWMethods.Keys.Contains, v => mw.WeightMethod = v, polymer.UNII);
			mw.Amount = polymer.ReadAmount(xMW, state);
            //Default to Daltons
            if (mw.Amount.Unit == null || mw.Amount.Unit.Equals("mol")){
                mw.Amount.Unit = "DA";
            }
			return mw;
		}

		public static PhysicalModification ReadPhysicalModificationGroup(this PolymerBase polymer, XElement xPhysModGroup, PolymerBaseReadingState state)
		{
            //What about PARAMETERS?
            PhysicalModification m = new PhysicalModification(state.RootObject);
			polymer.ReadProteinModification(m, xPhysModGroup, state);
			return m;
		}

        public static NAPhysicalModification ReadNAPhysicalModificationGroup(this PolymerBase polymer, XElement xPhysModGroup, PolymerBaseReadingState state)
        {
            //What about PARAMETERS?
            NAPhysicalModification m = new NAPhysicalModification(state.RootObject);
            polymer.ReadNAModification(m, xPhysModGroup, state);
            return m;
        }

        public static ProteinModification ReadProteinModification(this PolymerBase polymer, ProteinModification m, XElement xProteinMod, PolymerBaseReadingState state)
		{
            
			SRSReadingUtils.readElement(xProteinMod, "ROLE", ValidatedValues.StructureModificationTypes.Keys.Contains, v => m.Role = v, polymer.UNII);
			m.Amount = polymer.ReadAmount(xProteinMod, state);
			return m;
		}

        public static NAModification ReadNAModification(this PolymerBase polymer, NAModification m, XElement xProteinMod, PolymerBaseReadingState state)
        {

            SRSReadingUtils.readElement(xProteinMod, "ROLE", ValidatedValues.NAStructureModificationTypes.Keys.Contains, v => m.Role = v, polymer.UNII);
            m.Amount = polymer.ReadAmount(xProteinMod, state);
            return m;
        }
    }
}
