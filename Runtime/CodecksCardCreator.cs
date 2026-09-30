using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace Codecks.Runtime
{
    [Serializable]
    public struct CardCreateRequestData
    {
        [SerializeField] public string content;
        [SerializeField] public List<string> fileNames;
        [SerializeField] public string severity;
        [SerializeField] public string userEmail;
    }

    [Serializable]
    public struct CardCreateFileResponseData
    {
        [SerializeField] public string fileName;
        [SerializeField] public string url;
        [SerializeField] public Dictionary<string, string> fields;
    }

    [Serializable]
    internal struct CardCreateResponseData
    {
        [SerializeField] public bool ok;
        [SerializeField] public string cardId;
        [SerializeField] public CardCreateFileResponseData[] uploadUrls;
    }

    public class CodecksCardCreator : MonoBehaviour
    {
        internal const int RequestTimeoutSeconds = 30;

        void IL2CPPCompatibility()
        {
            // To generate proper IL2CPP code, generics must be called somewhere.
            var dummy = new List<CardCreateFileResponseData>();

            throw new Exception("Never call this!");
        }

        // Retained only to avoid breaking old scripts/serialized scenes. Never used for requests.
        [HideInInspector, Obsolete("Assign settings instead. This legacy endpoint is ignored.")]
        public string codecksURL = CodecksSettings.DefaultEndpoint;
        [HideInInspector, Obsolete("Configure settings.reportToken instead. This legacy token is ignored.")]
        public string defaultToken;

        [Tooltip("Required report settings. Use Tools > Codecks > Set Up Imported Samples to wire imported sample scenes.")]
        public CodecksSettings settings;
        private readonly HashSet<CardCreationOperation> activeOperations = new HashSet<CardCreationOperation>();

        public delegate void CardCreationResultDelegate(bool success, string result);

        private void OnDestroy()
        {
            foreach (var operation in activeOperations.ToArray())
                operation.Complete(false, "Codecks report request was cancelled because its creator was destroyed.");
        }

        internal static string BuildCreateReportUrl(string endpoint, string token)
        {
            return endpoint + "?token=" + Uri.EscapeDataString(token);
        }

        internal static Func<string, string, UnityWebRequest> PostRequestFactory = HttpPost;

        static UnityWebRequest HttpPost(string url, string bodyJsonString)
        {
            var request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
            try
            {
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(bodyJsonString));
                request.downloadHandler = new DownloadHandlerBuffer();
                request.timeout = RequestTimeoutSeconds;
                request.SetRequestHeader("Content-Type", "application/json");
                return request;
            }
            catch
            {
                request.Dispose();
                throw;
            }
        }

        internal static bool TryValidateCreateResponse(
            CardCreateResponseData response,
            ICollection<string> requestedFileNames,
            out CardCreateFileResponseData[] uploadResponses,
            out string error)
        {
            uploadResponses = response.uploadUrls ?? Array.Empty<CardCreateFileResponseData>();
            if (!response.ok)
            {
                error = "Codecks rejected the report request.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(response.cardId))
            {
                error = "Codecks returned an invalid report response.";
                return false;
            }

            if (requestedFileNames.Count == 0)
            {
                if (uploadResponses.Length != 0)
                {
                    error = "Codecks returned upload instructions when no attachments were requested.";
                    return false;
                }

                error = null;
                return true;
            }

            if (uploadResponses.Length != requestedFileNames.Count)
            {
                error = "Codecks returned an incomplete set of upload instructions.";
                return false;
            }

            var seenFileNames = new HashSet<string>();
            foreach (var uploadResponse in uploadResponses)
            {
                if (string.IsNullOrEmpty(uploadResponse.fileName) ||
                    !requestedFileNames.Contains(uploadResponse.fileName) ||
                    !seenFileNames.Add(uploadResponse.fileName) ||
                    !IsValidUploadInstruction(uploadResponse))
                {
                    error = "Codecks returned invalid upload instructions.";
                    return false;
                }
            }

            error = null;
            return true;
        }

        internal static bool TryDeserializeCreateResponse(string responseText, out CardCreateResponseData response,
            out string error)
        {
            try
            {
                response = JsonConvert.DeserializeObject<CardCreateResponseData>(responseText);
                error = null;
                return true;
            }
            catch (Exception)
            {
                response = default;
                error = "Codecks returned an invalid report response.";
                return false;
            }
        }

        static bool IsValidUploadInstruction(CardCreateFileResponseData uploadResponse)
        {
            if (!Uri.TryCreate(uploadResponse.url, UriKind.Absolute, out Uri uploadUri) ||
                (uploadUri.Scheme != Uri.UriSchemeHttp && uploadUri.Scheme != Uri.UriSchemeHttps) ||
                uploadResponse.fields == null)
                return false;

            return uploadResponse.fields.All(field => !string.IsNullOrEmpty(field.Key) && field.Value != null);
        }

        public enum CodecksFileType
        {
            Binary,
            PlainText,
            JSON,
            PNG,
            JPG
        }

        public enum CodecksSeverity
        {
            None,
            Low,
            High,
            Critical
        }

        /// <summary>
        /// Sends a report and optional attachments to Codecks.
        /// </summary>
        /// <param name="text">The text that will appear on the card.</param>
        /// <param name="files">Files to attach to the report, or null for no attachments.</param>
        /// <param name="severity">The optional report severity.</param>
        /// <param name="userEmail">The optional email address for report updates.</param>
        /// <param name="resultDelegate">Receives success and either the card id or a safe diagnostic message.</param>
        public void CreateNewCard(string text, Dictionary<string, (byte[], CodecksFileType)> files = null,
            CodecksSeverity severity = CodecksSeverity.None, string userEmail = null,
            CardCreationResultDelegate resultDelegate = null)
        {
            if (files != null && files.Any(file => file.Value.Item1 == null))
                throw new Exception("Null file in files list");

            StartNewCardRequest(text, files, severity, userEmail, resultDelegate);
        }

        public void CreateNewCard(string text, CodecksSeverity severity = CodecksSeverity.None,
            string userEmail = null, CardCreationResultDelegate resultDelegate = null)
        {
            StartNewCardRequest(text, null, severity, userEmail, resultDelegate);
        }

        internal bool TryResolveConfiguration(out string token, out string endpoint, out string error)
        {
            token = null;
            endpoint = null;
            if (settings == null)
            {
                error = "Codecks report settings are required. Assign a CodecksSettings asset or run Tools > Codecks > Set Up Imported Samples.";
                return false;
            }
            token = settings.reportToken;
            endpoint = settings.endpoint;
            if (string.IsNullOrWhiteSpace(token))
            {
                error = "Codecks settings contain an empty report token.";
                return false;
            }
            if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) ||
                string.IsNullOrEmpty(uri.Host) || !string.IsNullOrEmpty(uri.UserInfo) ||
                !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
            {
                error = "Codecks settings contain an invalid create-report endpoint; use an absolute HTTP(S) URL without credentials, query or fragment.";
                return false;
            }
            error = null;
            return true;
        }

        void StartNewCardRequest(string text, Dictionary<string, (byte[], CodecksFileType)> files,
            CodecksSeverity severity, string userEmail, CardCreationResultDelegate resultDelegate)
        {
            var operation = new CardCreationOperation(this, resultDelegate);
            activeOperations.Add(operation);

            if (!TryResolveConfiguration(out string tokenToUse, out string endpoint, out string configurationError))
            {
                operation.Complete(false, configurationError);
                return;
            }

            files ??= new Dictionary<string, (byte[], CodecksFileType)>();

            UnityWebRequest request = null;
            try
            {
                string severityString = severity switch
                {
                    CodecksSeverity.Low => "low",
                    CodecksSeverity.High => "high",
                    CodecksSeverity.Critical => "critical",
                    _ => null
                };

                var cardData = new CardCreateRequestData
                {
                    content = text,
                    fileNames = files.Keys.ToList(),
                    severity = severityString,
                    userEmail = userEmail
                };

                string json = JsonConvert.SerializeObject(cardData).Replace(",\"severity\":null", "");
                request = PostRequestFactory(BuildCreateReportUrl(endpoint, tokenToUse), json);
                if (request == null)
                    throw new InvalidOperationException("Codecks request factory returned no request.");
                operation.SetRequest(request);
            }
            catch (Exception)
            {
                request?.Dispose();
                operation.Complete(false, "could not prepare the Codecks report request.");
                return;
            }

            try
            {
                StartCoroutine(CreateNewCardCoroutine(operation, request, files));
            }
            catch (Exception)
            {
                operation.Complete(false, "could not start the Codecks report request.");
            }
        }

        IEnumerator CreateNewCardCoroutine(CardCreationOperation operation, UnityWebRequest request,
            Dictionary<string, (byte[], CodecksFileType)> files)
        {
            UnityWebRequestAsyncOperation requestOperation;
            try
            {
                requestOperation = request.SendWebRequest();
            }
            catch (Exception)
            {
                operation.Complete(false, "could not start the Codecks report request.");
                yield break;
            }

            yield return requestOperation;
            if (operation.IsCompleted)
                yield break;

            if (request.result != UnityWebRequest.Result.Success)
            {
                operation.Complete(false, $"request unsuccessful: {request.result}");
                yield break;
            }

            string responseText = request.downloadHandler.text;
            operation.DisposeRequest(request);

            if (!TryDeserializeCreateResponse(responseText, out CardCreateResponseData response, out string responseError))
            {
                operation.Complete(false, responseError);
                yield break;
            }

            if (!TryValidateCreateResponse(response, files.Keys, out var uploadResponses, out string validationError))
            {
                operation.Complete(false, validationError);
                yield break;
            }

            foreach (var uploadResponse in uploadResponses)
            {
                UnityWebRequest uploadRequest = null;
                try
                {
                    var formData = new List<IMultipartFormSection>();
                    foreach (var field in uploadResponse.fields)
                        formData.Add(new MultipartFormDataSection(field.Key, field.Value));

                    var fileData = files[uploadResponse.fileName];
                    string contentType = GetContentType(fileData.Item2);
                    formData.Add(new MultipartFormDataSection("Content-Type", contentType));
                    formData.Add(new MultipartFormFileSection("file", fileData.Item1, uploadResponse.fileName, contentType));
                    uploadRequest = UnityWebRequest.Post(uploadResponse.url, formData);
                    uploadRequest.timeout = RequestTimeoutSeconds;
                    operation.SetRequest(uploadRequest);
                }
                catch (Exception)
                {
                    uploadRequest?.Dispose();
                    operation.Complete(false, "could not prepare a Codecks attachment upload.");
                    yield break;
                }

                UnityWebRequestAsyncOperation uploadOperation;
                try
                {
                    uploadOperation = uploadRequest.SendWebRequest();
                }
                catch (Exception)
                {
                    operation.Complete(false, "could not start a Codecks attachment upload.");
                    yield break;
                }

                yield return uploadOperation;
                if (operation.IsCompleted)
                    yield break;

                if (uploadRequest.result != UnityWebRequest.Result.Success)
                {
                    operation.Complete(false,
                        $"error uploading file {uploadResponse.fileName}: {uploadRequest.result}");
                    yield break;
                }

                operation.DisposeRequest(uploadRequest);
            }

            operation.Complete(true, response.cardId);
        }

        private sealed class CardCreationOperation
        {
            private readonly CodecksCardCreator owner;
            private readonly CardCreationResultDelegate resultDelegate;
            private UnityWebRequest request;

            public CardCreationOperation(CodecksCardCreator owner, CardCreationResultDelegate resultDelegate)
            {
                this.owner = owner;
                this.resultDelegate = resultDelegate;
            }

            public bool IsCompleted { get; private set; }

            public void SetRequest(UnityWebRequest newRequest)
            {
                request = newRequest;
            }

            public void DisposeRequest(UnityWebRequest completedRequest)
            {
                if (request != completedRequest)
                    return;

                request.Dispose();
                request = null;
            }

            public void Complete(bool success, string result)
            {
                if (IsCompleted)
                    return;

                IsCompleted = true;
                request?.Dispose();
                request = null;
                owner.activeOperations.Remove(this);
                try
                {
                    resultDelegate?.Invoke(success, result);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception, owner);
                }
            }
        }

        static string GetContentType(CodecksFileType fileType)
        {
            return fileType switch
            {
                CodecksFileType.PlainText => "text/plain",
                CodecksFileType.JSON => "application/json",
                CodecksFileType.PNG => "image/png",
                CodecksFileType.JPG => "image/jpeg",
                _ => "application/octet-stream"
            };
        }
    }
}
