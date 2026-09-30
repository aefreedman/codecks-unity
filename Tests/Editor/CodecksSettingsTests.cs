using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Codecks.Editor;
using Codecks.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Codecks.Tests.Editor
{
    public class CodecksSettingsTests
    {
        private GameObject host;
        private CodecksCardCreator creator;
        private CodecksSettings configuration;
        private bool createdDefaultAsset;
        private bool createdTestFolder;
        private const string TestFolder = "Assets/Samples/CodecksSetupTests";
        private const string TestScene = TestFolder + "/CodecksUIToolkitFeedbackReporterScene.unity";

        [SetUp]
        public void SetUp()
        {
            createdDefaultAsset = false;
            createdTestFolder = false;
            host = new GameObject("Dummy Codecks settings test");
            creator = host.AddComponent<CodecksCardCreator>();
            configuration = ScriptableObject.CreateInstance<CodecksSettings>();
            configuration.reportToken = "settings-dummy";
            configuration.endpoint = "http://127.0.0.1:12345/configured";
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(host);
            UnityEngine.Object.DestroyImmediate(configuration);
            var scene = SceneManager.GetSceneByPath(TestScene);
            if (createdTestFolder && scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
            if (createdTestFolder && AssetDatabase.IsValidFolder(TestFolder)) AssetDatabase.DeleteAsset(TestFolder);
            if (createdDefaultAsset) AssetDatabase.DeleteAsset(CodecksSettingsSetup.DefaultAssetPath);
        }

        [Test]
        public void ExplicitReferenceSharesConfigurationAcrossCreators()
        {
            creator.settings = configuration;
            AssertConfiguration(creator, "settings-dummy", configuration.endpoint);
            var second = host.AddComponent<CodecksCardCreator>();
            second.settings = configuration;
            AssertConfiguration(second, "settings-dummy", configuration.endpoint);
        }

        [Test]
        public void MissingReferenceFailsEvenWhenDefaultAssetExists()
        {
            var shared = CreateDefault();
            shared.reportToken = "default-dummy";
            Assert.That(creator.TryResolveConfiguration(out _, out _, out string error), Is.False);
            Assert.That(error, Does.StartWith("Codecks report settings are required."));
        }

        [Test]
        public void HiddenObsoleteLegacyFieldsCannotBypassRequiredReference()
        {
            foreach (string name in new[] { "defaultToken", "codecksURL" })
            {
                var field = typeof(CodecksCardCreator).GetField(name);
                Assert.That(field.GetCustomAttribute<HideInInspector>(), Is.Not.Null);
                Assert.That(field.GetCustomAttribute<ObsoleteAttribute>(), Is.Not.Null);
                field.SetValue(creator, name == "defaultToken" ? "legacy-dummy" : "http://127.0.0.1:12345/legacy");
            }
            Assert.That(creator.TryResolveConfiguration(out _, out _, out _), Is.False);
            Assert.That(typeof(CodecksCardCreator).GetMethod("Start", BindingFlags.Instance | BindingFlags.NonPublic), Is.Null,
                "The backend must not read legacy token files.");
        }

        [Test]
        public void MissingSettingsCompletesOnceWithoutConstructingRequest()
        {
            AssertConfigurationFailure("Codecks report settings are required.");
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        public void EmptyTokenCompletesOnceWithoutNetwork(string token)
        {
            creator.settings = configuration;
            configuration.reportToken = token;
            AssertConfigurationFailure("Codecks settings contain an empty report token.");
        }

        [TestCase("")]
        [TestCase("not-a-url")]
        [TestCase("ftp://example.invalid/report")]
        [TestCase("https://example.invalid/report?token=anything")]
        [TestCase("https://example.invalid/report#fragment")]
        [TestCase("https://user:password@example.invalid/report")]
        public void InvalidEndpointCompletesOnceWithoutFallbackOrNetwork(string endpoint)
        {
            creator.settings = configuration;
            configuration.endpoint = endpoint;
            AssertConfigurationFailure("Codecks settings contain an invalid create-report endpoint");
        }

        [Test]
        public void SetupActionPreservesExistingAssetAndToken()
        {
            var first = CreateDefault();
            first.reportToken = "existing-dummy";
            first.endpoint = "http://127.0.0.1:12345/preserved";
            var second = CodecksSettingsSetup.GetOrCreateDefaultAsset();
            Assert.That(second, Is.SameAs(first));
            Assert.That(second.reportToken, Is.EqualTo("existing-dummy"));
            Assert.That(second.endpoint, Is.EqualTo("http://127.0.0.1:12345/preserved"));
        }

        [Test]
        public void SetupActionRefusesOccupiedWrongTypePath()
        {
            CreateDefault();
            AssetDatabase.DeleteAsset(CodecksSettingsSetup.DefaultAssetPath);
            var other = new TextAsset("dummy occupied path");
            AssetDatabase.CreateAsset(other, CodecksSettingsSetup.DefaultAssetPath);
            Assert.Throws<InvalidOperationException>(() => CodecksSettingsSetup.GetOrCreateDefaultAsset());
            Assert.That(AssetDatabase.LoadMainAssetAtPath(CodecksSettingsSetup.DefaultAssetPath), Is.SameAs(other));
        }

        [Test]
        public void BootstrapWiresClosedSceneAndIsIdempotent()
        {
            var shared = CreateDefault();
            shared.reportToken = "bootstrap-dummy";
            CreateClosedScene();
            Assert.That(CodecksSettingsSetup.WireClosedSampleScene(TestScene, shared), Is.EqualTo(CodecksSettingsSetup.WiringResult.Updated));
            byte[] after = File.ReadAllBytes(TestScene);
            Assert.That(CodecksSettingsSetup.WireClosedSampleScene(TestScene, shared), Is.EqualTo(CodecksSettingsSetup.WiringResult.AlreadyConfigured));
            Assert.That(File.ReadAllBytes(TestScene), Is.EqualTo(after));
            var scene = EditorSceneManager.OpenScene(TestScene, OpenSceneMode.Additive);
            Assert.That(scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<CodecksCardCreator>(true)).Single().settings, Is.SameAs(shared));
            Assert.That(shared.reportToken, Is.EqualTo("bootstrap-dummy"));
        }

        [Test]
        public void BootstrapSkipsOpenSceneWithoutChangingBytesOrDirtyState()
        {
            var shared = CreateDefault();
            CreateClosedScene();
            var scene = EditorSceneManager.OpenScene(TestScene, OpenSceneMode.Additive);
            byte[] before = File.ReadAllBytes(TestScene);
            EditorSceneManager.MarkSceneDirty(scene);
            Assert.That(CodecksSettingsSetup.WireClosedSampleScene(TestScene, shared), Is.EqualTo(CodecksSettingsSetup.WiringResult.OpenScene));
            Assert.That(scene.isDirty, Is.True);
            Assert.That(scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<CodecksCardCreator>(true)).Single().settings, Is.Null);
            Assert.That(File.ReadAllBytes(TestScene), Is.EqualTo(before));
        }

        [Test]
        public void BootstrapPreservesExistingExplicitReference()
        {
            var shared = CreateDefault();
            Assert.That(AssetDatabase.IsValidFolder(TestFolder), Is.False);
            AssetDatabase.CreateFolder("Assets/Samples", "CodecksSetupTests");
            createdTestFolder = true;
            var custom = ScriptableObject.CreateInstance<CodecksSettings>();
            AssetDatabase.CreateAsset(custom, TestFolder + "/CustomSettings.asset");
            CreateClosedScene(custom, false);
            Assert.That(CodecksSettingsSetup.WireClosedSampleScene(TestScene, shared), Is.EqualTo(CodecksSettingsSetup.WiringResult.AlreadyConfigured));
            var scene = EditorSceneManager.OpenScene(TestScene, OpenSceneMode.Additive);
            Assert.That(scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<CodecksCardCreator>(true)).Single().settings, Is.SameAs(custom));
        }

        [Test]
        public void BootstrapRefusesNonSampleSceneScope()
        {
            var shared = CreateDefault();
            Assert.Throws<InvalidOperationException>(() => CodecksSettingsSetup.WireClosedSampleScene("Assets/Retest/Consumer.unity", shared));
        }

        private CodecksSettings CreateDefault()
        {
            Assert.That(File.Exists(CodecksSettingsSetup.DefaultAssetPath), Is.False,
                "Use a disposable host; never overwrite or read a real settings asset.");
            var asset = CodecksSettingsSetup.GetOrCreateDefaultAsset();
            createdDefaultAsset = true;
            return asset;
        }

        private void CreateClosedScene(CodecksSettings settings = null, bool createFolder = true)
        {
            if (createFolder)
            {
                Assert.That(AssetDatabase.IsValidFolder(TestFolder), Is.False);
                if (!AssetDatabase.IsValidFolder("Assets/Samples")) AssetDatabase.CreateFolder("Assets", "Samples");
                AssetDatabase.CreateFolder("Assets/Samples", "CodecksSetupTests");
                createdTestFolder = true;
            }
            string source = AssetDatabase.FindAssets("CodecksUIToolkitFeedbackReporterScene t:Scene")
                .Select(AssetDatabase.GUIDToAssetPath).Single(p => p.StartsWith("Assets/Samples/") && p != TestScene);
            Assert.That(AssetDatabase.CopyAsset(source, TestScene), Is.True);
            if (settings == null) return;
            var previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.OpenScene(TestScene, OpenSceneMode.Additive);
            foreach (var item in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<CodecksCardCreator>(true)))
                item.settings = settings;
            EditorSceneManager.SaveScene(scene, TestScene);
            EditorSceneManager.CloseScene(scene, true);
            SceneManager.SetActiveScene(previous);
        }

        private void AssertConfigurationFailure(string prefix)
        {
            var original = CodecksCardCreator.PostRequestFactory;
            int requests = 0, callbacks = 0;
            try
            {
                CodecksCardCreator.PostRequestFactory = (_, _) => { requests++; return null; };
                creator.CreateNewCard("dummy report", CodecksCardCreator.CodecksSeverity.None, resultDelegate: (success, result) =>
                {
                    callbacks++;
                    Assert.That(success, Is.False);
                    Assert.That(result, Does.StartWith(prefix));
                });
                Assert.That(requests, Is.Zero);
                Assert.That(callbacks, Is.EqualTo(1));
                var operations = typeof(CodecksCardCreator).GetField("activeOperations", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(creator);
                Assert.That((int)operations.GetType().GetProperty("Count").GetValue(operations), Is.Zero);
            }
            finally { CodecksCardCreator.PostRequestFactory = original; }
        }

        private static void AssertConfiguration(CodecksCardCreator target, string token, string endpoint)
        {
            Assert.That(target.TryResolveConfiguration(out string actualToken, out string actualEndpoint, out string error), Is.True, error);
            Assert.That(actualToken, Is.EqualTo(token));
            Assert.That(actualEndpoint, Is.EqualTo(endpoint));
        }
    }
}
