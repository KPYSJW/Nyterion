using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Diagnostics;  // ConditionalAttribute를 위해 필요
using Debug = UnityEngine.Debug;  // Debug 모호성 해결
using Nytherion.Data.ScriptableObjects.Items;
using System;
using Nytherion.Core;

namespace Nytherion.Utils
{
    public static class InventoryLogger
    {
        private const string LOG_PREFIX = "[Inventory] ";

        [Conditional("ENABLE_LOG")]
        public static void Log(string message)
            => Debug.Log(LOG_PREFIX + message);

        [Conditional("ENABLE_LOG_WARNING")]
        public static void LogWarning(string message)
            => Debug.LogWarning(LOG_PREFIX + message);

        [Conditional("ENABLE_LOG_ERROR")]
        public static void LogError(string message)
            => Debug.LogError(LOG_PREFIX + message);
    }
}