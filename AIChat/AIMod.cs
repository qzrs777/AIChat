using System;
using System.Collections;
using System.Text;
using System.Text.RegularExpressions;
using System.Reflection;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using System.Diagnostics;

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
        private MonoBehaviour _heroineService;
        private Animator _cachedAnimator;

        private MethodInfo _changeAnimSmoothMethod;
        private MethodInfo _lookInitMethod;
        private MethodInfo _lookAtMethod;

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

            Logger.LogInfo(">>> AIMod V1.1.0  已加载 <<<");
        }

        void Update()
        {
            // 自动连接游戏核心
            if (_heroineService == null && Time.frameCount % 100 == 0) FindHeroineService();

            // 口型同步逻辑
            if (_isAISpeaking && _cachedAnimator != null && _audioSource != null)
            {
                bool shouldTalk = _audioSource.isPlaying;

                // 只有状态改变时才调用，优化性能
                if (_cachedAnimator.GetBool("Enable_Talk") != shouldTalk)
                {
                    _cachedAnimator.SetBool("Enable_Talk", shouldTalk);
                }

                // 语音播完，立即归还控制权
                if (!shouldTalk)
                {
                    _isAISpeaking = false;
                    _cachedAnimator.SetBool("Enable_Talk", false);
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
                    // // 每次打开时，重新计算 X 轴居中
                    // if (_showInputWindow)
                    // {
                    //     float margin = 20f;
                    //     _windowRect.x = margin;
                    //     _windowRect.y = margin;
                    // }
                    // e.Use();
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
            string status = _heroineService != null ? "🟢 核心已连接" : "🔴 正在寻找核心...";
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
                GUILayout.EndVertical(); // <--- 必须结束！

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
            GUI.backgroundColor = _isRecording ? Color.red : Color.green;
            string micBtnText = _isRecording ? "🔴 松开结束" : "🎤 按住说话";

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

        // =========================================================================================
        // 【新增辅助函数】确保对话文本（字幕）强制换行，以防过长溢出屏幕。
        // =========================================================================================
        /// <summary>
        /// 在长文本中插入换行符，以确保文本在 UI 中可见。
        /// </summary>
        /// <param name="text">原始文本</param>
        /// <param name="maxLineLength">每行最大字符数</param>
        /// <returns>带有换行符的文本</returns>
        private string InsertLineBreaks(string text, int maxLineLength = 25)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= maxLineLength)
            {
                return text;
            }

            StringBuilder sb = new StringBuilder();
            int currentLength = 0;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                sb.Append(c);
                currentLength++;

                if (currentLength >= maxLineLength && c != '\n')
                {
                    // 检查下一个字符是否已经是换行符，避免双重换行
                    if (i + 1 < text.Length && text[i + 1] != '\n')
                    {
                        sb.Append('\n');
                        currentLength = 0;
                    }
                }

                if (c == '\n')
                {
                    currentLength = 0;
                }
            }
            return sb.ToString();
        }

        // ================= 【新增 ASR 请求逻辑】 =================
        IEnumerator SendAudioToASR(byte[] wavData)
        {
            _isProcessing = true; // 锁定 UI，显示思考中
            string url = _sovitsUrlConfig.Value.TrimEnd('/') + "/asr";

            WWWForm form = new WWWForm();
            form.AddBinaryData("file", wavData, "voice.wav", "audio/wav");

            using (UnityWebRequest www = UnityWebRequest.Post(url, form))
            {
                yield return www.SendWebRequest();

                if (www.result == UnityWebRequest.Result.Success)
                {
                    string json = www.downloadHandler.text;
                    Logger.LogInfo($"[ASR] 服务器返回: {json}");

                    // 简单的 JSON 解析: {"text": "你好"}
                    string recognizedText = ExtractJsonValue(json, "text");

                    if (!string.IsNullOrEmpty(recognizedText))
                    {
                        // 【核心功能】直接发送识别结果给 AI 处理
                        StartCoroutine(AIProcessRoutine(recognizedText));
                    }
                }
                else
                {
                    Logger.LogError($"[ASR] 请求失败: {www.error}");
                }
            }

            _isProcessing = false; // 解锁 UI
        }
        
        // 简易 JSON 提取辅助函数
        private string ExtractJsonValue(string json, string key)
        {
            var match = Regex.Match(json, $"\"{key}\"\\s*:\\s*\"(.*?)\"");
            return match.Success ? Regex.Unescape(match.Groups[1].Value) : "";
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
            ForceShowWindow(originalTextObj);
            originalTextObj.SetActive(false);
            GameObject myTextObj = CreateOverlayText(parentObj);
            Text myText = myTextObj.GetComponent<Text>();
            myText.text = "Thinking..."; myText.color = Color.yellow;

            // 2. 准备请求数据
            string apiKey = _apiKeyConfig.Value;
            string modelName = _modelConfig.Value;
            string persona = _personaConfig.Value;
            string extraJson = _useLocalOllama.Value ? $@",""stream"": false" : "";
            string jsonBody = $@"{{ ""model"": ""{modelName}"", ""messages"": [ {{ ""role"": ""system"", ""content"": ""{EscapeJson(persona)}"" }}, {{ ""role"": ""user"", ""content"": ""{EscapeJson(prompt)}"" }} ]{extraJson} }}";
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
                        fullResponse = ExtractContentFromOllama(request.downloadHandler.text);
                        Logger.LogInfo($"ExtractContentFromOllama: \n\t{fullResponse}");
                    }
                    else
                    {
                        fullResponse = ExtractContentRegex(request.downloadHandler.text);
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

                // 按 ||| 分割
                string[] parts = fullResponse.Split(new string[] { "|||" }, StringSplitOptions.None);

                // 【核心修改：严格的格式检查】
                if (parts.Length >= 3)
                {
                    // 格式正确：[动作] ||| 日语 ||| 中文
                    emotionTag = parts[0].Trim().Replace("[", "").Replace("]", "");
                    voiceText = parts[1].Trim();
                    subtitleText = parts[2].Trim();

                    Logger.LogInfo($"Parse Response With\n\temotionTag: {emotionTag}\n\tvoiceText: {voiceText}\n\tsubtitleText: {subtitleText}");
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
                }

                // 【应用换行】 在将字幕文本显示到 UI 之前，强制插入换行符
                subtitleText = InsertLineBreaks(subtitleText, 25);

                // 只有当 voiceText 不为空，且看起来像是日语时，才请求 TTS
                // 简单的日语检测：看是否包含假名 (Hiragana/Katakana)
                // 这是一个可选的保险措施
                bool isJapanese = Regex.IsMatch(voiceText, @"[\u3040-\u309F\u30A0-\u30FF]");
                Logger.LogInfo($"isJapanese: {isJapanese}");

                if (!string.IsNullOrEmpty(voiceText) && isJapanese)
                {
                    myText.text = "Generating Voice...";
                    AudioClip downloadedClip = null;
                    // 【修改点 1: 移除 apiKey 参数，因为 TTS 是本地部署】
                    yield return StartCoroutine(DownloadVoiceWithRetry(voiceText, (clip) => downloadedClip = clip));

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
                yield return StartCoroutine(CheckTTSHealthOnce((ready) =>
                {
                    _isTTSServiceReady = ready;
                }));
                yield return new WaitForSeconds(TTSHealthCheckInterval);
            }
        }

        IEnumerator CheckTTSHealthOnce(Action<bool> onResult)
        {
            string ttsUrl = _sovitsUrlConfig.Value.TrimEnd('/') + "/tts";
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
                    Logger.LogDebug("[TTS Health] 检测到服务已启动 (返回 422/200 等)");
                }
                else
                {
                    string error = req.error ?? $"HTTP {req.responseCode}";
                    Logger.LogDebug($"[TTS Health] 服务未就绪: {error}");
                }

                onResult?.Invoke(isReady);
            }
        }

        // 【修改点 2: DownloadVoice 协程函数移除 apiKey 参数，并修复 DownloadHandler】
        IEnumerator DownloadVoiceWithRetry(string textToSpeak, Action<AudioClip> onComplete, int maxRetries = 3, float timeoutSeconds = 30f)
        {
            Logger.LogInfo("[TTS] 开始生成语音...");

            string url = _sovitsUrlConfig.Value + "/tts";
            string refPath = _refAudioPathConfig.Value;

            if (!File.Exists(refPath))
            {
                string defaultPath = Path.Combine(BepInEx.Paths.PluginPath, "ChillAIMod", "Voice.wav");
                if (File.Exists(defaultPath)) refPath = defaultPath;
                else
                {
                    Logger.LogError($"[TTS] 找不到参考音频: {refPath}");
                    onComplete?.Invoke(null);
                    yield break;
                }
            }

            string jsonBody = $@"{{ 
                ""text"": ""{EscapeJson(textToSpeak)}"", 
                ""text_lang"": ""{_targetLangConfig.Value}"", 
                ""ref_audio_path"": ""{EscapeJson(refPath)}"", 
                ""prompt_text"": ""{EscapeJson(_promptTextConfig.Value)}"", 
                ""prompt_lang"": ""{_promptLangConfig.Value}"" 
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
                            Logger.LogInfo($"[TTS] 语音生成成功（第 {attempt} 次尝试）（耗时 {requestDuration:F2}s）");
                            onComplete?.Invoke(clip);
                            yield break; // 成功则退出
                        }
                    }

                    Logger.LogWarning($"[TTS] 第 {attempt}/{maxRetries} 次尝试失败（耗时 {requestDuration:F2}s）: {request.error}");
                    if (attempt < maxRetries)
                    {
                        yield return new WaitForSeconds(2f); // 重试前等待
                    }
                }
            }

            Logger.LogError("[TTS] 所有重试均失败，放弃生成语音");
            onComplete?.Invoke(null);
        }

        IEnumerator PlayNativeAnimation(string emotion, AudioClip voiceClip)
        {
            if (_heroineService == null || _changeAnimSmoothMethod == null) yield break;

            Logger.LogInfo($"[动画] 执行: {emotion}");
            float clipDuration = (voiceClip != null) ? voiceClip.length : 3.0f;
            // 1. 归位 (除了喝茶)
            if (emotion != "Drink")
            {
                CallNativeChangeAnim(250);
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
                    CallNativeChangeAnim(250);
                    yield return new WaitForSecondsRealtime(0.5f);
                    animID = 256; // DrinkTea
                    break;

                case "Think":
                    animID = 252; // Thinking
                    break;

                case "Wave":
                    animID = 5001;
                    CallNativeChangeAnim(animID);

                    // 等待抬手
                    yield return new WaitForSecondsRealtime(0.3f);
                    // 强制看玩家
                    ControlLookAt(1.0f, 0.5f);

                    // 等待动作或语音结束 (取长者)
                    float waitTime = Mathf.Max(clipDuration, 2.5f);
                    yield return new WaitForSecondsRealtime(waitTime);

                    // 归位
                    CallNativeChangeAnim(250);
                    RestoreLookAt();

                    _isAISpeaking = false;
                    yield break; // 退出
            }

            // 执行通用动作
            CallNativeChangeAnim(animID);

            // 等待语音播完
            yield return new WaitForSecondsRealtime(clipDuration);

            // 恢复
            RestoreLookAt();
            _isAISpeaking = false;
        }

        // --- 辅助方法 ---
        void CallNativeChangeAnim(int id)
        {
            try { _changeAnimSmoothMethod.Invoke(_heroineService, new object[] { id }); }
            catch (Exception ex) { Logger.LogError($"Anim Error: {ex.Message}"); }
        }

        void ControlLookAt(float scale, float speed)
        {
            try { _lookAtMethod.Invoke(_heroineService, new object[] { scale, speed, 0 }); }
            catch { }
        }

        void RestoreLookAt()
        {
            if (_lookInitMethod != null) try { _lookInitMethod.Invoke(_heroineService, null); } catch { }
        }

        void FindHeroineService()
        {
            var allComponents = FindObjectsOfType<MonoBehaviour>();
            foreach (var comp in allComponents)
            {
                if (comp.GetType().FullName == "Bulbul.HeroineService")
                {
                    _heroineService = comp;
                    _cachedAnimator = comp.GetComponent<Animator>();

                    _changeAnimSmoothMethod = comp.GetType().GetMethod("ChangeHeroineAnimationForInteger", BindingFlags.Public | BindingFlags.Instance);
                    _lookInitMethod = comp.GetType().GetMethod("LookInitSlowly", BindingFlags.Public | BindingFlags.Instance);
                    _lookAtMethod = comp.GetType().GetMethod("ChangeLookScaleAnimation", BindingFlags.Public | BindingFlags.Instance);

                    if (_changeAnimSmoothMethod != null) Logger.LogWarning($"✅ 核心连接成功: {comp.gameObject.name}");
                    return;
                }
            }
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

            AudioClip validClip = TrimAudioClip(_recordingClip, position);

            // 3. 编码并发送
            byte[] wavData = EncodeToWAV(validClip);
            StartCoroutine(SendAudioToASR(wavData));
        }

        AudioClip TrimAudioClip(AudioClip original, int endPosition)
        {
            float[] data = new float[endPosition * original.channels];
            original.GetData(data, 0);

            AudioClip newClip = AudioClip.Create("TrimmedVoice", endPosition, original.channels, original.frequency, false);
            newClip.SetData(data, 0);
            return newClip;
        }

        string ExtractContentRegex(string json)
        {
            try { var match = Regex.Match(json, "\"content\"\\s*:\\s*\"(.*?)\""); return match.Success ? Regex.Unescape(match.Groups[1].Value) : null; }
            catch { return null; }
        }

        private string ExtractContentFromOllama(string jsonResponse)
        {
            try
            {
                var match = Regex.Match(jsonResponse, "\"content\"\\s*:\\s*\"([^\"]*)\"");
                if (match.Success)
                {
                    return Regex.Unescape(match.Groups[1].Value);
                }
                return null;
            }
            catch (Exception ex)
            {
                Logger.LogError($"[Ollama] 解析失败: {ex.Message}");
                return null;
            }
        }

        string EscapeJson(string s)
        {
            return s?.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "").Replace("\n", "\\n") ?? "";
        }

        GameObject CreateOverlayText(GameObject parent)
        {
            GameObject go = new GameObject(">>> AI_TEXT <<<");
            go.transform.SetParent(parent.transform, false);
            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.sizeDelta = Vector2.zero;
            Text txt = go.AddComponent<Text>();
            txt.fontSize = 26;
            txt.alignment = TextAnchor.UpperCenter;
            txt.horizontalOverflow = HorizontalWrapMode.Wrap;
            Font f = Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (f == null) f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (f != null) txt.font = f;
            return go;
        }

        void ForceShowWindow(GameObject target)
        {
            target.SetActive(true);
            var p = target.transform.parent;
            while (p != null && p.name != "Canvas")
            {
                p.gameObject.SetActive(true);
                p = p.parent;
            }
            foreach (var c in target.GetComponentsInParent<CanvasGroup>()) c.alpha = 1f;
            target.transform.parent.parent.localScale = Vector3.one;
        }

        void OnApplicationQuit()
        {
            Logger.LogInfo("[Chill AI Mod] 退出中...");
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
                    KillProcessTree(_launchedTTSProcess);
                    Logger.LogInfo("TTS 服务已关闭");
                }
                catch (Exception ex)
                {
                    Logger.LogWarning($"关闭 TTS 服务时出错: {ex.Message}");
                }
            }
        }

        private void KillProcessTree(Process process)
        {
            if (process == null || process.HasExited) return;

            try
            {
                int pid = process.Id;
                Logger.LogInfo($"[TTS Cleanup] 使用 taskkill 终止进程树 (PID: {pid})");

                // 在新进程中执行 taskkill /T /F /PID <pid>
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = "taskkill",
                    Arguments = $"/T /F /PID {pid}", // /T = 终止子进程, /F = 强制
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                using (Process killer = Process.Start(psi))
                {
                    killer.WaitForExit(3000); // 等待最多 3 秒
                }

                Logger.LogInfo($"[TTS Cleanup] taskkill 执行完毕 (PID: {pid})");
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"[TTS Cleanup] taskkill 失败: {ex.Message}");
            }
        }

        // ================= 【新增 WAV 编码工具】 =================
        private byte[] EncodeToWAV(AudioClip clip)
        {
            using (MemoryStream stream = new MemoryStream())
            {
                // 1. 获取数据
                float[] samples = new float[clip.samples * clip.channels];
                clip.GetData(samples, 0);

                // 2. 写入 WAV 头 (44 bytes)
                int hz = clip.frequency;
                int channels = clip.channels;
                int samplesCount = samples.Length;

                Byte[] riff = Encoding.UTF8.GetBytes("RIFF");
                stream.Write(riff, 0, 4);

                Byte[] chunkSize = BitConverter.GetBytes(samplesCount * 2 + 36);
                stream.Write(chunkSize, 0, 4);

                Byte[] wave = Encoding.UTF8.GetBytes("WAVE");
                stream.Write(wave, 0, 4);

                Byte[] fmt = Encoding.UTF8.GetBytes("fmt ");
                stream.Write(fmt, 0, 4);

                Byte[] subChunk1 = BitConverter.GetBytes(16);
                stream.Write(subChunk1, 0, 4);

                UInt16 one = 1;
                Byte[] audioFormat = BitConverter.GetBytes(one);
                stream.Write(audioFormat, 0, 2);

                Byte[] numChannels = BitConverter.GetBytes(channels);
                stream.Write(numChannels, 0, 2);

                Byte[] sampleRate = BitConverter.GetBytes(hz);
                stream.Write(sampleRate, 0, 4);

                Byte[] byteRate = BitConverter.GetBytes(hz * channels * 2);
                stream.Write(byteRate, 0, 4);

                UInt16 blockAlign = (ushort)(channels * 2);
                stream.Write(BitConverter.GetBytes(blockAlign), 0, 2);

                UInt16 bps = 16;
                Byte[] bitsPerSample = BitConverter.GetBytes(bps);
                stream.Write(bitsPerSample, 0, 2);

                Byte[] datastring = Encoding.UTF8.GetBytes("data");
                stream.Write(datastring, 0, 4);

                Byte[] subChunk2 = BitConverter.GetBytes(samplesCount * 2);
                stream.Write(subChunk2, 0, 4);

                // 3. 写入数据 (将 float -1.0~1.0 转换为 short -32768~32767)
                Int16[] intData = new Int16[samplesCount];
                Byte[] bytesData = new Byte[samplesCount * 2];
                int rescaleFactor = 32767;

                for (int i = 0; i < samplesCount; i++)
                {
                    intData[i] = (short)(samples[i] * rescaleFactor);
                    Byte[] byteArr = BitConverter.GetBytes(intData[i]);
                    byteArr.CopyTo(bytesData, i * 2);
                }

                stream.Write(bytesData, 0, bytesData.Length);
                return stream.ToArray();
            }
        }
    }
}