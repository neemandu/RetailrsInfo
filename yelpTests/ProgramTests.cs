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
            var isGood = Program.IsEmailGood(@"info@happydazecostumes.com");
            Assert.IsTrue(isGood);
        }

        [TestMethod()]
        public void GetSocialFromWebSiteTest()
        {
            Program.GetSocialFromWebSite("partytime-rentals.com", out string fb, out string instagram,
                out List<string> emailList, out string linkedin, out string twitter, out string phone);
        }

        [TestMethod()]
        public void RunTest()
        {
            List<string> locations = new List<string> { "New York, NY"};
            List<string> categories = new List<string> { "partysupplies" };
            Program.Run(locations, categories);
        }
    }
}