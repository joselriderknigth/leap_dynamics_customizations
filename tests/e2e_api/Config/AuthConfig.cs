using System;
using Microsoft.Extensions.Configuration;

namespace E2E.Api.Tests.Config
{
    public class AuthConfig
    {
        public string DynamicsUrl { get; set; } = null!;
        public string DataverseTenant { get; set; } = null!;
        public string DataverseClientId { get; set; } = null!;
        public string DataverseClientSecret { get; set; } = null!;

        public static AuthConfig Load(IConfiguration config)
        {
            return new AuthConfig
            {
                DynamicsUrl        = config["DYNAMICS_URL"] ?? throw new Exception("Missing DYNAMICS_URL"),
                DataverseTenant     = config["DATAVERSE_TENANT"] ?? throw new Exception("Missing DATAVERSE_TENANT"),
                DataverseClientId   = config["DATAVERSE_CLIENTID"] ?? throw new Exception("Missing DATAVERSE_CLIENTID"),
                DataverseClientSecret = config["DATAVERSE_CLIENTSECRET"] ?? throw new Exception("Missing DATAVERSE_CLIENTSECRET")
            };
        }
    }
}
