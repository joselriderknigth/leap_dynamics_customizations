using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using E2E.Api.Tests.Config;
using E2E.Api.Tests.Services;
using E2E.Api.Tests.Utils;

namespace E2E.Api.Tests.Fixtures
{
    public class CaseFixture : ITestFixture
    {
        private readonly FixtureConfig _config;
        private readonly CaseService _cases;
        private readonly JobService _jobs;
        private readonly List<Guid> _createdIds = new();

        // For cleanup
        private readonly UsageFormService _usageForms;
        private readonly UsageFormDetailService _usageFormDetails;
        private readonly SalesAdjustmentService _salesAdjustments;
        private readonly SupplierCommissionService _supplierCommissions;
        private readonly SalesRepCommissionService _repCommissions;

        public Guid CaseId { get; private set; }

        public CaseFixture(DataverseClient client, FixtureConfig config)
        {
            _config = config;
            _cases = new CaseService(client);
            _jobs = new JobService(client);

            // For cleanup
            _usageForms = new UsageFormService(client);
            _usageFormDetails = new UsageFormDetailService(client);
            _salesAdjustments = new SalesAdjustmentService(client);
            _supplierCommissions = new SupplierCommissionService(client);
            _repCommissions = new SalesRepCommissionService(client);
        }

        public async Task<Guid> SetupAsync(
            Guid hospitalId,
            Guid physicianId,
            Guid procedureId,
            string? patientId = null,
            DateTime? caseStartTime = null
        )
        {
            var casePayload = new Dictionary<string, object>
            {
                ["leap_Hospital@odata.bind"] = $"/accounts({hospitalId})",
                ["leap_Surgeon@odata.bind"] = $"/contacts({physicianId})",
                ["leap_Procedure@odata.bind"] = $"/leap_procedures({procedureId})",
                ["leap_casestarttime"] =
                    caseStartTime?.ToString("o") ?? DateTime.UtcNow.ToString("o"),
                ["leap_SalesPerson@odata.bind"] = $"/systemusers({_config.OwnerUserId})",
                ["ownerid@odata.bind"] = $"/systemusers({_config.OwnerUserId})",
                ["leap_notes"] = _config.CorrelationId,
                ["statecode"] = _config.DefaultStateCode,
                ["statuscode"] = _config.DefaultStatusCode,
            };

            if (!string.IsNullOrEmpty(patientId))
                casePayload["leap_patientid"] = patientId;

            var id = await _cases.CreateAsync(casePayload);
            _createdIds.Add(id);
            return id;
        }

        public async Task<Guid> RegenCaseAsync(Guid caseId)
        {
            var updatePayload = new Dictionary<string, object> { ["leap_regeneratecase"] = true };
            await _cases.UpdateAsync(caseId, updatePayload);
            return caseId;
        }

        public async Task WaitUntilRegenerateCompleteAsync(
            Guid caseId,
            TimeSpan? timeout = null,
            TimeSpan? pollInterval = null
        )
        {
            timeout ??= TimeSpan.FromSeconds(60);
            pollInterval ??= TimeSpan.FromSeconds(2);

            var start = DateTime.UtcNow;

            // Step 1: Wait for regenerate flag to complete and usage forms to be processed
            bool usageFormsComplete = false;
            while (!usageFormsComplete)
            {
                var caseRecord = await _cases.GetAsync(caseId);
                var isCompleted =
                    caseRecord.TryGetProperty("leap_regeneratecase", out var regenCaseFlag)
                    && regenCaseFlag.ValueKind == JsonValueKind.False;
                var isProcessed =
                    caseRecord.TryGetProperty(
                        "leap_allusageformscomplete",
                        out var processingCompleteFlag
                    )
                    && processingCompleteFlag.ValueKind == JsonValueKind.True;

                if (isCompleted && isProcessed)
                {
                    usageFormsComplete = true;
                    break;
                }

                if (DateTime.UtcNow - start > timeout)
                    throw new TimeoutException(
                        $"Case {caseId} did not finish regeneration within {timeout.Value.TotalSeconds}s"
                    );

                await Task.Delay(pollInterval.Value);
            }

            // Step 2: Get all Usage Form Details for this case
            var usageFormDetails = await _usageFormDetails.GetByCaseAsync(caseId);
            var usageFormDetailIds = usageFormDetails
                .Where(d => d.TryGetProperty("leap_usageformdetailid", out var id))
                .Select(d => Guid.Parse(d.GetProperty("leap_usageformdetailid").GetString()!))
                .ToList();

            if (!usageFormDetailIds.Any())
            {
                throw new InvalidOperationException(
                    $"No Usage Form Details found for case {caseId} after regeneration"
                );
            }

            // Step 3: Wait for all jobs to be created (1 per usage form detail)
            List<JsonElement> managerJobs;
            while (true)
            {
                managerJobs = await _jobs.GetJobsByUsageFormDetailsAndTypeAsync(
                    usageFormDetailIds,
                    OptionSetValues.Job.Type.ManagerCommissionGeneration
                );

                // TODO: consider new products which won't have a SRep commission job
                if (managerJobs.Count == usageFormDetailIds.Count)
                    break;

                if (DateTime.UtcNow - start > timeout)
                    throw new TimeoutException(
                        $"Expected {usageFormDetailIds.Count} Manager Standard commission jobs for case {caseId}, but {managerJobs.Count} were created within {timeout.Value.TotalSeconds}s"
                    );

                await Task.Delay(pollInterval.Value);
            }

            // Step 4: Wait for all jobs to complete (succeeded or failed)
            while (true)
            {
                managerJobs = await _jobs.GetJobsByUsageFormDetailsAndTypeAsync(
                    usageFormDetailIds,
                    OptionSetValues.Job.Type.ManagerCommissionGeneration
                );

                var allCompleted = true;
                foreach (var job in managerJobs)
                {
                    if (job.TryGetProperty("statuscode", out var statusCode))
                    {
                        var status = statusCode.GetInt32();

                        if (status == OptionSetValues.Job.Status.Failed)
                        {
                            var jobId = job.TryGetProperty("leap_jobid", out var id)
                                ? id.GetString()
                                : "unknown";
                            throw new InvalidOperationException(
                                $"Manager Standard commission job {jobId} for case {caseId} failed"
                            );
                        }

                        if (status != OptionSetValues.Job.Status.Succeeded)
                        {
                            allCompleted = false;
                        }
                    }
                    else
                    {
                        allCompleted = false;
                    }
                }

                if (allCompleted)
                    return;

                if (DateTime.UtcNow - start > timeout)
                    throw new TimeoutException(
                        $"Not all Manager Standard commission jobs for case {caseId} completed within {timeout.Value.TotalSeconds}s"
                    );

                await Task.Delay(pollInterval.Value);
            }
        }

        public async Task CleanupAsync()
        {
            foreach (var id in _createdIds)
            {
                await _salesAdjustments.DeleteByCaseIdAsync(id);
                await _supplierCommissions.DeleteByCaseIdAsync(id);
                await _repCommissions.DeleteByCaseIdAsync(id);
                await _usageFormDetails.DeleteByCaseIdAsync(id);
                await _usageForms.DeleteByCaseIdAsync(id);
                await _cases.DeleteAsync(id);
            }

            _createdIds.Clear();
        }
    }
}
