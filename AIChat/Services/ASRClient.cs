using AIChat.Core;
using AIChat.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace AIChat.Services
{
    public static  class ASRClient
    {
        public static IEnumerator SendAudioToASR(byte[] wavData , string baseUrl, Action<string> onResult)
        {
            string url = baseUrl.TrimEnd('/') + "/asr";
            WWWForm form = new WWWForm();
            form.AddBinaryData("file", wavData, "voice.wav", "audio/wav");
            using (UnityWebRequest www = UnityWebRequest.Post(url, form))

            {
                yield return www.SendWebRequest();
                if (www.result == UnityWebRequest.Result.Success)

                {
                    string json = www.downloadHandler.text;
                    Log.Info($"[ASR] 服务器返回: {json}");
                    // 简单的 JSON 解析: {"text": "你好"}
                    string recognizedText = ResponseParser.ExtractJsonValue(json, "text");
                    // 将结果通过回调抛出
                    onResult?.Invoke(recognizedText);
                }
                else

                {
                    Log.Error($"[ASR] 请求失败: {www.error}");
                    onResult?.Invoke(null);
                }
            }
        }
    }
}
