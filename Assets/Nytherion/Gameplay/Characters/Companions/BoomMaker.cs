using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace Nytherion.GamePlay.Characters.Companions
{
    /// <summary>
    /// 적을 발견하면 회전 동작과 함께 폭발 로봇을 내보내는 BoomMaker입니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BoomMaker : SummonedCompanion
    {
        [Header("폭발 로봇 소환")]
        [FormerlySerializedAs("pilotPrefab")]
        [SerializeField] private GameObject explosiveRobotPrefab;
        [FormerlySerializedAs("pilotSpawnOffset")]
        [SerializeField] private Vector3 explosiveRobotSpawnOffset = new Vector3(0f, -0.2f, 0f);
        [FormerlySerializedAs("maximumActivePilots")]
        [SerializeField, Min(1)] private int maximumActiveRobots = 3;

        private readonly List<ExplosiveRobot> activeRobots = new List<ExplosiveRobot>();
        private bool hasLoggedMissingRobot;
        private Transform pendingTarget;
        private bool isSummonPending;

        protected override bool TryAttack(Transform target)
        {
            activeRobots.RemoveAll(robot => robot == null);
            if (target == null || isSummonPending || activeRobots.Count >= Mathf.Max(1, maximumActiveRobots))
            {
                return false;
            }

            if (explosiveRobotPrefab == null)
            {
                if (!hasLoggedMissingRobot)
                {
                    hasLoggedMissingRobot = true;
                    Debug.LogWarning("[BoomMaker] 폭발 로봇 프리팹이 연결되지 않았습니다.", this);
                }
                return false;
            }

            pendingTarget = target;
            isSummonPending = true;
            attackFreezeTimer = attackFreezeDuration;
            SetMoving(false);
            TriggerAttackAnimation();
            return true;
        }

        /// <summary>
        /// 소환 애니메이션의 세 번째 프레임 이벤트에서 호출됩니다.
        /// </summary>
        public void DeployExplosiveRobot()
        {
            if (!isSummonPending)
            {
                return;
            }

            isSummonPending = false;
            Transform target = pendingTarget;
            pendingTarget = null;
            activeRobots.RemoveAll(robot => robot == null);
            if (explosiveRobotPrefab == null || activeRobots.Count >= Mathf.Max(1, maximumActiveRobots))
            {
                return;
            }

            Vector3 spawnPosition = transform.TransformPoint(explosiveRobotSpawnOffset);
            GameObject robotObject = Instantiate(explosiveRobotPrefab, spawnPosition, Quaternion.identity);
            ExplosiveRobot robot = robotObject.GetComponent<ExplosiveRobot>();
            if (robot == null)
            {
                Debug.LogError("[BoomMaker] 폭발 로봇 프리팹에 ExplosiveRobot 컴포넌트가 없습니다.", this);
                Destroy(robotObject);
                return;
            }

            robot.Initialize(
                transform,
                target,
                GetProjectileDamage(ownerCombat != null ? ownerCombat.currentWeapon : null),
                spriteRenderer);
            activeRobots.Add(robot);
        }

        private void OnDisable()
        {
            pendingTarget = null;
            isSummonPending = false;
        }
    }
}
