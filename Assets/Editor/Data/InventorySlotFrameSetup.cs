using System;
using System.Collections.Generic;
using System.IO;
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
    /// <summary>인벤토리 테두리의 임포트 설정을 적용하고 실제 UGUI 렌더링으로 픽셀 두께를 검증한다.</summary>
    [InitializeOnLoad]
    public static class InventorySlotFrameSetup
    {
        private const string Output = "output/inventory-slot-frame";
        private const string PrefabPath = "Assets/Prefabs/UI/Inventory/InventorySlot.prefab";
        private const string ArtPath = "Assets/Nytherion/Art/UI/Inventory/";
        private const string PendingPlay = "Nytherion.InventorySlotFrame.VerifyPlay";
        private static double playReadyAt;
        private static readonly string[] SpriteNames =
            { "Slot", "CommonSlot", "UncommonSlot", "RareSlot", "EpicSlot", "LegendarySlot" };

        static InventorySlotFrameSetup()
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
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!File.Exists(Output + "/setup.request")) return;
            File.Delete(Output + "/setup.request");
            try { Apply(); Verify(); }
            catch (Exception error)
            {
                File.WriteAllText(Output + "/verification.txt", "FAIL " + error);
                Debug.LogException(error);
            }
        }

        private static void VerifyLiveSlots()
        {
            InventoryUI inventory = Object.FindObjectOfType<InventoryUI>(true);
            if (inventory == null) throw new InvalidOperationException("실행 씬에 InventoryUI가 없습니다.");
            bool wasOpen = inventory.IsOpen;
            try
            {
                inventory.Open();
                Canvas.ForceUpdateCanvases();
                // 컨트롤러와 실제 슬롯 부모는 씬에서 서로 다른 오브젝트다.
                InventorySlotUI[] slots = Object.FindObjectsOfType<InventorySlotUI>(true);
                if (slots.Length == 0) throw new InvalidOperationException("실행 중 인벤토리 슬롯이 생성되지 않았습니다.");
                foreach (InventorySlotUI slot in slots)
                {
                    Image image = slot.GetComponent<Image>();
                    if (image == null || image.type != Image.Type.Sliced || image.sprite == null ||
                        image.sprite.border != Vector4.one * 4f || slot.GetComponent<PixelPerfectSlotFrame>() == null)
                        throw new InvalidOperationException("실행 중 슬롯 테두리 설정 누락: " + slot.name);
                    slot.GetComponent<PixelPerfectSlotFrame>().Refresh();
                }
                File.WriteAllText(Output + "/play-verification.txt", "PASS " + SceneManager.GetActiveScene().name +
                    " 플레이 모드: 인벤토리 열기, 실제 생성 슬롯 " + slots.Length + "개의 픽셀 보정/분할 테두리 설정 확인");
            }
            finally { if (!wasOpen) inventory.Close(); }
        }

        [MenuItem("Tools/Nytherion/Inventory/Apply Pixel Perfect Slot Frames")]
        public static void Apply()
        {
            Directory.CreateDirectory(Output);
            foreach (string name in SpriteNames)
            {
                string path = ArtPath + name + ".png";
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) throw new InvalidOperationException("슬롯 이미지 누락: " + path);
                // 모서리와 외곽선을 보존하고 단색 중앙 영역만 늘린다.
                importer.spriteBorder = Vector4.one * 4f;
                importer.SaveAndReimport();
            }
            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                PixelPerfectSlotFrame.Apply(root.GetComponent<Image>());
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            File.WriteAllText(Output + "/setup.txt", "PASS 인벤토리 프리팹과 6개 배경의 픽셀 테두리 설정 적용 (GUID 유지)");
        }

        [MenuItem("Tools/Nytherion/Inventory/Verify Slot Frame Rendering")]
        public static void Verify()
        {
            Directory.CreateDirectory(Output);
            Scene scene = EditorSceneManager.NewPreviewScene();
            RenderTexture target = null;
            Texture2D readback = null;
            var results = new List<string>();
            try
            {
                var cameraObject = new GameObject("슬롯 검증 카메라", typeof(Camera));
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
                var root = new GameObject("슬롯 검증 Canvas", typeof(RectTransform), typeof(Canvas));
                SceneManager.MoveGameObjectToScene(root, scene);
                root.layer = 31;
                Canvas canvas = root.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1f;
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                var slot = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                slot.transform.SetParent(root.transform, false);
                slot.layer = 31;
                foreach (Transform child in slot.transform) child.gameObject.SetActive(false);
                RectTransform rect = slot.GetComponent<RectTransform>();
                rect.anchorMin = rect.anchorMax = Vector2.one * 0.5f;
                Image image = slot.GetComponent<Image>();
                PixelPerfectSlotFrame effect = slot.GetComponent<PixelPerfectSlotFrame>();
                if (effect == null || image.type != Image.Type.Sliced)
                    throw new InvalidOperationException("프리팹 픽셀 보정 컴포넌트/분할 설정 누락");
                readback = new Texture2D(640, 640, TextureFormat.RGB24, false);
                foreach (string name in SpriteNames)
                {
                    image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ArtPath + name + ".png");
                    foreach (float scale in new[] { 0.24f, 0.75f, 1f, 1.25f, 1.5f })
                    {
                        canvas.scaleFactor = scale;
                        foreach (Vector2 position in new[] { Vector2.zero, new Vector2(0.37f, 0.63f), new Vector2(5.21f, -3.47f) })
                        {
                            rect.anchoredPosition = position;
                            Canvas.ForceUpdateCanvases();
                            effect.Refresh();
                            Canvas.ForceUpdateCanvases();
                            camera.Render();
                            RenderTexture previous = RenderTexture.active;
                            try
                            {
                                RenderTexture.active = target;
                                readback.ReadPixels(new Rect(0, 0, 640, 640), 0, 0);
                                readback.Apply();
                            }
                            finally { RenderTexture.active = previous; }
                            Vector3[] corners = new Vector3[4];
                            rect.GetWorldCorners(corners);
                            Vector2 min = camera.WorldToScreenPoint(corners[0]);
                            Vector2 max = camera.WorldToScreenPoint(corners[2]);
                            int left = Mathf.RoundToInt(min.x), right = Mathf.RoundToInt(max.x) - 1;
                            int bottom = Mathf.RoundToInt(min.y), top = Mathf.RoundToInt(max.y) - 1;
                            int x = (left + right) / 2, y = (bottom + top) / 2;
                            int[] widths = { EdgeWidth(readback, left, y, 1, 0), EdgeWidth(readback, right, y, -1, 0),
                                EdgeWidth(readback, x, bottom, 0, 1), EdgeWidth(readback, x, top, 0, -1) };
                            int expected = Mathf.Max(1, Mathf.FloorToInt(rect.rect.width * scale / image.sprite.rect.width + 0.5f));
                            foreach (int width in widths)
                                if (width != expected) throw new InvalidOperationException(name + " 배율=" + scale +
                                    " 위치=" + position + " 선 두께=" + string.Join(",", widths) + " 예상=" + expected);
                            if (scale == 0.75f && position == Vector2.zero)
                                File.WriteAllBytes(Output + "/" + name + ".png", readback.EncodeToPNG());
                        }
                    }
                    results.Add("PASS " + name + ": 5개 화면 배율 × 3개 위치에서 좌·우·상·하 테두리 두께 일치");
                }
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
                if (target != null) { target.Release(); Object.DestroyImmediate(target); }
                if (readback != null) Object.DestroyImmediate(readback);
            }
            File.WriteAllLines(Output + "/verification.txt", results);
        }

        private static int EdgeWidth(Texture2D texture, int x, int y, int dx, int dy)
        {
            Color32 edge = texture.GetPixel(x, y);
            if (edge.r == 0 && edge.g == 0 && edge.b == 0) return 0;
            for (int width = 1; width <= 12; width++)
            {
                Color32 pixel = texture.GetPixel(x + dx * width, y + dy * width);
                if (pixel.r != edge.r || pixel.g != edge.g || pixel.b != edge.b) return width;
            }
            return -1;
        }
    }
}
