using Nytherion.Core.Enums;
using UnityEditor;
using UnityEngine;

namespace Nytherion.Editor
{
    [CustomPropertyDrawer(typeof(StatType))]
    public sealed class StatTypeDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            EditorGUI.BeginChangeCheck();
            var value = (StatType)EditorGUI.EnumPopup(position, label, (StatType)property.intValue,
                option => !((StatType)option).IsRetired(), false);
            // 기존 데이터의 폐기된 값은 표시하되 새로 선택할 수는 없게 합니다.
            if (EditorGUI.EndChangeCheck()) property.intValue = (int)value;
            EditorGUI.EndProperty();
        }
    }
}
