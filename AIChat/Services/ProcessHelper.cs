using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AIChat.Utils;

namespace AIChat.Services
{
    public static class ProcessHelper
    {
        public static void KillProcessTree(Process process)
        {
            if (process == null || process.HasExited) return;

            try
            {
                int pid = process.Id;
                Log.Info($"[TTS Cleanup] 使用 taskkill 终止进程树 (PID: {pid})");

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

                Log.Info($"[TTS Cleanup] taskkill 执行完毕 (PID: {pid})");
            }
            catch (Exception ex)
            {
                Log.Warning($"[TTS Cleanup] taskkill 失败: {ex.Message}");
            }
        }
    }
}
