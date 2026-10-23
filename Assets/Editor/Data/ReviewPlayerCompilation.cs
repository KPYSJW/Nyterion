using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Player;
using UnityEngine;

namespace Nytherion.Editor
{
    [InitializeOnLoad]
    public static class ReviewPlayerCompilation
    {
        private const string Output = "output/review-player-compile";
        static ReviewPlayerCompilation() => EditorApplication.update += Update;
        private static void Update()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (File.Exists(Output + "/clear-progress.request"))
            {
                File.Delete(Output + "/clear-progress.request");
                EditorUtility.ClearProgressBar();
                File.WriteAllText(Output + "/progress-recovery.txt", "PASS 진행 표시 해제 요청 완료\n" +
                    "isCompiling=" + EditorApplication.isCompiling + "\nisUpdating=" + EditorApplication.isUpdating +
                    "\nisPlaying=" + EditorApplication.isPlaying + "\nisPaused=" + EditorApplication.isPaused +
                    "\nscene=" + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
                return;
            }
            if (EditorApplication.isPlayingOrWillChangePlaymode ||
                !File.Exists(Output + "/compile.request")) return;
            File.Delete(Output + "/compile.request");
            Compile();
        }

        [MenuItem("Tools/Nytherion/Review/Compile Windows Player Scripts")]
        public static void Compile()
        {
            Directory.CreateDirectory(Output);
            var errors = new List<string>();
            Application.LogCallback capture = (message, trace, type) =>
            {
                if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(message);
            };
            Application.logMessageReceived += capture;
            try
            {
                var settings = new ScriptCompilationSettings
                {
                    target = BuildTarget.StandaloneWindows64,
                    group = BuildTargetGroup.Standalone,
                    options = ScriptCompilationOptions.None
                };
                var result = PlayerBuildInterface.CompilePlayerScripts(settings, Output + "/assemblies");
                if (result.assemblies == null || result.assemblies.Count == 0 || errors.Count != 0)
                    throw new InvalidOperationException("Player 스크립트 컴파일 실패: " + string.Join("\n", errors));
                File.WriteAllText(Output + "/verification.txt", "PASS Windows64 비개발 Player 스크립트 컴파일: " + result.assemblies.Count +
                    "개 어셈블리\n전체 Player 빌드는 실행하지 않았습니다. 기존 Localization 설정 누락은 별도 해결 대상입니다.");
            }
            catch (Exception error) { File.WriteAllText(Output + "/verification.txt", "FAIL " + error); }
            finally
            {
                Application.logMessageReceived -= capture;
                // 컴파일 API가 완료돼도 남을 수 있는 진행 창을 반드시 정리한다.
                EditorUtility.ClearProgressBar();
            }
        }
    }
}
