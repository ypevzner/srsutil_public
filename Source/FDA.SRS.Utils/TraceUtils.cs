using Serilog;
using System;
using System.Diagnostics;

namespace FDA.SRS.Utils
{
	public class TraceUtils
	{
		public static void WriteUNIITrace(TraceEventType eventType, string unii, string hash, string format, params object[] args)
		{
			if ( args.Length == 0 )
				format = format.Replace("{", "{{").Replace("}", "}}");

			String message = String.Format("{0},{1},{2},{3},\"{4}\"", DateTime.Now.ToString("s"), eventType, unii, hash, String.Format(format, args));

			Trace.WriteLine(message);

			switch ( eventType ) {
				case TraceEventType.Verbose:
					Log.Logger.Verbose(message);
					break;
				case TraceEventType.Information:
					Log.Logger.Information(message);
					break;
				case TraceEventType.Warning:
					Log.Logger.Warning(message);
					break;
				case TraceEventType.Error:
				case TraceEventType.Critical:
					Log.Logger.Error(message);
					break;
				default:
					Log.Logger.Verbose(message);
					break;
			}
			
		}

		public static void ReportError(string category, string unii, string format, params object[] pars)
		{
			if ( SplOptions.ConvertOptions.Strict )
				throw new SrsException(category, String.Format(format, pars));
			else
				WriteUNIITrace(TraceEventType.Error, unii, null, format, pars);
		}
	}
}
