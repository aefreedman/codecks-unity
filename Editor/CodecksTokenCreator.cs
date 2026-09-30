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

        internal static bool TryDeserializeTokenResponse(string responseText, out TokenResponseData response)
        {
            try
            {
                response = JsonConvert.DeserializeObject<TokenResponseData>(responseText);
                return response.ok && !string.IsNullOrEmpty(response.token);
            }
            catch (Exception)
            {
                response = default;
                return false;
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
                request = PostRequestFactory(BuildTokenUrl(accessKey), requestJson);
                if (request == null)
                    throw new InvalidOperationException("Codecks request factory returned no request.");

                UnityWebRequestAsyncOperation operation = request.SendWebRequest();
                operation.completed += _ =>
                {
                    try
                    {
                        if (request.result != UnityWebRequest.Result.Success)
                        {
                            Debug.LogWarning($"Codecks token request failed: {request.result}.");
                            Complete(null);
                            return;
                        }

                        if (!TryDeserializeTokenResponse(request.downloadHandler.text, out TokenResponseData response))
                        {
                            Debug.LogWarning("Codecks token request was rejected or returned no token.");
                            Complete(null);
                            return;
                        }

                        Complete(response.token);
                    }
                    catch (Exception)
                    {
                        Debug.LogWarning("Codecks token request returned an invalid response.");
                        Complete(null);
                    }
                    finally
                    {
                        request.Dispose();
                    }
                };
            }
            catch (Exception)
            {
                request?.Dispose();
                Debug.LogWarning("Could not start the Codecks token request.");
                Complete(null);
            }
        }
    }
}
