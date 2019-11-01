using GooglePlacesApi;
using GooglePlacesApi.Loggers;
using GooglePlacesApi.Models;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace yelp
{
    public class DomainFinder
    {
        private readonly GoogleApiSettings _settings;
        private readonly GooglePlacesApiService _service;

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
            var result = _service.GetPredictionsAsync(searchTerm).ConfigureAwait(false).GetAwaiter().GetResult();
            List<string> apis = new List<string>();
            if(result != null && result.Status.Equals("OK") && result.Items != null && result.Items.Count > 0)
            {
                List<string> types = new List<string> { "store", "establishment" };
                foreach(var item in result.Items)
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
            return new List<string>();
        }

        internal List<string> GetAllWebSites(string searchTerm, HttpClient httpClient)
        {
            try
            {
                //           searchTerm = "Alicia's Jewelers Bayside NY";
                List<string> placeIds = GetPlaceIds(searchTerm);
                List<string> domains = new List<string>();
                foreach(var placeId in placeIds)
                {
                    string url = $"https://maps.googleapis.com/maps/api/place/details/json?place_id={placeId}&key=AIzaSyD6Fi3_OAslSsgOQzdJxmQS0TrP2hpdtBw";
                    string responseBody = httpClient.GetStringAsync(url).Result;
                    JObject o = JObject.Parse(responseBody);
                    bool isStore = false;
                    for(var i = 0; i < o["result"]["types"].Count() && !isStore; i++)
                    {
                        if (o["result"]["types"][i].ToString() == "store")
                            isStore = true;
                    }
                    if(isStore)
                    {
                        var e = o["result"]["website"];
                        if (!string.IsNullOrEmpty(e?.ToString()))
                        {
                            try
                            {
                                responseBody = httpClient.GetStringAsync(e?.ToString()).Result;
                                if (!string.IsNullOrEmpty(responseBody))
                                {
                                    domains.Add(e.ToString());
                                }
                            }
                            catch(Exception ex) { }
                        }
                    }
                }
                
                return domains;
            }
            catch (Exception x) { return new List<string>(); }
        }
    }
}
