using System;

namespace Nytherion.Core.Utils
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class RelicDisplayNameAttribute : Attribute
    {
        public string DisplayName { get; }
        public RelicDisplayNameAttribute(string displayName)
        {
            DisplayName = displayName;
        }
    }
}