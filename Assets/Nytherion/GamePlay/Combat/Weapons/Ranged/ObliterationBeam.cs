using System.Collections.Generic;
using Nytherion.Core.Interfaces;
using Nytherion.Core.Managers;
using Nytherion.Data.ScriptableObjects.Weapons;
using Nytherion.GamePlay.Skills;
using UnityEngine;

namespace Nytherion.GamePlay.Combat
{
    /// <summary>원본 스프라이트 광선의 재생, 단계별 판정과 풀 수명을 관리합니다.</summary>
    public class ObliterationBeam : MonoBehaviour, IProjectileGateBeam
    {
        [SerializeField] private SpriteRenderer beamRenderer;
        [SerializeField] private SpriteRenderer startEffectRenderer;
        [SerializeField] private SpriteRenderer endEffectRenderer;
        private readonly List<Collider2D> overlaps = new List<Collider2D>(32);
        private readonly List<RaycastHit2D> obstructions = new List<RaycastHit2D>(8);
        private readonly HashSet<IDamageable> hitTargets = new HashSet<IDamageable>();
        private ObliterationWeapon owner;
        private ObliterationWeaponData data;
        private Transform origin;
        private ObjectPoolManager pool;
        private string poolTag;
        private Vector2 direction;
        private ContactFilter2D targetFilter;
        private ContactFilter2D obstructionFilter;
        private float damage;
        private float maxLength;
        private float width;
        private float elapsed;
        private int nextTick;
        private readonly ProjectileGateBeamPath<ObliterationBeam> gatePath = new ProjectileGateBeamPath<ObliterationBeam>();
        private ObliterationBeam gateSource;
        private Vector3 gateStart;
        private Vector2 gateDirection;
        private Vector3 BeamStart => gateSource != null ? gateStart : origin.position;

        public bool IsFiring { get; private set; }
        public float CurrentLength { get; private set; }
        public float CurrentWidth => width;

        public void Initialize(ObliterationWeapon owner, Transform origin, Vector2 direction,
            ObliterationWeaponData data, int stage, float damage, ObjectPoolManager pool)
        {
            gatePath.Clear();
            gateSource = null;
            this.owner = owner;
            this.origin = origin;
            this.direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
            this.data = data;
            this.damage = Mathf.Max(0f, damage);
            this.pool = pool;
            poolTag = data.projectilePrefab.name;
            maxLength = data.GetLength(stage);
            width = data.GetWidth(stage) * owner.EffectSizeMultiplier;
            elapsed = 0f;
            nextTick = 0;
            overlaps.Clear();
            obstructions.Clear();
            hitTargets.Clear();
            targetFilter = new ContactFilter2D { useTriggers = true };
            targetFilter.SetLayerMask(data.targetLayers);
            obstructionFilter = new ContactFilter2D { useTriggers = false };
            obstructionFilter.SetLayerMask(data.obstructionLayers);
            IsFiring = true;

            SpriteRenderer source = owner.GetComponent<SpriteRenderer>();
            if (source != null)
            {
                beamRenderer.sortingLayerID = source.sortingLayerID;
                beamRenderer.sortingOrder = WeaponVFXHelper.BackgroundBeamSortingOrder;
                PrepareEndpointRenderer(startEffectRenderer, source.sortingLayerID, WeaponVFXHelper.BackgroundBeamSortingOrder + 1);
            }
            PrepareEndpointRenderer(endEffectRenderer, beamRenderer.sortingLayerID,
                Mathf.Max(WeaponVFXHelper.HitEffectSortingOrder, beamRenderer.sortingOrder + 1));
            WeaponVFXHelper.ConfigureBeamSorting(gameObject, beamRenderer, startEffectRenderer, endEffectRenderer);
            beamRenderer.enabled = true;
            UpdateGeometry();
            UpdateVisual();
        }

        private void LateUpdate()
        {
            if (gateSource != null) return;
            Advance(Time.deltaTime);
        }

        private void Advance(float deltaTime)
        {
            if (!IsFiring) return;
            if (owner == null || !owner.isActiveAndEnabled || origin == null || !origin.gameObject.activeInHierarchy)
            {
                StopImmediately();
                return;
            }
            if (deltaTime <= 0f) return;
            elapsed += deltaTime;
            UpdateGeometry();

            // 정수 틱 번호를 사용해 프레임이 지연되어도 피해 횟수를 유지합니다.
            double tickTime = data.DamageStartTime + nextTick * (double)Mathf.Max(0.01f, data.tickInterval);
            while (tickTime < data.DamageEndTime - 0.000001d && tickTime <= elapsed + 0.000001d)
            {
                nextTick++;
                PerformDamageTick();
                if (!IsFiring) return;
                tickTime = data.DamageStartTime + nextTick * (double)Mathf.Max(0.01f, data.tickInterval);
            }
            if (elapsed >= data.BeamDuration)
            {
                StopImmediately();
                return;
            }
            UpdateVisual();
        }

        private void UpdateGeometry()
        {
            // PlayerCombat.Update에서 회전한 무기의 현재 조준축을 같은 프레임에 따라갑니다.
            Vector2 currentDirection = gateSource != null ? gateDirection : owner.CurrentFireDirection;
            if (currentDirection.sqrMagnitude > 0.0001f)
                direction = currentDirection.normalized;
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            transform.SetPositionAndRotation(BeamStart, Quaternion.Euler(0f, 0f, angle));
            CurrentLength = maxLength;
            obstructions.Clear();
            Physics2D.BoxCast(BeamStart, new Vector2(0.001f, width), angle,
                direction, obstructionFilter, obstructions, maxLength);
            foreach (RaycastHit2D hit in obstructions)
            {
                if (hit.collider != null && !hit.collider.transform.IsChildOf(owner.CasterTransform))
                    CurrentLength = Mathf.Min(CurrentLength, hit.distance);
            }
            if (gateSource == null)
            {
                CurrentLength = gatePath.Update(owner.CasterTransform, BeamStart, direction, CurrentLength,
                    data.projectilePrefab, pool);
                for (int i = 0; i < gatePath.Count; i++)
                    gatePath.GetBranch(i).UpdateGateBranch(this, gatePath.GetStart(i), gatePath.GetDirection(i),
                        gatePath.SizeMultiplier);
            }
        }

        private void UpdateGateBranch(ObliterationBeam source, Vector3 start, Vector2 outputDirection, float scale)
        {
            bool firstUse = gateSource != source;
            gateSource = source;
            owner = source.owner;
            origin = source.origin;
            data = source.data;
            pool = source.pool;
            poolTag = source.poolTag;
            gateStart = start;
            gateDirection = outputDirection;
            damage = source.damage;
            maxLength = source.maxLength;
            width = source.width * scale;
            elapsed = source.elapsed;
            targetFilter = source.targetFilter;
            obstructionFilter = source.obstructionFilter;
            IsFiring = source.IsFiring;
            beamRenderer.sortingLayerID = source.beamRenderer.sortingLayerID;
            beamRenderer.sortingOrder = source.beamRenderer.sortingOrder;
            if (firstUse)
            {
                PrepareEndpointRenderer(endEffectRenderer, beamRenderer.sortingLayerID,
                    Mathf.Max(WeaponVFXHelper.HitEffectSortingOrder, beamRenderer.sortingOrder + 1));
                WeaponVFXHelper.ConfigureBeamSorting(gameObject, beamRenderer, startEffectRenderer, endEffectRenderer);
            }
            UpdateGeometry();
            UpdateVisual();
        }

        private void UpdateVisual()
        {
            int sequenceFrame = Mathf.Min(data.BeamSequenceLength - 1,
                Mathf.FloorToInt(elapsed * Mathf.Max(1f, data.beamFramesPerSecond)));
            int frameIndex = data.GetBeamFrameIndex(sequenceFrame);
            Sprite beamSprite = data.beamFrames[frameIndex];
            Sprite endpointSprite = data.startEndFrames[frameIndex];
            beamRenderer.sprite = beamSprite;
            beamRenderer.enabled = CurrentLength > 0.001f;
            // 원본은 세로 광선입니다. -90도 돌려 +X로 뻗게 하고 피벗에 관계없이 총구에 맞춥니다.
            Vector3 size = beamSprite.bounds.size;
            Vector3 scale = new Vector3(width / (Mathf.Max(0.01f, size.x) *
                Mathf.Max(0.01f, data.beamOpaqueWidthRatio)), CurrentLength / Mathf.Max(0.01f, size.y), 1f);
            Quaternion rotation = Quaternion.Euler(0f, 0f, -90f);
            beamRenderer.transform.localRotation = rotation;
            beamRenderer.transform.localScale = scale;
            beamRenderer.transform.localPosition = Vector3.right * (CurrentLength * 0.5f) -
                rotation * Vector3.Scale(beamSprite.bounds.center, scale);

            // 시작/끝 이미지는 광선과 같은 프레임 인덱스를 사용하고 현재 단계의 폭에 맞춰 함께 커집니다.
            float endpointScale = width / (Mathf.Max(0.01f, endpointSprite.bounds.size.y) *
                Mathf.Max(0.01f, data.beamOpaqueWidthRatio));
            UpdateEndpointRenderer(startEffectRenderer, endpointSprite, Vector3.zero,
                Quaternion.identity, endpointScale, beamRenderer.enabled && gateSource == null);
            UpdateEndpointRenderer(endEffectRenderer, endpointSprite, Vector3.right * CurrentLength,
                Quaternion.Euler(0f, 0f, 180f), endpointScale, beamRenderer.enabled && !gatePath.HasGate);
        }

        private static void PrepareEndpointRenderer(SpriteRenderer renderer, int sortingLayerId, int sortingOrder)
        {
            if (renderer == null) return;
            renderer.sortingLayerID = sortingLayerId;
            renderer.sortingOrder = sortingOrder;
            renderer.color = Color.white;
            renderer.enabled = true;
        }

        private static void UpdateEndpointRenderer(SpriteRenderer renderer, Sprite sprite,
            Vector3 localPosition, Quaternion localRotation, float scale, bool visible)
        {
            if (renderer == null) return;
            renderer.sprite = sprite;
            renderer.enabled = visible;
            renderer.transform.localPosition = localPosition;
            renderer.transform.localRotation = localRotation;
            renderer.transform.localScale = new Vector3(scale, scale, 1f);
        }

        private void PerformDamageTick()
        {
            if (CurrentLength <= 0f) return;
            overlaps.Clear();
            hitTargets.Clear();
            Vector2 center = (Vector2)BeamStart + direction * (CurrentLength * 0.5f);
            Physics2D.OverlapBox(center, new Vector2(CurrentLength, width), transform.eulerAngles.z,
                targetFilter, overlaps);
            foreach (Collider2D hit in overlaps)
            {
                if (hit == null || !hit.gameObject.activeInHierarchy || hit.transform.IsChildOf(owner.CasterTransform)) continue;
                IDamageable target = hit.GetComponentInParent<IDamageable>();
                if (!(target is MonoBehaviour behaviour) || !behaviour.isActiveAndEnabled || !hitTargets.Add(target)) continue;
                // 사망으로 콜라이더가 비활성화되기 전에 중심 근처에 타격 애니메이션을 재생합니다.
                Vector2 hitOffset = Random.insideUnitCircle * Mathf.Max(0f, data.hitEffectOffsetRadius);
                Vector3 hitPosition = hit.bounds.center + new Vector3(hitOffset.x, hitOffset.y, 0f);
                WeaponVFXHelper.PlayHitEffect(data.hitEffectPrefab, hitPosition);
                owner.DealAttackDamage(target, damage);
                if (!IsFiring || owner == null) return;
                if (behaviour != null && behaviour.isActiveAndEnabled) owner.ApplyStatusEffects(target);
                if (!IsFiring || owner == null) return;
            }
            for (int i = 0; i < gatePath.Count; i++)
                gatePath.GetBranch(i).PerformDamageTick();
        }

        public void StopImmediately()
        {
            if (!IsFiring) return;
            ResetState();
            if (pool != null && !string.IsNullOrEmpty(poolTag)) pool.ReturnToPool(poolTag, gameObject);
            else
            {
                gameObject.SetActive(false);
                Destroy(gameObject);
            }
        }

        private void ResetState()
        {
            gatePath.Clear();
            IsFiring = false;
            if (owner != null && gateSource == null) owner.OnBeamReleased(this);
            owner = null;
            origin = null;
            gateSource = null;
            if (beamRenderer != null) beamRenderer.enabled = false;
            if (startEffectRenderer != null) startEffectRenderer.enabled = false;
            if (endEffectRenderer != null) endEffectRenderer.enabled = false;
            overlaps.Clear();
            obstructions.Clear();
            hitTargets.Clear();
        }

        private void OnDisable()
        {
            ResetState();
        }
    }
}
