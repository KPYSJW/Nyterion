using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Nytherion.Data.ScriptableObjects.Items;

/// <summary>
/// 툴팁 패널을 관리하는 클래스입니다.
/// 아이템에 대한 추가 정보를 표시하는 UI 요소를 제어합니다.
/// </summary>

namespace Nytherion.UI.Inventory
{
    public class TooltipPanel : MonoBehaviour
    {
        public static TooltipPanel Instance;

        /// <summary>
        /// 툴팁 패널 게임 오브젝트입니다.
        /// </summary>
        [SerializeField] private GameObject panel;

        /// <summary>
        /// 툴팁의 캔버스 그룹입니다. 마우스 이벤트를 방지하는 데 사용됩니다.
        /// </summary>
        [SerializeField] private CanvasGroup canvasGroup;

        /// <summary>
        /// 아이템 이름을 표시하는 텍스트 컴포넌트입니다.
        /// </summary>
        [SerializeField] private TextMeshProUGUI nameText;

        /// <summary>
        /// 아이템 설명을 표시하는 텍스트 컴포넌트입니다.
        /// </summary>
        [SerializeField] private TextMeshProUGUI descriptionText;

        /// <summary>
        /// 컴포넌트 초기화 시 호출됩니다.
        /// 싱글톤 인스턴스를 설정하고 툴팁을 숨깁니다.
        /// </summary>
        private void Awake()
        {
            // 싱글톤 패턴 적용
            if (Instance == null) 
                Instance = this;
            else 
                Destroy(gameObject);

            // 툴팁이 마우스 이벤트를 가로채지 않도록 설정
            canvasGroup.blocksRaycasts = false;
            
            // 시작 시 툴팁 숨기기
            HideTooltip();
        }

        /// <summary>
        /// 매 프레임 마지막에 호출됩니다.
        /// 툴팁이 활성화된 경우 마우스 위치를 따라가도록 합니다.
        /// </summary>
        private void LateUpdate()
        {
            if (panel.activeSelf)
            {
                // 마우스 위치로 툴팁 위치 업데이트
                transform.position = Input.mousePosition;
            }
        }

        /// <summary>
        /// 툴팁을 표시합니다.
        /// </summary>
        /// <param name="item">표시할 아이템 데이터</param>
        public void ShowTooltip(ItemData item)
        {
            if (item == null) 
                return;
                
            // 아이템 정보로 툴팁 내용 설정
            SetContent(item.itemName, item.description);
            
            // 툴팁 활성화
            panel.SetActive(true);
        }

        /// <summary>
        /// 툴팁을 숨깁니다.
        /// </summary>
        public void HideTooltip()
        {
            panel.SetActive(false);
        }

        /// <summary>
        /// 툴팁의 내용을 설정합니다.
        /// </summary>
        /// <param name="name">표시할 이름</param>
        /// <param name="desc">표시할 설명</param>
        private void SetContent(string name, string desc)
        {
            nameText.text = name;
            descriptionText.text = desc;
        }
    }
}
