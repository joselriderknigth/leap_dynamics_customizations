using System;
using System.Net;

namespace E2E.Api.Tests.Utils
{
    public sealed class DataverseException : Exception
    {
        public HttpStatusCode StatusCode { get; }
        public string RawContent { get; }

        public DataverseException(HttpStatusCode statusCode, string rawContent)
            : base($"Dataverse returned {(int)statusCode} ({statusCode})")
        {
            StatusCode = statusCode;
            RawContent = rawContent;
        }
    }
}
