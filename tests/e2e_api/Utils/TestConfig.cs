using System;
using System.IO;

using Microsoft.Extensions.Configuration;

using E2E.Api.Tests.Config;

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
            var envPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".env");
            if (!File.Exists(envPath))
                throw new FileNotFoundException($"Missing .env file at expected path: {envPath}");

            DotNetEnv.Env.Load(envPath);

            var configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: true)
                .AddEnvironmentVariables()
                .Build();

            return new TestConfig
            {
                Auth  = AuthConfig.Load(configuration),
                Token = TokenConfig.Load(configuration),
                Users = TestUserConfig.Load(configuration),
                Environment = EnvironmentConfig.Load(configuration),
                SeedData    = SeedDataConfig.Load(configuration)
            };
        }
    }
}
