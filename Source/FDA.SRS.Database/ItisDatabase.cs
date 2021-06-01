using SQLite;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FDA.SRS.Database
{
	public class ItisDatabase : IDisposable, ITaxonomyValidator
	{
		private SQLiteConnection _connection;
		private string _encoding;

		private SQLiteCommand createCommand(string cmd, params object[] ps)
		{
			var _command = _connection.CreateCommand(cmd, ps);

			if ( !String.IsNullOrEmpty(_encoding) ) {
				_command.CommandText = String.Format("PRAGMA encoding = \"{0}\";", _encoding);
				_command.ExecuteNonQuery();
			}

			return _command;
		}

		class Name
		{
			public string complete_name { get; set; }
		}
		private HashSet<string> _longNames;
		public HashSet<string> Names
		{
			get {
				if ( _longNames == null ) {
					SQLiteCommand cmd = createCommand("select complete_name from taxonomic_units");
					_longNames = new HashSet<string>(cmd.ExecuteQuery<Name>().Select(n => n.complete_name.ToLower()).Distinct());
				}
				return _longNames;
			}
		}

		class Author
		{
			public string taxon_author { get; set; }
		}
		private HashSet<string> _authors;
		public HashSet<string> Authors
		{
			get
			{
				if ( _authors == null ) {
					SQLiteCommand cmd = createCommand("select taxon_author from taxon_authors_lkp");
					_authors = new HashSet<string>(cmd.ExecuteQuery<Author>().Select(n => n.taxon_author.ToLower()).Distinct());
				}
				return _authors;
			}
		}

		class Reference
		{
			public string complete_name { get; set; }
			public string taxon_author { get; set; }
		}
		private HashSet<string> _references;
        private HashSet<string> _referencesLower;
        public HashSet<string> References
		{
			get {
				if ( _references == null ) {
					SQLiteCommand cmd = createCommand("select complete_name, taxon_author from taxonomic_units u join taxon_authors_lkp as a on u.taxon_author_id = a.taxon_author_id");
					_references = new HashSet<string>(cmd.ExecuteQuery<Reference>().Select(r => String.Format("{0} {1}", r.complete_name, r.taxon_author)).Distinct());
                    _referencesLower = new HashSet<string>(cmd.ExecuteQuery<Reference>().Select(r => String.Format("{0} {1}", r.complete_name, r.taxon_author).ToLower()).Distinct());

                }
				return _references;
			}
		}
		
		public ItisDatabase(string file, string encoding = null)
		{
			_connection = new SQLiteConnection(file);
			_encoding = encoding;
		}

		public void Dispose()
		{
			_connection.Close();
			_connection.Dispose();
		}

		public bool ValidateName(string name)
		{
			return Names.Contains(name?.ToLower());
		}

		public bool ValidateAuthor(string name)
		{
			return Authors.Contains(name?.ToLower());
		}

		public bool ValidateReference(string name)
		{
			bool direct= References.Contains(name);
            if (!direct) {
                return _referencesLower.Contains(name?.ToLower());
            }
            return direct;
		}
	}
}
