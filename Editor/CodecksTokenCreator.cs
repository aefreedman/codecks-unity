using Newtonsoft.Json;
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace Codecks.Editor
{
    [Serializable]
    public struct TokenRequestData
    {
        [SerializeField] public string label;
    }

    [Serializable]
    internal struct TokenResponseData
    {
        [SerializeField] public bool ok;
        [SerializeField] public string token;
    }

    public class CodecksTokenCreator
    {
        const int RequestTimeoutSeconds = 30;

        internal static string BuildTokenUrl(string accessKey)
        {
            return "https://api.codecks.io/user-report/v1/create-report-token?accessKey=" +
                Uri.EscapeDataString(accessKey);
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

        public static void CreateAndSetNewToken(string accessKey, string tokenLabel, Action<bool> callback)
        {
            void HandleTokenResult(string token)
            {
                if (string.IsNullOrEmpty(token))
                {
                    callback?.Invoke(false);
                    return;
                }

                bool result = false;
                try
                {
                    var resourcePath = Path.Combine(Application.dataPath, "Resources", "Codecks");
                    if (!Directory.Exists(resourcePath))
                        Directory.CreateDirectory(resourcePath);

                    File.WriteAllText(Path.Combine(resourcePath, "codecksToken.txt"), token);
                    AssetDatabase.Refresh();
                    result = true;
                }
                catch (Exception ex)
                {
                    Debug.LogException(new Exception("Error saving codecks token", ex));
                }

                callback?.Invoke(result);
            }

            try
            {
                CreateNewToken(accessKey, tokenLabel, HandleTokenResult);
            }
            catch (Exception ex)
            {
                Debug.LogException(new Exception("Error creating codecks token", ex));
                callback?.Invoke(false);
            }
        }

        public static void CreateNewToken(string accessKey, string tokenLabel, Action<string> callback)
        {
            UnityWebRequest request = null;
            bool completed = false;

            void Complete(string token)
            {
                if (completed)
                    return;

                completed = true;
                callback?.Invoke(token);
            }

            try
            {
                string requestJson = JsonConvert.SerializeObject(new TokenRequestData { label = tokenLabel });
                request = HttpPost(BuildTokenUrl(accessKey), requestJson);
                UnityWebRequestAsyncOperation operation = request.SendWebRequest();
                operation.completed += _ =>
                {
                    try
                    {
                        if (request.result != UnityWebRequest.Result.Success)
                        {
                            Debug.LogWarning($"Codecks token request failed: {request.result} {request.error}");
                            Complete(null);
                            return;
                        }

                        TokenResponseData response = JsonConvert.DeserializeObject<TokenResponseData>(request.downloadHandler.text);
                        if (!response.ok || string.IsNullOrEmpty(response.token))
                        {
                            Debug.LogWarning("Codecks token request was rejected or returned no token.");
                            Complete(null);
                            return;
                        }

                        Complete(response.token);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogException(new Exception("Error reading Codecks token response", ex));
                        Complete(null);
                    }
                    finally
                    {
                        request.Dispose();
                    }
                };
            }
            catch (Exception ex)
            {
                request?.Dispose();
                Debug.LogException(new Exception("Error starting Codecks token request", ex));
                Complete(null);
            }
        }
    }
}
