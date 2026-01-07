using System;
#if !CONSOLE_PROGRAME
using BepInEx;
using BepInEx.Logging;
#endif

namespace AIChat.Utils {
    public interface Logger
    {
        void LogInfo(string message);
        void LogWarning(string message);
        void LogError(string message);
    }

    public static class Log
    {
        private static Logger _instance;

#if !CONSOLE_PROGRAME
        public static void Init(ManualLogSource pluginLogger = null)
        {
            _instance = new BepInExLogger(pluginLogger);
        }

        private class BepInExLogger : Logger
        {
            private readonly ManualLogSource _logSource;
            public BepInExLogger(ManualLogSource logSource = null)
            {
                if (logSource == null)
                {
                    _logSource = BepInEx.Logging.Logger.CreateLogSource("AIChat");
                }
                else{
                    _logSource = logSource;
                }
            }

            public void LogInfo(string message) => _logSource.LogInfo(message);
            public void LogWarning(string message) => _logSource.LogWarning(message);
            public void LogError(string message) => _logSource.LogError(message);
        }
#else
        public static void Init()
        {
            _instance = new ConsoleLogger();
        }

        private class ConsoleLogger : Logger
        {
            public void LogInfo(string message)
            {
                Console.ForegroundColor = System.ConsoleColor.Cyan;
                Console.WriteLine($"[AIChat] [INFO] {DateTime.Now:HH:mm:ss} {message}");
                Console.ResetColor();
            }

            public void LogWarning(string message)
            {
                Console.ForegroundColor = System.ConsoleColor.Yellow;
                Console.WriteLine($"[AIChat] [WARNING] {DateTime.Now:HH:mm:ss} {message}");
                Console.ResetColor();
            }

            public void LogError(string message)
            {
                Console.ForegroundColor = System.ConsoleColor.Red;
                Console.WriteLine($"[AIChat] [ERROR] {DateTime.Now:HH:mm:ss} {message}");
                Console.ResetColor();
            }
        }
#endif

        public static void Info(string message)
        {
            EnsureInstanceInitialized();
            _instance.LogInfo(message);
        }

        public static void Warning(string message)
        {
            EnsureInstanceInitialized();
            _instance.LogWarning(message);
        }

        public static void Error(string message)
        {
            EnsureInstanceInitialized();
            _instance.LogError(message);
        }

        private static void EnsureInstanceInitialized()
        {
            if (_instance != null) return;

#if !CONSOLE_PROGRAME
            _instance = new BepInExLogger();
#else
            _instance = new ConsoleLogger();
#endif
        }
    }
}