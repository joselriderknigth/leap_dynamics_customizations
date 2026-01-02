using System;
using Microsoft.Extensions.Configuration;

namespace E2E.Api.Tests.Config
{
    public class EnvironmentConfig
    {
        public string OrganizationId { get; set; } = null!;

        public static EnvironmentConfig Load(IConfiguration config)
        {
            return new EnvironmentConfig
            {
                OrganizationId = config["ORGANIZATION_ID"] ?? throw new Exception("Missing ORGANIZATION_ID")
            };
        }
    }
}

