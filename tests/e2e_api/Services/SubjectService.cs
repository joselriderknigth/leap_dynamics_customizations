using System;
using System.Text.Json;
using System.Threading.Tasks;

using E2E.Api.Tests.Config;
using E2E.Api.Tests.Utils;

namespace E2E.Api.Tests.Services
{
    public sealed class SubjectService : DynamicsServiceBase<SubjectService>
    {
        protected override string EntityName => EntityNames.Subject;
        public SubjectService(DataverseClient client) : base(client) { }

       public Task<Guid> GetByTitleAsync(string title) => Client.GetByNameAsync(
            EntityName,
            nameField: "title",
            nameValue: title,
            idField: "subjectid");
    }
}
