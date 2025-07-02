using UnityEngine;

namespace Nytherion.Core
{
    public static class Layers
    {
        public static readonly int Interactable = LayerMask.NameToLayer("Interactable");
        public static readonly int Player = LayerMask.NameToLayer("Player");
    }
}