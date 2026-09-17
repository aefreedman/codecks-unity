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

        public string codecksURL = "https://api.codecks.io/user-report/v1/create-report";
        public string defaultToken;

        private string loadedToken;

        public delegate void CardCreationResultDelegate(bool success, string result);

        private void Start()
        {
            var loadedTokenFile = Resources.Load<TextAsset>("Codecks/codecksToken");
            if (loadedTokenFile != null)
                loadedToken = loadedTokenFile.text;
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

        void StartNewCardRequest(string text, Dictionary<string, (byte[], CodecksFileType)> files,
            CodecksSeverity severity, string userEmail, CardCreationResultDelegate resultDelegate)
        {
            string tokenToUse = string.IsNullOrEmpty(loadedToken) ? defaultToken : loadedToken;
            if (string.IsNullOrEmpty(tokenToUse))
            {
                resultDelegate?.Invoke(false, "empty codecks token");
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
                request = PostRequestFactory(BuildCreateReportUrl(codecksURL, tokenToUse), json);
                if (request == null)
                    throw new InvalidOperationException("Codecks request factory returned no request.");
            }
            catch (Exception)
            {
                request?.Dispose();
                resultDelegate?.Invoke(false, "could not prepare the Codecks report request.");
                return;
            }

            try
            {
                StartCoroutine(CreateNewCardCoroutine(request, files, resultDelegate));
            }
            catch (Exception)
            {
                request.Dispose();
                resultDelegate?.Invoke(false, "could not start the Codecks report request.");
            }
        }

        IEnumerator CreateNewCardCoroutine(UnityWebRequest request,
            Dictionary<string, (byte[], CodecksFileType)> files, CardCreationResultDelegate resultDelegate)
        {
            UnityWebRequestAsyncOperation requestOperation;
            try
            {
                requestOperation = request.SendWebRequest();
            }
            catch (Exception)
            {
                request.Dispose();
                resultDelegate?.Invoke(false, "could not start the Codecks report request.");
                yield break;
            }

            string responseText;
            using (request)
            {
                yield return requestOperation;

                if (request.result != UnityWebRequest.Result.Success)
                {
                    resultDelegate?.Invoke(false, $"request unsuccessful: {request.result}");
                    yield break;
                }

                responseText = request.downloadHandler.text;
            }

            if (!TryDeserializeCreateResponse(responseText, out CardCreateResponseData response, out string responseError))
            {
                resultDelegate?.Invoke(false, responseError);
                yield break;
            }

            if (!TryValidateCreateResponse(response, files.Keys, out var uploadResponses, out string validationError))
            {
                resultDelegate?.Invoke(false, validationError);
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
                }
                catch (Exception)
                {
                    uploadRequest?.Dispose();
                    resultDelegate?.Invoke(false, "could not prepare a Codecks attachment upload.");
                    yield break;
                }

                UnityWebRequestAsyncOperation uploadOperation;
                try
                {
                    uploadOperation = uploadRequest.SendWebRequest();
                }
                catch (Exception)
                {
                    uploadRequest.Dispose();
                    resultDelegate?.Invoke(false, "could not start a Codecks attachment upload.");
                    yield break;
                }

                using (uploadRequest)
                {
                    yield return uploadOperation;

                    if (uploadRequest.result != UnityWebRequest.Result.Success)
                    {
                        resultDelegate?.Invoke(false,
                            $"error uploading file {uploadResponse.fileName}: {uploadRequest.result}");
                        yield break;
                    }
                }
            }

            resultDelegate?.Invoke(true, response.cardId);
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
