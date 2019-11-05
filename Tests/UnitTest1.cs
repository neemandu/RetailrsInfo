using System;
using System.Net.Http;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using yelp;

namespace Tests
{
    [TestClass]
    public class UnitTest1
    {
        [TestMethod]
        public void AddSocialTest()
        {
            HttpClient _httpClient = new HttpClient();
            Program.GetSocialFromWebSite("http://www.romanjewels.com/", out string fb, out string instagram, out string email,
                                                        out string linkedin, out string twitter, _httpClient);
            //Database d = new Database(@"C:\Projects\RetailrsInfo\yelp\bin\Debug\database.sqlite3");
            //d.UpdateSocial("http://www.romanjewels.com/", fb, instagram, email, linkedin, twitter);
        }

    }
}
