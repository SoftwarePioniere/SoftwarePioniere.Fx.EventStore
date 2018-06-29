using System;
using System.IO;
using Microsoft.Extensions.Configuration;

namespace SoftwarePioniere.EventStore.Tests
{
    public class Configurator
    {
        static Configurator()
        {
            Console.WriteLine($"AppContext.BaseDirectory: {AppContext.BaseDirectory}");
            var basePath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..");
            Console.WriteLine($"Base Path: {basePath}");
            var fullPath = Path.GetFullPath(basePath);
            Console.WriteLine($"FullBasePath: {fullPath}");

            var builder = new ConfigurationBuilder()
                //.AddEnvironmentVariables()
                .SetBasePath(fullPath)
                .AddJsonFile("appsettings.secret.json", true, true)
                .AddJsonFile("appsettings.json", true, true);

#if !DEBUG
           builder.AddEnvironmentVariables("SOPI_TESTS_");
#endif

            Instance = new Configurator
            {
                ConfigurationRoot = builder.Build()
            };
        }

        public IConfiguration ConfigurationRoot { get; private set; }

        public static Configurator Instance { get; }

        public T Get<T>(string sectionName)
        {
            return ConfigurationRoot.GetSection(sectionName).Get<T>();
        }

    }
}
