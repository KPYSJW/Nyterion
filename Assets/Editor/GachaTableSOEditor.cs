using UnityEngine;
using UnityEditor;
using Nytherion.Data.ScriptableObjects.Gacha;
using System.Linq;

[CustomEditor(typeof(GachaTableSO))]
public class GachaTableSOEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        GachaTableSO gachaTable = (GachaTableSO)target;

        EditorGUILayout.Space(20);
        
        EditorGUILayout.LabelField("계산된 확률 (Calculated Probabilities)", EditorStyles.boldLabel);

        if (gachaTable.gachaPools == null || gachaTable.gachaPools.Count == 0)
        {
            EditorGUILayout.HelpBox("가챠 풀이 설정되지 않았습니다.", MessageType.Info);
            return;
        }

        float totalWeight = gachaTable.gachaPools.Sum(pool => pool.drawWeight);

        if (totalWeight <= 0)
        {
            EditorGUILayout.HelpBox("확률 가중치의 총합이 0보다 커야 합니다.", MessageType.Warning);
            return;
        }
        
        foreach (var pool in gachaTable.gachaPools)
        {
            if (pool != null)
            {
                float probability = (pool.drawWeight / totalWeight) * 100;
                EditorGUILayout.LabelField($"{pool.rarity}", $"{probability:F2}%");
            }
        }
    }
}