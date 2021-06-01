using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using FDA.SRS.Utils;

namespace FDA.SRS.Database
{
    public class Exporter
    {
        public static void export_sdf(string ofile, IEnumerable<int> substance_ids, IEnumerable<int> sdf_ids)
        {
            using (StreamWriter sw = new StreamWriter(ofile))
            using (SqlConnection conn = new SqlConnection(ConfigurationManager.ConnectionStrings["SRSConnectionString"].ConnectionString))
            {
                StringBuilder sb = new StringBuilder("select * from sdfs");
                if (substance_ids != null && substance_ids.Count() > 0)
                    sb.AppendFormat(" where substance_id in ({0})", String.Join(",", substance_ids));
                else if (sdf_ids != null && sdf_ids.Count() > 0)
                    sb.AppendFormat(" where sdf_id in ({0})", String.Join(",", sdf_ids));

                conn.ExecuteReader(sb.ToString(), r =>
                {
                    SdfRecord sdf = new SdfRecord();
                    sdf.Mol = r["sdf"] as string;
                    if (!(r["cdbregno"] is DBNull))
                        sdf.AddField("CDBREGNO", r["cdbregno"].ToString());
                    if (!(r["fda_id"] is DBNull))
                        sdf.AddField("FDA_ID", r["fda_id"].ToString());
                    if (!(r["smiles"] is DBNull))
                        sdf.AddField("SMILES", r["smiles"].ToString());
                    if (!(r["inchi"] is DBNull))
                        sdf.AddField("InChI", r["inchi"].ToString());
                    if (!(r["inchi_key"] is DBNull))
                        sdf.AddField("InChIKey", r["inchi_key"].ToString());

                    sdf.AddField("Indigo-SMILES", sdf.Molecule.SMILES);
                    sdf.AddField("Indigo-InChI", sdf.Molecule.InChI);
                    sdf.AddField("Indigo-InChIKey", sdf.Molecule.InChIKey);

                    sw.Write(sdf.ToString());
                });
            }
        }

        public enum ExportWhat { All, Single, Mixture, File }

        public static void export_xml(string dir, ExportWhat export, IEnumerable<int> substance_ids, string original_file)
        {
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            using (SqlConnection conn = new SqlConnection(ConfigurationManager.ConnectionStrings["SRSConnectionString"].ConnectionString))
            {
                conn.Open();

                StringBuilder sql;
                if (export == ExportWhat.Single)
                    sql = new StringBuilder("select s.out_id, s.out_xml from substances s join sdfs f on s.substance_id = f.substance_id where out_xml is not null and f.mixture_id is null");
                else if (export == ExportWhat.Mixture)
                    sql = new StringBuilder("select s.out_id, s.out_xml from substances s join (select distinct xml_id from substances ss join sdfs f on ss.substance_id = f.mixture_id where ss.out_xml is not null) a on s.xml_id = a.xml_id");
                else // All
                    sql = new StringBuilder("select s.out_id, s.out_xml from substances s where s.out_xml is not null");

                if (substance_ids != null && substance_ids.Count() > 0)
                    sql.AppendFormat(" and substance_id in ({0})", String.Join(",", substance_ids));

                using (SqlCommand cmd = new SqlCommand(sql.ToString(), conn))
                {
                    using (SqlDataReader r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            XDocument xdoc = XDocument.Load(new StringReader(r[1] as string));
                            File.WriteAllText(Path.Combine(dir, String.Format("{0}.xml", r[0])), r[1] as string);
                        }
                    }
                }
            }
        }

        public static void export_images(IEnumerable<int> xml_ids, string prefix, int width, int height)
        {
            export_with_connext(xml_ids, prefix, width, height);
        }

        private static void export_with_linq(IEnumerable<int> xml_ids, string prefix, int width, int height)
        {
            /*SRSDBDataContext ctx = new SRSDBDataContext(ConfigurationManager.ConnectionStrings["SRSConnectionString"].ConnectionString);
			foreach ( int xml_id in xml_ids ) {
				IEnumerable<string> mols = ctx.GetSubstanceMols(xml_id).Select(m => m.mol);
				int iMol = 0;
				foreach ( string mol in mols ) {
					if ( !String.IsNullOrEmpty(mol) ) {
						string fileBase = String.Format("{0}-{1}-{2}", prefix, xml_id, iMol++);
						File.WriteAllText(fileBase + ".mol", mol);
						File.WriteAllBytes(fileBase + ".png", new NewMolecule(mol).GetImage(width, height));
					}
				}
			}*/
        }

        private static void export_with_connext(IEnumerable<int> xml_ids, string prefix, int width, int height)
        {
            using (SqlConnection conn = new SqlConnection(ConfigurationManager.ConnectionStrings["SRSConnectionString"].ConnectionString))
            {
                foreach (int xml_id in xml_ids)
                {
                    int iMol = 0;
                    conn.ExecuteReader(String.Format("exec GetSubstanceMols {0}", xml_id), r =>
                    {
                        string mol = r["mol"] as string;
                        if (!String.IsNullOrEmpty(mol))
                        {
                            string fileBase = String.Format("{0}-{1}-{2}", prefix, xml_id, iMol++);
                            File.WriteAllText(fileBase + ".mol", mol);
                            File.WriteAllBytes(fileBase + ".png", new SDFUtil.NewMolecule(mol).GetImage(width, height));
                        }
                    });
                }
            }
        }
    }
}
