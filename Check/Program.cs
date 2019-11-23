using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using yelp;

namespace Check
{
    public class Program
    {
        static void Main(string[] args)
        {
            string location = "New York, NY";
            string category = "partysupplies";
            var yelpResults = yelp.Program.GetBusineesesFromYelp(category, 50, location, 0);

            DomainFinder df = new DomainFinder();

            List<string> storesByNameNotByPhone = new List<string>();
            List<string> storesByPhoneNotByName = new List<string>();
            foreach (var business in yelpResults.Businesses)
            {
                string searchTerm = $"{business.Name} {business.Location.City} {business.Location.State}";
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


                Console.WriteLine($"YELP - Store name: {business.Name} | City: {business.Location.City} | State: {business.Location.State}");
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
            Console.WriteLine($"Out of {yelpResults.Businesses.Count} Businesses:");
            Console.WriteLine($"Stores By Phone Not By Name: {storesByPhoneNotByName.Count} | Stores By Name Not By Phone: {storesByNameNotByPhone.Count}");
            Console.Read();
        }
    }
}
