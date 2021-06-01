using System.Diagnostics;

namespace FDA.SRS.Utils
{
	public class SrsTextWriterTraceListener : TextWriterTraceListener
    {
        public SrsTextWriterTraceListener(System.IO.Stream stream, string name) : base(stream, name) { }
        public SrsTextWriterTraceListener(System.IO.Stream stream) : base(stream) { }
        public SrsTextWriterTraceListener(string fileName, string name) : base(fileName, name) { }
        public SrsTextWriterTraceListener(string fileName) : base(fileName) { }
        public SrsTextWriterTraceListener(System.IO.TextWriter writer, string name) : base(writer, name) { }
        public SrsTextWriterTraceListener(System.IO.TextWriter writer) : base(writer) { }

        
    }
}
