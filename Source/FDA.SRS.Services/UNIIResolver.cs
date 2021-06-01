using FDA.SRS.Utils;
using System.Diagnostics;

namespace FDA.SRS.Services
{
	public class UNIIResolver : IStructureResolver
	{
		public StructureResolverResult Resolve(string term)
		{
			TraceUtils.WriteUNIITrace(TraceEventType.Warning, null, null, "UNII resolver is not implemented - cannot resolve {0}", term);
			return new StructureResolverResult { Confidence = 0, Fragment = null };
		}
	}
}
