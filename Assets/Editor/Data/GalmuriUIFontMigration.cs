using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Nytherion.Editor
{
    /// <summary>씬·프리팹의 텍스트와 새 TMP 텍스트의 기본 폰트를 Galmuri11로 통일한다.</summary>
    [InitializeOnLoad]
    public static class GalmuriUIFontMigration
    {
        private const string FontPath = "Assets/Nytherion/Art/UI/Fonts/Galmuri/Galmuri11 SDF.asset";
        private const string SourcePath = "Assets/Nytherion/Art/UI/Fonts/Galmuri/Galmuri11.ttf";
        private const string Output = "output/galmuri-font";
        private const string ResumePlay = "Nytherion.Galmuri.ResumePlay";
        private const string VerifyPlay = "Nytherion.Galmuri.VerifyPlay";
        private const string StopAfterVerification = "Nytherion.Galmuri.StopAfterVerification";
        private static double playStartedAt;

        static GalmuriUIFontMigration() => EditorApplication.update += Update;

        private static void Update()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (File.Exists(Output + "/verify-play.request"))
            {
                File.Delete(Output + "/verify-play.request");
                SessionState.SetBool(StopAfterVerification, !EditorApplication.isPlaying);
                SessionState.SetBool(VerifyPlay, true);
                EditorApplication.isPlaying = true;
            }
            if (File.Exists(Output + "/migrate.request"))
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    SessionState.SetBool(ResumePlay, true);
                    EditorApplication.isPlaying = false;
                    return;
                }
                File.Delete(Output + "/migrate.request");
                try
                {
                    Apply();
                    if (SessionState.GetBool(ResumePlay, false))
                    {
                        SessionState.SetBool(ResumePlay, false);
                        SessionState.SetBool(VerifyPlay, true);
                        EditorApplication.isPlaying = true;
                    }
                }
                catch (Exception error)
                {
                    SessionState.SetBool(ResumePlay, false);
                    File.WriteAllText(Output + "/migration.txt", "FAIL " + error);
                    Debug.LogException(error);
                }
            }

            if (!SessionState.GetBool(VerifyPlay, false) || !EditorApplication.isPlaying) return;
            if (playStartedAt == 0d) playStartedAt = EditorApplication.timeSinceStartup;
            if (EditorApplication.timeSinceStartup - playStartedAt < 10d) return;
            SessionState.SetBool(VerifyPlay, false);
            GameObject sampleCanvas = null;
            try
            {
                TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
                sampleCanvas = new GameObject("Galmuri 폰트 검증", typeof(RectTransform), typeof(Canvas));
                sampleCanvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                TMP_FontAsset bold = font.fontWeightTable[7].regularTypeface;
                for (int i = 0; i < 2; i++)
                {
                    var sample = new GameObject("Galmuri 검증 " + i, typeof(RectTransform), typeof(TextMeshProUGUI));
                    sample.transform.SetParent(sampleCanvas.transform, false);
                    var text = sample.GetComponent<TextMeshProUGUI>();
                    text.font = font;
                    text.text = i == 0 ? "설정 인벤토리 장착 판매 스킬 유물 0123456789 ABC abc" : "<b>한글 굵게 장착 해제 0123456789 ABC</b>";
                    text.fontSize = 32f;
                    text.rectTransform.sizeDelta = new Vector2(1000f, 150f);
                    text.rectTransform.anchoredPosition = new Vector2(0f, i * -150f);
                    text.ForceMeshUpdate();
                    TMP_CharacterInfo[] visible = text.textInfo.characterInfo.Take(text.textInfo.characterCount)
                        .Where(character => character.isVisible).ToArray();
                    if (visible.Length == 0 || visible.Any(character => character.fontAsset != (i == 0 ? font : bold)))
                        throw new InvalidOperationException("Galmuri 기본/굵게 글리프 렌더링 실패");
                }
                TMP_Text[] texts = Resources.FindObjectsOfTypeAll<TMP_Text>()
                    .Where(text => text.gameObject.scene.IsValid() &&
                        !EditorSceneManager.IsPreviewScene(text.gameObject.scene)).ToArray();
                string[] wrong = texts.Where(text => text.font != font).Select(text => text.name).ToArray();
                if (texts.Length == 0 || wrong.Length != 0)
                    throw new InvalidOperationException("실행 중 폰트 불일치: " + string.Join(", ", wrong));
                foreach (TMP_Text text in texts.Where(text => text.isActiveAndEnabled))
                    text.ForceMeshUpdate();
                File.WriteAllText(Output + "/play-verification.txt",
                    "PASS " + SceneManager.GetActiveScene().name + " 실행 중 텍스트 " + texts.Length +
                    "개 Galmuri11 적용 및 활성 텍스트 메시 생성\nPASS 한글·영문·숫자 기본/굵게 글리프 렌더링");
            }
            catch (Exception error)
            {
                File.WriteAllText(Output + "/play-verification.txt", "FAIL " + error);
                Debug.LogException(error);
            }
            finally
            {
                if (sampleCanvas != null) UnityEngine.Object.Destroy(sampleCanvas);
                playStartedAt = 0d;
                if (SessionState.GetBool(StopAfterVerification, false))
                {
                    SessionState.SetBool(StopAfterVerification, false);
                    EditorApplication.isPlaying = false;
                }
            }
        }

        [MenuItem("Tools/Nytherion/폰트/전체 텍스트를 Galmuri11로 변경")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("편집 모드에서 실행해 주세요.");

            Directory.CreateDirectory(Output);
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            Font source = AssetDatabase.LoadAssetAtPath<Font>(SourcePath);
            if (font == null || source == null) throw new InvalidOperationException("Galmuri11 폰트가 없습니다.");

            // 사용자가 아직 저장하지 않은 씬은 자동으로 저장하거나 다시 열지 않는다.
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene.isDirty)
                    throw new InvalidOperationException("변경 전 씬을 저장해 주세요: " + scene.path);
            }

            var results = new List<string>();
            string[] prefabs = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" })
                .Select(AssetDatabase.GUIDToAssetPath).OrderBy(path => path, StringComparer.Ordinal).ToArray();
            // 중첩 프리팹의 원본부터 처리해 불필요한 인스턴스 오버라이드를 줄인다.
            var completed = new HashSet<string>();
            foreach (string path in prefabs) MigratePrefab(path, completed, font, source, results);

            Scene originalActive = SceneManager.GetActiveScene();
            string[] scenes = AssetDatabase.FindAssets("t:Scene", new[] { "Assets" })
                .Select(AssetDatabase.GUIDToAssetPath).OrderBy(path => path, StringComparer.Ordinal).ToArray();
            try
            {
                foreach (string path in scenes)
                {
                    Scene scene = SceneManager.GetSceneByPath(path);
                    bool wasOpen = scene.IsValid() && scene.isLoaded;
                    if (!wasOpen) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                    try
                    {
                        string original = File.ReadAllText(path);
                        Dictionary<string, string> references = FontReferences(scene.GetRootGameObjects(), font, source);
                        int changes = scene.GetRootGameObjects().Sum(root => ChangeTexts(root, font, source));
                        if (changes > 0)
                        {
                            EditorSceneManager.MarkSceneDirty(scene);
                            if (!EditorSceneManager.SaveScene(scene))
                                throw new InvalidOperationException("씬 저장 실패: " + path);
                            PreserveOtherFields(path, original, references);
                        }
                        int count = scene.GetRootGameObjects().Sum(root => VerifyTexts(root, font, source));
                        results.Add("PASS 씬: " + path + " / 텍스트 " + count + "개, 변경 " + changes + "개");
                    }
                    finally { if (!wasOpen) EditorSceneManager.CloseScene(scene, true); }
                }
            }
            finally
            {
                if (originalActive.IsValid() && originalActive.isLoaded) SceneManager.SetActiveScene(originalActive);
            }

            var settings = new SerializedObject(TMP_Settings.instance);
            settings.FindProperty("m_defaultFontAsset").objectReferenceValue = font;
            settings.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssetIfDirty(TMP_Settings.instance);
            if (TMP_Settings.defaultFontAsset != font) throw new InvalidOperationException("TMP 기본 폰트 적용 실패");
            results.Add("PASS 새 텍스트 및 런타임 생성 텍스트의 TMP 기본 폰트: Galmuri11 SDF");

            foreach (string path in prefabs)
            {
                GameObject root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                VerifyTexts(root, font, source);
            }
            results.Add("PASS 프리팹 " + prefabs.Length + "개, 씬 " + scenes.Length + "개 전수 검사");
            File.WriteAllLines(Output + "/migration.txt", results);
            Debug.Log("전체 UI의 Galmuri11 폰트 적용 완료");
        }

        private static void MigratePrefab(string path, HashSet<string> completed, TMP_FontAsset font,
            Font source, List<string> results)
        {
            if (!completed.Add(path)) return;
            foreach (string dependency in AssetDatabase.GetDependencies(path, false)
                .Where(dependency => dependency.EndsWith(".prefab", StringComparison.Ordinal) &&
                    dependency.StartsWith("Assets/", StringComparison.Ordinal)))
                MigratePrefab(dependency, completed, font, source, results);

            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null || (asset.GetComponentsInChildren<TMP_Text>(true).Length == 0 &&
                asset.GetComponentsInChildren<Text>(true).Length == 0)) return;

            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                string original = File.ReadAllText(path);
                Dictionary<string, string> references = FontReferences(new[] { root }, font, source);
                int changes = ChangeTexts(root, font, source);
                if (changes > 0)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, path, out bool saved);
                    if (!saved) throw new InvalidOperationException("프리팹 저장 실패: " + path);
                    PreserveOtherFields(path, original, references);
                }
                int count = VerifyTexts(root, font, source);
                results.Add("PASS 프리팹: " + path + " / 텍스트 " + count + "개, 변경 " + changes + "개");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static Dictionary<string, string> FontReferences(IEnumerable<GameObject> roots,
            TMP_FontAsset font, Font source)
        {
            var references = new Dictionary<string, string>();
            foreach (GameObject root in roots)
            {
                foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
                {
                    if (text.font != null) references[Reference(text.font)] = Reference(font);
                    if (text.fontSharedMaterial != null)
                        references[Reference(text.fontSharedMaterial)] = Reference(font.material);
                }
                foreach (Text text in root.GetComponentsInChildren<Text>(true))
                    if (text.font != null) references[Reference(text.font)] = Reference(source);
            }
            return references;
        }

        private static string Reference(UnityEngine.Object asset)
        {
            if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string guid, out long fileId))
                throw new InvalidOperationException("폰트 참조 식별 실패: " + asset.name);
            return "{fileID: " + fileId + ", guid: " + guid + ", type: " + (asset is Font ? 3 : 2) + "}";
        }

        private static void PreserveOtherFields(string path, string original, Dictionary<string, string> references)
        {
            // Unity가 오래된 컴포넌트를 재직렬화하더라도 폰트와 머티리얼 참조 변경만 남긴다.
            // 기존 GUID·파일 ID·미인식 필드·레이아웃은 원본 문자열 그대로 보존한다.
            foreach (KeyValuePair<string, string> reference in references)
                original = original.Replace(reference.Key, reference.Value);
            File.WriteAllText(path, original);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        }

        private static int ChangeTexts(GameObject root, TMP_FontAsset font, Font source)
        {
            int changes = 0;
            foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
            {
                if (text.font == font && text.fontSharedMaterial == font.material) continue;
                // 직렬화된 폰트 두 필드만 바꿔 크기·스타일·정렬·레이아웃을 보존한다.
                var serialized = new SerializedObject(text);
                serialized.FindProperty("m_fontAsset").objectReferenceValue = font;
                serialized.FindProperty("m_sharedMaterial").objectReferenceValue = font.material;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                if (PrefabUtility.IsPartOfPrefabInstance(text))
                    PrefabUtility.RecordPrefabInstancePropertyModifications(text);
                changes++;
            }
            foreach (Text text in root.GetComponentsInChildren<Text>(true))
            {
                if (text.font == source) continue;
                text.font = source;
                EditorUtility.SetDirty(text);
                if (PrefabUtility.IsPartOfPrefabInstance(text))
                    PrefabUtility.RecordPrefabInstancePropertyModifications(text);
                changes++;
            }
            return changes;
        }

        private static int VerifyTexts(GameObject root, TMP_FontAsset font, Font source)
        {
            TMP_Text[] texts = root.GetComponentsInChildren<TMP_Text>(true);
            foreach (TMP_Text text in texts)
                if (text.font != font || text.fontSharedMaterial != font.material)
                    throw new InvalidOperationException("TMP 폰트·머티리얼 불일치: " + root.name + "/" + text.name);
            Text[] legacy = root.GetComponentsInChildren<Text>(true);
            foreach (Text text in legacy)
                if (text.font != source)
                    throw new InvalidOperationException("UI 폰트 불일치: " + root.name + "/" + text.name);
            return texts.Length + legacy.Length;
        }
    }
}
