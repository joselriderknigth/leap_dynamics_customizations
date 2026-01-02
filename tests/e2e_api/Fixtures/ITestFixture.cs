using System.Threading.Tasks;

public interface ITestFixture
{
   Task CleanupAsync();
}
