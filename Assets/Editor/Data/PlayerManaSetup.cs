using System;
using System.IO;
using System.Linq;
using Nytherion.Data.ScriptableObjects.Player;
using Nytherion.Data.ScriptableObjects.Skill;
using Nytherion.GamePlay.Characters.Player;
using Nytherion.GamePlay.Skills;
using Nytherion.UI.Components;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Nytherion.Editor
{
    [InitializeOnLoad]
    public static class PlayerManaSetup
    {
        private const string Output = "output/player-mana";
        static PlayerManaSetup() => EditorApplication.update += Update;

        private static void Update()
        {
            if (!EditorApplication.isCompiling && !EditorApplication.isUpdating &&
                !EditorApplication.isPlayingOrWillChangePlaymode && File.Exists(Output + "/verify.request"))
            {
                File.Delete(Output + "/verify.request");
                VerifyOnly();
                return;
            }
            if (!EditorApplication.isCompiling && !EditorApplication.isUpdating &&
                !EditorApplication.isPlayingOrWillChangePlaymode && File.Exists(Output + "/align.request"))
            {
                File.Delete(Output + "/align.request");
                const string path = "Assets/Scenes/GameScene.unity";
                Scene scene = SceneManager.GetSceneByPath(path);
                bool loaded = scene.IsValid() && scene.isLoaded;
                if (!loaded) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                try
                {
                    if (scene.isDirty && !EditorSceneManager.SaveScene(scene,
                        Output + "/GameScene-before-align-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".unity", true))
                        throw new InvalidOperationException("정렬 전 씬 사본 저장 실패");
                    HPBarUI hud = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<HPBarUI>(true)).Single();
                    AlignBarCanvas(hud);
                    EditorSceneManager.MarkSceneDirty(scene);
                    if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("정렬 씬 저장 실패");
                    File.WriteAllText(Output + "/alignment.txt", "PASS HPCanvas 픽셀 반올림 해제, 원본 크기와 비율 유지");
                }
                finally { if (!loaded) EditorSceneManager.CloseScene(scene, true); }
                return;
            }
            if (!EditorApplication.isCompiling && !EditorApplication.isUpdating && File.Exists(Output + "/inspect.request"))
            {
                File.Delete(Output + "/inspect.request");
                var report = new System.Text.StringBuilder();
                foreach (HPBarUI hud in UnityEngine.Object.FindObjectsOfType<HPBarUI>(true))
                {
                    report.AppendLine("scene=" + hud.gameObject.scene.path + " playing=" + EditorApplication.isPlaying);
                    var properties = new SerializedObject(hud);
                    Image hp = properties.FindProperty("hpFillImage").objectReferenceValue as Image;
                    if (hp == null) continue;
                    Image background = hp.transform.parent.Find("Background")?.GetComponent<Image>();
                    Image mp = properties.FindProperty("mpFillImage").objectReferenceValue as Image;
                    foreach (Image image in new[] { background, hp, mp != null ? mp.transform.parent.GetComponent<Image>() : null, mp })
                    {
                        if (image == null) continue;
                        Rect adjusted = image.GetPixelAdjustedRect();
                        Rect drawn = adjusted;
                        if (image.preserveAspect && image.sprite != null)
                        {
                            float aspect = image.sprite.rect.width / image.sprite.rect.height;
                            if (aspect > drawn.width / drawn.height)
                            {
                                float height = drawn.width / aspect;
                                drawn.y += (drawn.height - height) * image.rectTransform.pivot.y;
                                drawn.height = height;
                            }
                            else
                            {
                                float width = drawn.height * aspect;
                                drawn.x += (drawn.width - width) * image.rectTransform.pivot.x;
                                drawn.width = width;
                            }
                        }
                        Canvas canvas = image.canvas;
                        Camera camera = canvas.rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.rootCanvas.worldCamera;
                        report.AppendLine(image.name + " rect=" + image.rectTransform.rect.ToString("F5") + " adjusted=" + adjusted.ToString("F5") +
                            " drawn=" + drawn.ToString("F5") + " screenMin=" + RectTransformUtility.WorldToScreenPoint(camera, image.transform.TransformPoint(drawn.min)).ToString("F5") +
                            " screenMax=" + RectTransformUtility.WorldToScreenPoint(camera, image.transform.TransformPoint(drawn.max)).ToString("F5") +
                            " preserve=" + image.preserveAspect + " sprite=" + image.sprite?.rect + " canvas=" + canvas.name + " scale=" + canvas.scaleFactor + " pixelPerfect=" + canvas.pixelPerfect);
                    }
                }
                File.WriteAllText(Output + "/inspection.txt", report.ToString());
                return;
            }
            if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(Output + "/setup.request")) return;
            File.Delete(Output + "/setup.request");
            try { Apply(); }
            catch (Exception error)
            {
                File.WriteAllText(Output + "/result.txt", "FAIL " + error);
                Debug.LogException(error);
            }
        }

        [MenuItem("Tools/Nytherion/Player/마나 및 마나바 설정")]
        public static void Apply()
        {
            Directory.CreateDirectory(Output);
            Sprite frameSprite = ImportSprite("MPBar");
            Sprite fillSprite = ImportSprite("MPBarFill");
            PlayerData player = AssetDatabase.LoadAssetAtPath<PlayerData>(
                "Assets/Nytherion/Data/ScriptableObjects/Player/Palyer.asset");
            player.maxMana = 100f;
            player.manaRegenPerSecond = 1f;
            EditorUtility.SetDirty(player);
            foreach (string guid in AssetDatabase.FindAssets("t:SkillData", new[] { "Assets/Nytherion/Data/ScriptableObjects/Skill" }))
            {
                SkillData skill = AssetDatabase.LoadAssetAtPath<SkillData>(AssetDatabase.GUIDToAssetPath(guid));
                if (!skill.IsRuntimeAvailable || skill.skillPrefab == null || skill.manaCost != 0) continue;
                skill.manaCost = 20;
                EditorUtility.SetDirty(skill);
            }
            AssetDatabase.SaveAssets();

            const string path = "Assets/Scenes/GameScene.unity";
            Scene scene = SceneManager.GetSceneByPath(path);
            bool loaded = scene.IsValid() && scene.isLoaded;
            if (loaded && scene.isDirty && !EditorSceneManager.SaveScene(scene,
                Output + "/GameScene-before-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".unity", true))
                throw new InvalidOperationException("변경 전 씬 사본 저장 실패");
            if (!loaded) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                HPBarUI hud = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<HPBarUI>(true)).Single();
                AlignBarCanvas(hud);
                SerializedObject data = new SerializedObject(hud);
                Image hpFill = (Image)data.FindProperty("hpFillImage").objectReferenceValue;
                RectTransform hpRect = (RectTransform)hpFill.transform.parent;
                Transform existing = hpRect.parent.Find("MPBar");
                Image frame = existing != null ? existing.GetComponent<Image>() : CreateImage("MPBar", hpRect.parent);
                RectTransform rect = frame.rectTransform;
                rect.anchorMin = hpRect.anchorMin;
                rect.anchorMax = hpRect.anchorMax;
                rect.pivot = hpRect.pivot;
                // 체력바와 같은 폭으로, 바로 아래에 원본 이미지 비율을 유지합니다.
                float height = hpRect.rect.width * frameSprite.rect.height / frameSprite.rect.width;
                rect.sizeDelta = new Vector2(hpRect.rect.width, height);
                rect.anchoredPosition = hpRect.anchoredPosition + Vector2.down * (hpRect.rect.height + 2f);
                rect.localScale = hpRect.localScale;
                frame.sprite = frameSprite;
                frame.color = Color.white;
                frame.raycastTarget = false;
                Transform oldFill = rect.Find("MPBarFill");
                Image fill = oldFill != null ? oldFill.GetComponent<Image>() : CreateImage("MPBarFill", rect);
                fill.rectTransform.anchorMin = Vector2.zero;
                fill.rectTransform.anchorMax = Vector2.one;
                fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;
                fill.sprite = fillSprite;
                fill.color = Color.white;
                fill.type = Image.Type.Filled;
                fill.fillMethod = Image.FillMethod.Horizontal;
                fill.fillOrigin = 0;
                fill.fillAmount = 1f;
                fill.raycastTarget = false;
                data.FindProperty("mpFillImage").objectReferenceValue = fill;
                data.ApplyModifiedPropertiesWithoutUndo();
                Require(fill.sprite != null && frame.sprite != null, "마나 이미지 참조");
                Require(rect.anchoredPosition.y < hpRect.anchoredPosition.y, "체력바 아래 배치");
                VerifyMana();
                EditorSceneManager.MarkSceneDirty(scene);
                Require(EditorSceneManager.SaveScene(scene), "씬 저장");
                File.WriteAllText(Output + "/result.txt", "PASS 마나 100 / 초당 회복 1, 기존 40·60 비용 유지 / 나머지 활성 스킬 비용 20\n" +
                    "GameScene MPBar/MPBarFill 연결, HP 아래 간격 2, 위치=" + rect.anchoredPosition + " 크기=" + rect.sizeDelta +
                    "\n마나 소비·부족·무료·쿨타임 중 미소비·재초기화 검증 통과 / Editor 컴파일 완료");
            }
            finally { if (!loaded) EditorSceneManager.CloseScene(scene, true); }
        }

        private static Image CreateImage(string name, Transform parent)
        {
            GameObject child = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            child.layer = parent.gameObject.layer;
            child.transform.SetParent(parent, false);
            return child.GetComponent<Image>();
        }

        private static void AlignBarCanvas(HPBarUI hud)
        {
            Canvas canvas = hud.GetComponentInParent<Canvas>();
            if (canvas == null || canvas.name != "HPCanvas")
                throw new InvalidOperationException("체력·마나바 전용 Canvas를 찾을 수 없습니다.");
            Undo.RecordObject(canvas, "체력·마나바 너비 정렬");
            // 픽셀 반올림 뒤 HP의 비율 유지가 폭을 재계산하면 MP보다 좁아집니다.
            canvas.overridePixelPerfect = true;
            canvas.pixelPerfect = false;
        }

        private static Sprite ImportSprite(string name)
        {
            string path = "Assets/Nytherion/Art/Characters/Player/Sprites/" + name + ".png";
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.alphaIsTransparency = true;
            TextureImporterSettings settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        [MenuItem("Tools/Nytherion/Player/마나 테스트 기능 검증")]
        public static void VerifyOnly()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            Directory.CreateDirectory(Output);
            try
            {
                VerifyMana();
                File.WriteAllText(Output + "/debug-verification.txt",
                    "PASS 기본 비용·마나 부족 차단·무한 마나 연속 시전·쿨타임 유지·잘못된 비용 차단·낮은 최대 마나에서도 시전·해제 후 정상 소모·최대 회복·회복량 변경 시 현재 마나 유지\n");
            }
            catch (Exception error)
            {
                File.WriteAllText(Output + "/debug-verification.txt", "FAIL " + error);
                Debug.LogException(error);
            }
        }

        private static void VerifyMana()
        {
            Scene preview = EditorSceneManager.NewPreviewScene();
            GameObject owner = new GameObject("마나 검증");
            SceneManager.MoveGameObjectToScene(owner, preview);
            SkillData data = ScriptableObject.CreateInstance<SkillData>();
            try
            {
                PlayerSkillManager mana = owner.AddComponent<PlayerSkillManager>();
                ManaVerificationSkill skill = owner.AddComponent<ManaVerificationSkill>();
                skill.caster = owner.transform;
                skill.skillData = data;
                data.manaCost = 60;
                data.coolDown = 100f;
                Require(skill.TryUse() && mana.CurrentMana == 40f, "스킬 사용 비용");
                Require(!skill.TryUse() && mana.CurrentMana == 40f, "쿨타임 중 미소비");
                data.coolDown = 0f;
                Require(!skill.CanUse() && !skill.TryUse() && mana.CurrentMana == 40f, "마나 부족 차단");
                Require(!mana.TrySpendMana(float.NaN) && !mana.TrySpendMana(float.PositiveInfinity), "유효하지 않은 비용 차단");
                Require(mana.TrySpendMana(40f) && mana.CurrentMana == 0f, "잔여 마나 소모");
                data.manaCost = 0;
                Require(skill.TryUse() && mana.CurrentMana == 0f, "무료 스킬");
                mana.InitializeMana(100f, 5f);
                Require(mana.CurrentMana == 100f, "마나 초기화");
                data.manaCost = 60;
                Require(mana.TrySpendMana(100f), "테스트 전 마나 소진");
                mana.SetDebugInfiniteMana(true);
                Require(skill.TryUse() && skill.TryUse() && mana.CurrentMana == 0f, "무한 마나 연속 시전");
                data.coolDown = 100f;
                Require(!skill.CanUse() && !skill.TryUse(), "무한 마나에서도 쿨타임 유지");
                skill.SetDebugCooldownReduction(1f);
                Require(skill.TryUse() && mana.CurrentMana == 0f, "쿨타임 감소와 무한 마나 동시 적용");
                Require(!mana.TrySpendMana(float.NaN) && !mana.HasMana(float.PositiveInfinity), "무한 마나의 유효하지 않은 비용 차단");
                mana.UpdateManaStats(10f, 5f);
                Require(skill.TryUse() && mana.MaxMana == 10f && mana.CurrentMana == 0f, "최대 마나보다 높은 비용 무시");
                mana.SetDebugInfiniteMana(false);
                Require(!skill.CanUse() && !skill.TryUse() && mana.CurrentMana == 0f, "무한 마나 해제 후 부족 차단");
                mana.UpdateManaStats(100f, 10f);
                Require(mana.CurrentMana == 0f && mana.ManaRegenPerSecond == 10f, "능력치 변경 시 현재 마나 유지");
                Require(mana.RestoreMana(mana.MaxMana) == 100f && mana.CurrentMana == 100f, "마나 최대 회복");
                Require(mana.RestoreMana(mana.MaxMana) == 0f && mana.CurrentMana == 100f, "최대 마나 초과 회복 차단");
                Require(skill.TryUse() && mana.CurrentMana == 40f && !skill.TryUse(), "해제 후 정상 소모 복구");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
                UnityEngine.Object.DestroyImmediate(data);
                EditorSceneManager.ClosePreviewScene(preview);
            }
        }

        private static void Require(bool result, string message)
        {
            if (!result) throw new InvalidOperationException(message);
        }
    }

    public sealed class ManaVerificationSkill : SkillBase
    {
        protected override void Activate() { }
    }
}
