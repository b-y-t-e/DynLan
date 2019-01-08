using System;
using System.Net;
using System.Runtime.Serialization;
using System.IO;
using System.Text;
using System.Collections.Generic;

namespace DynLan.Helpers
{
    public static class SerializationHelper
    {
        private static Dictionary<Type, DataContractSerializer> _serializers = new Dictionary<Type, DataContractSerializer>();

        ////////////////////////////////////////////////////////////////////////////////////////

#if !PCL
        public static T FromFile<T>(String FilePath)
        {
            var bytes = File.ReadAllBytes(FilePath);
            return Deserialize<T>(bytes);
        }

        public static void ToFile(Object Item, String FilePath)
        {
            var bytes = SerializeToBytes(Item);

            if (File.Exists(FilePath))
                File.Delete(FilePath);

            var directory = Path.GetDirectoryName(FilePath);

            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllBytes(
                FilePath,
                bytes ?? new byte[0]);
        }
#endif

        ////////////////////////////////////////////////////////////////////////////////////////

        public static Byte[] SerializeToBytes(this Object Item)
        {
            if (Item == null) return null;
            else
            {
                var lType = Item.GetType();

                if (!_serializers.ContainsKey(lType))
                    lock (_serializers)
                        if (!_serializers.ContainsKey(lType))
                            _serializers[lType] = new DataContractSerializer(lType);

                DataContractSerializer lSerializer = null;
                lock (_serializers)
                    lSerializer = _serializers[lType];

                using (var lMemory = new System.IO.MemoryStream())
                {
                    lSerializer.WriteObject(lMemory, Item);
                    var lBytes = lMemory.ToArray();
                    return lBytes;
                }
            }
        }

        public static String Serialize(this Object Item)
        {
            var lBytes = SerializeToBytes(Item);
            if (lBytes == null) return null;
            else
            {
                return Encoding.UTF8.GetString(lBytes, 0, lBytes.Length);
            }
        }

        public static T Deserialize<T>(this String String, Boolean ThrowError = true)
        {
            if (String.IsNullOrEmpty(String)) return default(T);
            else
            {
                try
                {
                    var lType = typeof(T);

                    if (!_serializers.ContainsKey(lType))
                        lock (_serializers)
                            if (!_serializers.ContainsKey(lType))
                                _serializers[lType] = new DataContractSerializer(lType);

                    DataContractSerializer lSerializer = null;
                    lock (_serializers)
                        lSerializer = _serializers[lType];

                    using (var lMemory = new System.IO.MemoryStream(Encoding.UTF8.GetBytes(String)))
                    {
                        return (T)lSerializer.ReadObject(lMemory);
                    }
                }
                catch (Exception ex)
                {
                    if (ThrowError) throw;
                    return default(T);
                }
            }
        }

        public static T Deserialize<T>(this Byte[] Bytes, Boolean ThrowError = true)
        {
            if (Bytes == null || Bytes.Length == 0)
                return default(T);
            else
            {
                try
                {
                    var lType = typeof(T);

                    if (!_serializers.ContainsKey(lType))
                        lock (_serializers)
                            if (!_serializers.ContainsKey(lType))
                                _serializers[lType] = new DataContractSerializer(lType);

                    DataContractSerializer lSerializer = null;
                    lock (_serializers)
                        lSerializer = _serializers[lType];

                    using (var lMemory = new System.IO.MemoryStream(Bytes))
                    {
                        return (T)lSerializer.ReadObject(lMemory);
                    }
                }
                catch (Exception ex)
                {
                    if (ThrowError) throw;
                    return default(T);
                }
            }
        }
    }
}