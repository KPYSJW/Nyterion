using Nytherion.Data.ScriptableObjects.Stage;
using Nytherion.GamePlay.Dungeon;
using UnityEngine;
using Zenject;

namespace Nytherion.Core.Managers
{
    public class StageManager : MonoBehaviour
    {
        [Header("스테이지 설정")]
        [Tooltip("게임 시작 시 로드할 가장 첫 번째 스테이지 데이터입니다.")]
        [SerializeField] private StageData startingStage;


        public StageData CurrentStage { get; private set; }

        private SceneTransitionManager sceneTransitionManager;
        private DungeonManager dungeonManager;

        [Inject]
        public void Construct(SceneTransitionManager sceneTransitionManager, DungeonManager dungeonManager)
        {
            sceneTransitionManager = sceneTransitionManager;
            dungeonManager = dungeonManager;
        }

        private void Awake()
        {
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);
            CurrentStage = startingStage;
        }

        /// <summary>
        /// 보스가 죽었을 때 호출되어 승리 포탈을 생성합니다.
        /// </summary>
        public void SpawnBossPortal(Vector3 position)
        {
            if (CurrentStage.dungeonData.victoryPortalPrefab != null)
            {
                GameObject portalObject = Instantiate(CurrentStage.dungeonData.victoryPortalPrefab, position, Quaternion.identity);
                BossPortal portal = portalObject.GetComponent<BossPortal>();
                if (portal != null)
                {
                    portal.Initialize(this);
                }
            }
        }

        /// <summary>
        /// 현재 스테이지를 클리어하고 다음 스테이지로 넘어갑니다.
        /// </summary>
        public void AdvanceToNextStage()
        {
            if (CurrentStage == null || CurrentStage.nextStageData == null)
            {
                Debug.Log("마지막 스테이지 클리어!");
                return;
            }

            string sceneToLoad = CurrentStage.victorySceneName;
            CurrentStage = CurrentStage.nextStageData;

            Debug.Log($"다음 스테이지 '{CurrentStage.stageName}'(으)로 진행합니다.");

            if (sceneToLoad == "Village")
            {
                Debug.Log($"마을로 이동");
                sceneTransitionManager.LoadScene(sceneToLoad);
            }
            else
            {
                dungeonManager?.RegenerateDungeon();
            }
        }
    }
}