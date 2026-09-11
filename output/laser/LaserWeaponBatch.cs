using System;
using System.IO;
using Nytherion.Data.ScriptableObjects.Weapons;
using Nytherion.GamePlay.Combat;
using Nytherion.GamePlay.Characters.Enemy;
using Nytherion.Data.ScriptableObjects.Enemy;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Nytherion.Editor
{
    [InitializeOnLoad]
    public static class LaserWeaponBatch
    {
        private const string Flag = "LaserWeaponBatch.Pending";
        private static double readyAt;
        private static bool waiting;
        private static LaserWeapon liveWeapon;
        private static EnemyBase liveEnemy;
        private static EnemyData liveEnemyData;
        private static float liveStart;
        private static bool liveChecking;
        private static bool captured;
        static LaserWeaponBatch()
        {
            EditorApplication.playModeStateChanged += OnPlayMode;
            EditorApplication.update += Update;
        }

        public static void Start()
        {
            if (!Application.dataPath.Replace('\\', '/').Contains("/output/laser/unity-verification/"))
                throw new InvalidOperationException("검증 복사본에서만 실행할 수 있습니다.");
            Directory.CreateDirectory("output/laser");
            PlayerSettings.productName = "Nytherion Laser Verification";
            EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity");
            SessionState.SetBool(Flag, true);
            SessionState.SetFloat(Flag + ".Deadline", (float)EditorApplication.timeSinceStartup + 180f);
            EditorApplication.isPlaying = true;
        }

        private static void OnPlayMode(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Flag, false))
            {
                readyAt = EditorApplication.timeSinceStartup + 2d;
                waiting = true;
            }
        }

        private static void Update()
        {
            if (liveChecking)
            {
                if (!captured && Time.time - liveStart >= 0.4f)
                {
                    CaptureBeam();
                    captured = true;
                }
                if (Time.time - liveStart >= 1.5f)
                {
                    liveChecking = false;
                    float health = (float)typeof(EnemyBase).GetField("currentHealth",
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(liveEnemy);
                    bool framePassed = Mathf.Abs(health - 976f) < 0.001f && !liveWeapon.IsFiring;
                    File.WriteAllText("output/laser/frame-check.txt", "actualDamage=" + (1000f - health) +
                        ", expectedDamage=24, IsFiring=" + liveWeapon.IsFiring + ", passed=" + framePassed);
                    string report = File.ReadAllText("output/laser/verification.json");
                    UnityEngine.Object.DestroyImmediate(liveWeapon.gameObject);
                    UnityEngine.Object.DestroyImmediate(liveEnemy.gameObject);
                    UnityEngine.Object.DestroyImmediate(liveEnemyData);
                    EditorApplication.Exit(framePassed && report.Contains("\"failures\": 0") ? 0 : 1);
                }
                return;
            }
            if (!SessionState.GetBool(Flag, false)) return;
            if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Flag + ".Deadline", float.MaxValue))
            {
                SessionState.SetBool(Flag, false);
                Debug.LogError("[LaserWeaponBatch] 플레이 진입/검증 시간 초과");
                EditorApplication.Exit(2);
                return;
            }
            if (!waiting || !EditorApplication.isPlaying || EditorApplication.timeSinceStartup < readyAt) return;
            waiting = false;
            SessionState.SetBool(Flag, false);
            try
            {
                LaserWeaponVerification.Run();
                BeginFrameCheck();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(3);
            }
        }

        private static void BeginFrameCheck()
        {
            LaserWeaponData source = AssetDatabase.LoadAssetAtPath<LaserWeaponData>(
                "Assets/Nytherion/Data/ScriptableObjects/Weapons/LaserEmitter.asset");
            liveWeapon = UnityEngine.Object.Instantiate(source.weaponPrefab,
                new Vector3(20000f, 20000f, 0f), Quaternion.identity) as LaserWeapon;
            liveWeapon.Initialize(source);
            GameObject enemyObject = new GameObject("Frame Check Enemy");
            enemyObject.transform.position = new Vector3(20003f, 20000f, 0f);
            enemyObject.layer = LayerMask.NameToLayer("Enemy");
            BoxCollider2D collider = enemyObject.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(0.2f, 0.2f);
            collider.isTrigger = true;
            liveEnemy = enemyObject.AddComponent<EnemyBase>();
            liveEnemyData = ScriptableObject.CreateInstance<EnemyData>();
            liveEnemyData.maxHealth = 1000f;
            liveEnemy.Initialize(liveEnemyData);
            Physics2D.SyncTransforms();
            Time.timeScale = 1f;
            liveStart = Time.time;
            liveWeapon.Attack(Vector2.right);
            liveChecking = true;
        }

        private static void CaptureBeam()
        {
            Camera camera = new GameObject("Laser Verification Camera").AddComponent<Camera>();
            camera.transform.position = new Vector3(20003.8f, 20000f, -10f);
            camera.orthographic = true;
            camera.orthographicSize = 3f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.035f, 0.05f, 0.09f);
            RenderTexture renderTexture = new RenderTexture(1280, 720, 24);
            camera.targetTexture = renderTexture;
            camera.Render();
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = renderTexture;
            Texture2D image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            image.Apply();
            File.WriteAllBytes("output/laser/beam-preview.png", image.EncodeToPNG());
            RenderTexture.active = previous;
            camera.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(image);
            UnityEngine.Object.DestroyImmediate(renderTexture);
            UnityEngine.Object.DestroyImmediate(camera.gameObject);
        }
    }
}
