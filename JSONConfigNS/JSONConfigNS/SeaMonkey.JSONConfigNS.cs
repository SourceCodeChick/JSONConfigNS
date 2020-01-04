using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SeaMonkey.Crypt;
using System;
using System.IO;
using System.Data.SqlClient;

namespace SeaMonkey.JSONConfigNS
{
	// The global Configuration object
	public class ConfigData
	{
		// Database specific section of the configuration model
		public DBConnData DB;
		public string EncryptionType { get; set; }

		/*[JsonIgnore]
		public bool IsEncrypted { get; set; }
		*/

		// Generic constructor
		public ConfigData()
		{
			DB = new DBConnData();
			EncryptionType = "";
		}

		public ConfigData(String srvr, String db, String un, String pw, int timeout = 15, string encType = "")
		{
			DB = new DBConnData(srvr, db, un, pw, timeout);
			EncryptionType = encType;
		}

		public ConfigData(String srvr, String db, int timeout = 15, string encType = "")
		{
			DB = new DBConnData(srvr, db, 15);
			EncryptionType = encType;
		}

		public void Clear()
		{
			DB = new DBConnData();
			EncryptionType = "";
		}

		public bool Viable()
		{
			bool IsViable = false;

			IsViable = DB.Server.Length > 0 && DB.Database.Length > 0 &&
				((DB.Username.Length > 0 && DB.Password.Length > 0) ||
				  DB.IntegratedSecurity);

			return IsViable;
		}

		// Constructor that takes an existing DB model
		public ConfigData(DBConnData db)
		{
			DB.Copy(db);
			EncryptionType = "";
		}

		// Copy an existing JSONConfigModel's contents into this one
		public void Copy(ConfigData Source)
		{
			Clear();
			EncryptionType = Source.EncryptionType;
			DB.Copy(Source.DB);
			if (DB.Password != null && DB.Password.Length > 0 && EncryptionType != null && !EncryptionType.Equals("None", StringComparison.CurrentCultureIgnoreCase))
			{
				AES.SetEncryptionType(EncryptionType);
				string EncryptionKeyStr = AES.EncryptionKeys[AES.EncryptionKey].ToString();
				DB.Password = AES.Decrypt(DB.Password, EncryptionKeyStr);
			}
			else
			{
				AES.EncryptionKey = Crypt.EncryptionType.None;
			}
		}

		// Try to load a configuration file with valid JSON content we can understand
		public bool Load(String FileName)
		{
			bool Loaded = false;
			Clear();
			// read JSON directly from a file
			using (StreamReader file = File.OpenText(FileName))
			using (JsonTextReader reader = new JsonTextReader(file))
			{
				try
				{
					// Create JSON object
					JObject JSON = (JObject)JToken.ReadFrom(reader);

					// Convert JSON object to C# object and invoke
					// this object's Copy() method to copy that object
					Copy(JSON.ToObject<ConfigData>());

					// Clean up
					JSON.RemoveAll();
					JSON = null;
					Loaded = true;
				}
				catch
				{
					Clear();
				}
				return Loaded;
			}
		}

		// Serialize the object to JSON and save it as a text file
		public void Save(String FileName)
		{
			// For JSON output to file:
			JsonSerializer serializer = new JsonSerializer();
			serializer.NullValueHandling = NullValueHandling.Ignore;
			serializer.Formatting = Formatting.Indented;

			string OrigPW = DB.Password;

			if (EncryptionType.Length > 0 && !EncryptionType.Equals("None", StringComparison.OrdinalIgnoreCase))
			{
				AES.SetEncryptionType(EncryptionType);
				string EncryptionKeyStr = AES.EncryptionKeys[AES.EncryptionKey].ToString();
				DB.Password = AES.Encrypt(DB.Password, EncryptionKeyStr);
			}

			using (StreamWriter sw = new StreamWriter(FileName))
			using (JsonWriter writer = new JsonTextWriter(sw))
			{
				serializer.Serialize(writer, this);
			}

			DB.Password = OrigPW;
		}

		public override String ToString()
		{
			return JsonConvert.SerializeObject(this, Formatting.Indented);
		}

        public String ToStringEncrypted()
        {
            string OrigPW = DB.Password;
            string Output = "";

            if (EncryptionType.Length > 0 && !EncryptionType.Equals("None", StringComparison.OrdinalIgnoreCase))
            {
                AES.SetEncryptionType(EncryptionType);
                string EncryptionKeyStr = AES.EncryptionKeys[AES.EncryptionKey].ToString();
                DB.Password = AES.Encrypt(DB.Password, EncryptionKeyStr);
            }
            Output = this.ToString();
            DB.Password = OrigPW;
            return Output;
        }

		public String SuggestFilename()
		{
			String FN;  
			FN = DB.Server.Trim() + "." +
				 DB.Database.Trim() + "." +
				 DB.Username.Trim() + ".cfg";
			return FN;
		}
	}

	// The JSONDBConfigModel represents a section or subobject within the larger JSONConfigModel (above)
	// This is done to prevent data members like Server/Username/Password that are specific to the 
	// database connection and subsequent connection string from being confused with other data members
	// of the rest of the configuration that might have similiar or identical names (but are not related to the DB)
	public class DBConnData
	{
		public String Server { get; set; }
		public String Database { get; set; }
		public String Username { get; set; }
		public String Password { get; set; }

		public bool IntegratedSecurity { get; set; }

		public int ConnectTimeout { get; set; }

		public bool PersistSecurityInfo { get; set; }

		[JsonIgnore]
		public string ConnectionString
		{
			get
			{
				SqlConnectionStringBuilder scb = new SqlConnectionStringBuilder();
				scb.DataSource = Server;
				scb.InitialCatalog = Database;
				scb.UserID = Username;
				scb.Password = Password;
				scb.PersistSecurityInfo = PersistSecurityInfo;
				scb.IntegratedSecurity = IntegratedSecurity;
				scb.ConnectTimeout = 15;
				return scb.ToString().Replace("User ID=;", "").Replace("Password=;", "");
			}
		}

		// Constructor - create new object and initialize it
		public DBConnData()
		{
			Server = "";
			Database = "";
			Username = "";
			Password = "";
			ConnectTimeout = 15;
			IntegratedSecurity = false;
			PersistSecurityInfo = false;
		}

		// Constructor - create new object and initialize it
		public DBConnData(String srvr, String db, String un, String pw, int timeout = 15)
		{
			Server = srvr; Database = db; Username = un; Password = pw; ConnectTimeout = timeout;
			IntegratedSecurity = (un == null || un.Length == 0); PersistSecurityInfo = false;
		}

		// Constructor - create new object and initialize it
		public DBConnData(String srvr, String db, int timeout = 15)
		{
			Server = srvr; Database = db; Username = ""; Password = ""; ConnectTimeout = timeout;
			IntegratedSecurity = true; PersistSecurityInfo = false;
		}

		// Copy an existing JSONConfigModel's contents into this one
		public void Copy(DBConnData Source)
		{
			Server = Source.Server;
			Database = Source.Database;
			Username = Source.Username;
			Password = Source.Password;
			ConnectTimeout = Source.ConnectTimeout;
			IntegratedSecurity = Source.IntegratedSecurity;
			PersistSecurityInfo = Source.PersistSecurityInfo;
		}
	}
}
