using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;
using ChillAIMod;
using AIChat.Core;

namespace AIChat.Utils
{
    public enum ThinkMode { Default, Enable, Disable }

    public struct LLMRequestContext
    {
        public string ApiUrl;
        public string ApiKey;
        public string ModelName;
        public string SystemPrompt;
        public string UserPrompt;
        public bool UseLocalOllama;
        public bool LogApiRequestBody;
        public bool FixApiPathForThinkMode;
        public ThinkMode ThinkMode;
        public HierarchicalMemory HierarchicalMemory;
        public string LogHeader;

        public LLMRequestContext(
            string apiUrl = "",
            string apiKey = "",
            string modelName = "",
            string systemPrompt = "",
            string userPrompt = "",
            bool useLocalOllama = false,
            bool logApiRequestBody = false,
            ThinkMode thinkMode = ThinkMode.Default,
            HierarchicalMemory hierarchicalMemory = null,
            string logHeader = "LLMRequest",
            bool fixApiPathForThinkMode = false
        )
        {
            ApiUrl = apiUrl;
            ApiKey = apiKey;
            ModelName = modelName;
            SystemPrompt = systemPrompt;
            UserPrompt = userPrompt;
            UseLocalOllama = useLocalOllama;
            LogApiRequestBody = logApiRequestBody;
            ThinkMode = thinkMode;
            HierarchicalMemory = hierarchicalMemory;
            LogHeader = logHeader;
            FixApiPathForThinkMode = fixApiPathForThinkMode;
        }
    }

    public struct LLMStandardResponse
    {
        public bool Success;
        public string EmotionTag;  // 动作标签，如 [Happy], [Think] 等
        public string VoiceText;   // 用于 TTS 的文本
        public string SubtitleText;// 用于字幕显示的文本

        public LLMStandardResponse(bool success, string emotionTag, string voiceText, string subtitleText)
        {
            Success = success;
            EmotionTag = emotionTag;
            VoiceText = voiceText;
            SubtitleText = subtitleText;
        }
    }

    public static class LLMUtils
    {
        public static LLMStandardResponse ParseStandardResponse(string response)
        {
            LLMStandardResponse ret = new LLMStandardResponse(false, "Think", "", response);
            // 按 ||| 分割（注意：有些模型可能会用单个 | ）
            string[] parts = response.Split(new string[] { "|||" }, StringSplitOptions.None);

            // 如果不是 |||，尝试单个 |
            if (parts.Length < 3)
            {
                parts = response.Split(new string[] { "|" }, StringSplitOptions.None);
            }

            // 【核心修改：严格的格式检查 + 正则容错】
            if (parts.Length >= 3)
            {
                // 用正则提取标签名，容错处理多余括号等情况
                var tagMatch = Regex.Match(parts[0], @"\[(\w+)\]");
                if (tagMatch.Success)
                {
                    ret.EmotionTag = tagMatch.Groups[1].Value;
                }
                else
                {
                    // fallback：去掉所有非字母字符
                    ret.EmotionTag = Regex.Replace(parts[0].Trim(), @"[^\w]", "");
                    if (string.IsNullOrEmpty(ret.EmotionTag)) ret.EmotionTag = "Idle";
                }
                ret.VoiceText = parts[1].Trim();
                ret.SubtitleText = parts[2].Trim();

                ret.Success = true;
            }

            // 兜底：格式不完整时尝试智能解析
            if (!ret.Success)
            {
                // 情况1: [Tag] 日文 ||| 中文（只有一个 |||）
                var twoPartMatch = Regex.Match(response, @"\[(\w+)\]\s*(.+?)\s*\|\|\|\s*(.+)");
                if (twoPartMatch.Success)
                {
                    ret.EmotionTag = twoPartMatch.Groups[1].Value;
                    ret.VoiceText = twoPartMatch.Groups[2].Value.Trim();
                    ret.SubtitleText = twoPartMatch.Groups[3].Value.Trim();
                    ret.Success = true;
                    Log.Warning($"[格式兜底-两段] [{ret.EmotionTag}] voice={ret.VoiceText} sub={ret.SubtitleText}");
                }
                // 情况2: [Tag] 纯内容（没有 |||）
                else
                {
                    var fallbackMatch = Regex.Match(response, @"\[(\w+)\]\s*(.+)");
                    if (fallbackMatch.Success)
                    {
                        ret.EmotionTag = fallbackMatch.Groups[1].Value;
                        ret.VoiceText = fallbackMatch.Groups[2].Value.Trim();
                        ret.SubtitleText = ret.VoiceText;
                        ret.Success = true;
                        Log.Warning($"[格式兜底-单段] [{ret.EmotionTag}] {ret.VoiceText}");
                    }
                    else
                    {
                        Log.Warning($"[格式错误] AI 回复不符合格式: {response}");
                    }
                }
            }

            return ret;
        }

        public static string BuildRequestBody(LLMRequestContext requestContext)
        {
            // 【集成分层记忆】获取带记忆上下文的提示词
            string userPromptWithMemory = GetContextWithMemory(requestContext.HierarchicalMemory, requestContext.UserPrompt);

            string jsonBody;
            string extraJson = requestContext.UseLocalOllama ? $@",""stream"": false" : "";
            // 【深度思考参数】
            extraJson += GetThinkParameterJson(requestContext.ThinkMode);

            if (requestContext.ModelName.Contains("gemma")) {
                // 将 persona 作为背景信息放在 user 消息的最前面
                string finalPrompt = $"[System Instruction]\n{requestContext.SystemPrompt}\n\n[User Message]\n{userPromptWithMemory}";
                jsonBody = $@"{{ ""model"": ""{requestContext.ModelName}"", ""messages"": [ {{ ""role"": ""user"", ""content"": ""{ResponseParser.EscapeJson(finalPrompt)}"" }} ]{extraJson} }}";
            } else {
                // Gemini 或 Ollama (如果是 Llama3 等) 通常支持 system role
                jsonBody = $@"{{ ""model"": ""{requestContext.ModelName}"", ""messages"": [ {{ ""role"": ""system"", ""content"": ""{ResponseParser.EscapeJson(requestContext.SystemPrompt)}"" }}, {{ ""role"": ""user"", ""content"": ""{ResponseParser.EscapeJson(userPromptWithMemory)}"" }} ]{extraJson} }}";
            }

            Log.Info($"[记忆系统] 启用状态: {requestContext.HierarchicalMemory != null}");
            // 【日志】打印完整的请求体（如果启用）
            if (requestContext.LogApiRequestBody)
            {
                // 【调试日志】显示完整的请求内容
                Log.Info($"[发送给LLM的完整内容]\n========================================\n[System Prompt]\n{requestContext.SystemPrompt}\n\n[User Content]\n{userPromptWithMemory}\n========================================");
                Log.Info($"[API请求] 完整请求体:\n{jsonBody}");
            }

            return jsonBody;
        }

        /// <summary>
        /// 获取深度思考参数的 JSON 字符串
        /// </summary>
        private static string GetThinkParameterJson(ThinkMode thinkMode)
        {
            if (thinkMode == ThinkMode.Enable)
            {
                return @",""think"": true";
            }
            else if (thinkMode == ThinkMode.Disable)
            {
                return @",""think"": false";
            }
            // Default 模式不添加 think 参数
            return "";
        }

        private static string GetContextWithMemory(HierarchicalMemory hierarchicalMemory, string currentPrompt)
        {
            if (hierarchicalMemory != null)
            {
                string memoryContext = hierarchicalMemory.GetContext();
                Log.Info($"[记忆系统] 当前记忆状态:\n{hierarchicalMemory.GetMemoryStats()}");

                // 如果有记忆内容，则拼接；否则只返回当前提示
                if (!string.IsNullOrWhiteSpace(memoryContext))
                {
                    return $"{memoryContext}\n\n【Current Input】\n{currentPrompt}";
                }
            }
            
            // 无记忆或未启用，直接返回原始 prompt
            return currentPrompt;
        }

        /// <summary>
        /// 获取适合当前think模式的API URL
        /// </summary>
        public static string GetApiUrlForThinkMode(LLMRequestContext requestContext)
        {
            string baseUrl = requestContext.ApiUrl;
            // 如果启用了API路径修正，且think模式不是Default，需要使用Ollama原生API (/api/chat)
            if (requestContext.FixApiPathForThinkMode && requestContext.ThinkMode != ThinkMode.Default)
            {
                // 将 /v1/chat/completions 替换为 /api/chat
                if (baseUrl.Contains("/v1/chat/completions"))
                {
                    baseUrl = baseUrl.Replace("/v1/chat/completions", "/api/chat");
                    Log.Info($"[Think Mode] 切换到 Ollama 原生 API: {baseUrl}");
                }
                // 如果URL已经是 /api/chat 或其他格式，保持不变
            }
            
            return baseUrl;
        }
    }
}
