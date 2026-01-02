using System;
using System.IO;

namespace E2E.Api.Tests.Utils;

public static class FetchXmlUtils
{
    public static string LoadFetchXml(string relativePath)
    {
        var fullPath = Path.Combine(AppContext.BaseDirectory, relativePath);

        if (!File.Exists(fullPath))
            throw new FileNotFoundException($"FetchXML file not found: {fullPath}");

        return File.ReadAllText(fullPath);
    }
}
