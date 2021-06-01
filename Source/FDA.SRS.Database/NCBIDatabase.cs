using System;
using System.Collections.Generic;

namespace FDA.SRS.Database
{
	public class NcbiDatabase : DownloadableDatabase, IDisposable, ITaxonomyValidator
	{
		private Dictionary<int, string> _longNames;
		public Dictionary<int, string> LongNames
		{
			get {
				if ( _longNames == null ) {
					_longNames = new Dictionary<int,string>();
					/*_command.CommandText = "Select * FROM longnames";
					using ( SQLiteDataReader reader = _command.ExecuteReader() ) {
						while ( reader.Read() ) {
							_longNames.Add(int.Parse(reader["tsn"].ToString()), reader["completename"].ToString().ToLower());
						}
					}*/
				}
				return _longNames;
			}
		}

		private HashSet<string> _authors;
		public HashSet<string> Authors
		{
			get
			{
				if ( _authors == null ) {
					_authors = new HashSet<string>();
					/*_command.CommandText = "Select * FROM strippedauthor";
					using ( SQLiteDataReader reader = _command.ExecuteReader() ) {
						while ( reader.Read() ) {
							string author = reader["shortauthor"].ToString().TrimEnd('0', '1', '2', '3', '4', '5', '6', '7', '8', '9', ' ').ToLower();
							if ( !_authors.Contains(author) )
								_authors.Add(author);
						}
					}*/
				}
				return _authors;
			}
		}

		public NcbiDatabase()
		{
			/*
			_connection = new SQLiteConnection(String.Format("data source={0}", file));
			_command = new SQLiteCommand(_connection);
			_connection.Open();*/
		}

		public void Dispose()
		{
			/*_connection.Close();
			_command.Dispose();
			_connection.Dispose();*/
		}

		public bool ValidateReference(string name)
		{
			// LongNames.ContainsValue(v.ToLower())
			// TODO: insert real validation
			return true;
		}

		public bool ValidateAuthority(string name)
		{
			// LongNames.ContainsValue(v.ToLower())
			// TODO: insert real validation
			return true;
		}

		public bool ValidateName(string name)
		{
			return true;
		}

		public bool ValidateAuthor(string name)
		{
			return true;
		}
	}
}
