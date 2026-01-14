using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace Ascon.Pilot.SDK.Services
{
    /// <summary>
    /// Централизованная система логирования
    /// </summary>
    public static class LoggingService
    {
        private static readonly object _lockObject = new object();
        private static readonly string LogFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs", "pilot-module.log");

        /// <summary>
        /// Уровни логирования
        /// </summary>
        public enum LogLevel
        {
            Debug,
            Info,
            Warning,
            Error,
            Critical
        }

        /// <summary>
        /// Логировать информационное сообщение
        /// </summary>
        public static void LogInfo(string message, string methodName = null, TimeSpan? elapsed = null)
        {
            Log(LogLevel.Info, message, methodName, elapsed);
        }

        /// <summary>
        /// Логировать предупреждение
        /// </summary>
        public static void LogWarning(string message, string methodName = null, TimeSpan? elapsed = null)
        {
            Log(LogLevel.Warning, message, methodName, elapsed);
        }

        /// <summary>
        /// Логировать ошибку
        /// </summary>
        public static void LogError(string message, Exception exception = null, string methodName = null, TimeSpan? elapsed = null)
        {
            Log(LogLevel.Error, message, methodName, elapsed, exception);
        }

        /// <summary>
        /// Логировать критическую ошибку
        /// </summary>
        public static void LogCritical(string message, Exception exception = null, string methodName = null, TimeSpan? elapsed = null)
        {
            Log(LogLevel.Critical, message, methodName, elapsed, exception);
        }

        /// <summary>
        /// Логировать отладочное сообщение
        /// </summary>
        public static void LogDebug(string message, string methodName = null, TimeSpan? elapsed = null)
        {
            Log(LogLevel.Debug, message, methodName, elapsed);
        }

        /// <summary>
        /// Логировать загрузку объекта
        /// </summary>
        public static void LogObjectLoad(string objectId, string methodName = null, TimeSpan? elapsed = null)
        {
            Log(LogLevel.Debug, $"Loading object {objectId}", methodName, elapsed);
        }

        /// <summary>
        /// Основной метод логирования
        /// </summary>
        private static void Log(LogLevel level, string message, string methodName = null, TimeSpan? elapsed = null, Exception exception = null)
        {
            try
            {
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                var levelStr = level.ToString().ToUpper();
                var methodStr = string.IsNullOrEmpty(methodName) ? "" : $" [{methodName}]";
                var elapsedStr = elapsed.HasValue ? $" (Elapsed: {elapsed.Value.TotalMilliseconds:F3}ms)" : "";
                var exceptionStr = exception != null ? $"\nException: {exception}" : "";

                var logMessage = $"[{timestamp}] {levelStr}{methodStr}: {message}{elapsedStr}{exceptionStr}";

                // Записать в файл
                WriteToFile(logMessage);

                // Записать в Debug (для отладки)
                Debug.WriteLine(logMessage);

                // Для критических ошибок показать MessageBox
                if (level == LogLevel.Critical)
                {
                    System.Windows.Forms.MessageBox.Show($"Критическая ошибка: {message}\n\nДетали: {exception?.Message}", 
                        "Критическая ошибка", 
                        System.Windows.Forms.MessageBoxButtons.OK, 
                        System.Windows.Forms.MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                // Если логирование не работает, записать в Debug
                Debug.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] LOGGING_ERROR: Failed to log message: {ex.Message}");
            }
        }

        /// <summary>
        /// Записать сообщение в файл
        /// </summary>
        private static void WriteToFile(string message)
        {
            lock (_lockObject)
            {
                try
                {
                    // Создать директорию для логов если не существует
                    var logDirectory = Path.GetDirectoryName(LogFilePath);
                    if (!Directory.Exists(logDirectory))
                    {
                        Directory.CreateDirectory(logDirectory);
                    }

                    // Записать в файл
                    File.AppendAllText(LogFilePath, message + Environment.NewLine);

                    // Ограничить размер файла логов (10 МБ)
                    var fileInfo = new FileInfo(LogFilePath);
                    if (fileInfo.Exists && fileInfo.Length > 10 * 1024 * 1024)
                    {
                        var backupPath = LogFilePath.Replace(".log", $".{DateTime.Now:yyyyMMdd_HHmmss}.log");
                        File.Move(LogFilePath, backupPath);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to write to log file: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Асинхронное логирование
        /// </summary>
        public static async Task LogAsync(LogLevel level, string message, string methodName = null, TimeSpan? elapsed = null, Exception exception = null)
        {
            await Task.Run(() => Log(level, message, methodName, elapsed, exception));
        }

        /// <summary>
        /// Очистить старые логи
        /// </summary>
        public static void CleanupOldLogs(int daysToKeep = 30)
        {
            try
            {
                var logDirectory = Path.GetDirectoryName(LogFilePath);
                if (!Directory.Exists(logDirectory))
                    return;

                var cutoffDate = DateTime.Now.AddDays(-daysToKeep);
                var logFiles = Directory.GetFiles(logDirectory, "*.log");

                foreach (var file in logFiles)
                {
                    var fileInfo = new FileInfo(file);
                    if (fileInfo.CreationTime < cutoffDate)
                    {
                        File.Delete(file);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to cleanup old logs: {ex.Message}");
            }
        }
    }
}