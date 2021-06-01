using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Net;
using System.IO;
using System.Xml;
using System.Xml.XPath;


namespace FDA.SRS.Utils
{
    public class EListReportTranslator
    {
        public static  void Translate(string input_file_path, string output_file_path)
        {
            const int kAccession = 0;   // Index of UNII column.
            const int kUri = 3;         // Index of document URI column.
            const int kColumns = 10;    // Expected number of columns in eList report.

            InChiKeyResolver resolver = new InChiKeyResolver();
            // Read original eList report and use resolver to retrieve InChiKey per SPL document.
            int row_no = 0;
            foreach (var row in DelimitedTextParser.GetRows(input_file_path, '|'))
            {
                ++row_no;
                if (row.Length != kColumns)
                {
                    Console.WriteLine("Row #{0}: expecting {1} fields, found {2}", row_no, kColumns, row.Length);
                    if ( 1 == row_no ) {  // Header
                        Console.WriteLine("Exiting...");
                    }
                    continue;
                }

                if (1 == row_no) // Skip header.
                {
                    continue;
                }
                Thread.Sleep(150); // Throttle rate of HTTP requests at ~ 6 requests per second.
                resolver.Add(row[kAccession], row[kUri]);
            }
            resolver.Wait();
            Console.WriteLine("Requests={0} processed={1}; success={2}",
                resolver.numRequests_,
                resolver.numRequestsCompleted_,
                resolver.numProcessed_);

            // Emit eList report with "InChiKey" column.
            using (System.IO.StreamWriter dest = new System.IO.StreamWriter(output_file_path, false))
            {
                foreach (var row in DelimitedTextParser.GetRows(input_file_path, '|'))
                {
                    if (row.Length != kColumns) continue;

                    string content = String.Join("|", row);
                    if (row[0] == "UNII")
                    {
                        content += "|" + "InChiKey";
                    }
                    else
                    {
                        content += "|" + resolver.Get(row[kUri]);
                    }
                    dest.WriteLine(content);
                }
            }
        }

        // Context for a worker thread.
        class Context
        {
            public InChiKeyResolver self;
            public int numTry;
            public string Id;
            public string Uri;

            public Context(InChiKeyResolver resolver, int num_try, string id, string uri)
            {
                self = resolver;
                numTry = num_try;
                Id = id;
                Uri = uri;
            }

            public int OnProcessed()
            {
                return Interlocked.Increment(ref self.numProcessed_);
            }

            public int OnCompleted()
            {
                return Interlocked.Increment(ref self.numRequestsCompleted_);
            }
        }

        class InChiKeyResolver
        {
            public int numRequests_ = 0;
            public int numRequestsCompleted_ = 0;
            public int numProcessed_ = 0;
            public ConcurrentQueue<Context> retryQueue_ = new ConcurrentQueue<Context>();

            private readonly object dictLock = new object(); // Sync object to serialize access to inchiKeys dictionary.
            private Dictionary<string, string> inchiKeys = new Dictionary<string, string>();

            public void Add(string id, string uri)
            {
                Interlocked.Increment(ref numRequests_);
                ThreadPool.QueueUserWorkItem(ProcessDoc, new Context(this, 1, id, uri));
            }

            public string Get(string key)
            {
                string value = "";
                inchiKeys.TryGetValue(key, out value);
                return value;
            }

            public void Wait()
            {
                for (;;)
                {
                    // Process items in retry queue if there are any.
                    // A download operation will be retried in case of HTTP errors (except Forbidden and NotFound).
                    while ( !retryQueue_.IsEmpty )
                    {
                        Context ctx;
                        if (retryQueue_.TryDequeue(out ctx) )
                        {
                            Thread.Sleep(150); // Throttle rate of HTTP requests.
                            ThreadPool.QueueUserWorkItem(ProcessDoc, new Context(this, ctx.numTry, ctx.Id, ctx.Uri));
                        }
                    }

                    if (numRequestsCompleted_ == numRequests_) return;
                    Thread.Sleep(1000);
                }
            }

            static void ProcessDoc(Object context)
            {
                const int kMaxTries = 3; // Setting kMaxRetry to 1 effectively disables retry operation.

                Context ctx = (Context)context;
                try
                {
                    WebRequest request = WebRequest.Create(ctx.Uri);
                    try
                    {
                        HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                        if (response.StatusCode == HttpStatusCode.OK)
                        {
                            Stream stream = response.GetResponseStream();
                            StreamReader reader = new StreamReader(stream);
                            try
                            {
                                string inchiKey = SplDocumentParser.GetInChiKey(reader);
                                if (inchiKey.Length > 0)
                                {
                                    lock (ctx.self.dictLock)
                                    {
                                        try
                                        {
                                            ctx.self.inchiKeys.Add(ctx.Uri, inchiKey);
                                        }
                                        catch (ArgumentException)
                                        {
                                            if (ctx.self.inchiKeys[ctx.Uri] != inchiKey)
                                            {
                                                Console.WriteLine("Duplicate UNII={0} different InChiKey: {1} vs. {2}", ctx.Id, ctx.self.inchiKeys[ctx.Uri], inchiKey);
                                            }
                                            else
                                            {
                                                Console.WriteLine("Duplicate row found UNII={0}", ctx.Id);
                                            }
                                        }
                                    }
                                }
                                ctx.OnProcessed();
                            }
                            catch (XmlException e)
                            {
                                Console.WriteLine("An error occurred while parsing XML document {0}: {1}", ctx.Uri, e);
                            }
                            finally
                            {
                                reader.Close();
                                stream.Close();
                            }
                        }
                        response.Close();
                    }
                    catch (WebException e)
                    {
                        if ( null != e.Response)
                        {
                            HttpWebResponse httpResponse = (HttpWebResponse)e.Response;
                            Console.WriteLine("HTTP_ERROR|{0}|{1}", httpResponse.StatusCode, ctx.Uri);
                            if (httpResponse.StatusCode != HttpStatusCode.NotFound && httpResponse.StatusCode != HttpStatusCode.Forbidden)
                            {
                                // Retry downloading this document.
                                if (ctx.numTry < kMaxTries)
                                {
                                    ctx.numTry += 1;
                                    ctx.self.retryQueue_.Enqueue(ctx);
                                    // Return at this point to avoid ctx.OnComplete().
                                    return;
                                }
                            }
                        }
                        else
                        {
                            Console.WriteLine("Unexpected error occurred while processing URI={0}: {1}", ctx.Uri, e);
                        }
                    }
                }
                catch (UriFormatException)
                {
                    Console.WriteLine("INVALID_URI|UNII={0}|URI={1}", ctx.Id, ctx.Uri);
                }
                catch(Exception e)
                {
                    Console.WriteLine("Unexpected error occurred while processing URI={0}: {1}", ctx.Uri, e);
                }

                // Report progress
                int num_completed = ctx.OnCompleted();
                if ( num_completed % 1000 == 0)
                {
                    Console.WriteLine("COMPLETED|{0}", num_completed);
                }
            }
        }

        class DelimitedTextParser
        {
            static public IEnumerable<string[]> GetRows(string file_path, char delim)
            {
                using (StreamReader stream = new StreamReader(file_path))
                {
                    for (;;)
                    {
                        string line = stream.ReadLine();
                        if (line == null)
                        {
                            break;
                        }
                        string[] fields = line.Split(delim);
                        yield return fields;
                    }
                }
            }
        }

        class SplDocumentParser
        {
            static public string GetInChiKey(string path)
            {
                using (StreamReader reader = new StreamReader(new FileStream(path, FileMode.Open)))
                {
                    return GetInChiKey(reader);
                }
            }

            static public string GetInChiKey(StreamReader stream)
            {
                // Return InChiKey for a moiety "only if the SPL file has a single moiety without a code and no other characteristics (SRS-373)".
                XPathDocument doc = new XPathDocument(stream);
                XPathNavigator nav = doc.CreateNavigator();
                XmlNamespaceManager manager = new XmlNamespaceManager(nav.NameTable);
                manager.AddNamespace("x", "urn:hl7-org:v3");

                XPathNodeIterator nodes = nav.Select("./x:document/x:component/x:structuredBody/x:component/x:section/x:subject/x:identifiedSubstance/x:identifiedSubstance[x:code[@codeSystem=\"2.16.840.1.113883.4.9\"]]", manager);
                if (nodes.Count != 1)
                {
                    return "";
                }
                nodes.MoveNext();

                XPathNodeIterator substances = nodes.Current.Select("./x:moiety[x:code]", manager);
                if (substances.Count > 0)
                {
                    return "";
                }

                substances = nodes.Current.Select("./x:moiety[not(x:code)][x:partMoiety[x:code]]", manager);
                if (substances.Count != 1)
                {
                    return "";
                }

                XPathNodeIterator codes = nodes.Current.Select("./x:code/@code", manager);
                codes.MoveNext();

                substances.MoveNext();
                XPathNodeIterator moietyCodes = substances.Current.Select("./x:partMoiety/x:code/@code", manager);
                if (moietyCodes.Count != 1)
                {
                    return "";
                }
                moietyCodes.MoveNext();

                if (moietyCodes.Current.ToString() == codes.Current.ToString())
                {
                    XPathNodeIterator chem_struct = substances.Current.Select("./x:subjectOf/x:characteristic/x:value[@mediaType=\"application/x-inchi-key\"]/text()", manager);
                    if (chem_struct.Count != 1)
                    {
                        return "";
                    }
                    chem_struct.MoveNext();
                    return chem_struct.Current.ToString();
                }
                else
                {
                    return "";
                }
            }
        }
    }
}
