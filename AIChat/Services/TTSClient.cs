using AIChat.Core;
using BepInEx.Logging;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace AIChat.Services
{
    public static class TTSClient
    {
        public static IEnumerator DownloadVoiceWithRetry(
            string url,
            string textToSpeak,
            string targetLang,
            string refPath,
            string promptText,
            string promptLang,
            ManualLogSource logger,
            Action<AudioClip> onComplete, 
            int maxRetries = 3, 
            float timeoutSeconds = 30f)
        {
            logger.LogInfo("[TTS] 开始生成语音...");



            if (!File.Exists(refPath))
            {
                string defaultPath = Path.Combine(BepInEx.Paths.PluginPath, "ChillAIMod", "Voice.wav");
                if (File.Exists(defaultPath)) refPath = defaultPath;
                else
                {
                    logger.LogError($"[TTS] 找不到参考音频: {refPath}");
                    onComplete?.Invoke(null);
                    yield break;
                }
            }

            string jsonBody = $@"{{ 
                ""text"": ""{ResponseParser.EscapeJson(textToSpeak)}"", 
                ""text_lang"": ""{targetLang}"", 
                ""ref_audio_path"": ""{ResponseParser.EscapeJson(refPath)}"", 
                ""prompt_text"": ""{ResponseParser.EscapeJson(promptText)}"", 
                ""prompt_lang"": ""{promptLang}"" 
            }}";

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
                {
                    byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
                    request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                    request.downloadHandler = new DownloadHandlerAudioClip(url, AudioType.WAV);
                    request.SetRequestHeader("Content-Type", "application/json");
                    request.timeout = (int)timeoutSeconds;

                    var requestStartTime = DateTime.UtcNow;

                    yield return request.SendWebRequest();

                    var requestDuration = (DateTime.UtcNow - requestStartTime).TotalSeconds;

                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        var clip = DownloadHandlerAudioClip.GetContent(request);
                        if (clip != null)
                        {
                            logger.LogInfo($"[TTS] 语音生成成功（第 {attempt} 次尝试）（耗时 {requestDuration:F2}s）");
                            onComplete?.Invoke(clip);
                            yield break; // 成功则退出
                        }
                    }

                    logger.LogWarning($"[TTS] 第 {attempt}/{maxRetries} 次尝试失败（耗时 {requestDuration:F2}s）: {request.error}");
                    if (attempt < maxRetries)
                    {
                        yield return new WaitForSeconds(2f); // 重试前等待
                    }
                }
            }

            logger.LogError("[TTS] 所有重试均失败，放弃生成语音");
            onComplete?.Invoke(null);
        }
        public static IEnumerator CheckTTSHealthOnce(string baseUrl, ManualLogSource logger, Action<bool> onResult)
        {
            string ttsUrl = baseUrl.TrimEnd('/') + "/tts";
            string minimalJson = @"{""text"": ""test""}";
            using (UnityWebRequest req = new UnityWebRequest(ttsUrl, "POST")) // 没有/ping能够检测服务是否启动，只能利用/tts发一个小包观测失败返回码
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(minimalJson);
                req.uploadHandler = new UploadHandlerRaw(bodyRaw);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.timeout = 8;

                yield return req.SendWebRequest();

                bool isReady = false;
                if (req.result == UnityWebRequest.Result.Success)
                {
                    isReady = true;
                }
                else if (req.responseCode == 422 || req.responseCode == 400) // 在我的电脑上返回400 bad request.
                {
                    isReady = true;
                }
                else
                {
                    // 404, 500, ConnectionError, Timeout 等 → 服务未就绪
                    isReady = false;
                }

                if (isReady)
                {
                    logger.LogDebug("[TTS Health] 检测到服务已启动 (返回 422/200 等)");
                }
                else
                {
                    string error = req.error ?? $"HTTP {req.responseCode}";
                    logger.LogDebug($"[TTS Health] 服务未就绪: {error}");
                }

                onResult?.Invoke(isReady);
            }
        }
    }
}
