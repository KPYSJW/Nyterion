using Nytherion.Core.Managers;
using Nytherion.Core.Enums;
using Nytherion.Data.ScriptableObjects.Skill;
using Nytherion.GamePlay.Skills;
using UnityEngine;
using VContainer;

namespace Nytherion.GamePlay.Characters.Player
{
    /// <summary>
    /// 플레이어의 스킬 장착, 관리 및 사용자 입력을 통한 스킬 실행을 담당하는 클래스
    /// </summary>
    public class PlayerSkillManager : MonoBehaviour
    {
        /// <summary> 스킬이 발사될 위치 /// </summary>
        public Transform weaponPoint;

        /// <summary> 현재 플레이어가 장착 중인 스킬들의 배열 /// </summary>
        public SkillBase[] equippedSkills = new SkillBase[3];

        /// <summary> 생성된 스킬 인스턴스들을 자식으로 담아둘 부모 오브젝트/// </summary>
        public Transform skillHolder;

        private IProgressionManager progressionManager;

        [Inject]
        public void Construct(IProgressionManager progressionManager)
        {
            this.progressionManager = progressionManager;
        }

        void Start()
        {
            // InputManager의 스킬 입력 이벤트에 스킬 사용 메서드를 구독
            if (InputManager.Instance != null)
                InputManager.Instance.onSkillInput += SkillInput;
        }

        private void OnDestroy()
        {
            // 메모리 누수 방지를 위해 오브젝트 파괴 시 이벤트 구독을 해제
            if (InputManager.Instance != null)
                InputManager.Instance.onSkillInput -= SkillInput;
        }

        /// <summary>
        /// InputManager로부터 전달받은 인덱스에 해당하는 스킬을 사용
        /// </summary>
        /// <param name="index">사용할 스킬의 슬롯 번호</param>
        void SkillInput(int index)
        {
            // 인덱스가 배열 범위를 벗어나지 않고, 해당 슬롯에 장착된 스킬이 있을 때만 실행
            if (index >= 0 && index < equippedSkills.Length && equippedSkills[index] != null)
            {
                if (equippedSkills[index].TryUse())
                {
                    // 스킬 사용 진척도 업데이트
                    progressionManager?.ProcessAction(ProgressionType.UseSkill, 1);
                }
            }
        }

        /// <summary>
        /// 여러 개의 스킬 데이터를 배열 형태로 한 번에 장착
        /// 주로 로드 할 때 전체 스킬 세팅 변경 시 사용
        /// </summary>
        /// <param name="newSkills">장착할 스킬 데이터 배열</param>
        public void SetEquippedSkills(SkillData[] newSkills)
        {
            for (int i = 0; i < newSkills.Length; i++)
            {
                EquipSkill(newSkills[i], i);
            }
        }

        /// <summary>
        /// 특정 슬롯에 스킬을 새롭게 장착, 필요한 컴포넌트 초기화
        /// 기존에 장착된 스킬이 있다면 파괴하고 교체
        /// </summary>
        /// <param name="newSkillData">장착할 스킬 데이터</param>
        /// <param name="slotIndex">스킬을 장착할 슬롯의 인덱스</param>
        private void EquipSkill(SkillData newSkillData, int slotIndex)
        {
            // 슬롯 번호가 유효하지 않으면 조기 종료
            if (slotIndex < 0 || slotIndex >= equippedSkills.Length) return;

            // 이미 해당 슬롯에 장착된 스킬이 있다면 기존 오브젝트를 메모리에서 제거 
            if (equippedSkills[slotIndex] != null)
            {
                Destroy(equippedSkills[slotIndex].gameObject);
                equippedSkills[slotIndex] = null;
            }

            // 새로운 스킬 데이터와 생성할 프리팹이 존재하는 경우 인스턴스화
            if (newSkillData != null && newSkillData.skillPrefab != null)
            {
                // 별도의 부모 오브젝트가 없다면 플레이어 자신을 부모로 설정 
                Transform parentTransform = skillHolder != null ? skillHolder : transform;
                GameObject skillInstance = Instantiate(newSkillData.skillPrefab, parentTransform);

                // 프리팹에 스킬 로직을 담당하는 SkillBase 컴포넌트가 있는지 확인
                if (skillInstance.TryGetComponent(out SkillBase skillBase))
                {

                    // 스킬 초기화: 데이터, 시전자, 발사 위치 할당
                    skillBase.skillData = newSkillData;
                    skillBase.caster = transform;
                    skillBase.firePoint = weaponPoint;

                    // 장착된 스킬 배열에 스킬 등록
                    equippedSkills[slotIndex] = skillBase;
                }
                else
                {
                    // 컴포넌트 누락으로 인한 오류 발생 시, 생성된 객체를 즉시 파괴
                    Debug.LogError($"[PlayerSkillManager] {newSkillData.skillName} 스킬 프리팹에 SkillBase(또는 상속 클래스)가 없습니다! 객체가 파괴되지 않고 쌓이게 됩니다.");
                    Destroy(skillInstance); 
                }
            }
        }
    }
}
