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
        SQLiteConnection _conn;

        public Database()
        {
            _conn = new SQLiteConnection("Data Source=database.sqlite3");
            if (!File.Exists("./database.sqlite3"))
            {
                SQLiteConnection.CreateFile("database.sqlite3");
                Console.WriteLine("DB file created");
            }

        }

        internal void ExecuteNonQuery(string query, string domain, string category, string storeName, string city, 
            string state, string email, string firstName, string lastName, string phone, string facebook, float rating, 
            float reviwers, string instagram, string position,
            string linkedIn, string seniority, string twitter
                    , string departmntn, string retailType)
        {
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
            object rows = cmd.ExecuteScalar();
            CloseConnection();
            if (!int.TryParse(rows.ToString(), out int intRow))
                return 0;
            return intRow;
        }

        public void OpenConnection()
        {
            if(_conn.State != System.Data.ConnectionState.Open)
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
    }
}
