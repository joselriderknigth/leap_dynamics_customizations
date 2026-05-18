using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using E2E.Api.Tests.Config;
using E2E.Api.Tests.Utils;

namespace E2E.Api.Tests.Services
{
    public sealed class JobService : DynamicsServiceBase<JobService>
    {
        protected override string EntityName => EntityNames.Job;

        public JobService(DataverseClient client)
            : base(client) { }

        public async Task<List<JsonElement>> GetJobsByUsageFormDetailsAndTypeAsync(
            List<Guid> usageFormDetailIds,
            int jobType
        )
        {
            if (usageFormDetailIds.Count == 0)
                return new List<JsonElement>();

            var conditions = string.Join(
                "",
                usageFormDetailIds.ConvertAll(id => $"<value>{id}</value>")
            );

            var fetchXml =
                $@"
              <fetch>
                <entity name='leap_job'>
                  <attribute name='leap_jobid' />
                  <attribute name='statuscode' />
                  <attribute name='leap_type' />
                  <attribute name='leap_usageformdetail' />
                  <filter type='and'>
                    <condition attribute='leap_usageformdetail' operator='in'>
                      {conditions}
                    </condition>
                    <condition attribute='leap_type' operator='eq' value='{jobType}' />
                  </filter>
                </entity>
              </fetch>";

            return await Client.GetMultipleByFetchXmlAsync("leap_jobs", fetchXml);
        }
    }
}
