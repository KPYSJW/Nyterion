using UnityEditor;
using UnityEngine;
using Nytherion.Core.Data;
using Nytherion.Core.Enums;

namespace Nytherion.Editor
{
    [CustomPropertyDrawer(typeof(RewardData))]
    public class RewardDataDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            property.isExpanded = EditorGUI.Foldout(new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight), property.isExpanded, label, true);
            
            if (property.isExpanded)
            {
                EditorGUI.indentLevel++;
                float yOffset = position.y + EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

                SerializedProperty typeProp = property.FindPropertyRelative("rewardType");
                EditorGUI.PropertyField(new Rect(position.x, yOffset, position.width, EditorGUIUtility.singleLineHeight), typeProp);
                yOffset += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

                RewardType currentType = (RewardType)typeProp.enumValueIndex;

                SerializedProperty amountProp = property.FindPropertyRelative("amount");
                EditorGUI.PropertyField(new Rect(position.x, yOffset, position.width, EditorGUIUtility.singleLineHeight), amountProp);
                yOffset += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

                if (currentType == RewardType.Skill)
                {
                    SerializedProperty skillProp = property.FindPropertyRelative("skillData");
                    EditorGUI.PropertyField(new Rect(position.x, yOffset, position.width, EditorGUIUtility.singleLineHeight), skillProp);
                }
                else if (currentType == RewardType.Item)
                {
                    SerializedProperty itemProp = property.FindPropertyRelative("itemData");
                    EditorGUI.PropertyField(new Rect(position.x, yOffset, position.width, EditorGUIUtility.singleLineHeight), itemProp);
                }
                else if (currentType == RewardType.Relic)
                {
                    SerializedProperty relicProp = property.FindPropertyRelative("relicData");
                    EditorGUI.PropertyField(new Rect(position.x, yOffset, position.width, EditorGUIUtility.singleLineHeight), relicProp);
                }

                EditorGUI.indentLevel--;
            }

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (!property.isExpanded) return EditorGUIUtility.singleLineHeight;

            float height = EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing; 
            height += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing; 
            height += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing; 

            SerializedProperty typeProp = property.FindPropertyRelative("rewardType");
            RewardType currentType = (RewardType)typeProp.enumValueIndex;

            if (currentType == RewardType.Skill || currentType == RewardType.Item || currentType == RewardType.Relic)
            {
                height += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
            }

            return height;
        }
    }
}
