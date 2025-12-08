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

namespace ChillAIMod
{
    [BepInPlugin("com.username.chillaimod", "Chill AI Mod", "1.0.0")]
    public class AIMod : BaseUnityPlugin
    {
        // ================= 【配置项】 =================
        private ConfigEntry<string> _apiKeyConfig;
        private ConfigEntry<string> _modelConfig;
        private ConfigEntry<string> _sovitsUrlConfig;
        private ConfigEntry<string> _refAudioPathConfig;
        private ConfigEntry<string> _promptTextConfig;
        private ConfigEntry<string> _promptLangConfig;
        private ConfigEntry<string> _targetLangConfig;
        private ConfigEntry<string> _personaConfig;

        private const string ChatApiUrl = "https://openrouter.ai/api/v1/chat/completions";

        // ================= 【UI 变量】 =================
        private bool _showInputWindow = false;
        private bool _showSettings = false;
        private Rect _windowRect = new Rect(Screen.width / 2 - 225, Screen.height / 2 - 150, 500, 0);
        private Vector2 _scrollPosition = Vector2.zero;

        private string _playerInput = "";
        private bool _isProcessing = false;

        private AudioSource _audioSource;
        private MonoBehaviour _heroineService;
        private Animator _cachedAnimator;

        private MethodInfo _changeAnimSmoothMethod;
        private MethodInfo _lookInitMethod;
        private MethodInfo _lookAtMethod;

        private bool _isAISpeaking = false;

        // 默认人设
        private const string DefaultPersona = @"
            You are Satone (聪音), a girl who loves writing novels and is full of imagination.
            
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
            Example 3: [Drink] ||| ふぅ…ちょっと休憩しない？画面越しだけど、乾杯。 ||| 呼……要不休息一下？虽然隔着屏幕，干杯。
        ";
        private Vector2 _personaScrollPosition = Vector2.zero;
        void Awake()
        {
            DontDestroyOnLoad(this.gameObject);
            this.gameObject.hideFlags = HideFlags.HideAndDontSave;
            _audioSource = this.gameObject.AddComponent<AudioSource>();
            _audioSource.playOnAwake = false;
            _audioSource.volume = 1.0f;

            // 绑定配置
            _apiKeyConfig = Config.Bind("1. General", "APIKey", "sk-or-v1-PasteYourKeyHere", "OpenRouter API Key");
            _modelConfig = Config.Bind("1. General", "ModelName", "openai/gpt-3.5-turbo", "LLM Model Name");

            _sovitsUrlConfig = Config.Bind("2. Audio", "SoVITS_URL", "http://127.0.0.1:9880", "GPT-SoVITS API URL");
            _refAudioPathConfig = Config.Bind("2. Audio", "RefAudioPath", @"D:\Voice_MainScenario_27_016.wav", "Ref Audio Path");
            _promptTextConfig = Config.Bind("2. Audio", "PromptText", "君が集中した時のシータ波を検出して、リンクをつなぎ直せば元通りになるはず。", "Ref Audio Text");
            _promptLangConfig = Config.Bind("2. Audio", "PromptLang", "ja", "Ref Lang");
            _targetLangConfig = Config.Bind("2. Audio", "TargetLang", "ja", "Target Lang");

            _personaConfig = Config.Bind("3. Persona", "SystemPrompt", DefaultPersona, "System Prompt");

            Logger.LogInfo(">>> AIMod V1.0.0  已加载 <<<");
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
                    e.Use();
                }
            }

            if (_showInputWindow)
            {
                // 动态调整窗口高度
                float targetHeight = _showSettings ? 600f : 200f;
                _windowRect.height = targetHeight;

                GUI.backgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.95f);
                _windowRect = GUI.Window(12345, _windowRect, DrawWindowContent, "Chill AI 控制台");
                GUI.FocusWindow(12345);
            }
        }

        void DrawWindowContent(int windowID)
        {
            // 外层滚动条（负责整个窗口）
            _scrollPosition = GUILayout.BeginScrollView(_scrollPosition);
            GUILayout.BeginVertical();

            // === 顶部状态 ===
            string status = _heroineService != null ? "🟢 核心已连接" : "🔴 正在寻找核心...";
            GUILayout.Label(status);

            // === 设置折叠按钮 ===
            if (GUILayout.Button(_showSettings ? "🔽 收起设置" : "▶️ 展开设置 (API / 人设 / 路径)", GUILayout.Height(25)))
            {
                _showSettings = !_showSettings;
                // 展开时窗口变高，收起时变矮
                _windowRect.height = _showSettings ? 650f : 250f;
            }

            if (_showSettings)
            {
                GUILayout.Space(10);
                GUILayout.BeginVertical("box");
                GUILayout.Label("<b>--- 基础配置 ---</b>");
                GUILayout.Label("API Key:");
                _apiKeyConfig.Value = GUILayout.TextField(_apiKeyConfig.Value);
                GUILayout.Label("Model Name:");
                _modelConfig.Value = GUILayout.TextField(_modelConfig.Value);
                GUILayout.EndVertical();

                GUILayout.Space(5);
                GUILayout.BeginVertical("box");
                GUILayout.Label("<b>--- 语音配置 ---</b>");
                GUILayout.Label("音频路径 (.wav):");
                _refAudioPathConfig.Value = GUILayout.TextField(_refAudioPathConfig.Value);
                GUILayout.Label("音频台词:");
                // 台词框也给个最大高度，防止撑爆
                _promptTextConfig.Value = GUILayout.TextArea(_promptTextConfig.Value, GUILayout.Height(50));
                GUILayout.EndVertical();

                GUILayout.Space(5);
                GUILayout.BeginVertical("box");
                GUILayout.Label("<b>--- 人设 (System Prompt) ---</b>");

                // 【核心修复】给长文本加一个独立的滚动区域
                // GUILayout.Height(150) 限制这个区域只占 150 像素高
                _personaScrollPosition = GUILayout.BeginScrollView(_personaScrollPosition, GUILayout.Height(150));

                // 里面的 TextArea 设置为 ExpandHeight，让它根据文字内容撑开
                // 这样外面的 ScrollView 就会出现滚动条了
                _personaConfig.Value = GUILayout.TextArea(_personaConfig.Value, GUILayout.ExpandHeight(true));

                GUILayout.EndScrollView(); // 结束内层滚动

                GUILayout.EndVertical();

                GUILayout.Space(10);
                if (GUILayout.Button("💾 保存所有配置", GUILayout.Height(30)))
                {
                    Config.Save();
                    Logger.LogInfo("配置已保存！");
                }
                GUILayout.Space(10);
            }

            // === 对话区域 ===
            GUILayout.Space(10);
            GUILayout.Label("<b>与聪音对话:</b>");

            GUI.backgroundColor = Color.white;
            _playerInput = GUILayout.TextField(_playerInput, GUILayout.Height(50));

            GUILayout.Space(5);
            GUI.backgroundColor = _isProcessing ? Color.gray : Color.cyan;

            if (GUILayout.Button(_isProcessing ? "思考中..." : "发送 (Send)", GUILayout.Height(40)))
            {
                if (!string.IsNullOrEmpty(_playerInput) && !_isProcessing)
                {
                    StartCoroutine(AIProcessRoutine(_playerInput));
                    _playerInput = "";
                }
            }

            GUILayout.EndVertical();
            GUILayout.EndScrollView(); // 结束外层滚动

            // 允许拖拽窗口
            GUI.DragWindow();
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
            string jsonBody = $@"{{ ""model"": ""{modelName}"", ""messages"": [ {{ ""role"": ""system"", ""content"": ""{EscapeJson(persona)}"" }}, {{ ""role"": ""user"", ""content"": ""{EscapeJson(prompt)}"" }} ] }}";
            string fullResponse = "";

            // 3. 发送 Chat 请求
            using (UnityWebRequest request = new UnityWebRequest(ChatApiUrl, "POST"))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("Authorization", "Bearer " + apiKey);
                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    fullResponse = ExtractContentRegex(request.downloadHandler.text);
                }
                else
                {
                    myText.text = "API Error: " + request.error;
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

                // 只有当 voiceText 不为空，且看起来像是日语时，才请求 TTS
                // 简单的日语检测：看是否包含假名 (Hiragana/Katakana)
                // 这是一个可选的保险措施
                bool isJapanese = Regex.IsMatch(voiceText, @"[\u3040-\u309F\u30A0-\u30FF]");

                if (!string.IsNullOrEmpty(voiceText) && isJapanese)
                {
                    myText.text = "Generating Voice...";
                    AudioClip downloadedClip = null;
                    yield return StartCoroutine(DownloadVoice(voiceText, (clip) => downloadedClip = clip));

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

        IEnumerator DownloadVoice(string textToSpeak, Action<AudioClip> onComplete)
        {
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

            string jsonBody = $@"{{ ""text"": ""{EscapeJson(textToSpeak)}"", ""text_lang"": ""{_targetLangConfig.Value}"", ""ref_audio_path"": ""{EscapeJson(refPath)}"", ""prompt_text"": ""{EscapeJson(_promptTextConfig.Value)}"", ""prompt_lang"": ""{_promptLangConfig.Value}"" }}";

            using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerAudioClip(url, AudioType.WAV);
                request.SetRequestHeader("Content-Type", "application/json");
                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                    onComplete?.Invoke(DownloadHandlerAudioClip.GetContent(request));
                else
                {
                    Logger.LogError($"TTS Error: {request.error}");
                    onComplete?.Invoke(null);
                }
            }
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

        string ExtractContentRegex(string json)
        {
            try { var match = Regex.Match(json, "\"content\"\\s*:\\s*\"(.*?)\""); return match.Success ? Regex.Unescape(match.Groups[1].Value) : null; }
            catch { return null; }
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
            txt.fontSize = 26; txt.alignment = TextAnchor.UpperLeft; txt.horizontalOverflow = HorizontalWrapMode.Wrap;
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
    }
}