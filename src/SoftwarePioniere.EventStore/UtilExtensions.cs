using System.Text;

namespace SoftwarePioniere.EventStore
{
    public static class UtilExtensions
    {
       
        public static string GetToStringFromEncoded(this byte[] arr)
        {
            return Encoding.UTF8.GetString(arr);
        }

        public static byte[] GetByteArrayFromStringEncoded(this string str)
        {
            return Encoding.UTF8.GetBytes(str);
        }

      
    }
}