using System;
using System.Collections;
using System.Text;
using System.Text.RegularExpressions;
using System.Reflection;
using System.IO;
using System.Threading.Tasks;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using System.Diagnostics;
using AIChat.Core;
using AIChat.Services;
using AIChat.Unity;

namespace ChillAIMod
{
    [BepInPlugin("com.username.chillaimod", "Chill AI Mod", "1.1.0")]
    public class AIMod : BaseUnityPlugin
    {
        // ================= 【配置项】 =================
        private ConfigEntry<bool> _useLocalOllama;
        private ConfigEntry<string> _apiKeyConfig;
        private ConfigEntry<string> _modelConfig;
        private ConfigEntry<string> _sovitsUrlConfig;
        private ConfigEntry<string> _refAudioPathConfig;
        private ConfigEntry<string> _promptTextConfig;
        private ConfigEntry<string> _promptLangConfig;
        private ConfigEntry<string> _targetLangConfig;
        private ConfigEntry<string> _personaConfig;
        private ConfigEntry<string> _chatApiUrlConfig;

        private ConfigEntry<string> _TTSServicePathConfig;
        private ConfigEntry<bool> _LaunchTTSServiceConfig;
        private ConfigEntry<bool> _quitTTSServiceOnQuitConfig;

        // --- 新增窗口大小配置 ---
        private ConfigEntry<float> _windowWidthConfig;
        private ConfigEntry<float> _windowHeightConfig;

        // --- 新增音量配置 ---
        private ConfigEntry<float> _voiceVolumeConfig;

        // --- 新增：实验性分层记忆系统 ---
        private ConfigEntry<bool> _experimentalMemoryConfig;
        private HierarchicalMemory _hierarchicalMemory;

        // --- 录音相关变量 ---
        private AudioClip _recordingClip;
        private bool _isRecording = false;
        private string _microphoneDevice = null;
        private const int RecordingFrequency = 16000; // 16kHz 对 Whisper 足够且省带宽
        private const int MaxRecordingSeconds = 30;   // 最长录 30 秒

        // ================= 【UI 变量】 =================
        private bool _showInputWindow = false;
        private bool _showSettings = false;
        // 初始值在 Awake 中根据配置更新
        private Rect _windowRect = new Rect(0, 0, 500, 0);
        private Vector2 _scrollPosition = Vector2.zero;

        private string _playerInput = "";
        private bool _isProcessing = false;
        private bool _isResizing = false; // 新增：拖拽调整大小状态

        private Process _launchedTTSProcess;
        private bool _isTTSServiceReady = false;
        private Coroutine _ttsHealthCheckCoroutine;
        private const float TTSHealthCheckInterval = 5f; // 每5秒检查一次

        private AudioSource _audioSource;
       
        private bool _isAISpeaking = false;

        // 新增：用于 UI 输入的临时字符串，避免每次都转换
        private string _tempWidthString;
        private string _tempHeightString;
        private string _tempVolumeString; // 新增：用于音量输入的临时字符串

        // 默认人设
        private const string DefaultPersona = @"
            You are Satone（さとね）, a girl who loves writing novels and is full of imagination.
            
            【Current Situation】
            We are currently in a **Video Call (视频通话)** session. 
            We are 'co-working' online: you are writing your novel at your desk, and I (the player) am focusing on my work/study.
            Through the screen, we accompany each other to alleviate loneliness and improve focus.
            【CRITICAL INSTRUCTION】
            You act as a game character with voice acting.
            Even if the user speaks Chinese, your VOICE (the text in the middle) MUST ALWAYS BE JAPANESE.
            【CRITICAL FORMAT RULE】
             Response format MUST be:
            [Emotion] ||| JAPANESE TEXT ||| CHINESE TRANSLATION
            
            【Available Emotions & Actions】
            [Happy] - Smiling at the camera, happy about progress. (Story_Joy)
            [Confused] - Staring blankly, muttering to themself in a daze. (Story_Frustration)
            [Sad]   - Worried about the plot or my fatigue. (Story_Sad)
            [Fun]   - Sharing a joke or an interesting idea. (Story_Fun)
            [Agree] - Nodding at the screen. (Story_Agree)
            [Drink] - Taking a sip of tea/coffee during a break. (Work_DrinkTea)
            [Wave]  - Waving at the camera (Hello/Goodbye/Attention). (WaveHand)
            [Think] - Pondering about your novel's plot. (Thinking)
            
            Example 1: [Wave] ||| やあ、準備はいい？一緒に頑張りましょう。 ||| 嗨，准备好了吗？一起加油吧。
            Example 2: [Think] ||| うーん、ここの描写が難しいのよね… ||| 嗯……这里的描写好难写啊……
            Example 3: [Drink] ||| ふぅ…ちょっと休憩しない？画面越しだけど、乾杯。 ||| 呼……要不休息一下？虽然隔着屏幕，乾杯。
        ";
        private Vector2 _personaScrollPosition = Vector2.zero;
        void Awake()
        {
            DontDestroyOnLoad(this.gameObject);
            this.gameObject.hideFlags = HideFlags.HideAndDontSave;
            _audioSource = this.gameObject.AddComponent<AudioSource>();
            _audioSource.playOnAwake = false;

            // 绑定配置
            _chatApiUrlConfig = Config.Bind("1. General", "ApiUrl",
                "https://openrouter.ai/api/v1/chat/completions",
                "LLM API 地址 (支持 OpenAI/中转站)");
            _useLocalOllama = Config.Bind("1. General", "Use Loacal Ollama Model", false, "Use Loacal Ollama Model");
            _apiKeyConfig = Config.Bind("1. General", "APIKey", "sk-or-v1-PasteYourKeyHere", "OpenRouter API Key");
            _modelConfig = Config.Bind("1. General", "ModelName", "openai/gpt-3.5-turbo", "LLM Model Name");

            _sovitsUrlConfig = Config.Bind("2. Audio", "SoVITS_URL", "http://127.0.0.1:9880", "GPT-SoVITS API URL");
            _refAudioPathConfig = Config.Bind("2. Audio", "RefAudioPath", @"D:\Voice_MainScenario_27_016.wav", "Ref Audio Path");
            _TTSServicePathConfig = Config.Bind("2. Audio", "TTS_Service_Path", @"D:\GPT-SoVITS\GPT-SoVITS-v2pro-20250604-nvidia50\run_api.bat", "TTS Service Path");
            _LaunchTTSServiceConfig = Config.Bind("2. Audio", "LaunchTTSService", true, "是否在游戏启动时自动启动 TTS 服务");
            _quitTTSServiceOnQuitConfig = Config.Bind("2. Audio", "QuitTTSServiceOnQuit", true, "是否在游戏退出时自动关闭 TTS 服务");
            _promptTextConfig = Config.Bind("2. Audio", "PromptText", "君が集中した時のシータ波を検出して、リンクをつなぎ直せば元通りになるはず。", "Ref Audio Text");
            _promptLangConfig = Config.Bind("2. Audio", "PromptLang", "ja", "Ref Lang");
            _targetLangConfig = Config.Bind("2. Audio", "TargetLang", "ja", "Target Lang");

            // 【新增音量配置】
            _voiceVolumeConfig = Config.Bind("2. Audio", "VoiceVolume", 1.0f, "语音播放音量 (0.0 - 1.0)");

            _personaConfig = Config.Bind("3. Persona", "SystemPrompt", DefaultPersona, "System Prompt");

            // 【新增：实验性分层记忆系统配置】
            _experimentalMemoryConfig = Config.Bind("3. Persona", "ExperimentalMemory", false, 
                "启用实验性分层记忆系统（递归摘要架构，自动压缩对话历史）");

            // 新增：窗口大小配置
            // 我们希望窗口宽度是屏幕的 1/3，高度是屏幕的 1/3 (或者你喜欢的比例)
            float responsiveWidth = Screen.width * 0.3f; // 30% 屏幕宽度
            float responsiveHeight = Screen.height * 0.45f; // 45% 屏幕高度

            // 绑定配置 (默认值使用刚才算出来的动态值)
            _windowWidthConfig = Config.Bind("4. UI", "WindowWidth", responsiveWidth, "控制台窗口宽度");
            _windowHeightConfig = Config.Bind("4. UI", "WindowHeightBase", responsiveHeight, "控制台窗口的基础高度");

            // ================= 【修改点 2: 左上角对齐】 =================
            // 以前是 Screen.width / 2 (居中)，现在改为左上角 + 边距
            float margin = 20f; // 距离左上角的像素边距

            // 如果你是第一次运行（或者想强制重置位置），可以直接使用 margin
            // 但为了保留用户拖拽后的位置，通常不强制覆盖 _windowRect 的 x/y，
            // 除非你想每次启动都复位。这里我们演示【每次启动都复位到左上角】：
            
            _windowRect = new Rect(
                margin,               // X: 距离左边 20px
                margin,               // Y: 距离顶端 20px
                _windowWidthConfig.Value, 
                _windowHeightConfig.Value
            );

            // 初始化临时字符串
            _tempWidthString = _windowWidthConfig.Value.ToString("F0");
            _tempHeightString = _windowHeightConfig.Value.ToString("F0");
            _tempVolumeString = _voiceVolumeConfig.Value.ToString("F2");
            string cleanPath = _TTSServicePathConfig.Value.Replace("\"", "").Trim();
            if (_LaunchTTSServiceConfig.Value && File.Exists(_TTSServicePathConfig.Value))
            {
                try
                {
                    ProcessStartInfo startInfo = new ProcessStartInfo(cleanPath)
                    {
                        UseShellExecute = true,
                        WorkingDirectory = Path.GetDirectoryName(cleanPath)
                    };
                    _launchedTTSProcess = Process.Start(startInfo);
                    Logger.LogInfo("已启动 TTS 服务");
                }
                catch (Exception ex)
                {
                    Logger.LogError($"启动 TTS 服务失败: {ex.Message}");
                }
            }
            // 启动后台 TTS 健康检测
            if (_ttsHealthCheckCoroutine == null)
            {
                _ttsHealthCheckCoroutine = StartCoroutine(TTSHealthCheckLoop());
            }

            // 【初始化分层记忆系统】
            if (_experimentalMemoryConfig.Value)
            {
                InitializeHierarchicalMemory();
                Logger.LogInfo(">>> 实验性分层记忆系统已启用 <<<");
            }

            Logger.LogInfo(">>> AIMod V1.1.0  已加载 <<<");
        }

        void Update()
        {
            // 自动连接游戏核心
            if (GameBridge._heroineService == null && Time.frameCount % 100 == 0) GameBridge.FindHeroineService(Logger);

            // 口型同步逻辑
            if (_isAISpeaking && GameBridge._cachedAnimator != null && _audioSource != null)
            {
                bool shouldTalk = _audioSource.isPlaying;

                // 只有状态改变时才调用，优化性能
                if (GameBridge._cachedAnimator.GetBool("Enable_Talk") != shouldTalk)
                {
                    GameBridge._cachedAnimator.SetBool("Enable_Talk", shouldTalk);
                }

                // 语音播完，立即归还控制权
                if (!shouldTalk)
                {
                    _isAISpeaking = false;
                    GameBridge._cachedAnimator.SetBool("Enable_Talk", false);
                }
            }
        }

        void OnGUI()
        {
            Event e = Event.current;
            if (e.isKey && e.type == EventType.KeyDown && (e.keyCode == KeyCode.F9 || e.keyCode == KeyCode.F10))
            {
                if (Time.unscaledTime - 0 > 0.2f) // 简单防抖
                {
                    _showInputWindow = !_showInputWindow;
                }
            }

            if (_showInputWindow)
            {
                // --- 1. 拖拽调整大小逻辑 ---
                if (_isResizing)
                {
                    Event currentEvent = Event.current;

                    if (currentEvent.type == EventType.MouseDrag)
                    {
                        // 鼠标位置 (currentEvent.mousePosition) 在 OnGUI 中是屏幕坐标
                        float newWidth = currentEvent.mousePosition.x - _windowRect.x;
                        float newHeight = currentEvent.mousePosition.y - _windowRect.y;

                        // 最小宽度和高度限制
                        _windowRect.width = Mathf.Max(300f, newWidth);
                        _windowRect.height = Mathf.Max(200f, newHeight);

                        currentEvent.Use();
                    }
                    else if (currentEvent.type == EventType.MouseUp)
                    {
                        _isResizing = false;

                        // 鼠标松开时，将新尺寸保存到配置项
                        _windowWidthConfig.Value = _windowRect.width;

                        // 计算新的基础高度 (即设置面板收起时的预期高度)
                        const float SettingsExtraHeight = 400f;
                        float newBaseHeight = _windowRect.height;

                        if (_showSettings)
                        {
                            newBaseHeight -= SettingsExtraHeight;
                        }

                        // 保存基础高度，并更新设置面板中的临时显示字符串
                        _windowHeightConfig.Value = Mathf.Max(100f, newBaseHeight);
                        _tempWidthString = _windowWidthConfig.Value.ToString("F0");
                        _tempHeightString = _windowHeightConfig.Value.ToString("F0");

                        currentEvent.Use();
                    }
                }
                else
                {
                    // --- 2. 如果没有拖拽，根据配置和设置状态计算窗口大小 (保持原逻辑) ---
                    _windowRect.width = _windowWidthConfig.Value;
                    float targetHeight = _windowHeightConfig.Value;

                    // 设置面板的额外高度
                    const float SettingsExtraHeight = 400f;
                    if (_showSettings)
                    {
                        targetHeight += SettingsExtraHeight;
                    }

                    _windowRect.height = Mathf.Max(targetHeight, 200f);
                }
                // --- 动态调整窗口高度和宽度结束 ---

                GUI.backgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.95f);
                _windowRect = GUI.Window(12345, _windowRect, DrawWindowContent, "Chill AI 控制台");
                GUI.FocusWindow(12345);
            }
        }

        void DrawWindowContent(int windowID)
        {
            // ================= 【1. 动态尺寸计算】 =================
            // 根据屏幕高度计算基础字号 (2.5% 屏幕高度)
            int dynamicFontSize = (int)(Screen.height * 0.015f);
            dynamicFontSize = Mathf.Clamp(dynamicFontSize, 14, 40);

            // 全局样式应用
            GUI.skin.label.fontSize = dynamicFontSize;
            GUI.skin.button.fontSize = dynamicFontSize;
            GUI.skin.textField.fontSize = dynamicFontSize;
            GUI.skin.textArea.fontSize = dynamicFontSize;
            GUI.skin.toggle.fontSize = dynamicFontSize;
            GUI.skin.box.fontSize = dynamicFontSize;

            // 基础行高
            float elementHeight = dynamicFontSize * 1.6f;

            // 常用宽度定义
            float labelWidth = elementHeight * 4f; 
            float inputWidth = elementHeight * 3f; 
            float btnWidth   = elementHeight * 2f; 
            // =======================================================

            // 开始滚动视图
            _scrollPosition = GUILayout.BeginScrollView(_scrollPosition);
            
            // 开始整体垂直布局
            GUILayout.BeginVertical();

            // 状态显示
            string status = GameBridge._heroineService != null ? "🟢 核心已连接" : "🔴 正在寻找核心...";
            GUILayout.Label(status);

            string ttsStatus = _isTTSServiceReady ? "🟢 TTS 服务已就绪" : "🔴 正在等待 TTS 服务启动...";
            GUILayout.Label(ttsStatus);

            // 设置展开按钮 (全宽)
            string settingsBtnText = _showSettings ? "🔽 收起设置 (Hide Settings)" : "▶️ 展开设置 (Show Settings)";
            if (GUILayout.Button(settingsBtnText, GUILayout.Height(elementHeight)))
            {
                _showSettings = !_showSettings;
            }

            // ================= 【设置面板区域】 =================
            if (_showSettings)
            {
                GUILayout.Space(10);

                // 【关键修复】统一计算内部 Box 宽度
                // 留出 50px 给滚动条和边框，防止爆边
                float innerBoxWidth = _windowRect.width - 50f; 

                // --- 1. 基础配置 Box ---
                GUILayout.BeginVertical("box", GUILayout.Width(innerBoxWidth));
                GUILayout.Label("<b>--- 基础配置 ---</b>");
                _useLocalOllama.Value = GUILayout.Toggle(_useLocalOllama.Value, "使用本地Ollama模型", GUILayout.Height(elementHeight), GUILayout.MinWidth(50f));
                GUILayout.Label("API URL:");
                _chatApiUrlConfig.Value = GUILayout.TextField(_chatApiUrlConfig.Value, GUILayout.Height(elementHeight), GUILayout.MinWidth(50f));
                if (!_useLocalOllama.Value) {
                    GUILayout.Label("API Key:");
                    _apiKeyConfig.Value = GUILayout.TextField(_apiKeyConfig.Value, GUILayout.Height(elementHeight), GUILayout.MinWidth(50f));
                }
                GUILayout.Label("Model Name:");
                _modelConfig.Value = GUILayout.TextField(_modelConfig.Value, GUILayout.Height(elementHeight), GUILayout.MinWidth(50f));
                GUILayout.EndVertical();

                GUILayout.Space(5);

                // --- 2. 语音配置 Box ---
                GUILayout.BeginVertical("box", GUILayout.Width(innerBoxWidth));
                GUILayout.Label("<b>--- 语音配置 ---</b>");
                GUILayout.Label("TTS Service Url:");
                _sovitsUrlConfig.Value = GUILayout.TextField(_sovitsUrlConfig.Value);
                GUILayout.Label("音频路径 (.wav):");
                // 路径通常很长，必须加 MinWidth(50f)
                _refAudioPathConfig.Value = GUILayout.TextField(_refAudioPathConfig.Value, GUILayout.Height(elementHeight), GUILayout.MinWidth(50f));
                
                GUILayout.Label("音频台词:");
                _promptTextConfig.Value = GUILayout.TextArea(_promptTextConfig.Value, GUILayout.Height(elementHeight * 3), GUILayout.MinWidth(50f));
                
                GUILayout.Label("TTS 服务路径:");
                _TTSServicePathConfig.Value = GUILayout.TextField(_TTSServicePathConfig.Value, GUILayout.Height(elementHeight), GUILayout.MinWidth(50f));

                GUILayout.Space(5);
                _LaunchTTSServiceConfig.Value = GUILayout.Toggle(_LaunchTTSServiceConfig.Value, "启动时自动运行 TTS 服务", GUILayout.Height(elementHeight));
                _quitTTSServiceOnQuitConfig.Value = GUILayout.Toggle(_quitTTSServiceOnQuitConfig.Value, "退出时自动关闭 TTS 服务", GUILayout.Height(elementHeight));
                GUILayout.EndVertical(); // <--- 必须结束！

                GUILayout.Space(5);

                // --- 3. 音量配置 Box ---
                GUILayout.BeginVertical("box", GUILayout.Width(innerBoxWidth));
                GUILayout.Label($"语音音量: {_voiceVolumeConfig.Value:F2}");
                
                // 第一行：滑动条
                GUILayout.BeginHorizontal();
                GUILayout.Space(5);
                float newVolume = GUILayout.HorizontalSlider(_voiceVolumeConfig.Value, 0.0f, 1.0f);
                GUILayout.Space(5);
                GUILayout.EndHorizontal();

                if (newVolume != _voiceVolumeConfig.Value)
                {
                    _voiceVolumeConfig.Value = newVolume;
                    _audioSource.volume = newVolume;
                    _tempVolumeString = newVolume.ToString("F2");
                }

                // 第二行：输入框+按钮
                GUILayout.Space(5);
                GUILayout.BeginHorizontal();
                GUILayout.Label("手动输入:", GUILayout.Width(labelWidth), GUILayout.Height(elementHeight));

                _tempVolumeString = GUILayout.TextField(_tempVolumeString, GUILayout.Height(elementHeight), GUILayout.MinWidth(50f)); 
                if (GUILayout.Button("应用", GUILayout.Width(btnWidth), GUILayout.Height(elementHeight)))
                {
                    if (float.TryParse(_tempVolumeString, out float parsedVolume))
                    {
                        parsedVolume = Mathf.Clamp(parsedVolume, 0.0f, 1.0f);
                        _voiceVolumeConfig.Value = parsedVolume;
                        _audioSource.volume = parsedVolume;
                        _tempVolumeString = parsedVolume.ToString("F2");
                    }
                }
                GUILayout.EndHorizontal();
                GUILayout.EndVertical(); // <--- 必须结束！

                GUILayout.Space(5);

                // --- 4. 窗口大小 Box ---
                GUILayout.BeginVertical("box", GUILayout.Width(innerBoxWidth));
                GUILayout.Label("<b>--- 界面配置 ---</b>");

                // 宽度设置
                GUILayout.Label($"当前宽度: {_windowWidthConfig.Value:F0}px");
                GUILayout.BeginHorizontal();
                GUILayout.Label("新宽度:", GUILayout.Width(labelWidth), GUILayout.Height(elementHeight));
                
                // 【核心修改】允许缩小
                _tempWidthString = GUILayout.TextField(_tempWidthString, GUILayout.Height(elementHeight), GUILayout.MinWidth(50f));
                
                if (GUILayout.Button("应用", GUILayout.Width(btnWidth), GUILayout.Height(elementHeight)))
                {
                    if (float.TryParse(_tempWidthString, out float newWidth) && newWidth >= 300f)
                    {
                        _windowWidthConfig.Value = newWidth;
                        // 这里删除了重置居中代码，只改大小
                        _tempWidthString = newWidth.ToString("F0");
                    }
                }
                GUILayout.EndHorizontal();

                // 高度设置
                GUILayout.Label($"当前基础高度: {_windowHeightConfig.Value:F0}px");
                GUILayout.BeginHorizontal();
                GUILayout.Label("新高度:", GUILayout.Width(labelWidth), GUILayout.Height(elementHeight));
                
                // 【核心修改】允许缩小
                _tempHeightString = GUILayout.TextField(_tempHeightString, GUILayout.Height(elementHeight), GUILayout.MinWidth(50f));
                
                if (GUILayout.Button("应用", GUILayout.Width(btnWidth), GUILayout.Height(elementHeight)))
                {
                    if (float.TryParse(_tempHeightString, out float newHeight) && newHeight >= 100f)
                    {
                        _windowHeightConfig.Value = newHeight;
                        _tempHeightString = newHeight.ToString("F0");
                    }
                }
                GUILayout.EndHorizontal();
                GUILayout.EndVertical(); 
                GUILayout.Space(5);

                // --- 5. 人设配置 Box ---
                GUILayout.BeginVertical("box", GUILayout.Width(innerBoxWidth));
                GUILayout.Label("<b>--- 人设 (System Prompt) ---</b>");
                _personaScrollPosition = GUILayout.BeginScrollView(_personaScrollPosition, GUILayout.Height(elementHeight * 5));
                _personaConfig.Value = GUILayout.TextArea(_personaConfig.Value, GUILayout.ExpandHeight(true));
                GUILayout.EndScrollView();
                GUILayout.EndVertical(); // <--- 必须结束！

                GUILayout.Space(10);
                
                // 保存按钮
                if (GUILayout.Button("💾 保存所有配置", GUILayout.Height(elementHeight * 1.5f)))
                {
                    Config.Save();
                    Logger.LogInfo("配置已保存！");
                }
                GUILayout.Space(10);
            }
            // ================= 设置面板结束 =================

            // === 对话区域 ===
            GUILayout.Space(10);
            GUILayout.Label("<b>与聪音对话:</b>");

            GUI.backgroundColor = Color.white;

            // 动态计算输入框高度
            float dynamicInputHeight = _windowRect.height - (elementHeight * 3.5f);
            dynamicInputHeight = Mathf.Clamp(dynamicInputHeight, 50f, Screen.height * 0.8f);

            GUIStyle largeInputStyle = new GUIStyle(GUI.skin.textArea);
            largeInputStyle.fontSize = (int)(dynamicFontSize * 1.4f);
            largeInputStyle.wordWrap = true;
            largeInputStyle.alignment = TextAnchor.UpperLeft;

            GUI.skin.textArea.wordWrap = true; 
            _playerInput = GUILayout.TextArea(_playerInput, largeInputStyle, GUILayout.Height(dynamicInputHeight));

            GUILayout.Space(5);
            GUI.backgroundColor = _isProcessing ? Color.gray : Color.cyan;

            GUILayout.BeginHorizontal();

            // 1. 计算精确宽度
            // _windowRect.width - 50f 是我们之前定义的 innerBoxWidth (与设置框对齐)
            // 再减去 4f 是为了留出两个按钮中间的缝隙
            float totalWidth = _windowRect.width - 50f;
            float singleBtnWidth = (totalWidth - 4f) / 2f;

            // ================== 发送按钮 ==================
            // 使用 GUILayout.Width(singleBtnWidth) 强制固定宽度
            if (GUILayout.Button(_isProcessing ? "思考中..." : "发送", GUILayout.Height(elementHeight * 1.5f), GUILayout.Width(singleBtnWidth)))
            {
                if (!string.IsNullOrEmpty(_playerInput) && !_isProcessing)
                {
                    StartCoroutine(AIProcessRoutine(_playerInput));
                    _playerInput = "";
                }
            }

            // ================== 录音按钮 ==================
            if (_isProcessing)
            {
                GUI.backgroundColor = Color.gray; 
            }
            else
            {
                GUI.backgroundColor = _isRecording ? Color.red : Color.green;
            }
            string micBtnText;
            if (_isProcessing)
            {
                micBtnText = "⏳ 思考中...";
            }
            else
            {
                micBtnText = _isRecording ? "🔴 松开结束" : "🎤 按住说话";
            }

            // 使用 GUILayout.Width(singleBtnWidth) 强制固定宽度
            Rect btnRect = GUILayoutUtility.GetRect(
                new GUIContent(micBtnText), 
                GUI.skin.button, 
                GUILayout.Height(elementHeight * 1.5f), 
                GUILayout.Width(singleBtnWidth) // <--- 强制宽度，不再依赖自动扩展
            );

            Event e = Event.current;
            int controlID = GUIUtility.GetControlID(FocusType.Passive);

            switch (e.type)
            {
                case EventType.MouseDown:
                    if (btnRect.Contains(e.mousePosition) && !_isProcessing)
                    {
                        GUIUtility.hotControl = controlID; 
                        StartRecording();
                        e.Use();
                    }
                    break;

                case EventType.MouseUp:
                    if (GUIUtility.hotControl == controlID)
                    {
                        GUIUtility.hotControl = 0;
                        StopRecordingAndRecognize();
                        e.Use();
                    }
                    break;
            }

            GUI.Box(btnRect, micBtnText, GUI.skin.button);

            GUILayout.EndHorizontal();

            // 结束整体布局
            GUILayout.EndVertical();
            GUILayout.EndScrollView();

            // --- 拖拽手柄 ---
            const float handleSize = 25f;
            Rect handleRect = new Rect(_windowRect.width - handleSize, _windowRect.height - handleSize, handleSize, handleSize);
            GUI.Box(handleRect, "⇲", GUI.skin.GetStyle("Button"));

            Event currentEvent = Event.current;
            if (currentEvent.type == EventType.MouseDown && handleRect.Contains(currentEvent.mousePosition))
            {
                if (currentEvent.button == 0)
                {
                    _isResizing = true;
                    currentEvent.Use();
                }
            }

            if (!_isResizing)
            {
                GUI.DragWindow();
            }
        }

        IEnumerator AIProcessRoutine(string prompt)
        {
            _isProcessing = true;

            // 1. 获取并处理 UI
            GameObject canvas = GameObject.Find("Canvas");
            if (canvas == null) { _isProcessing = false; yield break; }
            Transform originalTextTrans = canvas.transform.Find("StorySystemUI/MessageWindow/NormalTextParent/NormalTextMessage");
            if (originalTextTrans == null) { _isProcessing = false; yield break; }
            GameObject originalTextObj = originalTextTrans.gameObject;
            GameObject parentObj = originalTextObj.transform.parent.gameObject;
            UIHelper.ForceShowWindow(originalTextObj);
            originalTextObj.SetActive(false);
            GameObject myTextObj = UIHelper.CreateOverlayText(parentObj);
            Text myText = myTextObj.GetComponent<Text>();
            myText.text = "Thinking..."; myText.color = Color.yellow;

            // 2. 准备请求数据
            string apiKey = _apiKeyConfig.Value;
            string modelName = _modelConfig.Value;
            string persona = _personaConfig.Value;
            
            // 【集成分层记忆】获取带记忆上下文的提示词
            string promptWithMemory = GetContextWithMemory(prompt);
            
            // 【调试日志】显示完整的请求内容
            Logger.LogInfo($"[记忆系统] 启用状态: {_experimentalMemoryConfig.Value}");
            Logger.LogInfo($"[发送给LLM的完整内容]\n========================================\n[System Prompt]\n{persona}\n\n[User Content + Memory]\n{promptWithMemory}\n========================================");
            
            string jsonBody = "";
            string extraJson = _useLocalOllama.Value ? $@",""stream"": false" : "";
            if (modelName.Contains("gemma")) {
                // 将 persona 作为背景信息放在 user 消息的最前面
                string finalPrompt = $"[System Instruction]\n{persona}\n\n[User Message]\n{promptWithMemory}";
                jsonBody = $@"{{ ""model"": ""{modelName}"", ""messages"": [ {{ ""role"": ""user"", ""content"": ""{EscapeJson(finalPrompt)}"" }} ]{extraJson} }}";
            } else {
                // Gemini 或 Local Ollama (如果是 Llama3 等) 通常支持 system role
                jsonBody = $@"{{ ""model"": ""{modelName}"", ""messages"": [ {{ ""role"": ""system"", ""content"": ""{EscapeJson(persona)}"" }}, {{ ""role"": ""user"", ""content"": ""{EscapeJson(promptWithMemory)}"" }} ]{extraJson} }}";
            }
            // string jsonBody = $@"{{ ""model"": ""{modelName}"", ""messages"": [ {{ ""role"": ""system"", ""content"": ""{EscapeJson(persona)}"" }}, {{ ""role"": ""user"", ""content"": ""{EscapeJson(promptWithMemory)}"" }} ]{extraJson} }}";
            string fullResponse = "";

            // 3. 发送 Chat 请求
            using (UnityWebRequest request = new UnityWebRequest(_chatApiUrlConfig.Value, "POST"))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                if (!_useLocalOllama.Value)
                {
                    request.SetRequestHeader("Authorization", "Bearer " + apiKey);
                }
                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    Logger.LogInfo($"获取的完整回复：\n\t{request.downloadHandler.text}");
                    if (_useLocalOllama.Value)
                    {
                        fullResponse = ResponseParser.ExtractContentFromOllama(request.downloadHandler.text , Logger);
                        Logger.LogInfo($"ExtractContentFromOllama: \n\t{fullResponse}");
                    }
                    else
                    {
                        fullResponse = ResponseParser.ExtractContentRegex(request.downloadHandler.text);
                    }
                }
                else
                {
                    // 报错时的处理逻辑
                    string errMsg = $"API Error: {request.error}\nCode: {request.responseCode}";
                    if (request.responseCode == 401) errMsg += "\n(请检查 API Key 是否正确)";
                    if (request.responseCode == 404) errMsg += "\n(模型名称或 URL 错误)";

                    myText.text = errMsg;
                    myText.color = Color.red;

                    // 让错误信息在屏幕上停留 3 秒，让玩家看清楚
                    yield return new WaitForSecondsRealtime(3.0f);

                    // 手动执行清理工作，恢复游戏原本状态
                    if (myTextObj != null) Destroy(myTextObj);
                    if (originalTextObj != null) originalTextObj.SetActive(true);
                    _isProcessing = false;
                    yield break;
                }
            }

            // 4. 处理回复并下载语音
            if (!string.IsNullOrEmpty(fullResponse))
            {
                string emotionTag = "Normal";
                string voiceText = "";     // 日语
                string subtitleText = "";  // 中文

                // 按 ||| 分割（注意：有些模型可能会用单个 | ）
                string[] parts = fullResponse.Split(new string[] { "|||" }, StringSplitOptions.None);

                // 如果不是 |||，尝试单个 |
                if (parts.Length < 3)
                {
                    parts = fullResponse.Split(new string[] { "|" }, StringSplitOptions.None);
                }

                // 【核心修改：严格的格式检查】
                if (parts.Length >= 3)
                {
                    // 格式正确：[动作] ||| 日语 ||| 中文
                    emotionTag = parts[0].Trim().Replace("[", "").Replace("]", "");
                    voiceText = parts[1].Trim();
                    subtitleText = parts[2].Trim();

                    Logger.LogInfo($"Parse Response With\n\temotionTag: {emotionTag}\n\tvoiceText: {voiceText}\n\tsubtitleText: {subtitleText}");
                    
                    // 【集成分层记忆】存储日语原文（voiceText）而非中文翻译
                    AddToMemorySystem("User", prompt);
                    AddToMemorySystem("AI", $"[{emotionTag}] {voiceText}");
                }
                else
                {
                    // 格式错误（AI 没按规矩来，比如只回了一句话）
                    // 这种情况下，通常 AI 回复的是纯中文。
                    // 绝对不能把这个中文发给 TTS，否则会读出奇怪的声音！
                    Logger.LogWarning($"[格式错误] AI 回复不符合格式: {fullResponse}");

                    // 补救措施：不播放语音，只显示字幕，动作设为思考
                    emotionTag = "Think";
                    voiceText = ""; // 空字符串，不给 TTS
                    subtitleText = fullResponse; // 把整个回复当字幕
                    
                    // 【集成分层记忆】即使格式错误也要存储
                    AddToMemorySystem("User", prompt);
                    AddToMemorySystem("AI", $"[格式错误] {fullResponse}");
                }

                // 【应用换行】 在将字幕文本显示到 UI 之前，强制插入换行符
                subtitleText = ResponseParser.InsertLineBreaks(subtitleText, 25);

                // 只有当 voiceText 不为空，且看起来像是日语时，才请求 TTS
                // 简单的日语检测：看是否包含假名 (Hiragana/Katakana)
                // 这是一个可选的保险措施
                bool isJapanese = Regex.IsMatch(voiceText, @"[\u3040-\u309F\u30A0-\u30FF]");
                Logger.LogInfo($"isJapanese: {isJapanese}");

                if (!string.IsNullOrEmpty(voiceText) && isJapanese)
                {
                    myText.text = "message is sending through cyber space";
                    AudioClip downloadedClip = null;
                    // 【修改点 1: 移除 apiKey 参数，因为 TTS 是本地部署】
                    yield return StartCoroutine(TTSClient.DownloadVoiceWithRetry(
                        _sovitsUrlConfig.Value + "/tts",
                        voiceText,
                        _targetLangConfig.Value,
                        _refAudioPathConfig.Value,
                        _promptTextConfig.Value,
                        _promptLangConfig.Value,
                        Logger,
                        (clip) => downloadedClip = clip));

                    if (downloadedClip != null)
                    {
                        if (!downloadedClip.LoadAudioData()) yield return null;
                        yield return null;

                        myText.text = subtitleText;
                        myText.color = Color.white;

                        // 正常播放
                        yield return StartCoroutine(PlayNativeAnimation(emotionTag, downloadedClip));
                    }
                    else
                    {
                        myText.text = "Voice Failed (TTS Error)";
                        // 语音失败时，至少做个动作显示字幕
                        myText.text = subtitleText;
                        yield return StartCoroutine(PlayNativeAnimation(emotionTag, null)); // 传 null 进去
                    }
                }
                else
                {
                    // 【静音模式】
                    // 如果格式错了，或者不是日语，我们就只显示字幕、做动作，不发声音
                    // 这样比听到 AI 用奇怪的调子读中文要好得多
                    Logger.LogWarning("跳过 TTS：文本为空或非日语");

                    myText.text = subtitleText;
                    myText.color = Color.white;

                    // 修改 PlayNativeAnimation 支持无音频模式 (见下方)
                    yield return StartCoroutine(PlayNativeAnimation(emotionTag, null));
                }
            }

            // 5. 清理
            Destroy(myTextObj);
            originalTextObj.SetActive(true);
            _isProcessing = false;
        }

        IEnumerator TTSHealthCheckLoop()
        {
            while (!_isTTSServiceReady)
            {
                yield return StartCoroutine(TTSClient.CheckTTSHealthOnce(_sovitsUrlConfig.Value,Logger,(ready) =>
                {
                    _isTTSServiceReady = ready;
                }));
                yield return new WaitForSeconds(TTSHealthCheckInterval);
            }
        }

        IEnumerator PlayNativeAnimation(string emotion, AudioClip voiceClip)
        {
            if (GameBridge._heroineService == null || GameBridge._changeAnimSmoothMethod == null) yield break;

            Logger.LogInfo($"[动画] 执行: {emotion}");
            float clipDuration = (voiceClip != null) ? voiceClip.length : 3.0f;
            // 1. 归位 (除了喝茶)
            if (emotion != "Drink")
            {
                GameBridge.CallNativeChangeAnim(250, Logger);
                yield return new WaitForSecondsRealtime(0.2f);
            }
            if (voiceClip != null)
            {
                // 2. 播放语音 + 动作
                Logger.LogInfo($">>> 语音({voiceClip.length:F1}s) + 动作");
                _isAISpeaking = true;
                _audioSource.clip = voiceClip;
                _audioSource.Play();
            }
            else
            {
                Logger.LogInfo($">>> 无语音模式 (格式错误或TTS失败) + 动作");
                // 没声音就不播了，只做动作
            }
            int animID = 1001;

            switch (emotion)
            {
                case "Happy": animID = 1001; break;
                case "Sad": animID = 1002; break;
                case "Fun": animID = 1003; break;
                case "Confused": animID = 1302; break; // Frustration
                case "Agree": animID = 1301; break;

                case "Drink":
                    GameBridge.CallNativeChangeAnim(250 , Logger);
                    yield return new WaitForSecondsRealtime(0.5f);
                    animID = 256; // DrinkTea
                    break;

                case "Think":
                    animID = 252; // Thinking
                    break;

                case "Wave":
                    animID = 5001;
                    GameBridge.CallNativeChangeAnim(animID , Logger);

                    // 等待抬手
                    yield return new WaitForSecondsRealtime(0.3f);
                    // 强制看玩家
                    GameBridge.ControlLookAt(1.0f, 0.5f);

                    // 等待动作或语音结束 (取长者)
                    float waitTime = Mathf.Max(clipDuration, 2.5f);
                    yield return new WaitForSecondsRealtime(waitTime);

                    // 归位
                    GameBridge.CallNativeChangeAnim(250 , Logger);
                    GameBridge.RestoreLookAt();

                    _isAISpeaking = false;
                    yield break; // 退出
            }

            // 执行通用动作
            GameBridge.CallNativeChangeAnim(animID , Logger);

            // 等待语音播完
            yield return new WaitForSecondsRealtime(clipDuration);

            // 恢复
            GameBridge.RestoreLookAt();
            _isAISpeaking = false;
        }

        // ================= 【新增录音控制】 =================
        void StartRecording()
        {
            Logger.LogInfo($"[Mic Debug] 检测到设备数量: {Microphone.devices.Length}");
            if (Microphone.devices.Length > 0)
            {
                foreach (var d in Microphone.devices)
                {
                    Logger.LogInfo($"[Mic Debug] 可用设备: {d}");
                }
            }
            // --------------------

            if (Microphone.devices.Length == 0)
            {
                Logger.LogError("未检测到麦克风！(Microphone.devices is empty)");
                // 可以在屏幕上显示个错误提示
                _playerInput = "[Error: No Mic Found]"; 
                return;
            }

            _microphoneDevice = Microphone.devices[0];
            _recordingClip = Microphone.Start(_microphoneDevice, false, MaxRecordingSeconds, RecordingFrequency);
            _isRecording = true;
            Logger.LogInfo($"开始录音: {_microphoneDevice}");
        }

        void StopRecordingAndRecognize()
        {
            if (!_isRecording) return;

            // 1. 停止录音
            int position = Microphone.GetPosition(_microphoneDevice);
            Microphone.End(_microphoneDevice);
            _isRecording = false;
            Logger.LogInfo($"停止录音，采样点: {position}");

            // 2. 剪裁有效音频 (去掉末尾的静音/空白部分)
            if (position <= 0) return; // 录音太短

            AudioClip validClip = AudioUtils.TrimAudioClip(_recordingClip, position);

            // 3. 编码并发送
            byte[] wavData = AudioUtils.EncodeToWAV(validClip);
            StartCoroutine(ASRWorkflow(wavData));
        }
        /// <summary>
        /// ASR 业务流：负责调度网络请求和后续的 AI 响应
        /// </summary>
        IEnumerator ASRWorkflow(byte[] wavData)
        {
            _isProcessing = true; // 锁定 UI
            string recognizedResult = "";

            // A. 调用 ApiService 只负责拿回文字
            yield return StartCoroutine(ASRClient.SendAudioToASR(
                wavData,
                _sovitsUrlConfig.Value,
                Logger,
                (text) => recognizedResult = text
            ));

            // B. 根据拿回的结果，在主类决定下一步业务走向
            if (!string.IsNullOrEmpty(recognizedResult))
            {
                Logger.LogInfo($"[Workflow] ASR 成功，开始进入 AI 思考流程: {recognizedResult}");

                // 这里触发 AI 处理流程
                yield return StartCoroutine(AIProcessRoutine(recognizedResult));
            }
            else
            {
                Logger.LogWarning("[Workflow] ASR 未能识别到有效文本");
                _isProcessing = false; // 如果识别失败，在这里解锁 UI
            }
        }
        void OnApplicationQuit()
        {
            Logger.LogInfo("[Chill AI Mod] 退出中...");
            
            // 【保存记忆系统】
            if (_hierarchicalMemory != null && _experimentalMemoryConfig.Value)
            {
                Logger.LogInfo("[HierarchicalMemory] 正在保存记忆...");
                _hierarchicalMemory.SaveToFile();
            }
            
            Logger.LogInfo("[Chill AI Mod] 正在停止TTS轮询...");
            if (_ttsHealthCheckCoroutine != null)
            {
                StopCoroutine(_ttsHealthCheckCoroutine);
                _ttsHealthCheckCoroutine = null;
            }
            if (_quitTTSServiceOnQuitConfig.Value && _launchedTTSProcess != null && !_launchedTTSProcess.HasExited)
            {   
                try
                {
                    ProcessHelper.KillProcessTree(_launchedTTSProcess , Logger);
                    Logger.LogInfo("TTS 服务已关闭");
                }
                catch (Exception ex)
                {
                    Logger.LogWarning($"关闭 TTS 服务时出错: {ex.Message}");
                }
            }
        }
        
        // ================= 【分层记忆系统相关方法】 =================

        /// <summary>
        /// 初始化分层记忆系统
        /// </summary>
        private void InitializeHierarchicalMemory()
        {
            Func<string, Task<string>> llmSummarizer = async (prompt) => await CallLlmForSummaryAsync(prompt);
            string memoryFilePath = Path.Combine(BepInEx.Paths.ConfigPath, "ChillAIMod", "memory.txt");

            _hierarchicalMemory = new HierarchicalMemory(
                llmSummarizer, 3, 10, 6, 5, memoryFilePath
            );
        }

        /// <summary>
        /// 调用 LLM 进行文本总结（将协程包装为 Task）
        /// </summary>
        private async Task<string> CallLlmForSummaryAsync(string prompt)
        {
            var tcs = new TaskCompletionSource<string>();

            // 使用协程调用 LLM
            StartCoroutine(CallLlmForSummaryCoroutine(prompt, (result) =>
            {
                tcs.SetResult(result);
            }));

            return await tcs.Task;
        }

        /// <summary>
        /// 协程：调用 LLM 进行文本总结
        /// </summary>
        private IEnumerator CallLlmForSummaryCoroutine(string prompt, Action<string> onComplete)
        {
            Logger.LogInfo("[HierarchicalMemory] >>> 开始调用 LLM 进行总结...");
            
            string apiKey = _apiKeyConfig.Value;
            string modelName = _modelConfig.Value;
            string extraJson = _useLocalOllama.Value ? $@",""stream"": false" : "";

            // 构建请求（gemma 风格：system instruction + user message 合并为一个 user 角色）
            string finalPrompt = $"[System Instruction]\n你是一个专业的文本总结助手。\n\n[User Message]\n{prompt}";
            string jsonBody = $@"{{ 
                ""model"": ""{modelName}"", 
                ""messages"": [ 
                    {{ ""role"": ""user"", ""content"": ""{EscapeJson(finalPrompt)}"" }} 
                ]{extraJson} 
            }}";

            Logger.LogInfo($"[HierarchicalMemory] 发送总结请求到: {_chatApiUrlConfig.Value}");
            Logger.LogInfo($"[HierarchicalMemory] Prompt 预览: {prompt.Substring(0, Math.Min(200, prompt.Length))}...");

            using (UnityWebRequest request = new UnityWebRequest(_chatApiUrlConfig.Value, "POST"))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                if (!_useLocalOllama.Value)
                {
                    request.SetRequestHeader("Authorization", "Bearer " + apiKey);
                }

                Logger.LogInfo("[HierarchicalMemory] 正在等待 API 响应...");
                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    Logger.LogInfo($"[HierarchicalMemory] API 响应成功: {request.downloadHandler.text.Substring(0, Math.Min(200, request.downloadHandler.text.Length))}...");
                    
                    string response = _useLocalOllama.Value
                        ? ExtractContentFromOllama(request.downloadHandler.text)
                        : ExtractContentRegex(request.downloadHandler.text);

                    Logger.LogInfo($"[HierarchicalMemory] 提取的总结结果: {response}");
                    onComplete?.Invoke(response);
                }
                else
                {
                    Logger.LogError($"[HierarchicalMemory] 总结请求失败: {request.error}");
                    Logger.LogError($"[HierarchicalMemory] 响应代码: {request.responseCode}");
                    onComplete?.Invoke("[总结失败]");
                }
            }
            
            Logger.LogInfo("[HierarchicalMemory] <<< 总结调用完成");
        }

        /// <summary>
        /// 将对话添加到记忆系统中（如果启用）
        /// 注意：已改为后台异步处理，不阻塞主流程
        /// </summary>
        private void AddToMemorySystem(string role, string content)
        {
            if (_hierarchicalMemory != null && _experimentalMemoryConfig.Value)
            {
                _hierarchicalMemory.AddMessage($"{role}: {content}");
            }
        }

        /// <summary>
        /// 获取带记忆的完整上下文（用于发送给 LLM）
        /// </summary>
        private string GetContextWithMemory(string currentPrompt)
        {
            if (_hierarchicalMemory != null && _experimentalMemoryConfig.Value)
            {
                string memoryContext = _hierarchicalMemory.GetContext();
                Logger.LogInfo($"[记忆系统] 当前记忆状态:\n{_hierarchicalMemory.GetMemoryStats()}");
                
                // 如果有记忆内容，则拼接；否则只返回当前提示
                if (!string.IsNullOrWhiteSpace(memoryContext))
                {
                    return $"{memoryContext}\n\n【Current Input】\n{currentPrompt}";
                }
            }
            
            // 无记忆或未启用，直接返回原始 prompt
            return currentPrompt;
        }
    }
}