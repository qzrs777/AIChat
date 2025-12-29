# Chill AI Mod — 简明使用说明

一行话概述：为游戏添加基于 LLM + 本地 VITS 的 AI 全语音对话（BepInEx 插件），让游戏角色支持实时语音与表情动作联动。

## 特色
- 使用任意兼容的聊天 API（如 OpenRouter/OpenAI）生成对话文本。
- 使用本地部署的 SoVITS/TTS 服务（通过 /tts）生成语音（无需将 TTS Key 传到云端）。
- UI 内可调节音量、窗口尺寸、保存配置，支持拖拽调整大小与精确数值输入。
- 要求 AI 输出严格格式：`[Emotion] ||| JAPANESE TEXT ||| CHINESE TRANSLATION`，插件根据情感切换动作并播放日语语音，显示中文字幕。
- 若 AI 未按格式返回，只显示字幕并以思考动作代替语音（避免错误语言被 TTS 读出）。

## 快速开始（最短路径}
1. 下载
   - 从仓库下载本项目发布的压缩包或编译后的 DLL。
   - 从 (https://github.com/RVC-Boss/GPT-SoVITS) 下载 VITS 模型（本地模型用于生成语音，否则会“无声”）。

2. 拷贝文件到游戏目录
   - 右键游戏 -> 管理 -> 浏览本地文件（或直接定位游戏根目录）。
   - 将压缩包内的 BEPINEX 等指定文件内容复制到游戏根目录。
   - 运行一次游戏（用于生成插件目录结构）。

3. 安装 Mod
   - 在游戏根目录下找到或等待生成的 `plugins` 文件夹，将本项目的 DLL（或整个 ChillAIMod 文件夹）放入 `plugins`。

4. 启动并运行本地 TTS（SoVITS）
   - 启动你本地的 SoVITS/TTS 服务，确保可访问（默认插件配置为 `http://127.0.0.1:9880`）。
   - 如果你的 TTS 启动器只有一个 txt 脚本或没有可执行文件，可创建一个 `.bat` 来启动（示例）
     - 示例（Windows）
       - 打开记事本，写入你的启动命令，保存为 `run_tts.bat`，双击运行。
   - 在游戏内或插件设置中填写 TTS 地址、参考音频路径（RefAudioPath）、EPIT/model 等参数。

## 配置（游戏内设置面板 / Config 文件键）
插件通过 BepInEx 配置项保存，下列为重要项（在设置面板中可直接修改）
- 1. General
  - ApiUrl — 聊天 API 地址（例如 `https://openrouter.ai/api/v1/chat/completions`）
  - APIKey — 聊天 API Key
  - ModelName — 聊天模型（例如 `openai/gpt-3.5-turbo`）
- 2. Audio
  - SoVITS_URL — 本地 TTS 服务地址（如 `http://127.0.0.1:9880`）
  - RefAudioPath — 参考音频路径（.wav）
  - TTS_Service_Path — 本地 TTS 服务路径。在游戏开启时自动启动。
  - PromptText — 参考音频对应的文本（用于声线迁移）
  - PromptLang / TargetLang — 参考文本与目标语言标识
  - VoiceVolume — 语音播放音量（0.0 - 1.0）
- 3. Persona
  - SystemPrompt — 给 LLM 的系统人设（默认内置一段示例人设）
- 4. UI
  - WindowWidth / WindowHeightBase — 控制台窗口宽度与基础高度

设置面板功能
- 音量滑块与精确输入（文本框 + 应用按钮）
- 窗口宽高滑块/文本输入 + 应用
- 保存所有配置按钮（会写入 BepInEx 的 config）

## 游戏内使用
- 打开/关闭控制台：按 F9 或 F10（切换）
- 在控制台输入文本后点击「发送 (Send)」，插件会将用户输入和 SystemPrompt 发给 LLM，接收响应并处理
  - 如果响应符合 `[Emotion] ||| 日语 ||| 中文` 格式
    - 提取情感标签驱动角色动作
    - 用日语部分请求本地 TTS 生成音频并播放（仅当检测到日文假名）
    - 显示中文字幕（自动换行处理）
  - 否则：跳过 TTS，仅显示字幕并以思考动作（Think）作为回退

UI 其他
- 支持拖拽右下角调整窗口大小；放开鼠标会把新尺寸保存到配置。
- 对话文本会自动插入换行以避免超出屏幕。

## AI 输出格式（必须）
必须严格遵守以下三段格式（中间用 `|||` 分隔）
[Emotion] ||| JAPANESE TEXT ||| CHINESE TRANSLATION

示例
- [Wave] ||| やあ、準備はいい？ ||| 嗨，准备好了吗？

可用情感（示例）：Happy、Confused、Sad、Fun、Agree、Drink、Wave、Think

## 调试与常见问题
- 没有声音
  - 确认已下载并正确配置本地 VITS 模型（EPIT、model 名称等）。
  - 检查 SoVITS_URL 是否正确并能从浏览器/工具访问（如 `http://127.0.0.1:9880/tts`）。
  - 确认 RefAudioPath 指向存在的 .wav 文件。
- 插件没有生效 / 找不到 plugins 文件夹
  - 运行一次游戏以生成必要的目录结构；然后将 DLL 放入 `plugins`。
- AI 返回中文但被 TTS 读出发音异常
  - 插件会检测是否含日文假名；若无假名则不会调用 TTS（仅显示字幕）。若你确实想让中文也生成语音，请确保 TTS 支持中文并在插件中调整 TargetLang。
- TTS 报错或返回空音频
  - 检查 TTS 服务日志、请求 JSON 格式（插件在请求体中会传入 text、text_lang、ref_audio_path、prompt_text、prompt_lang）。
  - 插件日志（BepInEx log）会打印 TTS 错误和响应文本，作为排查依据。

## 安全与隐私
- 聊天内容会发送到你配置的聊天 API（如 OpenRouter/OpenAI）；请注意 API Key 与隐私策略。
- TTS 请求默认向本地 SoVITS 服务发起（不上传语音到第三方），更安全且延迟低。

## 示例 Persona（默认已内置）
插件内置了一个示例 SystemPrompt，示范如何强制 AI 始终以日语语音输出，并给出格式约束（请在设置中编辑以适配你的角色）。

