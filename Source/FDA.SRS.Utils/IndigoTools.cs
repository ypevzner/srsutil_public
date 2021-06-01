using com.epam.indigo;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace FDA.SRS.Utils
{
	public static class IndigoExtensions
	{
		public static void RenderToFile(this string mol, string path, IDictionary<string, string> options = null)
		{
            
            using ( Indigo indigo = new Indigo() ) {
                IndigoRenderer renderer = new IndigoRenderer(indigo);
                if ( options != null ) {
					foreach ( var kv in options ) {
						var ss = kv.Value.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList();
						if ( ss.Count() == 3 )
							indigo.setOption(kv.Key, float.Parse(ss[0]), float.Parse(ss[1]), float.Parse(ss[2]));
						else if ( ss.Count() == 2 )
							indigo.setOption(kv.Key, int.Parse(ss[0]), int.Parse(ss[1]));
						else if ( ss.Count() == 1 ) {
                            if (ss[0].Equals("true", StringComparison.InvariantCultureIgnoreCase) || ss[0].Equals("false", StringComparison.InvariantCultureIgnoreCase))
                                indigo.setOption(kv.Key, bool.Parse(ss[0]));
                            else if (ss[0].Contains('.'))
                                indigo.setOption(kv.Key, float.Parse(ss[0]));
                            else
                                indigo.setOption(kv.Key, ss[0]);
						}
						else
							Trace.TraceWarning("Invalid Indigo option format: {0}", ss[0]);
					}
				}

				using ( IndigoObject io = indigo.loadMolecule(mol) ) {
                    //YP Moved this line to the top of the procedure in order to fix the "property not defined" error
					//IndigoRenderer renderer = new IndigoRenderer(indigo);
					renderer.renderToFile(io, path);
				}
			}
		}

		public static byte[] DrawMolecule(this string value, string fmt1, string fmt2, int? w, int? h, bool? c)
		{
			using ( Indigo indigo = new Indigo() ) {
				string m = value;
				if ( fmt1 == "inchi" )
					m = InChI.InChIToSMILES(m);

				using ( IndigoObject io = indigo.loadMolecule(m) ) {
					if ( c != null )
						indigo.setOption("render-coloring", c.ToString());

					indigo.setOption("render-output-format", fmt2);
					indigo.setOption("render-margins", 10, 10);
					if ( w != null )
						indigo.setOption("render-image-width", (int)w);
					if ( h != null )
						indigo.setOption("render-image-height", (int)h);

					IndigoRenderer renderer = new IndigoRenderer(indigo);
					return renderer.renderToBuffer(io);
				}
			}
		}

		public static bool IsMeso(this Indigo indigo, IndigoObject o1, IndigoObject o2)
		{
			IndigoInchi indigoInChI = new IndigoInchi(indigo);
			return indigoInChI.getInchi(o1) == indigoInChI.getInchi(o2);
		}

		public static string GetInChI(this Indigo indigo, IndigoObject o)
		{
			try {
				IndigoInchi indigoInChI = new IndigoInchi(indigo);
				return indigoInChI.getInchi(o);
			}
			catch ( IndigoException ) {
				return null;
			}
		}

		public static string ToInChIKey(this Indigo indigo, string inchi)
		{
			try {
				IndigoInchi indigoInChI = new IndigoInchi(indigo);
				return indigoInChI.getInchiKey(inchi);
			}
			catch ( IndigoException ) {
				return null;
			}
		}

		public static void ProcessMol(this string mol, Action<Indigo, IndigoObject> a)
		{
			using ( Indigo indigo = new Indigo() ) {
				a(indigo, indigo.loadMolecule(mol));
			}
		}

		public static void IterateMol(this string file, Action<Indigo, IndigoObject> a)
		{
			using ( Indigo indigo = new Indigo() ) {
				a(indigo, indigo.loadMoleculeFromFile(file));
			}
		}

		public static void IterateSdf(this string file, Func<Indigo, IndigoObject, bool> a)
		{
			using ( Indigo indigo = new Indigo() ) {
				foreach ( IndigoObject o in indigo.iterateSDFile(file) ) {
					if ( !a(indigo, o) )
						break;
				}
			}
		}
	}
}
