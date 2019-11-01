using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace yelp
{
    public class Details
    {
        //public string yelpUrl { get; set; }
        public string Domain { get; set; }
        public string Category { get; set; }
        public string StoreName { get; set; }
        public string City { get; set; }
        public string State { get; set; }
        public string Email { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Phone { get; set; }
        public string Facebook { get; set; }
        public float Rating { get; set; }
        public float Reviewers { get; set; }
        public string Instagram { get; set; }
        public string Position { get; internal set; }
        public string LinkedIn { get; internal set; }
        public string Seniority { get; internal set; }
        public string Twitter { get; internal set; }
        public string Departmnt { get; internal set; }
    }
}
