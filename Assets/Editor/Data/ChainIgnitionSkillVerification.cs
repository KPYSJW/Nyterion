using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Nytherion.Core.Data;
using Nytherion.Core.Enums;
using Nytherion.Core.Interfaces;
using Nytherion.Core.Managers;
using Nytherion.Data.ScriptableObjects.Player;
using Nytherion.Data.ScriptableObjects.Skill;
using Nytherion.Data.ScriptableObjects.Enemy;
using Nytherion.GamePlay.Characters.Enemy;
using Nytherion.GamePlay.Skills;
using Nytherion.Gameplay.Relics.Modules;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using Nytherion.UI.Test;
using Object = UnityEngine.Object;

namespace Nytherion.Editor
{
    /// <summary>실제 스킬 프리팹과 Physics2D를 사용하여 파동, 중복 피해, 이동 및 재사용을 검증합니다.</summary>
    [InitializeOnLoad]
    public static class ChainIgnitionSkillVerification
    {
        private const string Output = "output/chain-ignition";
        private const string Pending = "ChainIgnitionVerification.Pending";
        private static readonly List<GameObject> objects = new List<GameObject>();
        private static readonly List<ScriptableObject> runtimeData = new List<ScriptableObject>();
        private static readonly List<string> checks = new List<string>();
        private static readonly Vector3 Center = new Vector3(1000f, 1000f, 0f);
        private static ChainIgnitionSkill skill;
        private static ChainIgnitionSkillData data;
        private static GameObject caster;
        private static ChainIgnitionWave firstWave;
        private static PlayerManager player;
        private static SkillDataManager skillStates;
        private static StatRelicEffect growthRelic;
        private static ChainIgnitionVerificationTarget[] growthTargets;
        private static ChainIgnitionVerificationTarget inactiveTarget, sizeTarget;
        private static AudioSource explosionAudio;
        private static bool checkedSoundTail;
        private static ChainIgnitionVerificationTarget[] ringTargets;
        private static ChainIgnitionVerificationTarget overlappingTarget, largeTarget, friendlyTarget, outsideTarget;
        private static ChainIgnitionVerificationTarget offsetTarget, unshiftedTarget;
        private static readonly List<ChainIgnitionVerificationTarget> crowdedTargets = new List<ChainIgnitionVerificationTarget>();
        private static float startTime, previousTimeScale;
        private static int stage;
        private static double readyAt;
        private static bool exitAfter;

        static ChainIgnitionSkillVerification()
        {
            EditorApplication.update += Update;
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Pending, false))
                    readyAt = EditorApplication.timeSinceStartup + 2d;
                if (state == PlayModeStateChange.ExitingPlayMode && skill != null) Cleanup();
            };
        }

        [MenuItem("Tools/Nytherion/Chain Ignition/Verify In Play Mode")]
        public static void Start()
        {
            if (SessionState.GetBool(Pending, false) || skill != null) return;
            if (!EditorApplication.isPlaying)
            {
                ChainIgnitionSkillData source = AssetDatabase.LoadAssetAtPath<ChainIgnitionSkillData>(ChainIgnitionSkillSetup.DataPath);
                if (source == null || source.wavePrefab == null || source.skillPrefab == null || !source.HasValidAnimation)
                    ChainIgnitionSkillSetup.CreateAssets();
            }
            exitAfter = !EditorApplication.isPlaying;
            SessionState.SetBool(Pending + ".ExitAfter", exitAfter);
            SessionState.SetBool(Pending, true);
            if (EditorApplication.isPlaying) readyAt = EditorApplication.timeSinceStartup + 0.1d;
            else EditorApplication.isPlaying = true;
        }

        private static void Update()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (File.Exists(Output + "/range-editor.verify.request") && !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                File.Delete(Output + "/range-editor.verify.request");
                VerifyRangeEditor();
            }
            // 컴파일로 취소된 플레이 진입 요청은 다음 검증을 막지 않도록 정리합니다.
            if (!EditorApplication.isPlayingOrWillChangePlaymode && skill == null && readyAt == 0d)
                SessionState.SetBool(Pending, false);
            if (File.Exists(Output + "/verify.request") && !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                File.Delete(Output + "/verify.request");
                Start();
            }
            try
            {
                if (SessionState.GetBool(Pending, false) && EditorApplication.isPlaying && readyAt > 0d &&
                    EditorApplication.timeSinceStartup >= readyAt)
                {
                    SessionState.SetBool(Pending, false);
                    exitAfter = SessionState.GetBool(Pending + ".ExitAfter", false);
                    Begin();
                }
                if (skill == null || EditorApplication.isPaused) return;
                float elapsed = Time.time - startTime;
                float sampleDelay = data.AnimationDuration * 0.4f;
                float visualEndTime = (ChainIgnitionSkillData.WaveCount - 1) * data.WaveInterval + data.AnimationDuration;
                float finishTime = (ChainIgnitionSkillData.WaveCount - 1) * data.WaveInterval +
                    Mathf.Max(data.AnimationDuration, data.explosionSound.length) + 0.1f;
                if (stage == 0 && elapsed >= sampleDelay)
                {
                    firstWave = Object.FindObjectsOfType<ChainIgnitionWave>().Single(wave => Vector3.Distance(wave.transform.position, Center) < 0.01f);
                    explosionAudio = firstWave.GetComponent<AudioSource>();
                    RequireSound(1, "1차 폭발 효과음 한 번 재생 및 SFX 설정 적용");
                    SpriteRenderer[] renderers = firstWave.GetComponentsInChildren<SpriteRenderer>();
                    Require(renderers.Count(renderer => renderer.enabled) == 1 && renderers[0].enabled,
                        "1레벨은 마우스에 가장 가까운 오른쪽 한 방향만 폭발 (활성: " +
                        string.Join(",", renderers.Where(renderer => renderer.enabled).Select(renderer => renderer.name)) +
                        ", 레벨: " + player.GetSkillLevel(data) + ", 추가 투사체: " + player.currentPlayerData.extraProjectiles + ")");
                    Require(Mathf.Abs(Vector3.Distance(GroundPosition(renderers[0]), Center) - 0.8f) < 0.01f &&
                        Mathf.Approximately(renderers[0].transform.localScale.x, 1.6f), "축소한 첫 원 반경 0.8과 연출 크기 1.6");
                    RequireHitRanges(1, 0.6f, "불꽃 아래의 바닥 중심에 실제 피해 반경 0.6 표시");
                    data.showHitRanges = false;
                    firstWave.SendMessage("Update");
                    RequireHitRanges(0, 0.6f, "실행 중 표시 옵션을 끄면 피해 범위 원 숨김");
                    data.showHitRanges = true;
                    firstWave.SendMessage("Update");
                    RequireHitRanges(1, 0.6f, "실행 중 표시 옵션을 다시 켜면 기존 원 재사용");
                    Require(ringTargets[0].Hits == 1 && ringTargets[1].Hits == 0 && ringTargets[2].Hits == 0,
                        "1차 폭발 중에는 2·3차 피해 없음");
                    Capture("wave-1.png");
                    caster.transform.position = Center + Vector3.right * 12f;
                    Physics2D.SyncTransforms();
                    stage = 1;
                }
                if (stage == 1 && elapsed >= data.WaveInterval + sampleDelay)
                {
                    RequireSound(2, "2차 시작 시 효과음 추가 재생, 프레임별 중복 재생 없음");
                    SpriteRenderer[] renderers = firstWave.GetComponentsInChildren<SpriteRenderer>();
                    Require(Vector3.Distance(firstWave.transform.position, Center) < 0.01f, "시전자 이동 후에도 시전 중심 고정");
                    Require(renderers[8].enabled && Mathf.Abs(Vector3.Distance(GroundPosition(renderers[8]), Center) - 1.6f) < 0.01f,
                        "두 번째 원 반경 1.6, 시전 때 선택한 방향 유지");
                    Require(renderers.Count(renderer => renderer.enabled) == 1 && renderers.Take(8).All(renderer => !renderer.enabled),
                        "1차 애니메이션 종료 후 2차만 재생");
                    RequireHitRanges(1, 0.6f, "2차 폭발의 선택된 방향만 피해 범위 표시");
                    Require(ringTargets[1].Hits == 1 && ringTargets[2].Hits == 0, "2차 폭발 중에는 3차 피해 없음");
                    Capture("wave-2.png");
                    stage = 2;
                }
                if (stage == 2 && elapsed >= 2f * data.WaveInterval + sampleDelay)
                {
                    RequireSound(3, "3차 시작 시 효과음 추가 재생, 총 세 번");
                    SpriteRenderer[] renderers = firstWave.GetComponentsInChildren<SpriteRenderer>();
                    Require(renderers[16].enabled && Mathf.Abs(Vector3.Distance(GroundPosition(renderers[16]), Center) - 2.4f) < 0.01f,
                        "마지막 원 반경 2.4, 시전 때 선택한 방향 유지");
                    Require(renderers.Count(renderer => renderer.enabled) == 1 && renderers.Take(16).All(renderer => !renderer.enabled),
                        "2차 애니메이션 종료 후 3차만 재생");
                    RequireHitRanges(1, 0.6f, "3차 폭발의 선택된 방향만 피해 범위 표시");
                    Capture("wave-3.png");
                    stage = 3;
                }
                if (stage == 3 && !checkedSoundTail && elapsed >= visualEndTime + 0.05f)
                {
                    Require(firstWave.GetComponentsInChildren<SpriteRenderer>().All(renderer => !renderer.enabled),
                        "애니메이션 종료 후 모든 폭발 숨김");
                    RequireHitRanges(0, 0.6f, "효과음 잔향 중에도 종료된 폭발의 범위 원은 숨김");
                    Require(firstWave.gameObject.activeSelf && explosionAudio.isPlaying,
                        "마지막 폭발의 1초 효과음이 애니메이션 종료 후에도 잘리지 않고 재생");
                    checkedSoundTail = true;
                }
                if (stage == 3 && elapsed >= finishTime)
                {
                    Require(ringTargets.All(target => target.Hits == 1 && Mathf.Approximately(target.Damage, 10f)), "각 원의 실제 Physics2D 피해");
                    Require(overlappingTarget.Hits == 1, "인접한 폭발과 다중 콜라이더 중복 피해 방지");
                    Require(largeTarget.Hits == 3 && Mathf.Approximately(largeTarget.Damage, 30f), "큰 적은 파동마다 한 번, 총 세 번 피격");
                    Require(friendlyTarget.Hits == 0 && outsideTarget.Hits == 0, "플레이어 레이어와 범위 밖 대상 제외");
                    Require(inactiveTarget.Hits == 0, "선택되지 않은 나머지 방향에는 피해 없음");
                    Require(crowdedTargets.All(target => target.Hits == 1), "32개를 넘는 밀집 대상도 누락 없이 피격");
                    Require(!firstWave.gameObject.activeSelf && !explosionAudio.isPlaying &&
                        firstWave.GetComponentsInChildren<SpriteRenderer>().All(renderer => !renderer.enabled), "애니메이션과 효과음 종료 후 로컬 풀 반환");
                    data.coolDown = 0f;
                    data.castCenterOffset = new Vector2(2f, 6f);
                    // 실제 스킬 중복 획득 및 유물 스탯 적용 경로를 사용합니다. 원본 에셋과 세이브 파일은 수정하지 않습니다.
                    skillStates.AcquireSkill(data);
                    skillStates.AcquireSkill(data);
                    skillStates.AcquireSkill(data);
                    Require(player.GetSkillLevel(data) == 3 && data.skillLevel == 8, "실제 획득 레벨 3을 원본 고정 레벨보다 우선하여 조회");
                    skillStates.storageSkills[0] = data;
                    SaveData save = new SaveData();
                    skillStates.PopulateSaveData(save);
                    SaveData restored = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(save));
                    Require(restored.ownedSkills.Any(entry => entry.skillId == data.skillID && entry.level == 3),
                        "성장한 레벨을 기존 세이브 형식으로 기록하고 JSON 왕복 후 유지");
                    growthRelic = new StatRelicEffect
                    {
                        statModifiers = new List<StatModifier>
                        {
                            new StatModifier { stat = StatType.ExtraProjectiles, value = 7f },
                            new StatModifier { stat = StatType.ProjectileSize, value = 0.5f, isPercentage = true },
                            new StatModifier { stat = StatType.AttackRange, value = 0.5f, isPercentage = true }
                        }
                    };
                    growthRelic.ApplyEffect(player, 1);
                    Require(player.currentPlayerData.extraProjectiles == 7f && Mathf.Approximately(player.currentPlayerData.projectileSizeMultiplier, 1.5f) &&
                        Mathf.Approximately(player.currentPlayerData.attackRangeMultiplier, 1.5f), "유물 적용으로 투사체 수, 크기, 범위 스탯 증가");
                    Vector3 shiftedCenter = caster.transform.position + (Vector3)data.castCenterOffset;
                    float spread = ChainIgnitionSkillData.GetSpreadRangeMultiplier(1.5f, 1.5f);
                    offsetTarget = Target("보정된 첫 원", shiftedCenter + Vector3.right * data.GetRingRadius(0) * spread, 0.05f);
                    unshiftedTarget = Target("보정 전 첫 원", caster.transform.position + Vector3.right * data.GetRingRadius(0), 0.05f);
                    growthTargets = Enumerable.Range(0, 8).Select(index =>
                    {
                        float angle = index * Mathf.PI / 4f;
                        return Target("성장한 방향 " + index, shiftedCenter + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * data.GetRingRadius(2) * spread, 0.05f);
                    }).ToArray();
                    float gapAngle = Mathf.PI / 8f;
                    sizeTarget = Target("확대된 개별 폭발 판정", shiftedCenter + new Vector3(Mathf.Cos(gapAngle), Mathf.Sin(gapAngle)) * 1.8f, 0.05f);
                    Physics2D.SyncTransforms();
                    Require(CastTowards(shiftedCenter + Vector3.right * 10f), "쿨다운 종료 후 재시전");
                    Require(firstWave.gameObject.activeSelf && Vector3.Distance(firstWave.transform.position, shiftedCenter) < 0.01f, "기존 폭발 오브젝트 재사용 및 X·Y 중심 보정 적용");
                    SpriteRenderer[] boostedRenderers = firstWave.GetComponentsInChildren<SpriteRenderer>();
                    Require(boostedRenderers.Count(renderer => renderer.enabled) == 8 && boostedRenderers.Take(8).All(renderer => renderer.enabled),
                        "레벨과 유물로 최대 8방향을 채우고 상한 초과 없음");
                    Require(Mathf.Approximately(boostedRenderers[0].transform.localScale.x, 2.4f) &&
                        Mathf.Abs(Vector3.Distance(GroundPosition(boostedRenderers[0]), shiftedCenter) - data.GetRingRadius(0) * spread) < 0.01f,
                        "크기 유물의 연출 확대와 50% 거리 보정, 범위 유물의 거리 확대 함께 적용");
                    RequireHitRanges(8, 0.9f, "유물로 확대된 실제 피해 반경 0.9를 선택한 8방향에 표시");
                    RequireSound(1, "재사용 후 효과음 상태 초기화 및 1차부터 재생");
                    growthRelic.RemoveEffect(player, 1);
                    skillStates.skillStates[data.skillID].level = 1;
                    Require(player.currentPlayerData.extraProjectiles == 0f && player.currentPlayerData.projectileSizeMultiplier == 1f &&
                        player.currentPlayerData.attackRangeMultiplier == 1f, "유물 해제 시 보정 스탯 원복");
                    caster.transform.position += Vector3.left * 12f;
                    startTime = Time.time;
                    stage = 4;
                    return;
                }
                if (stage == 4 && elapsed >= finishTime)
                {
                    Require(!firstWave.gameObject.activeSelf, "반복 사용 후 타이머 초기화 및 종료");
                    Require(offsetTarget.Hits == 1 && Mathf.Approximately(offsetTarget.Damage, 14f) && unshiftedTarget.Hits == 0,
                        "중심 보정으로 연출과 피해 판정이 함께 이동하고 시전자 이동 후에도 고정");
                    Require(growthTargets.All(target => target.Hits == 1 && Mathf.Approximately(target.Damage, 14f)),
                        "3레벨 피해 14와 선택한 8방향은 시전 중 유물·레벨 변경 후에도 유지");
                    Require(sizeTarget.Hits == 1 && Mathf.Approximately(sizeTarget.Damage, 14f), "크기 증가가 실제 Physics2D 피해 반경에도 적용");
                    Require(CastTowards(caster.transform.position + (Vector3)data.castCenterOffset + Vector3.right * 10f) && explosionAudio.isPlaying,
                        "비활성화 검증을 위한 효과음 재생 중 시전");
                    SpriteRenderer[] resetRenderers = firstWave.GetComponentsInChildren<SpriteRenderer>();
                    Require(resetRenderers.Count(renderer => renderer.enabled) == 1 && Mathf.Approximately(resetRenderers[0].transform.localScale.x, 1.6f),
                        "다음 시전에서는 해제된 유물과 감소한 레벨을 반영해 기본 한 방향으로 복귀");
                    skill.gameObject.SetActive(false);
                    Require(!firstWave.gameObject.activeSelf && !explosionAudio.isPlaying, "스킬 비활성화 시 폭발과 재생 중인 효과음 정리");
                    Finish(null);
                }
            }
            catch (Exception exception) { Finish(exception); }
        }

        [MenuItem("Tools/Nytherion/Chain Ignition/Verify Range Editor")]
        public static void VerifyRangeEditor()
        {
            ChainIgnitionSkillData clone = null;
            UnityEditor.Editor editor = null;
            try
            {
                ChainIgnitionSkillData source = AssetDatabase.LoadAssetAtPath<ChainIgnitionSkillData>(ChainIgnitionSkillSetup.DataPath);
                clone = Object.Instantiate(source);
                editor = UnityEditor.Editor.CreateEditor(clone);
                if (!(editor is ChainIgnitionSkillDataEditor)) throw new InvalidOperationException("피해 범위 전용 Inspector 등록 실패");
                float original = source.explosionRadius;
                Undo.IncrementCurrentGroup();
                typeof(ChainIgnitionSkillDataEditor).GetMethod("SetRadius", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(editor, new object[] { original + 0.25f });
                Undo.FlushUndoRecordObjects();
                if (!Mathf.Approximately(clone.explosionRadius, original + 0.25f))
                    throw new InvalidOperationException("손잡이의 피해 반경 변경 저장 실패");
                Undo.PerformUndo();
                if (!Mathf.Approximately(clone.explosionRadius, original)) throw new InvalidOperationException("반경 변경 Undo 실패");
                Undo.PerformRedo();
                if (!Mathf.Approximately(clone.explosionRadius, original + 0.25f) || !Mathf.Approximately(source.explosionRadius, original))
                    throw new InvalidOperationException("반경 변경 Redo 또는 원본 에셋 보호 실패");
                float originalVertical = clone.explosionVerticalRadius;
                clone.useEllipticalHitRange = true;
                Undo.IncrementCurrentGroup();
                typeof(ChainIgnitionSkillDataEditor).GetMethod("SetAxisRadius", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(editor, new object[] { 1, originalVertical + 0.1f });
                Undo.FlushUndoRecordObjects();
                if (!Mathf.Approximately(clone.explosionVerticalRadius, originalVertical + 0.1f) ||
                    !Mathf.Approximately(clone.explosionRadius, original + 0.25f)) throw new InvalidOperationException("세로 반경 독립 조절 실패");
                Undo.PerformUndo();
                if (!Mathf.Approximately(clone.explosionVerticalRadius, originalVertical)) throw new InvalidOperationException("세로 반경 Undo 실패");
                Undo.PerformRedo();
                if (!Mathf.Approximately(clone.explosionVerticalRadius, originalVertical + 0.1f)) throw new InvalidOperationException("세로 반경 Redo 실패");
                Directory.CreateDirectory(Output);
                File.WriteAllText(Output + "/range-editor-verification.txt",
                    "Unity " + Application.unityVersion + "\nPASS 전용 Inspector 등록\nPASS 가로·세로 손잡이 독립 조절과 직렬화\nPASS 두 축의 Undo / Redo\nPASS 원본 에셋 보호\nRESULT: PASS");
                Debug.Log("[ChainIgnitionSkillVerification] 피해 범위 Inspector 및 Undo/Redo 검증 통과.");
            }
            catch (Exception exception)
            {
                Directory.CreateDirectory(Output);
                File.WriteAllText(Output + "/range-editor-verification.txt", "RESULT: FAIL\n" + exception);
                Debug.LogException(exception);
            }
            finally
            {
                if (editor != null) Object.DestroyImmediate(editor);
                if (clone != null)
                {
                    Undo.ClearUndo(clone);
                    Object.DestroyImmediate(clone);
                }
                Undo.IncrementCurrentGroup();
            }
        }

        private static void Begin()
        {
            checks.Clear();
            Directory.CreateDirectory(Output);
            File.WriteAllText(Output + "/verification-progress.txt", "검증 시작\n");
            previousTimeScale = Time.timeScale;
            Time.timeScale = 1f;
            ChainIgnitionSkillData source = AssetDatabase.LoadAssetAtPath<ChainIgnitionSkillData>(ChainIgnitionSkillSetup.DataPath);
            Texture2D sheet = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Nytherion/Art/Skills/Sprites/ChainIgnition.png");
            Require(source != null && sheet != null && source.HasValidAnimation &&
                source.explosionFrames.Length == sheet.width / sheet.height && source.icon != null,
                "스킬 데이터, 원본 시트의 전체 프레임 및 아이콘 연결");
            Require(source.explosionFrames.Select((frame, index) =>
                frame.rect == new Rect(index * sheet.height, 0, sheet.height, sheet.height)).All(valid => valid),
                "수정한 프레임 크기와 재생 순서 반영");
            Require(source.skillPrefab.GetComponent<ChainIgnitionSkill>() != null && source.wavePrefab != null, "스킬 및 폭발 프리팹 연결");
            Require(source.explosionSound == AssetDatabase.LoadAssetAtPath<AudioClip>(ChainIgnitionSkillSetup.SoundPath) &&
                source.explosionSound != null && Mathf.Approximately(source.explosionSound.length, 1f), "첨부한 1초 WAV 효과음 연결");
            AudioSource prefabAudio = source.wavePrefab.GetComponent<AudioSource>();
            Require(prefabAudio != null && !prefabAudio.playOnAwake && !prefabAudio.loop && prefabAudio.spatialBlend == 0f,
                "폭발 프리팹의 자동 재생 없는 2D AudioSource 연결");
            SkillDatabaseSO database = AssetDatabase.LoadAssetAtPath<SkillDatabaseSO>("Assets/Nytherion/Data/ScriptableObjects/Skill/SkillDatabaseSO.asset");
            Require(database.allSkills.Count(entry => entry == source) == 1 && database.GetSkillById(source.skillID) == source, "데이터베이스 등록과 저장 ID 조회");
            data = Object.Instantiate(source);
            runtimeData.Add(data);
            // 원본에서 조절한 피해 범위와 볼륨은 보존하고, 기존 원형 동작은 고정된 복제 데이터로 검증합니다.
            data.explosionRadius = 0.6f;
            data.useEllipticalHitRange = false;
            // 기존 파동·밀집 적 회귀 검증은 고정 거리에서 실행하고 새 기본 거리와 성장 보정은 별도로 확인합니다.
            data.firstRingRadius = 0.8f;
            data.range = 2.4f;
            Require(source.baseProjectileCount == 1 && source.levelsPerProjectile == 2 && source.damagePerLevel == 2f &&
                source.firstRingRadius == 1f && source.range == 3f && source.explosionSoundVolume >= 0f && source.explosionSoundVolume <= 1f,
                "기본 폭발 거리 1·2·3과 성장 설정 및 유효한 효과음 볼륨 저장");
            Require(new SerializedObject(data).FindProperty("castCenterOffset") != null, "Inspector 중심 보정 필드 직렬화");
            Require(new SerializedObject(data).FindProperty("explosionVisualOffset") != null && data.explosionVisualOffset.y > 0f,
                "Inspector 이미지 보정 필드와 바닥 위로 올라가는 기본 정렬 저장");
            caster = Track(new GameObject("[ChainIgnitionVerification] 시전자"));
            caster.transform.position = Center;
            PlayerData baseStats = ScriptableObject.CreateInstance<PlayerData>();
            runtimeData.Add(baseStats);
            baseStats.maxHealth = 100f;
            baseStats.projectileSizeMultiplier = 1f;
            baseStats.attackRangeMultiplier = 1f;
            player = caster.AddComponent<PlayerManager>();
            SerializedObject serializedPlayer = new SerializedObject(player);
            serializedPlayer.FindProperty("basePlayerData").objectReferenceValue = baseStats;
            serializedPlayer.ApplyModifiedPropertiesWithoutUndo();
            skillStates = Track(new GameObject("[ChainIgnitionVerification] 스킬 레벨")).AddComponent<SkillDataManager>();
            skillStates.skillStates[data.skillID] = new SkillState { level = 1 };
            player.Construct(null, null, null, null, skillStates);
            player.Initialize();
            runtimeData.Add(player.currentPlayerData);
            data.skillLevel = 8;
            VerifyDebugPanel(source);
            VerifySizeRangeGrowth(source);
            VerifyDirectionSelection();
            VerifyGroundDamage();
            VerifyEllipticalDamage();
            DiagnoseShownHitRange(source);
            skill = Object.Instantiate(source.skillPrefab, caster.transform).GetComponent<ChainIgnitionSkill>();
            skill.skillData = data;
            skill.caster = caster.transform;
            GameObject weaponPoint = Track(new GameObject("[ChainIgnitionVerification] 발사점"));
            weaponPoint.transform.position = Center + Vector3.right * 30f;
            skill.firePoint = weaponPoint.transform;
            VerifyAimAlignment();
            ringTargets = Enumerable.Range(0, 3).Select(index => Target("원 " + index, Center + Vector3.right * data.GetRingRadius(index), 0.05f)).ToArray();
            overlappingTarget = Target("중복 판정", Center + new Vector3(0.683f, 0.283f), 0.05f);
            new GameObject("추가 콜라이더").transform.SetParent(overlappingTarget.transform, false);
            overlappingTarget.transform.GetChild(0).gameObject.layer = LayerMask.NameToLayer("Enemy");
            overlappingTarget.transform.GetChild(0).gameObject.AddComponent<CircleCollider2D>().radius = 0.05f;
            largeTarget = Target("큰 적", Center, 3f);
            friendlyTarget = Target("플레이어 레이어", Center + Vector3.right * 0.8f, 0.05f, 0);
            inactiveTarget = Target("선택되지 않은 왼쪽", Center + Vector3.left * 2.4f, 0.05f);
            outsideTarget = Target("범위 밖", Center + Vector3.right * 7f, 0.05f);
            for (int i = 0; i < 40; i++) crowdedTargets.Add(Target("밀집 " + i, Center + Vector3.right * 2.4f, 0.03f));
            Physics2D.SyncTransforms();
            Require(CastTowards(Center + Vector3.right * 10f), "실제 SkillBase.TryUse 시전과 마우스 입력 경로");
            Require(!skill.TryUse(), "시전 직후 쿨다운 중 재시전 차단");
            startTime = Time.time;
            stage = 0;
            checkedSoundTail = false;
        }

        private static void VerifyDebugPanel(ChainIgnitionSkillData source)
        {
            ChainIgnitionDebugUI sceneUI = Object.FindObjectsOfType<ChainIgnitionDebugUI>(true)
                .Single(ui => ui.gameObject.scene == SceneManager.GetActiveScene());
            DebugPanelUI scenePanel = sceneUI.GetComponentInParent<DebugPanelUI>();
            Require(sceneUI.name == "ContentPanel2" && scenePanel != null &&
                new SerializedObject(scenePanel).FindProperty("contentPanel2").objectReferenceValue == sceneUI.gameObject,
                "ContentPanel2에 연쇄 점화 UI와 디버그 패널 참조 연결");
            Require((bool)typeof(ChainIgnitionDebugUI).GetField("initialized", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(sceneUI),
                "실제 GameScene의 주입된 플레이어로 디버그 UI 초기화");
            bool wasOpen = scenePanel.IsOpen;
            scenePanel.Open(false);
            Require(sceneUI.gameObject.activeInHierarchy && scenePanel.GetComponent<CanvasGroup>().interactable,
                "F12 디버그 패널 열기 경로로 ContentPanel2 조작 활성화");
            scenePanel.Close();
            Require(!sceneUI.gameObject.activeSelf, "디버그 패널 닫기 시 ContentPanel2 숨김");
            if (wasOpen) scenePanel.Open(false);

            string sourceBefore = EditorJsonUtility.ToJson(source);
            GameObject fixture = Track(new GameObject("[ChainIgnitionVerification] 디버그 UI", typeof(RectTransform), typeof(Canvas)));
            fixture.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            ChainIgnitionDebugUI ui = Object.Instantiate(sceneUI, fixture.transform);
            ui.gameObject.SetActive(true);
            RectTransform uiRect = ui.GetComponent<RectTransform>();
            uiRect.anchorMin = uiRect.anchorMax = new Vector2(0.5f, 0.5f);
            uiRect.sizeDelta = new Vector2(800f, 960f);
            uiRect.anchoredPosition = Vector2.zero;
            ui.Initialize(player, null);
            Transform root = ui.transform.Find("ChainIgnitionControls");
            Transform rows = root.Find("Scroll/Viewport/Rows");
            ChainIgnitionDebugSettings settings = player.GetComponent<ChainIgnitionDebugSettings>();
            Require(settings != null && !settings.useTestSettings && !settings.overrideProjectileCount,
                "디버그 조작 전에는 레벨·유물에 따른 기존 시전 유지");
            Require(rows.GetComponentsInChildren<Slider>(true).Length == 13 && rows.GetComponentsInChildren<TMP_InputField>(true).Length == 13 &&
                rows.GetComponentsInChildren<Toggle>(true).Length == 4,
                "13개 슬라이더/숫자 입력과 4개 표시·판정 토글 생성");
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(rows.GetComponent<RectTransform>());
            RectTransform viewport = root.Find("Scroll/Viewport").GetComponent<RectTransform>();
            Require(viewport.rect.height > 0f && viewport.rect.width > 0f &&
                rows.GetComponent<RectTransform>().rect.height > viewport.rect.height &&
                root.Find("Scroll").GetComponent<ScrollRect>().content == rows,
                "컨트롤의 유효한 화면 크기와 스크롤 연결");
            rows.Find("ProjectileCount/Slider").GetComponent<Slider>().value = 3;
            Require(settings.projectileCount == 3 && settings.overrideProjectileCount && settings.useTestSettings,
                "투사체 슬라이더 조절 시 3개 고정 및 테스트 모드 활성화");
            void Input(string name, string value) => rows.Find(name + "/Value").GetComponent<TMP_InputField>().onEndEdit.Invoke(value);
            Input("SizeMultiplier", "1.5");
            Require(Mathf.Approximately(rows.Find("RangeMultiplier/Slider").GetComponent<Slider>().value, 1.25f) &&
                rows.Find("RangeMultiplier/Value").GetComponent<TMP_InputField>().text == "1.25",
                "디버그 크기 배율 1.5 입력 시 거리 배율 표시가 1.25로 자동 증가");
            Input("RangeMultiplier", "2");
            Input("FirstRingRadius", "1"); Input("LastRingRadius", "3");
            Input("HorizontalRadius", "0.4"); Input("VerticalRadius", "0.2");
            Input("VisualScale", "1.2"); Input("CenterX", "-0.25"); Input("CenterY", "-0.5");
            Input("SoundVolume", "0");
            rows.Find("UseEllipse").GetComponent<Toggle>().isOn = false;
            Require(!settings.useEllipse && !rows.Find("VerticalRadius/Value").GetComponent<TMP_InputField>().interactable,
                "원형 전환 시 세로 반경 입력 비활성화");
            rows.Find("UseEllipse").GetComponent<Toggle>().isOn = true;
            rows.Find("ShowHitRanges").GetComponent<Toggle>().isOn = true;
            root.Find("Actions/Cast").GetComponent<Button>().onClick.Invoke();
            ChainIgnitionSkill probe = (ChainIgnitionSkill)typeof(ChainIgnitionDebugUI)
                .GetField("testSkill", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(ui);
            var waves = (List<ChainIgnitionWave>)typeof(ChainIgnitionSkill)
                .GetField("waves", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(probe);
            ChainIgnitionWave wave = waves.Single();
            ChainIgnitionSkillData castData = (ChainIgnitionSkillData)typeof(ChainIgnitionWave)
                .GetField("data", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(wave);
            Vector2 radii = (Vector2)typeof(ChainIgnitionWave).GetField("castExplosionRadii", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(wave);
            Vector3[] positions = (Vector3[])typeof(ChainIgnitionWave).GetField("explosionGroundPositions", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(wave);
            Require(wave.GetComponentsInChildren<SpriteRenderer>().Count(renderer => renderer.enabled) == 3 && castData != source,
                "장착 없는 테스트 시전 버튼에서 실제 3방향 폭발과 시전 전용 데이터 사용");
            Require(Vector2.Distance(radii, new Vector2(0.6f, 0.3f)) < 0.001f &&
                Mathf.Abs(positions[0].magnitude - 2f) < 0.001f && Mathf.Abs(positions[8].magnitude - 4f) < 0.001f &&
                Mathf.Abs(positions[16].magnitude - 6f) < 0.001f &&
                Vector3.Distance(wave.transform.position, player.transform.position + new Vector3(-0.25f, -0.5f)) < 0.001f,
                "입력한 크기·거리 배율, 타원 반경과 전체 중심 보정을 실제 파동에 적용");
            Require(Mathf.Approximately(wave.GetComponentsInChildren<SpriteRenderer>()[0].transform.localScale.x, 1.8f),
                "이펙트 크기와 전체 크기 배율 결합");
            Input("VerticalRadius", "0.5");
            Require(Mathf.Approximately(castData.explosionVerticalRadius, 0.2f) && Mathf.Approximately(settings.verticalRadius, 0.5f),
                "값을 바꿔도 진행 중인 파동의 시전 데이터 유지");
            Input("ProjectileCount", "99"); Input("HorizontalRadius", "NaN");
            Require(settings.projectileCount == 8 && Mathf.Approximately(settings.horizontalRadius, 0.4f),
                "투사체 최대 8개 제한 및 잘못된 숫자 입력 복원");
            root.Find("Actions/Reset").GetComponent<Button>().onClick.Invoke();
            Require(!settings.useTestSettings && !settings.overrideProjectileCount &&
                Mathf.Approximately(settings.horizontalRadius, source.explosionRadius) &&
                Mathf.Approximately(settings.verticalRadius, source.explosionVerticalRadius) &&
                settings.GetProjectileCount(source, 5, 2) == source.GetProjectileCount(5, 2),
                "기본값 복원 버튼과 레벨·유물 투사체 계산 복구");
            Require(EditorJsonUtility.ToJson(source) == sourceBefore &&
                Mathf.Approximately(player.currentPlayerData.projectileSizeMultiplier, 1f) &&
                Mathf.Approximately(player.currentPlayerData.attackRangeMultiplier, 1f),
                "디버그 조절과 시전 후 원본 스킬 에셋·플레이어 능력치 보호");
            Object.DestroyImmediate(fixture);
        }

        private static void VerifySizeRangeGrowth(ChainIgnitionSkillData source)
        {
            float oldSize = player.currentPlayerData.projectileSizeMultiplier;
            float oldRange = player.currentPlayerData.attackRangeMultiplier;
            ChainIgnitionSkillData probeData = Object.Instantiate(source);
            runtimeData.Add(probeData);
            probeData.explosionSoundVolume = 0f;
            float[] sizes = { 1f, 1.5f, 2f, 3f, 2f };
            float[] ranges = { 1f, 1f, 1f, 1f, 1.4f };
            float[] expected = { 1f, 1.25f, 1.5f, 2f, 1.9f };
            try
            {
                for (int test = 0; test < sizes.Length; test++)
                {
                    player.currentPlayerData.projectileSizeMultiplier = sizes[test];
                    player.currentPlayerData.attackRangeMultiplier = ranges[test];
                    ChainIgnitionSkill probe = Object.Instantiate(source.skillPrefab, caster.transform).GetComponent<ChainIgnitionSkill>();
                    probe.skillData = probeData;
                    probe.caster = caster.transform;
                    ChainIgnitionWave wave = null;
                    try
                    {
                        Require(probe.TryUse(), "기본 거리·크기 성장 검증 실제 스킬 시전");
                        wave = ((List<ChainIgnitionWave>)typeof(ChainIgnitionSkill)
                            .GetField("waves", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(probe)).Single();
                        Vector3[] positions = (Vector3[])typeof(ChainIgnitionWave)
                            .GetField("explosionGroundPositions", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(wave);
                        Require(Mathf.Abs(positions[0].magnitude - expected[test]) < 0.001f &&
                            Mathf.Abs(positions[8].magnitude - 2f * expected[test]) < 0.001f &&
                            Mathf.Abs(positions[16].magnitude - 3f * expected[test]) < 0.001f,
                            $"실제 1·2·3차 거리: 크기 {sizes[test]}, 범위 유물 {ranges[test]}이면 거리 배율 {expected[test]}");
                    }
                    finally
                    {
                        if (wave != null) Object.DestroyImmediate(wave.gameObject);
                        Object.DestroyImmediate(probe.gameObject);
                    }
                }
            }
            finally
            {
                player.currentPlayerData.projectileSizeMultiplier = oldSize;
                player.currentPlayerData.attackRangeMultiplier = oldRange;
            }
        }

        private static void RequireSound(int count, string name)
        {
            bool[] played = (bool[])typeof(ChainIgnitionWave).GetField("playedSoundWaves", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(firstWave);
            Require(played.Count(value => value) == count && explosionAudio.isPlaying &&
                Mathf.Approximately(explosionAudio.volume, UserSettings.GetSfxVolume()), name);
        }

        private static void RequireHitRanges(int count, float radius, string name)
        {
            LineRenderer[] visible = firstWave.GetComponentsInChildren<LineRenderer>().Where(line => line.enabled).ToArray();
            SpriteRenderer[] explosions = firstWave.GetComponentsInChildren<SpriteRenderer>().Where(renderer => renderer.enabled).ToArray();
            Require(visible.Length == count && visible.All(line => line.loop && !line.useWorldSpace &&
                line.sharedMaterial != null && Enumerable.Range(0, line.positionCount).All(point =>
                    Mathf.Abs(line.GetPosition(point).magnitude - radius) < 0.001f) &&
                explosions.Any(explosion => Vector3.Distance(GroundPosition(explosion), line.transform.position) < 0.001f)), name);
        }

        private static Vector3 GroundPosition(SpriteRenderer explosion)
        {
            return explosion.transform.position - (Vector3)data.explosionVisualOffset * explosion.transform.localScale.x;
        }

        private static void VerifyGroundDamage()
        {
            ChainIgnitionSkillData probeData = Object.Instantiate(data);
            runtimeData.Add(probeData);
            probeData.explosionSound = null;
            probeData.damageFrame = 0;
            Vector3 probeCenter = Center + Vector3.left * 100f;
            Vector3 blastCenter = probeCenter + Vector3.right * probeData.GetRingRadius(0);
            EnemyData enemyData = ScriptableObject.CreateInstance<EnemyData>();
            runtimeData.Add(enemyData);
            enemyData.maxHealth = 100f;
            EnemyBase feetInside = GroundEnemy("지면 위치가 범위 안", blastCenter, enemyData);
            EnemyBase torsoInside = GroundEnemy("공격용 콜라이더만 범위 안", blastCenter + Vector3.down * 1.37f, enemyData);
            EnemyBase headInside = GroundEnemy("몸체의 세로 끝이 범위 안", blastCenter + Vector3.down * 1.34f, enemyData);
            EnemyBase edgeInside = GroundEnemy("몸체가 경계에 닿음", blastCenter + Vector3.right * 0.79f, enemyData);
            EnemyBase edgeOutside = GroundEnemy("몸체가 경계 밖", blastCenter + Vector3.right * 0.83f, enemyData);
            // 몸체가 닿지 않는 적의 공격용 콜라이더만 폭발 위치에 놓습니다.
            GameObject attack = new GameObject("공격용 콜라이더");
            attack.transform.SetParent(torsoInside.transform, false);
            attack.layer = LayerMask.NameToLayer("Enemy");
            attack.transform.position = blastCenter;
            attack.AddComponent<CircleCollider2D>().radius = 0.1f;
            Physics2D.SyncTransforms();
            Require(feetInside.TryGetGroundHitBounds(out Bounds bodyBounds) &&
                Vector2.Distance(bodyBounds.center, blastCenter) < 0.001f &&
                Vector2.Distance(bodyBounds.size, new Vector2(0.4f, 1.5f)) < 0.001f,
                "실제 EnemyBase의 지면 피격 영역은 몸체의 전체 가로·세로 크기와 일치");
            ChainIgnitionWave probe = Object.Instantiate(data.wavePrefab);
            Track(probe.gameObject);
            probe.Begin(probeData, probeCenter, Vector2.right, 1, 10f);
            FieldInfo health = typeof(EnemyBase).GetField("currentHealth", BindingFlags.Instance | BindingFlags.NonPublic);
            Require(Mathf.Approximately((float)health.GetValue(feetInside), 90f) &&
                Mathf.Approximately((float)health.GetValue(torsoInside), 100f) &&
                Mathf.Approximately((float)health.GetValue(headInside), 90f),
                "몸체의 중심과 세로 끝 모두 피격, 공격 콜라이더만 겹치면 피해 없음");
            Require(Mathf.Approximately((float)health.GetValue(edgeInside), 90f) &&
                Mathf.Approximately((float)health.GetValue(edgeOutside), 100f), "몸체의 가장자리 접촉과 범위 밖 구분");
            probe.gameObject.SetActive(false);
            probeData.explosionVisualOffset = new Vector2(0.2f, 1.5f);
            probe.Begin(probeData, probeCenter, Vector2.right, 1, 10f);
            Require(Mathf.Approximately((float)health.GetValue(feetInside), 80f) &&
                Mathf.Approximately((float)health.GetValue(torsoInside), 100f), "이미지 X·Y 보정을 바꿔도 실제 바닥 판정 위치 유지");
            probe.gameObject.SetActive(false);
            probeData.visualScale *= 2f;
            probe.Begin(probeData, probeCenter, Vector2.right, 1, 10f, 1.5f);
            SpriteRenderer explosion = probe.GetComponentsInChildren<SpriteRenderer>().First(renderer => renderer.enabled);
            Vector3 expectedOffset = (Vector3)probeData.explosionVisualOffset * probeData.visualScale * 1.5f;
            Require(Vector3.Distance(explosion.transform.position - expectedOffset, blastCenter) < 0.001f &&
                Mathf.Approximately((float)health.GetValue(feetInside), 70f), "Visual Scale과 크기 유물 변경 후에도 불꽃의 바닥 정렬 유지");
            probe.gameObject.SetActive(false);
            feetInside.GetComponent<BoxCollider2D>().enabled = false;
            Physics2D.SyncTransforms();
            Require(!feetInside.TryGetGroundHitBounds(out _), "비활성 몸체의 지면 피격 영역 제외");
            VerifyEnemyPrefabGroundAreas();
        }

        private static EnemyBase GroundEnemy(string name, Vector3 position, EnemyData enemyData)
        {
            GameObject obj = Track(new GameObject("[ChainIgnitionVerification] " + name));
            obj.transform.position = position;
            obj.layer = LayerMask.NameToLayer("Enemy");
            obj.AddComponent<BoxCollider2D>().size = new Vector2(0.4f, 1.5f);
            EnemyBase enemy = obj.AddComponent<EnemyBase>();
            enemy.Initialize(enemyData);
            return enemy;
        }

        private static void VerifyEllipticalDamage()
        {
            ChainIgnitionSkillData probeData = Object.Instantiate(data);
            runtimeData.Add(probeData);
            probeData.useEllipticalHitRange = true;
            probeData.explosionRadius = 0.6f;
            probeData.explosionVerticalRadius = 0.2f;
            probeData.damageFrame = 0;
            probeData.explosionSound = null;
            probeData.showHitRanges = true;
            Vector3 center = Center + Vector3.left * 150f;
            Vector3 floor = center + Vector3.right * probeData.GetRingRadius(0);
            EnemyData enemyData = ScriptableObject.CreateInstance<EnemyData>();
            runtimeData.Add(enemyData);
            enemyData.maxHealth = 100f;
            Vector2 edge = new Vector2(0.6f, 0.2f) / Mathf.Sqrt(2f);
            Vector2 normal = new Vector2(1f / 0.6f, 1f / 0.2f).normalized;
            Vector2 corner = new Vector2(0.2f, 0.75f);
            Vector2[] offsets = { new Vector2(0.79f, 0f), new Vector2(0.83f, 0f),
                new Vector2(0f, 0.94f), new Vector2(0f, 0.97f),
                corner + edge - normal * 0.01f, corner + edge + normal * 0.02f };
            EnemyBase[] targets = offsets.Select((offset, index) => GroundEnemy("타원 경계 " + index,
                floor + (Vector3)offset, enemyData)).ToArray();
            Physics2D.SyncTransforms();
            ChainIgnitionWave probe = Object.Instantiate(data.wavePrefab);
            Track(probe.gameObject);
            probe.Begin(probeData, center, Vector2.right, 1, 10f);
            FieldInfo health = typeof(EnemyBase).GetField("currentHealth", BindingFlags.Instance | BindingFlags.NonPublic);
            Require(targets.Select((enemy, index) => Mathf.Approximately((float)health.GetValue(enemy), index % 2 == 0 ? 90f : 100f)).All(value => value),
                "실제 EnemyBase의 전체 몸체와 타원의 가로·세로·대각선 경계 접촉 판정");
            Require(ChainIgnitionHitRange.OverlapsCircle(edge + normal * 0.1f, new Vector2(0.6f, 0.2f), 0.1f) &&
                !ChainIgnitionHitRange.OverlapsCircle(edge + normal * 0.12f, new Vector2(0.6f, 0.2f), 0.1f) &&
                ChainIgnitionHitRange.OverlapsCircle(new Vector2(0f, 0.65f), new Vector2(0.2f, 0.6f), 0.1f),
                "타원 경계의 정확한 접선과 세로가 더 긴 타원 지원");
            LineRenderer line = probe.GetComponentsInChildren<LineRenderer>().Single(renderer => renderer.enabled);
            Require(Enumerable.Range(0, line.positionCount).All(index =>
            {
                Vector3 point = line.GetPosition(index);
                return Mathf.Abs(point.x * point.x / 0.36f + point.y * point.y / 0.04f - 1f) < 0.001f;
            }), "게임 화면의 표시점은 실제 타원 반경 0.6 × 0.2와 일치");
            probe.gameObject.SetActive(false);
            probe.Begin(probeData, center, Vector2.right, 1, 10f, 1.5f);
            line = probe.GetComponentsInChildren<LineRenderer>().Single(renderer => renderer.enabled);
            Require(Mathf.Abs(line.GetPosition(0).x - 0.9f) < 0.001f && Mathf.Abs(line.GetPosition(16).y - 0.3f) < 0.001f &&
                Mathf.Approximately((float)health.GetValue(targets[3]), 90f), "크기 증가 유물은 타원의 두 축과 실제 피해 영역을 함께 확대");
            probeData.explosionVerticalRadius = 2f;
            probe.SendMessage("Update");
            Require(Mathf.Abs(line.GetPosition(16).y - 0.3f) < 0.001f, "시전 중 설정 변경에도 시전 당시의 타원 유지");
            probe.gameObject.SetActive(false);
        }

        private static void VerifyEnemyPrefabGroundAreas()
        {
            GameObject parent = Track(new GameObject("[ChainIgnitionVerification] 적 프리팹 확인"));
            parent.SetActive(false);
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/Characters/Enemies", "Assets/Resources/Sprites/Monster", "Assets/Prefabs/Debug" }))
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                if (prefab.GetComponent<EnemyBase>() == null) continue;
                GameObject clone = Object.Instantiate(prefab, parent.transform);
                foreach (Behaviour behaviour in clone.GetComponentsInChildren<Behaviour>(true))
                    if (!(behaviour is EnemyBase) && !(behaviour is Collider2D)) behaviour.enabled = false;
                clone.transform.position = Center + Vector3.down * 100f;
                parent.SetActive(true);
                Physics2D.SyncTransforms();
                EnemyBase enemy = clone.GetComponent<EnemyBase>();
                Collider2D body = enemy.GetComponent<Collider2D>();
                Require(enemy.TryGetGroundHitBounds(out Bounds bounds) &&
                    Vector2.Distance(bounds.center, body.bounds.center) < 0.001f &&
                    Vector2.Distance(bounds.size, body.bounds.size) < 0.001f &&
                    body.gameObject.layer == LayerMask.NameToLayer("Enemy"),
                    "적 프리팹 " + prefab.name + "의 Inspector 참조, 전체 몸체 판정 및 Enemy 레이어 확인");
                Require(body.OverlapPoint(new Vector2(bounds.center.x, bounds.max.y - 0.01f)),
                    "적 프리팹 " + prefab.name + "의 몸체 세로 끝에서 일반 공격 Physics2D 접촉 확인");
                parent.SetActive(false);
                Object.DestroyImmediate(clone);
            }
        }

        private static void DiagnoseShownHitRange(ChainIgnitionSkillData source)
        {
            var report = new List<string>();
            ChainIgnitionSkillData probeData = Object.Instantiate(source);
            runtimeData.Add(probeData);
            probeData.explosionSound = null;
            probeData.showHitRanges = true;
            Vector3 center = Center + Vector3.left * 250f;
            Vector3 floor = center + Vector3.right * probeData.GetRingRadius(0);
            EnemyData enemyData = ScriptableObject.CreateInstance<EnemyData>();
            runtimeData.Add(enemyData);
            enemyData.maxHealth = 100f;
            EnemyBase moving = GroundEnemy("표시 범위 진입 진단", floor + Vector3.right * 8f, enemyData);
            FieldInfo health = typeof(EnemyBase).GetField("currentHealth", BindingFlags.Instance | BindingFlags.NonPublic);
            ChainIgnitionWave probe = Object.Instantiate(source.wavePrefab);
            Track(probe.gameObject);
            Physics2D.SyncTransforms();
            probe.Begin(probeData, center, Vector2.right, 1, 10f);
            FieldInfo time = typeof(ChainIgnitionWave).GetField("startTime", BindingFlags.Instance | BindingFlags.NonPublic);
            time.SetValue(probe, Time.time - probeData.DamageDelay - 0.02f);
            probe.SendMessage("Update");
            moving.transform.position = floor + Vector3.right * (probeData.explosionRadius + 0.098f);
            Physics2D.SyncTransforms();
            moving.TryGetGroundHitCircle(out Vector2 feet, out float footRadius);
            probe.SendMessage("Update");
            report.Add("LATE_ENTRY: inside=" + ChainIgnitionHitRange.OverlapsCircle(feet - (Vector2)floor, probeData.GetExplosionRadii(), footRadius) +
                " visible=" + probe.GetComponentsInChildren<LineRenderer>().Any(line => line.enabled) + " health=" + health.GetValue(moving));
            probe.gameObject.SetActive(false);
            moving.transform.position = floor + Vector3.right * 8f;
            Physics2D.SyncTransforms();
            moving.transform.position = floor + Vector3.right * (probeData.explosionRadius + 0.098f);
            probeData.damageFrame = 0;
            probe.Begin(probeData, center, Vector2.right, 1, 10f);
            report.Add("MOVED_THIS_FRAME: autoSync=" + Physics2D.autoSyncTransforms + " health=" + health.GetValue(moving));
            Require(Mathf.Approximately((float)health.GetValue(moving), 90f), "물리 프레임을 기다리지 않고 이동 직후 표시 타원 경계에 닿은 적 피격");
            probe.gameObject.SetActive(false);
            Physics2D.SyncTransforms();
            probe.Begin(probeData, center, Vector2.right, 1, 10f);
            report.Add("AFTER_SYNC: health=" + health.GetValue(moving));
            probe.gameObject.SetActive(false);

            GameObject dummyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Debug/TrainingDummy.prefab");
            if (dummyPrefab != null)
            {
                TrainingDummy dummy = Object.Instantiate(dummyPrefab).GetComponent<TrainingDummy>();
                Track(dummy.gameObject);
                dummy.transform.position = floor + Vector3.down * probeData.GetExplosionRadii().y * 0.7f;
                dummy.Initialize(enemyData);
                EventManager events = Track(new GameObject("[ChainIgnitionVerification] 허수아비 피격 이벤트")).AddComponent<EventManager>();
                dummy.Construct(events, null, null);
                int hits = 0;
                events.OnEnemyDamagedByPlayerDetailed += hit => { if (hit.Target == dummy) hits++; };
                Physics2D.SyncTransforms();
                dummy.TryGetGroundHitCircle(out Vector2 dummyGround, out float dummyRadius);
                Vector2 body = dummy.GetComponent<Collider2D>().bounds.center;
                bool bodyInside = ChainIgnitionHitRange.OverlapsCircle(body - (Vector2)floor, probeData.GetExplosionRadii(), 0f);
                probe.Begin(probeData, center, Vector2.right, 1, 10f);
                report.Add("STATIC_DUMMY: bodyInside=" + bodyInside + " groundOffset=" + (dummyGround - body) +
                    " groundInside=" + ChainIgnitionHitRange.OverlapsCircle(dummyGround - (Vector2)floor, probeData.GetExplosionRadii(), dummyRadius) +
                    " hits=" + hits);
                Require(bodyInside && Vector2.Distance(dummyGround, body) < 0.001f && hits == 1,
                    "표시 타원의 아래쪽 내부에 서 있는 실제 허수아비도 한 번 피격 (기존 중심 -0.25 보정으로 빗나가던 위치)");
                probe.gameObject.SetActive(false);
                Object.DestroyImmediate(dummy.gameObject);
            }

            GameObject parent = Track(new GameObject("[ChainIgnitionVerification] 실제 프리팹 경계 진단"));
            parent.SetActive(false);
            var filter = new ContactFilter2D { useTriggers = true };
            filter.SetLayerMask(source.targetLayers);
            var candidates = new List<Collider2D>();
            Vector2 axes = source.GetExplosionRadii();
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/Characters/Enemies", "Assets/Resources/Sprites/Monster", "Assets/Prefabs/Debug" }))
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                if (prefab.GetComponent<EnemyBase>() == null) continue;
                GameObject clone = Object.Instantiate(prefab, parent.transform);
                foreach (Behaviour behaviour in clone.GetComponentsInChildren<Behaviour>(true))
                    if (!(behaviour is EnemyBase) && !(behaviour is Collider2D)) behaviour.enabled = false;
                clone.transform.position = Center + Vector3.down * 250f;
                parent.SetActive(true);
                Physics2D.SyncTransforms();
                EnemyBase enemy = clone.GetComponent<EnemyBase>();
                if (enemy.TryGetGroundHitCircle(out feet, out footRadius))
                {
                    int missed = 0;
                    for (int angle = 0; angle < 8; angle++)
                    {
                        float theta = angle * Mathf.PI / 4f;
                        Vector2 edge = new Vector2(Mathf.Cos(theta) * axes.x, Mathf.Sin(theta) * axes.y);
                        Vector2 normal = new Vector2(Mathf.Cos(theta) / axes.x, Mathf.Sin(theta) / axes.y).normalized;
                        Vector2 blast = feet - edge - normal * Mathf.Max(0f, footRadius - 0.002f);
                        candidates.Clear();
                        Physics2D.OverlapCircle(blast, Mathf.Max(axes.x, axes.y), filter, candidates);
                        bool inside = ChainIgnitionHitRange.OverlapsCircle(feet - blast, axes, footRadius);
                        bool found = candidates.Any(hit => hit.GetComponentInParent<EnemyBase>() == enemy);
                        if (inside && !found) missed++;
                    }
                    report.Add("PREFAB " + prefab.name + ": bodyLayer=" + enemy.GetComponent<Collider2D>().gameObject.layer +
                        " footOffset=" + (feet - (Vector2)enemy.transform.position) + " footRadius=" + footRadius + " missed=" + missed + "/8");
                }
                parent.SetActive(false);
                Object.DestroyImmediate(clone);
            }
            File.WriteAllLines(Output + "/hit-range-diagnostics.txt", report);
        }

        private static bool CastTowards(Vector3 worldPosition, ChainIgnitionSkill ability = null)
        {
            Require(Camera.main != null && Mouse.current != null, "메인 카메라와 마우스 입력 장치 준비");
            Vector2 previousPosition = Mouse.current.position.ReadValue();
            InputUpdateType updateType = InputState.currentUpdateType;
            try
            {
                // EditorApplication.update의 입력 버퍼를 사용하고 검사 직후 원래 값을 복원합니다.
                InputState.Change(Mouse.current.position, (Vector2)Camera.main.WorldToScreenPoint(worldPosition), updateType);
                return (ability != null ? ability : skill).TryUse();
            }
            finally { InputState.Change(Mouse.current.position, previousPosition, updateType); }
        }

        private static void VerifyAimAlignment()
        {
            // 원본 에셋의 중심 보정을 그대로 사용해 실제 마우스 입력부터 바닥 위치까지 확인합니다.
            ChainIgnitionSkillData probeData = Object.Instantiate(data);
            runtimeData.Add(probeData);
            probeData.coolDown = 0f;
            probeData.explosionSound = null;
            foreach (float distance in new[] { 0.9f, 8f })
            {
                bool allAligned = true;
                for (int direction = 0; direction < ChainIgnitionSkillData.DirectionCount; direction++)
                {
                    // 같은 프레임의 연속 호출은 쿨다운 0이어도 막히므로 각 방향은 새 시전자로 확인합니다.
                    ChainIgnitionSkill probe = Object.Instantiate(data.skillPrefab, caster.transform).GetComponent<ChainIgnitionSkill>();
                    Track(probe.gameObject);
                    probe.skillData = probeData;
                    probe.caster = caster.transform;
                    probe.firePoint = skill.firePoint;
                    float angle = direction * Mathf.PI / 4f;
                    Vector3 aim = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
                    Require(CastTowards(caster.transform.position + aim * distance, probe), "조준 정렬 검증 시전");
                    ChainIgnitionWave wave = Object.FindObjectsOfType<ChainIgnitionWave>().Single(candidate =>
                        Vector3.Distance(candidate.transform.position, caster.transform.position + (Vector3)probeData.castCenterOffset) < 0.001f);
                    SpriteRenderer explosion = wave.GetComponentsInChildren<SpriteRenderer>().Single(renderer => renderer.enabled);
                    Vector3 groundPosition = explosion.transform.position - (Vector3)probeData.explosionVisualOffset * explosion.transform.localScale.x;
                    allAligned &= explosion.name == "Explosion_1_" + (direction + 1) &&
                        Vector3.Distance(groundPosition, caster.transform.position + aim * probeData.GetRingRadius(0)) < 0.001f;
                    wave.gameObject.SetActive(false);
                    Object.DestroyImmediate(probe.gameObject);
                }
                Require(allAligned, "원본 중심 보정으로 거리 " + distance + "의 8방향 조준과 바닥 타격 중심 일치");
            }
        }

        private static void VerifyDirectionSelection()
        {
            Require(data.GetProjectileCount(1) == 1 && data.GetProjectileCount(2) == 1 && data.GetProjectileCount(3) == 2 &&
                data.GetProjectileCount(5) == 3 && data.GetProjectileCount(9) == 5 && data.GetProjectileCount(20) == 8 &&
                data.GetProjectileCount(3, 100f) == 8 && data.GetProjectileCount(1, -100f) == 1,
                "지정한 레벨 간격의 투사체 성장과 1~8개 상한");
            Require(data.GetDamage(1) == 10f && data.GetDamage(3) == 14f && data.GetDamage(10) == 28f,
                "레벨마다 기본 피해 2 증가");
            PlayerManager scenePlayer = Object.FindObjectsOfType<PlayerManager>().FirstOrDefault(candidate => candidate != player);
            Require(scenePlayer != null && typeof(PlayerManager).GetField("skillDataManager", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(scenePlayer) != null, "GameScene 플레이어의 SkillDataManager 의존성 실제 주입");

            ChainIgnitionSkillData probeData = Object.Instantiate(data);
            runtimeData.Add(probeData);
            probeData.explosionSound = null;
            ChainIgnitionWave probe = Object.Instantiate(data.wavePrefab);
            Track(probe.gameObject);
            Vector3 probeCenter = Center + Vector3.right * 100f;
            UnityEngine.Random.State previousRandom = UnityEngine.Random.state;
            try
            {
                UnityEngine.Random.InitState(12345);
                probe.Begin(probeData, probeCenter, Vector2.left + Vector2.down, 1, 10f);
                Require(probe.GetComponentsInChildren<SpriteRenderer>().Where(renderer => renderer.enabled).Single().name == "Explosion_1_6",
                    "왼쪽 아래 마우스 방향과 가까운 고정 8방향 중 하나 선택");
                HashSet<int> tiedChoices = new HashSet<int>();
                bool validTwoDirections = true;
                bool validTies = true;
                for (int iteration = 0; iteration < 64; iteration++)
                {
                    probe.Begin(probeData, probeCenter, Vector2.right, 2, 10f);
                    SpriteRenderer[] renderers = probe.GetComponentsInChildren<SpriteRenderer>();
                    validTwoDirections &= renderers.Count(renderer => renderer.enabled) == 2 && renderers[0].enabled &&
                        (renderers[1].enabled || renderers[7].enabled);
                    float angle = 22.5f * Mathf.Deg2Rad;
                    probe.Begin(probeData, probeCenter, new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)), 1, 10f);
                    renderers = probe.GetComponentsInChildren<SpriteRenderer>();
                    int chosen = Array.FindIndex(renderers, renderer => renderer.enabled);
                    validTies &= chosen == 0 || chosen == 1;
                    tiedChoices.Add(chosen);
                }
                Require(validTwoDirections, "두 개면 마우스에 가장 가까운 방향과 그다음 가까운 방향을 중복 없이 선택");
                Require(validTies && tiedChoices.Count == 2, "거리가 같은 두 방향은 무작위로 양쪽 모두 선택");
                bool validAllCounts = true;
                for (int count = 1; count <= 8; count++)
                {
                    probe.Begin(probeData, probeCenter, Vector2.up, count, 10f);
                    validAllCounts &= probe.GetComponentsInChildren<SpriteRenderer>().Count(renderer => renderer.enabled) == count;
                }
                Require(validAllCounts, "1~8개 모든 단계에서 고정 방향을 중복 없이 채움");
                probe.gameObject.SetActive(false);
            }
            finally { UnityEngine.Random.state = previousRandom; }
        }

        private static GameObject Track(GameObject obj) { objects.Add(obj); return obj; }

        private static ChainIgnitionVerificationTarget Target(string name, Vector3 position, float radius, int layer = -1)
        {
            GameObject obj = Track(new GameObject("[ChainIgnitionVerification] " + name));
            obj.transform.position = position;
            obj.layer = layer >= 0 ? layer : LayerMask.NameToLayer("Enemy");
            obj.AddComponent<CircleCollider2D>().radius = radius;
            return obj.AddComponent<ChainIgnitionVerificationTarget>();
        }

        private static void Require(bool condition, string name)
        {
            if (!condition) throw new InvalidOperationException(name);
            checks.Add("PASS " + name);
            File.AppendAllText(Output + "/verification-progress.txt", "PASS " + name + "\n");
        }

        private static void Capture(string filename)
        {
            GameObject cameraObject = new GameObject("[ChainIgnitionVerification] 렌더 카메라");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.enabled = false;
            camera.orthographic = true;
            camera.orthographicSize = 5.3f;
            camera.transform.position = Center + Vector3.back * 10f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.025f, 0.035f, 0.065f);
            RenderTexture texture = new RenderTexture(640, 640, 24);
            Texture2D image = new Texture2D(640, 640, TextureFormat.RGB24, false);
            RenderTexture previous = RenderTexture.active;
            try
            {
                camera.targetTexture = texture;
                camera.Render();
                RenderTexture.active = texture;
                image.ReadPixels(new Rect(0, 0, 640, 640), 0, 0);
                image.Apply();
                File.WriteAllBytes(Output + "/" + filename, image.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                camera.targetTexture = null;
                Object.DestroyImmediate(image);
                Object.DestroyImmediate(texture);
                Object.DestroyImmediate(cameraObject);
            }
        }

        private static void Finish(Exception error)
        {
            Directory.CreateDirectory(Output);
            File.WriteAllText(Output + "/verification.txt", "Unity " + Application.unityVersion + " / " + SceneManager.GetActiveScene().name +
                " / PlayMode=" + EditorApplication.isPlaying + "\n" + string.Join("\n", checks) + "\n" +
                (error == null ? "RESULT: PASS" : "RESULT: FAIL\n" + error));
            if (error != null) Debug.LogException(error);
            else Debug.Log("[ChainIgnitionSkillVerification] 모든 검증을 통과했습니다.");
            Cleanup();
            SessionState.SetBool(Pending, false);
            if (exitAfter) EditorApplication.isPlaying = false;
        }

        private static void Cleanup()
        {
            foreach (GameObject obj in objects) if (obj != null) Object.DestroyImmediate(obj);
            objects.Clear();
            foreach (ScriptableObject obj in runtimeData) if (obj != null) Object.DestroyImmediate(obj);
            runtimeData.Clear();
            skill = null;
            data = null;
            crowdedTargets.Clear();
            Time.timeScale = previousTimeScale;
        }
    }

    public class ChainIgnitionVerificationTarget : MonoBehaviour, IDamageable
    {
        public int Hits { get; private set; }
        public float Damage { get; private set; }
        public void TakeDamage(float damageAmount, bool isChain = false) { Hits++; Damage += damageAmount; }
    }
}
