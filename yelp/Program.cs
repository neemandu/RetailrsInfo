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
    public class StringConstants
    {
        public static readonly string STATUS_GOOD = "GOOD";
        public static readonly string STATUS_MEDIUM = "MEDIUM";
        public static readonly string STATUS_BAD = "BAD";
        public static readonly string CATEGORY_STORE = "STORE";
        public static readonly string CATEGORY_CHAIN = "CHAIN";
    }

    public class Program
    {
        private static readonly NLog.Logger _logger = NLog.LogManager.GetCurrentClassLogger();
        static string _hunter_api_key = ConfigurationManager.AppSettings.Get("hunter_api_key");
        private static int _numOfEmailsForChain = 8;

        static void Main(string[] args)
        {
            //Test();

            string yelp_api_key = ConfigurationManager.AppSettings.Get("yelp_api_key");
            var yelpClient = new Yelp.Api.Client(yelp_api_key);
            Database db = new Database();
            DomainFinder df = new DomainFinder();
            Dictionary<string, List<EmailDetails>> domainsEmails = new Dictionary<string, List<EmailDetails>>();


            List<string> locations = GetLocations();
            List<string> categories = GetCategories();
            List<Details> details = new List<Details>();
            foreach (string location in locations)
            {
                _logger.Info($"Exploring {location}");
                try
                {
                    foreach (string category in categories)
                    {
                        _logger.Info($"     Category: {category}");
                        try
                        {
                            int maxResults = 50;
                            int offset = 0;

                            SearchResponse businesses = GetBusineesesFromYelp(category, maxResults, location, offset);
                            do
                            {
                                _logger.Info($"         Found {businesses.Businesses?.Count ?? 0} businesses... offset {offset}");
                                if (businesses != null && businesses.Businesses != null)
                                {
                                    foreach (var business in businesses.Businesses)
                                    {
                                        try
                                        {
                                            List<GoogleStoreModel> googleStores = new List<GoogleStoreModel>();

                                            List<string> placeIdsByPhone = df.GetPlaceIdsByPhone(business.Phone);

                                            if (placeIdsByPhone == null || placeIdsByPhone.Count == 0)
                                            {
                                                string str = $"{business.Name} {business.Location.City} {business.Location.State}";

                                                List<string> placeIdsBySearchTerm = df.GetPlaceIdsBySearchTerm(str);
                                                if (placeIdsBySearchTerm == null || placeIdsBySearchTerm.Count == 0)
                                                {
                                                    InsertYelpDataToDB(business, StringConstants.STATUS_GOOD, db);
                                                    continue;
                                                }
                                                else
                                                {
                                                    googleStores = GetGoogleStoresByIds(placeIdsBySearchTerm, df);
                                                    if (googleStores.Any(item => ArePhonesTheSame(business.Phone, item.Phone)))
                                                    {
                                                        googleStores = googleStores.Where(item => ArePhonesTheSame(business.Phone, item.Phone)).ToList();
                                                    }
                                                }
                                            }
                                            else
                                            {
                                                googleStores = GetGoogleStoresByIds(placeIdsByPhone, df);
                                            }

                                            foreach (var googleStore in googleStores)
                                            {
                                                if (!IsWebsiteWorking(googleStore.Website))
                                                {
                                                    InsertYelpAndGoogleDataToDB(null, null, null, null, null, business,
                                                        StringConstants.CATEGORY_STORE, StringConstants.STATUS_BAD, business.Phone, db);
                                                    continue;
                                                }
                                                else
                                                {
                                                    string domain = df.GetDomainFromUrl(googleStore.Website);

                                                    _logger.Info($"GetSocialFromWebSite: {domain}");
                                                    GetSocialFromWebSite(domain, out string fb, out string instagram, out List<string> emailsList,
                                                        out string linkedin, out string twitter, out string phone);

                                                    List<EmailDetails> emails = GetDomainEmailsFromDB(domain, googleStore.Name, db);
                                                    int numOfEMailsFromHunter = emails?.Count ?? 0;
                                                    if (emails == null || emails.Count == 0)
                                                    {
                                                        (emails, numOfEMailsFromHunter) = GetEmailsFromHunter(domain, googleStore.Name);
                                                    }

                                                    emails = emails.Where(email => IsEmailGood(email.Email)).ToList();

                                                    if (numOfEMailsFromHunter >= _numOfEmailsForChain)
                                                    {
                                                        InsertYelpAndGoogleDataToDB(domain, fb, instagram, linkedin, twitter,  business, 
                                                            StringConstants.CATEGORY_CHAIN, StringConstants.STATUS_GOOD, phone, db);
                                                    }
                                                    else
                                                    {
                                                        string sitePhone = string.IsNullOrEmpty(phone) ? googleStore.Phone : phone;
                                                        if (ArePhonesTheSame(business.Phone, sitePhone))
                                                        {
                                                            InsertYelpGoogleAndHunterDataToDB(business, googleStore, emails, StringConstants.CATEGORY_STORE, StringConstants.STATUS_GOOD, db);
                                                        }
                                                        else
                                                        {
                                                            InsertYelpDataToDB(business, StringConstants.STATUS_MEDIUM, db);
                                                            InsertGoogleHunterDataToDB(business, googleStore, emails, StringConstants.STATUS_MEDIUM, db);
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            _logger.Error(ex, "");
                                        }
                                    }
                                    offset += 50;
                                    businesses = GetBusineesesFromYelp(category, maxResults, location, offset);
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
            _logger.Info("FINISH");

        }

        private static void InsertYelpAndGoogleDataToDB(string domain, string fb, string instagram, string linkedin, string twitter,
            BusinessResponse business, string category, string status,string phone, Database db)
        {
            AddRecordToDb(new yelp.Details
            {
                Domain = domain,
                Email = null,
                FirstName = null,
                LastName = null,
                Position = null,
                LinkedIn = linkedin,
                Twitter = twitter,
                Seniority = null,
                City = business.Location.City,
                State = business.Location.State,
                Category = string.Join(", ", business.Categories.Select(c => c.Title).ToList<string>()),
                StoreName = business.Name,
                Phone = phone,
                Facebook = fb,
                Rating = business.Rating,
                Reviewers = business.ReviewCount,
                Instagram = instagram,
                Departmnt = null,
                RetailsType = category,
                Address1 = business.Location.Address1,
                Address2 = business.Location.Address2,
                ZipCode = business.Location.ZipCode,
                InfoQuality = status
            }, db);
        }

        private static void InsertYelpGoogleAndHunterDataToDB(BusinessResponse business, GoogleStoreModel googleStore, 
            List<EmailDetails> emails, string category, string status, Database db)
        {
            foreach (var email in emails)
            {
                AddRecordToDb(new yelp.Details
                {
                    YelpUrl = business.Url,
                    Domain = googleStore.Website,
                    Email = email.Email,
                    FirstName = email.FirstName,
                    LastName = email.LastName,
                    Position = email.Position,
                    LinkedIn = email.LinkedIn,
                    Twitter = email.Twitter,
                    Seniority = email.Seniority,
                    City = business.Location.City,
                    State = business.Location.State,
                    Category = string.Join(", ", business.Categories.Select(c => c.Title).ToList<string>()),
                    StoreName = business.Name,
                    Phone = business.Phone,
                    Facebook = null,
                    Rating = business.Rating,
                    Reviewers = business.ReviewCount,
                    Instagram = null,
                    Departmnt = email.Departmnt,
                    RetailsType = category,
                    Address1 = business.Location.Address1,
                    Address2 = business.Location.Address2,
                    ZipCode = business.Location.ZipCode,
                    InfoQuality = status
                }, db);
            }
        }

        private static void InsertGoogleHunterDataToDB(BusinessResponse business, GoogleStoreModel googleStore, List<EmailDetails> emails, string status, Database db)
        {
            foreach(var email in emails)
            {
                AddRecordToDb(new yelp.Details
                {
                    YelpUrl = null,
                    Domain = googleStore.Website,
                    Email = email.Email,
                    FirstName = email.FirstName,
                    LastName = email.LastName,
                    Position = email.Position,
                    LinkedIn = email.LinkedIn,
                    Twitter = email.Twitter,
                    Seniority = email.Seniority,
                    City = business.Location.City,
                    State = business.Location.State,
                    Category = string.Join(", ", business.Categories.Select(c => c.Title).ToList<string>()),
                    StoreName = googleStore.Name,
                    Phone = googleStore.Phone,
                    Facebook = null,
                    Rating = -1,
                    Reviewers = -1,
                    Instagram = null,
                    Departmnt = email.Departmnt,
                    RetailsType = (emails?.Count ?? 0) >= _numOfEmailsForChain ? StringConstants.CATEGORY_CHAIN : StringConstants.CATEGORY_STORE,
                    Address1 = business.Location.Address1,
                    Address2 = business.Location.Address2,
                    ZipCode = business.Location.ZipCode,
                    InfoQuality = status
                }, db);
            }
        }

        private static (List<EmailDetails>, int) GetEmailsFromHunter(string domain, string companyName)
        {
            string url = $"https://api.hunter.io/v2/domain-search?domain={domain}&limit=5&api_key={_hunter_api_key}";
            _logger.Info("Searching hunter with domain: " + domain);
            _logger.Info($"{url}");

            string responseBody = "";
            using (var client = new HttpClient())
            {
                var res = client.GetAsync(url).GetAwaiter().GetResult();
                using (var sr = new StreamReader(res.Content.ReadAsStreamAsync().GetAwaiter().GetResult()))
                {
                    responseBody = sr.ReadToEnd();
                }
            }

            _logger.Info($"hunter response: {responseBody}");
            JObject o = JObject.Parse(responseBody);
            var hunter_emails = o["data"]["emails"];
            var emails = CreateEmailListFromHunter(hunter_emails);
            if (emails == null || emails.Count == 0)
            {
                url = $"https://api.hunter.io/v2/domain-search?company={companyName}&limit=5&api_key={_hunter_api_key}";
                _logger.Info("Searching hunter with company name: " + companyName);

                responseBody = "";
                using (var client = new HttpClient())
                {
                    var res = client.GetAsync(url).GetAwaiter().GetResult();
                    using (var sr = new StreamReader(res.Content.ReadAsStreamAsync().GetAwaiter().GetResult()))
                    {
                        responseBody = sr.ReadToEnd();
                    }
                }

                _logger.Info($"hunter response: {responseBody}");
                o = JObject.Parse(responseBody);
                var hunter_emails_comp = o["data"]["emails"];
                emails = CreateEmailListFromHunter(hunter_emails_comp);
                
            }
            int numOfEmails = int.Parse(o["meta"]["results"]?.Value<string>());
            return (emails, numOfEmails);
        }

        private static bool IsWebsiteWorking(string website)
        {
            if (website == null)
                return false;
            using (var client = new HttpClient())
            {
                var res = client.GetAsync(website).GetAwaiter().GetResult();
                if (res.IsSuccessStatusCode)
                {
                    return true;
                }
            }

            return false;
        }

        private static List<GoogleStoreModel> GetGoogleStoresByIds(List<string> placeIdsBySearchTerm, DomainFinder df)
        {
            List<GoogleStoreModel> ret = new List<GoogleStoreModel>();
            foreach (var placeId in placeIdsBySearchTerm.Take(3))
            {
                var store = df.GetGoogleStoreByPlaceId(placeId);
                ret.Add(store);
            }
            return ret;
        }

        private static bool ArePhonesTheSame(string phone1, string phone2)
        {
            string p1 = phone1;
            if(p1.Length > 10)
            {
                p1 = phone1.Substring(phone1.Length - 10, phone1.Length);
            }
            string p2 = phone2;
            if (p2.Length > 10)
            {
                p2 = phone2.Substring(phone2.Length - 10, phone2.Length);
            }
            return ((p1?.Replace("+", "")?.Replace("-", "")?.Replace("(", "")?.Replace(")", "")?.Replace(" ", "")?.Trim() ?? "")
                     == (p2?.Replace("+", "")?.Replace("-", "")?.Replace("(", "")?.Replace(")", "")?.Replace(" ", "")?.Trim() ?? "z"));
        }

        private static void InsertYelpDataToDB(BusinessResponse business, string status, Database db)
        {
            AddRecordToDb(new yelp.Details
            {
                Domain = null,
                Email = null,
                FirstName = null,
                LastName = null,
                Position = null,
                LinkedIn = null,
                Twitter = null,
                Seniority = null,
                City = business.Location.City,
                State = business.Location.State,
                Category = string.Join(", ", business.Categories.Select(c => c.Title).ToList<string>()),
                StoreName = business.Name,
                Phone = business.Phone,
                Facebook = null,
                Rating = business.Rating,
                Reviewers = business.ReviewCount,
                Instagram = null,
                Departmnt = null,
                RetailsType = StringConstants.CATEGORY_STORE,
                Address1 = business.Location.Address1,
                Address2 = business.Location.Address2,
                ZipCode = business.Location.ZipCode,
                InfoQuality = status
            }, db);
        }

        private static void Test()
        {
            string location = "New York, NY";
            string category = "partysupplies";
            var yelpResults = yelp.Program.GetBusineesesFromYelpSmallTypeStore(category, 50, location, 0);

            DomainFinder df = new DomainFinder();

            List<string> storesByNameNotByPhone = new List<string>();
            List<string> storesByPhoneNotByName = new List<string>();
            foreach (var business in yelpResults)
            {
                string searchTerm = $"{business.Name} {business.City} {business.State}";
                List<string> placeIdsBySearchTerm = df.GetPlaceIdsBySearchTerm(searchTerm);
                List<GoogleStoreModel> googleNameResults = new List<GoogleStoreModel>();
                foreach (var place in placeIdsBySearchTerm)
                {
                    var store = df.GetGoogleStoreByPlaceId(place);
                    googleNameResults.Add(store);
                }


                string phone = business.Phone;
                var phoneStorePlaceIds = df.GetPlaceIdsByPhone(phone.Replace("+", ""));
                List<GoogleStoreModel> googlePhoneRs = new List<GoogleStoreModel>();
                foreach (var place in phoneStorePlaceIds)
                {
                    var store = df.GetGoogleStoreByPlaceId(place);
                    googlePhoneRs.Add(store);
                }


                Console.WriteLine($"YELP - Store name: {business.Name} | City: {business.City} | State: {business.State}");
                Console.WriteLine($"Google by search term: {googleNameResults.Count} items | Google by phone: {googlePhoneRs.Count} items");

                if (googleNameResults == null || googleNameResults.Count == 0)
                {
                    Console.WriteLine($"Google by search term - NOT FOUND!!!");
                }
                else
                {
                    Console.WriteLine($"Google by search term results ({googleNameResults.Count} items):");
                    foreach (var place in googleNameResults)
                    {
                        Console.WriteLine($"    Store name: {place.Name} | Phone: {place.Phone} | Website: {place.Website}");
                    }
                }
                if (googlePhoneRs == null || googlePhoneRs.Count == 0)
                {
                    Console.WriteLine($"Google by phone - NOT FOUND!!!");
                }
                else
                {
                    Console.WriteLine($"Google by phone results ({googlePhoneRs.Count} items):");
                    foreach (var place in googlePhoneRs)
                    {
                        Console.WriteLine($"    Store name: {place.Name} | Phone: {place.Phone} | Website: {place.Website}");
                    }
                }

                if (googleNameResults != null && googleNameResults.Count > 0 &&
                    (googlePhoneRs == null || googlePhoneRs.Count == 0))
                {
                    storesByNameNotByPhone.Add(business.Name);
                }

                if (googlePhoneRs != null && googlePhoneRs.Count > 0 &&
                    (googleNameResults == null || googleNameResults.Count == 0))
                {
                    storesByPhoneNotByName.Add(business.Name);
                }
            }
            Console.WriteLine($"************************************************************");
            Console.WriteLine($"******************                     *********************");
            Console.WriteLine($"******************      Statistics     *********************");
            Console.WriteLine($"******************                     *********************");
            Console.WriteLine($"************************************************************");
            Console.WriteLine();
            Console.WriteLine($"Out of {yelpResults.Count} Businesses:");
            Console.WriteLine($"Stores By Phone Not By Name: {storesByPhoneNotByName.Count} | Stores By Name Not By Phone: {storesByNameNotByPhone.Count}");
            Console.Read();

            Environment.Exit(1);
        }

        public static SearchResponse GetBusineesesFromYelp(string category, int maxResults, string location, int offset)
        {
            var request = new SearchRequest
            {
                Categories = category,
                MaxResults = maxResults,
                Location = location,
                ResultsOffset = offset
            };

            string yelp_api_key = ConfigurationManager.AppSettings.Get("yelp_api_key");
            var yelpClient = new Yelp.Api.Client(yelp_api_key);
            return yelpClient.SearchBusinessesAllAsync(request).Result;
        }

        public static List<SmallTypeStoreModel> GetBusineesesFromYelpSmallTypeStore(string category, int maxResults, string location, int offset)
        {
            var request = new SearchRequest
            {
                Categories = category,
                MaxResults = maxResults,
                Location = location,
                ResultsOffset = offset
            };

            string yelp_api_key = ConfigurationManager.AppSettings.Get("yelp_api_key");
            var yelpClient = new Yelp.Api.Client(yelp_api_key);
            var yelpRes = yelpClient.SearchBusinessesAllAsync(request).Result;

            List<SmallTypeStoreModel> resLst = new List<SmallTypeStoreModel>();
            foreach (var bs in yelpRes.Businesses)
            {
                resLst.Add(new SmallTypeStoreModel
                {
                    City = bs.Location.City,
                    Name = bs.Name,
                    State = bs.Location.State,
                    Phone = bs.Phone
                });
            }

            return resLst;
        }

        public static void Exit(string finishRsn)
        {
            _logger.Info(finishRsn);
            _logger.Info("FINISH");
            Environment.Exit(1);
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
	@Email,
    @UpdateDate
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
            _logger.Info($"CreateEmailListFromHunter");
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
            _logger.Info($"FINISH CreateEmailListFromHunter");
            return ret;
        }

        public static bool IsEmailGood(string mail)
        {
            _logger.Info($"Checking email: {mail}  | ");
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
                    Exit("________________________________________________");
                }

                result = o["data"]["result"]?.ToString() ?? "undeliverable";
            }
            if (o.ContainsKey("errors"))
            {
                JObject ss = JObject.Parse(o["errors"][0].ToString());
                if (ss.ContainsKey("code"))
                {
                    string code = ss["code"].ToString();
                    if (code == "222")
                        result = "deliverable";
                    else
                    {
                        _logger.Warn($"hunter error - did not find email {mail}. msg - {o["errors"].ToString()}");
                        result = "undeliverable";
                    }
                }
                else
                {
                    _logger.Error($"hunter error - did not find email {mail}. msg - {o["errors"].ToString()}");
                    result = "undeliverable";
                }
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

        public static void GetSocialFromWebSite(string domain, out string fb, out string instagram,
            out List<string> emailsList, out string linkedin, out string twitter, out string phone)
        {
            _logger.Info($"GetSocialFromWebSite for domain {domain}");
            fb = "";
            instagram = "";
            emailsList = new List<string>();
            linkedin = "";
            twitter = "";
            phone = "";
            string responseBody = "";
            try
            {
                if (domain.Contains("http") || domain.Contains("https") || domain.Contains("www"))
                {
                    _logger.Info("             GetSocialFromWebSite has start");
                    responseBody = GetResponse(domain);
                }
                else
                {
                    _logger.Info("GetSocialFromWebSite has no start");
                    responseBody = GetSiteContent(domain, "http://");
                    if (string.IsNullOrEmpty(responseBody))
                        responseBody = GetSiteContent(domain, "https://");
                    if (string.IsNullOrEmpty(responseBody))
                        responseBody = GetSiteContent(domain, "http://www.");
                    if (string.IsNullOrEmpty(responseBody))
                        responseBody = GetSiteContent(domain, "https://www.");
                }
                if (!string.IsNullOrEmpty(responseBody))
                {
                    GetSocialsFromWebSite(responseBody, ref fb, ref instagram, ref emailsList, ref linkedin, ref twitter, ref phone);
                }

                _logger.Info("GetSocialFromWebSite finished");
            }
            catch (Exception e)
            {
                _logger.Error(e, e.Message);
            }
        }

        private static void GetSocialsFromWebSite(string responseBody, ref string fb, ref string instagram, ref List<string> emailsList, ref string linkedin, ref string twitter, ref string phone)
        {
            string fbregex = @"(?:(?:http|https):\/\/)?(?:www.)?facebook.com\/(?:(?:\w)*#!\/)?(?:pages\/)?(?:[?\w\-]*\/)?(?:profile.php\?id=(?=\d.*))?([\w\-]*)?";
            var lst = GetMatchedList(responseBody, fbregex);
            if (lst != null && lst.Count > 0)
            {
                fb = GetCorrectSocial(lst);
            }

            string instregex = @"(?:(?:http|https):\/\/)?(?:www.)?(?:instagram.com|instagr.am)\/([A-Za-z0-9-_\.]+)";
            //instagram = GetMatched(responseBody, instregex);
            lst = GetMatchedList(responseBody, instregex);
            if (lst != null && lst.Count > 0)
            {
                instagram = GetCorrectSocial(lst);
            }

            string emailregex = @"([a-zA-Z0-9.!#$%&'*+\/=?^_`{|}~-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,})";
            emailsList = GetMatchedList(responseBody, emailregex);

            string linkedinregex = @"(?:(?:http|https):\/\/)?(?:www.)?(?:linkedin.com)(\/([A-Za-z0-9-_\.]+))+";
            //linkedin = GetMatched(responseBody, linkedinregex);
            lst = GetMatchedList(responseBody, linkedinregex);
            if (lst != null && lst.Count > 0)
            {
                linkedin = GetCorrectSocial(lst);
            }

            string twitterregex = @"(?:(?:http|https):\/\/)?(?:www.)?(?:twitter.com)\/([A-Za-z0-9-_\.]+)";
            //twitter = GetMatched(responseBody, twitterregex);
            lst = GetMatchedList(responseBody, twitterregex);
            if (lst != null && lst.Count > 0)
            {
                twitter = GetCorrectSocial(lst);
            }

            string phoneregex = @">([+]{0,1}[0-9]{0,3}[ .-]{0,1}[(]{0,1}[0-9]{3}[)]{0,1}[ .-]{0,1}[0-9]{3}[ .-]{0,1}[0-9]{4})";
            phone = GetMatched(responseBody, phoneregex);
            phone = phone.Replace(">", "").Replace("+", "")?.Replace("-", "")?.Replace("(", "")?.Replace(")", "")?.Replace(" ", "")?.Trim();
        }

        private static string GetResponse(string domain)
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

        private static string GetCorrectSocial(List<string> lst)
        {
            string correct = null;
            foreach (string item in lst)
            {
                using (var client = new HttpClient())
                {
                    var res = client.GetAsync(item).GetAwaiter().GetResult();
                    if (res.IsSuccessStatusCode)
                    {
                        correct = item;
                        break;
                    }
                }
            }

            return correct;
        }

        private static string GetSiteContent(string domain, string val)
        {
            _logger.Info($"GetSiteContent domain: {domain} | val: {val}");
            if (string.IsNullOrEmpty(domain))
                return "";
            if (!domain.Contains(val))
            {
                try
                {
                    string url = val + domain;

                    string responseBody = "";
                    using (var client = new HttpClient())
                    {
                        var res = client.GetAsync(url).GetAwaiter().GetResult();
                        if (!res.IsSuccessStatusCode)
                            return "";
                        using (var sr = new StreamReader(res.Content.ReadAsStreamAsync().GetAwaiter().GetResult()))
                        {
                            responseBody = sr.ReadToEnd();
                        }
                    }


                    return responseBody;
                }
                catch (Exception ex)
                {
                    _logger.Error($"GetSiteContent domain - {domain} | val - {val}");
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
            return new List<string> { "partysupplies" };
            //return new List<string> { "giftshops", "officeequipment", "bookstores" };
        }

        private static List<string> GetLocations()
        {
            return new List<string> {
"Albany, NY",
"Amsterdam, NY",
"Auburn, NY",
"Batavia, NY",
"Beacon, NY",
"Binghamton, NY",
"Buffalo, NY",
"Canandaigua, NY",
"Cohoes, NY",
"Corning, NY",
"Cortland, NY",
"Dunkirk, NY",
"Elmira, NY",
"Fulton, NY",
"Geneva, NY",
"Glen Cove, NY",
"Glens Falls, NY",
"Gloversville, NY",
"Hornell, NY",
"Hudson, NY",
"Ithaca, NY",
"Jamestown, NY",
"Johnstown, NY",
"Kingston, NY",
"Lackawanna, NY",
"Little Falls, NY",
"Lockport, NY",
"Long Beach, NY",
"Mechanicville, NY",
"Middletown, NY",
"Mount Vernon, NY",
"New Rochelle, NY",
"New York, NY",
"Newburgh, NY",
"Niagara Falls, NY",
"North Tonawanda, NY",
"Norwich, NY",
"Ogdensburg, NY",
"Olean, NY",
"Oneida, NY",
"Oneonta, NY",
"Oswego, NY",
"Peekskill, NY",
"Plattsburgh, NY",
"Port Jervis, NY",
"Poughkeepsie, NY",
"Rensselaer, NY",
"Rochester, NY",
"Rome, NY",
"Rye, NY",
"Salamanca, NY",
"Saratoga Springs, NY",
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
                            @Address2,
                            @UpdateDate,
                            @YelpUrl,
                            @InfoQuality
                            )";
                db.ExecuteNonQuery(query, details.Domain, details.Category, details.StoreName, details.City
                    , details.State, details.Email, details.FirstName, details.LastName, details.Phone, details.Facebook,
                    details.Rating, details.Reviewers, details.Instagram, details.Position, details.LinkedIn, details.Seniority, details.Twitter
                    , details.Departmnt, details.RetailsType, details.Address1, details.Address2, details.ZipCode, details.YelpUrl, details.InfoQuality);
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
            catch (Exception ex)
            {
                _logger.Error(ex, ex.Message);
                domain = "";
            }
        }
    }
}
