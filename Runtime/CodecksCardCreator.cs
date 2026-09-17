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

        static UnityWebRequest HttpPost(string url, string bodyJsonString)
        {
            var request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST)
            {
                uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(bodyJsonString)),
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = RequestTimeoutSeconds
            };
            request.SetRequestHeader("Content-Type", "application/json");
            return request;
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
                    string.IsNullOrEmpty(uploadResponse.url) ||
                    uploadResponse.fields == null)
                {
                    error = "Codecks returned invalid upload instructions.";
                    return false;
                }
            }

            error = null;
            return true;
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

        public void CreateNewCard(string text, Dictionary<string, (byte[], CodecksFileType)> files = null,
            CodecksSeverity severity = CodecksSeverity.None, string userEmail = null,
            CardCreationResultDelegate resultDelegate = null)
        {
            if (files != null && files.Any(file => file.Value.Item1 == null))
                throw new Exception("Null file in files list");

            StartCoroutine(CreateNewCardCoroutine(text, files, severity, userEmail, resultDelegate));
        }

        public void CreateNewCard(string text, CodecksSeverity severity = CodecksSeverity.None,
            string userEmail = null, CardCreationResultDelegate resultDelegate = null)
        {
            StartCoroutine(CreateNewCardCoroutine(text, null, severity, userEmail, resultDelegate));
        }

        IEnumerator CreateNewCardCoroutine(string text, Dictionary<string, (byte[], CodecksFileType)> files = null,
            CodecksSeverity severity = CodecksSeverity.None, string userEmail = null,
            CardCreationResultDelegate resultDelegate = null)
        {
            string tokenToUse = string.IsNullOrEmpty(loadedToken) ? defaultToken : loadedToken;
            if (string.IsNullOrEmpty(tokenToUse))
            {
                resultDelegate?.Invoke(false, "empty codecks token");
                yield break;
            }

            files ??= new Dictionary<string, (byte[], CodecksFileType)>();

            UnityWebRequest request;
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
                request = HttpPost(BuildCreateReportUrl(codecksURL, tokenToUse), json);
            }
            catch (Exception ex)
            {
                resultDelegate?.Invoke(false, $"exception sending initial request: {ex.Message}");
                yield break;
            }

            string responseText;
            using (request)
            {
                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    resultDelegate?.Invoke(false, $"request unsuccessful: {request.result} {request.error}");
                    yield break;
                }

                responseText = request.downloadHandler.text;
            }

            CardCreateResponseData response;
            try
            {
                response = JsonConvert.DeserializeObject<CardCreateResponseData>(responseText);
            }
            catch (Exception ex)
            {
                resultDelegate?.Invoke(false, $"exception deserializing response: {ex.Message}");
                yield break;
            }

            if (!TryValidateCreateResponse(response, files.Keys, out var uploadResponses, out string validationError))
            {
                resultDelegate?.Invoke(false, validationError);
                yield break;
            }

            foreach (var uploadResponse in uploadResponses)
            {
                var formData = new List<IMultipartFormSection>();
                foreach (var field in uploadResponse.fields)
                    formData.Add(new MultipartFormDataSection(field.Key, field.Value));

                var fileData = files[uploadResponse.fileName];
                string contentType = GetContentType(fileData.Item2);
                formData.Add(new MultipartFormDataSection("Content-Type", contentType));
                formData.Add(new MultipartFormFileSection("file", fileData.Item1, uploadResponse.fileName, contentType));

                using (UnityWebRequest uploadRequest = UnityWebRequest.Post(uploadResponse.url, formData))
                {
                    uploadRequest.timeout = RequestTimeoutSeconds;
                    yield return uploadRequest.SendWebRequest();

                    if (uploadRequest.result != UnityWebRequest.Result.Success)
                    {
                        resultDelegate?.Invoke(false,
                            $"error uploading file {uploadResponse.fileName}: {uploadRequest.result} {uploadRequest.error}");
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
