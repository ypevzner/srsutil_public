using Microsoft.VisualStudio.TestTools.UnitTesting;
using Serilog;

namespace UnitTestProject
{
	[TestClass]
	public static class InitTests
	{
		[AssemblyInitialize]
		public static void Init(TestContext ctx)
		{
			Log.Logger = new LoggerConfiguration()
				.WriteTo.File("SRS.log")
				.CreateLogger();
			Log.Logger.Information("Starting unit tests");
		}
	}
}
