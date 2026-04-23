using Nytherion.Core.Managers;
using Nytherion.GamePlay.Characters.Player;
using Nytherion.Data.ScriptableObjects.Skill;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using Nytherion.Core.Enums;
using Nytherion.UI.Components;

namespace Nytherion.UI.Skill
{
    /// <summary>
    /// 스킬 UI 전체를 제어하며, UI 슬롯과 게임 데이터 매니저를 동기화하는 클래스
    /// </summary>
    public class SkillUIController : MonoBehaviour
    {
        [Header("Toggle Settings")]
        [Tooltip("스킬 UI 전체를 담고 있는 패널 오브젝트")]
        [SerializeField] private GameObject uiPanel;

        [Header("UI References")]
        [Tooltip("장착된 스킬을 표시할 UI 슬롯 배열")]
        [SerializeField] private SkillSlotUI[] equipSlots;

        [Tooltip("보유 중인 스킬 목록이 생성될 부모 Transform")]
        [SerializeField] private Transform storageContent;

        [Tooltip("스킬 슬롯 UI 프리팹")]
        [SerializeField] private GameObject slotPrefab;

        [Tooltip("보관함에 생성할 최대 슬롯 개수")]
        [SerializeField] private int maxStorageSlots = 20;


        private InputManager inputManager;
        private SkillDataManager skillDataManager;
        private PlayerSkillManager playerSkillManager;
        private SaveLoadManager saveLoadManager;
        private IProgressionManager progressionManager;

        private SkillSlotUI[] storageSlots;

        [Tooltip("스킬 보관함 영역 (드롭 이벤트를 처리하기 위한 참조)")]
        [SerializeField] private SkillStorageArea storageDropArea;


        /// <summary>
        /// VContainer를 통해 필요한 매니저와 UI 참조를 주입받아 초기화
        /// </summary>
        [Inject]
        public void Construct(
            SkillDataManager skillDataManager,
            PlayerSkillManager playerSkillManager,
            SaveLoadManager saveLoadManager,
            InputManager inputManager,
            IProgressionManager progressionManager,
            GameSceneUIRefs uiRefs
            )
        {
            this.skillDataManager = skillDataManager;
            this.playerSkillManager = playerSkillManager;
            this.saveLoadManager = saveLoadManager;
            this.inputManager = inputManager;
            this.progressionManager = progressionManager;
            this.uiPanel = uiRefs.SkillMainPanel;
            this.storageDropArea = uiRefs.storageDropArea;
            this.storageContent = uiRefs.storageContent;
            this.equipSlots = uiRefs.equipSlots;
        }

        private void Start()
        {
            // 보관함 슬롯 UI 객체들을 미리 생성
            InitializeStorageSlots();

            // 장착 슬롯에 상호작용 이벤트 등록
            foreach (var slot in equipSlots)
            {
                slot.OnDoubleClick += HandleDoubleClick;
                slot.OnDropSkill += HandleDrop;
            }

            // 시작 시 UI 패널 숨김 처리
            if (uiPanel != null) uiPanel.SetActive(false);

            // 데이터 변경 시 UI 동기화하도록 이벤트 구독
            if (skillDataManager != null)
            {
                skillDataManager.OnSkillDataChanged += SyncUIFromData;
                SyncUIFromData(); // 초기 데이터로 UI 1회 갱신
            }

            // 특정 영역에 드롭 시 보관함으로 이동시키는 이벤트 구독
            if (storageDropArea != null) storageDropArea.OnDropToStorage += HandleDropToStorageBackground;

            // 단축키 입력을 통한 UI 토글 이벤트 구독
            if (inputManager != null) inputManager.onToggleSkillUI += ToggleUI;

            // 새 스킬 해금 이벤트 구독
            if (progressionManager != null) progressionManager.OnSkillUnlocked += HandleSkillUnlocked;

        }
        private void OnDestroy()
        {
            // 메모리 누수 방지를 위해 객체 파괴 시 모든 이벤트 구독 해제
            if (skillDataManager != null) skillDataManager.OnSkillDataChanged -= SyncUIFromData;
            if (inputManager != null) inputManager.onToggleSkillUI -= ToggleUI;
            if (progressionManager != null) progressionManager.OnSkillUnlocked -= HandleSkillUnlocked;
        }

        /// <summary>
        /// 스킬 UI 패널의 활성화 상태를 켜거나 끈다.
        /// </summary>
        public void ToggleUI()
        {
            bool isActive = !uiPanel.activeSelf;
            uiPanel.SetActive(isActive);

            if (isActive)
            {
                // UI가 열릴 때 최신 데이터로 동기화
                SyncUIFromData();
            }
            else
            {
                // UI가 닫힐 때 켜져있을 수 있는 툴팁 강제로 숨김
                if (TooltipPanel.Instance != null)
                {
                    TooltipPanel.Instance.HideTooltip();
                }
            }
        }

        /// <summary>
        /// 지정된 최대 갯수만큼 보관함 슬롯 프리팹을 생성하고 이벤트를 할당
        /// </summary>
        private void InitializeStorageSlots()
        {
            storageSlots = new SkillSlotUI[maxStorageSlots];
            for (int i = 0; i < maxStorageSlots; i++)
            {
                GameObject go = Instantiate(slotPrefab, storageContent);
                SkillSlotUI newSlot = go.GetComponent<SkillSlotUI>();

                newSlot.slotType = SkillSlotType.Storage;
                newSlot.slotIndex = i;

                newSlot.Setup(null, skillDataManager);

                // 상호작용 이벤트 등록 
                newSlot.OnDoubleClick += HandleDoubleClick;
                newSlot.OnDropSkill += HandleDrop;

                storageSlots[i] = newSlot;
            }
        }

        /// <summary>
        /// SkillDataManager가 가지고 있는 데이터를 읽어와서 UI 슬롯에 반영
        /// </summary>
        public void SyncUIFromData()
        {
            if (skillDataManager == null) return;
            if (storageSlots == null || equipSlots == null) return;

            // 장착 슬롯 갱신
            int equipCount = 0;
            for (int i = 0; i < equipSlots.Length; i++)
            {
                equipSlots[i].Setup(skillDataManager.equippedSkills[i], skillDataManager);
                if (skillDataManager.equippedSkills[i] != null) equipCount++;
            }

            // 보관함 슬롯 갱신
            int storageCount = 0;
            for (int i = 0; i < storageSlots.Length; i++)
            {
                if (i < skillDataManager.storageSkills.Length)
                {
                    storageSlots[i].Setup(skillDataManager.storageSkills[i], skillDataManager);
                    if (skillDataManager.storageSkills[i] != null) storageCount++;
                }
            }

            Debug.Log($"[SkillUIController] 스킬 UI 동기화 완료! 현재 화면에 장착: {equipCount}개, 보관함: {storageCount}개 그려짐.");

            if (playerSkillManager != null)
                playerSkillManager.SetEquippedSkills(skillDataManager.equippedSkills);
        }

        private bool IsEquipped(SkillData skillToCheck)
        {
            if (skillToCheck == null) return false;
            foreach (var equippedSkill in skillDataManager.equippedSkills)
            {
                if (equippedSkill == skillToCheck) return true;
            }
            return false;
        }

        /// <summary>
        /// 슬롯을 더블 클릭했을 때 장착 <-> 해제 상태로 서로 전환
        /// </summary>
        /// <param name="clickedSlot"></param>
        private void HandleDoubleClick(SkillSlotUI clickedSlot)
        {
            // 보관함의 스킬을 더블 클릭 => 비어있는 장착 슬롯으로 이동
            if (clickedSlot.slotType == SkillSlotType.Storage && clickedSlot.GetSkill() != null)
            {
                SkillSlotUI target = GetAvailableEquipSlot();
                SwapSkills(clickedSlot, target);
            }
            // 장착된 스킬을 더블 클릭 => 비어있는 보관함 슬롯으로 이동
            else if (clickedSlot.slotType == SkillSlotType.Equipped && clickedSlot.GetSkill() != null)
            {
                SkillSlotUI emptyStorageSlot = GetAvailableStorageSlot();
                if (emptyStorageSlot != null) SwapSkills(clickedSlot, emptyStorageSlot);
            }
        }

        /// <summary>
        /// 특정 슬롯에서 다른 슬롯으로 드래그 앤 드롭했을 때 스킬 위치를 교환합니다
        /// </summary>
        private void HandleDrop(SkillSlotUI fromSlot, SkillSlotUI toSlot)
        {
            SwapSkills(fromSlot, toSlot);
        }

        /// <summary>
        /// 장착된 스킬을 보관함 빈 배경 영역으로 드롭했을 때 해제하는 기능을 처리
        /// </summary>
        private void HandleDropToStorageBackground(SkillSlotUI draggedEquipSlot)
        {
            SkillSlotUI emptyStorageSlot = GetAvailableStorageSlot();

            if (emptyStorageSlot != null)
            {
                SwapSkills(draggedEquipSlot, emptyStorageSlot);
            }
        }

        /// <summary>
        /// 두 UI 슬롯간의 스킬 데이터를 교환하고 시스템에 변경 사항을 반영
        /// </summary>
        private void SwapSkills(SkillSlotUI slotA, SkillSlotUI slotB)
        {
            if (slotA == null || slotB == null) return;

            SkillData skillA = slotA.GetSkill();
            SkillData skillB = slotB.GetSkill();

            // 데이터 맞교환
            slotA.Setup(skillB, skillDataManager);
            slotB.Setup(skillA, skillDataManager);

            // 교환 후 변경된 상태를 매니저에 전달 및 저장
            UpdatePlayerSkills();
            SyncUIFromData();
        }

        /// <summary>
        /// 비어있는 장착 슬롯을 찾아서 반환. 빈 슬롯이 없으면 첫 번째 슬롯을 반환하여 덮어쓰도록 한다.
        /// </summary>
        /// <returns></returns>
        private SkillSlotUI GetAvailableEquipSlot()
        {
            foreach (var slot in equipSlots)
            {
                if (slot.GetSkill() == null) return slot;
            }
            return equipSlots[0];
        }

        /// <summary>
        ///  비어있는 보관함 슬롯을 찾아서 반환. 가득 찼다면 null 반환
        /// </summary>
        private SkillSlotUI GetAvailableStorageSlot()
        {
            foreach (var slot in storageSlots)
            {
                if (slot.GetSkill() == null) return slot;
            }
            return null;
        }

        /// <summary>
        /// 외부에서 스킬이 해금되었을 때 호출 되어 데이터를 추가하고 저장
        /// </summary>
        private void HandleSkillUnlocked(SkillData newSkillData)
        {
            if (newSkillData != null)
            {
                if (skillDataManager != null)
                {
                    // 데이터 매니저에 스킬 추가
                    skillDataManager.AcquireSkill(newSkillData);

                    // 획득 직후 자동 저장
                    if (saveLoadManager != null)
                    {
                        saveLoadManager.SaveGame();
                    }
                }
            }
            else
            {
                Debug.LogError($"[UI] 해금된 SkillData가 null입니다.");
            }
        }

        /// <summary>
        /// UI 상의 슬롯 배치 상태를 긁어모아 SkillDataManager와 PlayerSkillManager에 동기화시키고 저장
        /// </summary>
        private void UpdatePlayerSkills()
        {
            // UI 배열에서 장착 스킬 데이터 수집
            SkillData[] currentEquipped = new SkillData[equipSlots.Length];
            for (int i = 0; i < equipSlots.Length; i++)
            {
                currentEquipped[i] = equipSlots[i].GetSkill();
            }

            // UI 배열에서 보관함 스킬 데이터 수집 (비어있는 중간 슬롯을 압축하여 모은다)
            SkillData[] currentStorage = new SkillData[storageSlots.Length];
            int storageIndex = 0;

            for (int i = 0; i < storageSlots.Length; i++)
            {
                SkillData skill = storageSlots[i].GetSkill();
                if (skill != null)
                {
                    currentStorage[storageIndex] = skill;
                    storageIndex++;
                }
            }

            // 수집한 상태를 데이터 매니저에 적용
            if (skillDataManager != null)
            {
                skillDataManager.UpdateSkills(currentEquipped, currentStorage);
            }

            // 수집한 장착 상태를 실제 캐릭터 컨트롤러 시스템에 전달
            if (playerSkillManager != null)
            {
                playerSkillManager.SetEquippedSkills(currentEquipped);
            }

            // 변경된 최종 상태를 디스크에 세이브
            if (saveLoadManager != null)
            {
                saveLoadManager.SaveGame();
            }
        }
    }
}
