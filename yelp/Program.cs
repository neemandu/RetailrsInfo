using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Yelp.Api.Models;

namespace yelp
{
    public class Program
    {
        private static readonly NLog.Logger _logger = NLog.LogManager.GetCurrentClassLogger();
        static string _hunter_api_key = ConfigurationManager.AppSettings.Get("hunter_api_key");
        static void Main(string[] args)
        {
            string yelp_api_key = ConfigurationManager.AppSettings.Get("yelp_api_key");
            var yelpClient = new Yelp.Api.Client(yelp_api_key);
            Database db = new Database();
            DomainFinder df = new DomainFinder();
            Dictionary<string, List<EmailDetails>> domainsEmails = new Dictionary<string, List<EmailDetails>>();


            List<string> locations = GetLocations();
            List<string> categories = GetCategories();
            int indent = 0;
            List<Details> details = new List<Details>();
            foreach (string location in locations)
            {
                _logger.Info($"Exploring {location}");
                try
                {
                    foreach (string category in categories)
                    {
                        _logger.Info($"     Category: {category}");
                        int offset = 0;
                        try
                        {
                            var request = new Yelp.Api.Models.SearchRequest
                            {
                                Categories = category,
                                MaxResults = 50,
                                Location = location,
                                ResultsOffset = offset
                            };

                            var businesses = yelpClient.SearchBusinessesAllAsync(request).Result;
                            do
                            {
                                _logger.Info($"         Found {businesses.Businesses?.Count ?? 0} businesses... offset {offset}");
                                if (businesses != null && businesses.Businesses != null)
                                {
                                    foreach (var business in businesses.Businesses)
                                    {
                                        try
                                        {
                                            string str = $"{business.Name} {business.Location.City} {business.Location.State}";

                                            List<DomainCompany> domComPair = df.GetAllWebSites(str);
                                            _logger.Info($"             Found {domComPair.Count} domains in google maps for {str}");
                                            foreach (var pair in domComPair)
                                            {
                                                try
                                                {

                                                    _logger.Info($"                 Domain: {pair.Domain} | Company {pair.Company}");
                                                    // fb and instagram
                                                    string realdomain = "";
                                                    string facebook = pair.Domain.Contains("fac") && pair.Domain.Contains("book") ? pair.Domain : "";
                                                    string insta = pair.Domain.Contains("insta") ? pair.Domain : "";
                                                    realdomain = string.IsNullOrEmpty(facebook) && string.IsNullOrEmpty(insta) ? pair.Domain : "";

                                                    // get emails
                                                    List<EmailDetails> emails = new List<EmailDetails>();
                                                    int numOfEmails = 0;
                                                    bool areEmailInDb = false;
                                                    if (domainsEmails.ContainsKey(pair.Domain))
                                                    {
                                                        _logger.Info($"                 Domain: {pair.Domain} is in the cache");
                                                        emails = domainsEmails[pair.Domain];
                                                    }
                                                    else
                                                    {
                                                        List<EmailDetails> emailsFromDb = GetDomainEmailsFromDB(realdomain, pair.Company, db);
                                                        if (emailsFromDb != null && emailsFromDb.Count > 0)
                                                        {
                                                            _logger.Info($"                 Domain: {realdomain} is in the DB");
                                                            emails = emailsFromDb;
                                                            areEmailInDb = true;
                                                        }
                                                        else
                                                        {
                                                            _logger.Info($"                 Getting emails from hunter");
                                                            string url = $"https://api.hunter.io/v2/domain-search?domain={realdomain}&limit=5&api_key={_hunter_api_key}";
                                                            if (string.IsNullOrEmpty(realdomain))
                                                                url = $"https://api.hunter.io/v2/domain-search?company={pair.Company}&limit=5&api_key={_hunter_api_key}";
                                                            _logger.Info("                 Searching hunter with url: ");
                                                            _logger.Info($"                 {url} ");

                                                            string responseBody = "";
                                                            using (var client = new HttpClient())
                                                            {
                                                                var res = client.GetAsync(url).GetAwaiter().GetResult();
                                                                using (var sr = new StreamReader(res.Content.ReadAsStreamAsync().GetAwaiter().GetResult()))
                                                                {
                                                                    responseBody = sr.ReadToEnd();
                                                                }
                                                            }

                                                            _logger.Info($"                 hunter response: {responseBody}");
                                                            JObject o = JObject.Parse(responseBody);
                                                            var hunter_emails = o["data"]["emails"];
                                                            emails = CreateEmailListFromHunter(hunter_emails);
                                                            numOfEmails = int.Parse(o["meta"]["results"]?.Value<string>());
                                                            realdomain = string.IsNullOrEmpty(realdomain) ? o["data"]["domain"].Value<string>() : realdomain;
                                                        }
                                                    }



                                                    _logger.Info($"                 Found {emails.Count()} emails for domain: {realdomain} | company: {pair.Company}");

                                                    List<string> emailAddrs = new List<string>();
                                                    string linkedinadd = "";
                                                    string twitt = "";
                                                    if (!string.IsNullOrEmpty(realdomain))
                                                    {
                                                        _logger.Info($"                 GetSocialFromWebSite: {realdomain}");
                                                        GetSocialFromWebSite(realdomain, out string fb, out string instagram, out List<string> emailsList,
                                                            out string linkedin, out string twitter);
                                                        emailAddrs = emailsList;
                                                        linkedinadd = linkedin;
                                                        twitt = twitter;
                                                        facebook = string.IsNullOrEmpty(fb) ? facebook : fb;
                                                        insta = string.IsNullOrEmpty(instagram) ? insta : instagram;
                                                    }
                                                    _logger.Info("                 Iterating emails from website");
                                                    foreach (string m in emailAddrs)
                                                    {
                                                        if (!emails.Any(mai => mai.Email == m))
                                                        {
                                                            if (IsEmailGood(m))
                                                            {
                                                                emails.Add(new EmailDetails
                                                                {
                                                                    Email = m
                                                                });

                                                            }
                                                        }
                                                    }

                                                    if (!domainsEmails.ContainsKey(pair.Domain))
                                                    {
                                                        if (!areEmailInDb)
                                                        {
                                                            List<EmailDetails> goodEmails = new List<EmailDetails>();
                                                            foreach (var em in emails)
                                                            {
                                                                if (IsEmailGood(em.Email))
                                                                {
                                                                    goodEmails.Add(em);
                                                                    AddEmailToDb(pair.Domain, pair.Company, em, db);
                                                                }
                                                            }
                                                            _logger.Info($"                 Good emails: {goodEmails.Count} / {emails.Count}");

                                                            emails = goodEmails;

                                                        }

                                                        domainsEmails.Add(pair.Domain, emails);

                                                    }

                                                    int emailCounter = 0;
                                                    DeleteRecordsInDb(pair.Domain, db);
                                                    foreach (var i in emails)
                                                    {
                                                        emailCounter++;
                                                        _logger.Info($"                 Adding record to DB {i.Email}");
                                                        AddRecordToDb(new yelp.Details
                                                        {
                                                            Domain = pair.Domain,
                                                            Email = i.Email,
                                                            FirstName = i.FirstName,
                                                            LastName = i.LastName,
                                                            Position = i.Position,
                                                            LinkedIn = i.LinkedIn,
                                                            Twitter = i.Twitter,
                                                            Seniority = i.Seniority,
                                                            City = business.Location.City,
                                                            State = business.Location.State,
                                                            Category = string.Join(", ", business.Categories.Select(c => c.Title).ToList<string>()),
                                                            StoreName = pair.Company,
                                                            Phone = string.IsNullOrEmpty(business.Phone) ? i.Phone : business.Phone,
                                                            Facebook = facebook,
                                                            Rating = business.Rating,
                                                            Reviewers = business.ReviewCount,
                                                            Instagram = insta,
                                                            Departmnt = i.Departmnt,
                                                            RetailsType = numOfEmails > 8 ? "Chain" : "Store",
                                                            Address1 = business.Location.Address1,
                                                            Address2 = business.Location.Address2,
                                                            ZipCode = business.Location.ZipCode
                                                        }, db);
                                                    }

                                                    if (emailCounter == 0)
                                                    {
                                                        AddRecordToDb(new yelp.Details
                                                        {
                                                            Domain = pair.Domain,
                                                            Email = null,
                                                            FirstName = null,
                                                            LastName = null,
                                                            Position = null,
                                                            LinkedIn = linkedinadd,
                                                            Twitter = twitt,
                                                            Seniority = null,
                                                            City = business.Location.City,
                                                            State = business.Location.State,
                                                            Category = string.Join(", ", business.Categories.Select(c => c.Title).ToList<string>()),
                                                            StoreName = pair.Company,
                                                            Phone = business.Phone,
                                                            Facebook = facebook,
                                                            Rating = business.Rating,
                                                            Reviewers = business.ReviewCount,
                                                            Instagram = insta,
                                                            Departmnt = null,
                                                            RetailsType = numOfEmails > 8 ? "Chain" : "Store",
                                                            Address1 = business.Location.Address1,
                                                            Address2 = business.Location.Address2,
                                                            ZipCode = business.Location.ZipCode
                                                        }, db);
                                                    }
                                                }
                                                catch (Exception x)
                                                {
                                                    _logger.Error(x, $"domain : {pair.Domain}  | company : {pair.Company}");
                                                }
                                            }
                                        }
                                        catch (Exception x)
                                        {
                                            _logger.Error(x, "");
                                        }
                                    }
                                    offset += 50;
                                    request.ResultsOffset = offset;
                                    businesses = yelpClient.SearchBusinessesAllAsync(request).Result;
                                }
                            }
                            while ((businesses?.Businesses?.Count ?? 0) > 0);
                        }
                        catch (Exception x)
                        { _logger.Error(x, ""); }
                    }
                }
                catch (Exception x)
                { _logger.Error(x, ""); }
            }
        }

        private static void DeleteRecordsInDb(string domain, Database db)
        {
            string query = $@"delete from Stores where Domain = @Domain";
            db.DeleteRecordsInDb(query, domain, db);
        }

        private static void DeleteEmailsInDb(string domain, Database db)
        {
            string query = $@"delete from DomainEmails where Domain = @Domain";
            db.DeleteEmailsInDb(query, domain, db);
        }

        private static void AddEmailToDb(string domain, string company, EmailDetails item, Database db)
        {
            string query = $@"insert into DomainEmails
                            values (
    @Domain,
    @Company,
	@Departmnt,
	@Phone,
	@Seniority,
	@Twitter   ,
	@LinkedIn  ,
	@Position  ,
	@LastName  ,
	@FirstName ,
	@Email
)";
            item.Domain = domain;
            item.Company = company;
            db.AddEmailToDb(query, item, db);
        }

        private static List<EmailDetails> GetDomainEmailsFromDB(string realdomain, string company, Database db)
        {
            List<EmailDetails> ret = new List<EmailDetails>();
            try
            {
                string query = $@"select * from DomainEmails 
                                  where Domain = @Domain";
                if (string.IsNullOrEmpty(realdomain))
                    query = $@"select * from DomainEmails 
                                  where Company = @Company";

                ret = db.GetEmails(query, realdomain, company, db);
                return ret;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "");
                return new List<EmailDetails>();
            }
        }

        private static List<EmailDetails> CreateEmailListFromHunter(JToken hunter_emails)
        {
            List<EmailDetails> ret = new List<EmailDetails>();
            foreach (var email in hunter_emails)
            {
                ret.Add(new EmailDetails
                {
                    Email = email["value"].ToString(),
                    FirstName = email["first_name"].ToString(),
                    LastName = email["last_name"].ToString(),
                    Departmnt = email["department"].ToString(),
                    LinkedIn = email["linkedin"].ToString(),
                    Phone = email["phone_number"].ToString(),
                    Position = email["position"].ToString(),
                    Seniority = email["seniority"].ToString(),
                    Twitter = email["twitter"].ToString()
                });
            }

            return ret;
        }

        public static bool IsEmailGood(string mail)
        {
            List<string> badEmailFormats = GetBadEmailFormats();
            foreach (string format in badEmailFormats)
            {
                if (string.IsNullOrWhiteSpace(mail) || mail.Contains(format))
                    return false;
            }

            string url = $"https://api.hunter.io/v2/email-verifier?email={mail}&api_key={_hunter_api_key}";

            string responseBody = "";
            using (var client = new HttpClient())
            {
                var res = client.GetAsync(url).GetAwaiter().GetResult();
                using (var sr = new StreamReader(res.Content.ReadAsStreamAsync().GetAwaiter().GetResult()))
                {
                    responseBody = sr.ReadToEnd();
                }
            }


            string result = "";
            JObject o = JObject.Parse(responseBody);
            if (o.ContainsKey("data"))
            {
                JObject ss = JObject.Parse(o["data"].ToString());
                if (!ss.ContainsKey("result"))
                {
                    _logger.Error($"HUNTER CHANGED API - no data->result key");
                    Environment.Exit(1);
                }

                result = o["data"]["result"]?.ToString() ?? "undeliverable";
            }
            if (o.ContainsKey("errors"))
            {
                _logger.Error($"hunter error - did not find email {mail}. msg - {o["errors"].ToString()}");
                result = "undeliverable";
            }
            return result != "undeliverable";
        }

        private static List<string> GetBadEmailFormats()
        {
            return new List<string>
            {
                "example",
                "wixpress",
                @"//"
            };
        }

        private static void GetSocialFromWebSite(string domain, out string fb, out string instagram,
            out List<string> emailsList, out string linkedin, out string twitter)
        {
            fb = "";
            instagram = "";
            emailsList = new List<string>();
            linkedin = "";
            twitter = "";
            string responseBody = "";
            try
            {
                if (domain.Contains("http") || domain.Contains("https") || domain.Contains("www"))
                {
                    _logger.Info("             GetSocialFromWebSite has start");
                    using (var client = new HttpClient())
                    {
                        var res = client.GetAsync(domain).GetAwaiter().GetResult();
                        using (var sr = new StreamReader(res.Content.ReadAsStreamAsync().GetAwaiter().GetResult()))
                        {
                            responseBody = sr.ReadToEnd();
                        }
                    }



                    if (!string.IsNullOrEmpty(responseBody))
                    {

                        string fbregex = @"(?:(?:http|https):\/\/)?(?:www.)?facebook.com\/(?:(?:\w)*#!\/)?(?:pages\/)?(?:[?\w\-]*\/)?(?:profile.php\?id=(?=\d.*))?([\w\-]*)?";
                        fb = GetMatched(responseBody, fbregex);

                        string instregex = @"(?:(?:http|https):\/\/)?(?:www.)?(?:instagram.com|instagr.am)\/([A-Za-z0-9-_\.]+)";
                        instagram = GetMatched(responseBody, instregex);

                        string emailregex = @"([a-zA-Z0-9.!#$%&'*+\/=?^_`{|}~-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,})";
                        emailsList = GetMatchedList(responseBody, emailregex);

                        string linkedinregex = @"(?:(?:http|https):\/\/)?(?:www.)?(?:linkedin.com)(\/([A-Za-z0-9-_\.]+))+";
                        linkedin = GetMatched(responseBody, linkedinregex);

                        string twitterregex = @"(?:(?:http|https):\/\/)?(?:www.)?(?:twitter.com|instagr.am)\/([A-Za-z0-9-_\.]+)";
                        twitter = GetMatched(responseBody, twitterregex);
                    }
                }
                else
                {
                    _logger.Info("             GetSocialFromWebSite has no start");
                    responseBody = GetSiteContent(domain, "http://");
                    if (string.IsNullOrEmpty(responseBody))
                        responseBody = GetSiteContent(domain, "https://");
                    if (string.IsNullOrEmpty(responseBody))
                        responseBody = GetSiteContent(domain, "http://www.");
                    if (string.IsNullOrEmpty(responseBody))
                        responseBody = GetSiteContent(domain, "https://www.");
                    if (!string.IsNullOrEmpty(responseBody))
                    {

                        string fbregex = @"(?:(?:http|https):\/\/)?(?:www.)?facebook.com\/(?:(?:\w)*#!\/)?(?:pages\/)?(?:[?\w\-]*\/)?(?:profile.php\?id=(?=\d.*))?([\w\-]*)?";
                        fb = GetMatched(responseBody, fbregex);

                        string instregex = @"(?:(?:http|https):\/\/)?(?:www.)?(?:instagram.com|instagr.am)\/([A-Za-z0-9-_\.]+)";
                        instagram = GetMatched(responseBody, instregex);

                        string emailregex = @"([a-zA-Z0-9.!#$%&'*+\/=?^_`{|}~-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,})";
                        emailsList = GetMatchedList(responseBody, emailregex);

                        string linkedinregex = @"(?:(?:http|https):\/\/)?(?:www.)?(?:linkedin.com)(\/([A-Za-z0-9-_\.]+))+";
                        linkedin = GetMatched(responseBody, linkedinregex);

                        string twitterregex = @"/http(?:s)?:\/\/(?:www\.)?twitter\.com\/([a-zA-Z0-9_]+)/";
                        twitter = GetMatched(responseBody, twitterregex);
                    }
                }
                _logger.Info("             GetSocialFromWebSite finished");
            }
            catch (Exception e) 
            {
                _logger.Error(e, e.Message);
            }
        }

        private static string GetSiteContent(string domain, string val)
        {
            if (!domain.Contains(val))
            {
                try
                {
                    string url = val + domain;

                    string responseBody = "";
                    using (var client = new HttpClient())
                    {
                        var res = client.GetAsync(url).GetAwaiter().GetResult();
                        using (var sr = new StreamReader(res.Content.ReadAsStreamAsync().GetAwaiter().GetResult()))
                        {
                            responseBody = sr.ReadToEnd();
                        }
                    }


                    return responseBody;
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, ex.Message);
                    return "";
                }
            }
            else
            {
                try
                {

                    string responseBody = "";
                    using (var client = new HttpClient())
                    {
                        var res = client.GetAsync(domain).GetAwaiter().GetResult();
                        using (var sr = new StreamReader(res.Content.ReadAsStreamAsync().GetAwaiter().GetResult()))
                        {
                            responseBody = sr.ReadToEnd();
                        }


                    }

                    return responseBody;
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, ex.Message);
                    return "";
                }
            }
        }

        private static string GetMatched(string responseBody, string regex)
        {
            Match m = Regex.Match(responseBody, regex, RegexOptions.IgnoreCase);
            if (m.Success && ((m.Groups?.Count ?? 0) > 0))
            {
                return m.Groups[0].Value;
            }
            return null;
        }

        private static List<string> GetMatchedList(string responseBody, string regex)
        {
            Dictionary<string, bool> retList = new Dictionary<string, bool>();
            var matches = Regex.Matches(responseBody, regex, RegexOptions.IgnoreCase);
            foreach (var match in matches)
            {
                string key = match.ToString();
                if (!retList.ContainsKey(key))
                    retList.Add(key, true);
            }
            return retList?.Keys?.ToList() ?? new List<string>();
        }

        private static List<string> GetCategories()
        {
            //return new List<string> { "watches", "giftshops" };
            return new List<string> { "giftshops", "officeequipment", "bookstores" };
        }

        private static List<string> GetLocations()
        {
            return new List<string> { //"Albany, NY",
//"Amsterdam, NY",
//"Auburn, NY",
//"Batavia, NY",
//"Beacon, NY",
//"Binghamton, NY",
//"Buffalo, NY",
//"Canandaigua, NY",
//"Cohoes, NY",
//"Corning, NY",
//"Cortland, NY",
//"Dunkirk, NY",
//"Elmira, NY",
//"Fulton, NY",
//"Geneva, NY",
//"Glen Cove, NY",
//"Glens Falls, NY",
//"Gloversville, NY",
//"Hornell, NY",
//"Hudson, NY",
//"Ithaca, NY",
//"Jamestown, NY",
//"Johnstown, NY",
//"Kingston, NY",
//"Lackawanna, NY",
//"Little Falls, NY",
//"Lockport, NY",
//"Long Beach, NY",
//"Mechanicville, NY",
//"Middletown, NY",
//"Mount Vernon, NY",
//"New Rochelle, NY",
//"New York, NY",
//"Newburgh, NY",
//"Niagara Falls, NY",
//"North Tonawanda, NY",
//"Norwich, NY",
//"Ogdensburg, NY",
//"Olean, NY",
//"Oneida, NY",
//"Oneonta, NY",
//"Oswego, NY",
//"Peekskill, NY",
//"Plattsburgh, NY",
//"Port Jervis, NY",
//"Poughkeepsie, NY",
//"Rensselaer, NY",
//"Rochester, NY",
//"Rome, NY",
//"Rye, NY",
//"Salamanca, NY",
//"Saratoga Springs, NY",
"Schenectady, NY",
"Sherrill, NY",
"Syracuse, NY",
"Tonawanda, NY",
"Troy, NY",
"Utica, NY",
"Watertown, NY",
"Watervliet, NY",
"White Plains, NY",
"Yonkers, NY"
};
        }

        private static void AddRecordToDb(Details details, Database db)
        {
            try
            {
                //                string update = $@"update Stores
                //set Domain = @Domain,
                //Email = @Email,FirstName = @FirstName, LastName = @LastName, Facebook = @Facebook,
                //Rating = @Rating, Reviewers = @Reviewers, Instagram = @Instagram
                //where StoreName = @StoreName and City = @City";
                string query = $@"INSERT INTO Stores 
                            VALUES (@Domain, 
                            @Category, 
                            @StoreName, 
                            @City, 
                            @State, 
                            @Email,
                            @FirstName,
                            @LastName,
                            @Phone,
                            @Facebook, 
                            @Rating,
                            @Reviewers,
                            @Instagram,
                            @Position,
                            @LinkedIn,
                            @Seniority,
                            @Twitter,
                            @Departmnt,
                            @RetailsType,
                            @ZipCode,
                            @Address1,
                            @Address2
                            )";
                db.ExecuteNonQuery(query, details.Domain, details.Category, details.StoreName, details.City
                    , details.State, details.Email, details.FirstName, details.LastName, details.Phone, details.Facebook,
                    details.Rating, details.Reviewers, details.Instagram, details.Position, details.LinkedIn, details.Seniority, details.Twitter
                    , details.Departmnt, details.RetailsType, details.Address1, details.Address2, details.ZipCode);
            }
            catch (Exception ex)
            {
                _logger.Error("ERROR AddRecordToDb : Domain: " + details.Domain);
                _logger.Error("ERROR AddRecordToDb : Category: " + details.Category);
                _logger.Error("ERROR AddRecordToDb : StoreName: " + details.StoreName);
                _logger.Error("ERROR AddRecordToDb : City: " + details.City);
                _logger.Error("ERROR AddRecordToDb : State: " + details.State);
                _logger.Error("ERROR AddRecordToDb : Email: " + details.Email);
                _logger.Error("ERROR AddRecordToDb : FirstName: " + details.FirstName);
                _logger.Error("ERROR AddRecordToDb : LastName: " + details.LastName);
                _logger.Error("ERROR AddRecordToDb : Phone: " + details.Phone);
                _logger.Error(ex, "");
            }
        }


        private static void WriteToExcel(List<Details> details, string category, string location)
        {
            _logger.Info("Writing to Excel...");
            Microsoft.Office.Interop.Excel.Application oXL;
            Microsoft.Office.Interop.Excel._Workbook oWB;
            Microsoft.Office.Interop.Excel._Worksheet oSheet;
            Microsoft.Office.Interop.Excel.Range oRng;
            object misvalue = System.Reflection.Missing.Value;
            try
            {
                //Start Excel and get Application object.
                oXL = new Microsoft.Office.Interop.Excel.Application();
                oXL.Visible = true;

                //Get a new workbook.
                oWB = (Microsoft.Office.Interop.Excel._Workbook)(oXL.Workbooks.Add(""));
                oSheet = (Microsoft.Office.Interop.Excel._Worksheet)oWB.ActiveSheet;

                //Add table headers going cell by cell.

                oSheet.Cells[1, 1] = "State";
                oSheet.Cells[1, 2] = "City";
                oSheet.Cells[1, 3] = "Category";
                oSheet.Cells[1, 4] = "Store Name";
                oSheet.Cells[1, 5] = "Domain";
                oSheet.Cells[1, 6] = "Email";
                oSheet.Cells[1, 7] = "First Name";
                oSheet.Cells[1, 8] = "Last Name";
                oSheet.Cells[1, 9] = "Phone";
                //oSheet.Cells[1, 9] = "Yelp Url";

                //Format A1:D1 as bold, vertical alignment = center.
                oSheet.get_Range("A1", "I1").Font.Bold = true;
                oSheet.get_Range("A1", "I1").VerticalAlignment =
                    Microsoft.Office.Interop.Excel.XlVAlign.xlVAlignCenter;

                // Create an array to multiple values at once.
                string[,] saNames = new string[details.Count, 9];

                for (int i = 0; i < details.Count; i++)
                {
                    saNames[i, 0] = details[i].State;
                    saNames[i, 1] = details[i].City;
                    saNames[i, 2] = details[i].Category;
                    saNames[i, 3] = details[i].StoreName;
                    saNames[i, 4] = details[i].Domain;
                    saNames[i, 5] = details[i].Email;
                    saNames[i, 6] = details[i].FirstName;
                    saNames[i, 7] = details[i].LastName;
                    saNames[i, 8] = details[i].Phone;
                    //saNames[i, 8] = details[i].yelpUrl;
                }

                //Fill A2:B6 with an array of values (First and Last Names).
                oSheet.get_Range("A2", $"I{details.Count + 1}").Value2 = saNames;



                //AutoFit columns A:D.
                oRng = oSheet.get_Range("A1", "I1");
                oRng.EntireColumn.AutoFit();

                oXL.Visible = false;
                oXL.UserControl = false;
                oWB.SaveAs($"c:\\temp\\yelp_{location}_{category}_{DateTime.Now.ToLongDateString()}.xlsx", Microsoft.Office.Interop.Excel.XlFileFormat.xlWorkbookDefault, Type.Missing, Type.Missing,
                    false, false, Microsoft.Office.Interop.Excel.XlSaveAsAccessMode.xlNoChange,
                    Type.Missing, Type.Missing, Type.Missing, Type.Missing, Type.Missing);

                oWB.Close();
                _logger.Info("Finished writing to Excel!");
                details = new List<Details>();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, ex.Message);
            }
        }

        private static void GetDomain(string url, out string domain)
        {
            try
            {
                string responseBody = "";
                using (var client = new HttpClient())
                {
                    var res = client.GetAsync(url).GetAwaiter().GetResult();
                    using (var sr = new StreamReader(res.Content.ReadAsStreamAsync().GetAwaiter().GetResult()))
                    {
                        responseBody = sr.ReadToEnd();
                    }

                }


                string sub = responseBody.Substring(responseBody.IndexOf("href=\"/biz_redir?url="));
                sub = sub.Substring(0, sub.IndexOf("&amp"));
                domain = WebUtility.UrlDecode(sub.Substring(21));
                domain = domain.Substring(domain.IndexOf("http://") < 0 ? 0 : domain.IndexOf("http://") + 7);
                domain = domain.Substring(domain.IndexOf("www.") < 0 ? 0 : domain.IndexOf("www.") + 4);
            }
            catch (Exception ex) {
                _logger.Error(ex, ex.Message);
                domain = ""; }
        }
    }
}
