using Microsoft.VisualStudio.TestTools.UnitTesting;
using yelp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace yelp.Tests
{
    [TestClass()]
    public class ProgramTests
    {
        [TestMethod()]
        public void IsEmailGoodTest()
        {
            var isGood = Program.IsEmailGood(@"//8b4e078a51d04e0e9efdf470027f0ec1@sentry.wixpress.com");
            Assert.IsFalse(isGood);
        }
    }
}