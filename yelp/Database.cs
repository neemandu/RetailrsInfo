using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.SQLite;
using System.IO;
using System.Data;

namespace yelp
{

    public class Database
    {
        private static readonly NLog.Logger _logger = NLog.LogManager.GetCurrentClassLogger();
        SQLiteConnection _conn;

        public Database()
        {
            _conn = new SQLiteConnection("Data Source=database.sqlite3");
            if (!File.Exists("./database.sqlite3"))
            {
                SQLiteConnection.CreateFile("database.sqlite3");
                _logger.Info("DB file created");
            }

        }

        public List<EmailDetails> GetEmails(string query, string realdomain, string company, Database db)
        {
            SQLiteCommand cmd = new SQLiteCommand(query, _conn);
            OpenConnection();
            cmd.CommandType = CommandType.Text;
            cmd.Parameters.Add(new SQLiteParameter("@Company", company));
            cmd.Parameters.Add(new SQLiteParameter("@Domain", realdomain));
            List<EmailDetails> ret = new List<EmailDetails>();
            using (SQLiteDataReader reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    ret.Add(new EmailDetails
                    {
                        Company = (reader["Company"] is DBNull) ? null : (string)reader["Company"],
                        Departmnt = (reader["Departmnt"] is DBNull) ? null : (string)reader["Departmnt"],
                        Domain = (reader["Domain"] is DBNull) ? null : (string)reader["Domain"],
                        Email = (reader["Email"] is DBNull) ? null : (string)reader["Email"],
                        FirstName = (reader["FirstName"] is DBNull) ? null : (string)reader["FirstName"],
                        LastName = (reader["LastName"] is DBNull) ? null : (string)reader["LastName"],
                        LinkedIn = (reader["LinkedIn"] is DBNull) ? null : (string)reader["LinkedIn"],
                        Phone = (reader["Phone"] is DBNull) ? null : (string)reader["Phone"],
                        Position = (reader["Position"] is DBNull) ? null : (string)reader["Position"],
                        Seniority = (reader["Seniority"] is DBNull) ? null : (string)reader["Seniority"],
                        Twitter = (reader["Twitter"] is DBNull) ? null : (string)reader["Twitter"]
                    });
                }
            }

            CloseConnection();
            return ret;
        }

        public List<string> GetDomainssWithoutEmails()
        {
            string query = @"select distinct Domain
                            From Stores
                            where (Email is null or Rtrim(LTrim(EMail)) = '')
                            order by domain";
            SQLiteCommand cmd = new SQLiteCommand(query, _conn);
            OpenConnection();
            cmd.CommandType = CommandType.Text;
            List<string> domains = new List<string>();
            using (SQLiteDataReader reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    string domain = (string)reader["Domain"];
                    domains.Add(domain);
                }
            }

            CloseConnection();
            return domains;
        }

        internal bool IsStoreExists(string name, string city, string state)
        {
            string query = "select count(*) from Stores where StoreName = @name and City = @city and State = @state";
            SQLiteCommand cmd = new SQLiteCommand(query, _conn);
            OpenConnection();
            cmd.CommandType = CommandType.Text;
            cmd.Parameters.Add(new SQLiteParameter("@city", city));
            cmd.Parameters.Add(new SQLiteParameter("@name", name));
            cmd.Parameters.Add(new SQLiteParameter("@state", state));
             object rows = cmd.ExecuteScalar();
            CloseConnection();
            if (!int.TryParse(rows.ToString(), out int intRow))
                return false;
            return intRow > 0;
        }

        public void UpdateSocial(string domain, string fb, string instagram, string email, string linkedin, string twitter)
        {
            if (!string.IsNullOrEmpty(fb))
            {
                string query = @"Update Stores
                            set Facebook = @Facebook
                            where Domain = @Domain and (Facebook is null or Rtrim(LTrim(Facebook)) = '')";
                SQLiteCommand cmd = new SQLiteCommand(query, _conn);
                OpenConnection();
                cmd.CommandType = CommandType.Text;
                cmd.Parameters.Add(new SQLiteParameter("@Domain", domain));
                cmd.Parameters.Add(new SQLiteParameter("@Facebook", fb));
                cmd.Parameters.Add(new SQLiteParameter("@UpdateDate", DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss.fff")));
                cmd.ExecuteNonQuery();
                CloseConnection();
            }

            //insta
            if (!string.IsNullOrEmpty(instagram))
            {
                var query = @"Update Stores
                            set Instagram = @Instagram
                            where Domain = @Domain and (Instagram is null or Rtrim(LTrim(Instagram)) = '')";
                var cmd = new SQLiteCommand(query, _conn);
                OpenConnection();
                cmd.CommandType = CommandType.Text;
                cmd.Parameters.Add(new SQLiteParameter("@Domain", domain));
                cmd.Parameters.Add(new SQLiteParameter("@Instagram", instagram));
                cmd.ExecuteNonQuery();
                CloseConnection();
            }
            //linkedin
            if (!string.IsNullOrEmpty(linkedin))
            {
                var query = @"Update Stores
                            set LinkedIn = @LinkedIn
                            where Domain = @Domain and (LinkedIn is null or Rtrim(LTrim(LinkedIn)) = '')";
                var cmd = new SQLiteCommand(query, _conn);
                OpenConnection();
                cmd.CommandType = CommandType.Text;
                cmd.Parameters.Add(new SQLiteParameter("@Domain", domain));
                cmd.Parameters.Add(new SQLiteParameter("@LinkedIn", linkedin));
                cmd.ExecuteNonQuery();
                CloseConnection();
            }

            //twitter
            if (!string.IsNullOrEmpty(twitter))
            {
                var query = @"Update Stores
                            set Twitter = @Twitter
                            where Domain = @Domain and (Twitter is null or Rtrim(LTrim(Twitter)) = '')";
                var cmd = new SQLiteCommand(query, _conn);
                OpenConnection();
                cmd.CommandType = CommandType.Text;
                cmd.Parameters.Add(new SQLiteParameter("@Domain", domain));
                cmd.Parameters.Add(new SQLiteParameter("@Twitter", twitter));
                cmd.ExecuteNonQuery();
                CloseConnection();
            }

            //email
            if (!string.IsNullOrEmpty(email))
            {
                string query = @"Update Stores
                            set Email = @Email
                            where Domain = @Domain and (Email is null or Rtrim(LTrim(Email)) = '')";
                var cmd = new SQLiteCommand(query, _conn);
                OpenConnection();
                cmd.CommandType = CommandType.Text;
                cmd.Parameters.Add(new SQLiteParameter("@Domain", domain));
                cmd.Parameters.Add(new SQLiteParameter("@Email", email));
                cmd.ExecuteNonQuery();
                CloseConnection();
            }
        }

        internal void ExecuteNonQuery(string query, string domain, string category, string storeName, string city,
            string state, string email, string firstName, string lastName, string phone, string facebook, float rating,
            float reviwers, string instagram, string position,
            string linkedIn, string seniority, string twitter
                    , string departmntn, string retailType, string address1, string address2, string zipCode, string yelpUrl, string infoQuality)
        {
            _logger.Info($"Writing to DB, Domain - {domain}");
            SQLiteCommand cmd = new SQLiteCommand(query, _conn);
            OpenConnection();
            cmd.CommandType = CommandType.Text;
            cmd.Parameters.Add(new SQLiteParameter("@Domain", domain));
            cmd.Parameters.Add(new SQLiteParameter("@Category", category));
            cmd.Parameters.Add(new SQLiteParameter("@StoreName", storeName));
            cmd.Parameters.Add(new SQLiteParameter("@City", city));
            cmd.Parameters.Add(new SQLiteParameter("@State", state));
            cmd.Parameters.Add(new SQLiteParameter("@Email", email));
            cmd.Parameters.Add(new SQLiteParameter("@FirstName", firstName));
            cmd.Parameters.Add(new SQLiteParameter("@LastName", lastName));
            cmd.Parameters.Add(new SQLiteParameter("@Phone", phone));
            cmd.Parameters.Add(new SQLiteParameter("@Facebook", facebook));
            cmd.Parameters.Add(new SQLiteParameter("@Rating", rating));
            cmd.Parameters.Add(new SQLiteParameter("@Reviewers", reviwers));
            cmd.Parameters.Add(new SQLiteParameter("@Instagram", instagram));
            cmd.Parameters.Add(new SQLiteParameter("@Position", position));
            cmd.Parameters.Add(new SQLiteParameter("@LinkedIn", linkedIn));
            cmd.Parameters.Add(new SQLiteParameter("@Seniority", seniority));
            cmd.Parameters.Add(new SQLiteParameter("@Twitter", twitter));
            cmd.Parameters.Add(new SQLiteParameter("@Departmnt", departmntn));
            cmd.Parameters.Add(new SQLiteParameter("@RetailsType", retailType));
            cmd.Parameters.Add(new SQLiteParameter("@Address1", address1));
            cmd.Parameters.Add(new SQLiteParameter("@Address2", address2));
            cmd.Parameters.Add(new SQLiteParameter("@ZipCode", zipCode));
            cmd.Parameters.Add(new SQLiteParameter("@YelpUrl", yelpUrl));
            cmd.Parameters.Add(new SQLiteParameter("@InfoQuality", infoQuality));
            cmd.Parameters.Add(new SQLiteParameter("@UpdateDate", DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss.fff")));
            cmd.ExecuteNonQuery();
            CloseConnection();
        }

        internal int ExecuteQuery(string query, string city, string name, string category)
        {
            SQLiteCommand cmd = new SQLiteCommand(query, _conn);
            OpenConnection();
            cmd.CommandType = CommandType.Text;
            cmd.Parameters.Add(new SQLiteParameter("@city", city));
            cmd.Parameters.Add(new SQLiteParameter("@name", name));
            cmd.Parameters.Add(new SQLiteParameter("@category", category));
            cmd.Parameters.Add(new SQLiteParameter("@UpdateDate", DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss.fff")));
            object rows = cmd.ExecuteScalar();
            CloseConnection();
            if (!int.TryParse(rows.ToString(), out int intRow))
                return 0;
            return intRow;
        }

        public void OpenConnection()
        {
            if (_conn.State != System.Data.ConnectionState.Open)
            {
                _conn.Open();
            }
        }

        public void CloseConnection()
        {
            if (_conn.State != System.Data.ConnectionState.Closed)
            {
                _conn.Close();
            }
        }

        internal void DeleteEmailsInDb(string query, string domain, Database db)
        {
            SQLiteCommand cmd = new SQLiteCommand(query, _conn);
            OpenConnection();
            cmd.CommandType = CommandType.Text;
            cmd.Parameters.Add(new SQLiteParameter("@Domain", domain));
            cmd.ExecuteNonQuery();
            CloseConnection();
        }

        internal void AddEmailToDb(string query, EmailDetails item, Database db)
        {
            SQLiteCommand cmd = new SQLiteCommand(query, _conn);
            OpenConnection();
            cmd.CommandType = CommandType.Text;
            cmd.Parameters.Add(new SQLiteParameter("@Domain", item.Domain));
            cmd.Parameters.Add(new SQLiteParameter("@Company", item.Company));
            cmd.Parameters.Add(new SQLiteParameter("@Departmnt", item.Departmnt));
            cmd.Parameters.Add(new SQLiteParameter("@Email", item.Email));
            cmd.Parameters.Add(new SQLiteParameter("@FirstName", item.FirstName));
            cmd.Parameters.Add(new SQLiteParameter("@LastName", item.LastName));
            cmd.Parameters.Add(new SQLiteParameter("@LinkedIn", item.LinkedIn));
            cmd.Parameters.Add(new SQLiteParameter("@Phone", item.Phone));
            cmd.Parameters.Add(new SQLiteParameter("@Position", item.Position));
            cmd.Parameters.Add(new SQLiteParameter("@Seniority", item.Seniority));
            cmd.Parameters.Add(new SQLiteParameter("@Twitter", item.Twitter));
            cmd.Parameters.Add(new SQLiteParameter("@UpdateDate", DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss.fff")));
            cmd.ExecuteNonQuery();
            CloseConnection();
        }

        internal void DeleteRecordsInDb(string query,string domain, Database db)
        {
            SQLiteCommand cmd = new SQLiteCommand(query, _conn);
            OpenConnection();
            cmd.CommandType = CommandType.Text;
            cmd.Parameters.Add(new SQLiteParameter("@Domain", domain));
            cmd.ExecuteNonQuery();
            CloseConnection();
        }
    }
}
