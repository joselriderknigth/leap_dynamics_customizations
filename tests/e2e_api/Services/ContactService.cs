using System;
using System.Text.Json;
using System.Threading.Tasks;

using E2E.Api.Tests.Config;
using E2E.Api.Tests.Utils;

namespace E2E.Api.Tests.Services
{
    public sealed class ContactService : DynamicsServiceBase<ContactService>
    {
        protected override string EntityName => EntityNames.Contact;
        public ContactService(DataverseClient client) : base(client) { }
    }
}
