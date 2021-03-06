using FDA.SRS.ObjectModel;
using FDA.SRS.Processing;
using FDA.SRS.Utils;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace FDA.SRS.Services
{
	/// <summary>
	/// FragmentFactory is used to find and create a fragment which can be a molecule, linker or modification (amino-acid substitution)
	/// It works with local registry as well as remote N2S services to find a molecule and retrieve connection points if any
	/// </summary>
	public class NAFragmentFactory
	{
		private static string CONFIG_SECTION = "NAFragmentsRegistry";

		private NAFragmentsRegistry _registry;
		private NAStructureResolver _resolver;
		public Dictionary<string, string> components_dictionary;

		[Flags]
		public enum ResolversFlags
		{
			Registry = 0x01,
			Resolver = 0x02,
			All = 0xFF
		}

		private static void Swap<T>(ref T t1, ref T t2)
		{
			T t = t1;
			t1 = t2;
			t2 = t;
		}

		public NAFragmentFactory(ResolversFlags resolvers = ResolversFlags.Registry)
		{
			if ( (resolvers & ResolversFlags.Registry) == ResolversFlags.Registry ) {
				_registry = new NAFragmentsRegistry();

                //YP commenting this since per SRS-334 Fragment.sdf will be loaded from launch directory like registry.sdf
                /*using ( TempFile tf = new TempFile() ) {
                    System.Console.WriteLine("Writing to:" + tf.FullPath + " number of bytes:" + System.Text.Encoding.UTF8.GetString(Resources.Fragments).Length);
                    File.WriteAllBytes(tf.FullPath, Resources.NAFragments);
					loadRegistryFile(tf.FullPath, Resources.registry_options);
				}
                */

				NameValueCollection files = (NameValueCollection)ConfigurationManager.GetSection(CONFIG_SECTION);
				if ( files != null ) {
					foreach ( var k in files.Keys.Cast<string>() ) {
						if ( !File.Exists(k) ) {
							//TraceUtils.WriteUNIITrace(TraceEventType.Error, null, null, "Registry file ({0}) cannot be found - skipping, but this may affect conversion results", k);
							TraceUtils.WriteUNIITrace(TraceEventType.Error, null, null, "Registry file ({0}) cannot be found - exiting", k);
							TraceUtils.WriteUNIITrace(TraceEventType.Error, null, null, "Resource files should be located in ({0})", Directory.GetCurrentDirectory());
							throw new FatalException("Fatal Error: Registry file " + k + " cannot be found");
							//continue;
						}

						loadRegistryFile(k, files[k]);
					}
				}
			}

			//YP SRS-395
			if (!File.Exists("NA_Fragments_dictionary.txt"))
			{
				//TraceUtils.WriteUNIITrace(TraceEventType.Error, null, null, "Registry file ({0}) cannot be found - skipping, but this may affect conversion results", k);
				TraceUtils.WriteUNIITrace(TraceEventType.Error, null, null, "Resource file ({0}) cannot be found - exiting", "NA_Fragments_dictionary.txt");
				TraceUtils.WriteUNIITrace(TraceEventType.Error, null, null, "Resource files should be located in ({0})", Directory.GetCurrentDirectory());
				throw new FatalException("Fatal Error: Resource file NA_Fragments_dictionary.txt cannot be found");
			}
			string dictionary_contents = File.ReadAllText("NA_Fragments_dictionary.txt");
			//components_dictionary = dictionary_contents.Replace("\r", "").Split('\n').ToDictionary(l => l.Split('\t')[0], l => l.Split('\t')[1]);
			components_dictionary = new Dictionary<string,string>();
			foreach (string string_item in dictionary_contents.Replace("\r", "").Split('\n'))
            {
				if (components_dictionary.ContainsKey(string_item.Split('\t')[0]))
				{
					Trace.WriteLine("Duplicate NA Fragment found: " + string_item.Split('\t')[0] + ". Ommitting it from the registry.");
				}
				else
                {
					components_dictionary.Add(string_item.Split('\t')[0], string_item.Split('\t')[1]);
				}
			}
			if ( (resolvers & ResolversFlags.Resolver) == ResolversFlags.Resolver )
				_resolver = new NAStructureResolver();
		}

		private void loadRegistryFile(string file, string options)
		{
			string v = String.IsNullOrEmpty(options) ? "UNII" : options;
			var ss = v.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList();
			string s1 = String.Empty, s2 = String.Empty;
			if ( ss.Count == 2 ) {
				s1 = ss[0];
				s2 = ss[1];
				if ( s1.Contains('=') )
					Swap(ref s1, ref s2);
			}
			else if ( ss.Count == 1 ) {
				if ( ss[0].Contains('=') )
					s2 = ss[0];
				else
					s1 = ss[0];
			}
			else {
				// More than 2-parts entry
				throw new ConfigurationErrorsException(String.Format("{0}: {1}", file, options));
			}

			_registry.Load(
				file,
				s1.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(n => n.Trim().ToLower()),
				s2.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries).ToDictionary(
					m => m.Split(new char[] { '=' }, StringSplitOptions.RemoveEmptyEntries)[0].Trim(),
					m => m.Split(new char[] { '=' }, StringSplitOptions.RemoveEmptyEntries)[1].Trim())
			);
		}

		public NAFragment Resolve(string term, SplObject rootObject = null)
		{
			if ( String.IsNullOrEmpty(term) )
				return null;

			NAFragment f = null;

			if ( f == null && _registry != null ) {
				f = _registry.Resolve(term);
				if ( f != null )
					f.Id = term;
			}

			if ( f == null && _resolver != null ) {
				NAStructureResolverResult r = _resolver.Resolve(term);
				if ( r != null && r.Confidence == 100 ) {
					f = r.Fragment;
					if ( f != null ) {
						f.Id = term;
						_registry.Add(f);   // Cache in registry to not resolve again next time
					}
				}
			}

			if ( f == null )
				return null;

			f.RootObject = rootObject;
			return f;
		}

		/// <summary>
		/// Create a [potentially multi-center] Link and resolve its Fragment. For multi-center links Fragment is not resolved at this stage, but later, along with its Connectors.
		/// </summary>
		/// <param name="protein">Protein</param>
		/// <param name="linkSites">A pairwise sequence of protein sites that this link connects. First number is subunit, second is position on subunit.</param>
		/// <param name="linker">Name or UNII or otherwise resolvable text identifier of a Fragment used in a Link</param>
		/// <returns>Returns completely or partially constructed Link</returns>
		public NALink CreateLink(NucleicAcid na, IEnumerable<Tuple<int, int>> linkSites, string linker, SplObject rootObject)
		{
			NALink link = new NALink(rootObject) { LinkType = linker };

			link.Linker = Resolve(linker, rootObject);
          
			if ( link.Linker == null )
				TraceUtils.WriteUNIITrace(TraceEventType.Warning, na.UNII, null, "Unknown linkage type: {0} - may be defined later...", linker);

			linkSites.ForEachWithIndex((t, i) => {
				int nsu = t.Item1, pos = t.Item2;
				if ( nsu < 0 || nsu >= na.Subunits.Count )
					throw new SrsException("subunit_ref", String.Format("Non-existent nucleotide subunit referenced: {0}", String.Join("-", String.Format("{0}_{1}", nsu + 1, pos + 1))));

				NASubunit su = na.Subunits[nsu];
				if ( pos < 0 || pos >= su.Sequence.Length )
					throw new SrsException("seq_ref", String.Format("Non-existent nucleotide subunit position referenced: {0}", String.Join("-", String.Format("{0}_{1}", nsu + 1, pos + 1))));

				if ( linker == "cys-cys" && su.Sequence.ToString()[pos] != 'C' )
					TraceUtils.ReportError("seq_ref", na.UNII, "Disulfide bridge connected to {0} ({1})", su.Sequence.ToString()[pos], AminoAcids.GetNameByLetter(su.Sequence.ToString()[pos]));
                
				// If Linker is defined - bind connectors right away, otherwise - defer till Linker and its connectors are picked up piecewise from repetitive <STRUCTURAL_MODIFICATION_GROUP>s
				// It is assumed that the order of protein sites in a link definition corresponds to the order of connectors in Fragment definition, which is not necessarily the case
				//begin YB: added position on proximal moiety (fragment)
				link.Sites.Add(new NASite(rootObject, "NUCLEOTIDE SUBSTITUTION POINT") {
					Subunit = su,
					Position = pos,
                    ConnectorRef =
						link.Linker == null ?
						null :
						link.Linker.Connectors[i]
				});
				//end YB: added position on proximal moiety (fragment)
			});

			// If link is completely defined now (not the case for multi-center links) - register it in a containing body (protein)
			if ( link.IsCompletelyDefined )
				na.RegisterFragment(link.Linker);

			return link;
		}

        public NALink CreateLink(NucleicAcid na, IEnumerable<Tuple<int, int>> linkSites, NAFragment linker_fragment, SplObject rootObject)
        {
            NALink link = new NALink(rootObject);

            link.Linker = linker_fragment;

            if (link.Linker == null)
                TraceUtils.WriteUNIITrace(TraceEventType.Warning, na.UNII, null, "Unknown linkage type: {0} - may be defined later...", "Implicit (likely circular) link");

            linkSites.ForEachWithIndex((t, i) => {
                int nsu = t.Item1, pos = t.Item2;
                if (nsu < 0 || nsu >= na.Subunits.Count)
                    throw new SrsException("subunit_ref", String.Format("Non-existent nucleotide subunit referenced: {0}", String.Join("-", String.Format("{0}_{1}", nsu + 1, pos + 1))));

                NASubunit su = na.Subunits[nsu];
                if (pos < 0 || pos >= su.Sequence.Length)
                    throw new SrsException("seq_ref", String.Format("Non-existent nucleotide subunit position referenced: {0}", String.Join("-", String.Format("{0}_{1}", nsu + 1, pos + 1))));

                // If Linker is defined - bind connectors right away, otherwise - defer till Linker and its connectors are picked up piecewise from repetitive <STRUCTURAL_MODIFICATION_GROUP>s
                // It is assumed that the order of protein sites in a link definition corresponds to the order of connectors in Fragment definition, which is not necessarily the case
                //begin YB: added position on proximal moiety (fragment)
                link.Sites.Add(new NASite(rootObject, "NUCLEOTIDE SUBSTITUTION POINT")
                {
                    Subunit = su,
                    Position = pos,
                    ConnectorRef =
                        link.Linker == null ?
                        null :
                        link.Linker.Connectors[i]
                });
                //end YB: added position on proximal moiety (fragment)
            });

            // If link is completely defined now (not the case for multi-center links) - register it in a containing body (protein)
            if (link.IsCompletelyDefined)
                na.RegisterFragment(link.Linker);

            return link;
        }
    }
}
