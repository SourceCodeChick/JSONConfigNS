using System;
using SeaMonkey.Globals;
using System.Data.SqlClient;

namespace JSONConfigNS_Example_CLI
{
    class Program
    {
        static void Main(string[] args)
        {
            if (!Global.Init())
            {
                // No database connection - abort
                return;
            }

            Global.DB.Conn.Open();
            string Query =
                "SELECT [Directory] FROM [" + Global.DB.Database + "].[dbo].[Source] WHERE [Enabled]=1 ORDER BY [Directory]";
            SqlCommand Cmd = new SqlCommand(Query, Global.DB.Conn);
            SqlDataReader dr = Cmd.ExecuteReader();
            if (dr != null && dr.HasRows)
            {
                while (dr.Read())
                {
                    Console.WriteLine(dr.GetString(0).Trim());
                }
            }
            dr.Close();
            Global.DB.Conn.Close();
            Console.ReadLine();
        }
    }
}
