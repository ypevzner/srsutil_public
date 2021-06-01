using FDA.SRS.Utils;
using System;
using System.Collections.Generic;
using System.Linq;


namespace FDA.SRS.ObjectModel
{
	public abstract class PolymerBase : SrsObject
	{
		public SdfRecord Sdf { get; set; }

		public List<ProteinModification> Modifications { get; set; } = new List<ProteinModification>();

        public List<NAModification> NAModifications { get; set; } = new List<NAModification>();

        public List<Fragment> Fragments { get; set; } = new List<Fragment>();

        public List<NAFragment> NAFragments { get; set; } = new List<NAFragment>();

        public MolecularWeight MolecularWeight { get; set; }

        private List<Tuple<Predicate<Fragment>, List<Tuple<int, int>>>> _FragmentConnectorsCache { get; set; } = new List<Tuple<Predicate<Fragment>, List<Tuple<int, int>>>>();

        private List<Tuple<Predicate<NAFragment>, List<Tuple<int, int>>>> _NAFragmentConnectorsCache { get; set; } = new List<Tuple<Predicate<NAFragment>, List<Tuple<int, int>>>>();
        /*
        * Set the fragment connector order for the sites used to override whatever
        * values are set in the registry.
        * 
        * 
        */
        public void setFragmentConnectorsData(List<Tuple<Predicate<Fragment>, List<Tuple<int, int>>>> refCon) {
            _FragmentConnectorsCache = refCon;
        }

        public void setNAFragmentConnectorsData(List<Tuple<Predicate<NAFragment>, List<Tuple<int, int>>>> refCon)
        {
            _NAFragmentConnectorsCache = refCon;
        }

        /**
         *
         * Search for any connector pair lists associated with the fragment. 
         * Returns null if nothing is applicable. 
         */
        public List<Tuple<int,int>> _getFragmentConnectorsFor(Fragment f) {
            if (_FragmentConnectorsCache == null) return null;
            return (_FragmentConnectorsCache).Filter(t => t.Item1.Invoke(f))
                                      .Select(t => t.Item2)
                                      .FirstOrDefault();
        }

        public List<Tuple<int, int>> _getNAFragmentConnectorsFor(NAFragment f)
        {
            if (_NAFragmentConnectorsCache == null) return null;
            return (_NAFragmentConnectorsCache).Filter(t => t.Item1.Invoke(f))
                                      .Select(t => t.Item2)
                                      .FirstOrDefault();
        }
    }
}
