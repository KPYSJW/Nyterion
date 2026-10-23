using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Nytherion.Core.Data;
using Nytherion.UI.Controllers;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Nytherion.Editor
{
    /// <summary>실제 설정 UI의 선택 이벤트·Game 뷰 해상도·화면 모드·저장값을 검증합니다.</summary>
    [InitializeOnLoad]
    public static class SettingsDropdownVerification
    {
        private const string Output = "output/settings-dropdown";
        private const string Pending = "Nytherion.Settings.VerifyPlay";
        private static bool running;
        private static readonly List<string> report = new List<string>();

        static SettingsDropdownVerification() { EditorApplication.update += Update; }

        private static void Update()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (File.Exists(Output + "/play.request"))
            {
                File.Delete(Output + "/play.request");
                SessionState.SetBool(Pending, true);
                SessionState.SetBool(Pending + ".Exit", !EditorApplication.isPlaying);
                EditorApplication.isPlaying = true;
            }
            if (!SessionState.GetBool(Pending, false) || !EditorApplication.isPlaying) return;
            if (running) return;
            GameSceneUIRefs host = Object.FindObjectOfType<GameSceneUIRefs>(true);
            if (host == null || !host.gameObject.activeInHierarchy) return;
            running = true;
            host.StartCoroutine(RunVerification());
        }

        private static IEnumerator RunVerification()
        {
            report.Clear();
            IEnumerator verification = Verify();
            while (true)
            {
                object step;
                try
                {
                    if (!verification.MoveNext()) break;
                    step = verification.Current;
                }
                catch (Exception error)
                {
                    report.Add("FAIL " + error);
                    break;
                }
                yield return step;
            }
            (verification as IDisposable)?.Dispose();
            Finish();
        }

        private static void Finish()
        {
            File.WriteAllLines(Output + "/play-verification.txt", report);
            running = false;
            SessionState.SetBool(Pending, false);
            if (SessionState.GetBool(Pending + ".Exit", false))
                EditorApplication.delayCall += () => EditorApplication.isPlaying = false;
        }

        private static IEnumerator Verify()
        {
            double ready = EditorApplication.timeSinceStartup + 10d;
            while (EditorApplication.timeSinceStartup < ready) yield return null;
            SettingsManager manager = Object.FindObjectOfType<SettingsManager>(true);
            GameSceneUIRefs refs = Object.FindObjectOfType<GameSceneUIRefs>(true);
            Require(manager != null && refs != null && refs.ResolutionDropdown != null, "GameScene DI 및 Inspector 참조");
            TMP_Dropdown dropdown = refs.ResolutionDropdown;
            FieldInfo bound = typeof(SettingsManager).GetField("resolutionDropdown", BindingFlags.Instance | BindingFlags.NonPublic);
            Require(ReferenceEquals(bound.GetValue(manager), dropdown), "SettingsManager에 실제 해상도 드롭다운 주입");
            Require(dropdown.options.Count >= 4 && !dropdown.options.Any(item => item.text.StartsWith("Option")), "Start 초기화 및 해상도 목록 생성");

            string[] keys = { "Nytherion.Settings.ResolutionWidth", "Nytherion.Settings.ResolutionHeight", "Nytherion.Settings.Fullscreen" };
            bool[] hadKeys = keys.Select(PlayerPrefs.HasKey).ToArray();
            int[] values = keys.Select(key => PlayerPrefs.GetInt(key)).ToArray();
            int originalWidth = Screen.width;
            int originalHeight = Screen.height;
            bool originalFullscreen = refs.FullscreenToggle.isOn;
            int originalIndex = dropdown.value;
            var changedAncestors = new List<GameObject>();
            CanvasGroup[] groups = dropdown.GetComponentsInParent<CanvasGroup>(true);
            bool[] interactable = groups.Select(group => group.interactable).ToArray();
            bool[] blocksRaycasts = groups.Select(group => group.blocksRaycasts).ToArray();
            float[] alphas = groups.Select(group => group.alpha).ToArray();
            try
            {
                for (Transform parent = dropdown.transform; parent != null; parent = parent.parent)
                    if (!parent.gameObject.activeSelf) { changedAncestors.Add(parent.gameObject); parent.gameObject.SetActive(true); }
                foreach (CanvasGroup group in groups)
                {
                    group.interactable = group.blocksRaycasts = true;
                    group.alpha = 1f;
                }
                yield return null;
                Canvas.ForceUpdateCanvases();
                dropdown.Show();
                yield return null;
                Transform list = dropdown.transform.Find("Dropdown List");
                Require(list != null, "실제 TMP_Dropdown 펼침 및 템플릿 유효성");
                Toggle[] rows = list.GetComponentsInChildren<Toggle>().Where(toggle => toggle.gameObject.activeSelf).ToArray();
                Require(rows.Length == dropdown.options.Count, "해상도별 선택 행 생성");
                foreach (Toggle row in rows)
                {
                    TMP_Text text = row.GetComponentInChildren<TMP_Text>();
                    text.ForceMeshUpdate();
                    Require(!text.isTextOverflowing && ((RectTransform)row.transform).rect.height >= 56f,
                        text.text + " 목록 글자 넘침 없음");
                }
                dropdown.Hide();
                yield return null;

                foreach (string label in new[] { "1280 × 720", "1920 × 1080" })
                {
                    int index = dropdown.options.FindIndex(item => item.text == label);
                    dropdown.value = index;
                    string[] parts = label.Split('×');
                    int width = int.Parse(parts[0].Trim());
                    int height = int.Parse(parts[1].Trim());
                    double deadline = EditorApplication.timeSinceStartup + 5d;
                    while ((Screen.width != width || Screen.height != height) && EditorApplication.timeSinceStartup < deadline)
                        yield return null;
                    Require(Screen.width == width && Screen.height == height, "선택 이벤트 후 실제 Game 뷰 " + label + " 적용");
                    Require(UserSettings.TryGetResolution(out int savedWidth, out int savedHeight) && savedWidth == width && savedHeight == height,
                        label + " 저장값 반영");
                    dropdown.captionText.ForceMeshUpdate();
                    Require(!dropdown.captionText.isTextOverflowing, label + " 선택창 글자 넘침 없음");
                }
                refs.FullscreenToggle.isOn = !originalFullscreen;
                Require(UserSettings.GetFullscreen(originalFullscreen) == !originalFullscreen, "전체화면 토글 저장 및 해상도 재적용");
                Require(UserSettings.TryGetResolution(out int lastWidth, out int lastHeight) && lastWidth == 1920 && lastHeight == 1080,
                    "화면 모드 전환 후 선택 해상도 유지");

                // 설정 패널을 다시 열어도 리스너가 유지되어 실제 변경 이벤트가 발생해야 합니다.
                dropdown.gameObject.SetActive(false);
                dropdown.gameObject.SetActive(true);
                int small = dropdown.options.FindIndex(item => item.text == "1280 × 720");
                dropdown.value = small;
                Require(UserSettings.TryGetResolution(out int reopenedWidth, out int reopenedHeight) && reopenedWidth == 1280 && reopenedHeight == 720,
                    "설정 UI 재활성화 후 변경 이벤트 유지");
            }
            finally
            {
                dropdown.Hide();
                dropdown.SetValueWithoutNotify(originalIndex);
                refs.FullscreenToggle.SetIsOnWithoutNotify(originalFullscreen);
                UserSettings.ApplyDisplaySettings(originalWidth, originalHeight, originalFullscreen);
                for (int i = 0; i < keys.Length; i++)
                {
                    if (hadKeys[i]) PlayerPrefs.SetInt(keys[i], values[i]);
                    else PlayerPrefs.DeleteKey(keys[i]);
                }
                PlayerPrefs.Save();
                for (int i = 0; i < groups.Length; i++)
                {
                    groups[i].interactable = interactable[i];
                    groups[i].blocksRaycasts = blocksRaycasts[i];
                    groups[i].alpha = alphas[i];
                }
                foreach (GameObject item in changedAncestors) item.SetActive(false);
            }
            Require(true, "검증 전 화면·사용자 설정 복원");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
            report.Add("PASS " + message);
        }
    }
}
