using System;
using System.IO;
using E2E.Api.Tests.Config;
using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace E2E.Api.Tests.Utils
{
    public class TestConfig
    {
        public AuthConfig Auth { get; private set; } = null!;
        public TokenConfig Token { get; private set; } = null!;
        public TestUserConfig Users { get; private set; } = null!;
        public EnvironmentConfig Environment { get; private set; } = null!;
        public SeedDataConfig SeedData { get; init; } = null!;

        public static TestConfig Load()
        {
            var testEnv = System.Environment.GetEnvironmentVariable("TEST_ENV") ?? "dev";

            Console.WriteLine($"[TestConfig] Loading '{testEnv}' environment...");

            var basePath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..");
            var envFile = $".env.{testEnv}";
            var envPath = Path.Combine(basePath, envFile);
            if (!File.Exists(envPath))
                throw new FileNotFoundException($"Missing .env file at expected path: {envPath}");

            DotNetEnv.Env.Load(envPath);

            Console.WriteLine($"[TestConfig] Loaded env file: {envFile}");

            var configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: true)
                .AddEnvironmentVariables()
                .Build();

            return new TestConfig
            {
                Auth = AuthConfig.Load(configuration),
                Token = TokenConfig.Load(configuration),
                Users = TestUserConfig.Load(configuration),
                Environment = EnvironmentConfig.Load(configuration),
                SeedData = SeedDataConfig.Load(configuration),
            };
        }
    }
}
