using System;
using System.Data.SqlClient;
using System.Net.NetworkInformation;

namespace PingWatcher
{
    class DbConnection
    {
        public static string sqlConnectionString = @"Data Source=localhost\SQLEXPRESS;Initial Catalog=PingWatcher;User id=sa;Password=Tidus339";
        
        public static void WriteToDb(PingReply target, PingReply gateway)
        {
            using(SqlConnection con = new SqlConnection(sqlConnectionString))
            {
                var time = DateTime.Now.ToString("G");
                string insertTarget = "INSERT INTO Outside VALUES('" + time + "', " + target.RoundtripTime + ", '" + target.Status.ToString() + "', '" + target.Address.ToString() + "');";
                string insertGateway= "INSERT INTO Gateway VALUES('" + time + "', " + gateway.RoundtripTime + ", '" + gateway.Status.ToString() + "', '" + gateway.Address.ToString() + "');";

                con.Open();
                using(SqlCommand cmd = new SqlCommand(insertTarget, con))
                {
                    cmd.ExecuteNonQuery();
                }
                using(SqlCommand cmd = new SqlCommand(insertGateway, con))
                {
                    cmd.ExecuteNonQuery();
                }
                con.Close();
            }
        }

        public static Commons.PingInformation PingInformation(string targetAddress, string gatewayAddress)
        {
            Commons.PingInformation information = new Commons.PingInformation();

            string targetQuery = @"SELECT AVG(RoundTripTime), SUM(CASE WHEN PingStatus = 'TimedOut' THEN 1 ELSE 0 END) FROM Outside WHERE IP = '" + targetAddress + "';";
            string gatewayQuery = @"SELECT AVG(RoundTripTime), SUM(CASE WHEN PingStatus = 'TimedOut' THEN 1 ELSE 0 END) FROM Gateway WHERE IP = '" + gatewayAddress + "';";

            using(SqlConnection con=new SqlConnection(sqlConnectionString))
            {
                con.Open();
                using (SqlCommand cmd = new SqlCommand(targetQuery, con))
                    using(SqlDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        information.TargetPing = Convert.ToInt32(reader[0]);
                        information.TargetTimeouts = Convert.ToInt32(reader[1]);
                    }
                }
                using (SqlCommand cmd = new SqlCommand(gatewayQuery, con))
                    using(SqlDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        information.GatewayPing = Convert.ToInt32(reader[0]);
                        information.GatewayTimeouts = Convert.ToInt32(reader[1]);
                    }
                }
                con.Close();
            }
            return information;
        }

    }
}
