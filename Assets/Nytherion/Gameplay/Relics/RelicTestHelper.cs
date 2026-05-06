using UnityEngine;
using Nytherion.Data.ScriptableObjects.Relics;
using Nytherion.Core.Managers;
using VContainer;

public class RelicTestHelper : MonoBehaviour
{
    [Header("테스트할 각인 데이터")]
    [Tooltip("버튼을 눌렀을 때 추가할 각인 에셋을 여기에 연결하세요.")]
    public RelicData testRelicToAdd;
    private RelicManager relicManager;

    [Inject]
    public void Construct(RelicManager relicManager)
    {
        this.relicManager = relicManager;
    }

    public void AddTestRelic()
    {
        if (testRelicToAdd == null)
        {
            Debug.LogError("RelicTestHelper: 테스트할 각인 데이터(RelicData)가 할당되지 않았습니다!");
            return;
        }

        relicManager.AddNewRelicToStorage(testRelicToAdd);
    }   
}