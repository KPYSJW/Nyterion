using System;
using System.Collections.Generic;
using System.IO;
using Nytherion.Data.ScriptableObjects.Items;
using Nytherion.Data.ScriptableObjects.Weapons;
using Nytherion.UI.Components;
using Nytherion.UI.Controllers;
using Nytherion.UI.Inventory;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Nytherion.Editor
{
    /// <summary>실제 슬롯과 아이콘의 렌더링에서 원본 픽셀의 사각형 블록이 유지되는지 확인한다.</summary>
    [InitializeOnLoad]
    public static class InventoryIconVerification
    {
        private const string Output = "output/inventory-icon";
        private const string PendingPlay = "Nytherion.InventoryIcon.VerifyPlay";
        private static double playReadyAt;
        private static readonly Dictionary<Sprite, Rect> SourceVisibleRects = new Dictionary<Sprite, Rect>();

        [Serializable]
        private class ScaleRequest
        {
            public string assetPath;
            public float scale;
            public float iconSlotScale;
        }

        static InventoryIconVerification()
        {
            EditorApplication.update += Update;
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredPlayMode)
                    playReadyAt = EditorApplication.timeSinceStartup + 5d;
            };
        }

        private static void Update()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (!EditorApplication.isPlayingOrWillChangePlaymode && File.Exists(Output + "/scale.request"))
            {
                ScaleRequest request = JsonUtility.FromJson<ScaleRequest>(File.ReadAllText(Output + "/scale.request"));
                File.Delete(Output + "/scale.request");
                WeaponData data = AssetDatabase.LoadAssetAtPath<WeaponData>(request.assetPath);
                if (data == null) throw new InvalidOperationException("아이콘 크기 조정 대상 누락: " + request.assetPath);
                Undo.RecordObject(data, "인벤토리 아이콘 크기 조정");
                data.inventoryIconScale = Mathf.Max(0.1f, request.scale);
                if (request.iconSlotScale > 0f) data.iconSlotScale = request.iconSlotScale;
                EditorUtility.SetDirty(data);
                AssetDatabase.SaveAssetIfDirty(data);
                File.WriteAllText(Output + "/scale-setup.txt", request.assetPath + ": inventoryIconScale=" + data.inventoryIconScale);
                return;
            }
            if (SessionState.GetBool(PendingPlay, false) && EditorApplication.isPlaying &&
                EditorApplication.timeSinceStartup >= playReadyAt)
            {
                SessionState.SetBool(PendingPlay, false);
                try { VerifyLiveSlots(); }
                catch (Exception error) { File.WriteAllText(Output + "/play-verification.txt", "FAIL " + error); }
                finally { if (SessionState.GetBool(PendingPlay + ".Exit", false)) EditorApplication.isPlaying = false; }
                return;
            }
            if (File.Exists(Output + "/play.request"))
            {
                File.Delete(Output + "/play.request");
                SessionState.SetBool(PendingPlay + ".Exit", !EditorApplication.isPlaying);
                SessionState.SetBool(PendingPlay, true);
                playReadyAt = EditorApplication.timeSinceStartup + 12d;
                if (!EditorApplication.isPlaying) EditorApplication.isPlaying = true;
                return;
            }
            if (EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(Output + "/verify.request")) return;
            File.Delete(Output + "/verify.request");
            Verify();
        }

        private static void VerifyLiveSlots()
        {
            InventoryUI inventory = Object.FindObjectOfType<InventoryUI>(true);
            if (inventory == null) throw new InvalidOperationException("실행 씬에 InventoryUI가 없습니다.");
            bool wasOpen = inventory.IsOpen;
            ItemData testItem = AssetDatabase.LoadAssetAtPath<ItemData>(
                "Assets/Nytherion/Data/ScriptableObjects/Weapons/SaintsStaff.asset");
            int verified = 0;
            try
            {
                inventory.Open();
                Canvas.ForceUpdateCanvases();
                foreach (BaseSlotUI slot in Object.FindObjectsOfType<BaseSlotUI>(true))
                {
                    if (!(slot is InventorySlotUI) && !(slot is EquipmentSlotUI)) continue;
                    if (!slot.gameObject.activeInHierarchy) continue;
                    ItemData original = slot.CurrentItem;
                    int count = slot.CurrentCount;
                    try
                    {
                        // 매니저의 인벤토리/저장 데이터는 건드리지 않고 표시만 검사한 뒤 즉시 복구한다.
                        slot.SetItem(testItem, 1);
                        Canvas.ForceUpdateCanvases();
                        PixelPerfectItemIcon layout = slot.IconImage.GetComponent<PixelPerfectItemIcon>();
                        if (layout == null || slot.IconImage.sprite != testItem.icon || !slot.IconImage.enabled)
                            throw new InvalidOperationException("실행 슬롯의 픽셀 아이콘 설정 누락: " + slot.name);
                        layout.Refresh();
                        if (Quaternion.Angle(slot.IconImage.rectTransform.localRotation, Quaternion.identity) > 0.01f)
                            throw new InvalidOperationException("실행 슬롯 아이콘 회전: " + slot.name);
                        VerifyContained(slot);
                        verified++;
                    }
                    finally { slot.SetItem(original, count); }
                }
                if (verified == 0) throw new InvalidOperationException("실행 슬롯이 없습니다.");
                File.WriteAllText(Output + "/play-verification.txt", "PASS " + SceneManager.GetActiveScene().name +
                    " 플레이 모드: 실제 인벤토리 열기, 장착·보관 슬롯 " + verified + "개 아이콘 표시/슬롯 내부 배치/원본 방향/복원");
            }
            finally { if (!wasOpen) inventory.Close(); }
        }

        [MenuItem("Tools/Nytherion/Inventory/Verify Pixel Icons")]
        public static void Verify()
        {
            Directory.CreateDirectory(Output);
            var results = new List<string>();
            Scene scene = EditorSceneManager.NewPreviewScene();
            RenderTexture target = null;
            Texture2D readback = null;
            var temporaryItems = new List<ItemData>();
            try
            {
                var cameraObject = new GameObject("아이콘 검증 카메라", typeof(Camera));
                SceneManager.MoveGameObjectToScene(cameraObject, scene);
                Camera camera = cameraObject.GetComponent<Camera>();
                camera.scene = scene;
                camera.enabled = false;
                camera.orthographic = true;
                camera.orthographicSize = 320f;
                camera.nearClipPlane = 0.1f;
                camera.farClipPlane = 10f;
                camera.cullingMask = 1 << 31;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                cameraObject.transform.position = new Vector3(20000f, 20000f, -5f);
                target = new RenderTexture(640, 640, 24) { antiAliasing = 1 };
                camera.targetTexture = target;
                readback = new Texture2D(640, 640, TextureFormat.RGB24, false);
                var root = new GameObject("아이콘 검증 Canvas", typeof(RectTransform), typeof(Canvas));
                SceneManager.MoveGameObjectToScene(root, scene);
                root.layer = 31;
                Canvas canvas = root.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1f;
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/Inventory/InventorySlot.prefab");
                var slotObject = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                slotObject.transform.SetParent(root.transform, false);
                foreach (Transform child in slotObject.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 31;
                InventorySlotUI slot = slotObject.GetComponent<InventorySlotUI>();
                RectTransform slotRect = slotObject.GetComponent<RectTransform>();
                slotObject.transform.Find("CountText").gameObject.SetActive(false);
                string[] paths = {
                    "Weapons/ArcaneDart.asset", "Weapons/Frenzy.asset", "Weapons/ShamanStaff.asset", "Weapons/SaintsStaff.asset",
                    "Weapons/GuardianStaff.asset", "Weapons/EvilEye.asset", "Weapons/ForestThorn.asset",
                    "Assets/Nytherion/Art/Relics/Sprites/BloomRing.png",
                    "Assets/Nytherion/Art/Relics/Sprites/SkullRing.png"
                };
                foreach (string path in paths)
                {
                    ItemData item;
                    if (path.StartsWith("Assets/", StringComparison.Ordinal))
                    {
                        // 장비 데이터와 독립적인 임시 아이템으로 비무기 아이콘 경로도 확인한다.
                        item = ScriptableObject.CreateInstance<ConsumableData>();
                        item.name = Path.GetFileNameWithoutExtension(path);
                        item.icon = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                        temporaryItems.Add(item);
                    }
                    else item = AssetDatabase.LoadAssetAtPath<ItemData>("Assets/Nytherion/Data/ScriptableObjects/" + path);
                    if (item == null || item.icon == null) throw new InvalidOperationException("검증 아이콘 누락: " + path);
                    foreach (float scale in new[] { 0.125f, 0.333f, 0.5f, 0.75f, 1f, 1.25f, 1.5f })
                    foreach (bool pixelPerfect in new[] { false, true })
                    {
                        canvas.scaleFactor = scale;
                        canvas.pixelPerfect = pixelPerfect;
                        slotRect.anchoredPosition = new Vector2(0.37f, -3.47f);
                        Canvas.ForceUpdateCanvases();
                        slot.SetItem(item, 1);
                        slot.GetComponent<Image>().enabled = false;
                        PixelPerfectItemIcon layout = slot.IconImage.GetComponent<PixelPerfectItemIcon>();
                        if (layout == null) throw new InvalidOperationException("픽셀 아이콘 레이아웃 누락");
                        layout.Refresh();
                        Canvas.ForceUpdateCanvases();
                        RectTransform icon = slot.IconImage.rectTransform;
                        if (Quaternion.Angle(icon.localRotation, Quaternion.identity) > 0.01f)
                            throw new InvalidOperationException("원본 방향이 유지되지 않음: " + path);
                        Vector3[] corners = new Vector3[4];
                        icon.GetWorldCorners(corners);
                        Vector2 min = camera.WorldToScreenPoint(corners[0]);
                        Vector2 max = camera.WorldToScreenPoint(corners[2]);
                        Vector2 size = max - min;
                        float pixelScale = size.x / item.icon.rect.width;
                        bool integerScale = !(item is WeaponData weapon) || weapon.inventoryIconScale <= 1f;
                        if (integerScale && pixelScale >= 1f && (Mathf.Abs(pixelScale - Mathf.Round(pixelScale)) > 0.002f ||
                            Mathf.Abs(min.x - Mathf.Round(min.x)) > 0.002f || Mathf.Abs(min.y - Mathf.Round(min.y)) > 0.002f))
                            throw new InvalidOperationException("정수 픽셀 배율/위치 불일치: " + path);
                        VerifyContained(slot);
                        camera.Render();
                        RenderTexture previous = RenderTexture.active;
                        try
                        {
                            RenderTexture.active = target;
                            readback.ReadPixels(new Rect(0, 0, 640, 640), 0, 0);
                            readback.Apply();
                        }
                        finally { RenderTexture.active = previous; }
                        if (integerScale && pixelScale >= 1f)
                            VerifyPixelBlocks(readback, min, item.icon.rect.size, Mathf.RoundToInt(pixelScale));
                        if (scale == 1f && !pixelPerfect)
                        {
                            if ((item.name == "SaintsStaff" && pixelScale < 2.99f) ||
                                (item.name == "Frenzy" && pixelScale < 1.99f))
                                throw new InvalidOperationException("기존 장비 표시 크기보다 작아짐: " + item.name);
                            if ((item.name == "GuardianStaff" || item.name == "EvilEye") &&
                                ReadSourceVisibleRect(item.icon).height * pixelScale <
                                (slotRect.rect.height - 16f) * scale - 1f)
                                throw new InvalidOperationException("개별 확대 장비가 슬롯 안쪽을 채우지 못함: " + item.name);
                            File.WriteAllBytes(Output + "/" + item.name + ".png", readback.EncodeToPNG());
                        }
                    }
                    slot.ClearSlot();
                    if (slot.IconImage.enabled || slot.IconImage.sprite != null)
                        throw new InvalidOperationException("슬롯 비우기 실패");
                    results.Add("PASS " + path + ": 7개 화면 배율, Canvas 픽셀 보정 켜짐/꺼짐, 슬롯 내부 배치, 원본 방향, 픽셀 블록, 교체/비우기");
                }
            }
            catch (Exception error) { results.Add("FAIL " + error); Debug.LogException(error); }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
                if (target != null) { target.Release(); Object.DestroyImmediate(target); }
                if (readback != null) Object.DestroyImmediate(readback);
                foreach (ItemData item in temporaryItems) Object.DestroyImmediate(item);
            }
            File.WriteAllLines(Output + "/verification.txt", results);
        }

        private static void VerifyPixelBlocks(Texture2D rendered, Vector2 origin, Vector2 sourceSize, int scale)
        {
            int left = Mathf.RoundToInt(origin.x), bottom = Mathf.RoundToInt(origin.y);
            bool hasVisiblePixel = false;
            for (int y = 0; y < sourceSize.y; y++)
            for (int x = 0; x < sourceSize.x; x++)
            {
                Color32 expected = rendered.GetPixel(left + x * scale, bottom + y * scale);
                if (expected.r != 0 || expected.g != 0 || expected.b != 0) hasVisiblePixel = true;
                for (int dy = 0; dy < scale; dy++)
                for (int dx = 0; dx < scale; dx++)
                {
                    Color32 actual = rendered.GetPixel(left + x * scale + dx, bottom + y * scale + dy);
                    if (actual.r != expected.r || actual.g != expected.g || actual.b != expected.b)
                        throw new InvalidOperationException("원본 한 픽셀의 화면 블록이 불균일함: " + x + "," + y);
                }
            }
            if (!hasVisiblePixel) throw new InvalidOperationException("아이콘이 실제 렌더링되지 않음");
        }

        private static void VerifyContained(BaseSlotUI slot)
        {
            Canvas canvas = slot.IconImage.canvas.rootCanvas;
            Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            RectTransform slotRect = (RectTransform)slot.transform;
            var corners = new Vector3[4];
            slotRect.GetWorldCorners(corners);
            Vector2 min = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
            Vector2 max = RectTransformUtility.WorldToScreenPoint(camera, corners[2]);
            Vector2 padding = new Vector2(8f * (max.x - min.x) / slotRect.rect.width,
                8f * (max.y - min.y) / slotRect.rect.height);
            RectTransform icon = slot.IconImage.rectTransform;
            Rect visible = ReadSourceVisibleRect(slot.IconImage.sprite);
            Vector2 sourceSize = slot.IconImage.sprite.rect.size;
            Vector2 localMin = icon.rect.min + Vector2.Scale(visible.min, icon.rect.size / sourceSize);
            Vector2 localMax = icon.rect.min + Vector2.Scale(visible.max, icon.rect.size / sourceSize);
            Vector2[] visibleCorners = { localMin, new Vector2(localMin.x, localMax.y), localMax,
                new Vector2(localMax.x, localMin.y) };
            foreach (Vector2 corner in visibleCorners)
            {
                Vector2 point = RectTransformUtility.WorldToScreenPoint(camera, icon.TransformPoint(corner));
                // 정수 픽셀 정렬로 생기는 최대 반 픽셀 이동만 허용한다.
                const float tolerance = 0.51f;
                if (point.x < min.x + padding.x - tolerance || point.x > max.x - padding.x + tolerance ||
                    point.y < min.y + padding.y - tolerance || point.y > max.y - padding.y + tolerance)
                    throw new InvalidOperationException("아이콘이 슬롯 안쪽 여백을 벗어남: " + slot.name + " / " + slot.CurrentItem.name);
            }
            Vector2 visibleCenter = RectTransformUtility.WorldToScreenPoint(camera,
                icon.TransformPoint((localMin + localMax) * 0.5f));
            Vector2 offset = visibleCenter - (min + max) * 0.5f;
            if (Mathf.Abs(offset.x) > 0.51f || Mathf.Abs(offset.y) > 0.51f)
                throw new InvalidOperationException("장비 그림이 슬롯 중심에서 벗어남: " + slot.CurrentItem.name);
        }

        private static Rect ReadSourceVisibleRect(Sprite sprite)
        {
            if (SourceVisibleRects.TryGetValue(sprite, out Rect cached)) return cached;
            // 런타임 GPU 계산과 독립적으로 원본 PNG의 알파 영역을 검사한다.
            var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                source.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(sprite)));
                Rect rect = sprite.rect;
                int width = Mathf.RoundToInt(rect.width), height = Mathf.RoundToInt(rect.height);
                int minX = width, minY = height, maxX = -1, maxY = -1;
                for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    if (source.GetPixel((int)rect.x + x, (int)rect.y + y).a == 0f) continue;
                    minX = Mathf.Min(minX, x); minY = Mathf.Min(minY, y);
                    maxX = Mathf.Max(maxX, x); maxY = Mathf.Max(maxY, y);
                }
                Rect visible = maxX >= minX && maxY >= minY
                    ? new Rect(minX, minY, maxX - minX + 1, maxY - minY + 1)
                    : new Rect(Vector2.zero, rect.size);
                SourceVisibleRects[sprite] = visible;
                return visible;
            }
            finally { Object.DestroyImmediate(source); }
        }
    }
}
