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
- 新增“持续通话”模式：开启后 Mod 会持续监听麦克风，检测到玩家说话并停顿后自动进行 ASR → LLM → TTS 回复，AI 说完后自动恢复监听。
- 支持在 AI 说话期间打断（Barge-in）：检测到新的语音输入会停止当前播放并立即开始新一轮对话。
- 新增 VAD（语音活动检测）参数配置：能量阈值、最短有效语音、停顿判定结束、恢复监听延迟、是否允许打断。
- 扩展公开 API：`IsContinuousCallActive`、`TryStartContinuousCall`、`TryStopContinuousCall`。
