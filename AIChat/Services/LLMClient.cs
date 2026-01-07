using AIChat.Core;
using AIChat.Utils;
using System;
using System.Collections;
using System.Text;
using UnityEngine.Networking;

namespace AIChat.Services
{
    public static  class LLMClient
    {
        public static IEnumerator SendLLMRequest(LLMRequestContext requestContext, Action<string> onSuccess, Action<string, long> onFailure)
        {
            string jsonBody = LLMUtils.BuildRequestBody(requestContext);
            string apiUrl = LLMUtils.GetApiUrlForThinkMode(requestContext);

            using (UnityWebRequest request = new UnityWebRequest(apiUrl, "POST"))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                if (!requestContext.UseLocalOllama)
                {
                    request.SetRequestHeader("Authorization", "Bearer " + requestContext.ApiKey);
                }

                Log.Info($"[{requestContext.LogHeader}] 正在等待 LLM API 响应...");
                var startTime = DateTime.UtcNow;

                yield return request.SendWebRequest();

                Log.Info($"[{requestContext.LogHeader}] LLM 响应完成，耗时: {(DateTime.UtcNow - startTime).TotalSeconds} 秒");

                if (request.result == UnityWebRequest.Result.Success)
                {
                    string rawResponse = request.downloadHandler.text;
                    onSuccess(rawResponse);
                }
                else
                {
                    string errorMsg = $"{request.error}";
                    long responseCode = request.responseCode;
                    onFailure(errorMsg, responseCode);
                }
            }
        }
    }
}
