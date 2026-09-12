/// <summary>
/// PwaHostAssetsTests
/// </summary>

namespace SafeExchange.Client.Web.Components.Tests
{
    using NUnit.Framework;

    [TestFixture]
    public class PwaHostAssetsTests
    {
        private static string ReadHostFile(string relativePath)
        {
            var directory = TestContext.CurrentContext.TestDirectory;
            while (directory is not null && !File.Exists(Path.Combine(directory, "SafeExchange.PWA.slnx")))
            {
                directory = Path.GetDirectoryName(directory);
            }

            Assert.That(directory, Is.Not.Null, "Could not locate the repository root.");

            var path = Path.Combine(directory!, "SafeExchange.PWA", "wwwroot", relativePath);
            Assert.That(File.Exists(path), Is.True, $"Missing host file: {path}");
            return File.ReadAllText(path);
        }

        [TestCase("quill.2.0.3.min.js")]
        [TestCase("quill-modules/image-drop.min.js")]
        [TestCase("quill-modules/quill-blot-formatter2.min.js")]
        public void EditorDependencyLoadsBeforeBlazor(string dependency)
        {
            // richTextEditor.js reads the Quill and QuillBlotFormatter2 globals while the module
            // is evaluated, and Blazor can import it from a component lifecycle as soon as it boots.
            var html = ReadHostFile("index.html");

            var dependencyIndex = html.IndexOf(dependency, StringComparison.Ordinal);
            var blazorIndex = html.IndexOf("_framework/blazor.webassembly.js", StringComparison.Ordinal);

            Assert.That(dependencyIndex, Is.GreaterThanOrEqualTo(0), $"{dependency} is not referenced.");
            Assert.That(blazorIndex, Is.GreaterThanOrEqualTo(0), "blazor.webassembly.js is not referenced.");
            Assert.That(dependencyIndex, Is.LessThan(blazorIndex), $"{dependency} must load before Blazor starts.");
        }

        [TestCase("async")]
        [TestCase("defer")]
        public void EditorDependenciesAreNotDeferred(string attribute)
        {
            var html = ReadHostFile("index.html");
            var quillTagIndex = html.IndexOf("quill.2.0.3.min.js", StringComparison.Ordinal);
            var tagEnd = html.IndexOf('>', quillTagIndex);
            var tagStart = html.LastIndexOf('<', quillTagIndex);

            var tag = html.Substring(tagStart, tagEnd - tagStart);

            Assert.That(tag, Does.Not.Contain(attribute), $"Quill must not be {attribute}: ordering is the whole point.");
        }

        [Test]
        public void HostingConfigurationIsExcludedFromPrecache()
        {
            // SWA reserves staticwebapp.config.json and serves a different response, so a
            // build-time integrity hash for it aborts the whole service worker install.
            var serviceWorker = ReadHostFile("service-worker.published.js");

            Assert.That(serviceWorker, Does.Contain(@"/^staticwebapp\.config\.json$/"));
        }

        [Test]
        public void ApplicationAssetsKeepTheirIntegrityHashes()
        {
            var serviceWorker = ReadHostFile("service-worker.published.js");

            Assert.That(serviceWorker, Does.Contain("integrity: asset.hash"));
            Assert.That(serviceWorker, Does.Contain(@"/^service-worker\.js$/"));
        }
    }
}
