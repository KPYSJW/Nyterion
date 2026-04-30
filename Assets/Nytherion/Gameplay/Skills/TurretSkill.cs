using Nytherion.Data.ScriptableObjects.Skill;
using UnityEngine;
using UnityEngine.AI;

namespace Nytherion.GamePlay.Skills
{
    /// <summary>
    /// 마우스 위치를 기반으로 유효한 바닥(NavMesh)을 탐색하여 터렛을 소환하는 스킬 로직 클래스
    /// </summary>
    public class TurretSkill : SkillBase
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
                Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
                mouseWorldPos.z = 0f; // 2D 환경이므로 z축은 0으로 고정

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
                
                if (NavMesh.SamplePosition(targetPosition, out hit, turretData.searchRadius, floorMask))
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
                    GameObject turretInstance = Instantiate(turretData.turretPrefab, finalSpawnPosition, Quaternion.identity);
                    
                    // 터렛 컨트롤러 컴포넌트를 찾아 데이터 주입
                    if (turretInstance.TryGetComponent(out TurretController controller))
                    {
                        controller.Initialize(turretData);
                    }
                }
            }
            else
            {
                Debug.LogError("[TurretSkill] 할당된 skillData가 TurretSkillData 타입이 아닙니다.");
            }
        }
    }
}