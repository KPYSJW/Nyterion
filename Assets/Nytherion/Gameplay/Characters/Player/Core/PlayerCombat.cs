using Nytherion.GamePlay.Combat;
using Nytherion.Core.Managers;
using UnityEngine;
using Nytherion.Data.ScriptableObjects.Weapons;
using Nytherion.GamePlay.Combat.Weapons;
using Nytherion.Core.Enums;
using System.Collections.Generic;
using VContainer;

namespace Nytherion.GamePlay.Characters.Player
{
    public class PlayerCombat : MonoBehaviour
    {
        [SerializeField] private Transform weaponPoint;
        [SerializeField] private Transform meleeWeaponPoint;

        [SerializeField] private float orbitRadius = 1.0f;

        [SerializeField] private float deadZoneRadius = 0.5f;

        [SerializeField] private float orbitSpeed = 15f;

        [SerializeField] private Vector3 centerOffset = new Vector3(0, 0.5f, 0);

        [Header("Render Settings")]
        [SerializeField] private int weaponSortingOrderOffset = -1;

        [Header("Aim Settings")]
        private bool useAngleLimit = false; // 360도 회전을 위해 인스펙터 오버라이드를 무시하고 항상 false로 고정
        [SerializeField] private float aimAngleLimit = 45f; // 중심각 기준 좌우 45도 (합 90도 부채꼴)

        public WeaponBase currentWeapon;
        public Vector2 LastAttackDirection { get; private set; } = Vector2.right;

        public event System.Action<WeaponBase> OnWeaponEquipped;
        public event System.Action<Vector2, Vector3> OnPlayerAttack;
        public event System.Action<Vector2, Vector3> OnPlayerAttackStarted;
        public event System.Action OnPlayerAttackEnd;

        // 테스트 속성은 무기 데이터에 기록하지 않고 플레이어 수명 동안만 유지한다.
        private readonly HashSet<EquipmentTrait> debugAttackTraits = new HashSet<EquipmentTrait>();

        public bool IsDebugAttackTraitActive(EquipmentTrait trait) => debugAttackTraits.Contains(trait);

        public void SetDebugAttackTrait(EquipmentTrait trait, bool active)
        {
            switch (trait)
            {
                case EquipmentTrait.Fire:
                case EquipmentTrait.Ice:
                case EquipmentTrait.Lightning:
                case EquipmentTrait.Poison:
                case EquipmentTrait.Curse:
                case EquipmentTrait.Holy:
                case EquipmentTrait.Demonic:
                    if (active) debugAttackTraits.Add(trait);
                    else debugAttackTraits.Remove(trait);
                    break;
            }
        }

        public void ResetDebugAttackTraits() => debugAttackTraits.Clear();

        public List<EquipmentTrait> MergeDebugAttackTraits(List<EquipmentTrait> baseTraits)
        {
            if (debugAttackTraits.Count == 0) return baseTraits ?? new List<EquipmentTrait>();

            // 발사한 투사체의 속성 목록과 원본 장비 데이터를 변경하지 않도록 복사한다.
            List<EquipmentTrait> traits = baseTraits != null
                ? new List<EquipmentTrait>(baseTraits) : new List<EquipmentTrait>();
            foreach (EquipmentTrait trait in debugAttackTraits)
            {
                if (!traits.Contains(trait)) traits.Add(trait);
            }
            return traits;
        }

        private InputManager inputManager;
        private PlayerManager playerManager;
        private PlayerController playerController;
        private SpriteRenderer playerSpriteRenderer;

        private float currentAngle = 0f;
        private bool isAttackHeld = false;
        private GameObject genericChargeEffect;
        private GameObject genericChargeEffectPrefab;
        private ObjectPoolManager genericChargeEffectPool;
        private bool isGenericChargePending;
        private float genericChargePressStartedAt;
        private bool isGenericCharging;
        private float genericChargeTime;
        private bool isGenericChargeReleased;
        private Vector2 genericChargeDirection;
        private Vector3 genericChargeTarget;

        public bool IsGenericCharging => isGenericCharging;
        public bool IsAttackActive => currentWeapon != null &&
            (isAttackHeld || isGenericChargePending || isGenericCharging ||
             (currentWeapon is IChargeableWeapon chargeable && chargeable.IsCharging) ||
             (currentWeapon is FrenzyWeapon frenzy && frenzy.IsFiring) ||
             (currentWeapon is CrimsonLotusWeapon laser && laser.IsFiring) ||
             (currentWeapon is ObliterationWeapon layLaser && layLaser.IsFiring) ||
             (currentWeapon is VoidRayWeapon voidRay && voidRay.IsFiring));
        public float GenericChargePercent
        {
            get
            {
                float maxChargeTime = GetGenericMaxChargeTime();
                return maxChargeTime > 0f ? Mathf.Clamp01(genericChargeTime / maxChargeTime) : 1f;
            }
        }

        [Inject]
        public void Construct(InputManager inputManager)
        {
            UnsubscribeAttackInput();
            this.inputManager = inputManager;
            // 씬에 미리 배치된 플레이어는 Start 이후 주입될 수도 있다.
            if (isActiveAndEnabled) SubscribeAttackInput();
        }

        private void Awake()
        {
            playerManager = GetComponent<PlayerManager>();
            playerController = GetComponent<PlayerController>();
            playerSpriteRenderer = GetComponent<SpriteRenderer>();
        }

        private void Start()
        {
            SubscribeAttackInput();
        }

        private void OnEnable()
        {
            SubscribeAttackInput();
        }

        private void SubscribeAttackInput()
        {
            if (inputManager != null)
            {
                UnsubscribeAttackInput();
                inputManager.onAttackDown += HandleAttackDown;
                inputManager.onAttackUp += HandleAttackUp;
            }
        }

        private void UnsubscribeAttackInput()
        {
            if (inputManager == null) return;
            inputManager.onAttackDown -= HandleAttackDown;
            inputManager.onAttackUp -= HandleAttackUp;
        }

        private void HandleAttackDown()
        {
            isAttackHeld = true;
            if (currentWeapon != null) playerController?.NotifyCombatActivity();
            if (ShouldUseGenericCharging())
            {
                BeginGenericCharge();
            }
            else
            {
                Attack();
            }
        }

        private void HandleAttackUp()
        {
            isAttackHeld = false;
            if (isGenericChargePending || isGenericCharging)
            {
                ReleaseGenericCharge();
            }
            else
            {
                AttackEnd();
            }
        }

        public void EquipWeapon(WeaponBase weaponPrefab, WeaponData data = null)
        {
            CancelGenericCharge();
            if (currentWeapon != null)
            {
                Destroy(currentWeapon.gameObject);
                currentWeapon = null;
            }

            if (weaponPrefab == null)
            {
                OnWeaponEquipped?.Invoke(null);
                return;
            }

            WeaponType type = (data != null) ? data.weaponType : (weaponPrefab.weaponData != null ? weaponPrefab.weaponData.weaponType : WeaponType.Ranged);

            if (type == WeaponType.Melee)
            {
                if (meleeWeaponPoint != null)
                {
                    currentWeapon = Instantiate(weaponPrefab, meleeWeaponPoint, false);
                }
            }
            else
            {
                if (weaponPoint != null)
                {
                    currentWeapon = Instantiate(weaponPrefab, weaponPoint, false);
                }
            }

            if (currentWeapon != null)
            {
                // Instantiate(..., false)에 의해 프리팹의 원래 localPosition이 유지됩니다.
                // 만약 WeaponData에 오버라이드용 visualPositionOffset이 명시되어 있다면 그것을 덮어씁니다.
                if (type != WeaponType.Melee)
                {
                    Vector3 posOffset = currentWeapon.transform.localPosition;
                    if (data != null && data.visualPositionOffset != Vector3.zero)
                    {
                        posOffset = data.visualPositionOffset;
                    }
                    else if (currentWeapon.weaponData != null && currentWeapon.weaponData.visualPositionOffset != Vector3.zero)
                    {
                        posOffset = currentWeapon.weaponData.visualPositionOffset;
                    }

                    currentWeapon.transform.localPosition = posOffset;
                }
                else
                {
                    Vector3 posOffset = currentWeapon.transform.localPosition;
                    if (data != null && data.visualPositionOffset != Vector3.zero)
                    {
                        posOffset = data.visualPositionOffset;
                    }
                    else if (currentWeapon.weaponData != null && currentWeapon.weaponData.visualPositionOffset != Vector3.zero)
                    {
                        posOffset = currentWeapon.weaponData.visualPositionOffset;
                    }
                    currentWeapon.transform.localPosition = posOffset;
                }

                WeaponData equippedData = data != null ? data : currentWeapon.weaponData;
                if (equippedData != null)
                {
                    float visualScale = equippedData.visualScale > 0f ? equippedData.visualScale : 1f;
                    currentWeapon.transform.localScale *= visualScale;
                }

                // Animator Controller 런타임 주입 (원거리 무기만 적용)
                if (type != WeaponType.Melee)
                {
                    Animator weaponAnimator = currentWeapon.GetComponent<Animator>();
                    if (weaponAnimator == null)
                    {
                        weaponAnimator = currentWeapon.GetComponentInChildren<Animator>();
                    }

                    if (weaponAnimator != null)
                    {
                        RuntimeAnimatorController controller = null;
                        if (data != null)
                        {
                            controller = data.animatorController;
                        }
                        else if (currentWeapon.weaponData != null)
                        {
                            controller = currentWeapon.weaponData.animatorController;
                        }
                        weaponAnimator.runtimeAnimatorController = controller;
                    }
                }

                // Void Ray는 WeaponItem처럼 프리팹만 전달되는 장착 경로에서도
                // 프리팹에 연결된 DataAsset으로 반드시 초기화해야 고정 자세 값을 유지합니다.
                if (equippedData != null && (data != null || currentWeapon is VoidRayWeapon))
                {
                    currentWeapon.Initialize(equippedData);
                }

                if (type != WeaponType.Melee)
                {
                    float rotationOffset = 0f;
                    if (data != null)
                    {
                        rotationOffset = data.spriteRotationOffset;
                    }
                    else if (currentWeapon.weaponData != null)
                    {
                        rotationOffset = currentWeapon.weaponData.spriteRotationOffset;
                    }

                    currentWeapon.transform.localRotation = Quaternion.Euler(0f, 0f, rotationOffset);
                }
                else
                {
                    currentWeapon.transform.localRotation = Quaternion.identity;
                }
            }
            
            OnWeaponEquipped?.Invoke(currentWeapon);
        }

        private void Update()
        {
            RotateWeaponToMouse();
            // 무기 루트의 애니메이션/좌우 반전을 유지하면서 근접 판정과 궤적을 함께 확대합니다.
            if (meleeWeaponPoint != null)
                meleeWeaponPoint.localScale = Vector3.one *
                    (currentWeapon is MeleeWeapon ? currentWeapon.EffectSizeMultiplier : 1f);

            if (isGenericChargePending || isGenericCharging)
            {
                if (!ShouldUseGenericCharging())
                {
                    CancelGenericCharge();
                }
                else if (isGenericChargeReleased)
                {
                    if (currentWeapon.CanAttack()) FireGenericCharge();
                }
                else
                {
                    UpdateGenericChargeProgress(Time.time - genericChargePressStartedAt);
                }
                return;
            }

            if (isAttackHeld && currentWeapon != null)
            {
                bool shouldRetry = currentWeapon is IChargeableWeapon
                    ? currentWeapon.AllowHeldAttackRetry
                    : currentWeapon.AllowAutoFire;
                // 재시도를 허용한 차징 무기는 대기 중 누른 입력을 유지해
                // 공격 가능 시점이 되는 첫 프레임에 다음 차징을 시작합니다.
                if (shouldRetry && currentWeapon.CanAttack())
                {
                    Attack();
                }
            }
        }

        private void LateUpdate()
        {
            UpdateWeaponSortingOrder();
        }

        private void UpdateWeaponSortingOrder()
        {
            if (currentWeapon == null)
            {
                return;
            }

            if (playerSpriteRenderer == null)
            {
                playerSpriteRenderer = GetComponentInChildren<SpriteRenderer>();
            }
            if (playerSpriteRenderer == null)
            {
                return;
            }

            SpriteRenderer[] weaponRenderers = currentWeapon.GetComponentsInChildren<SpriteRenderer>(true);
            if (weaponRenderers.Length == 0)
            {
                return;
            }

            SpriteRenderer rootWeaponRenderer = currentWeapon.GetComponent<SpriteRenderer>();
            int currentBaseOrder = rootWeaponRenderer != null
                ? rootWeaponRenderer.sortingOrder
                : weaponRenderers[0].sortingOrder;
            int sortingOrderOffset = currentWeapon.weaponData != null
                ? currentWeapon.weaponData.sortingOrderOffset
                : weaponSortingOrderOffset;
            int targetBaseOrder = playerSpriteRenderer.sortingOrder + sortingOrderOffset;
            for (int i = 0; i < weaponRenderers.Length; i++)
            {
                SpriteRenderer weaponRenderer = weaponRenderers[i];
                int relativeOrder = weaponRenderer.sortingOrder - currentBaseOrder;
                weaponRenderer.sortingLayerID = playerSpriteRenderer.sortingLayerID;
                weaponRenderer.sortingOrder = targetBaseOrder + relativeOrder;
            }
        }

        private void RotateWeaponToMouse()
        {
            if (inputManager == null || weaponPoint == null) return;

            Vector2 mouseScreenPos = inputManager.MousePosition;

            if (Camera.main != null)
            {
                Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(new Vector3(mouseScreenPos.x, mouseScreenPos.y, 0f));
                mouseWorldPos.z = 0f;

                Vector3 playerCenter = transform.position + centerOffset;

                if (currentWeapon is VoidRayWeapon voidRayWeapon)
                {
                    weaponPoint.localPosition = Vector3.zero;
                    weaponPoint.localRotation = Quaternion.identity;
                    weaponPoint.localScale = Vector3.one;
                    voidRayWeapon.UpdateAimAndPose(mouseWorldPos, playerCenter);
                    return;
                }

                if (currentWeapon != null && currentWeapon.OverrideRotation)
                {
                    weaponPoint.localPosition = Vector3.zero;
                    weaponPoint.localRotation = Quaternion.identity;
                    weaponPoint.localScale = Vector3.one;

                    if (meleeWeaponPoint != null)
                    {
                        meleeWeaponPoint.localPosition = Vector3.zero;
                        meleeWeaponPoint.localRotation = Quaternion.identity;
                        meleeWeaponPoint.localScale = Vector3.one;
                    }
                    return;
                }

                Vector2 mouseVector = mouseWorldPos - playerCenter;
                bool keepVoidRayDirection = currentWeapon is VoidRayWeapon voidRay &&
                    voidRay.firePoint != null &&
                    ((Vector2)(mouseWorldPos - voidRay.firePoint.position)).sqrMagnitude < 0.0064f;

                float targetAngle = currentAngle;
                // 총구와 조준점이 거의 겹치면 atan2의 불안정한 값 대신 마지막 유효 각도를 유지합니다.
                if (!keepVoidRayDirection && mouseVector.magnitude >= deadZoneRadius)
                {
                    targetAngle = Mathf.Atan2(mouseVector.y, mouseVector.x) * Mathf.Rad2Deg;
                }

                if (useAngleLimit && playerController != null)
                {
                    float centerAngle = playerController.IsFacingRight ? 0f : 180f;
                    float angleDiff = Mathf.DeltaAngle(centerAngle, targetAngle);
                    angleDiff = Mathf.Clamp(angleDiff, -aimAngleLimit, aimAngleLimit);
                    targetAngle = centerAngle + angleDiff;
                }

                currentAngle = Mathf.LerpAngle(currentAngle, targetAngle, Time.deltaTime * orbitSpeed);

                Vector2 currentDirection = new Vector2(Mathf.Cos(currentAngle * Mathf.Deg2Rad), Mathf.Sin(currentAngle * Mathf.Deg2Rad));

                // 레이저도 장착 오프셋을 유지합니다. 총구를 중심 조준선으로 옮기면 Y 오프셋이 상쇄됩니다.
                weaponPoint.position = playerCenter + (Vector3)(currentDirection * orbitRadius);
                weaponPoint.rotation = Quaternion.Euler(0, 0, currentAngle);

                float normalizedAngle = Mathf.DeltaAngle(0, currentAngle);
                if (Mathf.Abs(normalizedAngle) > 90f)
                {
                    weaponPoint.localScale = new Vector3(1f, -1f, 1f);
                }
                else
                {
                    weaponPoint.localScale = new Vector3(1f, 1f, 1f);
                }

                if (currentWeapon is CrimsonLotusWeapon laserWeapon)
                    laserWeapon.UpdateAimAndPose(mouseWorldPos);
            }
        }

        public void Attack()
        {
            if (currentWeapon != null && currentWeapon.CanAttack())
            {
                playerController?.NotifyCombatActivity();
                currentWeapon.ResetGenericChargeMultiplier();
                Vector2 fireDirection = currentWeapon is VoidRayWeapon voidRay
                    ? voidRay.CurrentFireDirection
                    : currentWeapon is CrimsonLotusWeapon laserWeapon
                        ? laserWeapon.CurrentFireDirection
                        : (Vector2)weaponPoint.right;
                Vector2 mouseScreenPos = inputManager.MousePosition;
                Vector3 targetWorldPos = Camera.main.ScreenToWorldPoint(new Vector3(mouseScreenPos.x, mouseScreenPos.y, 0f));
                targetWorldPos.z = 0f;
                LastAttackDirection = fireDirection.normalized;
                OnPlayerAttackStarted?.Invoke(fireDirection, targetWorldPos);
                currentWeapon.Attack(fireDirection, targetWorldPos);

                OnPlayerAttack?.Invoke(fireDirection, targetWorldPos);
            }
        }

        public void AttackEnd()
        {
            if (currentWeapon != null)
            {
                playerController?.NotifyCombatActivity();
                currentWeapon.AttackEnd();
                OnPlayerAttackEnd?.Invoke();
            }
        }

        private bool ShouldUseGenericCharging()
        {
            return currentWeapon != null &&
                   !(currentWeapon is VoidRayWeapon) &&
                   !(currentWeapon is IChargeableWeapon) &&
                   playerManager != null &&
                   playerManager.playerRelicManager != null &&
                   playerManager.playerRelicManager.IsRelicActive("ManaCharger");
        }

        private void BeginGenericCharge()
        {
            isGenericChargePending = true;
            genericChargePressStartedAt = Time.time;
            isGenericCharging = false;
            genericChargeTime = 0f;
            isGenericChargeReleased = false;
            UpdateGenericChargeProgress(0f);
        }

        private void UpdateGenericChargeProgress(float heldTime)
        {
            float threshold = currentWeapon != null && currentWeapon.weaponData != null
                ? Mathf.Max(0f, currentWeapon.weaponData.chargeThresholdTime) : 0.15f;
            if (heldTime < threshold) return;
            if (!isGenericCharging)
            {
                isGenericChargePending = false;
                isGenericCharging = true;
                ShowGenericChargeEffect();
            }
            // 일반 클릭 판별에 사용한 시간은 실제 충전량에 포함하지 않습니다.
            genericChargeTime = Mathf.Clamp(heldTime - threshold, 0f, GetGenericMaxChargeTime());
        }

        private void ReleaseGenericCharge()
        {
            if (!ShouldUseGenericCharging())
            {
                CancelGenericCharge();
                return;
            }

            // Update 사이에 버튼을 놓아도 실제 누른 시간으로 일반/차징을 구분합니다.
            UpdateGenericChargeProgress(Time.time - genericChargePressStartedAt);
            genericChargeDirection = currentWeapon is CrimsonLotusWeapon laserWeapon
                ? laserWeapon.CurrentFireDirection : (Vector2)weaponPoint.right;
            Vector2 mouseScreenPos = inputManager.MousePosition;
            genericChargeTarget = Camera.main.ScreenToWorldPoint(new Vector3(mouseScreenPos.x, mouseScreenPos.y, 0f));
            genericChargeTarget.z = 0f;
            // 쿨다운 중 놓은 차징은 충전량과 조준점을 보존하고 공격 가능한 첫 프레임에 발사합니다.
            isGenericChargeReleased = true;
            if (currentWeapon.CanAttack()) FireGenericCharge();
        }

        private void FireGenericCharge()
        {
            HideGenericChargeEffect();
            bool wasCharging = isGenericCharging;
            float chargePercent = GenericChargePercent;
            Vector2 fireDirection = genericChargeDirection;
            Vector3 targetWorldPos = genericChargeTarget;
            isGenericChargePending = false;
            isGenericCharging = false;
            isGenericChargeReleased = false;
            genericChargeTime = 0f;

            LastAttackDirection = fireDirection.normalized;
            playerController?.NotifyCombatActivity();
            OnPlayerAttackStarted?.Invoke(fireDirection, targetWorldPos);
            if (wasCharging)
                currentWeapon.AttackWithGenericCharge(fireDirection, targetWorldPos, chargePercent);
            else
            {
                currentWeapon.ResetGenericChargeMultiplier();
                currentWeapon.Attack(fireDirection, targetWorldPos);
            }
            currentWeapon.AttackEnd();
            OnPlayerAttack?.Invoke(fireDirection, targetWorldPos);
            OnPlayerAttackEnd?.Invoke();
        }

        private float GetGenericMaxChargeTime()
        {
            if (currentWeapon == null || currentWeapon.weaponData == null) return 1f;
            return Mathf.Max(0.01f, currentWeapon.weaponData.maxChargeTime);
        }

        private void ShowGenericChargeEffect()
        {
            HideGenericChargeEffect();
            if (currentWeapon == null || currentWeapon.weaponData == null ||
                currentWeapon.weaponData.chargeEffectPrefab == null) return;

            genericChargeEffectPrefab = currentWeapon.weaponData.chargeEffectPrefab;
            genericChargeEffectPool = ObjectPoolManager.Instance;
            Transform anchor = currentWeapon is RangedWeapon ranged && ranged.firePoint != null
                ? ranged.firePoint
                : currentWeapon is IsolationWeapon isolation && isolation.firePoint != null
                    ? isolation.firePoint : currentWeapon.transform;
            genericChargeEffect = genericChargeEffectPool != null
                ? genericChargeEffectPool.SpawnFromPool(genericChargeEffectPrefab, anchor.position, anchor.rotation)
                : Instantiate(genericChargeEffectPrefab, anchor.position, anchor.rotation);
            if (genericChargeEffect == null) return;
            genericChargeEffect.transform.SetParent(anchor, false);
            genericChargeEffect.transform.localPosition = genericChargeEffectPrefab.transform.localPosition;
            genericChargeEffect.transform.localRotation = genericChargeEffectPrefab.transform.localRotation;
            genericChargeEffect.transform.localScale = genericChargeEffectPrefab.transform.localScale;

            // 무기의 표시 순서를 기준으로 차징 연출을 배치합니다.
            SpriteRenderer weaponRenderer = currentWeapon.GetComponentInChildren<SpriteRenderer>();
            foreach (SpriteRenderer renderer in genericChargeEffect.GetComponentsInChildren<SpriteRenderer>())
            {
                if (weaponRenderer == null) break;
                renderer.sortingLayerID = weaponRenderer.sortingLayerID;
                renderer.sortingOrder = weaponRenderer.sortingOrder + 1;
            }
            foreach (Animator effectAnimator in genericChargeEffect.GetComponentsInChildren<Animator>())
            {
                effectAnimator.Rebind();
                effectAnimator.Play(0, 0, 0f);
                effectAnimator.Update(0f);
            }
        }

        private void HideGenericChargeEffect()
        {
            if (genericChargeEffect != null)
            {
                if (genericChargeEffectPool != null)
                    genericChargeEffectPool.ReturnToPool(genericChargeEffectPrefab.name, genericChargeEffect);
                else Destroy(genericChargeEffect);
            }
            genericChargeEffect = null;
            genericChargeEffectPrefab = null;
            genericChargeEffectPool = null;
        }

        private void CancelGenericCharge()
        {
            HideGenericChargeEffect();
            isGenericChargePending = false;
            isGenericCharging = false;
            genericChargeTime = 0f;
            isGenericChargeReleased = false;
            currentWeapon?.ResetGenericChargeMultiplier();
        }

        private void OnDisable()
        {
            isAttackHeld = false;
            CancelGenericCharge();
            UnsubscribeAttackInput();
        }
    }
}
