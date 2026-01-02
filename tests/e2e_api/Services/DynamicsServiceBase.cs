using System;
using System.Text.Json;
using System.Threading.Tasks;
using E2E.Api.Tests.Utils;

namespace E2E.Api.Tests.Services
{
    public abstract class DynamicsServiceBase<TService>
        where TService : class
    {
        protected readonly DataverseClient Client;
        protected abstract string EntityName { get; }

        protected DynamicsServiceBase(DataverseClient client) => Client = client;

        private static string TryExtractDataverseMessage(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                return doc.RootElement.GetProperty("error").GetProperty("message").GetString()
                    ?? "(no message)";
            }
            catch
            {
                return "(unable to parse message)";
            }
        }

        private static string TryExtractDataverseCode(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                return doc.RootElement.GetProperty("error").GetProperty("code").GetString()
                    ?? "(no code)";
            }
            catch
            {
                return "(unable to parse code)";
            }
        }

        protected static Exception Wrap(
            Exception ex,
            string entity,
            string operation,
            object? context = null
        )
        {
            var details = context switch
            {
                Guid id => $"Entity: {entity}, Id: {id}",
                null => $"Entity: {entity}",
                _ => $"Entity: {entity}, Context: {JsonSerializer.Serialize(context)}",
            };

            if (ex is DataverseException d)
            {
                var dvMessage = TryExtractDataverseMessage(d.RawContent);
                var dvCode = TryExtractDataverseCode(d.RawContent);

                return new InvalidOperationException(
                    $"Failed to {operation} {details}.\n"
                        + $"StatusCode: {(int)d.StatusCode} ({d.StatusCode}).\n"
                        + $"Dataverse Message: {dvMessage}\n"
                        + $"Dataverse Code: {dvCode}\n"
                        + $"Raw Response: {d.RawContent}",
                    ex
                );
            }

            return new InvalidOperationException(
                $"Failed to {operation} {details}. See inner exception for details.",
                ex
            );
        }

        public virtual async Task<Guid> CreateAsync(object payload)
        {
            try
            {
                return await Client.CreateAsync(EntityName, payload);
            }
            catch (Exception ex)
            {
                throw Wrap(ex, EntityName, "Create", payload);
            }
        }

        public virtual async Task<JsonElement> GetAsync(Guid id)
        {
            try
            {
                return await Client.GetAsync(EntityName, id);
            }
            catch (Exception ex)
            {
                throw Wrap(ex, EntityName, "Get", id);
            }
        }

        public virtual async Task UpdateAsync(Guid id, object payload)
        {
            try
            {
                await Client.UpdateAsync(EntityName, id, payload);
            }
            catch (Exception ex)
            {
                throw Wrap(ex, EntityName, "Update", id);
            }
        }

        public virtual async Task DeleteAsync(Guid id)
        {
            try
            {
                await Client.DeleteAsync(EntityName, id);
            }
            catch (Exception ex)
            {
                throw Wrap(ex, EntityName, "Delete", id);
            }
        }
    }
}
