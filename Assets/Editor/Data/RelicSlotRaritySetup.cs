using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using Nytherion.Core.Enums;
using Nytherion.UI.Inventory;
using Nytherion.UI.Components;
using Nytherion.UI.RelicBoard;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Nytherion.Editor
{
    [InitializeOnLoad]
    public static class RelicSlotRaritySetup
    {
        private const string Output = "output/relic-slot-rarity";
        private const string Inventory = "Assets/Prefabs/UI/Inventory/InventorySlot.prefab";
        private static readonly string[] Paths = { "Assets/Prefabs/UI/Relic/StorageSlot.prefab", "Assets/Prefabs/UI/Relic/RelicSlotCell.prefab" };
        private static readonly string[] Fields = { "commonSlotSprite", "uncommonSlotSprite", "rareSlotSprite", "epicSlotSprite", "legendarySlotSprite" };
        static RelicSlotRaritySetup() => EditorApplication.update += Update;

        private static void Update()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode ||
                !File.Exists(Output + "/setup.request")) return;
            File.Delete(Output + "/setup.request");
            ApplyAndVerify();
        }

        [MenuItem("Tools/Nytherion/Relics/Apply And Verify Rarity Slot Frames")]
        public static void ApplyAndVerify()
        {
            Directory.CreateDirectory(Output);
            var results = new List<string>();
            try
            {
                var inventory = new SerializedObject(AssetDatabase.LoadAssetAtPath<GameObject>(Inventory).GetComponent<InventorySlotUI>());
                foreach (string field in Fields)
                    Require(inventory.FindProperty(field).objectReferenceValue is Sprite, "인벤토리 등급 이미지 누락: " + field);
                string archive = "Documentation/RelicSlotRarityOriginals.zip";
                if (!File.Exists(archive))
                    using (var zip = ZipFile.Open(archive, ZipArchiveMode.Create))
                        foreach (string path in Paths)
                        {
                            zip.CreateEntryFromFile(path, path);
                            zip.CreateEntryFromFile(path + ".meta", path + ".meta");
                        }
                foreach (string path in Paths)
                {
                    string guid = AssetDatabase.AssetPathToGUID(path);
                    GameObject root = PrefabUtility.LoadPrefabContents(path);
                    try
                    {
                        RelicSlotCell cell = root.GetComponent<RelicSlotCell>();
                        Image frame = cell != null ? cell.BackgroundImage : root.GetComponent<Image>();
                        Require(frame != null, "프레임 Inspector 참조 누락: " + path);
                        var visual = frame.GetComponent<RelicSlotVisual>();
                        if (visual == null) visual = frame.gameObject.AddComponent<RelicSlotVisual>();
                        var destination = new SerializedObject(visual);
                        destination.FindProperty("defaultSlotSprite").objectReferenceValue = frame.sprite;
                        foreach (string field in Fields)
                            destination.FindProperty(field).objectReferenceValue = inventory.FindProperty(field).objectReferenceValue;
                        destination.ApplyModifiedPropertiesWithoutUndo();
                        PrefabUtility.SaveAsPrefabAsset(root, path);
                    }
                    finally { PrefabUtility.UnloadPrefabContents(root); }
                    Require(AssetDatabase.AssetPathToGUID(path) == guid, "프리팹 GUID 유지");
                    results.Add("PASS 인벤토리 5등급 스프라이트 연결·기존 빈 슬롯 유지: " + path);
                }
                VerifyAndRender(inventory);
                results.Add("PASS 5등급 × 보관·장착 슬롯: 이미지 일치, 강조 해제 시 등급 유지, 비우기 시 기본 프레임·아이콘 복원");
                results.Add("PASS 실제 프리팹 렌더링: inventory-storage-equipped.png (위: 인벤토리 / 중간: 보관 / 아래: 장착)");
            }
            catch (Exception error) { results.Add("FAIL " + error); }
            File.WriteAllLines(Output + "/verification.txt", results);
        }

        private static void VerifyAndRender(SerializedObject inventory)
        {
            Scene scene = EditorSceneManager.NewPreviewScene();
            RenderTexture target = null;
            Texture2D readback = null;
            try
            {
                var cameraRoot = new GameObject("등급 프레임 검증 카메라", typeof(Camera));
                SceneManager.MoveGameObjectToScene(cameraRoot, scene);
                Camera camera = cameraRoot.GetComponent<Camera>();
                camera.scene = scene;
                camera.enabled = false;
                camera.orthographic = true;
                camera.orthographicSize = 300f;
                camera.nearClipPlane = 0.1f;
                camera.farClipPlane = 10f;
                camera.cullingMask = 1 << 31;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.035f, 0.04f, 0.055f);
                cameraRoot.transform.position = new Vector3(20000, 20000, -5);
                target = new RenderTexture(1000, 600, 24) { antiAliasing = 1 };
                camera.targetTexture = target;
                var canvasRoot = new GameObject("등급 프레임 검증 Canvas", typeof(RectTransform), typeof(Canvas));
                SceneManager.MoveGameObjectToScene(canvasRoot, scene);
                canvasRoot.layer = 31;
                Canvas canvas = canvasRoot.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1f;
                Sprite inventoryDefault = (Sprite)inventory.FindProperty("defaultSlotSprite").objectReferenceValue;
                for (int row = 0; row < 3; row++)
                for (int column = 0; column < 6; column++)
                {
                    string path = row == 0 ? Inventory : Paths[row - 1];
                    var slot = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path), scene);
                    slot.transform.SetParent(canvasRoot.transform, false);
                    foreach (Transform child in slot.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 31;
                    var rect = slot.GetComponent<RectTransform>();
                    rect.anchorMin = rect.anchorMax = Vector2.one * 0.5f;
                    rect.anchoredPosition = new Vector2(-390 + column * 156, 170 - row * 170);
                    rect.sizeDelta = Vector2.one * (row == 2 ? 96 : 128);
                    Rarity? rarity = column == 0 ? (Rarity?)null : (Rarity)(column - 1);
                    Sprite expected = rarity.HasValue ? (Sprite)inventory.FindProperty(Fields[(int)rarity.Value]).objectReferenceValue : inventoryDefault;
                    if (row == 0)
                    {
                        slot.GetComponent<Image>().sprite = expected;
                        PixelPerfectSlotFrame.Apply(slot.GetComponent<Image>());
                        foreach (Transform child in slot.transform) child.gameObject.SetActive(false);
                    }
                    else
                    {
                        var cell = slot.GetComponent<RelicSlotCell>();
                        Image frame = cell != null ? cell.BackgroundImage : slot.GetComponent<Image>();
                        var visual = frame.GetComponent<RelicSlotVisual>();
                        Sprite empty = (Sprite)new SerializedObject(visual).FindProperty("defaultSlotSprite").objectReferenceValue;
                        visual.SetRarity(rarity);
                        Require(frame.sprite == (rarity.HasValue ? expected : empty), "등급 프레임 불일치: " + path + " " + rarity);
                        Color original = frame.color;
                        visual.Highlight(true);
                        visual.Highlight(false);
                        Require(frame.color == original, "강조 색상 복원");
                        if (cell != null)
                        {
                            cell.SetRelic(expected);
                            cell.ClearCell();
                            var icon = (Image)new SerializedObject(cell).FindProperty("relicIcon").objectReferenceValue;
                            Require(!icon.enabled && icon.sprite == null, "비운 장착 슬롯의 아이콘 제거");
                        }
                        else visual.SetRarity(null);
                        Require(frame.sprite == empty, "비운 슬롯의 기본 프레임 복원");
                        visual.SetRarity(rarity);
                    }
                }
                Canvas.ForceUpdateCanvases();
                camera.Render();
                readback = new Texture2D(1000, 600, TextureFormat.RGB24, false);
                RenderTexture previous = RenderTexture.active;
                try
                {
                    RenderTexture.active = target;
                    readback.ReadPixels(new Rect(0, 0, 1000, 600), 0, 0);
                    readback.Apply();
                }
                finally { RenderTexture.active = previous; }
                File.WriteAllBytes(Output + "/inventory-storage-equipped.png", readback.EncodeToPNG());
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
                if (target != null) { target.Release(); Object.DestroyImmediate(target); }
                if (readback != null) Object.DestroyImmediate(readback);
            }
        }
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }
}
