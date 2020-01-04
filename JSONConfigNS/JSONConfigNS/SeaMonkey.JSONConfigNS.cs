using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Security.Cryptography;
using System.Text;

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
				AES.EncryptionKey = JSONConfigNS.EncryptionType.None;
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


	//--------------------------------------------------------------------------------------------
	// Encryption Code Below
	//--------------------------------------------------------------------------------------------

	public enum EncryptionType 
	{
		None=0, AES_2018=1, AES_2019=2, 
		AES_2020v1=3, AES_2020v2=4, AES_2020v3 = 5
	}

	public class EncryptionInfo
	{
		public EncryptionType Setting;
		public string Name;
		public string Password;

		public EncryptionInfo(EncryptionType encSetting, string name, string pw)
		{
			Setting = encSetting;
			Name = name;
			Password = pw;
		}
	}


	/// <summary>
	/// This class uses a symmetric key algorithm (Rijndael/AES) to encrypt and 
	/// decrypt data. As long as encryption and decryption routines use the same
	/// parameters to generate the keys, the keys are guaranteed to be the same.
	/// The class uses static functions with duplicate code to make it easier to
	/// demonstrate encryption and decryption logic. In a real-life application, 
	/// this may not be the most efficient way of handling encryption, so - as
	/// soon as you feel comfortable with it - you may want to redesign this class.
	/// </summary>
	public static class AES
	{
		private static string saltValue = "s@1t?Of%the#EarthValue";    // can be any string
		private static string hashAlgorithm = "SHA1";                  // can be "MD5"
		private static int passwordIterations = 2;                     // can be any number
		private static string initVector = "@1C2b4E1a3F9h6G8";         // must be 16 bytes
		private static int keySize = 256;                              // can be 192 or 128
		public static EncryptionType EncryptionKey = EncryptionType.None;
		public static Dictionary<EncryptionType, string> EncryptionKeys = new Dictionary<EncryptionType, string>();

		public static void Init(bool force = false)
		{
			// Encryption types is used as a lookup table so that we can put the dictionary keys
			// from the left into configuration files to get they encryption key we want to use
			// instead of embedding that encryption key into config files directly or limiting
			// ourselves to a single encryption key for all uses.

			// These are random strings - replace and/or expand to include your own
			// This allows adding an encryption type to the config file that then applies the
			// associated string as the encryption/decryption key (rather than having a single
			// hard-coded encryption key or having to include the key in the config file
			if (force || EncryptionKeys.Count == 0)
			{
				EncryptionKeys.Clear();
				EncryptionKeys.Add(EncryptionType.None, "");
				EncryptionKeys.Add(EncryptionType.AES_2018, "Sea%Mo?|?18mn+01D#14");
				EncryptionKeys.Add(EncryptionType.AES_2019, "Ravenous7#Bengali25@");
				EncryptionKeys.Add(EncryptionType.AES_2020v1, "3qwell@Awp0rtoon!tY*");
				EncryptionKeys.Add(EncryptionType.AES_2020v2, "6qwell?OppOrtUnitea!");
				EncryptionKeys.Add(EncryptionType.AES_2020v3, "Marv1Mrs2Ma!z3lthe--");
			}
			EncryptionKey = EncryptionType.None;
		}

		public static void Init(EncryptionType encKey)
		{
			Init();
			EncryptionKey = encKey;
		}

		public static void Init(string encKeyStr)
		{
			Init();
			if (!SetEncryptionType(encKeyStr))
			{
				EncryptionKey = EncryptionType.None;
			}
		}

		public static EncryptionType GetEncryptionType(string EncTypeStr)
		{
			Init();
			EncryptionType RetVal = EncryptionType.None;

			foreach (var encKey in EncryptionKeys)
			{
				if (encKey.Key.ToString().Equals(EncTypeStr, StringComparison.CurrentCultureIgnoreCase))
				{
					RetVal = encKey.Key;
					break;
				}
			}

			return RetVal;
		}

		public static bool SetEncryptionType(string EncTypeStr)
		{
			bool Success = false;
			Init();
			foreach (var encKey in EncryptionKeys)
			{
				if (encKey.Key.ToString().Equals(EncTypeStr, StringComparison.CurrentCultureIgnoreCase))
				{
					EncryptionKey = encKey.Key;
					Success = true;
				}
				if (Success) break;
			}

			return Success;
		}

		public static string Decrypt(string EncryptedVal)
		{
			string EncryptionKeyString = AES.EncryptionKeys[AES.EncryptionKey].ToString();
			string Decrypted = _Decrypt(
				EncryptedVal,
				EncryptionKeyString,
				saltValue,
				hashAlgorithm,
				passwordIterations,
				initVector,
				keySize);

			return Decrypted;
		}

	public static string Encrypt(string UnencryptedVal)
		{
			Init();
			string EncryptionKeyString = AES.EncryptionKeys[AES.EncryptionKey].ToString();
			string Encrypted = _Encrypt(
				UnencryptedVal,
				EncryptionKeyString,
				saltValue,
				hashAlgorithm,
				passwordIterations,
				initVector,
				keySize);
			return Encrypted;
		}
		public static string Encrypt(string UnencryptedVal, string Password)
		{
			Init();
			string Encrypted = _Encrypt(
				UnencryptedVal,
				Password,
				saltValue,
				hashAlgorithm,
				passwordIterations,
				initVector,
				keySize);
			return Encrypted;
		}

		public static string Decrypt(string EncryptedVal, string Password)
		{
			string Decrypted = _Decrypt(
				EncryptedVal,
				Password,
				saltValue,
				hashAlgorithm,
				passwordIterations,
				initVector,
				keySize);

			return Decrypted;
		}


		/// <summary>
		/// Encrypts specified plaintext using Rijndael symmetric key algorithm
		/// and returns a base64-encoded result.
		/// </summary>
		/// <param name="plainText">
		/// Plaintext value to be encrypted.
		/// </param>
		/// <param name="passPhrase">
		/// Passphrase from which a pseudo-random password will be derived. The
		/// derived password will be used to generate the encryption key.
		/// Passphrase can be any string. In this example we assume that this
		/// passphrase is an ASCII string.
		/// </param>
		/// <param name="saltValue">
		/// Salt value used along with passphrase to generate password. Salt can
		/// be any string. In this example we assume that salt is an ASCII string.
		/// </param>
		/// <param name="hashAlgorithm">
		/// Hash algorithm used to generate password. Allowed values are: "MD5" and
		/// "SHA1". SHA1 hashes are a bit slower, but more secure than MD5 hashes.
		/// </param>
		/// <param name="passwordIterations">
		/// Number of iterations used to generate password. One or two iterations
		/// should be enough.
		/// </param>
		/// <param name="initVector">
		/// Initialization vector (or IV). This value is required to encrypt the
		/// first block of plaintext data. For RijndaelManaged class IV must be 
		/// exactly 16 ASCII characters long.
		/// </param>
		/// <param name="keySize">
		/// Size of encryption key in bits. Allowed values are: 128, 192, and 256. 
		/// Longer keys are more secure than shorter keys.
		/// </param>
		/// <returns>
		/// Encrypted value formatted as a base64-encoded string.
		/// </returns>
		private static string _Encrypt
		(
			string plainText,
			string passPhrase,
			string saltValue,
			string hashAlgorithm,
			int passwordIterations,
			string initVector,
			int keySize
		)
		{
			// Convert strings into byte arrays.
			// Let us assume that strings only contain ASCII codes.
			// If strings include Unicode characters, use Unicode, UTF7, or UTF8 
			// encoding.
			byte[] initVectorBytes = Encoding.ASCII.GetBytes(initVector);
			byte[] saltValueBytes = Encoding.ASCII.GetBytes(saltValue);

			if (plainText == null || plainText.Length == 0)
			{
				return "";
			}

			// Convert our plaintext into a byte array.
			// Let us assume that plaintext contains UTF8-encoded characters.
			byte[] plainTextBytes = Encoding.UTF8.GetBytes(plainText);

			// First, we must create a password, from which the key will be derived.
			// This password will be generated from the specified passphrase and 
			// salt value. The password will be created using the specified hash 
			// algorithm. Password creation can be done in several iterations.
			PasswordDeriveBytes password = new PasswordDeriveBytes
			(
				passPhrase,
				saltValueBytes,
				hashAlgorithm,
				passwordIterations
			);

			// Use the password to generate pseudo-random bytes for the encryption
			// key. Specify the size of the key in bytes (instead of bits).
			byte[] keyBytes = password.GetBytes(keySize / 8);

			// Create uninitialized Rijndael encryption object.
			RijndaelManaged symmetricKey = new RijndaelManaged()
			{
				// It is reasonable to set encryption mode to Cipher Block Chaining
				// (CBC). Use default options for other symmetric key parameters.
				Mode = CipherMode.CBC
			};


			// Generate encryptor from the existing key bytes and initialization 
			// vector. Key size will be defined based on the number of the key 
			// bytes.
			ICryptoTransform encryptor = symmetricKey.CreateEncryptor
			(
				keyBytes,
				initVectorBytes
			);

			// Define memory stream which will be used to hold encrypted data.
			MemoryStream memoryStream = new MemoryStream();

			// Define cryptographic stream (always use Write mode for encryption).
			CryptoStream cryptoStream = new CryptoStream
			(
				memoryStream,
				encryptor,
				CryptoStreamMode.Write
			);

			// Start encrypting.
			cryptoStream.Write(plainTextBytes, 0, plainTextBytes.Length);

			// Finish encrypting.
			cryptoStream.FlushFinalBlock();

			// Convert our encrypted data from a memory stream into a byte array.
			byte[] cipherTextBytes = memoryStream.ToArray();

			// Close both streams.
			memoryStream.Close();
			cryptoStream.Close();

			// Convert encrypted data into a base64-encoded string.
			string cipherText = Convert.ToBase64String(cipherTextBytes);

			// Return encrypted string.
			return cipherText;
		}

		/// <summary>
		/// Decrypts specified ciphertext using Rijndael symmetric key algorithm.
		/// </summary>
		/// <param name="cipherText">
		/// Base64-formatted ciphertext value.
		/// </param>
		/// <param name="passPhrase">
		/// Passphrase from which a pseudo-random password will be derived. The
		/// derived password will be used to generate the encryption key.
		/// Passphrase can be any string. In this example we assume that this
		/// passphrase is an ASCII string.
		/// </param>
		/// <param name="saltValue">
		/// Salt value used along with passphrase to generate password. Salt can
		/// be any string. In this example we assume that salt is an ASCII string.
		/// </param>
		/// <param name="hashAlgorithm">
		/// Hash algorithm used to generate password. Allowed values are: "MD5" and
		/// "SHA1". SHA1 hashes are a bit slower, but more secure than MD5 hashes.
		/// </param>
		/// <param name="passwordIterations">
		/// Number of iterations used to generate password. One or two iterations
		/// should be enough.
		/// </param>
		/// <param name="initVector">
		/// Initialization vector (or IV). This value is required to encrypt the
		/// first block of plaintext data. For RijndaelManaged class IV must be
		/// exactly 16 ASCII characters long.
		/// </param>
		/// <param name="keySize">
		/// Size of encryption key in bits. Allowed values are: 128, 192, and 256.
		/// Longer keys are more secure than shorter keys.
		/// </param>
		/// <returns>
		/// Decrypted string value.
		/// </returns>
		/// <remarks>
		/// Most of the logic in this function is similar to the Encrypt
		/// logic. In order for decryption to work, all parameters of this function
		/// - except cipherText value - must match the corresponding parameters of
		/// the Encrypt function which was called to generate the
		/// ciphertext.
		/// </remarks>
		private static string _Decrypt
		(
			string cipherText,
			string passPhrase,
			string saltValue,
			string hashAlgorithm,
			int passwordIterations,
			string initVector,
			int keySize
		)
		{
			// Convert strings defining encryption key characteristics into byte
			// arrays. Let us assume that strings only contain ASCII codes.
			// If strings include Unicode characters, use Unicode, UTF7, or UTF8
			// encoding.
			byte[] initVectorBytes = Encoding.ASCII.GetBytes(initVector);
			byte[] saltValueBytes = Encoding.ASCII.GetBytes(saltValue);

			byte[] cipherTextBytes;

			try
			{
				// Convert our ciphertext into a byte array.
				cipherTextBytes = Convert.FromBase64String(cipherText);
			}
			catch
			{
				return cipherText;
			}

			// First, we must create a password, from which the key will be 
			// derived. This password will be generated from the specified 
			// passphrase and salt value. The password will be created using
			// the specified hash algorithm. Password creation can be done in
			// several iterations.
			PasswordDeriveBytes password = new PasswordDeriveBytes
			(
				passPhrase,
				saltValueBytes,
				hashAlgorithm,
				passwordIterations
			);

			// Use the password to generate pseudo-random bytes for the encryption
			// key. Specify the size of the key in bytes (instead of bits).
			byte[] keyBytes = password.GetBytes(keySize / 8);

			// Create uninitialized Rijndael encryption object.
			RijndaelManaged symmetricKey = new RijndaelManaged()
			{
				// It is reasonable to set encryption mode to Cipher Block Chaining
				// (CBC). Use default options for other symmetric key parameters.
				Mode = CipherMode.CBC
			};


			// Generate decryptor from the existing key bytes and initialization 
			// vector. Key size will be defined based on the number of the key 
			// bytes.
			ICryptoTransform decryptor = symmetricKey.CreateDecryptor
			(
				keyBytes,
				initVectorBytes
			);

			// Define memory stream which will be used to hold encrypted data.
			MemoryStream memoryStream = new MemoryStream(cipherTextBytes);

			// Define cryptographic stream (always use Read mode for encryption).
			CryptoStream cryptoStream = new CryptoStream
			(
				memoryStream,
				decryptor,
				CryptoStreamMode.Read
			);

			// Since at this point we don't know what the size of decrypted data
			// will be, allocate the buffer long enough to hold ciphertext;
			// plaintext is never longer than ciphertext.
			byte[] plainTextBytes = new byte[cipherTextBytes.Length];

			int decryptedByteCount = 0;
			string plainText = "";

			// Start decrypting.
			try
			{
				decryptedByteCount = cryptoStream.Read
				(
					plainTextBytes,
					0,
					plainTextBytes.Length
				);

				// Convert decrypted data into a string. 
				// Let us assume that the original plaintext string was UTF8-encoded.
				plainText = Encoding.UTF8.GetString
				(
					plainTextBytes,
					0,
					decryptedByteCount
				);

				// Close both streams.
				memoryStream.Close();
				cryptoStream.Close();
			}
			catch
			{
			}


			// Return decrypted string.   
			return plainText;
		}
	}
}
