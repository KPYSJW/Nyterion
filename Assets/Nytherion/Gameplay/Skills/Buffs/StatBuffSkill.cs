using Nytherion.Core.Managers;
using Nytherion.Data.ScriptableObjects.Skill;
using Nytherion.Core.Data;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Nytherion.GamePlay.Combat;

namespace Nytherion.GamePlay.Skills
{
    /// <summary>
    /// 일정 시간 동안 플레이어의 지정된 스탯들을 상승시키는 버프 스킬
    /// </summary>
    [DefaultExecutionOrder(1100)]
    public class StatBuffSkill : SkillBase
    {
        private StatBuffSkillData statBuffData;
        private PlayerManager playerManager;
        private Coroutine activeBuffCoroutine;
        private bool isBuffApplied = false;
        private readonly SpriteBodyBounds bodyBounds = new SpriteBodyBounds();
        private SpriteRenderer activationRenderer;
        private SpriteRenderer playerBody;
        private float activationElapsed;

        // 적용된 버프 정보를 추적하여 나중에 제거할 때 사용
        private List<StatModifier> appliedModifiers = new List<StatModifier>();

        private void Start()
        {
            playerManager = SkillOwner;
        }

        protected override void Activate()
        {
            if (skillData != null)
            {
                statBuffData = skillData as StatBuffSkillData;
                if (statBuffData == null)
                {
                    Debug.LogError("[StatBuffSkill] SkillData가 StatBuffSkillData 타입이 아닙니다.");
                    return;
                }

                if (playerManager == null) playerManager = SkillOwner;
                if (playerManager != null)
                {
                    // 버프 시간 갱신 처리를 위해 기존 코루틴 정지
                    if (activeBuffCoroutine != null)
                    {
                        StopCoroutine(activeBuffCoroutine);
                    }
                    activeBuffCoroutine = StartCoroutine(BuffRoutine());
                    PlayActivationEffect();
                }
                else
                {
                    Debug.LogError("[StatBuffSkill] PlayerManager를 찾을 수 없습니다.");
                }
            }
        }

        private void PlayActivationEffect()
        {
            if (statBuffData.activationFrames == null || statBuffData.activationFrames.Length == 0) return;
            Transform owner = caster != null ? caster : playerManager.transform;
            playerBody = owner.GetComponent<SpriteRenderer>();
            if (playerBody == null) playerBody = owner.GetComponentInChildren<SpriteRenderer>();
            if (activationRenderer == null)
            {
                GameObject visual = new GameObject("StatBuffActivationVFX");
                visual.transform.SetParent(transform, false);
                activationRenderer = visual.AddComponent<SpriteRenderer>();
            }
            activationElapsed = 0f;
            activationRenderer.enabled = true;
            UpdateActivationEffect();
        }

        private void LateUpdate()
        {
            if (activationRenderer == null || !activationRenderer.enabled) return;
            activationElapsed += Time.deltaTime;
            UpdateActivationEffect();
        }

        private void UpdateActivationEffect()
        {
            int frame = Mathf.FloorToInt(activationElapsed * Mathf.Max(1f, statBuffData.activationFramesPerSecond));
            if (frame >= statBuffData.activationFrames.Length)
            {
                activationRenderer.enabled = false;
                return;
            }

            Transform owner = caster != null ? caster : playerManager.transform;
            Vector3 feet = owner.position;
            if (playerBody != null && playerBody.sprite != null)
            {
                Bounds bounds = bodyBounds.GetBounds(playerBody);
                feet = new Vector3(bounds.center.x, bounds.min.y, owner.position.z);
                SortActivationEffectAboveWeapon(activationRenderer, playerBody);
            }
            Transform visual = activationRenderer.transform;
            visual.position = feet + (Vector3)statBuffData.activationFeetOffset;
            // 좌우 반전과 이동 방향에 관계없이 빛은 발밑에서 수직으로 올라갑니다.
            visual.rotation = Quaternion.identity;
            Vector3 parentScale = transform.lossyScale;
            float scale = Mathf.Max(0.01f, statBuffData.activationScale);
            visual.localScale = new Vector3(scale / SafeScale(parentScale.x), scale / SafeScale(parentScale.y), 1f);
            activationRenderer.sprite = statBuffData.activationFrames[frame];
        }

        private static float SafeScale(float value) => Mathf.Abs(value) < 0.001f ? 0.001f : value;

        private void OnDisable()
        {
            if (activeBuffCoroutine != null) StopCoroutine(activeBuffCoroutine);
            activeBuffCoroutine = null;
            if (activationRenderer != null) activationRenderer.enabled = false;
            RemoveBuffs();
        }

        /// <summary>
        /// 스탯 보넛스를 적용하고 지속 시간 후 원상복구하는 코루틴
        /// </summary>
        private IEnumerator BuffRoutine()
        {
            // 버프가 적용되어 있지 않은 상태라면 새로 적용
            if (!isBuffApplied)
            {
                appliedModifiers.Clear();

                // 데이터에 정의된 모든 스탯 버프를 순회하며 최종 버프 값을 계산하여 적용
                foreach (StatModifier modifier in statBuffData.statModifiers)
                {
                    // 스킬 레벨에 비례하여 증가치 계산
                    float finalValue = modifier.value + (modifier.valuePerLevel * Mathf.Max(0, playerManager.GetSkillLevel(statBuffData) - 1));
                    
                    // 적용할 모디파이어 인스턴스 생성
                    StatModifier scaledModifier = new StatModifier {
                        stat = modifier.stat,
                        value = finalValue,
                        valuePerLevel = modifier.valuePerLevel,
                        isPercentage = modifier.isPercentage
                    };

                    // 해제용 리스트에 보관 및 플레이어에게 실제 스탯 적용
                    appliedModifiers.Add(scaledModifier);
                    playerManager.AddTemporaryStatModifier(scaledModifier);
                }
                isBuffApplied = true;
                Debug.Log($"[StatBuffSkill] 스탯 버프 활성화! (지속시간: {statBuffData.buffDuration}초, 스킬레벨: {statBuffData.skillLevel})");
            }
            else
            {
                // 이미 버프가 걸려있다면 새로 중첩시키지 않고 지속 시간만 갱신
                Debug.Log($"[StatBuffSkill] 스탯 버프 지속시간 갱신!");
            }

            // 지속 시간 대기
            yield return new WaitForSeconds(statBuffData.buffDuration);

            RemoveBuffs();
        }

        /// <summary>
        /// 적용되었던 모든 임시 스탯 버프를 제거하여 플레이어의 스탯을 원상복구
        /// </summary>
        private void RemoveBuffs()
        {
            if (isBuffApplied && playerManager != null)
            {
                foreach (StatModifier modifier in appliedModifiers)
                {
                    playerManager.RemoveTemporaryStatModifier(modifier);
                }
                appliedModifiers.Clear();
                isBuffApplied = false;
                Debug.Log("[StatBuffSkill] 스탯 버프 종료!");
            }
        }

        private void OnDestroy()
        {
            // 스킬 오브젝트 파괴 시 스탯이 영구적으로 남는것을 방지
            if (isBuffApplied)
            {
                RemoveBuffs();
            }
        }
    }
}
