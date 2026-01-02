using System;

using E2E.Api.Tests.Config;

namespace E2E.Api.Tests.Utils
{
    public sealed class TokenCache
    {
        private readonly int _renewalBufferSeconds;

        public TokenCache(TokenConfig tokenConfig)
        {
            _renewalBufferSeconds = tokenConfig.RenewalBufferSeconds;
        }

        public string? Token { get; private set; }
        public DateTime Expiry { get; private set; } = DateTime.MinValue;

        public bool IsValid => Token is not null && DateTime.UtcNow < Expiry;

        public void Set(string token, int expiresInSeconds)
        {
            Token = token;
            Expiry = DateTime.UtcNow.AddSeconds(expiresInSeconds - _renewalBufferSeconds);
        }
    }
}
