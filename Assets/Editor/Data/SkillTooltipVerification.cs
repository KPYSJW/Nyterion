using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Nytherion.Data.ScriptableObjects.Skill;
using Nytherion.UI.Skill;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Nytherion.Editor
{
    /// <summary>현재 씬의 스킬 UI 연결과 전용 툴팁 수명 주기를 플레이 모드에서 확인한다.</summary>
    [InitializeOnLoad]
    public static class SkillTooltipVerification
    {
        private const string Pending = "Nytherion.SkillTooltip.Verify";
        private static double readyAt;

        static SkillTooltipVerification()
        {
            EditorApplication.update += Update;
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredPlayMode)
                    readyAt = EditorApplication.timeSinceStartup + 5d;
            };
        }

        private static void Update()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (File.Exists(SkillTooltipSetup.Output + "/setup.request")) return;
            string request = SkillTooltipSetup.Output + "/verify.request";
            if (File.Exists(request))
            {
                File.Delete(request);
                SessionState.SetBool(Pending, true);
                SessionState.SetBool(Pending + ".Exit", !EditorApplication.isPlaying);
                readyAt = EditorApplication.timeSinceStartup + 5d;
                if (!EditorApplication.isPlaying) EditorApplication.isPlaying = true;
            }
            if (!SessionState.GetBool(Pending, false) || !EditorApplication.isPlaying ||
                EditorApplication.timeSinceStartup < readyAt) return;
            SessionState.SetBool(Pending, false);
            var results = new List<string>();
            GameObject slotObject = null;
            SkillUIController controller = null;
            bool wasOpen = false;
            try
            {
                controller = Object.FindObjectOfType<SkillUIController>();
                Require(controller != null, "현재 씬의 SkillUIController 생성", results);
                SkillTooltip tooltip = Field<SkillTooltip>(controller, "skillTooltip");
                Require(tooltip != null, "DI 컨트롤러에서 스킬 전용 툴팁 생성", results);
                CanvasGroup group = tooltip.GetComponent<CanvasGroup>();
                Require(!group.blocksRaycasts && !group.interactable, "툴팁이 슬롯 입력을 가로막지 않음", results);
                SkillData skill = AssetDatabase.LoadAssetAtPath<SkillData>(
                    "Assets/Nytherion/Data/ScriptableObjects/Skill/PyroTank_Skill.asset");
                wasOpen = controller.IsOpen;
                controller.Open(false);
                slotObject = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Prefabs/UI/Skills/SkillStorageSlot.prefab"), tooltip.transform.parent);
                var slot = slotObject.GetComponent<SkillSlotUI>();
                slot.Setup(skill, null, tooltip);
                var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
                slot.OnPointerEnter(pointer);
                Require(group.alpha == 1f && Field<TextMeshProUGUI>(tooltip, "nameText").text == skill.DisplayName,
                    "슬롯 호버 시 이름 및 툴팁 표시", results);
                Require(Field<TextMeshProUGUI>(tooltip, "descriptionText").text == skill.Description,
                    "스킬 설명 표시", results);
                Require(Field<TextMeshProUGUI>(tooltip, "statsText").text.Split('\n').Length == 2,
                    "능력치는 피해·쿨타임만 표시", results);
                Require(!Field<TextMeshProUGUI>(tooltip, "progressText").text.Contains("No translation"),
                    "레벨·경험치 번역 키 연결", results);
                Require(!Field<TextMeshProUGUI>(tooltip, "progressText").text.Contains("{0}"),
                    "경험치 포맷 인수 적용", results);
                slot.OnPointerExit(pointer);
                Require(group.alpha == 0f, "포인터 이탈 시 숨김", results);
                slot.OnPointerEnter(pointer);
                slot.OnBeginDrag(pointer);
                Require(group.alpha == 0f, "드래그 시작 시 숨김", results);
                slot.OnEndDrag(pointer);
                slot.OnPointerEnter(pointer);
                controller.Close();
                Require(group.alpha == 0f, "스킬창 닫기 시 숨김", results);
                slot.Setup(null, null, tooltip);
                slot.OnPointerEnter(pointer);
                Require(group.alpha == 0f, "빈 슬롯에서는 표시하지 않음", results);
                CapturePreview(skill, results);
                File.WriteAllLines(SkillTooltipSetup.Output + "/verification.txt", results);
            }
            catch (Exception error)
            {
                results.Add("FAIL " + error);
                File.WriteAllLines(SkillTooltipSetup.Output + "/verification.txt", results);
                Debug.LogException(error);
            }
            finally
            {
                if (slotObject != null) Object.DestroyImmediate(slotObject);
                if (controller != null && wasOpen) controller.Open(false);
                if (SessionState.GetBool(Pending + ".Exit", false)) EditorApplication.isPlaying = false;
            }
        }

        public static void RefreshPreview()
        {
            CapturePreview(AssetDatabase.LoadAssetAtPath<SkillData>(
                "Assets/Nytherion/Data/ScriptableObjects/Skill/PyroTank_Skill.asset"), new List<string>());
        }

        private static void CapturePreview(SkillData skill, List<string> results)
        {
            var root = new GameObject("[SkillTooltipPreview]", typeof(RectTransform), typeof(Canvas));
            var cameraObject = new GameObject("[SkillTooltipPreviewCamera]", typeof(Camera));
            RenderTexture target = null;
            Texture2D image = null;
            RenderTexture previous = RenderTexture.active;
            try
            {
                Canvas canvas = root.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                ((RectTransform)root.transform).sizeDelta = new Vector2(500f, 1000f);
                root.transform.position = new Vector3(10000f, 0f, 0f);
                SkillTooltip tooltip = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                    SkillTooltipSetup.PrefabPath), root.transform).GetComponent<SkillTooltip>();
                tooltip.Show(null, skill, 3, 2, 4);
                RectTransform rect = (RectTransform)tooltip.transform;
                rect.localScale = Vector3.one;
                rect.localPosition = new Vector3(-200f, rect.rect.height / 2f, 0f);
                foreach (Transform child in root.GetComponentsInChildren<Transform>()) child.gameObject.layer = 31;
                Camera camera = cameraObject.GetComponent<Camera>();
                camera.enabled = false;
                camera.orthographic = true;
                int height = Mathf.CeilToInt(rect.rect.height + 64f);
                camera.orthographicSize = height / 2f;
                camera.transform.position = new Vector3(10000f, 0f, -10f);
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color32(15, 17, 28, 255);
                camera.cullingMask = 1 << 31;
                canvas.worldCamera = camera;
                target = new RenderTexture(464, height, 24);
                camera.targetTexture = target;
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = target;
                image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0f, 0f, target.width, target.height), 0, 0);
                image.Apply();
                File.WriteAllBytes(SkillTooltipSetup.Output + "/preview.png", image.EncodeToPNG());
                Require(rect.rect.height >= LayoutUtility.GetPreferredHeight(rect) - 1f,
                    "긴 설명에 맞춰 툴팁 높이 확장 및 미리보기 저장", results);
            }
            finally
            {
                RenderTexture.active = previous;
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(root);
                if (image != null) Object.DestroyImmediate(image);
                if (target != null) { target.Release(); Object.DestroyImmediate(target); }
            }
        }

        private static T Field<T>(Object target, string name) =>
            (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);

        private static void Require(bool condition, string message, List<string> results)
        {
            if (!condition) throw new InvalidOperationException(message);
            results.Add("PASS " + message);
        }
    }
}
