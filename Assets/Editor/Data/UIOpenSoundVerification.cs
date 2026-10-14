using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Nytherion.Core.Managers;
using Nytherion.UI.Controllers;
using Nytherion.UI.Skill;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Nytherion.Editor
{
    /// <summary>실제 씬의 DI와 패널 열기 효과음, 중복 호출 및 볼륨 동작을 검증한다.</summary>
    [InitializeOnLoad]
    public static class UIOpenSoundVerification
    {
        private const string Pending = "Nytherion.UIOpenSound.Verify";
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        private static double readyAt;

        static UIOpenSoundVerification()
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
            string request = UIOpenSoundSetup.Output + "/verify.request";
            if (File.Exists(request) && !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                File.Delete(request);
                if (SceneManager.GetActiveScene().name != "GameScene" || SceneManager.GetActiveScene().isDirty)
                {
                    File.WriteAllText(UIOpenSoundSetup.Output + "/verification.txt", "FAIL 저장된 GameScene을 편집 모드에서 열어 주세요.");
                    return;
                }
                File.WriteAllText(UIOpenSoundSetup.Output + "/verification.txt", "플레이 모드 검증 시작\n");
                SessionState.SetBool(Pending, true);
                EditorApplication.isPlaying = true;
                return;
            }
            if (!SessionState.GetBool(Pending, false) || !EditorApplication.isPlaying) return;
            // UI 검증 중 게임 진행 데이터가 자동 저장되지 않도록 막는다.
            foreach (SaveLoadManager save in Object.FindObjectsOfType<SaveLoadManager>())
                typeof(SaveLoadManager).GetField("isSaveBlocked", Hidden).SetValue(save, true);
            if (EditorApplication.timeSinceStartup < readyAt) return;

            var report = new List<string>();
            try
            {
                AudioManager audio = Object.FindObjectOfType<AudioManager>();
                Require(audio != null, "오디오 매니저 DI 생성 누락");
                float previousVolume = audio.GetSFXVolume();
                bool hadVolume = PlayerPrefs.HasKey("Nytherion.Settings.SfxVolume");
                try
                {
                    audio.SetSFXVolume(0.37f);
                    VerifyPanel(Object.FindObjectOfType<InventoryUI>(), audio, report);
                    VerifyPanel(Object.FindObjectOfType<RelicUIController>(), audio, report);
                    if (SceneManager.GetActiveScene().name == "GameScene")
                        VerifyPanel(Object.FindObjectOfType<SkillUIController>(), audio, report);
                }
                finally
                {
                    audio.SetSFXVolume(previousVolume);
                    if (!hadVolume) PlayerPrefs.DeleteKey("Nytherion.Settings.SfxVolume");
                }
                File.AppendAllLines(UIOpenSoundSetup.Output + "/verification.txt", report);
                if (SceneManager.GetActiveScene().name == "GameScene")
                {
                    EditorSceneManager.LoadSceneInPlayMode("Assets/Scenes/Village.unity", new LoadSceneParameters(LoadSceneMode.Single));
                    readyAt = EditorApplication.timeSinceStartup + 5d;
                    return;
                }
                Finish();
            }
            catch (Exception error)
            {
                File.AppendAllText(UIOpenSoundSetup.Output + "/verification.txt", "FAIL " + error + "\n");
                Debug.LogException(error);
                Finish();
            }
        }

        private static void VerifyPanel(UIPanelBase panel, AudioManager audio, List<string> report)
        {
            Require(panel != null, "대상 패널 DI 생성 누락");
            panel.Close();
            panel.Toggle();
            Require(panel.IsOpen, panel.name + " 열기 실패");
            AudioSource source = (AudioSource)typeof(AudioManager).GetField("uiSfxSource", Hidden).GetValue(audio);
            Require(source != null && source.isPlaying, panel.name + " 열기 효과음 미재생");
            Require(source.spatialBlend == 0f && !source.loop && !source.playOnAwake &&
                Mathf.Approximately(source.volume, 0.37f), "2D 효과음/음량 설정 실패");
            source.Stop();
            panel.Open();
            Require(!source.isPlaying, "이미 열린 패널의 중복 효과음");
            panel.Toggle();
            Require(!panel.IsOpen && !source.isPlaying, "패널 닫기 시 효과음 재생");
            panel.Open(false);
            Require(panel.IsOpen && source.isPlaying, "비독점 열기 시 효과음 누락");
            audio.SetSFXVolume(0f);
            Require(source.volume == 0f, "재생 중 음소거 미반영");
            source.Stop();
            panel.Close();
            panel.Open();
            Require(panel.IsOpen && !source.isPlaying, "효과음 0일 때 사운드 재생");
            panel.Close();
            audio.SetSFXVolume(0.37f);
            report.Add($"PASS {SceneManager.GetActiveScene().name}/{panel.GetType().Name}: 열기 재생, 중복 열기·닫기 무음, Open(false), 2D 및 효과음 볼륨/음소거");
        }

        private static void Finish()
        {
            SessionState.SetBool(Pending, false);
            foreach (SaveLoadManager save in Object.FindObjectsOfType<SaveLoadManager>())
                typeof(SaveLoadManager).GetField("isSaveBlocked", Hidden).SetValue(save, true);
            EditorApplication.isPlaying = false;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
