using UnityEngine; 

namespace Nytherion.Utils
{
    public enum LogLevel
    {
        None = 0,  
        Error = 1, 
        Warning = 2,
        Info = 3   
    }

    public static class InventoryLogger
    {
        private const string LOG_PREFIX = "[Inventory] ";
        public static LogLevel CurrentLogLevel { get; set; } = LogLevel.Info; 

        
        public static void Log(string message)
        {
            if (CurrentLogLevel >= LogLevel.Info)
            {
                Debug.Log(LOG_PREFIX + message);
            }
        }

        
        public static void LogWarning(string message)
        {
            if (CurrentLogLevel >= LogLevel.Warning)
            {
                Debug.LogWarning(LOG_PREFIX + message);
            }
        }

        
        public static void LogError(string message)
        {
            if (CurrentLogLevel >= LogLevel.Error)
            {
                Debug.LogError(LOG_PREFIX + message);
            }
        }
    }
}