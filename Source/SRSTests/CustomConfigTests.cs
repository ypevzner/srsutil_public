using FDA.SRS.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;

namespace UnitTestProject
{
	[TestClass]
	public class CustomConfigTests
	{
		[TestMethod]
		public void CodesConfigSectionTest()
		{
			var s = SplCodesSection.Get("SPL.Codes");
			Assert.IsNotNull(s);
			Assert.IsTrue(s.Codes.Count() > 0);

			Assert.IsFalse(String.IsNullOrWhiteSpace(s.Codes["AMINO ACID SUBSTITUTION POINT"].Description));
		}
	}
}
