using FDA.SRS.ObjectModel;
using FDA.SRS.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace FDA.SRS.Services
{
	/// <summary>
	/// Reads fragments definitions from file and adds to existing set
	/// </summary>
	public class FragmentsRegistry
	{
		private Dictionary<string, Fragment> _nameIndex = new Dictionary<string, Fragment>();
		private Dictionary<string, Fragment> _inchiKeyIndex = new Dictionary<string, Fragment>();

		public void Load()
		{
			Load("registry.sdf", new List<string> { "unii" }, null);
		}

		public void Load(string sdf_file, IEnumerable<string> fields, IDictionary<string, string> map)
		{
			using ( SdfReader r = new SdfReader(sdf_file) ) {
				r.FieldsMap = map;
				r.Splitters = new Dictionary<string, Func<string, IEnumerable<string>>> { { "SYNONYMS", SdfReader.DefaultSplitter } };
				Load(r, fields);
			}
		}

		public void Load(SdfReader sdf, IEnumerable<string> fields)
		{
			foreach ( SdfRecord r in sdf.Records )
			{
				if ( r.Molecule == null || r.Molecule.InChI == null )
					continue;   // We cannot use this record for resolution - skip
                
				IList<string> synonyms = new List<string>();

				string unii = null;

                
                

                if ( fields.Contains("unii") ) {
                    
                    unii = ( r["UNII"] ?? new List<string>() ).FirstOrDefault();

					if ( !String.IsNullOrEmpty(unii) && !synonyms.Contains(unii.ToLower()) )
						synonyms.Add(unii.ToLower());
				}
                /*
                if ((r["UNII"] ?? new List<string>()).FirstOrDefault().ToUpper()== "JKX41V9FYM")
                {
                    //continue;
                    int test = 1;
                }
                */
				string name = null;
                //YP commenting it out as per SRS-334 expected format of Fragments.sdf doesn't include NAME but NAMES field in the sdf.
                //if ( fields.Contains("name") ) {
                //	name = ( r["NAME"] ?? new List<string>() ).FirstOrDefault();
                //	if ( !String.IsNullOrEmpty(name) && !synonyms.Contains(name.ToLower()) )
                //		synonyms.Add(name.ToLower());
                //}

                string[] stringSeparators = new string[] { ",", ";", "\n", "\r\n" };

                if (r["NAMES"] != null && fields.Contains("synonyms"))
                {
                    //r["NAMES"].ForAll(s => synonyms.Add(s.ToLower()));
                    r["NAMES"].First().Split(stringSeparators, StringSplitOptions.RemoveEmptyEntries).ForAll(t => synonyms.Add(t.ToLower()));
                }
                
                //YP as with NAME field SYNONYMS field isn't expected to be in Fragments.sdf either
                //if ( r["SYNONYMS"] != null && fields.Contains("synonyms") )
				//	r["SYNONYMS"].ForAll(s => synonyms.Add(s.ToLower()));

				// We need some name - chose first synonym if none was given
				if ( String.IsNullOrEmpty(name) && synonyms.Count == 1 )
					name = synonyms.First();

				// Get or add RegistryEntry
				Fragment f = null;
				SDFUtil.IMolecule m = r.Molecule;
				if ( !_inchiKeyIndex.ContainsKey(m.InChIKey) )
				{
					TraceUtils.WriteUNIITrace(TraceEventType.Information, unii, null, "Creating new entry ({0}, {1}) from registry", unii ?? name, m.InChIKey);

					// Create new entry
					f = new Fragment(null) {
						Name = name,
						UNII = unii,
						Synonyms = synonyms,
						Molecule = m
					};
					_inchiKeyIndex.Add(m.InChIKey, f);
				}
				else {
					TraceUtils.WriteUNIITrace(TraceEventType.Warning, unii, null, "Dupplicate entry ({0}, {1}) found in registry", unii ?? name, m.InChIKey);

					// Update existing entry 
					f = _inchiKeyIndex[m.InChIKey];

					if ( String.IsNullOrEmpty(f.Name) && !String.IsNullOrEmpty(name) )
						f.Name = name;

					if ( String.IsNullOrEmpty(f.UNII) && !String.IsNullOrEmpty(unii) )
						f.UNII = unii;

                    f.Molecule = m;

					synonyms.ForAll(s => {
						if ( !f.Synonyms.Contains(s) )
							f.Synonyms.Add(s);
					});
				}

				IList<int> conns = ( r["CONNECTORS"] ?? new List<string>() )
					.SelectMany(s => s.Split(new char[] { ',', ';', '\n' }, StringSplitOptions.RemoveEmptyEntries))
					.Select(s => int.Parse(s.Trim()))
					.ToList();
				if ( conns.Count % 2 != 0 )
					TraceUtils.WriteUNIITrace(TraceEventType.Warning, unii, null, "Odd connection points - only first 0, 2, 4, etc will be used");


                IList<string[]> connResidues = (r["CONNECTOR_RESIDUES"] ?? new List<string>())
                    .SelectMany(s => s.Split(new char[] {'\n','\r' }, StringSplitOptions.RemoveEmptyEntries))
                    .Select(s => s.Split(new char[] { ',',';' }, StringSplitOptions.RemoveEmptyEntries))
                    .ToList();


                for ( int i = 0; i < conns.Count; i += 2 ) {
                    string[] con = null;
                    if (connResidues.Count > i / 2) {
                        con = connResidues[i / 2];
                    }

                    f.AddConnectorsPair(conns[i], conns[i + 1],con, unii);
				}

				// Extract connectors from asterisks
				if ( conns.Count == 0 && m is SDFUtil.NewMolecule ) {
                    SDFUtil.NewMolecule nm = m as SDFUtil.NewMolecule;
					if ( nm.Ends != null && nm.Ends.Count == 2 )
						f.AddConnectorsPair(nm.Ends.First().Key, nm.Ends.Last().Key, unii);
				}

                //YP commented out sorting of the connectors per YB request SRS-281
                //f.SortConnectors();

				// Add those names that can be missing to a name registry
				synonyms.ForAll(s => {
					if ( !_nameIndex.ContainsKey(s) )
						_nameIndex.Add(s, f);
				});
			}
		}

		public Fragment Add(Fragment f)
		{
			return f;
		}

		/// <summary>
		/// Resolve text ID into a Fragment definition. Returned Fragment is immutable object and has to be cloned in case if modification is required.
		/// </summary>
		/// <param name="term">Term to resolve into a chemical substance</param>
		/// <returns></returns>
		public Fragment Resolve(string term)
		{
			if ( String.IsNullOrEmpty(term) )
				return null;

			string n = term.ToLower();
			var r = _nameIndex.Select(b=> {
               // System.Console.WriteLine(b.Key);
                return b;
            }).Where(_re => _re.Key == n);


			return r.Any() ? r.First().Value : null;
		}
	}
}
