using UnityEngine;

namespace Nytherion.Data.ScriptableObjects.Skill
{
    /// <summary>
    /// 터렛 스킬의 기본 속성을 정의하는 ScriptableObject 클래스
    /// </summary>
    [CreateAssetMenu(fileName = "TurretSkillData", menuName = "Data/Skill/TurretSkillData")]
    public class TurretSkillData : SkillData
    {
        [Header("Turret Settings")]
        
        /// <summary> 소환될 터렛의 프리팹 </summary>
        [Tooltip("소환될 터렛의 게임 오브젝트 프리팹")]
        public GameObject turretPrefab;
        
        /// <summary> 시전자에서 조준 방향으로 포탑을 배치할 수 있는 최대 거리 </summary>
        [Tooltip("시전자에서 포탑 착지 지점까지의 최대 거리입니다.")]
        public float searchRadius;
        
        /// <summary> 터렛이 배치될 수 있는 내비메시 영역의 이름 </summary>
        [Tooltip("터렛이 배치될 수 있는 내비메시 상의 바닥 영역 이름.")]
        public string floorAreaName = "Floor";
        
        /// <summary> 맵에 동시에 존재할 수 있는 터렛의 최대 개수 </summary>
        [Tooltip("맵에 동시에 존재할 수 있는 터렛의 최대 개수. 초과 시 가장 오래된 터렛이 파괴.")]
        public int maxTurretCount = 3;
        
        /// <summary> 터렛이 소환된 후 유지되는 시간 </summary>
        [Tooltip("터렛이 필드에 유지되는 시간")]
        public float duration = 10f;
        
        /// <summary> 터렛의 공격 주기</summary>
        [Tooltip("터렛이 투사체를 발사하는 간격")]
        public float attackInterval = 1f;

        [Header("배치 연출")]
        [Tooltip("시전자 위치에서 조준 방향의 바닥으로 날아가 착지합니다.")]
        public bool launchAroundCaster;

        [Min(0f), Tooltip("시전자와 착지 지점 사이의 최소 거리입니다.")]
        public float minimumDeploymentDistance = 0.6f;

        [Header("Projectile Settings")]
        
        /// <summary> 오브젝트 풀에서 꺼내올 투사체의 식별 태그 </summary>
        [Tooltip("오브젝트 풀에서 사용할 투사체의 태그")]
        public string projectilePoolTag = "Player_Arrow";

        [Tooltip("연결되어 있으면 풀 태그 대신 이 투사체 프리팹을 사용합니다.")]
        public GameObject projectilePrefab;

        [Tooltip("포탑 기준 발사 위치입니다.")]
        public Vector3 projectileSpawnOffset;
        
        /// <summary> 터렛이 발사하는 투사체의 이동 속도 </summary>
        [Tooltip("발사된 투사체의 이동 속도")]
        public float projectileSpeed = 10f;
    }
}
