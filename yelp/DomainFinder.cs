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
        public DomainFinder()
        {
            string api_key = ConfigurationManager.AppSettings.Get("google_api");
            _settings = GoogleApiSettings.Builder
                                            .WithApiKey(api_key)
                                            .WithType(PlaceTypes.Establishment)
                        .WithDetailLevel(DetailLevel.Contact)
                        .Build();
            _service = new GooglePlacesApiService(_settings);
        }

        private List<string> GetPlaceIds(string searchTerm)
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

        public List<DomainCompany> GetAllWebSites(string searchTerm)
        {
            Dictionary<string, string> domainsDict = new Dictionary<string, string>();
            try
            {
                //           searchTerm = "Alicia's Jewelers Bayside NY";


                List<string> placeIds = GetPlaceIds(searchTerm);
                List<string> domains = new List<string>();
                _logger.Info($"             Found {placeIds.Count} placesIds fromgoogle maps api for {searchTerm}");
                foreach (var placeId in placeIds)
                {
                    Thread.Sleep(200);
                    string url = $"https://maps.googleapis.com/maps/api/place/details/json?place_id={placeId}&key=AIzaSyD6Fi3_OAslSsgOQzdJxmQS0TrP2hpdtBw";
                    string responseBody = "";

                    bool isGoodCall = true;
                    using (var httpClient = new HttpClient())
                    {
                        var res = httpClient.GetAsync(url).GetAwaiter().GetResult();
                        if (!res.IsSuccessStatusCode)
                        {
                            _logger.Error("             url: " + url);
                            _logger.Error($"             Bad response from google api : {res.StatusCode} | {res.Content}");
                            isGoodCall = false;
                        }
                        else
                        {
                            using (var sr = new StreamReader(res.Content.ReadAsStreamAsync().GetAwaiter().GetResult()))
                            {
                                responseBody = sr.ReadToEnd();
                            }
                        }
                    }
                    if (!isGoodCall)
                        continue;

                    JObject o = JObject.Parse(responseBody);
                    bool isStore = false;
                    for (var i = 0; i < o["result"]["types"].Count() && !isStore; i++)
                    {
                        if (o["result"]["types"][i].ToString() == "store" ||
                            o["result"]["types"][i].ToString() == "establishment")
                            isStore = true;
                    }
                    if (isStore)
                    {
                        var e = o["result"]["website"];
                        if (!string.IsNullOrEmpty(e?.ToString()))
                        {
                            try
                            {
                                using (var client = new HttpClient())
                                {
                                    var res = client.GetAsync(e.ToString()).GetAwaiter().GetResult();
                                    if (res.IsSuccessStatusCode)
                                    {
                                        string dom = GetDomainFromUrl(e.ToString());
                                        string company = o["result"]["name"].ToString();
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
