using DbUp;
using Microsoft.Extensions.Configuration;
using System;
using System.Linq;
using System.IO;
using System.Reflection;
using dotenv.net;
using System.Collections;

namespace DatabaseReleaseAutomation.DbUpDemo
{
    class Program
    {
        static string GetProjectRootDirectory()
        {
            var currentDirectory = Directory.GetCurrentDirectory();
            while (!currentDirectory.EndsWith("DatabaseReleaseAutomation"))
            {
                currentDirectory = Directory.GetParent(currentDirectory).FullName;
            }
            return currentDirectory;
        }

        static int Main(string[] args)
        {
            // Use the method to find the project root directory
            var projectRoot = GetProjectRootDirectory();
            Console.WriteLine($"Project root directory: {projectRoot}");

            // Path to the .env file
            var envFilePath = Path.Combine(projectRoot, "config.env");
            Console.WriteLine($"Loading .env file from: {envFilePath}");

            // Load environment variables from .env file explicitly specifying the path
            DotEnv.Load(options: new DotEnvOptions(envFilePaths: new[] { envFilePath }));

            // Debugging: Print all environment variables to verify loading
            foreach (DictionaryEntry de in Environment.GetEnvironmentVariables())
            {
                Console.WriteLine($"{de.Key} = {de.Value}");
            }

            // Sets the connection string value from the environment variables or app settings
            var connectionString = Environment.GetEnvironmentVariable("CONNECTION_STRING");

            if (string.IsNullOrEmpty(connectionString))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Connection string is missing.");
                Console.ResetColor();
                return -1;
            }

            // Creates the database if it doesn't already exist
            EnsureDatabase.For.SqlDatabase(connectionString);

            // Creates the DbUp builder, setting the connection string to use, scripts to apply, and to log output to the console. Can be configured as desired
            var upgrader = DeployChanges.To
                .SqlDatabase(connectionString)
                .WithScriptsEmbeddedInAssembly(Assembly.GetExecutingAssembly())
                .LogToConsole()
                .Build();

            // Performs the upgrade as per the configuration and scripts loaded above
            var result = upgrader.PerformUpgrade();

            // Determine what to do if the result was unsuccessful
            if (!result.Successful)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(result.Error);
                Console.ResetColor();
#if DEBUG
                Console.ReadLine();
#endif
                return -1;
            }

            // Completed update successfully
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Success!");
            Console.ResetColor();
            return 0;
        }
    }
}
