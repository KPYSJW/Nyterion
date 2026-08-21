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
        
        /// <summary> 마우스 포인터를 기준으로 터렛이 소환될 수 있는 최대 탐색 반경 </summary>
        [Tooltip("마우스 포인터를 기준으로 터렛이 소환될 수 있는 최대 반경.")]
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

        [Header("Projectile Settings")]
        
        /// <summary> 오브젝트 풀에서 꺼내올 투사체의 식별 태그 </summary>
        [Tooltip("오브젝트 풀에서 사용할 투사체의 태그")]
        public string projectilePoolTag = "Player_Arrow";
        
        /// <summary> 터렛이 발사하는 투사체의 이동 속도 </summary>
        [Tooltip("발사된 투사체의 이동 속도")]
        public float projectileSpeed = 10f;
    }
}
