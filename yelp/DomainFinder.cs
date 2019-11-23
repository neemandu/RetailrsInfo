using GooglePlacesApi;
using GooglePlacesApi.Loggers;
using GooglePlacesApi.Models;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace yelp
{
    public class DomainFinder
    {
        private readonly GoogleApiSettings _settings;
        private readonly GooglePlacesApiService _service;
        private static readonly NLog.Logger _logger = NLog.LogManager.GetCurrentClassLogger();
        private readonly string _google_api_key;
        public DomainFinder()
        {
            _google_api_key = ConfigurationManager.AppSettings.Get("google_api");
            _settings = GoogleApiSettings.Builder
                                            .WithApiKey(_google_api_key)
                                            .WithType(PlaceTypes.Establishment)
                        .WithDetailLevel(DetailLevel.Contact)
                        .Build();
            _service = new GooglePlacesApiService(_settings);
        }

        public List<string> GetPlaceIdsByPhone(string phone)
        {
            try
            {
                Thread.Sleep(200);
                string googlePhoneUrl = $"https://maps.googleapis.com/maps/api/place/findplacefromtext/json?input=%2B{phone}&inputtype=phonenumber&fields=place_id&key={_google_api_key}";

                string responseBody = "";
                using (var client = new HttpClient())
                {
                    var res = client.GetAsync(googlePhoneUrl).GetAwaiter().GetResult();

                    if (res.IsSuccessStatusCode)
                    {
                        using (var sr = new StreamReader(res.Content.ReadAsStreamAsync().GetAwaiter().GetResult()))
                        {
                            responseBody = sr.ReadToEnd();
                        }

                        JObject o = JObject.Parse(responseBody);

                        JToken[] array = o["candidates"].ToArray();

                        return array.Select(item => item["place_id"].ToString()).ToList();
                    }
                    
                }

            }
            catch (Exception ex)
            {
                _logger.Error(ex, "GetPlaceIds: phone: " + phone);
            }
            return new List<string>();
        }

        public List<string> GetPlaceIdsBySearchTerm(string searchTerm)
        {
            try
            {
                Thread.Sleep(200);
                var result = _service.GetPredictionsAsync(searchTerm).ConfigureAwait(false).GetAwaiter().GetResult();
                List<string> apis = new List<string>();
                if (result != null && result.Status.Equals("OK") && result.Items != null && result.Items.Count > 0)
                {
                    List<string> types = new List<string> { "store", "establishment" };
                    foreach (var item in result.Items)
                    {
                        bool other = true;
                        foreach (var type in item.Types)
                        {
                            if (types.Contains(type))
                            {
                                other = false;
                            }
                        }
                        if (!other)
                        {
                            apis.Add(item.PlaceId);
                        }
                    }
                    return apis;
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "GetPlaceIds: searchTerm: " + searchTerm);
            }
            return new List<string>();
        }

        public List<DomainCompany> GetAllWebSites(string businessName, string phone, string searchTerm)
        {
            Dictionary<string, string> domainsDict = new Dictionary<string, string>();
            try
            {
                //           searchTerm = "Alicia's Jewelers Bayside NY";


                List<string> placeIds = GetPlaceIdsBySearchTerm(searchTerm);
                List<string> domains = new List<string>();
                _logger.Info($"             Found {placeIds.Count} placesIds from google maps api for {searchTerm}");
                foreach (var placeId in placeIds)
                {
                    Thread.Sleep(200);
                    GoogleStoreModel store = GetGoogleStoreByPlaceId(placeId);
                    bool isGoodCall = true;
                    if(store == null)
                        isGoodCall = false;

                    string name = store.Name;
                    if (!isGoodCall)
                        continue;


                    
                    bool isStore = false;
                    if(store.Types.Contains("establishment") ||
                        store.Types.Contains("store"))
                    {
                        isStore = true;
                    }

                    if (isStore)
                    {
                        var googlePhone = store.Phone;
                        if (!string.IsNullOrEmpty(googlePhone) && !string.IsNullOrEmpty(phone))
                        {
                            googlePhone = googlePhone.Replace("+", "").Replace("-", "").Replace("(", "").Replace(")", "").Replace(" ", "").Trim();
                            phone = phone.Replace("+", "").Replace("-", "").Replace("(", "").Replace(")", "").Replace(" ", "").Trim();

                            if (googlePhone != phone)
                                continue;
                        }
                        
                        var website = store.Website;
                        if (!string.IsNullOrEmpty(website?.ToString()))
                        {
                            try
                            {
                                using (var client = new HttpClient())
                                {
                                    var res = client.GetAsync(website.ToString()).GetAwaiter().GetResult();
                                    if (res.IsSuccessStatusCode)
                                    {
                                        string dom = GetDomainFromUrl(website.ToString());
                                        string company = store.Name.ToString();
                                        if (!domainsDict.ContainsKey(dom))
                                        {
                                            domainsDict.Add(dom, company);
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.Error(ex, "");
                            }
                        }
                    }
                }
                List<DomainCompany> ret = new List<DomainCompany>();
                foreach (var p in domainsDict)
                {
                    ret.Add(new DomainCompany
                    {
                        Domain = p.Key,
                        Company = p.Value
                    });
                }
                return ret;
            }
            catch (Exception x)
            {
                _logger.Error(x, "");
                return new List<DomainCompany>();
            }
        }

        public GoogleStoreModel GetGoogleStoreByPlaceId(string placeId)
        {
            GoogleStoreModel ret = new GoogleStoreModel();
            string url = $"https://maps.googleapis.com/maps/api/place/details/json?place_id={placeId}&key=AIzaSyD6Fi3_OAslSsgOQzdJxmQS0TrP2hpdtBw";
            string responseBody = "";


            using (var httpClient = new HttpClient())
            {
                var res = httpClient.GetAsync(url).GetAwaiter().GetResult();
                if (!res.IsSuccessStatusCode)
                {
                    _logger.Error("             url: " + url);
                    _logger.Error($"             Bad response from google api : {res.StatusCode} | {res.Content}");
                    return null;
                }
                else
                {
                    using (var sr = new StreamReader(res.Content.ReadAsStreamAsync().GetAwaiter().GetResult()))
                    {
                        responseBody = sr.ReadToEnd();
                    }
                }
            }
            JObject o = JObject.Parse(responseBody);
            ret.Name = o["result"]["name"].ToString();
            ret.Types = new List<string>();
            for (var i = 0; i < o["result"]["types"].Count(); i++)
            {
                ret.Types.Add(o["result"]["types"][i].ToString());
            }

            ret.Phone = o["result"]["international_phone_number"]?.ToString() ?? "";

            ret.Website = o["result"]["website"]?.ToString() ?? "";

            return ret;
        }

        private string GetDomainFromUrl(string url)
        {
            string regex = @"(?:\/\/|[^\/]+)*";

            Match m = Regex.Match(url, regex, RegexOptions.IgnoreCase);
            if (m.Success && ((m.Groups?.Count ?? 0) > 0))
            {
                return m.Groups[0].Value.Replace("http://", "").Replace("https://", "").Replace("www.", "").Replace("www2.", "");
            }
            return null;
        }
    }
}
