using System;
using E2E.Api.Tests.Config;
using E2E.Api.Tests.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public abstract class BaseTest
{
    protected DataverseClient Client = null!;
    protected FixtureConfig Config = null!;
    protected TestUserConfig Users = null!;
    protected EnvironmentConfig Environment = null!;
    protected SeedDataConfig Seed = null!;

    [TestInitialize]
    public void BaseSetup()
    {
        var env = TestConfig.Load();

        Users = env.Users;
        Environment = env.Environment;
        Seed = env.SeedData;

        Config = new FixtureConfig
        {
            Environment = Environment,
            CorrelationId = Guid.NewGuid().ToString(),
            OwnerUserId = Users.SalesRepUserId,
            DefaultStateCode = 0,
            DefaultStatusCode = 1,
        };

        Client = new DataverseClient(env);
    }
}
