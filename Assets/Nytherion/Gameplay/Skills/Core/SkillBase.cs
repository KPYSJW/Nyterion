using Nytherion.Data.ScriptableObjects.Skill;
using UnityEngine;
using Nytherion.GamePlay.Characters.Player;

namespace Nytherion.GamePlay.Skills
{
    /// <summary>
    /// 게임 내 모든 스킬의 뼈대가 되는 추상 기본 클래스
    /// 스킬의 데이터 참조, 시전자 정보, 쿨다운 관리 및 기본 실행 흐름을 담당
    /// </summary>
    public abstract class SkillBase : MonoBehaviour
    {
        /// <summary> 스킬의 기본 정보와 능력치를 담고 있는 데이터/// </summary>
        public SkillData skillData;

        protected float EffectSizeMultiplier
        {
            get
            {
                var player = caster != null ? caster.GetComponentInParent<Nytherion.Core.Managers.PlayerManager>()
                    : GetComponentInParent<Nytherion.Core.Managers.PlayerManager>();
                return player != null && player.currentPlayerData != null
                    ? Mathf.Max(0.01f, player.currentPlayerData.projectileSizeMultiplier) : 1f;
            }
        }

        /// <summary> 스킬을 시전 한 주체의 Transform/// </summary>
        [System.NonSerialized] public Transform caster;

        /// <summary> 발사체 기반  스킬인 경우 발사되는 기준 위치/// </summary>
        [System.NonSerialized] public Transform firePoint;

        /// <summary> 마지막으로 스킬을 사용한 시간 (초기값은 게임 시작 즉시 사용 가능하도록 음수 무한대로 설정/// </summary>
        [System.NonSerialized] private float lastUseTime = -Mathf.Infinity;

        // 테스트 감소율은 스킬 에셋이나 저장 데이터에 기록하지 않습니다.
        public float DebugCooldownReduction { get; private set; }

        public virtual void SetDebugCooldownReduction(float reduction)
        {
            if (float.IsNaN(reduction) || float.IsInfinity(reduction)) return;
            DebugCooldownReduction = Mathf.Clamp01(reduction);
        }

        protected Nytherion.Core.Managers.PlayerManager SkillOwner => caster != null
            ? caster.GetComponentInParent<Nytherion.Core.Managers.PlayerManager>()
            : GetComponentInParent<Nytherion.Core.Managers.PlayerManager>();

        protected float SkillDamageMultiplier => SkillOwner != null && SkillOwner.currentPlayerData != null
            ? Mathf.Max(0f, SkillOwner.currentPlayerData.skillDamageMultiplier) : 1f;

        protected float ApplySkillDamage(float damage) => Mathf.Max(0f, damage) * SkillDamageMultiplier;

        protected float ApplyDebugCooldownReduction(float duration) =>
            Mathf.Max(0f, duration) * (1f - DebugCooldownReduction) *
            (SkillOwner != null && SkillOwner.currentPlayerData != null
                ? Mathf.Max(0f, SkillOwner.currentPlayerData.skillCooldownMultiplier) : 1f);

        /// <summary>장착 해제 중에도 게임 시간 기준으로 유지되는 쿨타임 상태.</summary>
        public class CooldownState
        {
            public float LastUseTime = -Mathf.Infinity;
        }

        public virtual CooldownState CaptureCooldownState() => new CooldownState { LastUseTime = lastUseTime };

        public virtual void RestoreCooldownState(CooldownState state)
        {
            if (state != null) lastUseTime = state.LastUseTime;
        }


        /// <summary>
        ///  스킬 사용을 시도. 사용 가능하다면 스킬을 활성화하고 쿨다운을 초기화
        /// </summary>
        /// <returns></returns>
        public virtual bool CanUse() => skillData != null && skillData.IsRuntimeAvailable &&
            HasRequiredMana() && Time.time >= lastUseTime + GetCooldownTime();

        private PlayerSkillManager ManaOwner => caster != null
            ? caster.GetComponentInParent<PlayerSkillManager>() : GetComponentInParent<PlayerSkillManager>();

        protected bool HasRequiredMana() => skillData != null && (skillData.manaCost <= 0 ||
            (ManaOwner != null && ManaOwner.HasMana(skillData.manaCost)));

        protected bool TrySpendMana() => skillData != null && (skillData.manaCost <= 0 ||
            (ManaOwner != null && ManaOwner.TrySpendMana(skillData.manaCost)));

        /// <summary>
        /// 실제 스킬의 효과나 로직이 구현되는 메서드
        /// </summary>
        /// <returns>스킬 사용 성공 여부</returns>
        public virtual bool TryUse()
        {

            if (CanUse() && TrySpendMana())
            {
                Activate();
                lastUseTime = Time.time;
                ManaOwner?.NotifySkillUsed(this);
                return true;
            }
            return false;
        }

        /// <summary>
        /// 실제 스킬의 효과나 로직이 구현되는 메서드
        /// </summary>
        protected abstract void Activate();

        /// <summary>
        /// 스킬의 전체 쿨다운 시간을 반환
        /// </summary>
        public virtual float GetCooldownTime() => ApplyDebugCooldownReduction(skillData.coolDown);

        /// <summary>
        /// 스킬이 다시 사용 가능해질 때까지 남은 시간을 반환
        /// </summary>
        /// <returns>남은 쿨다운 시간(최소 0초)</returns>
        public virtual float GetRemainingCooldown()
        {
            float remaining = (lastUseTime + GetCooldownTime()) - Time.time;
            return Mathf.Max(0f, remaining);
        }
    }
}

