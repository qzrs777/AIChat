<!--
此文件的全部内容（除注释外）将作为 Release 说明。
开头这几行注释不会被写入到 Release 说明，请始终保留。

发布（Release）稳定版的方法：
- 使得本地**最新**的提交（commit）信息为“[Release]版本号”，例如“[Release]1.0.0”。
- 推送到 main 分支，GitHub 将自动构建并发布。
- 发布完成之后，请清空非注释的部分，再加一行“更新内容：”作为初始模板。

注意：
- tag 会自动创建，不需要在本地打 git tag。
- 若有相同 tag 的 Release，原 Release 将被删除。
- BepInEx 对插件版本号有格式要求，所以 V1.0.0 这种是不可以的，将会无法加载。
-->
更新内容：
- 感谢 [DSeaStar](https://github.com/DSeaStar) 提交本次持续通话相关更新。
- 新增客户端能量阈值 VAD（语音活动检测），实现 `VoiceActivityDetector.cs`。
- 支持循环麦克风录音与自动语音分段，可在检测到玩家说话并停顿后自动进入新一轮 ASR → LLM → TTS 对话。
- 新增 `VoiceCall` 配置分区与 UI 开关，支持调整持续通话、VAD 阈值、最短有效语音、停顿判定、恢复监听延迟、是否允许打断等参数。
- 支持在 AI 说话期间打断（Barge-in）：检测到新的语音输入会停止当前播放，并立即开始新一轮对话。
- AI 回复播放结束后自动恢复麦克风监听。
- 扩展公开 API：`IsContinuousCallActive`、`TryStartContinuousCall`、`TryStopContinuousCall`。
- 更新 README 与发布说明。
