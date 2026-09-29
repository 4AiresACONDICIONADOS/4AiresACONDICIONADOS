using BreathOfEclipse.Core;
using NUnit.Framework;

namespace BreathOfEclipse.Tests
{
    public class QuickSlotMapTests
    {
        [Test]
        public void Assign_PutsFormOnSlot()
        {
            var map = QuickSlotMap.Assign(new[] { 0, 3, 5, 6 }, 1, 2);
            CollectionAssert.AreEqual(new[] { 0, 2, 5, 6 }, map);
        }

        [Test]
        public void Assign_FormAlreadyOnAnotherSlot_Swaps()
        {
            var map = QuickSlotMap.Assign(new[] { 0, 3, 5, 6 }, 0, 6);
            CollectionAssert.AreEqual(new[] { 6, 3, 5, 0 }, map, "VII moves to key 1, I takes its old key 4");
        }

        [Test]
        public void Assign_OutOfRangeSlot_LeavesMapUnchanged()
        {
            var original = new[] { 0, 1, 2, 3 };
            CollectionAssert.AreEqual(original, QuickSlotMap.Assign(original, 7, 4));
            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3 }, original, "the input array is never modified");
        }
    }
}
