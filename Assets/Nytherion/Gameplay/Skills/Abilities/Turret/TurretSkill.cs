using Nytherion.Data.ScriptableObjects.Skill;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using Nytherion.Core.Managers;

namespace Nytherion.GamePlay.Skills
{
    /// <summary>
    /// 조준 위치의 바닥에 포탑을 배치하며, 배치 연출이 켜져 있으면 시전자에게서 날아갑니다.
    /// </summary>
    public class TurretSkill : SkillBase
    {
        private bool chargesInitialized;
        private int charges;
        private int chargeCapacity;
        private float nextRechargeTime = Mathf.Infinity;
        private float nextSummonTime = -Mathf.Infinity;

        private RootiUpgradeRuntime Upgrades => caster != null ? caster.GetComponent<RootiUpgradeRuntime>() : null;
        public int CurrentCharges { get { RefreshCharges(); return charges; } }
        public int MaxCharges { get { RefreshCharges(); return chargeCapacity; } }
        public float RemainingRechargeTime
        {
            get
            {
                RefreshCharges();
                return charges < chargeCapacity ? Mathf.Max(0f, nextRechargeTime - Time.time) : 0f;
            }
        }

        private void Update() => RefreshCharges();

        public override bool CanUse()
        {
            if (!(skillData is TurretSkillData data) || caster == null || data.turretPrefab == null ||
                data.turretPrefab.GetComponent<TurretController>() == null) return false;
            RefreshCharges();
            return charges > 0 && Time.time >= nextSummonTime;
        }

        public override bool TryUse()
        {
            if (!CanUse()) return false;
            Activate();
            charges--;
            TurretSkillData data = (TurretSkillData)skillData;
            nextSummonTime = Time.time + Mathf.Max(0.5f, data.minimumSummonInterval);
            // 두 번째 충전을 사용해도 이미 진행 중인 첫 번째 회복은 초기화하지 않습니다.
            if (float.IsPositiveInfinity(nextRechargeTime))
                nextRechargeTime = Time.time + Mathf.Max(0.01f, data.coolDown);
            return true;
        }

        public override float GetRemainingCooldown()
        {
            RefreshCharges();
            float recovery = charges > 0 ? 0f : Mathf.Max(0f, nextRechargeTime - Time.time);
            return Mathf.Max(recovery, nextSummonTime - Time.time, 0f);
        }

        private void RefreshCharges()
        {
            if (!(skillData is TurretSkillData data)) return;
            RootiUpgradeRuntime upgrades = Upgrades;
            int capacity = Mathf.Max(1, data.maxCharges) + (upgrades != null ? upgrades.AdditionalCharges : 0);
            if (!chargesInitialized)
            {
                chargesInitialized = true;
                charges = chargeCapacity = capacity;
                return;
            }
            float rechargeDuration = Mathf.Max(0.01f, data.coolDown);
            while (charges < chargeCapacity && Time.time >= nextRechargeTime)
            {
                charges++;
                nextRechargeTime += rechargeDuration;
            }
            chargeCapacity = capacity;
            charges = Mathf.Min(charges, chargeCapacity);
            if (charges >= chargeCapacity) nextRechargeTime = Mathf.Infinity;
            else if (float.IsPositiveInfinity(nextRechargeTime)) nextRechargeTime = Time.time + rechargeDuration;
        }

        /// <summary>
        /// 스킬 실행 시 호출되는 활성화 메서드.
        /// 목표 위치를 계산하고 내비메시 검사를 통해 터렛 생성
        /// </summary>
        protected override void Activate()
        {
            if (skillData is TurretSkillData turretData)
            {
                // 시전자(플레이어)의 현재 위치를 가져옴
                Vector3 playerPosition = caster.position;

                // 마우스 월드 좌표를 가져와서 목표 위치 계산
                Camera aimCamera = Camera.main;
                Vector2 mouseScreenPosition = Mouse.current != null
                    ? Mouse.current.position.ReadValue()
                    : Vector2.zero;
                Vector3 mouseWorldPos = aimCamera != null && Mouse.current != null
                    ? aimCamera.ScreenToWorldPoint(new Vector3(mouseScreenPosition.x, mouseScreenPosition.y, 0f))
                    : playerPosition;
                mouseWorldPos.z = playerPosition.z;

                Vector3 directionToMouse = mouseWorldPos - playerPosition;
                
                // 마우스 위치가 설정된 탐색 반경(searchRadius)을 벗어난 경우 최대 반경으로 제한
                if (directionToMouse.magnitude > turretData.searchRadius)
                {
                    directionToMouse = directionToMouse.normalized * turretData.searchRadius;
                }
                Vector3 targetPosition = playerPosition + directionToMouse;

                // 내비메시 시스템에서 터렛이 배치될 수 있는 바닥 영역의 고유 마스크 값 계산
                int areaIndex = NavMesh.GetAreaFromName(turretData.floorAreaName);
                int floorMask = areaIndex != -1 ? 1 << areaIndex : NavMesh.AllAreas;

                // 내비메시 시스템을 통해 목표 위치 근처의 유효한 스폰 위치 탐색(SamplePosition)
                Vector3 finalSpawnPosition;
                NavMeshHit hit;
                
                if (turretData.launchAroundCaster)
                {
                    finalSpawnPosition = FindAimedLandingPosition(playerPosition, mouseWorldPos, turretData, floorMask);
                }
                else if (NavMesh.SamplePosition(targetPosition, out hit, turretData.searchRadius, floorMask))
                {
                    // 유효한 바닥 지점을 성공적으로 찾은 경우
                    finalSpawnPosition = hit.position;
                }
                else
                {
                    // 주변에 바닥이 없어 예외가 발생한 경우, 시전자 위치로 고정
                    finalSpawnPosition = playerPosition;
                }

                // 도출된 최종 좌표에 터렛 프리팹 생성 및 초기화
                if (turretData.turretPrefab != null)
                {
                    RootiUpgradeRuntime upgrades = Upgrades;
                    int summonCount = 1 + (upgrades != null ? upgrades.AdditionalSummons : 0);
                    Vector3 aim = mouseWorldPos - playerPosition;
                    Vector3 side = aim.sqrMagnitude > 0.0001f
                        ? new Vector3(-aim.y, aim.x, 0f).normalized : Vector3.up;
                    for (int i = 0; i < summonCount; i++)
                    {
                        Vector3 destination = finalSpawnPosition;
                        if (summonCount > 1)
                        {
                            Vector3 candidate = destination + side * ((i - (summonCount - 1) * 0.5f) * turretData.duplicateSpacing);
                            if (turretData.launchAroundCaster)
                                destination = FindAimedLandingPosition(playerPosition, candidate, turretData, floorMask);
                            else if (NavMesh.SamplePosition(candidate, out NavMeshHit duplicateHit, 0.2f, floorMask))
                                destination = duplicateHit.position;
                        }
                        Vector3 launchPosition = turretData.launchAroundCaster ? playerPosition : destination;
                        ObjectPoolManager pool = ObjectPoolManager.Instance;
                        GameObject turretInstance = pool != null
                            ? pool.SpawnFromPool(turretData.turretPrefab, launchPosition, Quaternion.identity, 3)
                            : Instantiate(turretData.turretPrefab, launchPosition, Quaternion.identity);
                        if (turretInstance == null) continue;
                        TurretController controller = turretInstance.GetComponent<TurretController>();
                        controller.SetPool(pool, turretData.turretPrefab.name);
                        controller.Initialize(turretData);
                        controller.EffectSizeMultiplier = EffectSizeMultiplier;
                        if (controller is RootiTurretController rooti) rooti.SetUpgradeOwner(caster);
                        controller.Deploy(launchPosition, destination);
                    }
                }
            }
            else
            {
                Debug.LogError("[TurretSkill] 할당된 skillData가 TurretSkillData 타입이 아닙니다.");
            }
        }

        private static Vector3 FindAimedLandingPosition(
            Vector3 playerPosition,
            Vector3 aimPosition,
            TurretSkillData data,
            int floorMask)
        {
            float maximumDistance = Mathf.Max(0.1f, data.searchRadius);
            float minimumDistance = Mathf.Clamp(data.minimumDeploymentDistance, 0f, maximumDistance);
            Vector3 aimOffset = aimPosition - playerPosition;
            aimOffset.z = 0f;
            if (aimOffset.sqrMagnitude < 0.0001f) return playerPosition;

            Vector3 aimDirection = aimOffset.normalized;
            float targetDistance = Mathf.Clamp(aimOffset.magnitude, minimumDistance, maximumDistance);

            // NavMeshPlus가 굽는 바닥은 캐릭터의 Z와 다를 수 있으므로 먼저 실제 NavMesh 깊이를 구한다.
            if (!NavMesh.SamplePosition(playerPosition, out NavMeshHit casterFloorHit,
                Mathf.Max(1f, maximumDistance), floorMask))
            {
                return playerPosition;
            }

            for (int i = 0; i < 12; i++)
            {
                // 목표 지점부터 시전자 쪽으로 같은 조준선상의 바닥만 탐색한다.
                float distance = Mathf.Lerp(targetDistance, minimumDistance, i / 11f);
                Vector3 candidate = playerPosition + aimDirection * distance;
                Vector3 floorCandidate = candidate;
                floorCandidate.z = casterFloorHit.position.z;
                if (!NavMesh.SamplePosition(floorCandidate, out NavMeshHit hit, 0.2f, floorMask)) continue;

                Vector3 landingPosition = hit.position;
                landingPosition.z = playerPosition.z;
                // 벽 가장자리에서 조준선 옆이나 뒤의 바닥으로 위치가 보정되는 것을 막는다.
                if (Vector2.Distance(candidate, landingPosition) > 0.01f) continue;
                float landingDistance = Vector2.Distance(playerPosition, landingPosition);
                if (Vector3.Dot(landingPosition - playerPosition, aimDirection) > 0f &&
                    landingDistance >= minimumDistance - 0.001f && landingDistance <= maximumDistance + 0.001f)
                {
                    return landingPosition;
                }
            }

            // 조준 방향에 배치 가능한 바닥이 없으면 시전자 자리에서 착지한다.
            return playerPosition;
        }
    }
}
