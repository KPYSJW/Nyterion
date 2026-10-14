using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Nytherion.GamePlay.Characters.Player;
using Nytherion.GamePlay.Combat;

namespace Nytherion.GamePlay.Characters.Enemy
{
    public class BossKnightCombatBehavior : MonoBehaviour, IEnemyCombatBehavior
    {
        public enum AttackMode
        {
            Random,
            Test
        }

        public enum BossAttackType
        {
            [InspectorName("1. 뇌진 강습")]
            ThunderAssault = 0,
            [InspectorName("2. 연속 방향 전환 돌진")]
            ChainDash = 1,
            [InspectorName("3. 과충전 추적")]
            OverchargedPursuit = 2,
            [InspectorName("4. 뇌광 횡단")]
            LightningCrossing = 3
        }

        [Header("Attack Selection / 공격 선택")]
        [Tooltip("Random: 완성된 공격을 랜덤 선택. Test: 지정한 공격만 반복. 변경은 다음 공격부터 적용됩니다.")]
        [SerializeField] private AttackMode attackMode = AttackMode.Random;
        [Tooltip("Test 모드에서만 사용합니다. 미완성 공격은 실행하지 않습니다.")]
        [SerializeField] private BossAttackType testAttack = BossAttackType.ThunderAssault;

        private static readonly BossAttackType[] AllAttacks =
        {
            BossAttackType.ThunderAssault,
            BossAttackType.ChainDash,
            BossAttackType.OverchargedPursuit,
            BossAttackType.LightningCrossing
        };
        private readonly List<BossAttackType> attackCandidates = new List<BossAttackType>(4);
        private BossAttackType? currentAttack;
        private BossAttackType? lastAttack;
        private BossAttackType? warnedUnavailableTestAttack;
        public BossAttackType? CurrentAttack => currentAttack;

        [Header("Thunder Assault / 뇌진 강습")]
        [SerializeField] private float cooldown=3f;
        [SerializeField] private float preparationDuration=1f;

        [SerializeField] private float dashSpeed = 18f;
        [SerializeField] private float dashDuration = 0.4f;
        [SerializeField, Min(0f)] private float wallStopDistance = 0.03f;
        [SerializeField] private bool logDashPosition = true;

        [Header("Chain Dash / 연속 방향 전환 돌진")]
        [SerializeField, Min(1)] private int chainDashCount = 3;
        [SerializeField, Min(0.01f)] private float chainDashSpeed = 18f;
        [SerializeField, Min(0.01f)] private float chainDashDuration = 0.4f;
        [SerializeField, Min(0f)] private float chainFirstPreparationDuration = 1f;
        [SerializeField, Min(0f)] private float chainReaimDuration = 0.35f;
        [SerializeField, Min(0f)] private float chainCooldown = 3f;

        [Header("Overcharged Pursuit / 과충전 추적")]
        [SerializeField, Min(0f)] private float overchargePreparationDuration = 1f;
        [SerializeField, Min(0.01f)] private float overchargePursuitDuration = 4f;
        [SerializeField, Min(0.01f)] private float overchargeStartSpeed = 6f;
        [SerializeField, Min(0.01f)] private float overchargeMaxSpeed = 18f;
        [SerializeField, Min(0.01f)] private float overchargeAcceleration = 24f;
        [SerializeField, Min(0f)] private float overchargeCooldown = 3f;
        [Header("Overcharge Shield / 번개 보호막 표시")]
        [Tooltip("2열 x 2행 보호막 시트입니다. 왼쪽 위부터 4프레임으로 재생합니다.")]
        [SerializeField] private Texture2D overchargeShieldTexture;
        [Tooltip("연결하면 기존 2x2 텍스처 대신 이 스프라이트를 순서대로 재생합니다.")]
        [SerializeField] private Sprite[] overchargeShieldSprites;
        [SerializeField, Min(0f)] private float overchargeShieldFramesPerSecond = 12f;
        [Tooltip("몸 크기에 대한 보호막 배율입니다. 원형 비율을 유지하며 과충전 공격 범위도 함께 바뀝니다.")]
        [SerializeField, Min(0.1f)] private float overchargeShieldScale = 1.6f;
        [Tooltip("보호막과 과충전 공격 범위를 함께 이동합니다. 월드 좌표 기준으로 Y가 양수면 위로 이동하며, 보스 몸의 콜라이더는 바뀌지 않습니다.")]
        [SerializeField] private Vector2 overchargeShieldOffset = Vector2.zero;
        [Tooltip("이미지 투명 여백을 제외하는 공격 반경 비율입니다. 씬의 초록 원이 실제 피해 범위입니다.")]
        [SerializeField, Range(0.1f, 1f)] private float overchargeShieldHitRadiusScale = 0.9f;
        [SerializeField, Min(0f)] private float overchargeElectricPadding = 0.1f;
        [SerializeField, Range(0f, 1f)] private float overchargeElectricAlpha = 0.85f;

        [Header("Lightning Crossing / 뇌광 횡단")]
        [SerializeField, Min(1)] private int crossingMinCount = 4;
        [SerializeField, Min(1)] private int crossingMaxCount = 5;
        [SerializeField, Min(0f)] private float crossingWarningDuration = 0.6f;
        [SerializeField, Min(0.01f)] private float crossingSpeed = 50f;
        [SerializeField, Min(0f)] private float crossingCooldown = 3f;
        [SerializeField, Min(0f)] private float crossingScreenMargin = 1f;
        [SerializeField, Min(1f)] private float crossingFallbackDistance = 15f;
        [Tooltip("비워 두면 Main Camera를 사용합니다. 벽/NavMesh 경계에서는 경로를 방 안으로 제한합니다.")]
        [SerializeField] private Camera crossingCamera;
        [Header("Crossing Departure / Return Effect")]
        [SerializeField] private SpriteRenderer crossingEffectRenderer;
        [SerializeField] private Sprite[] crossingEffectFrames;
        [SerializeField, Min(0f)] private float crossingDepartureDuration = 1.2f;
        [SerializeField, Min(0f)] private float crossingReturnDuration = 1.2f;
        [SerializeField] private Color crossingPixelColor = new Color(0.2f, 0.75f, 1f, 1f);
        [SerializeField, Min(1f)] private float crossingPixelSize = 5f;
        private float crossingTransitionStartedAt;
        private Animator crossingBodyAnimator;
        private float crossingOriginalAnimatorSpeed;
        private bool crossingAnimationFrozen;
        private SpriteRenderer crossingPixelRenderer;
        private Material crossingPixelMaterial;
        private MaterialPropertyBlock crossingPixelProperties;
        private static readonly int CrossingDissolveId = Shader.PropertyToID("_DissolveAmount");

        [Header("Dash Range Preview")]
        [SerializeField] private GameObject dashRangePreview;
        [Tooltip("콜라이더를 사용할 수 없을 때만 쓰는 예고 폭입니다. 정상 상태에서는 콜라이더에 맞춰 자동 계산합니다.")]
        [SerializeField, Min(0.05f)] private float previewWidth = 3.26f;
        private GoblinChargePreview previewController;
        private Transform previewOriginalParent;
        private Vector3 previewOriginalLocalPosition;
        private Quaternion previewOriginalLocalRotation;
        private Vector3 previewOriginalLocalScale;

        [Header("Running Foot Effect")]
        [SerializeField] private SpriteRenderer runEffectRenderer;
        [SerializeField] private Animator runEffectAnimator;
        [SerializeField, Min(0f)] private float runEffectMinimumSpeed = 0.15f;
        private Vector3 runEffectLastPosition;
        private bool runEffectHasLastPosition;

        [Header("Pattern Outline")]
        [SerializeField, InspectorName("1. 뇌진 강습 색상")]
        private Color patternOutlineColor = new Color(0.35f, 0.85f, 1f, 1f);
        [SerializeField, InspectorName("2. 연속 돌진 색상")]
        private Color chainDashOutlineColor = new Color(1f, 0.85f, 0.25f, 1f);
        [SerializeField, InspectorName("3. 과충전 추적 색상")]
        private Color overchargeOutlineColor = new Color(0.72f, 0.4f, 1f, 1f);
        [SerializeField, InspectorName("4. 뇌광 횡단 색상")]
        private Color crossingOutlineColor = new Color(1f, 0.25f, 0.3f, 1f);
        [SerializeField, Range(0f, 4f)] private float patternOutlineWidth = 1f;
        private MaterialPropertyBlock outlineProperties;
        private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
        private static readonly int OutlineWidthId = Shader.PropertyToID("_OutlineWidth");
        private static readonly int OutlineEnabledId = Shader.PropertyToID("_OutlineEnabled");

        [Header("Dash End Shockwave")]
        [SerializeField, Min(0.1f)] private float shockwaveRadius = 4f;
        [SerializeField, Min(0.01f)] private float shockwaveDuration = 0.5f;
        [SerializeField, Min(0f)] private float shockwaveDamage = 10f;
        [SerializeField, Min(0.01f)] private float shockwaveWidth = 0.15f;
        [SerializeField] private Color shockwaveColor = new Color(0.15f, 0.65f, 1f, 0.9f);
        [SerializeField] private Vector2 shockwaveLocalOffset = new Vector2(0f, -2.23f);
        [Header("Shockwave Lightning Animation")]
        [SerializeField] private Material shockwaveLightningMaterial;
        [SerializeField, Min(1)] private int shockwaveFrameCount = 6;
        [SerializeField, Min(0f)] private float shockwaveFramesPerSecond = 12f;
        [SerializeField, Min(1)] private int shockwaveTextureRepeat = 4;
        [SerializeField] private float shockwaveTextureScrollSpeed = 0.35f;
        private static readonly int WaveFrameCountId = Shader.PropertyToID("_FrameCount");
        private static readonly int WaveFrameIndexId = Shader.PropertyToID("_FrameIndex");
        private static readonly int WaveRepeatId = Shader.PropertyToID("_Repeat");
        private static readonly int WaveScrollId = Shader.PropertyToID("_Scroll");
        private const int ShockwaveSegments = 64;
        private readonly Vector3[] shockwavePoints = new Vector3[ShockwaveSegments];
        private EnemyBase enemyBase;
        private StatusEffectManager statusEffectManager;
        private sealed class ShockwaveInstance
        {
            public LineRenderer Line;
            public Material Material;
            public PlayerHealth Player;
            public Collider2D[] PlayerColliders;
            public Vector2 Origin;
            public float Elapsed;
            public float PreviousRadius;
            public bool Active;
            public bool HitPlayer;
        }
        private readonly List<ShockwaveInstance> shockwaves = new List<ShockwaveInstance>();

        private SpriteRenderer overchargeRenderer;
        private Material overchargeMaterial;
        private Sprite[] overchargeShieldFrames;
        private bool ownsOverchargeShieldFrames;
        private Texture2D loadedOverchargeShieldTexture;
        private Knight overchargeContact;
        private Transform shieldPlayer;
        private Collider2D[] shieldPlayerColliders;
        private UnityEngine.AI.NavMeshAgent overchargeAgent;
        private float originalOverchargeSpeed;
        private float originalOverchargeAcceleration;
        private float overchargeStartedAt;
        private float overchargeEndsAt;
        private float overchargeEffectStartedAt;

        private float dashTimeRemaining;
        private int remainingChainDashes;
        private float plannedDashDistance;
        private float activeDashSpeed;
        private Vector2 dashStartPosition;
        private float nextAttackTime;
        private float preparationEndTime;
        private Vector2 dashDirection;
        private Vector2 dashAimCenter;
        private EnemyAIController activeEnemy;
        private bool originalAgentUpdatePosition;
        private bool changedAgentUpdatePosition;
        private Vector3 bodyToAgentOffset;
        private Collider2D bodyCollider;
        private readonly RaycastHit2D[] castHits = new RaycastHit2D[12];
        private bool finishDashNextFixedUpdate;
        private bool blockedChaseMovement;
        private SpriteRenderer crossingSprite;
        private KnightMovementAfterimageVFX crossingAfterimages;
        private bool crossingOwnsVisibility;
        private bool crossingOriginalSpriteEnabled;
        private bool crossingOriginalColliderEnabled;
        private bool crossingOriginalAfterimagesEnabled;
        private int remainingCrossings;
        private float crossingTravelDistance;
        private Vector3 crossingColliderCenterOffset;
        private Vector3 crossingLastSafeBody;
        private Vector3 crossingLastSafeNav;
        private readonly Collider2D[] crossingOverlaps = new Collider2D[32];
        private readonly RaycastHit2D[] crossingPlayerHits = new RaycastHit2D[32];

        private enum AttackPhase
        {
            None,
            Preparing,
            Dashing,
            OverchargeCharging,
            OverchargePursuing,
            CrossingPreparing,
            CrossingDeparting,
            CrossingReturning
        }
        private AttackPhase currentPhase;
        public bool IsPreparing => isActiveAndEnabled &&
            (currentPhase == AttackPhase.Preparing || currentPhase == AttackPhase.OverchargeCharging) &&
            activeEnemy != null && activeEnemy.isActiveAndEnabled;
        public bool IsDashing => isActiveAndEnabled &&
            currentPhase == AttackPhase.Dashing &&
            activeEnemy != null && activeEnemy.isActiveAndEnabled;
        public bool IsOvercharging => isActiveAndEnabled &&
            (currentPhase == AttackPhase.OverchargeCharging || currentPhase == AttackPhase.OverchargePursuing) &&
            activeEnemy != null && activeEnemy.isActiveAndEnabled &&
            (enemyBase == null || !enemyBase.isDead);
        public bool IsOverchargePursuing => IsOvercharging &&
            currentPhase == AttackPhase.OverchargePursuing;
        public bool IsCrossingHidden => crossingOwnsVisibility &&
            currentPhase == AttackPhase.CrossingPreparing;
        public bool IsCrossingTransition => currentPhase == AttackPhase.CrossingDeparting ||
            currentPhase == AttackPhase.CrossingReturning;

        public bool TryGetDashMotion(out Vector2 direction, out float speed)
        {
            direction = dashDirection;
            speed = activeDashSpeed;
            return IsDashing && (enemyBase == null || !enemyBase.isDead) &&
                direction.sqrMagnitude > 0.001f && speed > 0f;
        }

        private void Awake()
        {
            bodyCollider = GetComponent<Collider2D>();
            enemyBase = GetComponent<EnemyBase>();
            overchargeContact = GetComponent<Knight>();
            crossingSprite = GetComponent<SpriteRenderer>();
            crossingBodyAnimator = GetComponent<Animator>();
            crossingAfterimages = GetComponent<KnightMovementAfterimageVFX>();
            if (dashRangePreview == null) return;

            previewController = dashRangePreview.GetComponent<GoblinChargePreview>();
            Transform previewTransform = dashRangePreview.transform;
            previewOriginalParent = previewTransform.parent;
            previewOriginalLocalPosition = previewTransform.localPosition;
            previewOriginalLocalRotation = previewTransform.localRotation;
            previewOriginalLocalScale = previewTransform.localScale;
            HideDashRangePreview();
        }

        public bool ShouldEnterAttackState(EnemyAIController enemy)
        {
            if (Time.time < nextAttackTime) return false;

            if (attackMode == AttackMode.Test && !IsAttackImplemented(testAttack))
            {
                WarnUnavailableTestAttack();
                return false;
            }

            warnedUnavailableTestAttack = null;
            return true;
        }

        private static bool IsAttackImplemented(BossAttackType attack)
        {
            // Add each pattern here only after its execution is implemented.
            return attack == BossAttackType.ThunderAssault || attack == BossAttackType.ChainDash ||
                attack == BossAttackType.OverchargedPursuit || attack == BossAttackType.LightningCrossing;
        }

        private void WarnUnavailableTestAttack()
        {
            if (warnedUnavailableTestAttack == testAttack) return;
            warnedUnavailableTestAttack = testAttack;
            Debug.LogWarning(
                $"[KnightBoss] 테스트 공격 {(int)testAttack + 1}번 ({testAttack})은 아직 미완성입니다. " +
                "유효한 Test Attack을 선택하거나 Attack Mode를 Random으로 바꿔주세요.", this);
        }

        private bool TrySelectAttack(out BossAttackType selected)
        {
            selected = BossAttackType.ThunderAssault;
            if (attackMode == AttackMode.Test)
            {
                if (!IsAttackImplemented(testAttack))
                {
                    WarnUnavailableTestAttack();
                    return false;
                }
                selected = testAttack;
                return true;
            }

            attackCandidates.Clear();
            foreach (BossAttackType attack in AllAttacks)
            {
                if (IsAttackImplemented(attack)) attackCandidates.Add(attack);
            }

            // With only one implemented attack, allow it to repeat rather than
            // leaving the boss with no valid attack. Test mode ignores this rule.
            if (attackCandidates.Count > 1 && lastAttack.HasValue)
                attackCandidates.Remove(lastAttack.Value);
            if (attackCandidates.Count == 0) return false;

            selected = attackCandidates[Random.Range(0, attackCandidates.Count)];
            return true;
        }

        public void EnterAttackState(EnemyAIController enemy)
        {
            // Select once per entire pattern; never reselect during its phases.
            if (!TrySelectAttack(out BossAttackType selected))
            {
                enemy.TransitionToState(enemy.chaseState);
                return;
            }
            currentAttack = selected;
            lastAttack = selected;
            activeEnemy = enemy;

            switch (selected)
            {
                case BossAttackType.ThunderAssault:
                    BeginThunderAssault(enemy);
                    break;
                case BossAttackType.ChainDash:
                    remainingChainDashes = Mathf.Max(1, chainDashCount);
                    BeginDashPreparation(enemy, chainFirstPreparationDuration);
                    break;
                case BossAttackType.OverchargedPursuit:
                    BeginOvercharge(enemy);
                    break;
                case BossAttackType.LightningCrossing:
                    BeginLightningCrossing(enemy);
                    break;
                default:
                    enemy.TransitionToState(enemy.chaseState);
                    break;
            }
        }

        private void BeginThunderAssault(EnemyAIController enemy)
        {
            BeginDashPreparation(enemy, preparationDuration);
        }

        private void BeginDashPreparation(EnemyAIController enemy, float waitDuration)
        {
            blockedChaseMovement = true;
            HoldPositionDuringPreparation(enemy);
            currentPhase = AttackPhase.Preparing;
            preparationEndTime = Time.time + Mathf.Max(0f, waitDuration);

            RefreshPreparationAim(enemy);
            ShowDashRangePreview(enemy);
        }

        private void RefreshPreparationAim(EnemyAIController enemy)
        {
            // Called only while preparing. Once dashing, keep the last
            // displayed direction so the boss follows the telegraphed line.
            if (bodyCollider == null) bodyCollider = GetComponent<Collider2D>();
            dashAimCenter = bodyCollider != null && bodyCollider.isActiveAndEnabled
                ? (Vector2)bodyCollider.bounds.center
                : GetCurrentPosition(enemy);

            Vector2 targetCenter = Vector2.zero;
            if (enemy.player != null)
            {
                Collider2D playerCollider = enemy.player.GetComponent<Collider2D>();
                targetCenter = playerCollider != null && playerCollider.isActiveAndEnabled &&
                    !playerCollider.isTrigger
                    ? (Vector2)playerCollider.bounds.center
                    : (Vector2)enemy.player.position;
            }
            dashDirection = enemy.player != null
                ? (targetCenter - dashAimCenter).normalized
                : Vector2.zero;
            if (dashDirection.sqrMagnitude > 0.001f)
                enemy.SetForcedFacingDirection(dashDirection);
            else
                enemy.ClearForcedFacingDirection();
        }

        public void UpdateAttackState(EnemyAIController enemy)
        {
            if (IsCrossingTransition)
            {
                HoldPositionDuringPreparation(enemy);
                UpdateCrossingTransitionEffect();
                float duration = currentPhase == AttackPhase.CrossingDeparting
                    ? crossingDepartureDuration : crossingReturnDuration;
                if (Time.time - crossingTransitionStartedAt >= Mathf.Max(0f, duration))
                {
                    HideCrossingTransitionEffect();
                    if (currentPhase == AttackPhase.CrossingDeparting)
                    {
                        if (!PrepareNextCrossing(enemy)) FinishCrossingWithoutPath(enemy);
                    }
                    else enemy.TransitionToState(enemy.chaseState);
                }
                return;
            }
            if (currentPhase == AttackPhase.CrossingPreparing)
            {
                // This telegraph is locked at creation, unlike attacks 1/2.
                HoldPositionDuringPreparation(enemy);
                if (Time.time >= preparationEndTime) BeginDash(enemy);
                return;
            }
            if (currentPhase == AttackPhase.OverchargeCharging)
            {
                HoldPositionDuringPreparation(enemy);
                RefreshPreparationAim(enemy);
                if (Time.time >= preparationEndTime) BeginOverchargePursuit(enemy);
                return;
            }

            if (currentPhase == AttackPhase.OverchargePursuing)
            {
                UpdateOverchargePursuit(enemy);
                return;
            }

            if(currentPhase==AttackPhase.Preparing)
            {
                HoldPositionDuringPreparation(enemy);
                RefreshPreparationAim(enemy);
                ShowDashRangePreview(enemy);
                if(Time.time>=preparationEndTime)
                {
                    BeginDash(enemy);
                }
                return;
            }

            if (currentPhase == AttackPhase.Dashing)
                SyncAgentPosition(enemy);
        }

        private void BeginLightningCrossing(EnemyAIController enemy)
        {
            if (enemy.player == null || enemy.rb == null || !enemy.rb.simulated ||
                enemy.agent == null || !enemy.agent.isActiveAndEnabled || !enemy.agent.isOnNavMesh)
            {
                enemy.TransitionToState(enemy.chaseState);
                return;
            }
            int minimum = Mathf.Max(1, crossingMinCount);
            int maximum = Mathf.Max(minimum, crossingMaxCount);
            remainingCrossings = Random.Range(minimum, maximum + 1);
            blockedChaseMovement = true;
            HoldPositionDuringPreparation(enemy);
            bodyToAgentOffset = GetBodyWorldPosition(enemy) - enemy.agent.nextPosition;
            crossingColliderCenterOffset = bodyCollider != null && bodyCollider.isActiveAndEnabled
                ? bodyCollider.bounds.center - GetBodyWorldPosition(enemy) : Vector3.zero;
            crossingLastSafeBody = GetBodyWorldPosition(enemy);
            crossingLastSafeNav = enemy.agent.nextPosition;
            originalAgentUpdatePosition = enemy.agent.updatePosition;
            enemy.agent.updatePosition = false;
            changedAgentUpdatePosition = true;

            crossingOriginalSpriteEnabled = crossingSprite != null && crossingSprite.enabled;
            crossingOriginalColliderEnabled = bodyCollider != null && bodyCollider.enabled;
            crossingOriginalAfterimagesEnabled = crossingAfterimages != null && crossingAfterimages.enabled;
            crossingOwnsVisibility = true;
            BeginCrossingTransition(enemy, false);
        }

        private void BeginCrossingTransition(EnemyAIController enemy, bool returning)
        {
            currentPhase = returning ? AttackPhase.CrossingReturning : AttackPhase.CrossingDeparting;
            crossingTransitionStartedAt = Time.time;
            activeDashSpeed = 0f;
            dashTimeRemaining = 0f;
            finishDashNextFixedUpdate = false;
            HoldPositionDuringPreparation(enemy);
            HideDashRangePreview();
            SetRunEffectVisible(false);
            SetCrossingVisible(!returning);
            // Visible departure is cosmetic: neither transition deals contact damage.
            if (bodyCollider != null) bodyCollider.enabled = false;
            if (crossingAfterimages != null) crossingAfterimages.enabled = false;
            BeginCrossingPixelEffect();
            UpdateCrossingTransitionEffect();
        }

        private void BeginCrossingPixelEffect()
        {
            if (crossingBodyAnimator != null && !crossingAnimationFrozen)
            {
                crossingOriginalAnimatorSpeed = crossingBodyAnimator.speed;
                crossingAnimationFrozen = true;
                crossingBodyAnimator.speed = 0f;
            }
            if (crossingSprite == null || crossingSprite.sprite == null) return;
            if (crossingPixelMaterial == null)
            {
                Shader shader = Resources.Load<Shader>("EnemyPixelDissolve");
                if (shader == null)
                {
                    Debug.LogWarning("[KnightBoss Crossing] EnemyPixelDissolve 셰이더를 찾을 수 없습니다.", this);
                    return;
                }
                // A separate frozen sprite avoids conflicts with hit flashes and actual death.
                crossingPixelMaterial = new Material(shader) { name = "Knight Boss Crossing Pixels (Runtime)" };
                GameObject pixelObject = new GameObject("Crossing Frozen Pixels");
                pixelObject.layer = gameObject.layer;
                pixelObject.transform.SetParent(crossingSprite.transform, false);
                crossingPixelRenderer = pixelObject.AddComponent<SpriteRenderer>();
                crossingPixelRenderer.sharedMaterial = crossingPixelMaterial;
                crossingPixelProperties = new MaterialPropertyBlock();
            }
            Sprite sprite = crossingSprite.sprite;
            crossingPixelRenderer.sprite = sprite;
            crossingPixelRenderer.color = crossingSprite.color;
            crossingPixelRenderer.flipX = crossingSprite.flipX;
            crossingPixelRenderer.flipY = crossingSprite.flipY;
            crossingPixelRenderer.sortingLayerID = crossingSprite.sortingLayerID;
            crossingPixelRenderer.sortingOrder = crossingSprite.sortingOrder;
            Vector2 uvMin = Vector2.one, uvMax = Vector2.zero;
            foreach (Vector2 uv in sprite.uv)
            {
                uvMin = Vector2.Min(uvMin, uv);
                uvMax = Vector2.Max(uvMax, uv);
            }
            float pixelsPerCell = Mathf.Max(1f, crossingPixelSize * sprite.pixelsPerUnit / 32f);
            crossingPixelProperties.Clear();
            crossingPixelProperties.SetTexture("_MainTex", sprite.texture);
            crossingPixelProperties.SetVector("_SpriteUVRect",
                new Vector4(uvMin.x, uvMin.y, uvMax.x - uvMin.x, uvMax.y - uvMin.y));
            crossingPixelProperties.SetVector("_PixelGrid", new Vector4(
                Mathf.Max(1f, sprite.rect.width / pixelsPerCell),
                Mathf.Max(1f, sprite.rect.height / pixelsPerCell), 0f, 0f));
            crossingPixelProperties.SetColor("_FragmentColor", crossingPixelColor);
            crossingPixelProperties.SetFloat("_Seed", Random.value);
            crossingPixelProperties.SetFloat(CrossingDissolveId,
                currentPhase == AttackPhase.CrossingReturning ? 1f : 0f);
            crossingPixelRenderer.SetPropertyBlock(crossingPixelProperties);
            crossingPixelRenderer.enabled = true;
            crossingSprite.enabled = false;
        }

        private void UpdateCrossingTransitionEffect()
        {
            if (IsCrossingTransition && crossingPixelRenderer != null && crossingPixelRenderer.enabled)
            {
                bool appearing = currentPhase == AttackPhase.CrossingReturning;
                float pixelDuration = appearing ? crossingReturnDuration : crossingDepartureDuration;
                float pixelProgress = Mathf.Clamp01((Time.time - crossingTransitionStartedAt) /
                    Mathf.Max(0.01f, pixelDuration));
                crossingPixelProperties.SetFloat(CrossingDissolveId, appearing ? 1f - pixelProgress : pixelProgress);
                crossingPixelRenderer.SetPropertyBlock(crossingPixelProperties);
            }
            if (crossingEffectRenderer == null) return;
            if (!IsCrossingTransition || crossingEffectFrames == null || crossingEffectFrames.Length == 0)
            {
                crossingEffectRenderer.enabled = false;
                return;
            }
            bool returning = currentPhase == AttackPhase.CrossingReturning;
            float duration = returning ? crossingReturnDuration : crossingDepartureDuration;
            float progress = Mathf.Clamp01((Time.time - crossingTransitionStartedAt) / Mathf.Max(0.01f, duration));
            int frame = Mathf.Min(crossingEffectFrames.Length - 1,
                Mathf.FloorToInt(progress * crossingEffectFrames.Length));
            if (returning) frame = crossingEffectFrames.Length - 1 - frame;
            crossingEffectRenderer.sprite = crossingEffectFrames[frame];
            if (crossingSprite != null)
            {
                crossingEffectRenderer.sortingLayerID = crossingSprite.sortingLayerID;
                crossingEffectRenderer.sortingOrder = crossingSprite.sortingOrder + 1;
            }
            crossingEffectRenderer.enabled = true;
        }

        private void HideCrossingTransitionEffect()
        {
            if (crossingEffectRenderer != null) crossingEffectRenderer.enabled = false;
            if (crossingPixelRenderer != null) crossingPixelRenderer.enabled = false;
            if (crossingAnimationFrozen)
            {
                if (crossingBodyAnimator != null) crossingBodyAnimator.speed = crossingOriginalAnimatorSpeed;
                crossingAnimationFrozen = false;
            }
        }

        private void SetCrossingVisible(bool visible)
        {
            if (!crossingOwnsVisibility) return;
            if (crossingSprite != null) crossingSprite.enabled = visible && crossingOriginalSpriteEnabled;
            if (bodyCollider != null) bodyCollider.enabled = visible && crossingOriginalColliderEnabled;
            // OnDisable recycles old ghosts; OnEnable resets their position,
            // so hidden teleports never produce a trail across the room.
            if (crossingAfterimages != null)
                crossingAfterimages.enabled = visible && crossingOriginalAfterimagesEnabled;
        }

        private void RestoreCrossingVisibility()
        {
            if (!crossingOwnsVisibility) return;
            SetCrossingVisible(true);
            crossingOwnsVisibility = false;
        }

        private bool PrepareNextCrossing(EnemyAIController enemy)
        {
            currentPhase = AttackPhase.CrossingPreparing;
            SetCrossingVisible(false);
            HideDashRangePreview();
            HoldPositionDuringPreparation(enemy);
            if (enemy.player == null || enemy.agent == null || !enemy.agent.isOnNavMesh) return false;

            Collider2D playerCollider = enemy.player.GetComponent<Collider2D>();
            Vector3 playerCenter = playerCollider != null && playerCollider.isActiveAndEnabled
                ? playerCollider.bounds.center : enemy.player.position;
            Vector3 requestedNav = playerCenter - crossingColliderCenterOffset - bodyToAgentOffset;
            requestedNav.z = enemy.agent.nextPosition.z;
            NavMeshQueryFilter filter = new NavMeshQueryFilter
                { agentTypeID = enemy.agent.agentTypeID, areaMask = enemy.agent.areaMask };
            if (!NavMesh.SamplePosition(requestedNav, out NavMeshHit anchor,
                Mathf.Max(1f, enemy.agent.radius * 2f), filter)) return false;

            GetCrossingScreenBounds(playerCenter, out Vector2 screenMin, out Vector2 screenMax);
            Vector3 anchorBody = anchor.position + bodyToAgentOffset;
            Vector3 anchorCenter = anchorBody + crossingColliderCenterOffset;
            float margin = Mathf.Max(0f, crossingScreenMargin) + enemy.agent.radius;
            for (int attempt = 0; attempt < 12; attempt++)
            {
                float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                if (!TryGetCrossingScreenDistances(anchorCenter, direction,
                    screenMin - Vector2.one * margin, screenMax + Vector2.one * margin,
                    out float startDistance, out float endDistance)) continue;
                startDistance = TraceCrossingNavDistance(anchor.position, -direction, Mathf.Max(0f, startDistance), filter);
                endDistance = TraceCrossingNavDistance(anchor.position, direction, Mathf.Max(0f, endDistance), filter);
                if (startDistance + endDistance < 1f) continue;
                dashDirection = direction;
                enemy.SetForcedFacingDirection(direction);
                if (!TryPlaceCrossingStart(enemy, anchor.position, direction, startDistance, endDistance)) continue;
                preparationEndTime = Time.time + Mathf.Max(0f, crossingWarningDuration);
                return true;
            }
            return false;
        }

        // Intersect the full crossing line with the screen rectangle, including diagonal paths.
        private static bool TryGetCrossingScreenDistances(Vector2 center, Vector2 direction,
            Vector2 minimum, Vector2 maximum, out float startDistance, out float endDistance)
        {
            startDistance = endDistance = 0f;
            float near = float.NegativeInfinity, far = float.PositiveInfinity;
            for (int axis = 0; axis < 2; axis++)
            {
                if (Mathf.Abs(direction[axis]) < 0.00001f)
                {
                    if (center[axis] < minimum[axis] || center[axis] > maximum[axis]) return false;
                    continue;
                }
                float first = (minimum[axis] - center[axis]) / direction[axis];
                float second = (maximum[axis] - center[axis]) / direction[axis];
                near = Mathf.Max(near, Mathf.Min(first, second));
                far = Mathf.Min(far, Mathf.Max(first, second));
                if (near > far) return false;
            }
            startDistance = Mathf.Max(0f, -near);
            endDistance = Mathf.Max(0f, far);
            return startDistance + endDistance > 0f;
        }

        private void GetCrossingScreenBounds(Vector3 playerCenter, out Vector2 minimum, out Vector2 maximum)
        {
            minimum = (Vector2)playerCenter - Vector2.one * crossingFallbackDistance;
            maximum = (Vector2)playerCenter + Vector2.one * crossingFallbackDistance;
            Camera camera = crossingCamera != null ? crossingCamera : Camera.main;
            if (camera == null) return;
            Plane plane = new Plane(Vector3.forward, new Vector3(0f, 0f, playerCenter.z));
            Vector2 low = Vector2.one * float.PositiveInfinity;
            Vector2 high = Vector2.one * float.NegativeInfinity;
            for (int i = 0; i < 4; i++)
            {
                Ray ray = camera.ViewportPointToRay(new Vector3(i % 2, i / 2, 0f));
                if (!plane.Raycast(ray, out float distance) || distance < 0f) return;
                Vector2 point = ray.GetPoint(distance);
                low = Vector2.Min(low, point);
                high = Vector2.Max(high, point);
            }
            minimum = low;
            maximum = high;
        }

        private static bool IsCrossingNavPointValid(Vector3 anchor, Vector3 point, NavMeshQueryFilter filter)
        {
            return NavMesh.SamplePosition(point, out NavMeshHit sample, 0.05f, filter) &&
                Vector3.Distance(point, sample.position) <= 0.05f &&
                !NavMesh.Raycast(anchor, point, out _, filter);
        }

        private float TraceCrossingNavDistance(Vector3 anchor, Vector2 direction, float requested, NavMeshQueryFilter filter)
        {
            float distance = 0f;
            // Test actual points rather than letting an off-mesh endpoint be
            // silently projected to a different row or disconnected island.
            for (int i = 0; i < 256 && distance < requested; i++)
            {
                float next = Mathf.Min(requested, distance + 0.5f);
                if (IsCrossingNavPointValid(anchor, anchor + (Vector3)direction * next, filter))
                {
                    distance = next;
                    continue;
                }
                float low = distance, high = next;
                for (int refinement = 0; refinement < 8; refinement++)
                {
                    float middle = (low + high) * 0.5f;
                    if (IsCrossingNavPointValid(anchor, anchor + (Vector3)direction * middle, filter)) low = middle;
                    else high = middle;
                }
                distance = low;
                break;
            }
            // SamplePosition accepts a 0.05-unit projection; step farther
            // inward than that tolerance so the returned point is truly on mesh.
            return Mathf.Max(0f, distance - Mathf.Max(0.06f, wallStopDistance));
        }

        private bool TryPlaceCrossingStart(EnemyAIController enemy, Vector3 anchor, Vector2 direction,
            float startDistance, float endDistance)
        {
            for (int attempt = 0; attempt <= 16; attempt++)
            {
                float retreat = Mathf.Min(startDistance, attempt * 0.25f);
                Vector3 navPosition = anchor - (Vector3)direction * (startDistance - retreat);
                if (!enemy.agent.Warp(navPosition)) continue;
                Vector3 bodyPosition = navPosition + bodyToAgentOffset;
                bodyPosition.z = crossingLastSafeBody.z;
                enemy.rb.position = bodyPosition;
                enemy.transform.position = bodyPosition;
                enemy.agent.nextPosition = navPosition;
                bool enabled = bodyCollider != null && bodyCollider.enabled;
                try
                {
                    if (bodyCollider != null && crossingOriginalColliderEnabled) bodyCollider.enabled = true;
                    Physics2D.SyncTransforms();
                    bool overlapsWall = false;
                    if (bodyCollider != null && bodyCollider.isActiveAndEnabled)
                    {
                        int count = bodyCollider.OverlapCollider(new ContactFilter2D { useTriggers = false }, crossingOverlaps);
                        for (int i = 0; i < count; i++)
                            if (crossingOverlaps[i] != null && crossingOverlaps[i].CompareTag(Nytherion.Core.Systems.Tags.Wall))
                                overlapsWall = true;
                    }
                    if (overlapsWall) continue;
                    crossingTravelDistance = startDistance + endDistance - retreat;
                    dashAimCenter = bodyCollider != null && bodyCollider.isActiveAndEnabled
                        ? (Vector2)bodyCollider.bounds.center : (Vector2)bodyPosition;
                    RefreshDashPlan();
                    if (plannedDashDistance < 0.1f) continue;
                    crossingTravelDistance = plannedDashDistance;
                    ShowDashRangePreview(enemy);
                    crossingLastSafeBody = bodyPosition;
                    crossingLastSafeNav = navPosition;
                    return true;
                }
                finally
                {
                    if (bodyCollider != null) bodyCollider.enabled = enabled;
                }
            }
            return false;
        }

        private void FinishCrossingWithoutPath(EnemyAIController enemy)
        {
            // A narrow room may have no valid crossing. Restore a known safe
            // position and visibility, never leave the boss hidden or off-mesh.
            if (enemy.agent != null && enemy.agent.isActiveAndEnabled)
                enemy.agent.Warp(crossingLastSafeNav);
            if (enemy.rb != null) enemy.rb.position = crossingLastSafeBody;
            enemy.transform.position = crossingLastSafeBody;
            Physics2D.SyncTransforms();
            Debug.LogWarning("[KnightBoss Crossing] 유효한 횡단 경로가 없어 이번 패턴을 종료합니다.", this);
            BeginCrossingTransition(enemy, true);
        }

        private void BeginOvercharge(EnemyAIController enemy)
        {
            HideDashRangePreview();
            blockedChaseMovement = true;
            HoldPositionDuringPreparation(enemy);
            currentPhase = AttackPhase.OverchargeCharging;
            preparationEndTime = Time.time + Mathf.Max(0f, overchargePreparationDuration);
            RefreshPreparationAim(enemy);
            EnsureOverchargeElectricity();
            if (overchargeRenderer != null) overchargeRenderer.enabled = false;
        }

        private void BeginOverchargePursuit(EnemyAIController enemy)
        {
            if (enemy.player == null || enemy.agent == null ||
                !enemy.agent.isActiveAndEnabled || !enemy.agent.isOnNavMesh)
            {
                enemy.TransitionToState(enemy.chaseState);
                return;
            }

            overchargeAgent = enemy.agent;
            originalOverchargeSpeed = overchargeAgent.speed;
            originalOverchargeAcceleration = overchargeAgent.acceleration;
            overchargeStartedAt = Time.time;
            overchargeEffectStartedAt = Time.time;
            overchargeEndsAt = Time.time + Mathf.Max(0.01f, overchargePursuitDuration);
            currentPhase = AttackPhase.OverchargePursuing;
            enemy.ClearForcedFacingDirection();
            enemy.SetMovementAllowed(true);
            UpdateOverchargePursuit(enemy);
        }

        private void UpdateOverchargePursuit(EnemyAIController enemy)
        {
            if (Time.time >= overchargeEndsAt || enemy.player == null ||
                overchargeAgent == null || !overchargeAgent.isActiveAndEnabled ||
                !overchargeAgent.isOnNavMesh)
            {
                enemy.TransitionToState(enemy.chaseState);
                return;
            }

            // Reuse normal pathfinding/turning/contact knockback. Knight writes
            // its normal speed here, so apply the temporary override afterwards.
            enemy.MoveTowardsPlayer();
            float progress = Mathf.Clamp01((Time.time - overchargeStartedAt) /
                Mathf.Max(0.01f, overchargeEndsAt - overchargeStartedAt));
            float startSpeed = Mathf.Max(0.01f, overchargeStartSpeed);
            overchargeAgent.speed = Mathf.Lerp(startSpeed, Mathf.Max(startSpeed, overchargeMaxSpeed), progress);
            overchargeAgent.acceleration = Mathf.Max(0.01f, overchargeAcceleration);
        }

        private void EnsureOverchargeElectricity()
        {
            bool hasSpriteFrames = overchargeShieldSprites != null && overchargeShieldSprites.Length > 0;
            if (!hasSpriteFrames && (overchargeShieldTexture == null || overchargeShieldTexture.width % 2 != 0 ||
                overchargeShieldTexture.height % 2 != 0))
            {
                if (overchargeRenderer != null) overchargeRenderer.enabled = false;
                Debug.LogWarning("[KnightBoss] 유효한 2x2 보호막 이미지를 연결해주세요.", this);
                return;
            }

            if (hasSpriteFrames)
            {
                if (overchargeShieldFrames != overchargeShieldSprites)
                {
                    ClearOverchargeShieldFrames();
                    overchargeShieldFrames = overchargeShieldSprites;
                }
            }
            else if (overchargeShieldFrames == null || !ownsOverchargeShieldFrames ||
                loadedOverchargeShieldTexture != overchargeShieldTexture)
            {
                ClearOverchargeShieldFrames();
                int width = overchargeShieldTexture.width / 2;
                int height = overchargeShieldTexture.height / 2;
                overchargeShieldFrames = new Sprite[4];
                ownsOverchargeShieldFrames = true;
                for (int i = 0; i < overchargeShieldFrames.Length; i++)
                {
                    int column = i % 2;
                    int row = i / 2;
                    // Sprite rectangles start at the texture's bottom left.
                    Rect rect = new Rect(column * width,
                        overchargeShieldTexture.height - (row + 1) * height, width, height);
                    overchargeShieldFrames[i] = Sprite.Create(overchargeShieldTexture,
                        rect, new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
                    overchargeShieldFrames[i].name = $"Knight Boss Shield Frame {i + 1}";
                }
                loadedOverchargeShieldTexture = overchargeShieldTexture;
            }

            if (overchargeRenderer != null) return;
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) return;
            overchargeMaterial = new Material(shader);
            overchargeMaterial.name = "Knight Boss Lightning Shield (Runtime)";
            GameObject electricity = new GameObject("Knight Boss Lightning Shield");
            electricity.layer = gameObject.layer;
            overchargeRenderer = electricity.AddComponent<SpriteRenderer>();
            overchargeRenderer.enabled = false;
            overchargeRenderer.sharedMaterial = overchargeMaterial;
            SpriteRenderer sprite = GetComponent<SpriteRenderer>();
            if (sprite != null)
            {
                overchargeRenderer.sortingLayerID = sprite.sortingLayerID;
                overchargeRenderer.sortingOrder = sprite.sortingOrder + 1;
            }
        }

        private void ClearOverchargeShieldFrames()
        {
            if (ownsOverchargeShieldFrames && overchargeShieldFrames != null)
            {
                foreach (Sprite frame in overchargeShieldFrames)
                    if (frame != null) Destroy(frame);
            }
            overchargeShieldFrames = null;
            ownsOverchargeShieldFrames = false;
            loadedOverchargeShieldTexture = null;
        }

        private void LateUpdate()
        {
            if (IsCrossingTransition) UpdateCrossingTransitionEffect();
            UpdateRunEffect();
            SetPatternOutline(isActiveAndEnabled && currentAttack.HasValue &&
                currentPhase != AttackPhase.None && activeEnemy != null &&
                activeEnemy.isActiveAndEnabled && (enemyBase == null || !enemyBase.isDead));
            if (!IsOverchargePursuing || activeEnemy == null || !activeEnemy.isActiveAndEnabled ||
                (enemyBase != null && enemyBase.isDead))
            {
                if (overchargeRenderer != null) overchargeRenderer.enabled = false;
                return;
            }
            if (overchargeRenderer == null || overchargeShieldFrames == null) return;

            GetOverchargeShieldGeometry(out Vector3 center, out float diameter);
            float elapsed = Mathf.Max(0f, Time.time - overchargeEffectStartedAt);
            int frameIndex = Mathf.FloorToInt(elapsed * Mathf.Max(0f, overchargeShieldFramesPerSecond)) %
                overchargeShieldFrames.Length;
            Sprite shield = overchargeShieldFrames[frameIndex];
            overchargeRenderer.sprite = shield;
            float scale = diameter / Mathf.Max(0.01f, Mathf.Max(shield.bounds.size.x, shield.bounds.size.y));
            overchargeRenderer.transform.SetPositionAndRotation(center, Quaternion.identity);
            // Uniform scale keeps the original circular art round, even if the
            // character collider is wider than it is tall. Damage is checked separately.
            overchargeRenderer.transform.localScale = Vector3.one * scale;
            Color color = Color.white;
            color.a = overchargeElectricAlpha;
            overchargeRenderer.color = color;
            overchargeRenderer.enabled = true;
            if (IsOverchargePursuing)
                CheckOverchargeShieldHit(center, diameter * 0.5f * overchargeShieldHitRadiusScale);
        }

        private void UpdateRunEffect()
        {
            Vector3 position = transform.position;
            float distance = runEffectHasLastPosition
                ? Vector2.Distance(runEffectLastPosition, position) : 0f;
            runEffectLastPosition = position;
            runEffectHasLastPosition = true;
            bool visible = isActiveAndEnabled && crossingSprite != null && crossingSprite.enabled &&
                (enemyBase == null || !enemyBase.isDead) && !IsPreparing && !IsCrossingHidden && !IsCrossingTransition &&
                Time.deltaTime > 0f && distance / Time.deltaTime > runEffectMinimumSpeed;
            SetRunEffectVisible(visible);
        }

        private void SetRunEffectVisible(bool visible)
        {
            if (runEffectRenderer == null) return;
            if (crossingSprite != null)
            {
                runEffectRenderer.sortingLayerID = crossingSprite.sortingLayerID;
                runEffectRenderer.sortingOrder = crossingSprite.sortingOrder - 1;
            }
            if (runEffectAnimator != null && runEffectAnimator.enabled != visible)
            {
                runEffectAnimator.enabled = visible;
                if (visible && runEffectAnimator.runtimeAnimatorController != null)
                    runEffectAnimator.Play("Boss_knight_runeffect", 0, 0f);
            }
            runEffectRenderer.enabled = visible;
        }

        private void SetPatternOutline(bool enabled)
        {
            if (crossingSprite == null) return;
            if (outlineProperties == null) outlineProperties = new MaterialPropertyBlock();
            // Preserve hit-flash and other per-renderer properties.
            crossingSprite.GetPropertyBlock(outlineProperties);
            outlineProperties.SetColor(OutlineColorId, GetPatternOutlineColor());
            outlineProperties.SetFloat(OutlineWidthId, patternOutlineWidth);
            outlineProperties.SetFloat(OutlineEnabledId, enabled ? 1f : 0f);
            crossingSprite.SetPropertyBlock(outlineProperties);
        }

        private Color GetPatternOutlineColor()
        {
            switch (currentAttack)
            {
                case BossAttackType.ChainDash: return chainDashOutlineColor;
                case BossAttackType.OverchargedPursuit: return overchargeOutlineColor;
                case BossAttackType.LightningCrossing: return crossingOutlineColor;
                default: return patternOutlineColor;
            }
        }

        private void GetOverchargeShieldGeometry(out Vector3 center, out float diameter)
        {
            center = activeEnemy != null ? GetBodyWorldPosition(activeEnemy) : transform.position;
            Vector2 extents = Vector2.one;
            Collider2D collider = bodyCollider != null ? bodyCollider : GetComponent<Collider2D>();
            if (collider != null && collider.isActiveAndEnabled)
            {
                Bounds bounds = collider.bounds;
                center.x = bounds.center.x;
                center.y = bounds.center.y;
                extents = bounds.extents;
            }
            diameter = 2f * (Mathf.Max(extents.x, extents.y) + overchargeElectricPadding) *
                Mathf.Max(0.1f, overchargeShieldScale);
            center += (Vector3)overchargeShieldOffset;
        }

        private void CheckOverchargeShieldHit(Vector2 center, float radius)
        {
            if (activeEnemy.player == null || overchargeContact == null || radius <= 0f) return;
            if (shieldPlayer != activeEnemy.player || shieldPlayerColliders == null)
            {
                shieldPlayer = activeEnemy.player;
                shieldPlayerColliders = shieldPlayer.GetComponentsInChildren<Collider2D>();
            }
            foreach (Collider2D collider in shieldPlayerColliders)
            {
                // Only the player's physical body, not weapon/interaction triggers.
                if (collider == null || !collider.isActiveAndEnabled || collider.isTrigger) continue;
                Vector2 hitPoint = collider.ClosestPoint(center);
                if ((hitPoint - center).sqrMagnitude > radius * radius) continue;
                if (overchargeContact.TryApplyShieldImpact(collider, center, hitPoint)) break;
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (overchargeShieldTexture == null ||
                (!IsOvercharging && !(attackMode == AttackMode.Test && testAttack == BossAttackType.OverchargedPursuit)))
                return;
            GetOverchargeShieldGeometry(out Vector3 center, out float diameter);
            float radius = diameter * 0.5f * overchargeShieldHitRadiusScale;
            Color previousColor = Gizmos.color;
            Gizmos.color = Color.green;
            for (int i = 0; i < ShockwaveSegments; i++)
            {
                float angle = i * Mathf.PI * 2f / ShockwaveSegments;
                float nextAngle = (i + 1) * Mathf.PI * 2f / ShockwaveSegments;
                Gizmos.DrawLine(center + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius,
                    center + new Vector3(Mathf.Cos(nextAngle), Mathf.Sin(nextAngle), 0f) * radius);
            }
            Gizmos.color = previousColor;
        }

        private void StopOvercharge()
        {
            if (overchargeAgent != null)
            {
                overchargeAgent.speed = originalOverchargeSpeed;
                overchargeAgent.acceleration = originalOverchargeAcceleration;
                overchargeAgent = null;
            }
            if (overchargeRenderer != null) overchargeRenderer.enabled = false;
            shieldPlayer = null;
            shieldPlayerColliders = null;
            overchargeStartedAt = overchargeEndsAt = overchargeEffectStartedAt = 0f;
        }

        private void BeginDash(EnemyAIController enemy)
        {
            if (currentAttack == BossAttackType.LightningCrossing) SetCrossingVisible(true);
            float selectedSpeed = GetSelectedDashSpeed();
            float selectedDuration = GetSelectedDashDuration();
            RefreshDashPlan();
            HideDashRangePreview();
            if (dashDirection.sqrMagnitude < 0.001f ||
                selectedSpeed <= 0f || selectedDuration <= 0f ||
                enemy.rb == null || !enemy.rb.simulated ||
                enemy.agent == null ||
                !enemy.agent.isActiveAndEnabled ||
                !enemy.agent.isOnNavMesh)
            {
                enemy.TransitionToState(enemy.chaseState);
                return;
            }

            enemy.StopMovement();
            enemy.ClearForcedDestination();
            enemy.SetForcedFacingDirection(dashDirection);
            dashStartPosition = GetCurrentPosition(enemy);
            activeDashSpeed = selectedSpeed;
            dashTimeRemaining = selectedDuration;

            // Preserve the existing body/navigation alignment rather than assuming
            // that the sprite pivot and the agent simulation position are identical.
            bodyToAgentOffset = GetBodyWorldPosition(enemy) - enemy.agent.nextPosition;

            // Let Rigidbody2D control position during the dash, as the goblin does.
            // Keep physics control across all three dashes and their pauses.
            // Capture the original setting only once, not again when it is false.
            if (!changedAgentUpdatePosition)
            {
                originalAgentUpdatePosition = enemy.agent.updatePosition;
                enemy.agent.updatePosition = false;
                changedAgentUpdatePosition = true;
            }

            if (logDashPosition)
            {
                Debug.Log(
                    $"[KnightBoss DashStart] body={GetBodyWorldPosition(enemy):F4} " +
                    $"agent={enemy.agent.nextPosition:F4} offset={bodyToAgentOffset:F4} " +
                    $"baseOffset={enemy.agent.baseOffset:F4} " +
                    $"speed={activeDashSpeed:F2} distance={plannedDashDistance:F2} " +
                    $"destination={dashStartPosition + dashDirection * plannedDashDistance:F4}", this);
            }

            currentPhase = AttackPhase.Dashing;
            finishDashNextFixedUpdate = false;
        }

        private void FixedUpdate()
        {
            if (currentPhase == AttackPhase.None || activeEnemy == null)
                return;

            if (!activeEnemy.isActiveAndEnabled)
            {
                ClearDashState();
                return;
            }

            if (currentPhase == AttackPhase.Preparing || currentPhase == AttackPhase.OverchargeCharging ||
                currentPhase == AttackPhase.CrossingPreparing || IsCrossingTransition)
            {
                HoldPositionDuringPreparation(activeEnemy);
                return;
            }

            // Navigation, not dash physics, owns movement during pursuit.
            if (currentPhase == AttackPhase.OverchargePursuing) return;

            // Read the result of the preceding physics step, not the next
            // MovePosition target, before returning control to navigation.
            SyncAgentPosition(activeEnemy);

            if (activeEnemy.rb == null || !activeEnemy.rb.simulated)
            {
                activeEnemy.TransitionToState(activeEnemy.chaseState);
                return;
            }

            float traveledDistance = Vector2.Dot(
                GetCurrentPosition(activeEnemy) - dashStartPosition, dashDirection);
            float remainingDistance = Mathf.Max(0f, plannedDashDistance - traveledDistance);
            if (finishDashNextFixedUpdate || dashTimeRemaining <= 0f || remainingDistance <= 0.001f)
            {
                FinishDash(activeEnemy);
                return;
            }

            float moveTime = Mathf.Min(Time.fixedDeltaTime, dashTimeRemaining);
            float moveDistance = Mathf.Min(activeDashSpeed * moveTime, remainingDistance);
            moveDistance = GetWallSafeMoveDistance(moveDistance, out bool willHitWall);

            if (moveDistance <= 0f)
            {
                FinishDash(activeEnemy);
                return;
            }

            Vector2 movement = dashDirection * moveDistance;
            if (currentAttack == BossAttackType.LightningCrossing && bodyCollider != null && bodyCollider.isActiveAndEnabled &&
                overchargeContact != null)
            {
                // High-speed crossings also sweep for players; the shared hit
                // cooldown prevents a second hit from the physics callback.
                int hits = bodyCollider.Cast(dashDirection, crossingPlayerHits, moveDistance);
                for (int i = 0; i < hits; i++)
                    if (crossingPlayerHits[i].collider != null && !crossingPlayerHits[i].collider.isTrigger &&
                        overchargeContact.TryApplyCrossingImpact(crossingPlayerHits[i].collider, crossingPlayerHits[i].point)) break;
            }
            activeEnemy.rb.MovePosition(GetCurrentPosition(activeEnemy) + movement);
            dashTimeRemaining = Mathf.Max(0f, dashTimeRemaining - moveTime);
            // Wait for this MovePosition to complete before restoring navigation.
            finishDashNextFixedUpdate = willHitWall || dashTimeRemaining <= 0f ||
                moveDistance >= remainingDistance;
        }

        private void FinishDash(EnemyAIController enemy)
        {
            if (currentPhase != AttackPhase.Dashing) return;
            if (currentAttack == BossAttackType.LightningCrossing)
            {
                crossingLastSafeBody = GetBodyWorldPosition(enemy);
                crossingLastSafeNav = enemy.agent.nextPosition;
                remainingCrossings--;
                if (remainingCrossings > 0)
                {
                    finishDashNextFixedUpdate = false;
                    activeDashSpeed = 0f;
                    dashTimeRemaining = 0f;
                    if (!PrepareNextCrossing(enemy)) FinishCrossingWithoutPath(enemy);
                    return;
                }
                PlayShockwave(enemy);
                BeginCrossingTransition(enemy, true);
                return;
            }
            else if (currentAttack == BossAttackType.ChainDash)
            {
                remainingChainDashes--;
                if (remainingChainDashes > 0)
                {
                    // Stay in this attack state: pause, reacquire the player,
                    // then start another dash without rerolling the pattern.
                    finishDashNextFixedUpdate = false;
                    activeDashSpeed = 0f;
                    dashTimeRemaining = 0f;
                    BeginDashPreparation(enemy, chainReaimDuration);
                    return;
                }
            }
            else if (currentAttack == BossAttackType.ThunderAssault)
            {
                PlayShockwave(enemy);
            }
            // ExitAttackState restores navigation without moving the shockwave.
            enemy.TransitionToState(enemy.chaseState);
        }

        private void PlayShockwave(EnemyAIController enemy)
        {
            if (enemyBase != null && enemyBase.isDead) return;
            // Reuse completed rings, but never restart a ring that is still expanding.
            ShockwaveInstance wave = shockwaves.Find(instance => !instance.Active);
            if (wave == null)
            {
                wave = new ShockwaveInstance();
                if (shockwaveLightningMaterial != null)
                {
                    // Each ring needs independent animation state.
                    wave.Material = new Material(shockwaveLightningMaterial);
                }
                else
                {
                    Shader shader = Shader.Find("Sprites/Default");
                    if (shader == null)
                    {
                        Debug.LogError("[KnightBoss Shockwave] Sprites/Default shader is missing.", this);
                        return;
                    }
                    wave.Material = new Material(shader);
                }
                wave.Material.name = "Knight Boss Shockwave (Runtime)";
                GameObject waveObject = new GameObject("Knight Boss Shockwave");
                waveObject.layer = gameObject.layer;
                wave.Line = waveObject.AddComponent<LineRenderer>();
                wave.Line.enabled = false;
                wave.Line.sharedMaterial = wave.Material;
                wave.Line.useWorldSpace = false;
                wave.Line.textureMode = LineTextureMode.Stretch;
                wave.Line.loop = true;
                wave.Line.positionCount = ShockwaveSegments;
                wave.Line.alignment = LineAlignment.View;
                SpriteRenderer sprite = GetComponent<SpriteRenderer>();
                if (sprite != null)
                {
                    wave.Line.sortingLayerID = sprite.sortingLayerID;
                    wave.Line.sortingOrder = sprite.sortingOrder - 1;
                }
                shockwaves.Add(wave);
            }

            Vector3 origin = GetBodyWorldPosition(enemy) +
                enemy.transform.TransformVector((Vector3)shockwaveLocalOffset);
            wave.Origin = origin;
            wave.Line.transform.SetPositionAndRotation(origin, Quaternion.identity);
            wave.Line.transform.localScale = Vector3.one;
            wave.Elapsed = 0f;
            wave.PreviousRadius = 0f;
            wave.HitPlayer = false;
            wave.Player = enemy.player != null
                ? enemy.player.GetComponentInParent<PlayerHealth>() : null;
            if (wave.Player == null && enemy.player != null)
                wave.Player = enemy.player.GetComponentInChildren<PlayerHealth>();
            wave.PlayerColliders = wave.Player != null
                ? wave.Player.GetComponentsInChildren<Collider2D>() : null;
            statusEffectManager = GetComponent<StatusEffectManager>();
            wave.Active = true;
            DrawShockwave(wave, 0f, 0f);
            wave.Line.enabled = true;
        }

        private void Update()
        {
            if (enemyBase != null && enemyBase.isDead)
            {
                StopShockwave();
                return;
            }

            foreach (ShockwaveInstance wave in shockwaves)
            {
                if (!wave.Active) continue;
                wave.Elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(wave.Elapsed / Mathf.Max(0.01f, shockwaveDuration));
                float radius = Mathf.Max(0.1f, shockwaveRadius) * progress;
                DrawShockwave(wave, radius, progress);
                CheckShockwaveHit(wave, wave.PreviousRadius, radius);
                wave.PreviousRadius = radius;
                if (progress >= 1f) StopShockwave(wave);
            }
        }

        private void DrawShockwave(ShockwaveInstance wave, float radius, float progress)
        {
            for (int i = 0; i < ShockwaveSegments; i++)
            {
                float angle = i * Mathf.PI * 2f / ShockwaveSegments;
                shockwavePoints[i] = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius;
            }
            wave.Line.SetPositions(shockwavePoints);
            wave.Line.startWidth = wave.Line.endWidth = Mathf.Max(0.01f, shockwaveWidth);
            bool hasLightningAnimation = wave.Material.HasProperty(WaveFrameIndexId);
            if (hasLightningAnimation)
            {
                int frameCount = Mathf.Max(1, shockwaveFrameCount);
                float frame = Mathf.Floor(wave.Elapsed * Mathf.Max(0f, shockwaveFramesPerSecond)) % frameCount;
                wave.Material.SetFloat(WaveFrameCountId, frameCount);
                wave.Material.SetFloat(WaveFrameIndexId, frame);
                // Match each frame's aspect ratio instead of stretching four
                // copies around every ring. Whole repeats keep the loop seam closed.
                Texture texture = wave.Material.mainTexture;
                float repeats = Mathf.Max(1, shockwaveTextureRepeat);
                if (texture != null && texture.height > 0)
                {
                    float frameAspect = (float)texture.width * frameCount / texture.height;
                    float tileLength = Mathf.Max(0.01f, shockwaveWidth * frameAspect);
                    repeats = Mathf.Max(1, Mathf.RoundToInt(2f * Mathf.PI * radius / tileLength));
                }
                wave.Material.SetFloat(WaveRepeatId, repeats);
                wave.Material.SetFloat(WaveScrollId, Mathf.Repeat(wave.Elapsed * shockwaveTextureScrollSpeed, 1f));
            }
            // The texture already contains purple color; don't multiply it by
            // the old blue tint. Keep the user's opacity and fade timing.
            Color color = hasLightningAnimation ? Color.white : shockwaveColor;
            color.a = shockwaveColor.a;
            color.a *= 1f - progress;
            wave.Line.startColor = wave.Line.endColor = color;
        }

        private void CheckShockwaveHit(ShockwaveInstance wave, float previousRadius, float radius)
        {
            if (wave.HitPlayer || wave.Player == null ||
                !wave.Player.isActiveAndEnabled || wave.PlayerColliders == null)
                return;

            float halfWidth = Mathf.Max(0.01f, shockwaveWidth) * 0.5f;
            foreach (Collider2D collider in wave.PlayerColliders)
            {
                if (collider == null || !collider.isActiveAndEnabled) continue;
                Vector2 hitPoint = collider.ClosestPoint(wave.Origin);
                float nearest = Vector2.Distance(wave.Origin, hitPoint);
                Bounds bounds = collider.bounds;
                // Bounds provide a conservative outer extent; ClosestPoint
                // supplies the exact inner extent. Sweep the expanding ring
                // between frames so a fast wave cannot skip the collider.
                Vector2 farthestOffset = new Vector2(
                    Mathf.Abs(bounds.center.x - wave.Origin.x) + bounds.extents.x,
                    Mathf.Abs(bounds.center.y - wave.Origin.y) + bounds.extents.y);
                if (nearest > radius + halfWidth || farthestOffset.magnitude < previousRadius - halfWidth)
                    continue;

                wave.HitPlayer = true;
                float damage = Mathf.Max(0f, shockwaveDamage);
                if (statusEffectManager != null)
                    damage *= statusEffectManager.GetOutgoingDamageMultiplier();
                wave.Player.TakeDamage(damage, hitPoint);
                return;
            }
        }

        private void StopShockwave()
        {
            foreach (ShockwaveInstance wave in shockwaves) StopShockwave(wave);
        }

        private static void StopShockwave(ShockwaveInstance wave)
        {
            wave.Active = false;
            if (wave.Line != null) wave.Line.enabled = false;
            wave.Player = null;
            wave.PlayerColliders = null;
        }

        private void OnDestroy()
        {
            if (crossingPixelRenderer != null) Destroy(crossingPixelRenderer.gameObject);
            if (crossingPixelMaterial != null) Destroy(crossingPixelMaterial);
            foreach (ShockwaveInstance wave in shockwaves)
            {
                if (wave.Line != null) Destroy(wave.Line.gameObject);
                if (wave.Material != null) Destroy(wave.Material);
            }
            shockwaves.Clear();
            if (overchargeRenderer != null) Destroy(overchargeRenderer.gameObject);
            if (overchargeMaterial != null) Destroy(overchargeMaterial);
            ClearOverchargeShieldFrames();
        }

        private static void HoldPositionDuringPreparation(EnemyAIController enemy)
        {
            // StopMovement cancels the path, but does not immediately remove
            // the knight's remaining velocity or block new chase commands.
            enemy.ClearForcedDestination();
            enemy.SetMovementAllowed(false);
            if (enemy.agent != null && enemy.agent.isActiveAndEnabled && enemy.agent.isOnNavMesh)
                enemy.agent.velocity = Vector3.zero;
            if (enemy.rb != null)
            {
                enemy.rb.velocity = Vector2.zero;
                enemy.rb.angularVelocity = 0f;
            }
        }

        private float GetWallSafeMoveDistance(float requestedDistance, out bool willHitWall)
        {
            willHitWall = false;

            if (bodyCollider == null)
            {
                bodyCollider = GetComponent<Collider2D>();
            }

            if (bodyCollider == null || requestedDistance <= 0f)
                return requestedDistance;

            int hitCount = bodyCollider.Cast(
                dashDirection,
                castHits,
                requestedDistance + wallStopDistance);

            float nearestWallDistance = float.PositiveInfinity;
            for (int i = 0; i < hitCount; i++)
            {
                Collider2D hitCollider = castHits[i].collider;
                if (hitCollider == null ||
                    !hitCollider.CompareTag(Nytherion.Core.Systems.Tags.Wall))
                {
                    continue;
                }

                nearestWallDistance = Mathf.Min(
                    nearestWallDistance,
                    castHits[i].distance);
            }

            if (float.IsPositiveInfinity(nearestWallDistance))
                return requestedDistance;

            willHitWall = true;
            return Mathf.Clamp(
                nearestWallDistance - wallStopDistance,
                0f,
                requestedDistance);
        }

        public void ExitAttackState(EnemyAIController enemy)
        {
            float recovery = currentAttack == BossAttackType.ChainDash ? chainCooldown : cooldown;
            if (currentAttack == BossAttackType.OverchargedPursuit) recovery = overchargeCooldown;
            if (currentAttack == BossAttackType.LightningCrossing) recovery = crossingCooldown;
            if (enemyBase != null && enemyBase.isDead) StopShockwave();
            ClearDashState();
            enemy.StopMovement();
            nextAttackTime = Time.time + Mathf.Max(0f, recovery);
        }
        public void ResetForReuse()
        {
            StopShockwave();
            ClearDashState();
            lastAttack = null;
            warnedUnavailableTestAttack = null;
            attackCandidates.Clear();
            nextAttackTime=Time.time+cooldown;
            preparationEndTime=0f;
        }

        private void OnDisable()
        {
            SetRunEffectVisible(false);
            runEffectHasLastPosition = false;
            StopShockwave();
            ClearDashState();
        }

        private void ClearDashState()
        {
            HideCrossingTransitionEffect();
            SetPatternOutline(false);
            StopOvercharge();
            HideDashRangePreview();
            RestoreCrossingVisibility();
            if (activeEnemy != null)
            {
                if (changedAgentUpdatePosition && activeEnemy.rb != null)
                    activeEnemy.rb.velocity = Vector2.zero;

                RestoreAgentPosition(activeEnemy);
                activeEnemy.ClearForcedFacingDirection();

                EnemyBase enemyBase = activeEnemy.GetComponent<EnemyBase>();
                if (blockedChaseMovement && activeEnemy.isActiveAndEnabled &&
                    (enemyBase == null || !enemyBase.isDead))
                    activeEnemy.SetMovementAllowed(true);
            }

            currentPhase = AttackPhase.None;
            currentAttack = null;
            finishDashNextFixedUpdate = false;
            dashDirection = Vector2.zero;
            dashAimCenter = Vector2.zero;
            dashTimeRemaining = 0f;
            remainingChainDashes = 0;
            remainingCrossings = 0;
            crossingTravelDistance = 0f;
            plannedDashDistance = 0f;
            activeDashSpeed = 0f;
            dashStartPosition = Vector2.zero;
            activeEnemy = null;
            changedAgentUpdatePosition = false;
            blockedChaseMovement = false;
            bodyToAgentOffset = Vector3.zero;
        }

        private float GetSelectedDashSpeed()
        {
            if (currentAttack == BossAttackType.LightningCrossing) return crossingSpeed;
            return currentAttack == BossAttackType.ChainDash ? chainDashSpeed : dashSpeed;
        }

        private float GetSelectedDashDuration()
        {
            if (currentAttack == BossAttackType.LightningCrossing)
                return crossingTravelDistance / Mathf.Max(0.01f, crossingSpeed);
            return currentAttack == BossAttackType.ChainDash ? chainDashDuration : dashDuration;
        }

        private void RefreshDashPlan()
        {
            // Player distance determines direction only, never the preview length.
            // Only a wall can shorten the speed * duration travel distance.
            plannedDashDistance = dashDirection.sqrMagnitude > 0.001f
                ? GetWallSafeMoveDistance(Mathf.Max(0f, GetSelectedDashSpeed() * GetSelectedDashDuration()), out _)
                : 0f;
        }

        private void ShowDashRangePreview(EnemyAIController enemy)
        {
            if (currentPhase == AttackPhase.Preparing) RefreshDashPlan();
            if (dashRangePreview == null || enemy == null ||
                dashDirection.sqrMagnitude <= 0.001f)
            {
                HideDashRangePreview();
                return;
            }

            if (plannedDashDistance <= 0f)
            {
                HideDashRangePreview();
                return;
            }

            Transform previewTransform = dashRangePreview.transform;
            GetDashPreviewBounds(enemy, out Vector3 origin, out float length, out float width);
            // Like the goblin preview, detach so sprite facing cannot mirror it.
            previewTransform.SetParent(null, true);
            previewTransform.position = origin;
            previewTransform.rotation = Quaternion.Euler(0f, 0f,
                Mathf.Atan2(dashDirection.y, dashDirection.x) * Mathf.Rad2Deg);
            previewTransform.localScale = Vector3.one;
            dashRangePreview.SetActive(true);
            previewController?.Configure(length, width);
        }

        private void GetDashPreviewBounds(EnemyAIController enemy,
            out Vector3 origin, out float length, out float width)
        {
            Vector3 bodyPosition = GetBodyWorldPosition(enemy);
            origin = new Vector3(dashAimCenter.x, dashAimCenter.y, bodyPosition.z);
            length = plannedDashDistance;
            width = previewWidth;
            if (bodyCollider == null) bodyCollider = GetComponent<Collider2D>();
            if (bodyCollider == null || !bodyCollider.isActiveAndEnabled) return;

            Vector2 forward = dashDirection.normalized;
            Vector2 side = new Vector2(-forward.y, forward.x);
            float minForward = float.PositiveInfinity, maxForward = float.NegativeInfinity;
            float minSide = float.PositiveInfinity, maxSide = float.NegativeInfinity;

            void IncludePoint(Vector2 worldPoint)
            {
                Vector2 relative = worldPoint - dashAimCenter;
                float along = Vector2.Dot(relative, forward);
                float across = Vector2.Dot(relative, side);
                minForward = Mathf.Min(minForward, along);
                maxForward = Mathf.Max(maxForward, along);
                minSide = Mathf.Min(minSide, across);
                maxSide = Mathf.Max(maxSide, across);
            }

            float padding = 0f;
            if (bodyCollider is EdgeCollider2D edge)
            {
                // Project the actual outline, not its world-axis bounding box.
                // TransformPoint also accounts for sprite-facing scale flips.
                Vector2[] points = edge.points;
                if (points.Length < 2) return;
                foreach (Vector2 point in points)
                    IncludePoint(edge.transform.TransformPoint(point + edge.offset));

                float scale = Mathf.Max(
                    edge.transform.TransformVector(Vector3.right).magnitude,
                    edge.transform.TransformVector(Vector3.up).magnitude);
                padding = edge.edgeRadius * scale;
            }
            else
            {
                // Conservative fallback if the boss collider type is replaced.
                Bounds bounds = bodyCollider.bounds;
                IncludePoint(new Vector2(bounds.min.x, bounds.min.y));
                IncludePoint(new Vector2(bounds.min.x, bounds.max.y));
                IncludePoint(new Vector2(bounds.max.x, bounds.min.y));
                IncludePoint(new Vector2(bounds.max.x, bounds.max.y));
            }

            minForward -= padding;
            maxForward += padding;
            minSide -= padding;
            maxSide += padding;
            // Keep the centerline on the exact aim line. An asymmetric outline
            // must change the width, not shift that line away from the player.
            Vector2 start = dashAimCenter + forward * minForward;
            origin = new Vector3(start.x, start.y, bodyPosition.z);
            // Cover the body's initial footprint through its final footprint,
            // rather than shifting a center-to-center line down to the feet.
            length = plannedDashDistance + maxForward - minForward;
            width = Mathf.Max(0.05f, 2f * Mathf.Max(Mathf.Abs(minSide), Mathf.Abs(maxSide)));
        }

        private void HideDashRangePreview()
        {
            if (dashRangePreview == null) return;
            dashRangePreview.SetActive(false);
            if (previewOriginalParent == null) return;

            Transform previewTransform = dashRangePreview.transform;
            previewTransform.SetParent(previewOriginalParent, false);
            previewTransform.localPosition = previewOriginalLocalPosition;
            previewTransform.localRotation = previewOriginalLocalRotation;
            previewTransform.localScale = previewOriginalLocalScale;
        }

        private static Vector2 GetCurrentPosition(EnemyAIController enemy)
        {
            return enemy.rb != null
                ? enemy.rb.position
                : (Vector2)enemy.transform.position;
        }

        private static Vector3 GetBodyWorldPosition(EnemyAIController enemy)
        {
            Vector2 position = GetCurrentPosition(enemy);
            return new Vector3(position.x, position.y, enemy.transform.position.z);
        }

        private void SyncAgentPosition(EnemyAIController enemy)
        {
            if (!changedAgentUpdatePosition || enemy == null || enemy.agent == null ||
                !enemy.agent.isActiveAndEnabled || !enemy.agent.isOnNavMesh)
                return;

            enemy.agent.nextPosition = GetBodyWorldPosition(enemy) - bodyToAgentOffset;
        }

        private void RestoreAgentPosition(EnemyAIController enemy)
        {
            if (!changedAgentUpdatePosition || enemy == null || enemy.agent == null)
                return;

            Vector3 endPosition = GetBodyWorldPosition(enemy);
            Vector3 requestedAgentPosition = endPosition - bodyToAgentOffset;
            if (enemy.agent.isActiveAndEnabled && enemy.agent.isOnNavMesh)
            {
                // The agent already followed the body throughout the dash.
                // Do not Warp the sprite's offset world position at handoff.
                enemy.agent.nextPosition = requestedAgentPosition;
            }

            enemy.agent.updatePosition = originalAgentUpdatePosition;

            if (logDashPosition)
            {
                Debug.Log(
                    $"[KnightBoss Restore] endBody={endPosition:F4} " +
                    $"requestedAgent={requestedAgentPosition:F4} " +
                    $"actualAgent={enemy.agent.nextPosition:F4} " +
                    $"resumedTransform={enemy.transform.position:F4} " +
                    $"bodyDelta={(GetBodyWorldPosition(enemy) - endPosition):F4} " +
                    $"transformDelta={(enemy.transform.position - endPosition):F4} " +
                    $"offset={bodyToAgentOffset:F4}", this);
            }

            changedAgentUpdatePosition = false;
        }
        

    }
}
