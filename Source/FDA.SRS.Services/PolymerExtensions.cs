using FDA.SRS.ObjectModel;
using FDA.SRS.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Xml.Linq;
using System.Xml.XPath;

namespace FDA.SRS.Processing
{
	public static class PolymerExtensions
	{
		public static Polymer ReadPolymer(this Polymer polymer, XElement xPolymer, SplObject rootObject, ConvertOptions options)
		{
			PolymerBaseReadingState state = new PolymerBaseReadingState { RootObject = rootObject };

			SRSReadingUtils.readElement(xPolymer, "POLYMER_CLASS", st => ValidatedValues.SequenceTypes.Keys.Contains(st), st => polymer.PolymerClass = st, polymer.UNII);
			SRSReadingUtils.readElement(xPolymer, "POLYMER_GEOMETRY", st => ValidatedValues.SequenceTypes.Keys.Contains(st), st => polymer.PolymerGeometry = st, polymer.UNII);
			SRSReadingUtils.readElement(xPolymer, "COPOLYMER_SEQUENCE_TYPE", st => ValidatedValues.SequenceTypes.Keys.Contains(st), st => polymer.CopolymerType = st, polymer.UNII);

			polymer.Fragments = new List<Fragment>();
			polymer.Modifications = new List<ProteinModification>();

			// Physical modifications
			xPolymer
				.XPathSelectElements("MODIFICATION_GROUP/PHYSICAL_MODIFICATION_GROUP")
				.ForAll(x => {
					if ( !String.IsNullOrWhiteSpace(x.Value) ) {
						var g = polymer.ReadPhysicalModificationGroup(x, state);
						if ( g != null )
							polymer.Modifications.Add(g);
					}
				});

			// Agent modifications
			xPolymer
				.XPathSelectElements("MODIFICATION_GROUP/AGENT_MODIFICATION_GROUP")
				.ForAll(x => {
					if ( !String.IsNullOrWhiteSpace(x.Value) ) {
						var g = polymer.ReadAgentModification(x, state);
						if ( g != null )
							polymer.Modifications.Add(g);
					}
				});

			// Structural modifications
			xPolymer
				.XPathSelectElements("MODIFICATION_GROUP/STRUCTURAL_MODIFICATION_GROUP")
				.ForAll(x => {
					if ( !String.IsNullOrWhiteSpace(x.Value) ) {
						var g = polymer.ReadStructuralModificationGroup(x, state);
						if ( g != null )
							polymer.Modifications.Add(g);
					}
				});

			// Molecular Weight
			var xMW = xPolymer.XPathSelectElement("MOLECULAR_WEIGHT");
			if ( xMW != null && !String.IsNullOrEmpty(xMW.Value) )
				polymer.MolecularWeight = polymer.ReadMolecularWeight(xMW, state);

			return polymer;
		}

		public static StructuralModificationGroup ReadStructuralModificationGroup(this Polymer polymer, XElement xStrModGroup, PolymerBaseReadingState state)
		{
			XElement xel = xStrModGroup.XPathSelectElement("RESIDUE_SITE");
			if ( xel == null || String.IsNullOrWhiteSpace(xel.Value) )
				throw new SrsException("mandatory_field_missing", "Residue sites are not specified - skipping the rest of modification");

			StructuralModificationGroup g = new StructuralModificationGroup(state.RootObject);
			
			SRSReadingUtils.readElement(xStrModGroup, "RESIDUE_MODIFIED", aa => AminoAcids.IsValidAminoAcidName(aa), v => g.Residue = v, polymer.UNII);

			if ( !String.IsNullOrEmpty(g.Residue) && g.ResidueSites.Any(s => !String.Equals(AminoAcids.GetNameByLetter(s.Letter), g.Residue, StringComparison.InvariantCultureIgnoreCase)) )
				TraceUtils.WriteUNIITrace(TraceEventType.Error, polymer.UNII, null, "Residue {0} does not match all positions", g.Residue);

			if ( String.IsNullOrEmpty(g.Residue) && g.ResidueSites.Count == 1 ) {
				ProteinSite site = g.ResidueSites.First();
				g.Residue = AminoAcids.GetNameByLetter(site.Letter);
				TraceUtils.WriteUNIITrace(TraceEventType.Information, polymer.UNII, null, "Residue restored from position: {0} => {1} => {2}", site, site.Letter, g.Residue);
			}

			xel = xStrModGroup.XPathSelectElement("MOLECULAR_FRAGMENT_MOIETY");
			if ( xel != null ) {
				StructuralModification m = new StructuralModification(state.RootObject);
				g.Modification = polymer.ReadStructuralModification(m, xel, state);
				if ( g.Modification == null )
					throw new SrsException("fragment", String.Format("Cannot read fragment: {0}", xel));
				if ( g.Modification.Fragment == null )
					throw new SrsException("fragment", String.Format("Cannot resolve fragment: {0}", xel));
				if ( !g.Modification.Fragment.IsLinker && !g.Modification.Fragment.IsModification )
					throw new SrsException("fragment", String.Format("Cannot define fragment connection points: {0}", xel));
			}

			return g;
		}

	}
}
