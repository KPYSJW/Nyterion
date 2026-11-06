using System;
using System.Collections.Generic;

namespace Nytherion.GamePlay.Characters.Enemy
{
    public sealed class BossWitchOrbInventory
    {
        private readonly List<int> slots = new List<int>();
        public IReadOnlyList<int> Slots => slots;
        public int Count => slots.Count;
        public int Capacity { get; private set; }

        public void Refill(int capacity)
        {
            Capacity = Math.Max(1, capacity);
            slots.RemoveAll(slot => slot >= Capacity);
            for (int slot = 0; slot < Capacity; slot++)
                if (!slots.Contains(slot)) slots.Add(slot);
        }

        public int ConsumeAt(int index)
        {
            if (index < 0 || index >= slots.Count) return -1;
            int slot = slots[index];
            slots.RemoveAt(index);
            return slot;
        }

        public void Clear()
        {
            slots.Clear();
            Capacity = 0;
        }
    }
}
