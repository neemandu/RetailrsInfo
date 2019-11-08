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
            var isGood = Program.IsEmailGood("nate@malcolmsrestaurant.com");
            Assert.IsFalse(isGood);
        }
    }
}