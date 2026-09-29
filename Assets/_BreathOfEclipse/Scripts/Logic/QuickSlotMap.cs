namespace BreathOfEclipse.Core
{
    /// <summary>
    /// Quick slots 1-4 of a breathing style: which form (0-based index, -1 = empty) each key uses.
    /// Pure logic so the swap rule is testable outside Unity.
    /// </summary>
    public static class QuickSlotMap
    {
        /// <summary>
        /// Returns a copy of <paramref name="current"/> with <paramref name="formIndex"/> on <paramref name="slot"/>.
        /// A form already on another slot swaps places with the slot's previous form, so no form appears twice.
        /// </summary>
        public static int[] Assign(int[] current, int slot, int formIndex)
        {
            var map = current != null ? (int[])current.Clone() : new int[0];
            if (slot < 0 || slot >= map.Length) return map;
            int previous = System.Array.IndexOf(map, formIndex);
            if (previous >= 0 && previous != slot) map[previous] = map[slot];
            map[slot] = formIndex;
            return map;
        }
    }
}
