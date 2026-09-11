using UnityEngine;
using Nytherion.Core.Managers;
using Nytherion.UI.RelicBoard;
using VContainer;
using System.Collections;
using System.Linq;

namespace Nytherion.GamePlay.Relics
{
    /// <summary>
    /// 각인 시스템의 디버그와 검증을 위한 유틸리티 클래스
    /// </summary>
    public class RelicSystemDebugger : MonoBehaviour
    {
        [Header("디버그 설정")]
        [SerializeField] private bool enableDebugMode = true;
        [SerializeField] private float systemCheckInterval = 5f;

        private RelicManager relicManager;
        private RelicGridUI relicGridUI;
        private RelicTooltip relicTooltip;

        [Inject]
        public void Construct(RelicManager relicManager)
        {
            this.relicManager = relicManager;
            Debug.Log($"[RelicSystemDebugger] VContainer 의존성 주입 완료 - RelicManager: {relicManager != null}");
        }

        private void Start()
        {
            if (enableDebugMode)
            {
                StartCoroutine(PeriodicSystemCheck());
                TestSystemConnections();
            }
        }

        private void TestSystemConnections()
        {
            Debug.Log("=== 각인 시스템 연결 테스트 시작 ===");

            // RelicManager 테스트
            if (relicManager != null)
            {
                Debug.Log($"✅ RelicManager 연결 성공");
                Debug.Log($"   - 그리드 크기: {relicManager.GridRows}x{relicManager.GridColumns}");
                Debug.Log($"   - 보관소 블록 수: {relicManager.GetStorageBlocks()?.Count() ?? 0}");
                Debug.Log($"   - 배치된 블록 수: {relicManager.GetPlacedBlocks()?.Count() ?? 0}");
            }
            else
            {
                Debug.LogError("❌ RelicManager 연결 실패");
            }

            // RelicGridUI 찾기 및 테스트
            relicGridUI = FindObjectOfType<RelicGridUI>();
            if (relicGridUI != null)
            {
                Debug.Log($"✅ RelicGridUI 발견");
                Debug.Log($"   - GameObject: {relicGridUI.gameObject.name}");
                Debug.Log($"   - Active: {relicGridUI.gameObject.activeInHierarchy}");
            }
            else
            {
                Debug.LogError("❌ RelicGridUI를 찾을 수 없습니다");
            }

            // RelicTooltip 찾기 및 테스트
            relicTooltip = FindObjectOfType<RelicTooltip>();
            if (relicTooltip != null)
            {
                Debug.Log($"✅ RelicTooltip 발견");
                Debug.Log($"   - GameObject: {relicTooltip.gameObject.name}");
                Debug.Log($"   - Active: {relicTooltip.gameObject.activeInHierarchy}");
                Debug.Log($"   - Instance: {RelicTooltip.Instance != null}");
            }
            else
            {
                Debug.LogError("❌ RelicTooltip을 찾을 수 없습니다");
            }

            Debug.Log("=== 각인 시스템 연결 테스트 완료 ===");
        }

        private IEnumerator PeriodicSystemCheck()
        {
            while (enableDebugMode)
            {
                yield return new WaitForSeconds(systemCheckInterval);

                if (relicManager != null)
                {
                    var storageCount = relicManager.GetStorageBlocks()?.Count() ?? 0;
                    var placedCount = relicManager.GetPlacedBlocks()?.Count() ?? 0;

                    Debug.Log($"[RelicSystemDebugger] 주기적 점검 - 보관소: {storageCount}개, 배치됨: {placedCount}개");
                }
            }
        }

        [ContextMenu("강제 시스템 재연결 테스트")]
        public void ForceSystemReconnectionTest()
        {
            TestSystemConnections();
        }

        [ContextMenu("각인 UI 강제 새로고침")]
        public void ForceRefreshRelicUI()
        {
            if (relicGridUI != null)
            {
                Debug.Log("[RelicSystemDebugger] RelicGridUI 강제 새로고침 중...");
                StartCoroutine(relicGridUI.Initialize());
            }
            else
            {
                Debug.LogError("[RelicSystemDebugger] RelicGridUI를 찾을 수 없습니다");
            }
        }

        [ContextMenu("테스트 블록 추가")]
        public void AddTestBlock()
        {
            if (relicManager != null)
            {
                Debug.Log("[RelicSystemDebugger] 테스트 블록 추가 시도...");
                // 이 기능은 RelicManager에 public 메서드가 필요합니다
            }
        }

        private void OnDestroy()
        {
            enableDebugMode = false;
        }

        #region 디버그 GUI
        private void OnGUI()
        {
            if (!enableDebugMode) return;

            GUILayout.BeginArea(new Rect(10, 10, 300, 200));
            GUILayout.Label("각인 시스템 디버그", GUI.skin.box);

            if (relicManager != null)
            {
                GUILayout.Label($"보관소 블록: {relicManager.GetStorageBlocks()?.Count() ?? 0}");
                GUILayout.Label($"배치된 블록: {relicManager.GetPlacedBlocks()?.Count() ?? 0}");
            }
            else
            {
                GUILayout.Label("RelicManager: 연결 안됨", GUI.skin.GetStyle("Label"));
            }

            if (GUILayout.Button("시스템 재연결 테스트"))
            {
                ForceSystemReconnectionTest();
            }

            if (GUILayout.Button("UI 강제 새로고침"))
            {
                ForceRefreshRelicUI();
            }

            GUILayout.EndArea();
        }
        #endregion
    }
}