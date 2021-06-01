using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.Common;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace FDA.SRS.Database
{
    public static class DBUtils
    {
        private static T cast<T>(object res)
        {
            T t;
            if ((res == null ? true : res is DBNull))
            {
                t = default(T);
            }
            else if (!typeof(T).IsAssignableFrom(res.GetType()))
            {
                Type underlyingType = Nullable.GetUnderlyingType(typeof(T));
                t = (underlyingType == null ? (T)Convert.ChangeType(res, typeof(T)) : (T)Convert.ChangeType(res, underlyingType));
            }
            else
            {
                t = (T)res;
            }
            return t;
        }

        public static int ExecuteCommand(this SqlConnection conn, string sql, params object[] args)
        {
            return DBUtils.ExecuteCommand(conn, null, sql, args);
        }

        public static int ExecuteCommand(this SqlTransaction tran, string sql, params object[] args)
        {
            return DBUtils.ExecuteCommand(tran.Connection, tran, sql, args);
        }

        private static int ExecuteCommand(SqlConnection conn, SqlTransaction tran, string sql, params object[] args)
        {
            int num;
            using (SqlCommand sqlCommandExt = new DBUtils.SqlCommandExt(sql, conn, tran, args))
            {
                num = sqlCommandExt.ExecuteNonQuery();
            }
            return num;
        }

        public static int ExecuteReader(this SqlConnection conn, string sql, DBUtils.ReaderDelegate dlgt, params object[] args)
        {
            return DBUtils.ExecuteReader(conn, null, sql, dlgt, args);
        }

        private static int ExecuteReader(SqlConnection conn, SqlTransaction tran, string sql, DBUtils.ReaderDelegate dlgt, params object[] args)
        {
            int num = 0;
            using (DBUtils.SqlCommandExt sqlCommandExt = new DBUtils.SqlCommandExt(sql, conn, tran, args))
            {
                using (SqlDataReader sqlDataReader = sqlCommandExt.ExecuteReader())
                {
                    while (sqlDataReader.Read())
                    {
                        num++;
                        dlgt(sqlDataReader);
                    }
                }
            }
            return num;
        }

        public static T ExecuteScalar<T>(this SqlConnection conn, string sql, params object[] args)
        {
            return DBUtils.ExecuteScalar<T>(conn, null, sql, args);
        }

        public static T ExecuteScalar<T>(this SqlTransaction tran, string sql, params object[] args)
        {
            return DBUtils.ExecuteScalar<T>(tran.Connection, tran, sql, args);
        }

        private static T ExecuteScalar<T>(SqlConnection conn, SqlTransaction tran, string sql, params object[] args)
        {
            T t;
            using (DBUtils.SqlCommandExt sqlCommandExt = new DBUtils.SqlCommandExt(sql, conn, tran, args))
            {
                t = DBUtils.cast<T>(sqlCommandExt.ExecuteScalar());
            }
            return t;
        }

        public delegate void ReaderDelegate(SqlDataReader r);

        private class SqlCommandExt : IDisposable
        {
            private SqlCommand m_Cmd;

            private bool m_Disposed;

            internal SqlCommandExt(string sql, SqlConnection conn, SqlTransaction tran, object[] args)
            {
                if (conn.State == ConnectionState.Closed)
                {
                    conn.Open();
                }
                bool flag = false;
                if (Regex.IsMatch(sql, "^exec\\s+\\S+$"))
                {
                    flag = true;
                    sql = sql.Substring(5);
                }
                this.m_Cmd = new SqlCommand(sql, conn, tran)
                {
                    CommandTimeout = conn.ConnectionTimeout
                };
                if (flag)
                {
                    this.m_Cmd.CommandType = CommandType.StoredProcedure;
                }
                DBUtils.SqlCommandExt.addParams(this.m_Cmd, args);
            }

            private static void addParams(SqlCommand cmd, object[] args)
            {
                object value;
                if ((args == null ? false : args.Length != 0))
                {
                    object[] objArray = args;
                    for (int i = 0; i < (int)objArray.Length; i++)
                    {
                        object obj = objArray[i];
                        if (obj != null)
                        {
                            PropertyInfo[] properties = obj.GetType().GetProperties();
                            for (int j = 0; j < (int)properties.Length; j++)
                            {
                                PropertyInfo str = properties[j];
                                object value1 = str.GetValue(obj, null);
                                if ((str.PropertyType != typeof(byte[]) ? false : value1 == null))
                                {
                                    cmd.Parameters.Add(str.Name, SqlDbType.VarBinary).Value = DBNull.Value;
                                }
                                else if (value1 == null)
                                {
                                    cmd.Parameters.AddWithValue(str.Name, DBNull.Value);
                                }
                                else if (str.PropertyType != typeof(XDocument))
                                {
                                    SqlParameterCollection parameters = cmd.Parameters;
                                    string name = str.Name;
                                    if (value1 == null)
                                    {
                                        value = DBNull.Value;
                                    }
                                    else
                                    {
                                        value = value1;
                                    }
                                    parameters.AddWithValue(name, value);
                                }
                                else
                                {
                                    cmd.Parameters.Add(str.Name, SqlDbType.Xml).Value = value1.ToString();
                                }
                            }
                        }
                    }
                }
            }

            public void Dispose()
            {
                this.Dispose(true);
                GC.SuppressFinalize(this);
            }

            protected virtual void Dispose(bool disposing)
            {
                if (!this.m_Disposed)
                {
                    if (disposing)
                    {
                        this.m_Cmd.Dispose();
                    }
                    this.m_Disposed = true;
                }
            }

            internal int ExecuteNonQuery()
            {
                return this.m_Cmd.ExecuteNonQuery();
            }

            internal SqlDataReader ExecuteReader()
            {
                return this.m_Cmd.ExecuteReader();
            }

            internal object ExecuteScalar()
            {
                return this.m_Cmd.ExecuteScalar();
            }

            ~SqlCommandExt()
            {
                this.Dispose(false);
            }

            public static implicit operator SqlCommand(DBUtils.SqlCommandExt t)
            {
                return t.m_Cmd;
            }
        }
    }

}
