using System;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization.Formatters;
using System.Text;
using Newtonsoft.Json;

namespace DynLanTests
{
    public class Osoba
    {
        public String Imie { get; set; }
    }
    /// <summary>
    /// defincja pola w resource dla klienta
    /// </summary>
    //[ProtoContract]
    public class ClientV
    {
        /// <summary>
        /// Value
        /// </summary>
        public Object RealValue { get; set; }

        /////////////////////////////////

        /// <summary>
        /// Value
        /// </summary>
        //[ProtoMember(1)]
        public String TransValue { get; set; }

        /// <summary>
        /// Status
        /// </summary>
        //[ProtoMember(2)]
        public String Status { get; set; }

        /////////////////////////////////

        public Object Value()
        {
            return RealValue;
        }

        /////////////////////////////////

        public Boolean IsSame(ClientV Other)
        {
            if (Other == null ||
                Other.TransValue != this.TransValue ||
                Other.Status != this.Status)
                return false;
            return true;
        }
    }
}



public static class JsonSerializerPrecise
{
    private static String BaseDir
    {
        get
        {
            var lUri = new Uri(Assembly.GetExecutingAssembly().CodeBase);
            return Path.GetDirectoryName(lUri.LocalPath);
        }
    }

    private static JsonSerializerSettings getSettings()
    {
        JsonSerializerSettings customJsonSettings = new JsonSerializerSettings()
        {
            DateFormatHandling = DateFormatHandling.IsoDateFormat,
            DateTimeZoneHandling = DateTimeZoneHandling.Local,
            TypeNameHandling = TypeNameHandling.All,
            TypeNameAssemblyFormat = FormatterAssemblyStyle.Simple
        };
        return customJsonSettings;
    }

    //////////////////////////////////

    public static Boolean IsSerializedJson(String Json)
    {
        try
        {
            object obj = DeserializeJson(Json);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static Boolean IsSerializedJsonString(String Json)
    {
        try
        {
            object obj = DeserializeJson(Json);
            return obj.GetType() == typeof(String);
        }
        catch
        {
            return false;
        }
    }

    public static String SerializeJson(Object Item)
    {
        if (Item == null)
        {
            return null;
        }
        else
        {
            return JsonConvert.SerializeObject(Item, getSettings());
        }
    }

    public static Byte[] SerializeJson2Bytes(Object Item)
    {
        if (Item == null)
        {
            return null;
        }
        else
        {
            return Encoding.UTF8.GetBytes(
                JsonConvert.SerializeObject(Item, getSettings()));
        }
    }

    public static T TryDeserializeJson<T>(String String)
    {
        try
        {
            return DeserializeJson<T>(String);
        }
        catch
        {
            return default(T);
        }
    }

    public static T DeserializeJson<T>(String String)
    {
        if (String.IsNullOrEmpty(String))
        {
            return default(T);
        }
        else
        {
            return (T)JsonConvert.DeserializeObject<T>(String, getSettings());
        }
    }

    public static Object DeserializeJson(String String)
    {
        if (String.IsNullOrEmpty(String))
        {
            return null;
        }
        else
        {
            return JsonConvert.DeserializeObject(String, getSettings());
        }
    }

    public static T DeserializeJsonFromBytes<T>(Byte[] Bytes)
    {
        String String = Bytes == null ? null : Encoding.UTF8.GetString(Bytes, 0, Bytes.Length);
        if (String.IsNullOrEmpty(String))
        {
            return default(T);
        }
        else
        {
            return (T)JsonConvert.DeserializeObject<T>(String, getSettings());
        }
    }

    public static Object DeserializeJsonFromBytes(Byte[] Bytes)
    {
        String String = Bytes == null ? null : Encoding.UTF8.GetString(Bytes, 0, Bytes.Length);
        if (String.IsNullOrEmpty(String))
        {
            return null;
        }
        else
        {
            return JsonConvert.DeserializeObject(String, getSettings());
        }
    }

    //////////////////////////////////

    public static String SerializeJsonToFile(String FilePath, Object Item)
    {
        var json = SerializeJson(Item);

        if (!Path.IsPathRooted(FilePath))
            FilePath = Path.Combine(BaseDir, FilePath);

        if (File.Exists(FilePath)) File.Delete(FilePath);
        File.WriteAllText(FilePath, json ?? "", Encoding.UTF8);
        return json;
    }

    public static T DeserializeJsonFromFile<T>(String FilePath)
    {
        if (!Path.IsPathRooted(FilePath))
            FilePath = Path.Combine(BaseDir, FilePath);

        if (File.Exists(FilePath))
        {
            return DeserializeJson<T>(
                File.ReadAllText(FilePath, Encoding.UTF8));
        }
        else
        {
            return default(T);
        }
    }

    //////////////////////////////////


}
