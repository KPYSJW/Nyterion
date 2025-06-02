using UnityEngine; // For Debug.Log, Debug.LogWarning, Debug.LogError
// System.Diagnostics is no longer needed as ConditionalAttribute is removed.
// using System.Diagnostics; 
// using Nytherion.Data.ScriptableObjects.Items; // Not directly used in this logger
// using System; // Not directly used in this logger
// using Nytherion.Core; // Not directly used in this logger

namespace Nytherion.Utils
{
    public enum LogLevel
    {
        None = 0,  // No logs
        Error = 1, // Only errors
        Warning = 2,// Errors and warnings
        Info = 3   // Errors, warnings, and info (verbose)
    }

    public static class InventoryLogger
    {
        private const string LOG_PREFIX = "[Inventory] ";
        public static LogLevel CurrentLogLevel { get; set; } = LogLevel.Info; // Default to Info

        // [Conditional("ENABLE_LOG")] // Removed
        public static void Log(string message)
        {
            if (CurrentLogLevel >= LogLevel.Info)
            {
                Debug.Log(LOG_PREFIX + message);
            }
        }

        // [Conditional("ENABLE_LOG_WARNING")] // Removed
        public static void LogWarning(string message)
        {
            if (CurrentLogLevel >= LogLevel.Warning)
            {
                Debug.LogWarning(LOG_PREFIX + message);
            }
        }

        // [Conditional("ENABLE_LOG_ERROR")] // Removed
        public static void LogError(string message)
        {
            if (CurrentLogLevel >= LogLevel.Error)
            {
                Debug.LogError(LOG_PREFIX + message);
            }
        }
    }
}