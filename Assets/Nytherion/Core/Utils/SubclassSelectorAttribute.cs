using UnityEngine;
using System;

namespace Nytherion.Core.Utils
{
    /// <summary>
    /// [SerializeReference] 필드에 다형성 객체를 선택할 수 있는 드롭다운 메뉴를 인스펙터에 표시
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    public class SubclassSelectorAttribute : PropertyAttribute { }
}