using System.Text;

namespace SoftwarePioniere.EventStore
{
    public static class UtilExtensions
    {
       
        public static string FromUtf8(this byte[] arr)
        {
            return Encoding.UTF8.GetString(arr);
        }

        public static byte[] ToUtf8(this string str)
        {
            return Encoding.UTF8.GetBytes(str);
        }

      
    }
}