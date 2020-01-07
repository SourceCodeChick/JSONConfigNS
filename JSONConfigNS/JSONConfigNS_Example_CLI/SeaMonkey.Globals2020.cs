using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using SeaMonkey.JSONConfigNS;	// NUGET Newtonsoft.JSON

namespace SeaMonkey.Globals
{
	public static class Global
	{
		public static string CONFIG_FILE = "db.cfg";		//	Update as desired and ensure this config file is here
		public static DBController DB = new DBController();
		public static ConfigData Config { get; set; }
		public static Stack<String> Debug = new Stack<string>();
		public static Stack<String> Errors = new Stack<string>();
		public static bool Verbose { get; set; }
		public static bool ShowErrors { get; set; }

		// Key function - initialize the object and make sure the configuration loads and can establish a database connection
		public static bool Init(bool _Verbose = true, bool _ShowErrors = true)
		{
			bool Loaded = false;
			bool Success = false;
			Debug = new Stack<string>();
			Errors = new Stack<string>();
			Verbose = _Verbose;
			ShowErrors = _ShowErrors;
			if (!System.IO.File.Exists(CONFIG_FILE))
			{
				Error(string.Format("Configuration file not found ({0}).", CONFIG_FILE));
				return Success;
			}
			Config = new ConfigData();
			Loaded = Config.Load(CONFIG_FILE);
			if (Loaded)
			{
				if (Config.Viable())
				{
					DB.Server = Config.DB.Server;
					DB.Database = Config.DB.Database;
					DB.ConnStr = Config.DB.ConnectionString;
					DB.Conn = new SqlConnection(DB.ConnStr);
					try
					{
						DB.Conn.Open();
						DB.Conn.Close();
						Success = true;
						if (Verbose)
							Console.WriteLine("Connection established (Server:  {0}; Database:  {1})", DB.Server, DB.Database);
					}
					catch (Exception ex)
					{
						Error(string.Format("Connection FAILED (Server:  {0}; Database:  {1}\n{2})", DB.Server, DB.Database, ex.Message));
					}
				}
				else
				{
					Error(string.Format("Configuration file not viable (may be from an older configuration file generator - consider recreating):  {0}", CONFIG_FILE));
				}
			}
			else
			{
				Error(string.Format("Failed to load configuration file:  {0}", CONFIG_FILE));
			}
			return Success;
		}

		// ----------------------------------------------------------------------------------------
		// Error handling functions
		// ----------------------------------------------------------------------------------------

		public static bool HasErrors()
		{
			return (Errors.Count > 0);
		}

		public static String LastDebug()
		{
			String Last = "";
			if (Debug.Count > 0)
			{
				Last = Debug.Peek();
			}
			return Last;
		}

		public static String LastError()
		{
			String Last = "";
			if (Errors.Count > 0)
			{
				Last = Errors.Peek();
			}
			return Last;
		}

		private static void Error(string Msg)
		{
			if (ShowErrors)
			{
				Console.WriteLine(Msg);
			}
			Errors.Push(Msg);
		}
	}

	public class DBController
	{
		public string Server { get; set; }
		public string Database { get; set; }
		public string ConnStr { get; set; }
		public SqlConnection Conn { get; set; }
	}
}