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
    public class DomainFinderTests
    {
        [TestMethod()]
        public void GetAllWebSitesTest()
        {
            DomainFinder df = new DomainFinder();
            df.GetAllWebSites("Barnes & Noble", "",  "Barnes & Noble Albany NY");
        }
    }
}