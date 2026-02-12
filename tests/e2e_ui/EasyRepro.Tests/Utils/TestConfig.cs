// Utils/TestConfig.cs
using Microsoft.Extensions.Configuration;

public class TestConfig {
  public DynamicsCfg Dynamics { get; init; } = new();
  public BrowserCfg  Browser  { get; init; } = new();
  public DvCfg       Dataverse{ get; init; } = new();
  public static TestConfig Load() =>
    new ConfigurationBuilder()
      .AddJsonFile("appsettings.json", optional:false)
      .AddUserSecrets<TestConfig>(optional:true)     // local dev
      .AddEnvironmentVariables()                    // CI
      .Build().Get<TestConfig>()!;
}
public record DynamicsCfg { public string Url { get; init; } = ""; public string User { get; init; } = ""; public string Password { get; init; } = ""; }
public record BrowserCfg  { public string Type { get; init; } = "Edge"; public bool Headless { get; init; } = true; public int ImplicitWaitMs { get; init; } = 4000; }
public record DvCfg       { public string ResourceUrl { get; init; } = ""; public string TenantId { get; init; } = ""; public string ClientId { get; init; } = ""; public string ClientSecret { get; init; } = ""; }
