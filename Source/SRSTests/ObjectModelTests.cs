using FDA.SRS.ObjectModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Serilog;
using System.Xml.Linq;
namespace UnitTestProject
{
	[TestClass]
	public class ObjectModelTests
	{

        private TestContext testContextInstance;

        /// <summary>
        ///  Gets or sets the test context which provides
        ///  information about and functionality for the current test run.
        ///</summary>
        public TestContext TestContext
        {
            get { return testContextInstance; }
            set { testContextInstance = value; }
        }
        [TestMethod]
		public void AmountTest()
		{

            Amount a = new Amount();
            TestContext.WriteLine("Message...");
            XElement xSpl = a.SPL;
			Assert.IsNotNull(xSpl);

			a = new Amount(123);
			xSpl = a.SPL;
			Assert.IsNotNull(xSpl);

			a = new Amount(1, 2, 3);
			xSpl = a.SPL;
			Assert.IsNotNull(xSpl);

			xSpl = Amount.UncertainZero.SPL;
			Assert.IsNotNull(xSpl);

			xSpl = Amount.UncertainNonZero.SPL;
			Assert.IsNotNull(xSpl);
		}

        [TestMethod]
        public void DenominatorTest()
        {
            Amount a = new Amount();
            //If denominator is set to 0, that's a problem.
            //This test makes sure that it will be set to 1 instead.
            
            a.Denominator = 0;

            Assert.AreEqual(a.Denominator, 1);
        }
    }
}
