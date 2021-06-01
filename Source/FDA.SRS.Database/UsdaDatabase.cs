using System;
using System.Collections.Generic;
using System.Data.Odbc;

namespace FDA.SRS.Database
{
	public class UsdaDatabase : IDisposable, ITaxonomyValidator
	{
		private OdbcConnection _connection;
		private OdbcCommand _command;

		private HashSet<string> _references;
		public HashSet<string> References
		{
			get {
				if ( _references == null ) {
					_references = new HashSet<string>();
					_command.CommandText = @"select taxon, taxon_author from species";
					using ( var reader = _command.ExecuteReader() ) {
						while ( reader.Read() ) {
							_references.Add(String.Format("{0} {1}", reader["taxon"].ToString(), reader["taxon_author"].ToString()));
						}
					}
				}
				return _references;
			}
		}

		public UsdaDatabase(string file, string encoding = null)
		{
			// _connection = new OleDbConnection(String.Format(@"Provider=Microsoft.Jet.OLEDB.4.0;Data Source={0};Extended Properties=DBASE IV;", file));
			_connection = new OdbcConnection(String.Format(@"Driver={Microsoft dBASE Driver (*.dbf)}; Dbq={0};", file));
			_command = new OdbcCommand("", _connection);
			_connection.Open();
		}

		public void Dispose()
		{
			_connection.Close();
			_command.Dispose();
			_connection.Dispose();
		}

		public bool ValidateName(string name)
		{
			return true;
		}

		public bool ValidateAuthor(string name)
		{
			return true;
		}

		public bool ValidateReference(string name)
		{
			return References.Contains(name);
		}
	}
}
