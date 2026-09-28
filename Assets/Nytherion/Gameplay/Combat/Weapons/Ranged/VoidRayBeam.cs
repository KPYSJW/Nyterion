using System.Collections.Generic;
using Nytherion.Core.Interfaces;
using Nytherion.Core.Managers;
using Nytherion.Data.ScriptableObjects.Weapons;
using Nytherion.GamePlay.Characters.Enemy;
using UnityEngine;
using UnityEngine.Rendering;

namespace Nytherion.GamePlay.Combat
{
    /// <summary>지속 광선의 좌표 계산, 충돌 필터링, 피해 주기와 풀 수명만 관리합니다.</summary>
    public class VoidRayBeam : MonoBehaviour
    {
        private VoidRayBeamVisual visual;

        private readonly List<RaycastHit2D> raycastHits = new List<RaycastHit2D>(64);
        private readonly Dictionary<Collider2D, IDamageable> damageableCache =
            new Dictionary<Collider2D, IDamageable>(32);
        private readonly List<Collider2D> staleCachedColliders = new List<Collider2D>(8);
        private readonly List<TargetHit> targetCandidates = new List<TargetHit>(16);
        private readonly List<TargetHit> currentTargets = new List<TargetHit>(3);
        private readonly List<Collider2D> chainOverlaps = new List<Collider2D>(32);
        private readonly List<Vector2> targetPoints = new List<Vector2>(3);

        private VoidRayWeapon owner;
        private VoidRayWeaponData data;
        private Transform origin;
        private ObjectPoolManager pool;
        private string poolTag;
        private ContactFilter2D collisionFilter;
        private ContactFilter2D targetFilter;
        private Vector2 direction = Vector2.right;
        private Vector2 muzzleDirection = Vector2.right;
        private Vector2 startPoint;
        private Vector2 endPoint;
        private float elapsed;
        private float fadeRemaining;
        private float visualAlpha = 1f;
        private bool connectedVisual;

        public bool IsFiring { get; private set; }
        public bool IsFading { get; private set; }
        public bool HasHit { get; private set; }
        public bool HasTargetHit => IsFiring && HasValidTarget();
        public int CurrentTargetCount => IsFiring ? CountValidTargets() : 0;
        public float CurrentLength { get; private set; }
        public Vector2 StartPoint => startPoint;
        public Vector2 EndPoint => endPoint;

        private void Awake()
        {
            EnsureVisual();
            visual.Hide();
        }

        public void Initialize(VoidRayWeapon owner, Transform origin, VoidRayWeaponData data,
            ObjectPoolManager pool)
        {
            this.owner = owner;
            this.origin = origin;
            this.data = data;
            this.pool = pool;
            poolTag = data.projectilePrefab != null ? data.projectilePrefab.name : gameObject.name;
            elapsed = 0f;
            fadeRemaining = 0f;
            visualAlpha = 1f;
            IsFiring = true;
            IsFading = false;
            HasHit = false;
            connectedVisual = false;
            raycastHits.Clear();
            targetCandidates.Clear();
            chainOverlaps.Clear();
            targetPoints.Clear();
            damageableCache.Clear();
            ClearCurrentTargets();

            collisionFilter = new ContactFilter2D { useTriggers = true };
            collisionFilter.SetLayerMask(data.targetLayers | data.obstructionLayers);
            targetFilter = new ContactFilter2D { useTriggers = true };
            targetFilter.SetLayerMask(data.targetLayers);

            EnsureVisual();
            visual.Initialize(owner.GetComponent<SpriteRenderer>(), data);
            UpdateGeometry();
            ProcessDamageTicks(Time.timeAsDouble);
            UpdateVisual();
        }

        private void LateUpdate()
        {
            if (IsFiring)
            {
                if (owner == null || !owner.isActiveAndEnabled || origin == null ||
                    !origin.gameObject.activeInHierarchy)
                {
                    StopImmediately();
                    return;
                }

                elapsed += Mathf.Max(0f, Time.deltaTime);
                UpdateGeometry();
                ProcessDamageTicks(Time.timeAsDouble);
                UpdateVisual();
                return;
            }

            if (!IsFading) return;
            elapsed += Mathf.Max(0f, Time.deltaTime);
            fadeRemaining -= Mathf.Max(0f, Time.deltaTime);
            float duration = data != null ? Mathf.Max(0f, data.visualFadeDuration) : 0f;
            visualAlpha = duration > 0f ? Mathf.Clamp01(fadeRemaining / duration) : 0f;
            if (visualAlpha <= 0f)
            {
                FinishAndRelease();
                return;
            }
            UpdateVisual();
        }

        private void UpdateGeometry()
        {
            ClearCurrentTargets();
            targetPoints.Clear();
            if (owner == null || origin == null || data == null) return;

            Vector2 currentDirection = owner.CurrentFireDirection;
            if (currentDirection.sqrMagnitude > 0.0001f)
                direction = currentDirection.normalized;
            Vector2 currentMuzzleDirection = owner.CurrentMuzzleDirection;
            if (currentMuzzleDirection.sqrMagnitude > 0.0001f)
                muzzleDirection = currentMuzzleDirection.normalized;

            startPoint = origin.position;
            float obstructionDistance = data.MaxRange;
            Collider2D obstructionCollider = null;

            raycastHits.Clear();
            targetCandidates.Clear();
            Physics2D.Raycast(startPoint, direction, collisionFilter, raycastHits, data.MaxRange);
            for (int i = 0; i < raycastHits.Count; i++)
            {
                RaycastHit2D hit = raycastHits[i];
                Collider2D hitCollider = hit.collider;
                if (hitCollider == null || IsCasterCollider(hitCollider)) continue;

                bool targetLayer = IsInLayerMask(hitCollider.gameObject.layer, data.targetLayers);
                bool obstructionLayer = IsInLayerMask(hitCollider.gameObject.layer, data.obstructionLayers);
                IDamageable hitTarget = null;
                MonoBehaviour hitTargetBehaviour = null;

                if (targetLayer)
                {
                    hitTarget = ResolveDamageable(hitCollider);
                    hitTargetBehaviour = hitTarget as MonoBehaviour;
                    if (!IsTargetAlive(hitTarget, hitTargetBehaviour))
                        continue;
                    AddOrUpdateTargetCandidate(hitCollider, hitTarget, hitTargetBehaviour, hit.distance);
                    continue;
                }

                if (!obstructionLayer || hitCollider.isTrigger)
                {
                    // 적 피격 Trigger만 허용하고 그 밖의 Trigger/레이어는 광선을 막지 않습니다.
                    continue;
                }

                if (hit.distance > obstructionDistance) continue;
                obstructionDistance = Mathf.Max(0f, hit.distance);
                obstructionCollider = hitCollider;
            }

            targetCandidates.Sort(CompareTargetHits);
            TargetHit firstTarget = default;
            bool foundFirstTarget = false;
            if (targetCandidates.Count > 0)
            {
                TargetHit candidate = targetCandidates[0];
                if (candidate.Distance <= obstructionDistance + 0.0001f)
                {
                    firstTarget = candidate;
                    foundFirstTarget = true;
                }
            }

            if (foundFirstTarget)
            {
                AddCurrentTarget(firstTarget);
                while (currentTargets.Count < data.MaxTargetCount)
                {
                    Vector2 chainOrigin = targetPoints[targetPoints.Count - 1];
                    if (!TryFindNextChainTarget(chainOrigin, out TargetHit nextTarget)) break;
                    AddCurrentTarget(nextTarget);
                }
            }

            endPoint = targetPoints.Count > 0
                ? targetPoints[targetPoints.Count - 1]
                : startPoint + direction * obstructionDistance;
            CurrentLength = GetCurrentPathLength();
            HasHit = currentTargets.Count > 0 || obstructionCollider != null;
        }

        private void AddCurrentTarget(TargetHit hit)
        {
            currentTargets.Add(hit);
            targetPoints.Add(hit.Collider.bounds.center);
        }

        private bool TryFindNextChainTarget(Vector2 chainOrigin, out TargetHit nextTarget)
        {
            nextTarget = default;
            chainOverlaps.Clear();
            targetCandidates.Clear();
            Physics2D.OverlapCircle(chainOrigin, data.ChainRange, targetFilter, chainOverlaps);

            for (int i = 0; i < chainOverlaps.Count; i++)
            {
                Collider2D collider = chainOverlaps[i];
                if (collider == null || IsCasterCollider(collider)) continue;

                IDamageable target = ResolveDamageable(collider);
                MonoBehaviour behaviour = target as MonoBehaviour;
                if (!IsTargetAlive(target, behaviour) || ContainsCurrentTarget(target)) continue;

                float distance = Vector2.Distance(chainOrigin, collider.bounds.center);
                AddOrUpdateTargetCandidate(collider, target, behaviour, distance);
            }

            if (targetCandidates.Count == 0) return false;
            targetCandidates.Sort(CompareTargetHits);
            nextTarget = targetCandidates[0];
            return true;
        }

        private bool ContainsCurrentTarget(IDamageable target)
        {
            for (int i = 0; i < currentTargets.Count; i++)
                if (ReferenceEquals(currentTargets[i].Target, target)) return true;
            return false;
        }

        private float GetCurrentPathLength()
        {
            if (targetPoints.Count == 0) return Vector2.Distance(startPoint, endPoint);

            float length = 0f;
            Vector2 segmentStart = startPoint;
            for (int i = 0; i < targetPoints.Count; i++)
            {
                length += Vector2.Distance(segmentStart, targetPoints[i]);
                segmentStart = targetPoints[i];
            }
            return length;
        }

        private void ProcessDamageTicks(double now)
        {
            if (!IsFiring || owner == null || data == null) return;
            while (owner != null && owner.TryConsumeDamageTick(now))
            {
                // 밀린 틱 사이에 대상이 이동/사망해도 이전 판정을 재사용하지 않습니다.
                UpdateGeometry();
                if (!HasValidTarget()) continue;

                float damage = owner.GetDamagePerTick();
                bool dealtDamage = false;
                for (int i = 0; i < currentTargets.Count; i++)
                {
                    TargetHit hit = currentTargets[i];
                    if (!IsTargetHitValid(hit)) continue;

                    hit.Target.TakeDamage(damage);
                    dealtDamage = true;
                    if (!IsFiring || owner == null) return;
                    if (IsTargetAlive(hit.Target, hit.Behaviour))
                        owner.ApplyStatusEffects(hit.Target);
                    else
                        RemoveCachedTarget(hit.Target);
                }

                if (dealtDamage) visual.NotifyDamageTick(elapsed);
                // 죽거나 이동한 적에 굵은 연결선이 한 프레임 더 남지 않게 합니다.
                UpdateGeometry();
            }
        }

        private void AddOrUpdateTargetCandidate(Collider2D collider, IDamageable target,
            MonoBehaviour behaviour, float distance)
        {
            for (int i = 0; i < targetCandidates.Count; i++)
            {
                TargetHit candidate = targetCandidates[i];
                if (!ReferenceEquals(candidate.Target, target)) continue;
                if (distance < candidate.Distance)
                    targetCandidates[i] = new TargetHit(collider, target, behaviour, distance);
                return;
            }

            targetCandidates.Add(new TargetHit(collider, target, behaviour, distance));
        }

        private bool HasValidTarget()
        {
            return CountValidTargets() > 0;
        }

        private int CountValidTargets()
        {
            int count = 0;
            for (int i = 0; i < currentTargets.Count; i++)
                if (IsTargetHitValid(currentTargets[i])) count++;
            return count;
        }

        private static bool IsTargetHitValid(TargetHit hit)
        {
            return IsTargetAlive(hit.Target, hit.Behaviour) && hit.Collider != null &&
                hit.Collider.enabled && hit.Collider.gameObject.activeInHierarchy;
        }

        private static int CompareTargetHits(TargetHit first, TargetHit second)
        {
            return first.Distance.CompareTo(second.Distance);
        }

        private IDamageable ResolveDamageable(Collider2D hitCollider)
        {
            if (damageableCache.TryGetValue(hitCollider, out IDamageable cached))
            {
                MonoBehaviour cachedBehaviour = cached as MonoBehaviour;
                if (IsTargetAlive(cached, cachedBehaviour)) return cached;
                damageableCache.Remove(hitCollider);
                return null;
            }
            IDamageable target = hitCollider.GetComponentInParent<IDamageable>();
            damageableCache.Add(hitCollider, target);
            return target;
        }

        private static bool IsTargetAlive(IDamageable target, MonoBehaviour behaviour)
        {
            return target != null && behaviour != null && behaviour.isActiveAndEnabled &&
                (!(target is EnemyBase enemy) || !enemy.isDead);
        }

        private void RemoveCachedTarget(IDamageable target)
        {
            staleCachedColliders.Clear();
            foreach (KeyValuePair<Collider2D, IDamageable> entry in damageableCache)
                if (ReferenceEquals(entry.Value, target)) staleCachedColliders.Add(entry.Key);
            for (int i = 0; i < staleCachedColliders.Count; i++)
                damageableCache.Remove(staleCachedColliders[i]);
            staleCachedColliders.Clear();
        }

        private bool IsCasterCollider(Collider2D hitCollider)
        {
            Transform caster = owner != null ? owner.CasterTransform : null;
            return caster != null && hitCollider.transform.IsChildOf(caster);
        }

        private static bool IsInLayerMask(int layer, LayerMask mask)
        {
            return (mask.value & (1 << layer)) != 0;
        }

        private void UpdateVisual()
        {
            if (IsFiring) connectedVisual = HasTargetHit;
            visual.Render(startPoint, endPoint, direction, muzzleDirection, HasHit, connectedVisual,
                targetPoints, elapsed, visualAlpha, IsFiring);
        }

        private void EnsureVisual()
        {
            if (visual == null) visual = new VoidRayBeamVisual(transform);
        }

        public void StopFiring()
        {
            if (!IsFiring) return;

            IsFiring = false;
            ClearCurrentTargets();
            VoidRayWeapon releasingOwner = owner;
            owner = null;
            releasingOwner?.OnBeamReleased(this);

            float fadeDuration = data != null ? Mathf.Max(0f, data.visualFadeDuration) : 0f;
            if (fadeDuration <= 0f)
            {
                FinishAndRelease();
                return;
            }

            IsFading = true;
            fadeRemaining = fadeDuration;
        }

        public void StopImmediately()
        {
            if (!IsFiring && !IsFading && owner == null) return;
            VoidRayWeapon releasingOwner = owner;
            owner = null;
            IsFiring = false;
            IsFading = false;
            releasingOwner?.OnBeamReleased(this);
            FinishAndRelease();
        }

        private void FinishAndRelease()
        {
            ObjectPoolManager returnPool = pool;
            string returnTag = poolTag;
            ResetState();
            if (returnPool != null && !string.IsNullOrEmpty(returnTag))
                returnPool.ReturnToPool(returnTag, gameObject);
            else if (Application.isPlaying)
                Destroy(gameObject);
            else
                DestroyImmediate(gameObject);
        }

        private void ResetState()
        {
            IsFiring = false;
            IsFading = false;
            HasHit = false;
            connectedVisual = false;
            visualAlpha = 0f;
            owner = null;
            origin = null;
            data = null;
            pool = null;
            poolTag = null;
            direction = Vector2.right;
            muzzleDirection = Vector2.right;
            ClearCurrentTargets();
            raycastHits.Clear();
            targetCandidates.Clear();
            chainOverlaps.Clear();
            targetPoints.Clear();
            damageableCache.Clear();
            staleCachedColliders.Clear();
            if (visual != null) visual.Hide();
        }

        private void ClearCurrentTargets()
        {
            currentTargets.Clear();
        }

        private readonly struct TargetHit
        {
            public readonly Collider2D Collider;
            public readonly IDamageable Target;
            public readonly MonoBehaviour Behaviour;
            public readonly float Distance;

            public TargetHit(Collider2D collider, IDamageable target,
                MonoBehaviour behaviour, float distance)
            {
                Collider = collider;
                Target = target;
                Behaviour = behaviour;
                Distance = distance;
            }
        }

        private void OnDisable()
        {
            VoidRayWeapon releasingOwner = owner;
            owner = null;
            releasingOwner?.OnBeamReleased(this);
            ResetState();
        }
    }

    /// <summary>지속 광선의 몸통, 발사점과 명중점 표현만 담당합니다.</summary>
    public sealed class VoidRayBeamVisual
    {
        private const int MaxConnectedStrands = 6;
        private static readonly int MainTextureProperty = Shader.PropertyToID("_MainTex");
        private static readonly int MainTextureSTProperty = Shader.PropertyToID("_MainTex_ST");
        private static readonly int FrameCountProperty = Shader.PropertyToID("_FrameCount");
        private static readonly int FrameIndexProperty = Shader.PropertyToID("_FrameIndex");
        private static readonly int FlowOffsetProperty = Shader.PropertyToID("_FlowOffset");
        private static readonly int StrokeExpansionProperty = Shader.PropertyToID("_StrokeExpansion");
        private static readonly int IntensityProperty = Shader.PropertyToID("_Intensity");

        private static readonly AnimationCurve ConstantWidthCurve = AnimationCurve.Linear(0f, 1f, 1f, 1f);
        private static readonly AnimationCurve AirBodyWidthCurve = new AnimationCurve(
            new Keyframe(0f, 1f),
            new Keyframe(0.18f, 0.82f),
            new Keyframe(0.4f, 1.18f),
            new Keyframe(0.62f, 0.72f),
            new Keyframe(0.8f, 0.94f),
            new Keyframe(1f, 0.3f));

        private readonly Transform root;
        private readonly LineRenderer[] connectedStrands = new LineRenderer[MaxConnectedStrands];
        private readonly List<LineRenderer> impactSparkLines = new List<LineRenderer>(24);
        private readonly MaterialPropertyBlock bodyMaterialProperties = new MaterialPropertyBlock();
        private LineRenderer outerLine;
        private LineRenderer secondaryLine;
        private LineRenderer coreLine;
        private SpriteRenderer legacyBeamRenderer;
        private SpriteRenderer startEffectRenderer;
        private SpriteRenderer endEffectRenderer;

        private VoidRayWeaponData data;
        private SpriteRenderer weaponRenderer;
        private readonly AnimationCurve connectedWidthCurve = new AnimationCurve();
        private float[] connectedSegmentLengths = new float[3];
        private float[] connectedSegmentScales = new float[3];
        private Keyframe[] connectedWidthKeys = new Keyframe[4];
        private bool hasStartFrames;
        private bool missingReferenceLogged;
        private float lastDamageElapsed = float.NegativeInfinity;
        private int damageTickSequence;
        private Vector4 bodyTextureTransform = new Vector4(1f, 1f, 0f, 0f);

        public VoidRayBeamVisual(Transform root)
        {
            this.root = root;
            EnsureReferences();
        }

        public void Initialize(SpriteRenderer weaponRenderer, VoidRayWeaponData data)
        {
            this.weaponRenderer = weaponRenderer;
            this.data = data;
            lastDamageElapsed = float.NegativeInfinity;
            damageTickSequence = 0;
            hasStartFrames = data != null && data.HasStartFrames;
            EnsureReferences();
            CacheBodyTextureTransform();
            EnsureImpactSparkCount(data != null ? data.MaxTargetCount * data.ImpactSparkCount : 0);
            EnsureConnectedWidthCapacity(data != null ? data.MaxTargetCount : 1);
            ConfigurePointCounts(false, 0);
            ConfigureSorting(weaponRenderer);
        }

        public void NotifyDamageTick(float elapsed)
        {
            lastDamageElapsed = elapsed;
            damageTickSequence++;
        }

        public void Render(Vector2 startPoint, Vector2 endPoint, Vector2 direction,
            Vector2 muzzleDirection, bool hasHit, bool hasTargetHit,
            IReadOnlyList<Vector2> targetPoints, float elapsed, float alpha, bool allowJitter)
        {
            if (data == null || outerLine == null || secondaryLine == null || coreLine == null) return;
            ConfigureSorting(weaponRenderer);
            int targetCount = hasTargetHit && targetPoints != null ? targetPoints.Count : 0;
            ConfigurePointCounts(hasTargetHit, targetCount);

            float phase = elapsed * data.bodyAnimationSpeed;
            // 벽 충돌과 적 연결은 다른 상태입니다. 굵은 몸통은 적에게 닿을 때만 유지합니다.
            float damagePulse = hasTargetHit
                ? 1f - Mathf.Clamp01((elapsed - lastDamageElapsed) / Mathf.Max(0.01f, data.damagePulseDuration))
                : 0f;
            float pulse = 1f + Mathf.Sin(phase * Mathf.PI * 2f) * 0.035f +
                damagePulse * Mathf.Clamp01(data.damagePulseStrength);
            float outerWidth = hasTargetHit ? data.OuterWidth : data.AirOuterWidth;
            float coreWidth = hasTargetHit ? data.CoreWidth : data.AirCoreWidth;
            float jitter = (hasTargetHit ? data.jitterStrength : data.airJitterStrength) *
                (allowJitter ? 1f : 0.7f);
            float beamLength = Vector2.Distance(startPoint, endPoint);
            jitter *= Mathf.Clamp01(beamLength / Mathf.Max(outerWidth * 2f, 0.01f));
            Vector2 normal = new Vector2(-direction.y, direction.x);
            if (hasTargetHit)
            {
                RenderConnectedArcGeometry(startPoint, targetPoints, direction, muzzleDirection,
                    elapsed, phase, jitter, alpha);
            }
            else
            {
                RenderAirBoltGeometry(startPoint, endPoint, direction, muzzleDirection, normal,
                    elapsed, jitter, alpha, hasHit, allowJitter);
            }
            UpdateBodyTextureAnimation(elapsed, hasTargetHit);
            Color outerColor = Color.Lerp(data.outerColor, data.coreColor, damagePulse * 0.35f);
            float tipAlpha = hasTargetHit ? 1f : data.AirTipAlpha;
            AnimationCurve beamWidthCurve = hasTargetHit
                ? UpdateConnectedWidthCurve(startPoint, targetPoints)
                : AirBodyWidthCurve;
            ConfigureLineAppearance(outerLine, outerWidth * pulse, outerColor,
                alpha * (hasTargetHit ? data.connectedGlowAlpha : 0.35f), tipAlpha,
                beamWidthCurve);

            int visibleStrands = hasTargetHit ? data.ConnectedStrandCount : data.AirStrandCount;
            for (int i = 0; i < connectedStrands.Length; i++)
            {
                if (i >= visibleStrands) continue;
                if (hasTargetHit)
                {
                    float widthVariation = 0.82f + Hash01(i, 29) * 0.36f;
                    Color strandColor = Color.Lerp(data.outerColor, data.coreColor,
                        0.55f + Hash01(i, 47) * 0.35f);
                    ConfigureLineAppearance(connectedStrands[i],
                        data.CoreWidth * data.secondaryArcWidthMultiplier * widthVariation,
                        strandColor, alpha * data.secondaryArcAlpha, 1f, beamWidthCurve);
                }
                else
                {
                    float widthVariation = 0.82f + Hash01(i, 71) * 0.24f;
                    Color strandColor = Color.Lerp(data.outerColor, data.coreColor,
                        0.68f + Hash01(i, 83) * 0.22f);
                    ConfigureLineAppearance(connectedStrands[i],
                        data.AirCoreWidth * data.airStrandWidthMultiplier * widthVariation,
                        strandColor, alpha * data.airStrandAlpha, 0.25f, ConstantWidthCurve);
                }
            }
            ConfigureLineAppearance(coreLine, coreWidth * pulse, data.coreColor,
                alpha * (hasTargetHit ? 1f : 0.8f), tipAlpha,
                beamWidthCurve);
            RenderImpactSparks(targetPoints, alpha, elapsed, hasTargetHit, damagePulse);

            int startFrameIndex = GetFrameIndex(data.startEndFrames, hasStartFrames, elapsed);
            Sprite startSprite = startFrameIndex >= 0 ? data.startEndFrames[startFrameIndex] : null;
            float startEffectWidth = data.AirOuterWidth * 2f *
                Mathf.Max(0.1f, data.startEffectSizeMultiplier);
            UpdateEndpoint(startEffectRenderer, startSprite, startPoint, muzzleDirection, true, alpha,
                startEffectWidth, damagePulse);
            if (endEffectRenderer != null) endEffectRenderer.enabled = false;
        }

        public void Hide()
        {
            if (legacyBeamRenderer != null) legacyBeamRenderer.enabled = false;
            if (outerLine != null) outerLine.enabled = false;
            if (coreLine != null) coreLine.enabled = false;
            for (int i = 0; i < connectedStrands.Length; i++)
                if (connectedStrands[i] != null) connectedStrands[i].enabled = false;
            for (int i = 0; i < impactSparkLines.Count; i++)
                if (impactSparkLines[i] != null) impactSparkLines[i].enabled = false;
            if (startEffectRenderer != null) startEffectRenderer.enabled = false;
            if (endEffectRenderer != null) endEffectRenderer.enabled = false;
        }

        private void RenderAirBoltGeometry(Vector2 startPoint, Vector2 logicalEndPoint,
            Vector2 direction, Vector2 muzzleDirection, Vector2 aimNormal, float elapsed,
            float jitter, float alpha, bool pinEndpoint, bool allowMotion)
        {
            bool visible = Vector2.SqrMagnitude(logicalEndPoint - startPoint) > 0.000001f && alpha > 0f;
            outerLine.enabled = visible;
            coreLine.enabled = visible;
            if (!visible)
            {
                for (int i = 0; i < connectedStrands.Length; i++)
                    connectedStrands[i].enabled = false;
                return;
            }

            // 사라지는 동안에는 마지막 형상을 유지합니다. 공격 판정은 이미 별도로 중단된 상태입니다.
            if (!allowMotion) return;

            int pointCount = data.AirVisualSegments;
            float beamLength = Vector2.Distance(startPoint, logicalEndPoint);
            GetNoiseSample(elapsed, data.AirPathRefreshInterval * 2.4f,
                out int broadSampleIndex, out float broadSampleBlend);
            GetNoiseSample(elapsed, data.AirPathRefreshInterval,
                out int detailSampleIndex, out float detailSampleBlend);
            Vector2 visualEndPoint = ResolveAirBoltEndPoint(startPoint, logicalEndPoint,
                direction, aimNormal, beamLength, broadSampleIndex, broadSampleBlend, pinEndpoint);

            // 회전한 총구 방향을 짧게 유지한 뒤 전체 경로가 끝점 쪽으로 수렴합니다.
            float startTangentLength = Mathf.Min(
                Mathf.Max(0.12f, data.muzzleBendDistance * 0.55f), beamLength * 0.12f);
            Vector2 safeMuzzleDirection = muzzleDirection.sqrMagnitude > 0.0001f
                ? muzzleDirection.normalized
                : direction;
            Vector2 visualDirection = visualEndPoint - startPoint;
            if (visualDirection.sqrMagnitude > 0.0001f) visualDirection.Normalize();
            else visualDirection = direction;
            Vector2 startTangent = safeMuzzleDirection * startTangentLength;
            Vector2 endTangent = visualDirection * (beamLength * 0.82f);

            for (int i = 0; i < pointCount; i++)
            {
                float t = i / (float)(pointCount - 1);
                Vector2 mainPoint = EvaluateAirMainPoint(startPoint, visualEndPoint,
                    startTangent, endTangent, aimNormal, t, jitter,
                    broadSampleIndex, broadSampleBlend, detailSampleIndex, detailSampleBlend,
                    out _);
                outerLine.SetPosition(i, mainPoint);
                coreLine.SetPosition(i, mainPoint);
            }

            RenderAirPartialTraces(startPoint, visualEndPoint, startTangent, endTangent,
                aimNormal, elapsed, jitter, broadSampleIndex, broadSampleBlend,
                detailSampleIndex, detailSampleBlend);
        }

        private Vector2 EvaluateAirMainPoint(Vector2 startPoint, Vector2 endPoint,
            Vector2 startTangent, Vector2 endTangent, Vector2 fallbackNormal, float t,
            float jitter, int broadSampleIndex, float broadSampleBlend,
            int detailSampleIndex, float detailSampleBlend, out Vector2 localNormal)
        {
            Vector2 basePoint = CubicHermite(startPoint, endPoint, startTangent, endTangent, t);
            Vector2 tangent = CubicHermiteTangent(startPoint, endPoint,
                startTangent, endTangent, t);
            localNormal = tangent.sqrMagnitude > 0.0001f
                ? new Vector2(-tangent.y, tangent.x).normalized
                : fallbackNormal;
            if (t <= 0f || t >= 1f) return basePoint;

            float envelope = Mathf.Sin(t * Mathf.PI);
            float broadNoise = SampleSpatialNoise(t, broadSampleIndex, broadSampleBlend,
                0x31f2e45bu, 2.45f);
            float detailNoise = SampleSpatialNoise(t, detailSampleIndex, detailSampleBlend,
                0x6a09e667u, 7.2f);
            float offset = SnapToPixel((broadNoise * 0.82f + detailNoise * 0.18f) *
                jitter * envelope);
            return SnapToPixel(basePoint + localNormal * offset);
        }

        private void RenderAirPartialTraces(Vector2 startPoint, Vector2 endPoint,
            Vector2 startTangent, Vector2 endTangent, Vector2 fallbackNormal, float elapsed,
            float jitter, int broadSampleIndex, float broadSampleBlend,
            int detailSampleIndex, float detailSampleBlend)
        {
            int activeTraces = data.airStrandAlpha > 0f ? data.AirStrandCount : 0;
            for (int traceIndex = 0; traceIndex < connectedStrands.Length; traceIndex++)
            {
                LineRenderer trace = connectedStrands[traceIndex];
                bool configured = traceIndex < activeTraces;
                if (!configured)
                {
                    trace.enabled = false;
                    continue;
                }

                float traceInterval = data.AirPathRefreshInterval * (1.35f + traceIndex * 0.25f);
                GetNoiseSample(elapsed + traceIndex * 0.037f, traceInterval,
                    out int traceSampleIndex, out float traceSampleBlend);
                uint seed = 0xbb67ae85u + (uint)traceIndex * 0x3c6ef372u;
                const float leadIn = 0.18f;
                const float cycleLength = 1.36f;
                float phaseOffset = activeTraces > 0
                    ? traceIndex * cycleLength / activeTraces
                    : 0f;
                float flowTime = elapsed * data.AirFlowSpeed + phaseOffset;
                int flowCycleIndex = Mathf.FloorToInt(flowTime / cycleLength);
                float flowPosition = Mathf.Repeat(flowTime, cycleLength) - leadIn;
                float lengthNoise = ArcNoise(traceIndex * 13 + 7,
                    flowCycleIndex, seed ^ 0xa54ff53au);
                float traceLength = Mathf.Lerp(0.18f, 0.38f, lengthNoise * 0.5f + 0.5f);
                float unclampedEndT = flowPosition + traceLength;
                bool active = unclampedEndT > 0f && flowPosition < 1f;
                trace.enabled = active;
                if (!active) continue;

                float startT = Mathf.Clamp01(flowPosition);
                float endT = Mathf.Clamp01(unclampedEndT);
                int tracePointCount = Mathf.Clamp(
                    Mathf.RoundToInt((endT - startT) * (data.AirVisualSegments - 1)) + 1,
                    3, 10);
                if (trace.positionCount != tracePointCount)
                    trace.positionCount = tracePointCount;

                float sideNoise = ArcNoise(traceIndex * 17 + 5,
                    flowCycleIndex, seed ^ 0x9e3779b9u);
                float side = sideNoise >= 0f ? 1f : -1f;
                float bowStrength = jitter * data.airStrandSpreadMultiplier *
                    Mathf.Lerp(0.32f, 0.72f, Mathf.Abs(sideNoise));

                for (int pointIndex = 0; pointIndex < tracePointCount; pointIndex++)
                {
                    float localT = pointIndex / (float)(tracePointCount - 1);
                    float mainT = Mathf.Lerp(startT, endT, localT);
                    Vector2 mainPoint = EvaluateAirMainPoint(startPoint, endPoint,
                        startTangent, endTangent, fallbackNormal, mainT, jitter,
                        broadSampleIndex, broadSampleBlend, detailSampleIndex, detailSampleBlend,
                        out Vector2 localNormal);
                    float branchEnvelope = Mathf.Sin(localT * Mathf.PI);
                    float ripple = SampleSpatialNoise(localT, traceSampleIndex, traceSampleBlend,
                        seed ^ 0x510e527fu, 3.4f) * jitter * 0.12f;
                    float branchOffset = SnapToPixel(
                        (side * bowStrength + ripple) * branchEnvelope);
                    trace.SetPosition(pointIndex,
                        SnapToPixel(mainPoint + localNormal * branchOffset));
                }
            }
        }

        private void CacheBodyTextureTransform()
        {
            Material material = coreLine != null ? coreLine.sharedMaterial : null;
            if (material == null || !material.HasProperty(MainTextureProperty))
            {
                bodyTextureTransform = new Vector4(1f, 1f, 0f, 0f);
                return;
            }

            Vector2 scale = material.GetTextureScale("_MainTex");
            Vector2 offset = material.GetTextureOffset("_MainTex");
            bodyTextureTransform = new Vector4(scale.x, scale.y, offset.x, offset.y);
        }

        private void UpdateBodyTextureAnimation(float elapsed, bool connected)
        {
            float flowOffset = connected
                ? 0f
                : Mathf.Repeat(-elapsed * data.AirFlowSpeed, 1f);
            int frameCount = data.BodyFrameCount;
            int frameIndex = frameCount > 1
                ? Mathf.FloorToInt(elapsed * Mathf.Max(0f, data.bodyAnimationSpeed)) % frameCount
                : 0;
            ApplyBodyTextureAnimation(outerLine, frameCount, frameIndex, flowOffset);
            ApplyBodyTextureAnimation(coreLine, frameCount, frameIndex, flowOffset);
            for (int i = 0; i < connectedStrands.Length; i++)
                ApplyBodyTextureAnimation(connectedStrands[i], frameCount, frameIndex, flowOffset);
        }

        private void ApplyBodyTextureAnimation(LineRenderer line, int frameCount,
            int frameIndex, float flowOffset)
        {
            if (line == null) return;
            bodyMaterialProperties.Clear();
            bodyMaterialProperties.SetVector(MainTextureSTProperty, bodyTextureTransform);
            bodyMaterialProperties.SetFloat(FrameCountProperty, frameCount);
            bodyMaterialProperties.SetFloat(FrameIndexProperty, frameIndex);
            bodyMaterialProperties.SetFloat(FlowOffsetProperty, flowOffset);
            line.SetPropertyBlock(bodyMaterialProperties);
        }

        private Vector2 ResolveAirBoltEndPoint(Vector2 startPoint, Vector2 logicalEndPoint,
            Vector2 direction, Vector2 normal, float beamLength, int sampleIndex,
            float sampleBlend, bool pinEndpoint)
        {
            if (pinEndpoint) return logicalEndPoint;

            int endpointNoiseIndex = data.AirVisualSegments + 7;
            float lengthNoise = SampleSteppedNoise(endpointNoiseIndex, sampleIndex, sampleBlend,
                0x7135ab21u);
            float sideNoise = SampleSteppedNoise(endpointNoiseIndex + 1, sampleIndex, sampleBlend,
                0xa24baed5u);
            float retreat = Mathf.Max(0f, data.airEndpointLengthJitter) *
                Mathf.Lerp(0.2f, 1f, lengthNoise * 0.5f + 0.5f);
            float visualLength = Mathf.Max(0.01f, beamLength - SnapToPixel(retreat));
            return SnapToPixel(startPoint + direction * visualLength +
                normal * SnapToPixel(sideNoise * Mathf.Max(0f, data.airEndpointSideJitter)));
        }

        private AnimationCurve UpdateConnectedWidthCurve(Vector2 startPoint,
            IReadOnlyList<Vector2> targetPoints)
        {
            if (targetPoints == null || targetPoints.Count == 0) return ConstantWidthCurve;

            int segmentCount = targetPoints.Count;
            EnsureConnectedWidthCapacity(segmentCount);
            float totalLength = 0f;
            Vector2 segmentStart = startPoint;
            float widthReference = Mathf.Max(0.01f, data.OuterWidth * 2f);
            for (int i = 0; i < segmentCount; i++)
            {
                float segmentLength = Vector2.Distance(segmentStart, targetPoints[i]);
                connectedSegmentLengths[i] = segmentLength;
                // 짧은 구간은 두께도 함께 줄여 광선이 눌린 덩어리처럼 보이지 않게 합니다.
                connectedSegmentScales[i] = Mathf.Clamp(segmentLength / widthReference, 0.2f, 1f);
                totalLength += segmentLength;
                segmentStart = targetPoints[i];
            }

            if (totalLength <= 0.0001f) return ConstantWidthCurve;

            connectedWidthKeys[0] = new Keyframe(0f, connectedSegmentScales[0], 0f, 0f);
            float accumulatedLength = 0f;
            for (int i = 1; i < segmentCount; i++)
            {
                accumulatedLength += connectedSegmentLengths[i - 1];
                float normalizedPosition = Mathf.Clamp01(accumulatedLength / totalLength);
                float junctionScale = Mathf.Min(
                    connectedSegmentScales[i - 1], connectedSegmentScales[i]);
                connectedWidthKeys[i] = new Keyframe(normalizedPosition, junctionScale, 0f, 0f);
            }
            connectedWidthKeys[segmentCount] =
                new Keyframe(1f, connectedSegmentScales[segmentCount - 1], 0f, 0f);
            connectedWidthCurve.keys = connectedWidthKeys;
            connectedWidthCurve.preWrapMode = WrapMode.Clamp;
            connectedWidthCurve.postWrapMode = WrapMode.Clamp;
            return connectedWidthCurve;
        }

        private void EnsureConnectedWidthCapacity(int count)
        {
            int requiredCount = Mathf.Max(1, count);
            if (connectedSegmentLengths.Length < requiredCount)
            {
                connectedSegmentLengths = new float[requiredCount];
                connectedSegmentScales = new float[requiredCount];
            }
            if (connectedWidthKeys.Length != requiredCount + 1)
                connectedWidthKeys = new Keyframe[requiredCount + 1];
        }

        private void RenderConnectedArcGeometry(Vector2 startPoint,
            IReadOnlyList<Vector2> targetPoints, Vector2 direction, Vector2 muzzleDirection,
            float elapsed, float phase, float jitter, float alpha)
        {
            bool visible = targetPoints != null && targetPoints.Count > 0 &&
                Vector2.SqrMagnitude(targetPoints[0] - startPoint) > 0.000001f && alpha > 0f;
            // 적 연결 중에는 밝은 중심선만 보여 주고, 연하고 굵은 외곽선은 숨깁니다.
            outerLine.enabled = visible && data.connectedGlowAlpha > 0f;
            coreLine.enabled = visible;
            int activeStrands = data.secondaryArcAlpha > 0f ? data.ConnectedStrandCount : 0;
            for (int strandIndex = 0; strandIndex < connectedStrands.Length; strandIndex++)
                connectedStrands[strandIndex].enabled = visible && strandIndex < activeStrands;
            if (!visible) return;

            float samplePosition = elapsed / data.PathRefreshInterval;
            int sampleIndex = Mathf.FloorToInt(samplePosition);
            float sampleBlend = Mathf.SmoothStep(0f, 1f, samplePosition - sampleIndex);
            int segmentPointCount = data.VisualSegments;
            Vector2 segmentStart = startPoint;

            for (int segmentIndex = 0; segmentIndex < targetPoints.Count; segmentIndex++)
            {
                Vector2 segmentEnd = targetPoints[segmentIndex];
                Vector2 segmentDirection = segmentEnd - segmentStart;
                float segmentLength = segmentDirection.magnitude;
                if (segmentLength > 0.0001f) segmentDirection /= segmentLength;
                else segmentDirection = direction;
                Vector2 segmentNormal = new Vector2(-segmentDirection.y, segmentDirection.x);
                float bendDistance = Mathf.Min(Mathf.Max(0f, data.muzzleBendDistance),
                    segmentLength * 0.35f);
                Vector2 startDirection = segmentDirection;
                if (segmentIndex == 0 && muzzleDirection.sqrMagnitude > 0.0001f)
                {
                    float muzzleBlend = Mathf.SmoothStep(0f, 1f,
                        segmentLength / Mathf.Max(0.01f, data.OuterWidth * 3f));
                    startDirection = Vector2.Lerp(
                        segmentDirection, muzzleDirection.normalized, muzzleBlend);
                    if (startDirection.sqrMagnitude > 0.0001f) startDirection.Normalize();
                    else startDirection = segmentDirection;
                }
                float segmentJitter = jitter * Mathf.SmoothStep(0f, 1f,
                    segmentLength / Mathf.Max(0.01f, data.OuterWidth * 2.5f));
                Vector2 firstControl = segmentStart + startDirection * bendDistance;
                Vector2 secondControl = segmentEnd - segmentDirection * (bendDistance * 0.55f);

                for (int i = 0; i < segmentPointCount; i++)
                {
                    int rendererIndex = segmentIndex * (segmentPointCount - 1) + i;
                    float t = i / (float)(segmentPointCount - 1);
                    Vector2 basePoint = CubicBezier(
                        segmentStart, firstControl, secondControl, segmentEnd, t);
                    if (i == 0 || i == segmentPointCount - 1)
                    {
                        outerLine.SetPosition(rendererIndex, basePoint);
                        coreLine.SetPosition(rendererIndex, basePoint);
                        for (int strandIndex = 0; strandIndex < activeStrands; strandIndex++)
                            connectedStrands[strandIndex].SetPosition(rendererIndex, basePoint);
                        continue;
                    }

                    // 각 적의 중심은 고정하고 연결 구간의 중간 지점만 흔듭니다.
                    Vector2 tangent = CubicBezierTangent(segmentStart, firstControl,
                        secondControl, segmentEnd, t);
                    Vector2 localNormal = tangent.sqrMagnitude > 0.0001f
                        ? new Vector2(-tangent.y, tangent.x).normalized
                        : segmentNormal;
                    float envelope = Mathf.Sin(t * Mathf.PI);
                    int noiseIndex = segmentIndex * segmentPointCount + i;
                    float segmentPhase = phase + segmentIndex * 0.41f;
                    float mainNoise = SampleArcOffset(noiseIndex, sampleIndex, sampleBlend,
                        segmentPhase, 3.17f + segmentIndex * 11.31f);
                    float mainOffset = SnapToPixel(mainNoise * segmentJitter * envelope);

                    Vector2 mainPoint = SnapToPixel(basePoint + localNormal * mainOffset);
                    outerLine.SetPosition(rendererIndex, mainPoint);
                    coreLine.SetPosition(rendererIndex, mainPoint);
                    for (int strandIndex = 0; strandIndex < activeStrands; strandIndex++)
                    {
                        float strandNoise = SampleArcOffset(noiseIndex, sampleIndex, sampleBlend,
                            segmentPhase, 17.43f + strandIndex * 13.71f + segmentIndex * 7.19f);
                        float strandBias = activeStrands > 1
                            ? (strandIndex / (float)(activeStrands - 1) - 0.5f) * 0.35f
                            : 0f;
                        float strandOffset = SnapToPixel(
                            (mainNoise * 0.18f + strandNoise * 0.82f + strandBias) * segmentJitter *
                            data.connectedStrandSpreadMultiplier * envelope);
                        connectedStrands[strandIndex].SetPosition(rendererIndex,
                            SnapToPixel(basePoint + localNormal * strandOffset));
                    }
                }

                segmentStart = segmentEnd;
            }
        }

        private static Vector2 CubicBezier(Vector2 start, Vector2 firstControl,
            Vector2 secondControl, Vector2 end, float t)
        {
            float inverse = 1f - t;
            return inverse * inverse * inverse * start +
                3f * inverse * inverse * t * firstControl +
                3f * inverse * t * t * secondControl +
                t * t * t * end;
        }

        private static Vector2 CubicBezierTangent(Vector2 start, Vector2 firstControl,
            Vector2 secondControl, Vector2 end, float t)
        {
            float inverse = 1f - t;
            return 3f * inverse * inverse * (firstControl - start) +
                6f * inverse * t * (secondControl - firstControl) +
                3f * t * t * (end - secondControl);
        }

        private static Vector2 CubicHermite(Vector2 start, Vector2 end,
            Vector2 startTangent, Vector2 endTangent, float t)
        {
            float squared = t * t;
            float cubed = squared * t;
            return (2f * cubed - 3f * squared + 1f) * start +
                (cubed - 2f * squared + t) * startTangent +
                (-2f * cubed + 3f * squared) * end +
                (cubed - squared) * endTangent;
        }

        private static Vector2 CubicHermiteTangent(Vector2 start, Vector2 end,
            Vector2 startTangent, Vector2 endTangent, float t)
        {
            float squared = t * t;
            return (6f * squared - 6f * t) * start +
                (3f * squared - 4f * t + 1f) * startTangent +
                (-6f * squared + 6f * t) * end +
                (3f * squared - 2f * t) * endTangent;
        }

        private void RenderImpactSparks(IReadOnlyList<Vector2> hitPoints, float alpha,
            float elapsed, bool connected, float damagePulse)
        {
            float duration = Mathf.Max(0.01f, data.impactSparkDuration);
            float age = elapsed - lastDamageElapsed;
            float life = connected && age >= 0f ? 1f - Mathf.Clamp01(age / duration) : 0f;
            int sparksPerTarget = data.ImpactSparkCount;
            int targetCount = hitPoints != null ? hitPoints.Count : 0;
            int visibleSparkCount = life > 0f ? targetCount * sparksPerTarget : 0;
            EnsureImpactSparkCount(visibleSparkCount);
            float progress = 1f - life;

            for (int i = 0; i < impactSparkLines.Count; i++)
            {
                LineRenderer spark = impactSparkLines[i];
                bool visible = i < visibleSparkCount;
                spark.enabled = visible;
                if (!visible) continue;

                int targetIndex = i / sparksPerTarget;
                int sparkIndex = i % sparksPerTarget;
                Vector2 hitPoint = hitPoints[targetIndex];
                int randomIndex = targetIndex * 97 + sparkIndex;
                float angle = Hash01(damageTickSequence, randomIndex * 19 + 7) * 360f;
                Vector2 sparkDirection = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad),
                    Mathf.Sin(angle * Mathf.Deg2Rad));
                float travelVariation = 0.55f +
                    Hash01(damageTickSequence, randomIndex * 31 + 11) * 0.65f;
                float travel = data.impactSparkLength * travelVariation *
                    Mathf.SmoothStep(0f, 1f, progress);
                float spawnRadius = data.OuterWidth *
                    (0.58f + Hash01(damageTickSequence, randomIndex * 37 + 13) * 0.18f);
                Vector2 gravityOffset = Vector2.down *
                    (data.impactSparkLength * 0.2f * progress * progress);
                Vector2 center = SnapToPixel(hitPoint +
                    sparkDirection * (spawnRadius + travel) + gravityOffset);
                float sizeVariation = 0.72f +
                    Hash01(damageTickSequence, randomIndex * 43 + 17) * 0.5f;
                float size = data.impactSparkWidth * sizeVariation * (0.55f + life * 0.45f);
                Vector2 halfSize = sparkDirection * (size * 0.5f);
                spark.SetPosition(0, center - halfSize);
                spark.SetPosition(1, center + halfSize);

                Color sparkColor = Color.Lerp(data.impactSparkColor, Color.white,
                    0.28f + damagePulse * 0.42f + life * 0.15f);
                ConfigureLineAppearance(spark, size, sparkColor,
                    alpha * Mathf.Clamp01(life * 1.35f));
            }
        }

        private static float SampleArcOffset(int pointIndex, int sampleIndex, float sampleBlend,
            float phase, float seed)
        {
            float current = ArcNoise(pointIndex, sampleIndex, (uint)(seed * 1000f));
            float next = ArcNoise(pointIndex, sampleIndex + 1, (uint)(seed * 1000f));
            float steppedNoise = Mathf.Lerp(current, next, sampleBlend);
            float flowingNoise = Mathf.Sin(pointIndex * 0.85f - phase * 0.45f + seed);
            return steppedNoise * 0.6f + flowingNoise * 0.4f;
        }

        private static float SampleSteppedNoise(int pointIndex, int sampleIndex,
            float sampleBlend, uint seed)
        {
            float current = ArcNoise(pointIndex, sampleIndex, seed);
            float next = ArcNoise(pointIndex, sampleIndex + 1, seed);
            return Mathf.Lerp(current, next, sampleBlend);
        }

        private static float SampleSpatialNoise(float t, int sampleIndex, float sampleBlend,
            uint seed, float frequency)
        {
            float spatialPosition = Mathf.Clamp01(t) * Mathf.Max(1f, frequency);
            int spatialIndex = Mathf.FloorToInt(spatialPosition);
            float spatialBlend = Mathf.SmoothStep(0f, 1f, spatialPosition - spatialIndex);
            float first = SampleSteppedNoise(spatialIndex, sampleIndex, sampleBlend, seed);
            float second = SampleSteppedNoise(spatialIndex + 1, sampleIndex, sampleBlend, seed);
            return Mathf.Lerp(first, second, spatialBlend);
        }

        private static void GetNoiseSample(float elapsed, float interval,
            out int sampleIndex, out float sampleBlend)
        {
            float samplePosition = elapsed / Mathf.Max(0.01f, interval);
            sampleIndex = Mathf.FloorToInt(samplePosition);
            sampleBlend = Mathf.SmoothStep(0f, 1f, samplePosition - sampleIndex);
        }

        private static float ArcNoise(int pointIndex, int sampleIndex, uint seed)
        {
            // 전역 Random 상태를 변경하지 않고 각 마디에 독립적인 꺾임을 만듭니다.
            unchecked
            {
                uint value = (uint)pointIndex * 0x1f123bb5u ^ (uint)sampleIndex * 0x05491333u ^ seed;
                value = (value ^ (value >> 16)) * 0x7feb352du;
                value = (value ^ (value >> 15)) * 0x846ca68bu;
                value ^= value >> 16;
                return (value & 0xffffu) / 32767.5f - 1f;
            }
        }

        private static float Hash01(int first, int second)
        {
            unchecked
            {
                uint value = (uint)first * 0x9e3779b9u ^ (uint)second * 0x85ebca6bu ^ 0xc2b2ae35u;
                value ^= value >> 16;
                value *= 0x7feb352du;
                value ^= value >> 15;
                return (value & 0xffffu) / 65535f;
            }
        }

        private static void ConfigureLineAppearance(LineRenderer line, float width, Color color,
            float alpha, float endAlphaMultiplier = 1f, AnimationCurve widthCurve = null)
        {
            Color startColor = color;
            startColor.a *= alpha;
            Color endColor = startColor;
            endColor.a *= Mathf.Clamp01(endAlphaMultiplier);
            line.widthCurve = widthCurve ?? ConstantWidthCurve;
            line.widthMultiplier = width;
            line.startColor = startColor;
            line.endColor = endColor;
        }

        private static float SnapToPixel(float value)
        {
            // GameScene PixelPerfectCamera의 Assets PPU(64)에 맞춰 광선 몸통을 픽셀 단위로 정렬합니다.
            return Mathf.Round(value * 64f) / 64f;
        }

        private static Vector2 SnapToPixel(Vector2 position)
        {
            return new Vector2(SnapToPixel(position.x), SnapToPixel(position.y));
        }

        private int GetFrameIndex(Sprite[] frames, bool hasFrames, float elapsed)
        {
            if (!hasFrames) return -1;
            float framesPerSecond = Mathf.Max(1f, data.bodyAnimationSpeed);
            return Mathf.FloorToInt(elapsed * framesPerSecond) % frames.Length;
        }

        private void UpdateEndpoint(SpriteRenderer renderer, Sprite sprite, Vector2 position,
            Vector2 facingDirection, bool visible, float alpha, float width, float damagePulse)
        {
            if (renderer == null) return;
            renderer.enabled = visible && sprite != null && alpha > 0f;
            if (!renderer.enabled) return;

            renderer.sprite = sprite;
            Color color = Color.Lerp(data.coreColor, Color.white, damagePulse);
            color.a = alpha;
            renderer.color = color;
            renderer.transform.position = position;
            renderer.transform.rotation = Quaternion.Euler(0f, 0f,
                Mathf.Atan2(facingDirection.y, facingDirection.x) * Mathf.Rad2Deg);
            float spriteHeight = Mathf.Max(0.01f, sprite.bounds.size.y);
            float scale = width / spriteHeight;
            renderer.transform.localScale = new Vector3(scale, scale, 1f);
        }

        private void EnsureReferences()
        {
            if (legacyBeamRenderer == null)
            {
                Transform legacyVisual = root.Find("Visual");
                if (legacyVisual != null) legacyBeamRenderer = legacyVisual.GetComponent<SpriteRenderer>();
            }
            if (startEffectRenderer == null)
            {
                Transform start = root.Find("StartEffect");
                if (start != null) startEffectRenderer = start.GetComponent<SpriteRenderer>();
            }
            if (endEffectRenderer == null)
            {
                Transform end = root.Find("EndEffect");
                if (end != null) endEffectRenderer = end.GetComponent<SpriteRenderer>();
            }
            if (outerLine == null) outerLine = FindOrCreateLine("OuterLine");
            if (secondaryLine == null) secondaryLine = FindOrCreateLine("SecondaryLine");
            if (coreLine == null) coreLine = FindOrCreateLine("CoreLine");
            connectedStrands[0] = secondaryLine;
            for (int i = 1; i < connectedStrands.Length; i++)
                if (connectedStrands[i] == null)
                    connectedStrands[i] = FindOrCreateLine("ConnectedStrand" + (i + 1));
            if (legacyBeamRenderer != null) legacyBeamRenderer.enabled = false;
            if (endEffectRenderer != null) endEffectRenderer.enabled = false;

            if (startEffectRenderer == null && !missingReferenceLogged)
            {
                missingReferenceLogged = true;
                Debug.LogError("[VoidRayBeamVisual] StartEffect SpriteRenderer 참조를 확인해 주세요.", root);
            }
        }

        private void EnsureImpactSparkCount(int count)
        {
            int requiredCount = Mathf.Max(0, count);
            while (impactSparkLines.Count < requiredCount)
            {
                int sparkIndex = impactSparkLines.Count;
                LineRenderer spark = FindOrCreateLine("ImpactSpark" + (sparkIndex + 1), 2);
                spark.enabled = false;
                spark.textureMode = LineTextureMode.Stretch;

                var properties = new MaterialPropertyBlock();
                spark.GetPropertyBlock(properties);
                properties.SetTexture(MainTextureProperty, Texture2D.whiteTexture);
                properties.SetFloat(StrokeExpansionProperty, 0f);
                properties.SetFloat(IntensityProperty, 2.8f);
                spark.SetPropertyBlock(properties);
                impactSparkLines.Add(spark);
            }
        }

        private LineRenderer FindOrCreateLine(string childName, int positionCount = 4)
        {
            Transform child = root.Find(childName);
            if (child == null)
            {
                child = new GameObject(childName).transform;
                child.SetParent(root, false);
            }

            LineRenderer line = child.GetComponent<LineRenderer>();
            if (line == null) line = child.gameObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = positionCount;
            line.loop = false;
            line.alignment = LineAlignment.View;
            line.textureMode = LineTextureMode.Tile;
            line.numCapVertices = 0;
            line.numCornerVertices = 0;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.generateLightingData = false;
            if (legacyBeamRenderer != null && legacyBeamRenderer.sharedMaterial != null)
                line.sharedMaterial = legacyBeamRenderer.sharedMaterial;
            return line;
        }

        private void ConfigurePointCounts(bool connected, int connectedSegmentCount)
        {
            if (data == null) return;
            int pointCount = connected
                ? Mathf.Max(1, connectedSegmentCount) * (data.VisualSegments - 1) + 1
                : data.AirVisualSegments;
            if (outerLine.positionCount != pointCount) outerLine.positionCount = pointCount;
            if (coreLine.positionCount != pointCount) coreLine.positionCount = pointCount;
            if (connected)
            {
                for (int i = 0; i < connectedStrands.Length; i++)
                    if (connectedStrands[i].positionCount != pointCount)
                        connectedStrands[i].positionCount = pointCount;
            }
            for (int i = 0; i < impactSparkLines.Count; i++)
                if (impactSparkLines[i].positionCount != 2)
                    impactSparkLines[i].positionCount = 2;
        }

        private void ConfigureSorting(SpriteRenderer weaponRenderer)
        {
            int sortingLayerId = weaponRenderer != null ? weaponRenderer.sortingLayerID : 0;
            int baseOrder = weaponRenderer != null ? weaponRenderer.sortingOrder : 0;
            int beamBaseOrder = data != null
                ? Mathf.Max(baseOrder + 1, data.beamSortingOrder)
                : baseOrder + 1;
            outerLine.sortingLayerID = sortingLayerId;
            outerLine.sortingOrder = beamBaseOrder;
            for (int i = 0; i < connectedStrands.Length; i++)
            {
                connectedStrands[i].sortingLayerID = sortingLayerId;
                connectedStrands[i].sortingOrder = beamBaseOrder + 1;
            }
            coreLine.sortingLayerID = sortingLayerId;
            coreLine.sortingOrder = beamBaseOrder + 2;
            PrepareEndpoint(startEffectRenderer, sortingLayerId, beamBaseOrder + 3);
            int sparkSortingOrder = data != null
                ? Mathf.Max(beamBaseOrder + 3, data.impactSparkSortingOrder)
                : beamBaseOrder + 3;
            for (int i = 0; i < impactSparkLines.Count; i++)
            {
                impactSparkLines[i].sortingLayerID = sortingLayerId;
                impactSparkLines[i].sortingOrder = sparkSortingOrder;
            }
        }

        private static void PrepareEndpoint(SpriteRenderer renderer, int sortingLayerId, int sortingOrder)
        {
            if (renderer == null) return;
            renderer.sortingLayerID = sortingLayerId;
            renderer.sortingOrder = sortingOrder;
        }
    }
}
