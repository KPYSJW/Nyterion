using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Nytherion.Core.Data;
using Nytherion.Core.Enums;
using Nytherion.Data.ScriptableObjects.Relics;
using Nytherion.UI.RelicBoard;
using Nytherion.UI.Skill;
using Nytherion.UI.Components;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Nytherion.Editor
{
    /// <summary>유물 효과 데이터와 슬롯 규격을 GUID를 보존하면서 Unity 에디터에서 적용한다.</summary>
    [InitializeOnLoad]
    public static class RelicPresentationSetup
    {
        public const string Output = "output/relic-presentation";
        private const string Relics = "Assets/Nytherion/Data/ScriptableObjects/Relics/";

        static RelicPresentationSetup() { EditorApplication.update += Update; }

        private static void Update()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (File.Exists(Output + "/refresh.request"))
            {
                File.Delete(Output + "/refresh.request");
                AssetDatabase.Refresh();
                return;
            }
            if (File.Exists(Output + "/audit.request"))
            {
                File.Delete(Output + "/audit.request");
                try { AuditScenes(); }
                catch (Exception error) { File.WriteAllText(Output + "/scene-audit.txt", "FAIL " + error); }
                return;
            }
            bool presentationOnly = File.Exists(Output + "/presentation-setup.request");
            string request = Output + (presentationOnly ? "/presentation-setup.request" : "/setup.request");
            if (!File.Exists(request)) return;
            File.Delete(request);
            try { Apply(!presentationOnly); }
            catch (Exception error)
            {
                File.WriteAllText(Output + "/setup.txt", "FAIL " + error);
                Debug.LogException(error);
            }
        }

        [MenuItem("Tools/Nytherion/Relics/Apply Effects And Slot Presentation")]
        public static void Apply()
        {
            Apply(true);
        }

        private static void Apply(bool configureEffects)
        {
            Directory.CreateDirectory(Output);
            const string commonFramePath = "Assets/Nytherion/Art/UI/Inventory/Slot.png";
            const string equippedFramePath = "Assets/Nytherion/Art/UI/Relics/EquipedRelic_Slot.png";
            ConfigureImporter(commonFramePath);
            ConfigureImporter(equippedFramePath);
            Sprite frame = AssetDatabase.LoadAssetAtPath<Sprite>(commonFramePath);
            Sprite equippedFrame = AssetDatabase.LoadAssetAtPath<Sprite>(equippedFramePath);
            if (frame == null) throw new InvalidOperationException("공통 슬롯 이미지가 없습니다.");
            if (equippedFrame == null) throw new InvalidOperationException("유물 장착 슬롯 이미지가 없습니다.");
            var results = new List<string>();
            if (configureEffects)
            {
                ConfigureStats("SimpleStats/PouchOfAbundance.asset", 1f, 0.05f, 0.02f);
                ConfigureStats("SimpleStats/PandorasBow.asset", 2f, 0f, 0f, 0.1f, 0.03f);
                RelicData globe = AssetDatabase.LoadAssetAtPath<RelicData>(Relics + "CombatUtility/Globe.asset");
                globe.grantsProjectilePiercing = true;
                EditorUtility.SetDirty(globe);
                AssetDatabase.SaveAssetIfDirty(globe);
                results.Add("풍요의 주머니: 투사체 +1, 원거리 피해 +5% / 황금 성배: 투사체 +2, 원거리 속도 +10% / 지구본: 적 관통");
            }

            foreach (string path in new[]
            {
                "Assets/Prefabs/UI/Relic/StorageSlot.prefab", "Assets/Prefabs/UI/Relic/RelicSlotCell.prefab",
                "Assets/Prefabs/UI/Relic/RelicBlockDraggable.prefab", "Assets/Prefabs/UI/SkillStorageSlot.prefab",
                "Assets/Prefabs/UI/SkillEquipSlot.prefab", "Assets/Prefabs/UI/SkillEquipSlot 1.prefab",
                "Assets/Prefabs/UI/SkillEquipSlot 2.prefab"
            })
            {
                if (!File.Exists(path)) continue;
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    bool storage = path.Contains("StorageSlot");
                    float slotSize = storage ? RelicGridUI.StorageSlotSize :
                        path.Contains("SkillEquipSlot") ? 160f : RelicGridUI.EquippedSlotSize;
                    float iconSize = storage || path.Contains("Draggable") ? 96f : 64f;
                    RectTransform rect = root.GetComponent<RectTransform>();
                    rect.sizeDelta = Vector2.one * slotSize;
                    Image background = root.GetComponent<Image>();
                    // 드래그 블록의 투명 Raycast 이미지에는 테두리를 추가하지 않는다.
                    if (background != null && !path.Contains("Draggable"))
                        ConfigureFrame(background, path.Contains("RelicSlotCell") ? equippedFrame : frame);
                    Transform iconTransform = root.transform.Find("Icon");
                    if (iconTransform != null)
                    {
                        Image icon = iconTransform.GetComponent<Image>();
                        ConfigureIcon(icon, iconSize);
                        icon.enabled = false;
                        icon.sprite = null;
                    }
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    results.Add("프리팹 규격 적용: " + path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }

            Scene activeScene = SceneManager.GetActiveScene();
            foreach (string path in new[] { "Assets/Scenes/GameScene.unity", "Assets/Scenes/Village.unity" })
            {
                Scene scene = SceneManager.GetSceneByPath(path);
                bool alreadyOpen = scene.IsValid() && scene.isLoaded;
                if (!alreadyOpen) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                try
                {
                    // 기존 편집 중인 씬도 수정 전 상태를 별도 사본으로 보존한다.
                    string backup = Output + "/" + scene.name + "-before.unity";
                    if (!File.Exists(backup)) EditorSceneManager.SaveScene(scene, backup, true);
                    foreach (GameObject sceneRoot in scene.GetRootGameObjects())
                    {
                        foreach (RelicGridUI ui in sceneRoot.GetComponentsInChildren<RelicGridUI>(true))
                        {
                            GridLayoutGroup board = ui.gridRoot.GetComponent<GridLayoutGroup>();
                            board.cellSize = Vector2.one * RelicGridUI.EquippedSlotSize;
                            RelicGridUI.ConfigureStorageLayout(ui.blockStorageParent);
                        }
                        foreach (SkillSlotUI slot in sceneRoot.GetComponentsInChildren<SkillSlotUI>(true))
                        {
                            ConfigureFrame(slot.GetComponent<Image>(), frame);
                            slot.GetComponent<RectTransform>().sizeDelta = Vector2.one *
                                (slot.slotType == SkillSlotType.Storage ? RelicGridUI.StorageSlotSize : 160f);
                            SerializedObject serialized = new SerializedObject(slot);
                            Image icon = serialized.FindProperty("skillIcon").objectReferenceValue as Image;
                            ConfigureIcon(icon, slot.slotType == SkillSlotType.Storage ? 96f : 64f);
                        }
                        foreach (RelicTooltip tooltip in sceneRoot.GetComponentsInChildren<RelicTooltip>(true))
                        {
                            SerializedObject serialized = new SerializedObject(tooltip);
                            TextMeshProUGUI title = serialized.FindProperty("nameText").objectReferenceValue as TextMeshProUGUI;
                            if (title != null) title.color = Color.white;
                        }
                    }
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                    results.Add("씬 참조/규격 적용: " + path);
                }
                finally { if (!alreadyOpen) EditorSceneManager.CloseScene(scene, true); }
            }
            if (activeScene.IsValid() && activeScene.isLoaded) SceneManager.SetActiveScene(activeScene);
            var database = AssetDatabase.LoadAssetAtPath<RelicDatabaseSO>(Relics + "RelicDatabase.asset");
            foreach (RelicData relic in database.allRelics.Where(data => data != null && data.IsRuntimeAvailable && data.Image == null))
                results.Add("아이콘 참조 누락: " + AssetDatabase.GetAssetPath(relic));
            File.WriteAllLines(Output + "/setup.txt", results);
        }

        private static void ConfigureStats(string path, float extra, float damage, float damagePerLevel,
            float speed = 0f, float speedPerLevel = 0f)
        {
            var relic = AssetDatabase.LoadAssetAtPath<SimpleStatRelicData>(Relics + path);
            relic.simpleStatModifiers = new List<StatModifier>
            {
                new StatModifier { stat = StatType.ExtraProjectiles, value = extra }
            };
            if (damage > 0f) relic.simpleStatModifiers.Add(new StatModifier
                { stat = StatType.RangedDamage, value = damage, valuePerLevel = damagePerLevel, isPercentage = true });
            if (speed > 0f) relic.simpleStatModifiers.Add(new StatModifier
                { stat = StatType.RangedSpeed, value = speed, valuePerLevel = speedPerLevel, isPercentage = true });
            relic.InitializeSimpleStats(true);
            EditorUtility.SetDirty(relic);
            AssetDatabase.SaveAssetIfDirty(relic);
        }

        private static void ConfigureFrame(Image image, Sprite sprite)
        {
            if (image == null) return;
            image.sprite = sprite;
            PixelPerfectSlotFrame.Apply(image);
        }

        private static void AuditScenes()
        {
            var results = new List<string>();
            foreach (string path in new[] { "Assets/Scenes/GameScene.unity", "Assets/Scenes/Village.unity" })
            {
                Scene scene = SceneManager.GetSceneByPath(path);
                bool alreadyOpen = scene.IsValid() && scene.isLoaded;
                if (!alreadyOpen) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                try
                {
                    foreach (GameObject root in scene.GetRootGameObjects())
                    {
                        foreach (RelicGridUI ui in root.GetComponentsInChildren<RelicGridUI>(true))
                        {
                            GridLayoutGroup storage = ui.blockStorageParent.GetComponent<GridLayoutGroup>();
                            if (storage.constraintCount != 4 || storage.cellSize != Vector2.one * 128f ||
                                !Mathf.Approximately(ui.blockStorageParent.rect.width, 527f + storage.padding.horizontal) ||
                                ui.blockStorageParent.anchorMin != ui.blockStorageParent.anchorMax)
                                throw new InvalidOperationException(scene.name + " 유물 보관함 Inspector 규격 불일치: 열=" +
                                    storage.constraintCount + " 칸=" + storage.cellSize + " 영역=" + ui.blockStorageParent.rect.size);
                            var serialized = new SerializedObject(ui);
                            var cell = serialized.FindProperty("slotCellPrefab").objectReferenceValue as GameObject;
                            if (AssetDatabase.GetAssetPath(cell.GetComponent<Image>().sprite) !=
                                "Assets/Nytherion/Art/UI/Relics/EquipedRelic_Slot.png")
                                throw new InvalidOperationException(scene.name + " 장착 슬롯 이미지 참조 불일치");
                            results.Add("PASS " + scene.name + ": 유물 보관함 폭=" + ui.blockStorageParent.rect.width +
                                "·4열·고정 앵커·장착 전용 이미지 참조 (높이는 ContentSizeFitter로 슬롯 생성 후 결정)");
                        }
                        foreach (SkillSlotUI slot in root.GetComponentsInChildren<SkillSlotUI>(true))
                        {
                            float size = slot.slotType == SkillSlotType.Storage ? 128f : 160f;
                            if (slot.GetComponent<RectTransform>().rect.size != Vector2.one * size ||
                                slot.GetComponent<PixelPerfectSlotFrame>() == null)
                                throw new InvalidOperationException(scene.name + " " + slot.name + " 스킬 슬롯 Inspector 규격 불일치");
                            results.Add("PASS " + scene.name + "/" + slot.name + ": " + size + "×" + size + "·픽셀 테두리 보정");
                        }
                    }
                }
                finally { if (!alreadyOpen) EditorSceneManager.CloseScene(scene, true); }
            }
            File.WriteAllLines(Output + "/scene-audit.txt", results);
        }

        private static void ConfigureImporter(string path)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            // 32×32 원본의 모서리 4픽셀만 고정하고 단색 중앙은 늘린다.
            if (importer.spriteBorder == Vector4.one * 4f && importer.filterMode == FilterMode.Point &&
                !importer.mipmapEnabled && importer.textureCompression == TextureImporterCompression.Uncompressed) return;
            importer.spriteBorder = Vector4.one * 4f;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        private static void ConfigureIcon(Image image, float size)
        {
            if (image == null) return;
            RectTransform rect = image.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = Vector2.one * size;
            rect.anchoredPosition = Vector2.zero;
            rect.localScale = Vector3.one;
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
            image.raycastTarget = false;
        }
    }
}
