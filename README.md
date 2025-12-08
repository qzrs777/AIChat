# \# Chill AI Mod - 让《放松时光》的女主活过来！

# 

# 这是一个为游戏《放松时光：与你共享Lo-Fi故事》开发的 AI 对话 Mod。

# 它接入了 LLM (大语言模型) 和 GPT-SoVITS (语音合成)，让聪音 (Satone) 能够真正地与你对话、思考，并做出挥手、喝茶等生动的动作。

# 

# \## ✨ 功能特点

# \* \*\*智能对话\*\*：基于 OpenRouter (支持 GPT-4, Claude 等) 的自由对话。

# \* \*\*全语音配音\*\*：对接本地 GPT-SoVITS，生成符合人设的日语配音。

# \* \*\*动作同步\*\*：根据对话情绪，自动触发游戏内的动作（挥手、喝茶、思考）。

# \* \*\*沉浸体验\*\*：包含视线锁定、口型同步和自然的动作过渡。

# \* \*\*游戏内配置\*\*：按 `F9` 呼出面板，直接在游戏里修改 API Key 和人设。

# 

# \## 📦 安装方法 (致玩家)

# 1\.  安装 \[BepInEx 5](https://github.com/BepInEx/BepInEx/releases)。

# 2\.  在 \[Releases](你的GitHub发布页链接) 下载 `ChillAIMod.dll`。

# 3\.  将 DLL 放入游戏目录的 `BepInEx/plugins` 文件夹。

# 4\.  \*\*前置要求\*\*：下载并启动 \[GPT-SoVITS](https://github.com/RVC-Boss/GPT-SoVITS) 本地服务（默认端口 9880）。

# 5\.  进入游戏按 `F9` 设置 API Key 和模型。

# 

# \## 🛠️ 构建指南 (致开发者)

# 如果你想自己编译源码：

# 1\.  克隆本仓库。

# 2\.  在项目根目录创建一个 `Libs` 文件夹。

# 3\.  从你的游戏目录 (`Chillwithyou\_Data/Managed`) 复制以下 DLL 到 `Libs`：

# &nbsp;   \* `Assembly-CSharp.dll`

# &nbsp;   \* `UnityEngine.dll`

# &nbsp;   \* `UnityEngine.CoreModule.dll`

# &nbsp;   \* `UnityEngine.UI.dll`

# &nbsp;   \* `BepInEx.dll`

# 4\.  使用 Visual Studio 打开 `.sln` 文件。

# 5\.  检查引用路径是否正确，然后生成。

# 

# \## 📄 开源协议

# MIT License

