using UnityEngine;
using Nytherion.Data.ScriptableObjects.Engravings; 
using Nytherion.Core;

public class EngravingTestHelper : MonoBehaviour
{
    [Header("테스트할 각인 데이터")]
    [Tooltip("버튼을 눌렀을 때 추가할 각인 에셋을 여기에 연결하세요.")]
    public EngravingData testEngravingToAdd;

    public void AddTestEngraving()
    {
        if (testEngravingToAdd == null)
        {
            Debug.LogError("EngravingTestHelper: 테스트할 각인 데이터(EngravingData)가 할당되지 않았습니다!");
            return;
        }

        EngravingManager.Instance.AddNewEngravingToStorage(testEngravingToAdd);
    }
}