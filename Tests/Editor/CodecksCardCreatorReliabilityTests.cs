using System;
using System.Collections.Generic;
using System.Reflection;
using Codecks.Editor;
using Codecks.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace Codecks.Tests.Editor
{
    public class CodecksCardCreatorReliabilityTests
    {
        [Test]
        public void BuildCreateReportUrl_EscapesToken()
        {
            string url = CodecksCardCreator.BuildCreateReportUrl("https://example.invalid/report", "token +&?=");

            Assert.That(url, Is.EqualTo("https://example.invalid/report?token=token%20%2B%26%3F%3D"));
        }

        [Test]
        public void BuildTokenUrl_EscapesAccessKey()
        {
            string url = CodecksTokenCreator.BuildTokenUrl("key +&?=");

            Assert.That(url, Does.EndWith("accessKey=key%20%2B%26%3F%3D"));
        }

        [Test]
        public void TryValidateCreateResponse_RejectsUnsuccessfulResponse()
        {
            var response = new CardCreateResponseData { ok = false };

            bool success = CodecksCardCreator.TryValidateCreateResponse(
                response, new List<string>(), out _, out string error);

            Assert.That(success, Is.False);
            Assert.That(error, Is.EqualTo("Codecks rejected the report request."));
        }

        [Test]
        public void TryDeserializeCreateResponse_RejectsMalformedJson()
        {
            bool success = CodecksCardCreator.TryDeserializeCreateResponse(
                "{not json", out _, out string error);

            Assert.That(success, Is.False);
            Assert.That(error, Is.EqualTo("Codecks returned an invalid report response."));
        }

        [Test]
        public void TryValidateCreateResponse_AcceptsNoAttachmentsWithoutUploadUrls()
        {
            var response = new CardCreateResponseData { ok = true, cardId = "card-123", uploadUrls = null };

            bool success = CodecksCardCreator.TryValidateCreateResponse(
                response, new List<string>(), out var uploads, out string error);

            Assert.That(success, Is.True);
            Assert.That(uploads, Is.Empty);
            Assert.That(error, Is.Null);
        }

        [Test]
        public void TryValidateCreateResponse_RejectsSuccessfulResponseWithoutCardId()
        {
            var response = new CardCreateResponseData { ok = true, uploadUrls = Array.Empty<CardCreateFileResponseData>() };

            bool success = CodecksCardCreator.TryValidateCreateResponse(
                response, new List<string>(), out _, out string error);

            Assert.That(success, Is.False);
            Assert.That(error, Is.EqualTo("Codecks returned an invalid report response."));
        }

        [Test]
        public void TryValidateCreateResponse_RejectsMissingUploadInstruction()
        {
            var response = new CardCreateResponseData
            {
                ok = true,
                cardId = "card-123",
                uploadUrls = Array.Empty<CardCreateFileResponseData>()
            };

            bool success = CodecksCardCreator.TryValidateCreateResponse(
                response, new List<string> { "expected.png" }, out _, out string error);

            Assert.That(success, Is.False);
            Assert.That(error, Is.EqualTo("Codecks returned an incomplete set of upload instructions."));
        }

        [Test]
        public void TryValidateCreateResponse_RejectsDuplicateUploadInstruction()
        {
            var upload = new CardCreateFileResponseData
            {
                fileName = "expected.png",
                url = "https://example.invalid/upload",
                fields = new Dictionary<string, string>()
            };
            var response = new CardCreateResponseData
            {
                ok = true,
                cardId = "card-123",
                uploadUrls = new[] { upload, upload }
            };

            bool success = CodecksCardCreator.TryValidateCreateResponse(
                response, new List<string> { "expected.png", "other.png" }, out _, out string error);

            Assert.That(success, Is.False);
            Assert.That(error, Is.EqualTo("Codecks returned invalid upload instructions."));
        }

        [Test]
        public void TryValidateCreateResponse_RejectsInvalidUploadUriOrField()
        {
            var response = new CardCreateResponseData
            {
                ok = true,
                cardId = "card-123",
                uploadUrls = new[]
                {
                    new CardCreateFileResponseData
                    {
                        fileName = "expected.png",
                        url = "not a uri",
                        fields = new Dictionary<string, string> { { "key", null } }
                    }
                }
            };

            bool success = CodecksCardCreator.TryValidateCreateResponse(
                response, new List<string> { "expected.png" }, out _, out string error);

            Assert.That(success, Is.False);
            Assert.That(error, Is.EqualTo("Codecks returned invalid upload instructions."));
        }

        [Test]
        public void Form_StaleSubmissionCallbacks_DoNotChangeNewSessionState()
        {
            var gameObject = new GameObject("Codecks form lifecycle test");
            try
            {
                var form = gameObject.AddComponent<CodecksCardCreatorForm>();
                SetPrivate(form, "session", 2);
                SetPrivate(form, "submissionInFlight", true);

                InvokePrivate(form, "HandleSubmissionResult", 1, true, "late success");
                InvokePrivate(form, "HandleSubmissionResult", 1, false, "late failure");

                Assert.That(GetPrivate<bool>(form, "submissionInFlight"), Is.True);
                Assert.That(GetPrivate<int>(form, "session"), Is.EqualTo(2));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void Form_OnDisable_InvalidatesPendingSubmission()
        {
            var gameObject = new GameObject("Codecks form lifecycle test");
            try
            {
                var form = gameObject.AddComponent<CodecksCardCreatorForm>();
                SetPrivate(form, "session", 4);
                SetPrivate(form, "submissionInFlight", true);

                InvokePrivate(form, "OnDisable");

                Assert.That(GetPrivate<int>(form, "session"), Is.EqualTo(5));
                Assert.That(GetPrivate<bool>(form, "submissionInFlight"), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void CreateNewCard_MissingSettingsCompletesCallbackOnce()
        {
            var gameObject = new GameObject("Codecks reliability test");
            try
            {
                var creator = gameObject.AddComponent<CodecksCardCreator>();
                int callbackCount = 0;
                bool success = true;

                creator.CreateNewCard("report", CodecksCardCreator.CodecksSeverity.None, null, (wasSuccessful, _) =>
                {
                    callbackCount++;
                    success = wasSuccessful;
                });

                Assert.That(callbackCount, Is.EqualTo(1));
                Assert.That(success, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void CreateNewCard_RequestConstructionFailureCompletesCallbackOnce()
        {
            var originalFactory = CodecksCardCreator.PostRequestFactory;
            var gameObject = new GameObject("Codecks reliability test");
            var settings = ScriptableObject.CreateInstance<CodecksSettings>();
            settings.reportToken = "dummy-token";
            try
            {
                CodecksCardCreator.PostRequestFactory = (_, _) => throw new InvalidOperationException("test failure");
                var creator = gameObject.AddComponent<CodecksCardCreator>();
                creator.settings = settings;
                int callbackCount = 0;
                string result = null;

                creator.CreateNewCard("report", CodecksCardCreator.CodecksSeverity.None, null, (success, message) =>
                {
                    Assert.That(success, Is.False);
                    callbackCount++;
                    result = message;
                });

                Assert.That(callbackCount, Is.EqualTo(1));
                Assert.That(result, Is.EqualTo("could not prepare the Codecks report request."));
            }
            finally
            {
                CodecksCardCreator.PostRequestFactory = originalFactory;
                UnityEngine.Object.DestroyImmediate(gameObject);
                UnityEngine.Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void TryValidateCreateResponse_RejectsUnexpectedUploadInstruction()
        {
            var response = new CardCreateResponseData
            {
                ok = true,
                cardId = "card-123",
                uploadUrls = new[]
                {
                    new CardCreateFileResponseData
                    {
                        fileName = "unexpected.png",
                        url = "https://example.invalid/upload",
                        fields = new Dictionary<string, string>()
                    }
                }
            };

            bool success = CodecksCardCreator.TryValidateCreateResponse(
                response, new List<string> { "expected.png" }, out _, out string error);

            Assert.That(success, Is.False);
            Assert.That(error, Is.EqualTo("Codecks returned invalid upload instructions."));
        }

        [Test]
        public void TryDeserializeTokenResponse_RejectsMalformedOrIncompleteResponse()
        {
            Assert.That(CodecksTokenCreator.TryDeserializeTokenResponse("{not json", out _), Is.False);
            Assert.That(CodecksTokenCreator.TryDeserializeTokenResponse("{\"ok\":true}", out _), Is.False);
        }

        private static void SetPrivate(object target, string fieldName, object value)
        {
            target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        }

        private static T GetPrivate<T>(object target, string fieldName)
        {
            return (T)target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
        }

        private static void InvokePrivate(object target, string methodName, params object[] arguments)
        {
            target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, arguments);
        }

        [Test]
        public void CreateNewToken_RequestStartupFailureCompletesCallbackOnce()
        {
            var originalFactory = CodecksTokenCreator.PostRequestFactory;
            try
            {
                CodecksTokenCreator.PostRequestFactory = (_, _) => throw new InvalidOperationException("test failure");
                int callbackCount = 0;
                string token = "unexpected";

                CodecksTokenCreator.CreateNewToken("key", "label", value =>
                {
                    callbackCount++;
                    token = value;
                });

                Assert.That(callbackCount, Is.EqualTo(1));
                Assert.That(token, Is.Null);
            }
            finally
            {
                CodecksTokenCreator.PostRequestFactory = originalFactory;
            }
        }
    }
}
