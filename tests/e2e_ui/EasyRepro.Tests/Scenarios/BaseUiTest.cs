// Scenarios/BaseUiTest.cs
using Microsoft.Dynamics365.UIAutomation.Api.UCI;

public abstract class BaseUiTest {
  protected XrmApp Xrm = null!;
  protected TestConfig Cfg = TestConfig.Load();

  [TestInitialize]
  public void Init() {
    var client = BrowserFactory.Create(Cfg.Browser); // your helper
    Xrm = new XrmApp(client);
    Xrm.OnlineLogin.Login(new Uri(Cfg.Dynamics.Url), Cfg.Dynamics.User, Cfg.Dynamics.Password);
  }
  [TestCleanup] public void Cleanup() => Xrm?.Dispose();
}
