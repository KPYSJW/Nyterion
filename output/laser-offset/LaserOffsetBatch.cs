using System;
using System.IO;
using Nytherion.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;

[InitializeOnLoad]
public static class LaserOffsetBatch
{
    private const string Pending = "LaserOffsetBatch.Pending";
    private static double readyAt;
    static LaserOffsetBatch()
    {
        EditorApplication.update += Update;
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
                readyAt = EditorApplication.timeSinceStartup + 3d;
        };
    }
    public static void Start()
    {
        if (!Application.dataPath.Replace('\\', '/').EndsWith("/tmp/laser-offset-check/Assets"))
            throw new InvalidOperationException("검증 복사본에서만 실행합니다.");
        PlayerSettings.productName = "Nytherion Laser Offset Verification";
        EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity");
        SessionState.SetBool(Pending, true);
        SessionState.SetFloat(Pending + ".Deadline", (float)EditorApplication.timeSinceStartup + 180f);
        EditorApplication.isPlaying = true;
    }
    private static void Update()
    {
        if (!SessionState.GetBool(Pending, false)) return;
        if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Pending + ".Deadline", float.MaxValue))
        {
            Debug.LogError("레이저 오프셋 검증 시간 초과");
            EditorApplication.Exit(2);
            return;
        }
        if (!EditorApplication.isPlaying || readyAt == 0d || EditorApplication.timeSinceStartup < readyAt) return;
        SessionState.SetBool(Pending, false);
        if (Mouse.current == null) InputSystem.AddDevice<Mouse>();
        LaserWeaponVerification.Run();
        string report = File.ReadAllText("output/laser/verification.json");
        EditorApplication.Exit(report.Contains("\"passed\": true") && report.Contains("\"failures\": 0") ? 0 : 1);
    }
}
