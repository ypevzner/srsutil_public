using FDA.SRS.ObjectModel;
using System;
using System.Collections.Generic;


#if USE_OPSIN
using uk.ac.cam.ch.wwmm.opsin;
#endif

namespace FDA.SRS.Services
{
    public class StructureResolverResult
	{
		public int Confidence;
		public Fragment Fragment;
	}

	public interface IStructureResolver
	{
		StructureResolverResult Resolve(string term);
	}

	public class StructureResolver : IStructureResolver
	{

#if USE_OPSIN
		private NameToStructure _OPSIN;
#endif

		[Flags]
		public enum ResolverFlags {
			UseOPSIN = 0x02,
		};

		public class ResolverOptions
		{
			public ResolverFlags Flags;
		}

		private Dictionary<Tuple<string, ResolverOptions>, StructureResolverResult> _cache = new Dictionary<Tuple<string, ResolverOptions>, StructureResolverResult>();

		public StructureResolver()
		{
			
		}

		public StructureResolverResult Resolve(string term)
		{
			return Resolve(term, new ResolverOptions {
				Flags =
					ResolverFlags.UseOPSIN
			});
		}

		public StructureResolverResult Resolve(string term, ResolverOptions options)
		{
			Tuple<string, ResolverOptions> key = new Tuple<string, ResolverOptions>(term, options);
			if ( _cache.ContainsKey(key) )
				return _cache[key];

#if USE_OPSIN
			if ( ( options.Flags & ResolverFlags.UseOPSIN ) == ResolverFlags.UseOPSIN ) {
				if ( _OPSIN == null )
					_OPSIN = NameToStructure.getInstance();
				string smiles = _OPSIN.parseToSmiles(term);
				if ( !String.IsNullOrEmpty(smiles) ) {
					IMolecule m = new NewMolecule(MolUtils.SMILESToMol(smiles));
					return new StructureResolverResult { Confidence = 100, Fragment = new Fragment(null) { Molecule = m } };
				}
			}
#endif
			var result = new StructureResolverResult { Confidence = 0, Fragment = null };
			_cache.Add(key, result);
			return result;
		}
	}
}
