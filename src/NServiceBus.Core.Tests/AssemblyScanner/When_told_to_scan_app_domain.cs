namespace NServiceBus.Core.Tests.AssemblyScanner
{
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Hosting.Helpers;
    using NUnit.Framework;

    [TestFixture]
    public class When_told_to_scan_app_domain
    {
        AssemblyScannerResults results;

        [SetUp]
        public void Context()
        {
            var someDir = Path.Combine(Path.GetTempPath(), "empty");
            Directory.CreateDirectory(someDir);

            results = new AssemblyScanner(someDir)
            {
                IncludeAppDomainAssemblies = true,
            }
                .GetScannableAssemblies();
        }

        class SomeHandlerThatEnsuresThatWeKeepReferencingNsbCore : IHandleMessages<string>
        {
            public Task Handle(string message)
            {
                return Task.FromResult(0);
            }
        }

        [Test]
        public void Should_use_AppDomain_Assemblies_if_flagged()
        {
            var collection = results.Assemblies.Select(a => a.GetName().Name).ToArray();

            CollectionAssert.Contains(collection, "NServiceBus.Core.Tests");
        }
    }
}