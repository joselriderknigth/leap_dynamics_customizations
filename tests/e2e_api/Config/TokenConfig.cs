using Microsoft.Extensions.Configuration;

namespace E2E.Api.Tests.Config
{
    public class TokenConfig
    {
        public int RenewalBufferSeconds { get; set; } = ConfigDefaults.TokenRenewalBufferSeconds;

        public static TokenConfig Load(IConfiguration config)
        {
            if (int.TryParse(config["Auth:TokenRenewalBufferSeconds"], out var parsed))
                return new TokenConfig { RenewalBufferSeconds = parsed };

            return new TokenConfig();
        }
    }
}
