using System;
using System.Reflection;
using Codecks.Editor;
using Codecks.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Codecks.Tests.Editor
{
    public class CodecksSettingsTests
    {
        private Func<CodecksSettings> originalLoader;
        private GameObject host;
        private CodecksCardCreator creator;
        private CodecksSettings configuration;
        private bool createdDefaultAsset;
        private bool createdLegacyFile;
        private const string LegacyPath = "Assets/Resources/Codecks/codecksToken.txt";

        [SetUp]
        public void SetUp()
        {
            originalLoader = CodecksCardCreator.SettingsLoader;
            CodecksCardCreator.SettingsLoader = () => null;
            host = new GameObject("Dummy Codecks settings test");
            creator = host.AddComponent<CodecksCardCreator>();
            creator.defaultToken = "legacy-component-dummy";
            creator.codecksURL = "http://127.0.0.1:12345/legacy";
            configuration = ScriptableObject.CreateInstance<CodecksSettings>();
            configuration.reportToken = "settings-dummy";
            configuration.endpoint = "http://127.0.0.1:12345/configured";
        }

        [TearDown]
        public void TearDown()
        {
            CodecksCardCreator.SettingsLoader = originalLoader;
            UnityEngine.Object.DestroyImmediate(host);
            UnityEngine.Object.DestroyImmediate(configuration);
            if (createdDefaultAsset) AssetDatabase.DeleteAsset(CodecksSettingsSetup.DefaultAssetPath);
            if (createdLegacyFile) AssetDatabase.DeleteAsset(LegacyPath);
        }

        [Test]
        public void ExplicitSettingsOverrideAutomaticAndLegacy()
        {
            creator.settings = configuration;
            CodecksCardCreator.SettingsLoader = () => throw new Exception("Explicit settings must not load automatic configuration");
            SetLoadedToken("legacy-resource-dummy");
            AssertConfiguration(creator, "settings-dummy", configuration.endpoint);
        }

        [Test]
        public void AutomaticConfigurationIsSharedByBothCreatorsWithoutWiring()
        {
            Assert.That(AssetDatabase.LoadMainAssetAtPath(CodecksSettingsSetup.DefaultAssetPath), Is.Null,
                "Run in a disposable host; never overwrite configured settings.");
            Assert.That(System.IO.File.Exists(CodecksSettingsSetup.DefaultAssetPath), Is.False);
            var shared = CodecksSettingsSetup.GetOrCreateDefaultAsset();
            createdDefaultAsset = true;
            shared.reportToken = "automatic-dummy";
            shared.endpoint = "http://127.0.0.1:12345/automatic";
            EditorUtility.SetDirty(shared);
            AssetDatabase.SaveAssetIfDirty(shared);
            CodecksCardCreator.SettingsLoader = () => Resources.Load<CodecksSettings>(CodecksSettings.ResourcePath);
            SetLoadedToken("legacy-resource-dummy");
            AssertConfiguration(creator, "automatic-dummy", shared.endpoint);
            var second = host.AddComponent<CodecksCardCreator>();
            AssertConfiguration(second, "automatic-dummy", shared.endpoint);
        }

        [Test]
        public void SetupActionPreservesExistingAssetAndToken()
        {
            Assert.That(System.IO.File.Exists(CodecksSettingsSetup.DefaultAssetPath), Is.False,
                "Run in a disposable host; never inspect or overwrite a real settings asset.");
            var first = CodecksSettingsSetup.GetOrCreateDefaultAsset();
            createdDefaultAsset = true;
            first.reportToken = "existing-dummy";
            first.endpoint = "http://127.0.0.1:12345/preserved";
            var second = CodecksSettingsSetup.GetOrCreateDefaultAsset();
            Assert.That(second, Is.SameAs(first));
            Assert.That(second.reportToken, Is.EqualTo("existing-dummy"));
            Assert.That(second.endpoint, Is.EqualTo("http://127.0.0.1:12345/preserved"));
        }

        [Test]
        public void SetupActionRefusesAnOccupiedWrongTypePath()
        {
            Assert.That(System.IO.File.Exists(CodecksSettingsSetup.DefaultAssetPath), Is.False);
            // Create folders safely first, then replace only our test-created asset.
            CodecksSettingsSetup.GetOrCreateDefaultAsset();
            createdDefaultAsset = true;
            AssetDatabase.DeleteAsset(CodecksSettingsSetup.DefaultAssetPath);
            var other = new TextAsset("dummy occupied path");
            AssetDatabase.CreateAsset(other, CodecksSettingsSetup.DefaultAssetPath);
            Assert.Throws<InvalidOperationException>(() => CodecksSettingsSetup.GetOrCreateDefaultAsset());
            Assert.That(AssetDatabase.LoadMainAssetAtPath(CodecksSettingsSetup.DefaultAssetPath), Is.SameAs(other));
        }

        [Test]
        public void NoSettingsPreservesLegacyResourceThenComponentPrecedence()
        {
            SetLoadedToken("legacy-resource-dummy");
            AssertConfiguration(creator, "legacy-resource-dummy", creator.codecksURL);
            SetLoadedToken("");
            AssertConfiguration(creator, "legacy-component-dummy", creator.codecksURL);
        }

        [Test]
        public void LegacyTokenFileStillLoadsWhenNoSettingsExist()
        {
            Assert.That(System.IO.File.Exists(LegacyPath), Is.False, "Do not read or overwrite a real token file.");
            // Creates only our disposable directory, with no configured settings asset left behind.
            Assert.That(System.IO.File.Exists(CodecksSettingsSetup.DefaultAssetPath), Is.False);
            CodecksSettingsSetup.GetOrCreateDefaultAsset();
            createdDefaultAsset = true;
            System.IO.File.WriteAllText(LegacyPath, "legacy-file-dummy");
            createdLegacyFile = true;
            AssetDatabase.ImportAsset(LegacyPath, ImportAssetOptions.ForceSynchronousImport);
            creator.GetType().GetMethod("Start", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(creator, null);
            AssertConfiguration(creator, "legacy-file-dummy", creator.codecksURL);
        }

        [Test]
        public void EmptySettingsTokenFailsWithoutLegacyFallbackOrNetwork()
        {
            configuration.reportToken = " ";
            creator.settings = configuration;
            int callbacks = 0;
            creator.CreateNewCard("dummy report", CodecksCardCreator.CodecksSeverity.None, resultDelegate: (success, result) =>
            {
                callbacks++;
                Assert.That(success, Is.False);
                Assert.That(result, Is.EqualTo("Codecks settings contain an empty report token."));
            });
            Assert.That(callbacks, Is.EqualTo(1));
        }

        [TestCase("")]
        [TestCase("not-a-url")]
        [TestCase("ftp://example.invalid/report")]
        [TestCase("https://example.invalid/report?token=anything")]
        [TestCase("https://example.invalid/report#fragment")]
        [TestCase("https://user:password@example.invalid/report")]
        public void InvalidSettingsEndpointFailsWithoutProductionFallback(string endpoint)
        {
            creator.settings = configuration;
            configuration.endpoint = endpoint;
            Assert.That(creator.TryResolveConfiguration(out _, out _, out string error), Is.False);
            Assert.That(error, Does.StartWith("Codecks settings contain an invalid create-report endpoint"));
            Assert.That(error, Does.Not.Contain(endpoint == "" ? "never-present" : endpoint));
        }

        [Test]
        public void InvalidEndpointCompletesOnceWithoutConstructingRequest()
        {
            creator.settings = configuration;
            configuration.endpoint = "invalid-endpoint";
            var originalFactory = CodecksCardCreator.PostRequestFactory;
            int requests = 0, callbacks = 0;
            try
            {
                CodecksCardCreator.PostRequestFactory = (_, _) => { requests++; return null; };
                creator.CreateNewCard("dummy report", CodecksCardCreator.CodecksSeverity.None, resultDelegate: (success, result) =>
                {
                    callbacks++;
                    Assert.That(success, Is.False);
                    Assert.That(result, Does.StartWith("Codecks settings contain an invalid create-report endpoint"));
                });
                Assert.That(requests, Is.Zero);
                Assert.That(callbacks, Is.EqualTo(1));
            }
            finally { CodecksCardCreator.PostRequestFactory = originalFactory; }
        }

        [Test]
        public void AutomaticEmptyAssetIsAuthoritativeRatherThanLegacyFallback()
        {
            configuration.reportToken = null;
            CodecksCardCreator.SettingsLoader = () => configuration;
            Assert.That(creator.TryResolveConfiguration(out _, out _, out string error), Is.False);
            Assert.That(error, Is.EqualTo("Codecks settings contain an empty report token."));
        }

        private void SetLoadedToken(string token) => creator.GetType().GetField("loadedToken", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(creator, token);
        private static void AssertConfiguration(CodecksCardCreator target, string token, string endpoint)
        {
            Assert.That(target.TryResolveConfiguration(out string actualToken, out string actualEndpoint, out string error), Is.True, error);
            Assert.That(actualToken, Is.EqualTo(token));
            Assert.That(actualEndpoint, Is.EqualTo(endpoint));
        }

    }
}
