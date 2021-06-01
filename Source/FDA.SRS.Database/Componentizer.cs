using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Linq;
using System.Text;

namespace FDA.SRS.Database
{
	public class Componentizer
    {
/*
        public static void componentize(bool clean, IEnumerable<int> xml_ids)
        {
            using ( SqlConnection conn = new SqlConnection(ConfigurationManager.ConnectionStrings["SRSConnectionString"].ConnectionString) ) {
                if ( clean )
                    conn.ExecuteCommand("exec PopulateMoleculesFromSubstances");
                StringBuilder sb = new StringBuilder("select mol_id, molecule from molecules");
                if ( xml_ids != null && xml_ids.Count() > 0 )
                    sb.AppendFormat(" where xml_id in ({0})", String.Join(",", xml_ids.Select(s => s.ToString())));
                conn.ExecuteReader(sb.ToString(), r => { componentizeRecord((int)r["mol_id"], r["molecule"] as string); });
            }
        }
*/
        private static void componentizeRecord(int mol_id, string mol)
        {
            /* TOFIX
             * using ( SqlConnection conn = new SqlConnection(ConfigurationManager.ConnectionStrings["SRSConnectionString"].ConnectionString) ) {
                NewMolecule m = new NewMolecule(mol);
                if ( m.DistinctMolecules().Count() > 0 )
                    File.WriteAllText(String.Format("{0}.mol", mol_id), mol);
                foreach ( Molecule c in m.DistinctMolecules() ) {
                    conn.ExecuteCommand("insert into molecules (par_id, molecule) values (@par_id, @molecule)", new { par_id = mol_id, molecule = c.ct });
                }
            }
             */
        }
    }
}
