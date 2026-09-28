using Nytherion.Data.ScriptableObjects.Skill;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

namespace Nytherion.GamePlay.Skills
{
    /// <summary>
    /// 조준 위치의 바닥에 포탑을 배치하며, 배치 연출이 켜져 있으면 시전자에게서 날아갑니다.
    /// </summary>
    public class LegacyTurretSkill : SkillBase
    {
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
                    Vector3 launchPosition = turretData.launchAroundCaster ? playerPosition : finalSpawnPosition;
                    GameObject turretInstance = Instantiate(turretData.turretPrefab, launchPosition, Quaternion.identity);
                    
                    // 터렛 컨트롤러 컴포넌트를 찾아 데이터 주입
                    if (turretInstance.TryGetComponent(out TurretController controller))
                    {
                        controller.Initialize(turretData);
                        controller.Deploy(launchPosition, finalSpawnPosition);
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
            for (int i = 0; i < 12; i++)
            {
                // 목표 지점부터 시전자 쪽으로 같은 조준선상의 바닥만 탐색한다.
                float distance = Mathf.Lerp(targetDistance, minimumDistance, i / 11f);
                Vector3 candidate = playerPosition + aimDirection * distance;
                if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 0.05f, floorMask)) continue;

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
