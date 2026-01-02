using System;

using System.ComponentModel.DataAnnotations;

using Microsoft.Extensions.Configuration;

namespace E2E.Api.Tests.Config
{
    public sealed class TestUserConfig
    {
        [Required]
        public Guid SalesRepUserId { get; init; }

        public Guid? AccountantUserId { get; init; }
        public Guid? AdminUserId { get; init; }

        public static TestUserConfig Load(IConfiguration config)
        {
            ArgumentNullException.ThrowIfNull(config);

            var section = new TestUserConfig
            {
                SalesRepUserId = ParseRequiredGuid(config, "SALES_REP_USER_ID"),
                AccountantUserId = ParseOptionalGuid(config, "ACCOUNTANT_USER_ID"),
                AdminUserId = ParseOptionalGuid(config, "ADMIN_USER_ID")
            };

            return section;
        }

        private static Guid ParseRequiredGuid(IConfiguration config, string key)
        {
            var value = config[key];
            if (string.IsNullOrWhiteSpace(value))
                throw new InvalidOperationException($"Missing required environment variable: {key}");

            if (!Guid.TryParse(value, out var guid))
                throw new FormatException($"Invalid GUID format for environment variable: {key}");

            return guid;
        }

        private static Guid? ParseOptionalGuid(IConfiguration config, string key)
        {
            var value = config[key];
            return Guid.TryParse(value, out var guid) ? guid : null;
        }
    }
}
