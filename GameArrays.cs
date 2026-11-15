using System;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;

namespace WardrobeAndTackOverhaul;

// Writes strings into the game's string arrays (outfits, tack) the way the game itself does.
// Il2CppStringArray's indexer stores the new string without telling the garbage collector, so with
// Unity's incremental GC the string can be freed while the array still points at it: the slot then
// holds some other text or nothing, and the game shows the item as taken off.
internal static class GameArrays
{
    // An il2cpp array is the object header (class, monitor), the bounds pointer and the length, then the elements.
    private static readonly int ElementsOffset = 2 * IntPtr.Size + IntPtr.Size + UIntPtr.Size;

    public static void Set(Il2CppStringArray array, int index, string value)
    {
        if (array == null)
            throw new ArgumentNullException(nameof(array));
        if (index < 0 || index >= array.Length)
            throw new ArgumentOutOfRangeException(nameof(index));
        var arrayPtr = array.Pointer;
        var slot = IntPtr.Add(arrayPtr, ElementsOffset + index * IntPtr.Size);
        IL2CPP.il2cpp_gc_wbarrier_set_field(arrayPtr, slot, IL2CPP.ManagedStringToIl2Cpp(value ?? ""));
    }

    // A new game array filled through Set, for assigning with the game's own (GC-safe) field setters.
    public static Il2CppStringArray New(int length, Func<int, string> value)
    {
        var array = new Il2CppStringArray(length);
        for (var i = 0; i < length; i++)
            Set(array, i, value(i));
        return array;
    }
}
