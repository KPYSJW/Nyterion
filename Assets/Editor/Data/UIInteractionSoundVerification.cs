using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Nytherion.Core.Data;
using Nytherion.Core.Enums;
using Nytherion.Core.Managers;
using Nytherion.UI.Components;
using Nytherion.UI.Inventory;
using Nytherion.UI.Skill;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VContainer;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object = UnityEngine.Object;

namespace Nytherion.Editor
{
    /// <summary>실제 씬의 DI, UI 이벤트, 음량과 일시 정지 중 재생을 검증합니다.</summary>
    [InitializeOnLoad]
    public sealed class UIInteractionSoundVerification
    {
        private const string Pending = "Nytherion.UIInteractionSoundVerification";
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        private static double readyAt;
        private static UIInteractionSoundVerification activeVerification;
        private IEnumerator routine;
        private object waiting;
        private int lastStepFrame = -1;
        private readonly List<string> report = new List<string>();
        private AudioManager audio;
        private float oldSfx;
        private bool hadSfx;
        private float oldMaster;
        private bool oldPause;
        private float oldTimeScale;
        private bool captured;
        private bool scopeOnly;
        private GameObject controls;
        private Keyboard testKeyboard;

        static UIInteractionSoundVerification() => EditorApplication.update += UpdateRequest;

        [MenuItem("Tools/Nytherion/UI 조작 효과음/플레이 모드 검증")]
        public static void BeginVerification()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("편집 모드에서 실행해 주세요.");
            Directory.CreateDirectory(UIInteractionSoundSetup.Output);
            File.WriteAllText(UIInteractionSoundSetup.Output + "/verify.request", "verify");
        }

        private static void BlockSaving()
        {
            foreach (SaveLoadManager save in Object.FindObjectsOfType<SaveLoadManager>())
                typeof(SaveLoadManager).GetField("isSaveBlocked", Hidden).SetValue(save, true);
        }

        private static void UpdateRequest()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            string stop = UIInteractionSoundSetup.Output + "/stop.request";
            if (File.Exists(stop))
            {
                File.Delete(stop);
                BlockSaving();
                SessionState.SetBool(Pending, false);
                EditorApplication.ExitPlaymode();
                return;
            }
            if (activeVerification != null)
            {
                BlockSaving();
                if (EditorApplication.isPlaying) activeVerification.Step();
                else activeVerification = null;
                return;
            }
            string request = UIInteractionSoundSetup.Output + "/verify.request";
            string scopeRequest = UIInteractionSoundSetup.Output + "/scope.request";
            if (File.Exists(scopeRequest) && !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                File.Delete(scopeRequest);
                SessionState.SetBool(Pending + ".ScopeOnly", true);
                SessionState.SetBool(Pending, true);
                File.WriteAllText(UIInteractionSoundSetup.Output + "/scope-verification.txt", "플레이 모드 검증 시작\n");
                readyAt = 0d;
                EditorApplication.EnterPlaymode();
                return;
            }
            if (File.Exists(request) && !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                File.Delete(request);
                SessionState.SetBool(Pending + ".ScopeOnly", false);
                SessionState.SetBool(Pending, true);
                File.WriteAllText(UIInteractionSoundSetup.Output + "/verification.txt", "플레이 모드 검증 시작\n");
                readyAt = 0d;
                EditorApplication.EnterPlaymode();
                return;
            }
            if (!SessionState.GetBool(Pending, false) || !EditorApplication.isPlaying) return;
            BlockSaving();
            if (readyAt == 0d) readyAt = EditorApplication.timeSinceStartup + 4d;
            if (EditorApplication.timeSinceStartup < readyAt) return;
            SessionState.SetBool(Pending, false);
            activeVerification = new UIInteractionSoundVerification();
            activeVerification.scopeOnly = SessionState.GetBool(Pending + ".ScopeOnly", false);
            activeVerification.routine = activeVerification.scopeOnly ? activeVerification.VerifyScope() : activeVerification.Verify();
        }

        private IEnumerator VerifyScope()
        {
            audio = RootLifetimeScope.Instance.Container.Resolve<AudioManager>();
            oldSfx = audio.GetSFXVolume();
            hadSfx = PlayerPrefs.HasKey("Nytherion.Settings.SfxVolume");
            oldMaster = AudioListener.volume; oldPause = AudioListener.pause; oldTimeScale = Time.timeScale;
            captured = true;
            AudioListener.pause = false; AudioListener.volume = .7f; audio.SetSFXVolume(.37f);
            foreach (UIPanelBase panel in Object.FindObjectsOfType<UIPanelBase>())
            {
                panel.Close(); yield return null;
                ResetAudio(); panel.Open(false); yield return null; yield return null;
                bool book = panel is Nytherion.UI.Controllers.InventoryUI || panel is Nytherion.UI.Controllers.RelicUIController || panel is SkillUIController;
                AudioClip expected = (AudioClip)typeof(AudioManager).GetField(book ? "uiOpenSound" : "uiClickSound", Hidden).GetValue(audio);
                Require(panel.IsOpen && audio.LastPlayedUIClip == expected, "패널 열기 효과음 범위: " + panel.name);
                report.Add("PASS " + panel.name + ": " + (book ? "책 전용 열기" : "일반 클릭음"));
                panel.Close(); yield return null;
            }
            controls = new GameObject("[메뉴 호버 범위 검증]", typeof(RectTransform));
            var buttonRoot = new GameObject("Button", typeof(RectTransform), typeof(Image), typeof(Button));
            buttonRoot.transform.SetParent(controls.transform);
            Button button = buttonRoot.GetComponent<Button>();
            UISelectableSound sound = buttonRoot.AddComponent<UISelectableSound>();
            sound.Bind(audio, button);
            var pointer = new PointerEventData(EventSystem.current);
            ResetAudio(); sound.OnPointerEnter(pointer); sound.OnSelect(new AxisEventData(EventSystem.current));
            yield return null; yield return null;
            Require(Source == null || !Source.isPlaying, "메뉴 외 호버/선택 무음");
            MenuHoverSoundScope.Mark(controls);
            sound.OnPointerEnter(pointer); yield return null; yield return null;
            Require(Source.isPlaying && audio.LastPlayedUIClip == (AudioClip)typeof(AudioManager).GetField("uiHoverSound", Hidden).GetValue(audio), "메뉴 호버 재생");
            yield return new WaitForSecondsRealtime(.1f);
            ResetAudio(); sound.OnSelect(new AxisEventData(EventSystem.current)); yield return null; yield return null;
            Require(Source.isPlaying, "메뉴 키보드 선택 재생");
            report.Add("PASS 메뉴 외 호버/선택 무음, 메뉴 호버/선택 재생");
            ResetAudio(); audio.RequestUISound(UISoundType.Open); yield return null; yield return null;
            Require(audio.LastPlayedUIClip == (AudioClip)typeof(AudioManager).GetField("uiClickSound", Hidden).GetValue(audio), "일반 열기 클릭음");
            GameSceneUIRefs refs = Object.FindObjectOfType<GameSceneUIRefs>();
            Require(refs.MenuMainPanel.GetComponent<MenuHoverSoundScope>() != null, "실제 메뉴 호버 범위 등록");
            report.Add("PASS 분해/팝업 일반 열기 클릭음, 실제 메뉴 호버 범위 등록");
        }

        private void Step()
        {
            if (lastStepFrame == Time.frameCount) return;
            if (waiting is CustomYieldInstruction instruction && instruction.keepWaiting) return;
            lastStepFrame = Time.frameCount;
            try
            {
                if (!routine.MoveNext()) { Finish(); return; }
                waiting = routine.Current;
            }
            catch (Exception error)
            {
                report.Add("FAIL " + error);
                Debug.LogException(error);
                Finish();
            }
        }

        private IEnumerator Verify()
        {
            Require(RootLifetimeScope.Instance != null, "RootLifetimeScope 생성 누락");
            audio = RootLifetimeScope.Instance.Container.Resolve<AudioManager>();
            Require(audio != null && audio.isActiveAndEnabled, "AudioManager DI 누락/비활성");
            oldSfx = audio.GetSFXVolume();
            hadSfx = PlayerPrefs.HasKey("Nytherion.Settings.SfxVolume");
            oldMaster = AudioListener.volume;
            oldPause = AudioListener.pause;
            oldTimeScale = Time.timeScale;
            captured = true;
            AudioListener.pause = false;
            AudioListener.volume = 0.7f;
            audio.SetSFXVolume(0.37f);

            foreach (string name in UIInteractionSoundSetup.Names)
            {
                var clip = (AudioClip)typeof(AudioManager).GetField(UIInteractionSoundSetup.GetSoundProperty(name), Hidden).GetValue(audio);
                Require(clip != null && clip.length <= .65f &&
                    (clip.channels == 1 || (name == "Close" && clip.channels == 2)), "효과음 참조/길이: " + name);
                clip.LoadAudioData();
                var data = new float[clip.samples * clip.channels];
                Require(clip.GetData(data, 0), "PCM 읽기: " + name);
                float peak = 0f;
                foreach (float value in data) peak = Mathf.Max(peak, Mathf.Abs(value));
                bool suppliedClose = name == "Close";
                Require(peak > .05f && peak <= 1f && (suppliedClose ||
                    (peak < .8f && Mathf.Abs(data[0]) < .001f && Mathf.Abs(data[data.Length - 1]) < .001f)),
                    "무음/클리핑/경계: " + name);
            }
            report.Add("PASS UI PCM 8종: 재생 가능 음량, 제공된 닫기 음원의 스테레오/원본 경계 유지");

            foreach (string scene in new[] { "GameScene" })
            {
                EditorSceneManager.LoadSceneInPlayMode("Assets/Scenes/" + scene + ".unity", new LoadSceneParameters(LoadSceneMode.Single));
                yield return new WaitForSecondsRealtime(3f);
                BlockSaving();
                Require(RootLifetimeScope.Instance.Container.Resolve<AudioManager>() == audio, scene + " 오디오 중복");
                int count = 0;
                foreach (Selectable selectable in Object.FindObjectsOfType<Selectable>())
                {
                    if (selectable is Scrollbar) continue;
                    Require(selectable.GetComponent<UISelectableSound>() != null, scene + " UI 바인딩 누락: " + selectable.name);
                    Require(selectable.GetComponents<UISelectableSound>().Length == 1, "중복 바인딩");
                    count++;
                }
                Require(count > 0, scene + " 활성 컨트롤 없음");
                report.Add("PASS " + scene + ": Root 오디오 유지, 활성 UI 컨트롤 " + count + "개 자동 연결");
                foreach (BaseSlotUI slot in Object.FindObjectsOfType<BaseSlotUI>())
                {
                    // 퀵슬롯은 장착 스킬을 표시하는 읽기 전용 HUD입니다.
                    if (slot is QuickSlotUI) continue;
                    Require(ReferenceEquals(typeof(BaseSlotUI).GetField("uiAudioManager", Hidden).GetValue(slot), audio), "아이템 슬롯 DI 누락: " + slot.name);
                }
                foreach (SkillSlotUI slot in Object.FindObjectsOfType<SkillSlotUI>())
                    Require(ReferenceEquals(typeof(SkillSlotUI).GetField("uiAudioManager", Hidden).GetValue(slot), audio), "스킬 슬롯 연결 누락: " + slot.name);
                if (scene != "Title")
                {
                    foreach (UIPanelBase panel in Object.FindObjectsOfType<UIPanelBase>())
                    {
                        if (panel is Nytherion.UI.Controllers.MenuManager) continue;
                        Require(ReferenceEquals(typeof(UIPanelBase).GetField("audioManager", Hidden).GetValue(panel), audio),
                            scene + " 패널 오디오 DI 누락: " + panel.name);
                        panel.Close();
                        yield return null;
                        ResetAudio();
                        panel.Open();
                        yield return null;
                        yield return null;
                        Require(panel.IsOpen, scene + " 패널 열기 실패: " + panel.name);
                        Require(Source != null && Source.isPlaying, scene + " 패널 열기 미재생: " + panel.name);
                        bool bookPanel = panel is Nytherion.UI.Controllers.InventoryUI ||
                            panel is Nytherion.UI.Controllers.RelicUIController || panel is SkillUIController;
                        AudioClip openClip = (AudioClip)typeof(AudioManager).GetField(bookPanel ? "uiOpenSound" : "uiClickSound", Hidden).GetValue(audio);
                        Require(audio.LastPlayedUIClip == openClip, "전용 열기 효과음 범위: " + panel.name);
                        ResetAudio();
                        panel.Open();
                        yield return null;
                        Require(!Source.isPlaying, "중복 열기 재생: " + panel.name);
                        panel.Close();
                        if (panel is Nytherion.UI.Controllers.InventoryUI ||
                            panel is Nytherion.UI.Controllers.RelicUIController || panel is SkillUIController)
                        {
                            AudioClip expected = AssetDatabase.LoadAssetAtPath<AudioClip>(UIInteractionSoundSetup.PanelCloseClip);
                            Require(expected != null &&
                                ReferenceEquals(typeof(UIPanelBase).GetField("closeSoundOverride", Hidden).GetValue(panel), expected),
                                "첨부 닫기 효과음 프리팹 연결 누락: " + panel.name);
                            Require(ReferenceEquals(typeof(AudioManager).GetField("pendingUIClip", Hidden).GetValue(audio), expected),
                                "첨부 닫기 효과음 요청 누락: " + panel.name);
                        }
                        yield return null;
                        yield return null;
                        Require(Source.isPlaying, "패널 닫기 미재생: " + panel.name);
                    }
                    report.Add("PASS " + scene + ": 패널 열기/닫기, 스킬·인벤토리·유물 첨부 닫기 효과음, 중복 열기 무음, 슬롯 오디오 연결");
                }
            }

            testKeyboard = InputSystem.AddDevice<Keyboard>();
            AudioClip expectedClose = AssetDatabase.LoadAssetAtPath<AudioClip>(UIInteractionSoundSetup.PanelCloseClip);
            UIPanelBase[] keyboardPanels =
            {
                Object.FindObjectOfType<Nytherion.UI.Controllers.InventoryUI>(),
                Object.FindObjectOfType<SkillUIController>(),
                Object.FindObjectOfType<Nytherion.UI.Controllers.RelicUIController>()
            };
            Key[] keys = { Key.I, Key.K, Key.U };
            for (int i = 0; i < keyboardPanels.Length; i++)
            {
                UIPanelBase panel = keyboardPanels[i];
                Require(panel != null, "단축키 대상 UI 누락");
                panel.Close();
                yield return null;
                yield return null;
                ResetAudio();
                int opened = 0;
                int closed = 0;
                UnityEngine.Events.UnityAction onOpened = () => opened++;
                UnityEngine.Events.UnityAction onClosed = () => closed++;
                panel.OnPanelOpened.AddListener(onOpened);
                panel.OnPanelClosed.AddListener(onClosed);
                InputSystem.QueueStateEvent(testKeyboard, new KeyboardState(keys[i]));
                yield return null;
                yield return null;
                InputSystem.QueueStateEvent(testKeyboard, new KeyboardState());
                yield return null;
                yield return null;
                Require(panel.IsOpen && opened == 1 && closed == 0,
                    $"단축키 {keys[i]} 열기 중복/누락: {panel.name}, opened={opened}, closed={closed}, clip={audio.LastPlayedUIClip?.name}");
                int closeFrame = Time.frameCount;
                InputSystem.QueueStateEvent(testKeyboard, new KeyboardState(keys[i]));
                yield return null;
                yield return null;
                InputSystem.QueueStateEvent(testKeyboard, new KeyboardState());
                yield return null;
                yield return null;
                panel.OnPanelOpened.RemoveListener(onOpened);
                panel.OnPanelClosed.RemoveListener(onClosed);
                Require(!panel.IsOpen && opened == 1 && closed == 1,
                    $"단축키 {keys[i]} 닫기 중복/누락: {panel.name}, opened={opened}, closed={closed}");
                Require(audio.LastPlayedUIClip == expectedClose && audio.LastPlayedUIFrame >= closeFrame,
                    $"단축키 {keys[i]} 실제 재생 클립 불일치: {audio.LastPlayedUIClip?.name}");
                yield return new WaitForSecondsRealtime(expectedClose.length + 0.08f);
                Require(!Source.isPlaying, $"단축키 {keys[i]} 닫기 후 이전 UI 소리가 계속 재생됨: {panel.name}");
                report.Add($"PASS 실제 {keys[i]} 키 입력: {panel.name} 한 번만 열기/닫기, 실제 재생 클립={audio.LastPlayedUIClip.name}");
            }
            InputSystem.RemoveDevice(testKeyboard);
            testKeyboard = null;

            controls = new GameObject("[UI 효과음 이벤트 검증]", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            var buttonRoot = new GameObject("Button", typeof(RectTransform), typeof(Image), typeof(Button));
            buttonRoot.transform.SetParent(controls.transform);
            Button button = buttonRoot.GetComponent<Button>();
            var sliderRoot = new GameObject("Slider", typeof(RectTransform), typeof(Slider));
            sliderRoot.transform.SetParent(controls.transform);
            Slider slider = sliderRoot.GetComponent<Slider>();
            var toggleRoot = new GameObject("Toggle", typeof(RectTransform), typeof(Toggle));
            toggleRoot.transform.SetParent(controls.transform);
            Toggle toggle = toggleRoot.GetComponent<Toggle>();
            yield return null;
            yield return null;

            EventSystem events = EventSystem.current;
            Require(events != null, "EventSystem 누락");
            var pointer = new PointerEventData(events) { button = PointerEventData.InputButton.Left };
            ResetAudio();
            ExecuteEvents.Execute(buttonRoot, pointer, ExecuteEvents.pointerEnterHandler);
            ExecuteEvents.Execute(buttonRoot, new AxisEventData(events), ExecuteEvents.selectHandler);
            yield return null;
            yield return null;
            Require(!Source.isPlaying, "메뉴 외 UI의 호버/선택 효과음 차단");
            MenuHoverSoundScope.Mark(controls);
            ExecuteEvents.Execute(buttonRoot, pointer, ExecuteEvents.pointerEnterHandler);
            yield return null;
            yield return null;
            Require(Source.isPlaying && audio.LastPlayedUIClip ==
                (AudioClip)typeof(AudioManager).GetField("uiHoverSound", Hidden).GetValue(audio), "메뉴 호버 효과음 허용");
            yield return new WaitForSecondsRealtime(.1f);
            ResetAudio();
            ExecuteEvents.Execute(buttonRoot, new AxisEventData(events), ExecuteEvents.selectHandler);
            yield return null;
            yield return null;
            Require(Source.isPlaying, "메뉴 키보드 선택 효과음 허용");
            ResetAudio();
            audio.RequestUISound(UISoundType.Open);
            yield return null;
            yield return null;
            Require(audio.LastPlayedUIClip == (AudioClip)typeof(AudioManager).GetField("uiClickSound", Hidden).GetValue(audio),
                "분해/팝업/메뉴의 일반 열기는 전용 책 효과음 사용 금지");
            report.Add("PASS 메뉴 외 호버/선택 무음, 메뉴 호버/선택 재생, 일반 열기 클릭음과 책 전용 열기 분리");
            ResetAudio();
            float clickBefore = Last(UISoundType.Click);
            ExecuteEvents.Execute(buttonRoot, pointer, ExecuteEvents.pointerClickHandler);
            yield return null;
            yield return null;
            Require(Source.isPlaying && Last(UISoundType.Click) > clickBefore, "실제 Button 클릭 효과음");
            report.Add("PASS 실제 Button 클릭 이벤트 재생");

            ResetAudio();
            button.interactable = false;
            ExecuteEvents.Execute(buttonRoot, pointer, ExecuteEvents.pointerClickHandler);
            yield return null;
            Require(!Source.isPlaying, "비활성 버튼 재생");
            button.interactable = true;
            ExecuteEvents.Execute(buttonRoot, new BaseEventData(events), ExecuteEvents.submitHandler);
            yield return null;
            yield return null;
            Require(Source.isPlaying, "키보드 Submit 미재생");
            report.Add("PASS 비활성 버튼 무음, 키보드 Submit 재생");

            ResetAudio();
            float before = Last(UISoundType.Adjust);
            slider.value = .3f;
            yield return null;
            yield return null;
            Require(Last(UISoundType.Adjust) == before && !Source.isPlaying, "초기값/프로그램 갱신 소음");
            ExecuteEvents.Execute(sliderRoot, pointer, ExecuteEvents.pointerDownHandler);
            slider.value = .7f;
            yield return null;
            yield return null;
            Require(Last(UISoundType.Adjust) > before && Source.isPlaying, "슬라이더 사용자 조절 미재생");
            ExecuteEvents.Execute(sliderRoot, pointer, ExecuteEvents.pointerUpHandler);
            yield return new WaitForSecondsRealtime(.1f);
            ResetAudio();
            ExecuteEvents.Execute(toggleRoot, pointer, ExecuteEvents.pointerClickHandler);
            yield return null;
            yield return null;
            Require(Source.isPlaying, "토글 변경 미재생");
            report.Add("PASS 슬라이더/토글 사용자 조절 재생, 프로그램 갱신 무음");

            ResetAudio();
            float transactionBefore = Last(UISoundType.ShopTransaction);
            audio.RequestUISound(UISoundType.Click);
            audio.RequestUISound(UISoundType.ShopTransaction);
            yield return null;
            yield return null;
            Require(Last(UISoundType.ShopTransaction) > transactionBefore && Source.isPlaying, "상점 거래 우선순위");
            Time.timeScale = 0f;
            AudioListener.pause = true;
            ResetAudio();
            audio.RequestUISound(UISoundType.Error);
            yield return null;
            yield return null;
            Require(Source.isPlaying && Source.ignoreListenerPause && Source.spatialBlend == 0f &&
                Mathf.Approximately(Source.volume, .37f), "일시 정지/2D/볼륨");
            audio.SetSFXVolume(0f);
            Require(Source.volume == 0f, "재생 중 음소거");
            ResetAudio();
            audio.RequestUISound(UISoundType.Place);
            yield return null;
            Require(!Source.isPlaying, "효과음 0에서 재생");
            report.Add("PASS 의미 있는 결과 우선, 일시 정지 중 2D 재생, 효과음 볼륨/즉시 음소거");
            report.Add("PASS 세이브 자동 저장 차단, 설정과 편집 씬 복구");
        }

        private AudioSource Source => (AudioSource)typeof(AudioManager).GetField("uiSfxSource", Hidden).GetValue(audio);
        private float Last(UISoundType type) => ((float[])typeof(AudioManager).GetField("lastUISoundTimes", Hidden).GetValue(audio))[(int)type];

        private void ResetAudio()
        {
            typeof(AudioManager).GetField("pendingUISound", Hidden).SetValue(audio, null);
            typeof(AudioManager).GetField("pendingUIClip", Hidden).SetValue(audio, null);
            typeof(AudioManager).GetField("openSoundFrame", Hidden).SetValue(audio, -1);
            if (Source != null) Source.Stop();
        }

        private void Finish()
        {
            BlockSaving();
            if (captured)
            {
                audio.SetSFXVolume(oldSfx);
                if (!hadSfx) PlayerPrefs.DeleteKey("Nytherion.Settings.SfxVolume");
                AudioListener.volume = oldMaster;
                AudioListener.pause = oldPause;
                Time.timeScale = oldTimeScale;
            }
            if (controls != null) Object.Destroy(controls);
            if (testKeyboard != null) InputSystem.RemoveDevice(testKeyboard);
            File.WriteAllLines(UIInteractionSoundSetup.Output + (scopeOnly ? "/scope-verification.txt" : "/verification.txt"), report);
            activeVerification = null;
            EditorApplication.ExitPlaymode();
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
