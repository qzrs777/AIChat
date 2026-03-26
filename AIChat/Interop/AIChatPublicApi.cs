using System;
using System.Collections.Generic;

namespace AIChat.Interop
{
    public sealed class AIChatApiConversationResult
    {
        public bool Success { get; set; }
        public bool IsApiError { get; set; }
        public string ErrorMessage { get; set; }
        public long ErrorCode { get; set; }
        public string InputSource { get; set; }
        public string UserPrompt { get; set; }
        public string EmotionTag { get; set; }
        public string VoiceText { get; set; }
        public string SubtitleText { get; set; }
        public string RawResponse { get; set; }
        public bool TtsAttempted { get; set; }
        public bool TtsSucceeded { get; set; }
        public DateTime TimestampUtc { get; set; }
    }

    public interface IAIChatPublicApi
    {
        string ApiVersion { get; }
        bool IsBusy { get; }
        bool IsReady { get; }

        event Action<AIChatApiConversationResult> ConversationCompleted;

        Dictionary<string, string> GetAllConfigValues();
        Dictionary<string, string> GetAllConfigDefaultValues();
        string GetConfigValue(string key);
        bool TrySetConfigValue(string key, string value, out string error);
        bool TrySaveConfig(out string error);
        bool SetConsoleVisible(bool visible, out string error);
        bool GetConsoleVisible();
        bool TryClearMemory(out string error);

        bool TryStartTextConversation(string text, string inputSource, out string error);
        bool TryStartVoiceConversationFromWav(byte[] wavData, string inputSource, out string error);
        bool TryStartVoiceCapture(out string error);
        bool TryStopVoiceCaptureAndSend(string inputSource, out string error);
    }
}