using System;
using System.IO;
using System.Reflection;

namespace SoftwarePioniere.EventStore
{
    public class TestFiles
    {      
        public static string GetFileContent(string fileName)
        {
            var assembly = typeof(TestFiles).GetTypeInfo().Assembly;

            var streamName = $"SoftwarePioniere.EventStore.files.{fileName}";

            using (var stream = assembly.GetManifestResourceStream(streamName))
            {
                if (stream == null) throw new InvalidOperationException($"Cannot open stream {streamName}");

                using (var textStreamReader = new StreamReader(stream))
                {
                    return textStreamReader.ReadToEnd();
                }
            }

        }
    }
}
