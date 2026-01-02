using System;
using System.Text.Json;
using System.Threading.Tasks;

using E2E.Api.Tests.Config;
using E2E.Api.Tests.Utils;

namespace E2E.Api.Tests.Services
{
    public sealed class AccountService : DynamicsServiceBase<AccountService>
    {
        protected override string EntityName => EntityNames.Account;
        public AccountService(DataverseClient client) : base(client) { }
    }
}
