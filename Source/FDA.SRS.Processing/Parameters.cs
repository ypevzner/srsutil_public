using FDA.SRS.Database;
using FDA.SRS.Utils;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Web.Script.Serialization;

namespace FDA.SRS.Processing
{
	/// <summary>
	/// Operational parameters are calculated based on provided command-line options
	/// </summary>
	public class OperationalParameters
	{
		public ISplsDatabase SplsDb { get; internal set; }

		public IEnumerable<string> InputFiles { get; internal set; }

		public int? SetId { get; internal set; }
	}
    
	/// <summary>
	/// Global parameters are calculated on demand (depending on CLI options), but are maintained globally
	/// </summary>
	public static class ReferenceDatabases
	{
		/// <summary>
		/// Reference SPL database (e.g. imported from Dailymed)
		/// </summary>
		public static ISplsDatabase RefSplsDb {
			get {
                try
                {
                    if (!_refSplsDbLoaded)
                    {
                        lock (_refSplsDbMutex)
                        {
                            if (!String.IsNullOrEmpty(ConfigurationManager.AppSettings["SplRefsDatabase"]))
                            {

                                _refSplsDb = new SplsDatabase(ConfigurationManager.AppSettings["SplRefsDatabase"]);

                            }
                            _refSplsDbLoaded = true;
                        }
                    }
                }catch(Exception e){
                    //Catche exception if can't load specified db
                }
				return _refSplsDb;
			}
		}
		private static object _refSplsDbMutex = new object();
		private static ISplsDatabase _refSplsDb = null;
		private static bool _refSplsDbLoaded;

		/// <summary>
		/// Globally cached SubstancesIndexing file
		/// </summary>
		public static ISubstanceIndexing Indexes {
			get {
				if ( !_indexesLoaded ) {
                    System.Console.WriteLine("READING INDEX NOW");
					lock ( _indexesMutex ) {
						if ( !String.IsNullOrEmpty(ConfigurationManager.AppSettings["SubstanceIndexingURI"]) )
							_indexes = new SubstanceIndexing(ConfigurationManager.AppSettings["SubstanceIndexingURI"]);
						_indexesLoaded = true;
					}
				}
				return _indexes;
			}
		}
		private static object _indexesMutex = new object();
		private static SubstanceIndexing _indexes;
		private static bool _indexesLoaded;
	}
}
