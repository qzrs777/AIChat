# Chill AI Mod — 简明使用说明

一行话概述：为游戏添加基于 LLM + 本地 VITS 的 AI 全语音对话（BepInEx 插件），让游戏角色支持实时语音与表情动作联动。

## 特色
- 使用任意兼容的聊天 API（如 OpenRouter/OpenAI）生成对话文本。
- 使用本地部署的 SoVITS/TTS 服务（通过 /tts）生成语音（无需将 TTS Key 传到云端）。
- UI 内可调节音量、窗口尺寸、保存配置，支持拖拽调整大小与精确数值输入。
- 要求 AI 输出严格格式：`[Emotion] ||| JAPANESE TEXT ||| CHINESE TRANSLATION`，插件根据情感切换动作并播放日语语音，显示中文字幕。
- 若 AI 未按格式返回，只显示字幕并以思考动作代替语音（避免错误语言被 TTS 读出）。

## 安装 Mod 本体
1. 下载 Mod
   - 从 [Releases](https://github.com/qzrs777/AIChat/releases) 下载本项目发布的压缩包，并解压。
     - 注：其核心部分 `AIChat.dll` 也可从本仓库源码构建得到。

2. 安装 Mod
   - 在 Steam 右键游戏 -> 管理 -> 浏览本地文件（或直接定位游戏根目录）。
   - 将压缩包内的 `BepInEx_*` 下的内容复制到游戏根目录。
     - Linux 用户请注意：Mod 能被加载的原理是，Windows 中的一些程序在启动时，同目录下的 DLL 文件（这里的是 `winhttp.dll`）比原本的 DLL 文件具有更高的优先级，从而被加载；但是在 Linux 下，Proton 自己的 DLL 文件具有更高的优先级，会无视同目录下的 `winhttp.dll`。所以，你需要在 Steam 的此游戏的设置里，将启动选项填写为 `WINEDLLOVERRIDES="winhttp=n,b" %command%` （其中 `winhttp` 就是 `winhttp.dll` 的文件名）。
   - 运行一次游戏（用于生成插件目录结构，这包括 `BepInEx` 目录下的 `config`、`core`、`patchers`、`plugins` 等目录）。
   - 将 `AIChat.dll` 放入 `BepInEx` 下的 `plugins` 目录中。

3. 配置 Mod
   - 打开游戏，按 F9 键调出 Mod 的界面。
   - 配置好 API URL 与 API Key 以及 Model Name 并保存，此时就可以在“与聪音对话”的文本框里进行对话了（仅文字；下一节将配置语音）。

## 语音配置（可选）
本项目依赖 [GPT-SoVITS](https://github.com/RVC-Boss/GPT-SoVITS) 生成语音，而它的文档略显混乱，所以这里提供较为详细的说明。

1. 安装 GPT-SoVITS：
   - Windows 用户：根据 GPT-SoVITS 的[文档](https://www.yuque.com/baicaigongchang1145haoyuangong/ib3g1e/dkxgpiy9zb96hob4)，直接下载整合包，解压后运行 `run_api.bat` 即可。如果没有 `run_api.bat`，可以自己建立一个 `run_api.bat.txt` 文件，编辑内容如下：
     ```bat
     @echo off
     .\runtime\python.exe api_v2.py -a 127.0.0.1 -p 9880
     pause
     ```
     然后重命名文件，将 `.txt` 后缀去掉即可。
   - Linux 用户：Nvidia 显卡用户推荐使用 Docker，因为 Docker 具有稳定、易迁移、方便统一管理的特性。若不想使用 Docker 或显卡不是 Nvidia 的，则需要使用 conda 来运行，请自行参考 [GPT-SoVITS 的 README](https://github.com/RVC-Boss/GPT-SoVITS#linux)，**注意不是文档也不是 User guide**。以下是使用 Docker 的步骤：
     - 安装 Docker、Docker Compose、Nvidia Container Toolkit 三件套，方法参见 [Debian | Docker Docs](https://docs.docker.com/engine/install/debian/#installation-methods)、[Plugin | Docker Docs](https://docs.docker.com/compose/install/linux/#install-using-the-repository) 和 [Installing the NVIDIA Container Toolkit](https://docs.nvidia.com/datacenter/cloud-native/container-toolkit/latest/install-guide.html)
     - 克隆 [GPT-SoVITS](https://github.com/RVC-Boss/GPT-SoVITS)：
     ```bash
     git clone --depth=1 https://github.com/RVC-Boss/GPT-SoVITS
     cd GPT-SoVITS
     ```
     - 运行 APIv2 服务：
       ```bash
       docker compose run --rm --service-ports GPT-SoVITS-CU126 python api_v2.py -a 0.0.0.0 -p 9880
       ```
       （注：Nvidia 50 系显卡请将上面的 `126` 改为 `128`）

2. 放置音频文件
   - 将前面下载过的 Mod 压缩包中的 `Voice_MainScenario_27_016.wav` 放到 `GPT-SoVITS` 的根目录下（对于 Windows 用户是整合包解压后的根目录，对于 Linux 用户是 Git 仓库的根目录）。
   - 测试：在浏览器打开[测试链接](http://127.0.0.1:9880/tts?text=%E3%81%93%E3%82%93%E3%81%AB%E3%81%A1%E3%81%AF%E3%80%81%E3%81%8A%E5%85%83%E6%B0%97%E3%81%A7%E3%81%99%E3%81%8B%EF%BC%9F%E4%BB%8A%E6%97%A5%E3%82%82%E4%B8%80%E7%B7%92%E3%81%AB%E9%A0%91%E5%BC%B5%E3%82%8A%E3%81%BE%E3%81%97%E3%82%87%E3%81%86%EF%BC%81&text_lang=ja&ref_audio_path=Voice_MainScenario_27_016.wav&prompt_text=%E5%90%9B%E3%81%8C%E9%9B%86%E4%B8%AD%E3%81%97%E3%81%9F%E6%99%82%E3%81%AE%E3%82%B7%E3%83%BC%E3%82%BF%E6%B3%A2%E3%82%92%E6%A4%9C%E5%87%BA%E3%81%97%E3%81%A6%E3%80%81%E3%83%AA%E3%83%B3%E3%82%AF%E3%82%92%E3%81%A4%E3%81%AA%E3%81%8E%E7%9B%B4%E3%81%9B%E3%81%B0%E5%85%83%E9%80%9A%E3%82%8A%E3%81%AB%E3%81%AA%E3%82%8B%E3%81%AF%E3%81%9A%E3%80%82&prompt_lang=ja&speed_factor=1.0)
   > 上面浏览器打开的是经过转义的链接，下面是测试链接的实际内容：
   > ```url
   > http://127.0.0.1:9880/tts?text=こんにちは、お元気ですか？今日も一緒に頑張りましょう！&text_lang=ja&ref_audio_path=Voice_MainScenario_27_016.wav&prompt_text=君が集中した時のシータ波を検出して、リンクをつなぎ直せば元通りになるはず。&prompt_lang=ja&speed_factor=1.0
   > ```
   > 它的基本作用是让这个 tts 服务模仿 `ref_audio_path` 所指定的音频文件（台词为 `prompt_text` 的值）来合成 `text` 的语音音频。
   > 实际上，这里测试使用的是 GTP-SoVITS 的 WebAPI 的 GET 用法，详见 [`api_v2.py`](https://github.com/RVC-Boss/GPT-SoVITS/blob/main/api_v2.py) 的注释。

   稍等片刻，将会下载一个大约 300 KiB 大小的 `tts.wav` 文件，播放它应当能清晰地听到三句与游戏角色相似的日语语音，时长约 5 秒。
   > 或者，直接在命令行用 `ffplay`（由 FFmpeg 提供）：
   > ```bash
   > ffplay -nodisp -autoexit 'http://127.0.0.1:9880/tts?text=こんにちは、お元気ですか？今日も一緒に頑張りましょう！&text_lang=ja&ref_audio_path=Voice_MainScenario_27_016.wav&prompt_text=君が集中した時のシータ波を検出して、リンクをつなぎ直せば元通りになるはず。&prompt_lang=ja&speed_factor=1.0&streaming_mode=True'
   > ```

3. 在 Mod 中配置
  - 确保上一步测试成功后，在游戏按 F9 键调出 Mod 的界面中，将 `音频路径(.wav)` 的值改为 `Voice_MainScenario_27_016.wav`，并且勾选 `不检测音频文件路径` 即可。
  - 下面两个参数默认都已填好，**一般不要改动**，如下：
    - 音频台词（即 wav 音频文件的原文）：`君が集中した時のシータ波を検出して、リンクをつなぎ直せば元通りになるはず。`
    - TTS Service Url：`http://127.0.0.1:9880`

## 配置（游戏内设置面板 / Config 文件键）
插件通过 BepInEx 配置项保存，下列为重要项（在设置面板中可直接修改）
- 1. General
  - ApiUrl — 聊天 API 地址（例如 `https://openrouter.ai/api/v1/chat/completions`, `http://127.0.0.1:11434/v1/chat/completions` for ollama, `https://generativelanguage.googleapis.com/v1beta/openai/chat/completions` for gemini）
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

