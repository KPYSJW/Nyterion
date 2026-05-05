using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Nytherion.Core.Utils;

namespace Nytherion.Editor
{
    [CustomPropertyDrawer(typeof(SubclassSelectorAttribute))]
    public class SubclassSelectorDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            Rect foldoutRect = new Rect(position.x, position.y, EditorGUIUtility.labelWidth, EditorGUIUtility.singleLineHeight);
            Rect dropdownRect = new Rect(position.x + EditorGUIUtility.labelWidth, position.y, position.width - EditorGUIUtility.labelWidth, EditorGUIUtility.singleLineHeight);

            // Foldout 토글
            property.isExpanded = EditorGUI.Foldout(foldoutRect, property.isExpanded, label, true);
            
            // 다형성 기반 타입 추출
            Type baseType = GetBaseType(property);
            if (baseType != null)
            {
                string currentTypeName = property.managedReferenceValue != null ? property.managedReferenceValue.GetType().Name : "None";
                GUIContent buttonLabel = new GUIContent(currentTypeName);

                // 버튼 클릭 시 메뉴 팝업
                if (GUI.Button(dropdownRect, buttonLabel, EditorStyles.popup))
                {
                    GenericMenu menu = new GenericMenu();
                    
                    // None 추가
                    menu.AddItem(new GUIContent("None"), property.managedReferenceValue == null, () =>
                    {
                        property.managedReferenceValue = null;
                        property.serializedObject.ApplyModifiedProperties();
                    });

                    // 파생 클래스들 검색
                    var types = AppDomain.CurrentDomain.GetAssemblies()
                        .SelectMany(s => s.GetTypes())
                        .Where(p => baseType.IsAssignableFrom(p) && !p.IsAbstract && !p.IsInterface);

                    foreach (var type in types)
                    {
                        menu.AddItem(new GUIContent(type.Name), property.managedReferenceValue?.GetType() == type, () =>
                        {
                            property.managedReferenceValue = Activator.CreateInstance(type);
                            property.isExpanded = true; // 생성 시 자동 확장
                            property.serializedObject.ApplyModifiedProperties();
                        });
                    }
                    menu.ShowAsContext();
                }
            }

            // 아래에 프로퍼티 내부 필드들 그리기
            if (property.isExpanded && property.managedReferenceValue != null)
            {
                EditorGUI.indentLevel++;
                Rect childRect = new Rect(position.x, position.y + EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing, position.width, EditorGUIUtility.singleLineHeight);
                
                SerializedProperty iterator = property.Copy();
                bool enterChildren = true;
                while (iterator.NextVisible(enterChildren))
                {
                    if (SerializedProperty.EqualContents(iterator, property.GetEndProperty())) break;

                    float childHeight = EditorGUI.GetPropertyHeight(iterator, true);
                    childRect.height = childHeight;
                    EditorGUI.PropertyField(childRect, iterator, true);
                    childRect.y += childHeight + EditorGUIUtility.standardVerticalSpacing;
                    
                    enterChildren = false; 
                }
                EditorGUI.indentLevel--;
            }

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float height = EditorGUIUtility.singleLineHeight;
            if (property.isExpanded && property.managedReferenceValue != null)
            {
                SerializedProperty iterator = property.Copy();
                bool enterChildren = true;
                while (iterator.NextVisible(enterChildren))
                {
                    if (SerializedProperty.EqualContents(iterator, property.GetEndProperty())) break;
                    height += EditorGUI.GetPropertyHeight(iterator, true) + EditorGUIUtility.standardVerticalSpacing;
                    enterChildren = false;
                }
            }
            return height;
        }

        private Type GetBaseType(SerializedProperty property)
        {
            string[] typeSplit = property.managedReferenceFieldTypename.Split(' ');
            if (typeSplit.Length == 2)
            {
                var assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == typeSplit[0]);
                if (assembly != null)
                {
                    return assembly.GetType(typeSplit[1]);
                }
            }
            return null;
        }
    }
}