using FDA.SRS.ObjectModel;
using FDA.SRS.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;

namespace FDA.SRS.Services
{
	/// <summary>
	/// Reads fragments definitions from file and adds to existing set
	/// </summary>
	public class NAFragmentsRegistry
	{
		private Dictionary<string, NAFragment> _nameIndex = new Dictionary<string, NAFragment>();
		private Dictionary<string, NAFragment> _inchiKeyIndex = new Dictionary<string, NAFragment>();

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
                //YP removing requirement for inChI to allow for fragments with connection atoms
				//if ( r.Molecule == null || r.Molecule.InChI == null )
                if (r.Molecule == null)
					continue;   // We cannot use this record for resolution - skip
                
				IList<string> synonyms = new List<string>();

				string unii = null;
				if ( fields.Contains("unii") ) {
                    if (r["UNII"].FirstOrDefault() == "XLK873Z96Y")
                    {

                    }
                    unii = ( r["UNII"] ?? new List<string>() ).FirstOrDefault();
					if (unii== "AOQ54IZZ6J")
                    {
					
                    }
					if ( !String.IsNullOrEmpty(unii) && !synonyms.Contains(unii.ToLower()) )
						synonyms.Add(unii.ToLower());
				}
                
                string name = null;
				if ( fields.Contains("name") ) {
					name = ( r["NAME"] ?? new List<string>() ).FirstOrDefault();
					if ( !String.IsNullOrEmpty(name) && !synonyms.Contains(name.ToLower()) )
						synonyms.Add(name.ToLower());
				}

				if ( r["SYNONYMS"] != null && fields.Contains("synonyms") )
					r["SYNONYMS"].ForAll(s => synonyms.Add(s.ToLower()));

				// We need some name - chose first synonym if none was given
				if ( String.IsNullOrEmpty(name) && synonyms.Count == 1 )
					name = synonyms.First();

				// Get or add RegistryEntry
				NAFragment f = null;
				SDFUtil.IMolecule m = r.Molecule;
                
				if (m.InChIKey == null || !_inchiKeyIndex.ContainsKey(m.InChIKey) )
				{
					TraceUtils.WriteUNIITrace(TraceEventType.Information, unii, null, "Creating new entry ({0}, {1}) from registry", unii ?? name, m.InChIKey);

                    // Create new entry
                    f = new NAFragment(null) {
                        Name = name,
                        UNII = unii,
                        Synonyms = synonyms,
                        Molecule = m
					};

                    //YP commented out as there may be no inchikey
                    //_inchiKeyIndex.Add(m.InChIKey, f);
                    //YP instead, add to the name index
                    synonyms.ForAll(s => {
                        if (!_nameIndex.ContainsKey(s))
                            _nameIndex.Add(s, f);
                    });
                }
				else {
					TraceUtils.WriteUNIITrace(TraceEventType.Warning, unii, null, "Dupplicate entry ({0}, {1}) found in registry", unii ?? name, m.InChIKey);

					// Update existing entry 
					f = _inchiKeyIndex[m.InChIKey];

					if ( String.IsNullOrEmpty(f.Name) && !String.IsNullOrEmpty(name) )
						f.Name = name;

					if ( String.IsNullOrEmpty(f.UNII) && !String.IsNullOrEmpty(unii) )
						f.UNII = unii;

					synonyms.ForAll(s => {
						if ( !f.Synonyms.Contains(s) )
							f.Synonyms.Add(s);
					});
				}
                if (f.UNII == "VD6PQK9DHG")
                {

                }
                //IList<int> component_conns = (r["CONNECTORS"] ?? new List<string>()).SelectMany(s => Regex.Replace(s, @"\s", ";").Split(';')).Select(s => int.Parse(s.Trim())).ToList();
                //YP if connectors_layout is present then the fragment is a nucleic acid component (sugar, base or linker)
                //if connectors_layout is absent from the SD file, then it's a regular registry fragment that can represent a modifiation or a linker
                if (r["CONNECTORS_LAYOUT"] != null)
                {
                    List<int> component_conns = (r["CONNECTORS"] ?? new List<string>())
                        .Select(s => s.Replace(System.Environment.NewLine, ";"))
                        .SelectMany(s => s.Split(new char[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
                        .Select(s => int.Parse(s.Trim()))
                        //.Where(s => s != 0) //newly added to leave out 0, which indicates no connector
                        .ToList();

                    List<string> component_conn_types = (r["CONNECTORS_LAYOUT"] ?? new List<string>())
                        .Select(s => s.Replace(System.Environment.NewLine, ";"))
                        .SelectMany(s => s.Split(new char[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
                        .Select(s => (s.Trim()))
                        //.Where(s => s != "0") //newly added to leave out 0, which indicates no connector
                        .ToList();

                    if (f.UNII == "VD6PQK9DHG")
                    {

                    }
                    f.AddConnectors(component_conns, component_conn_types, f.UNII);
                }
                else
                //if connectors_layout is absent from the SD file, then it's a regular registry fragment that can represent a modifiation or a linker
                {

                    IList<int> conns = (r["CONNECTORS"] ?? new List<string>())
                        .SelectMany(s => s.Split(new char[] { ',', ';', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                        .Select(s => int.Parse(s.Trim()))
                        .ToList();
                    if (conns.Count % 2 != 0)
                        TraceUtils.WriteUNIITrace(TraceEventType.Warning, unii, null, "Odd connection points - only first 0, 2, 4, etc will be used");


                    IList<string[]> connResidues = (r["CONNECTOR_RESIDUES"] ?? new List<string>())
                        .SelectMany(s => s.Split(new char[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
                        .Select(s => s.Split(new char[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
                        .ToList();

					try
					{
						for (int i = 0; i < conns.Count; i += 2)
						{
							//YP Issue 1

							if (f.getCanonicalAtoms().Length == 0)
							{
								f.AddConnectorsPair(conns[i], conns[i + 1], unii);
							}
							else
							{
								f.AddConnectorsPair((conns[i]==0 ? 0 : f.getCanonicalAtoms()[conns[i] - 1] + 1), (conns[i+1]==0 ? 0 : f.getCanonicalAtoms()[conns[i + 1] - 1] + 1), unii);
							}
						}
					}
					catch
                    {
						continue;
                    }
                }
                // Extract connectors from asterisks
                /*
                if ( conns.Count == 0 && m is NewMolecule ) {
					NewMolecule nm = m as NewMolecule;
					if ( nm.Ends != null && nm.Ends.Count == 2 )
						f.AddConnectorsPair(nm.Ends.First().Key, nm.Ends.Last().Key, unii);
				}
                */

                // Add those names that can be missing to a name registry
                synonyms.ForAll(s => {
					if ( !_nameIndex.ContainsKey(s) )
						_nameIndex.Add(s, f);
				});
			}
		}

		public NAFragment Add(NAFragment f)
		{
			return f;
		}

		/// <summary>
		/// Resolve text ID into a Fragment definition. Returned Fragment is immutable object and has to be cloned in case if modification is required.
		/// </summary>
		/// <param name="term">Term to resolve into a chemical substance</param>
		/// <returns></returns>
		public NAFragment Resolve(string term)
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
