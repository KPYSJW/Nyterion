using System.Collections.Generic;
using Nytherion.Core.Managers;
using Nytherion.Data.ScriptableObjects.Skill;
using Nytherion.GamePlay.Characters.Player;
using UnityEngine;

namespace Nytherion.GamePlay.Skills
{
    /// <summary>플레이어 중심을 따라가는 실드. 피해 흡수는 PlayerHealth에서 처리한다.</summary>
    [DefaultExecutionOrder(1100)] // 무기 정렬 갱신(1000) 이후 실드의 표시 순서를 결정한다.
    public sealed class BubbleShieldSkill : SkillBase
    {
        public enum ShieldPhase { Inactive, Summon, Sustained, Crash }

        public ShieldPhase Phase { get; private set; }
        private PlayerHealth health;
        private SpriteRenderer ownerRenderer;
        private PlayerCombat ownerCombat;
        private readonly List<SpriteRenderer> weaponRenderers = new List<SpriteRenderer>();
        private readonly List<BubbleShieldHitEffect> hitEffects = new List<BubbleShieldHitEffect>();
        private SpriteRenderer shieldRenderer;
        private BubbleShieldSkillData activeData;
        private float phaseStartedAt;

        public override bool CanUse() => base.CanUse() && caster != null &&
            caster.gameObject.activeInHierarchy && caster.TryGetComponent(out PlayerHealth target) &&
            target.isActiveAndEnabled && target.CurrentHealth > 0f &&
            target.CurrentShield <= 0f && skillData is BubbleShieldSkillData data &&
            data.shieldSprite != null && data.summonFrames != null && data.summonFrames.Length > 0 &&
            data.crashFrames != null && data.crashFrames.Length > 0;

        protected override void Activate()
        {
            Unsubscribe();
            health = caster.GetComponent<PlayerHealth>();
            ownerRenderer = caster.GetComponentInChildren<SpriteRenderer>();
            ownerCombat = caster.GetComponent<PlayerCombat>();
            activeData = (BubbleShieldSkillData)skillData;
            if (shieldRenderer == null)
            {
                GameObject visual = new GameObject("BubbleShield Visual");
                visual.transform.SetParent(transform, false);
                shieldRenderer = visual.AddComponent<SpriteRenderer>();
            }
            shieldRenderer.enabled = true;
            shieldRenderer.color = activeData.shieldColor;
            health.ShieldChanged += HandleShieldChanged;
            health.ShieldHit += PlayHitEffect;
            PlayerManager player = caster.GetComponentInParent<PlayerManager>();
            int level = player != null ? player.GetSkillLevel(activeData) : Mathf.Max(1, activeData.skillLevel);
            SetPhase(ShieldPhase.Summon);
            // 생성 연출의 첫 프레임부터 피해를 막는다. 지속시간은 생성 완료 후부터 계산한다.
            health.ApplyShield(activeData.GetShield(level), this, activeData.shieldBarSprite);
            shieldRenderer.sprite = activeData.summonFrames[0];
            FollowOwner();
        }

        private void HandleShieldChanged(float current, float max)
        {
            if (current <= 0f && (Phase == ShieldPhase.Summon || Phase == ShieldPhase.Sustained))
                SetPhase(ShieldPhase.Crash);
        }

        private void PlayHitEffect(Vector2 hitPoint)
        {
            if (activeData.hitEffectPrefab == null || shieldRenderer == null ||
                (Phase != ShieldPhase.Summon && Phase != ShieldPhase.Sustained)) return;
            FollowOwner();
            Vector2 direction = hitPoint - (Vector2)shieldRenderer.transform.position;
            if (direction.sqrMagnitude < 0.0001f) direction = Vector2.up;
            BubbleShieldHitEffect effect = null;
            foreach (BubbleShieldHitEffect candidate in hitEffects)
            {
                if (candidate != null && !candidate.gameObject.activeSelf) { effect = candidate; break; }
            }
            if (effect == null)
            {
                effect = Instantiate(activeData.hitEffectPrefab, transform);
                hitEffects.Add(effect);
            }
            effect.Play(shieldRenderer, direction.normalized, activeData.hitSurfaceRadius, activeData.hitEffectScale);
        }

        private void SetPhase(ShieldPhase phase)
        {
            Phase = phase;
            phaseStartedAt = Time.time;
            if (phase == ShieldPhase.Crash && shieldRenderer != null)
                shieldRenderer.sprite = activeData.crashFrames[0];
        }

        private void Update()
        {
            if (Phase == ShieldPhase.Inactive) return;
            if (caster == null || !caster.gameObject.activeInHierarchy || health == null ||
                !health.isActiveAndEnabled || health.CurrentHealth <= 0f)
            { Close(); return; }

            float elapsed = Time.time - phaseStartedAt;
            float fps = Mathf.Max(1f, activeData.framesPerSecond);
            if (Phase == ShieldPhase.Summon)
            {
                int index = Mathf.FloorToInt(elapsed * fps);
                if (index >= activeData.summonFrames.Length)
                {
                    SetPhase(ShieldPhase.Sustained);
                    shieldRenderer.sprite = activeData.shieldSprite;
                }
                else shieldRenderer.sprite = activeData.summonFrames[index];
            }
            else if (Phase == ShieldPhase.Sustained && elapsed >= activeData.duration)
                health.ClearShield(this);
            else if (Phase == ShieldPhase.Crash)
            {
                int index = Mathf.FloorToInt(elapsed * fps);
                if (index >= activeData.crashFrames.Length) Close();
                else shieldRenderer.sprite = activeData.crashFrames[index];
            }
        }

        private void LateUpdate()
        {
            if (Phase != ShieldPhase.Inactive) FollowOwner();
        }

        private void FollowOwner()
        {
            if (shieldRenderer == null || caster == null) return;
            shieldRenderer.transform.position = ownerRenderer != null ? ownerRenderer.bounds.center : caster.position;
            shieldRenderer.transform.rotation = Quaternion.identity;
            float diameter = ownerRenderer != null
                ? Mathf.Max(ownerRenderer.bounds.size.x, ownerRenderer.bounds.size.y) : 1f;
            float spriteSize = Mathf.Max(0.01f, activeData.shieldSprite.bounds.size.x);
            // 부모의 반전이나 확대·축소에 관계없이 구형 비율을 유지한다.
            Vector3 parentScale = transform.lossyScale;
            float size = Mathf.Max(0.5f, diameter) * activeData.sizeMultiplier / spriteSize;
            shieldRenderer.transform.localScale = new Vector3(
                size / Mathf.Max(0.001f, Mathf.Abs(parentScale.x)),
                size / Mathf.Max(0.001f, Mathf.Abs(parentScale.y)), 1f);
            if (ownerRenderer != null)
            {
                shieldRenderer.sortingLayerID = ownerRenderer.sortingLayerID;
                int foregroundOrder = ownerRenderer.sortingOrder;
                if (ownerCombat != null && ownerCombat.currentWeapon != null)
                {
                    ownerCombat.currentWeapon.GetComponentsInChildren(true, weaponRenderers);
                    foreach (SpriteRenderer weaponRenderer in weaponRenderers)
                        foregroundOrder = Mathf.Max(foregroundOrder, weaponRenderer.sortingOrder);
                }
                shieldRenderer.sortingOrder = foregroundOrder + 2;
            }
        }

        private void Close()
        {
            Unsubscribe();
            foreach (BubbleShieldHitEffect effect in hitEffects)
                if (effect != null) effect.gameObject.SetActive(false);
            if (health != null) health.ClearShield(this);
            Phase = ShieldPhase.Inactive;
            if (shieldRenderer != null) shieldRenderer.enabled = false;
        }

        private void Unsubscribe()
        {
            if (health != null)
            {
                health.ShieldChanged -= HandleShieldChanged;
                health.ShieldHit -= PlayHitEffect;
            }
        }

        private void OnDisable() => Close();
        private void OnDestroy() => Unsubscribe();
    }
}
