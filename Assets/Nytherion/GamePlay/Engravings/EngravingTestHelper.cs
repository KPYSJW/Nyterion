using UnityEngine;
using Nytherion.Data.ScriptableObjects.Engravings;
using Nytherion.Core.Managers;
using Zenject;

public class EngravingTestHelper : MonoBehaviour
{
    [Header("테스트할 각인 데이터")]
    [Tooltip("버튼을 눌렀을 때 추가할 각인 에셋을 여기에 연결하세요.")]
    public EngravingData testEngravingToAdd;
    private EngravingManager engravingManager;

    [Inject]
    public void Construct(EngravingManager engravingManager)
    {
        this.engravingManager = engravingManager;
    }

    public void AddTestEngraving()
    {
        if (testEngravingToAdd == null)
        {
            Debug.LogError("EngravingTestHelper: 테스트할 각인 데이터(EngravingData)가 할당되지 않았습니다!");
            return;
        }

        engravingManager.AddNewEngravingToStorage(testEngravingToAdd);
    }   
}