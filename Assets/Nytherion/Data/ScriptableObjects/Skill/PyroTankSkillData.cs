using Nytherion.GamePlay.Skills;
using UnityEngine;

namespace Nytherion.Data.ScriptableObjects.Skill
{
    [CreateAssetMenu(fileName = "PyroTank_Skill", menuName = "Data/Skill/Pyro Tank")]
    public class PyroTankSkillData : SkillData
    {
        [Header("조준 방향 점프 등장")]
        [Min(0f)] public float spawnForwardOffset = 0.55f;
        [Min(0f)] public float jumpDistance = 1f;
        [Tooltip("0이면 점프 등장 없이 바로 적을 추적합니다.")]
        [Min(0f)] public float jumpDuration = 0.45f;
        [Tooltip("충돌체는 지면에서 이동하고 그림만 이 높이까지 떠오릅니다.")]
        [Min(0f)] public float jumpHeight = 0.55f;
        public AnimationClip jumpAnimation;

        [Header("전차 돌진")]
        [Tooltip("탐색 반경은 기본 사거리(range)를 사용하며 전차의 현재 위치가 중심입니다.")]
        [Min(0.01f)] public float moveSpeed = 6f;
        [Min(0.01f)] public float lifetime = 5f;
        [Min(0.01f)] public float collisionRadius = 0.22f;
        [Tooltip("원본 이미지의 앞부분이 왼쪽인 경우에만 켭니다. 폭열 전차 원본은 오른쪽이 앞입니다.")]
        public bool invertFacing = false;
        public LayerMask enemyLayers;
        public LayerMask obstacleLayers;
        public PyroTankController tankPrefab;

        [Header("폭발")]
        [Min(0.01f)] public float explosionRadius = 1.5f;
        public AnimationClip explosionAnimation;

        public float ExplosionDuration => explosionAnimation != null ? explosionAnimation.length : 0.7f;
    }
}
