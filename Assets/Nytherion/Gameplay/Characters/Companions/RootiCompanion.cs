using UnityEngine;

namespace Nytherion.GamePlay.Characters.Companions
{
    /// <summary>
    /// 꽃채찍을 휘두른 뒤 지정 프레임에서 씨앗을 발사하는 루티 소환수입니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RootiCompanion : SummonedCompanion
    {
        private Transform pendingTarget;
        private bool isSeedLaunchPending;

        protected override bool TryAttack(Transform target)
        {
            if (target == null || isSeedLaunchPending)
            {
                return false;
            }

            pendingTarget = target;
            isSeedLaunchPending = true;
            attackFreezeTimer = attackFreezeDuration;
            SetMoving(false);
            TriggerAttackAnimation();
            return true;
        }

        /// <summary>
        /// Flower Whip Attack의 세 번째 프레임 Animation Event에서 호출됩니다.
        /// </summary>
        public void LaunchSeed()
        {
            if (!isSeedLaunchPending)
            {
                return;
            }

            Transform target = pendingTarget;
            pendingTarget = null;
            isSeedLaunchPending = false;
            LaunchProjectileAtTarget(target);
        }

        private void OnDisable()
        {
            pendingTarget = null;
            isSeedLaunchPending = false;
        }
    }
}
