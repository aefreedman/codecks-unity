using System.Collections;
using System.Collections.Generic;
using Codecks.Editor;
using Codecks.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

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
            var response = new CardCreateResponseData { ok = true, uploadUrls = null };

            bool success = CodecksCardCreator.TryValidateCreateResponse(
                response, new List<string>(), out var uploads, out string error);

            Assert.That(success, Is.True);
            Assert.That(uploads, Is.Empty);
            Assert.That(error, Is.Null);
        }

        [Test]
        public void TryValidateCreateResponse_RejectsMissingUploadInstruction()
        {
            var response = new CardCreateResponseData { ok = true, uploadUrls = new CardCreateFileResponseData[0] };

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
            var response = new CardCreateResponseData { ok = true, uploadUrls = new[] { upload, upload } };

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

        [UnityTest]
        public IEnumerator CreateNewCard_EmptyTokenCompletesCallbackOnce()
        {
            var gameObject = new GameObject("Codecks reliability test");
            var creator = gameObject.AddComponent<CodecksCardCreator>();
            int callbackCount = 0;
            bool success = true;

            creator.CreateNewCard("report", CodecksCardCreator.CodecksSeverity.None, null, (wasSuccessful, _) =>
            {
                callbackCount++;
                success = wasSuccessful;
            });

            yield return null;

            Assert.That(callbackCount, Is.EqualTo(1));
            Assert.That(success, Is.False);
            Object.DestroyImmediate(gameObject);
        }

        [Test]
        public void TryValidateCreateResponse_RejectsUnexpectedUploadInstruction()
        {
            var response = new CardCreateResponseData
            {
                ok = true,
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
    }
}
