using System;
using System.IO;
using Nytherion.UI.Components;
using Nytherion.UI.Skill;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Nytherion.Editor
{
    /// <summary>전투용 쿨타임 HUD를 생성하고 DI로 생성되는 스킬 컨트롤러에 연결한다.</summary>
    [InitializeOnLoad]
    public static class SkillCooldownHUDSetup
    {
        public const string Output = "output/skill-cooldown";
        public const string PrefabPath = "Assets/Prefabs/UI/Skills/SkillCooldownHUD.prefab";
        public const string ControllerPath = "Assets/Prefabs/Infrastructure/Managers/SkillUIController.prefab";
        private const string SquarePath = "Assets/Nytherion/Art/UI/Legacy/Sprites/Square.png";

        static SkillCooldownHUDSetup() => EditorApplication.update += Update;

        private static void Update()
        {
            if (!EditorApplication.isCompiling && File.Exists(Output + "/refresh.request"))
            {
                File.Delete(Output + "/refresh.request");
                AssetDatabase.Refresh();
                return;
            }
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode ||
                (!File.Exists(Output + "/setup.request") && !File.Exists(Output + "/layout.request"))) return;
            if (File.Exists(Output + "/layout.request"))
            {
                File.Delete(Output + "/layout.request");
                try { UpdateOverlayLayout(); }
                catch (Exception error) { File.WriteAllText(Output + "/layout.txt", "FAIL " + error); Debug.LogException(error); }
                return;
            }
            File.Delete(Output + "/setup.request");
            try { Create(); }
            catch (Exception error) { File.WriteAllText(Output + "/setup.txt", "FAIL " + error); Debug.LogException(error); }
        }

        [MenuItem("Tools/Nytherion/Skills/쿨타임 HUD 생성 및 연결")]
        public static void Create()
        {
            Directory.CreateDirectory(Output);
            GameObject root = new GameObject("SkillCooldownHUD", typeof(RectTransform), typeof(SkillCooldownHUD));
            GameObject controller = null;
            try
            {
                var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Nytherion/Art/UI/Fonts/Galmuri/Galmuri11 SDF.asset");
                if (font == null) throw new InvalidOperationException("Galmuri11 폰트가 없습니다.");
                Sprite frameSprite = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/Skills/SkillEquipSlot.prefab").GetComponent<Image>().sprite;
                Sprite fillSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
                Sprite squareSprite = AssetDatabase.LoadAssetAtPath<Sprite>(SquarePath);
                if (frameSprite == null || fillSprite == null || squareSprite == null) throw new InvalidOperationException("슬롯/쿨타임 스프라이트가 없습니다.");
                var serialized = new SerializedObject(root.GetComponent<SkillCooldownHUD>());
                SerializedProperty slots = serialized.FindProperty("slots");
                slots.arraySize = 3;
                string[] keys = { "Q", "R", "E" }; // InputManager의 슬롯 0·1·2와 같은 순서.
                for (int i = 0; i < 3; i++)
                {
                    RectTransform slotRoot = Rect("Cooldown_" + keys[i], root.transform, Vector2.one * 100f, Vector2.zero);
                    Image overlay = Image("CooldownOverlay", slotRoot, Vector2.one * 80f, Vector2.zero, new Color(0f, 0f, 0f, 0.72f));
                    ConfigureOverlay(overlay, squareSprite);
                    overlay.enabled = false;
                    TMP_Text remaining = Text("CooldownText", slotRoot, font, Vector2.one * 80f, Vector2.zero, 30f);
                    remaining.enabled = false;
                    TMP_Text count = Text("ChargeCount", slotRoot, font, new Vector2(28f, 28f), new Vector2(27f, -26f), 22f);
                    count.color = new Color(0.6f, 1f, 0.85f);
                    count.enabled = false;
                    Image track = Image("RechargeTrack", slotRoot, new Vector2(80f, 5f), new Vector2(0f, -45f), new Color(0.08f, 0.1f, 0.15f));
                    Image progress = Image("RechargeFill", track.transform, new Vector2(80f, 5f), Vector2.zero, new Color(0.4f, 0.9f, 0.75f));
                    progress.sprite = fillSprite;
                    progress.type = UnityEngine.UI.Image.Type.Filled;
                    progress.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
                    progress.fillOrigin = 0;
                    track.gameObject.SetActive(false);
                    Image flash = Image("ReadyFlash", slotRoot, Vector2.one * 100f, Vector2.zero, new Color(0.6f, 1f, 0.85f));
                    flash.sprite = frameSprite;
                    PixelPerfectSlotFrame.Apply(flash);
                    flash.enabled = false;
                    SerializedProperty slot = slots.GetArrayElementAtIndex(i);
                    Assign(slot, "root", slotRoot);
                    Assign(slot, "cooldownOverlay", overlay);
                    Assign(slot, "cooldownText", remaining);
                    Assign(slot, "readyFlash", flash);
                    Assign(slot, "chargeText", count);
                    Assign(slot, "rechargeTrack", track.gameObject);
                    Assign(slot, "rechargeFill", progress);
                }
                serialized.ApplyModifiedPropertiesWithoutUndo();
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                controller = PrefabUtility.LoadPrefabContents(ControllerPath);
                var controllerData = new SerializedObject(controller.GetComponent<SkillUIController>());
                controllerData.FindProperty("cooldownHUDPrefab").objectReferenceValue = prefab.GetComponent<SkillCooldownHUD>();
                controllerData.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(controller, ControllerPath);
                File.WriteAllText(Output + "/setup.txt", "PASS 기존 Q/R/E 슬롯에 붙일 쿨타임 효과 3개 생성, 컨트롤러 Inspector 참조 연결");
            }
            finally
            {
                if (controller != null) PrefabUtility.UnloadPrefabContents(controller);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [MenuItem("Tools/Nytherion/Skills/쿨타임 덮개를 슬롯에 맞추기")]
        public static void UpdateOverlayLayout()
        {
            GameObject prefab = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                Sprite square = AssetDatabase.LoadAssetAtPath<Sprite>(SquarePath);
                if (square == null) throw new InvalidOperationException("사각형 덮개 스프라이트가 없습니다.");
                var data = new SerializedObject(prefab.GetComponent<SkillCooldownHUD>());
                SerializedProperty slots = data.FindProperty("slots");
                for (int i = 0; i < slots.arraySize; i++)
                {
                    SerializedProperty slot = slots.GetArrayElementAtIndex(i);
                    var root = (RectTransform)slot.FindPropertyRelative("root").objectReferenceValue;
                    root.sizeDelta = Vector2.one * 100f;
                    ConfigureOverlay((Image)slot.FindPropertyRelative("cooldownOverlay").objectReferenceValue, square);
                }
                PrefabUtility.SaveAsPrefabAsset(prefab, PrefabPath);
                Directory.CreateDirectory(Output);
                File.WriteAllText(Output + "/layout.txt", "PASS 사각형 덮개·슬롯 전체 크기·12시부터 시계 방향으로 걷히는 설정 적용 (3개 슬롯)");
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
        }

        private static void ConfigureOverlay(Image overlay, Sprite square)
        {
            overlay.sprite = square;
            overlay.preserveAspect = false;
            overlay.type = UnityEngine.UI.Image.Type.Filled;
            overlay.fillMethod = UnityEngine.UI.Image.FillMethod.Radial360;
            overlay.fillOrigin = (int)UnityEngine.UI.Image.Origin360.Top;
            // 남은 비율이 1→0으로 감소하므로 채움 방향을 반대로 해야 시계 방향으로 걷힌다.
            overlay.fillClockwise = false;
            RectTransform rect = overlay.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private static void Assign(SerializedProperty slot, string name, UnityEngine.Object value) =>
            slot.FindPropertyRelative(name).objectReferenceValue = value;

        private static RectTransform Rect(string name, Transform parent, Vector2 size, Vector2 position)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.gameObject.layer = 5;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            return rect;
        }

        private static Image Image(string name, Transform parent, Vector2 size, Vector2 position, Color color)
        {
            Image image = Rect(name, parent, size, position).gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static TMP_Text Text(string name, Transform parent, TMP_FontAsset font, Vector2 size, Vector2 position, float fontSize)
        {
            var text = Rect(name, parent, size, position).gameObject.AddComponent<TextMeshProUGUI>();
            text.font = font;
            text.fontSize = fontSize;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            text.enableWordWrapping = false;
            text.text = "";
            var shadow = text.gameObject.AddComponent<Shadow>();
            shadow.effectColor = Color.black;
            shadow.effectDistance = new Vector2(1f, -1f);
            return text;
        }
    }
}
