using System;
#if !CONSOLE_PROGRAME
using BepInEx;
using BepInEx.Logging;
#endif

namespace AIChat.Utils {
    public interface Logger
    {
        void LogInfo(string message);
        void LogDebug(string message);
        void LogMessage(string message);
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
            public void LogDebug(string message) => _logSource.LogDebug(message);
            public void LogMessage(string message) => _logSource.LogMessage(message);
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
                Console.WriteLine($"[INFO] {DateTime.Now:HH:mm:ss} {message}");
                Console.ResetColor();
            }

            public void LogDebug(string message)
            {
                Console.ForegroundColor = System.ConsoleColor.Gray;
                Console.WriteLine($"[DEBUG] {DateTime.Now:HH:mm:ss} {message}");
                Console.ResetColor();
            }

            public void LogMessage(string message)
            {
                Console.ForegroundColor = System.ConsoleColor.Green;
                Console.WriteLine($"[Message] {DateTime.Now:HH:mm:ss} {message}");
                Console.ResetColor();
            }

            public void LogWarning(string message)
            {
                Console.ForegroundColor = System.ConsoleColor.Yellow;
                Console.WriteLine($"[WARNING] {DateTime.Now:HH:mm:ss} {message}");
                Console.ResetColor();
            }

            public void LogError(string message)
            {
                Console.ForegroundColor = System.ConsoleColor.Red;
                Console.WriteLine($"[ERROR] {DateTime.Now:HH:mm:ss} {message}");
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

        public static void Debug(string message)
        {
            EnsureInstanceInitialized();
            _instance.LogDebug(message);
        }

        public static void Message(string message)
        {
            EnsureInstanceInitialized();
            _instance.LogMessage(message);
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