using UnityEngine;

namespace Nytherion.Core.Utils
{
    /// <summary>
    /// 프로젝트 전체의 로그 설정을 관리하는 유틸리티 클래스
    /// </summary>
    public static class LogConfig
    {
        /// <summary>
        /// 매니저 관련 상세 로그 표시 여부 (에디터에서만 활성화)
        /// </summary>
        public static bool EnableManagerLogs =>
#if UNITY_EDITOR
            true;
#else
            false;
#endif

        /// <summary>
        /// UI 관련 상세 로그 표시 여부
        /// </summary>
        public static bool EnableUILogs =>
#if UNITY_EDITOR
            false; // UI 로그는 기본적으로 비활성화
#else
            false;
#endif

        /// <summary>
        /// 저장/로드 관련 로그 표시 여부
        /// </summary>
        public static bool EnableSaveLoadLogs =>
#if UNITY_EDITOR
            true;
#else
            false;
#endif

        /// <summary>
        /// DI 컨테이너 관련 로그 표시 여부
        /// </summary>
        public static bool EnableDILogs =>
#if UNITY_EDITOR
            false; // DI 로그는 디버깅 시에만 필요
#else
            false;
#endif

        /// <summary>
        /// 조건부 로그 출력 (매니저용)
        /// </summary>
        public static void LogManager(string message)
        {
            if (EnableManagerLogs)
                Debug.Log(message);
        }

        /// <summary>
        /// 조건부 로그 출력 (UI용)
        /// </summary>
        public static void LogUI(string message)
        {
            if (EnableUILogs)
                Debug.Log(message);
        }

        /// <summary>
        /// 조건부 로그 출력 (저장/로드용)
        /// </summary>
        public static void LogSaveLoad(string message)
        {
            if (EnableSaveLoadLogs)
                Debug.Log(message);
        }

        /// <summary>
        /// 조건부 로그 출력 (DI용)
        /// </summary>
        public static void LogDI(string message)
        {
            if (EnableDILogs)
                Debug.Log(message);
        }
    }
}