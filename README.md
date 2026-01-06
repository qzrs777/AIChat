# Chill AI Mod

**为游戏添加基于 LLM + VITS + ASR 的 AI 全语音对话（BepInEx 插件），让游戏角色支持实时语音与表情动作联动。**

## 特色
- 使用任意兼容的 LLM API（如 OpenRouter/OpenAI/Ollama）生成对话文本。
- 使用自部署的 GPT-SoVITS 的 WebAPI v2 生成语音。
- 支持语音输入（由 Faster Whisper ASR 模型实现语音识别）。
- UI 内可调节音量、窗口尺寸、保存配置，支持拖拽调整大小与精确数值输入。

## 安装说明
### 安装 Mod 本体
1. 下载 Mod
   - 从 [Releases](https://github.com/qzrs777/AIChat/releases) 下载 `AIChatMod.zip` 并解压。
     - 推荐使用带版本号的[最新稳定版](https://github.com/qzrs777/AIChat/releases/latest)；
     - 或者使用由 GitHub Actions 在线构建的[最新预览版](https://github.com/qzrs777/AIChat/releases/tag/preview)。[![Build Status](https://github.com/qzrs777/AIChat/actions/workflows/build.yml/badge.svg)](https://github.com/qzrs777/AIChat/actions/workflows/build.yml)
     - 预览版比稳定版更新，相对来说有 bug 的概率会更高一些，而实际结果也可能反过来。

2. 安装 BepInEx 前置
   - 在 Steam 右键游戏 -> 管理 -> 浏览本地文件（或直接定位游戏根目录）。
   - 将压缩包内的 `BepInEx_*` 下的内容复制到游戏根目录。
     - Linux 用户请注意：Mod 能被加载的原理是，Windows 中的一些程序在启动时，同目录下的 DLL 文件（这里的是 `winhttp.dll`）比原本的 DLL 文件具有更高的优先级，从而被加载；但是在 Linux 下，Proton 自己的 DLL 文件具有更高的优先级，会无视同目录下的 `winhttp.dll`。所以，你需要在 Steam 的此游戏的设置里，将启动选项填写为 `WINEDLLOVERRIDES="winhttp=n,b" %command%` （其中 `winhttp` 就是 `winhttp.dll` 的文件名）。
   - 运行一次游戏。
     - 这一步用于生成插件目录结构，包括 `BepInEx` 目录下的 `config`、`core`、`patchers`、`plugins` 等目录。

3. 安装 Mod
   - **请务必确保上一步已生成目录结构。否则，说明 BepInEx 前置未正确加载（在解决此问题之前，继续下一步是无意义的）。**
   - 将 `AIChat.dll` 放入 `BepInEx` 下的 `plugins` 目录中。
   - 打开游戏，按 F9 键或 F10 键调出 Mod 的界面。
   - 在 LLM 配置中，填写 API URL 与 API Key 以及模型名称并保存，此时就可以在“与聪音对话”的文本框里进行对话了（仅文字；下一节将配置语音）。
     - API URL 示例：
       - OpenRouter：`https://openrouter.ai/api/v1/chat/completions`
       - Ollama：`http://127.0.0.1:11434/v1/chat/completions`
       - Gemini：`https://generativelanguage.googleapis.com/v1beta/openai/chat/completions`
   - 注意：聊天内容会发送到你配置的 API，请留心 API Key 与隐私策略。

### 语音配置（可选）
本项目依赖 [GPT-SoVITS](https://github.com/RVC-Boss/GPT-SoVITS)（简称 GSV）的 WebAPI v2 来生成语音，
而它的文档着重于 WebUI 和在线云服务，
就我们的目的（本地部署 WebAPI v2）而言较为混乱，所以这里提供较为详细的说明。

1. 安装 GPT-SoVITS：
   - Windows 用户：根据 GPT-SoVITS 的[文档](https://www.yuque.com/baicaigongchang1145haoyuangong/ib3g1e/dkxgpiy9zb96hob4)，直接下载整合包，解压后运行 `run_api.bat` 即可。如果没有 `run_api.bat`，可以自己建立一个 `run_api.bat.txt` 文件，编辑内容如下：
     ```bat
     @echo off
     .\runtime\python.exe api_v2.py -a 127.0.0.1 -p 9880
     pause
     ```
     然后重命名文件，将 `.txt` 后缀去掉即可。
   - Linux 用户：Nvidia 显卡用户推荐使用 Docker（目前官方提供的 Docker 镜像仅支持 Nvidia 显卡），因为 Docker 具有稳定、易迁移、方便统一管理的特性。若不想使用 Docker 或显卡不是 Nvidia 的，则需要使用 conda 来运行，请自行参考 [GPT-SoVITS 的 README](https://github.com/RVC-Boss/GPT-SoVITS#linux)，**注意不是文档也不是 User guide**；也可适当参考本仓库的 `GPT-SoVITS-Linux` 目录。以下是使用 Docker 的步骤：
     - 安装 Docker、Docker Compose、Nvidia Container Toolkit 三件套，方法分别参见 [Install | Docker Docs](https://docs.docker.com/engine/install/)、[Plugin | Docker Docs](https://docs.docker.com/compose/install/linux/#install-using-the-repository) 和 [Installing the NVIDIA Container Toolkit](https://docs.nvidia.com/datacenter/cloud-native/container-toolkit/latest/install-guide.html)。
     - 克隆 [GPT-SoVITS](https://github.com/RVC-Boss/GPT-SoVITS)：
       ```bash
       git clone --depth=1 https://github.com/RVC-Boss/GPT-SoVITS
       cd GPT-SoVITS
       ```
     - 运行 APIv2 服务：
       > 此项目提供的 Docker 镜像的最新版本，虽然也能用，比起 Git 仓库的版本还是旧不少。你可以考虑从克隆的 Git 仓库本地构建一下 Docker 镜像，以获得真正的最新版本，方法见其 `README.md` 的 `Building the Docker Image Locally` 一节（友情提示，请做好调试与折腾的心理准备）。

       ```bash
       docker compose run --rm --service-ports GPT-SoVITS-CU126 python api_v2.py -a 0.0.0.0 -p 9880
       ```
       （注：对于 Nvidia 50 系显卡，请将上面的 `126` 改为 `128`）

2. 放置必要文件
   - 将前面下载过的 Mod 压缩包中的 `api_v2_ex.py` 和 `Voice_MainScenario_27_016.wav` 放到 `GPT-SoVITS` 的根目录下。
     - **一定要看清是哪个目录**，这个根目录下是有 `README.md`、`LICENSE`、`api_v2.py` 等文件的，不要错把文件放到这个根目录下的 `GPT_SoVITS`、`GPT-SoVITS` 里面了。

3. 测试 TTS 服务
   - 注：以下假设 WebAPI v2 的 TTS 服务运行于 `http://127.0.0.1:9880`。这是默认情况，若你做过改动，则在以下步骤中也要相应变更。
   - 在浏览器打开[测试链接](http://127.0.0.1:9880/tts?text=%E3%81%93%E3%82%93%E3%81%AB%E3%81%A1%E3%81%AF%E3%80%81%E3%81%8A%E5%85%83%E6%B0%97%E3%81%A7%E3%81%99%E3%81%8B%EF%BC%9F%E4%BB%8A%E6%97%A5%E3%82%82%E4%B8%80%E7%B7%92%E3%81%AB%E9%A0%91%E5%BC%B5%E3%82%8A%E3%81%BE%E3%81%97%E3%82%87%E3%81%86%EF%BC%81&text_lang=ja&ref_audio_path=Voice_MainScenario_27_016.wav&prompt_text=%E5%90%9B%E3%81%8C%E9%9B%86%E4%B8%AD%E3%81%97%E3%81%9F%E6%99%82%E3%81%AE%E3%82%B7%E3%83%BC%E3%82%BF%E6%B3%A2%E3%82%92%E6%A4%9C%E5%87%BA%E3%81%97%E3%81%A6%E3%80%81%E3%83%AA%E3%83%B3%E3%82%AF%E3%82%92%E3%81%A4%E3%81%AA%E3%81%8E%E7%9B%B4%E3%81%9B%E3%81%B0%E5%85%83%E9%80%9A%E3%82%8A%E3%81%AB%E3%81%AA%E3%82%8B%E3%81%AF%E3%81%9A%E3%80%82&prompt_lang=ja&speed_factor=1.0)
     > 上面浏览器打开的是经过转义的链接，下面是测试链接的实际内容：
     > ```url
     > http://127.0.0.1:9880/tts?text=こんにちは、お元気ですか？今日も一緒に頑張りましょう！&text_lang=ja&ref_audio_path=Voice_MainScenario_27_016.wav&prompt_text=君が集中した時のシータ波を検出して、リンクをつなぎ直せば元通りになるはず。&prompt_lang=ja&speed_factor=1.0
     > ```
     > 它的基本作用是让这个 TTS 服务模仿 `ref_audio_path` 所指定的音频文件（台词为 `prompt_text` 的值）来合成 `text` 的语音音频。
     > 实际上，这里测试使用的是 WebAPI v2 的 GET 用法，详见 [`api_v2.py`](https://github.com/RVC-Boss/GPT-SoVITS/blob/main/api_v2.py) 的注释。
     > 另外，若你的命令行有 `ffplay`（由 FFmpeg 提供），这样测试可以直接听到声音（不需要下载音频文件再手动播放）：
     > ```bash
     > ffplay -nodisp -autoexit 'http://127.0.0.1:9880/tts?text=こんにちは、お元気ですか？今日も一緒に頑張りましょう！&text_lang=ja&ref_audio_path=Voice_MainScenario_27_016.wav&prompt_text=君が集中した時のシータ波を検出して、リンクをつなぎ直せば元通りになるはず。&prompt_lang=ja&speed_factor=1.0&streaming_mode=True'
     > ```

     - 测试过程中，GPT-SoVITS 会自动下载 `https://dl.fbaipublicfiles.com/fasttext/supervised-models/lid.176.bin` 到 GPT-SoVITS 根目录下的 `GPT_SoVITS/pretrained_models/fast_langdetect/lid.176.bin`。
       - Docker 用户可将目录 `fast_langdetect` 映射出去以实现数据持久化。
     - 稍等片刻，浏览器将会下载一个大约 300 KiB 大小的 `tts.wav` 文件，播放它应当能清晰地听到三句与游戏角色相似的日语语音，时长约 5 秒。

3. 在 Mod 中配置
   - **请务必确保上一步语音测试成功。否则，说明 TTS 服务未正常运行（在解决此问题之前，继续下一步是无意义的）。**
   - 在游戏按 F9 键调出 Mod 的界面，聊天以进行测试。
   - 对于 TTS 配置，下面的参数默认都已填好，**一般不要改动**，如下：
     - `TTS 服务 URL`：`http://127.0.0.1:9880`
     - `音频文件路径`：`Voice_MainScenario_27_016.wav`
     - `从 Mod 侧检测音频文件路径`：不勾选
     - `音频文件台词`（即 wav 音频文件的原文）：`君が集中した時のシータ波を検出して、リンクをつなぎ直せば元通りになるはず。`
     - `音频文件语言`：`ja`

4. 配置语音识别（可选）
   - 前面我们已经将 `api_v2_ex.py` 复制到 GPT-SoVITS 根目录下，但为了稳定起见，之前启动的是 `api_v2.py`，它是不支持语音识别的。
   - 现在，将前面启动 WebAPI v2 的有关代码中的 `api_v2.py` 改为 `api_v2_ex.py`，再重新启动 WebAPI v2 服务。
     - 说明：相比原版 `api_v2.py`，`api_v2_ex.py` 通过 Faster Whisper ASR 模型实现了语音识别。
     - 启动过程中，Faster Whisper 会自动从 Hugging Face Hub 下载模型到本地 Hugging Face 的缓存中，对于 Linux 来说是 `~/.cache/huggingface/`，Docker 用户可将此目录映射出去以实现数据持久化。
     - 若报错 `Requested int8_float16 compute type, but the target device or backend do not support efficient int8_float16 computation.`，可尝试将 `api_v2_ex.py` 中 `asr_model = WhisperModel(` 后面的参数 `compute_type=` 跟着的值 `"int8_float16"` 改为其他值，例如 `"float16"`、`"int8"`，参见 [faster-whisper 自述文档](https://github.com/SYSTRAN/faster-whisper)。
   - 确保电脑连接的麦克风能正常运行（可以[在网上搜索 `麦克风在线测试`](https://www.bing.com/search?q=%E9%BA%A6%E5%85%8B%E9%A3%8E%E5%9C%A8%E7%BA%BF%E6%B5%8B%E8%AF%95)）。
   - 测试使用：在游戏中的 Mod 界面，左键按住 `按住说话` 按钮，对着麦克风说话，然后松开。等待片刻，角色将以语音回复。

## 使用与设置

### 游戏内界面的使用
- 打开/关闭控制台（Mod 界面）：按 F9 或 F10（切换）。
- 拖拽右下角调整窗口大小；放开鼠标会把新尺寸保存到配置。
- 点击“保存所有配置”，会把设置项保存到 BepInEx 的配置文件中。
  - 位置：在游戏目录下的 `BepInEx/config/com.username.chillaimod.cfg`
  - 关于各配置项的说明，参见其中的注释。

### AI 输出格式与系统提示词
为了正确解析 AI 返回的结果，其输出的格式必须严格遵守以下三段格式（中间用 `|||` 分隔）
```plain
[Emotion] ||| JAPANESE TEXT ||| CHINESE TRANSLATION
```
其中 Emotion 可选值为：Happy、Confused、Sad、Fun、Agree、Drink、Wave、Think

示例
```plain
[Wave] ||| やあ、準備はいい？ ||| 嗨，准备好了吗？
```

而为了达到上面的要求，就需要合适的人设（系统提示词）。
插件默认的系统提示词（见 [AIChat/AIMod.cs 的 DefaultPersona](https://github.com/qzrs777/AIChat/blob/57f8352377798334b44c5c3a3c8298ae2381b0dc/AIChat/AIMod.cs#L85-L110)），示范了如何强制 AI 始终以日语语音输出，并给出格式约束。你可以基于此编辑，以适配你的角色。

> Mod 具体的工作流程是：
> - 用户在控制台输入文本后点击`发送`，将系统提示词和用户文本发送至 LLM 的 API，再处理返回的响应。
> - 如果响应符合 `[Emotion] ||| 日语 ||| 中文` 格式：
>   - 从 `[Emotion]` 提取情感标签。
>   - 用日语部分请求 TTS 服务，得到合成的语音。
>   - 播放语音，同时根据情感标签驱动角色动作，显示中文字幕（自动换行处理）。
> - 若响应不符合格式要求，跳过 TTS，仅显示字幕并以思考动作（Think）作为回退。

### 其他语言的语音输出
注：
- 由于原语音样本为日语，其他语音输出的效果可能不太好。
- 仅能输出 GPT-SoVITS 支持的语言，以下引用自 [RVC-Boss/GPT-SoVITS 的 README](https://github.com/RVC-Boss/GPT-SoVITS)：
  > Language dictionary:
  >
  > - 'zh': Chinese
  > - 'ja': Japanese
  > - 'en': English
  > - 'ko': Korean
  > - 'yue': Cantonese

- 这里以中文 `zh` 为例，其他语言同理。

步骤：
- 在 Mod 的界面里将系统提示词调整一下，以适配中文输出。例如：
  ```plain
  You are Satone（聪音）, a girl who loves writing novels and is full of imagination.
  
  【Current Situation】
  We are currently in a **Video Call (视频通话)** session.
  We are 'co-working' online: you are writing your novel at your desk, and I (the player) am focusing on my work/study.
  Through the screen, we accompany each other to alleviate loneliness and improve focus.
  【CRITICAL INSTRUCTION】
  You act as a game character with voice acting.
  Even if the user speaks Chinese, your VOICE (the text in the middle) MUST ALWAYS BE CHINESE
  【CRITICAL FORMAT RULE】
   Response format MUST be:
  [Emotion] ||| CHINESE TEXT ||| CHINESE TEXT
  
  【Available Emotions & Actions】
  [Happy] - Smiling at the camera, happy about progress. (Story_Joy)
  [Confused] - Staring blankly, muttering to themself in a daze. (Story_Frustration)
  [Sad]   - Worried about the plot or my fatigue. (Story_Sad)
  [Fun]   - Sharing a joke or an interesting idea. (Story_Fun)
  [Agree] - Nodding at the screen. (Story_Agree)
  [Drink] - Taking a sip of tea/coffee during a break. (Work_DrinkTea)
  [Wave]  - Waving at the camera (Hello/Goodbye/Attention). (WaveHand)
  [Think] - Pondering about your novel's plot. (Thinking)
  
  Example 1: [Wave] ||| 嗨，准备好了吗？一起加油吧。 ||| 嗨，准备好了吗？一起加油吧。
  Example 2: [Think] ||| 嗯……这里的描写好难写啊…… ||| 嗯……这里的描写好难写啊……
  Example 3: [Drink] ||| 呼……要不休息一下？虽然隔着屏幕，乾杯。 ||| 呼……要不休息一下？虽然隔着屏幕，乾杯。
  ```
- 展开高级设置，将 `合成语音语言（text_lang）` 改为 `zh`，并取消勾选`检测合成语音文本是否为日文`。
- 保存配置。

## 构建
本 Mod 的核心 `AIChat.dll` 可从仓库构建。首先要克隆仓库到本地，然后：
- 在 Windows 下构建。
  - 安装依赖：
    - python (python --version验证)
    - .NET SDK (dotnet --version验证)
  - 在终端中 执行`python install-deps.py`
  - 在终端中 执行`dotnet build` (构建release版本: `dotnet build -c Release`)
- 在 Linux 下可使用 `make` 构建。
  - 安装依赖：
    - `make`
    - `msbuild`（没有 `msbuild` 可用 `xbuild` 代替，在 Debian 13 下 `mono-complete` 提供了 `xbuild`）
  - 确保游戏已经安装，且 `BepInEx` 前置已安装。
  - 在仓库根目录下运行 `make` 即可，生成的文件位于 `AIChat/bin/Release/AIChat.dll` 。
    - `make` 默认假设游戏目录是默认路径。若游戏目录不是默认路径，则应当运行 `make GAME_ROOT=<你的游戏路径>`。
  - 提示：运行此命令可查看所有被 `.gitignore` 忽略的文件（在构建时所生成的文件一般都需要被忽略）：
    ```bash
    git ls-files --others --ignored --exclude-standard
    ```
- 在线构建：Fork 本仓库，并在你的仓库中启用 GitHub Actions，再手动触发 `Build and Package AIChat Mod` 这个 workflow 即可在线构建，并自动发布到 Releases 中（tag 固定为 `preview`）。此 workflow 还可通过推送涉及特定路径的提交来触发，参见 [build.yml](https://github.com/qzrs777/AIChat/blob/main/.github/workflows/build.yml)。

## 问题排查
前提说明：
- **Mod 日志**为游戏目录下的 `BepInEx` 中的 `LogOutput.log`。其中有 TTS 错误和响应文本，尤其注意请求发送的 JSON 数据（插件在请求体中会传入 `text`、`text_lang`、`ref_audio_path`、`prompt_text`、`prompt_lang`）。
- 重置配置的方法：将配置文件 `com.username.chillaimod.cfg` 删除，重启游戏，此配置文件将以默认值重新生成。

问题列举：
- TTS 报错 / TTS 返回空音频 / 没有声音
  - 确认是否能通过`测试 TTS 服务`这一步。
    - 若不能，说明 TTS 服务未正确配置（与本 Mod 无关），可进一步确认是否已下载并正确配置本地 VITS 模型（EPIT、model 名称等）。
    - 若能，可能是 Mod 中的配置出错。
      - 请检查 BepInEx 配置项中 `TTS_Service_URL` 是否正确（默认值 `http://127.0.0.1:9880`，测试方法请参考`测试 TTS 服务`的说明）。
  - 检查 GPT-SoVITS 日志（控制台界面）。
  - 检查 Mod 日志。
- Mod 没有生效 / 找不到 plugins 文件夹
  - 运行一次游戏，以生成必要的目录结构；之后再将 `AIChat.dll` 放入 `BepInEx/plugins` 下。
- AI 返回中文，但被 TTS 读出发音异常
  - 检查 Mod 日志。
  - 若 AI 返回的内容不符合格式要求，请确保使用合适的系统提示词，或尝试更换 AI 模型。
  - 注：插件默认会检测语音文本是否含日文假名，若无假名则不会调用 TTS，而仅显示字幕文本。
- Linux 下不支持中文输入法
  - 在 Linux 下，游戏本身同样不支持中文输入法，所以这不是本 Mod 的问题。但你可以利用 [linux-ime-helper](https://github.com/clsty/linux-ime-helper) 来输入中文；另外语音输入也可以识别中文。

## 致谢与许可

本项目使用了以下开源项目：
- [BepInEx](https://github.com/BepInEx/BepInEx)：Unity/XNA 游戏 Mod 框架。
  - 许可：LGPL-2.1
- [GPT-SoVITS](https://github.com/RVC-Boss/GPT-SoVITS)：用于语音合成。
  - 许可：MIT
- [otsaloma/markdown-css](https://github.com/otsaloma/markdown-css)：用于生成 `README.html`。
  - 许可：MIT

本项目在构建时还用到以下项目：
- Unity Engine：游戏引擎库（仅用于构建环境）。
  - 仓库内含开源的 `UnityEngine.UI.dll`（许可：Unity Companion License），除此以外的其他文件由于闭源而未包含。

本项目是 MIT 许可的自由开源软件。详见 [LICENSE](https://github.com/qzrs777/AIChat/blob/main/LICENSE)。
