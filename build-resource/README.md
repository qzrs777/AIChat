这里存放构建 Mod 需要用到的资源文件，主要用于在线构建（本地构建可直接引用游戏所在目录）。

## 文件说明
- `mokgamedir` 中的文件用于构建 `AIChat.dll`（随后将其放入 `assets` 中进行打包）
  - 此目录与游戏目录结构一致，但仅保留构建所需的部分（参见 [qzrs777/AIChat 仓库中的 AIChat/AIChat.csproj](https://github.com/qzrs777/AIChat/blob/main/AIChat/AIChat.csproj) 所引用的文件）。
  - BepInEx 和闭源的 UnityEngine DLL 文件不包含在仓库中，CI 在线构建时会从官方源下载。注意，因为 `UnityEngine.UI.dll` 无法直接获取，并且它采用开源许可，所以这里直接包含了它（以及许可文件）。
- `assets` 中的文件用于直接打包，其中 BepInEx 文件未包含在 Git 仓库中，CI 在线构建时会从官方源下载。
