using Nytherion.GamePlay.Skills;
using UnityEngine;

namespace Nytherion.Data.ScriptableObjects.Skill
{
    [CreateAssetMenu(fileName = "MeteorStrike_Skill", menuName = "Data/Skill/Meteor Strike")]
    public class MeteorStrikeSkillData : SkillData
    {
        [Header("레벨 성장")]
        [Min(0f)] public float damagePerLevel = 20f;

        [Header("화면 전체 순차 조준")]
        [Tooltip("각 적을 향한 운석의 낙하 시작 간격입니다.")]
        [Min(0.01f)] public float strikeInterval = 0.12f;
        public LayerMask targetLayers;

        [Header("운석 연출")]
        [Min(0.01f)] public float dropHeight = 10f;
        [Tooltip("목표 기준 낙하 시작점의 가로 거리입니다. 음수는 왼쪽 위, 양수는 오른쪽 위, 0은 수직 낙하입니다.")]
        public float dropHorizontalOffset = -6f;
        [Min(0.01f)] public float fallSpeed = 20f;
        [Tooltip("착탄 후 폭발만 세로로 보정합니다. 양수는 아래, 음수는 위로 이동하며 낙하 경로에는 영향을 주지 않습니다.")]
        public float explosionYOffset = 0.4f;
        public MeteorStrikeImpact impactPrefab;

        public float GetDamage(int level) => Mathf.Max(0f, damage + Mathf.Max(0f, damagePerLevel) * Mathf.Max(0, level - 1));
    }
}
