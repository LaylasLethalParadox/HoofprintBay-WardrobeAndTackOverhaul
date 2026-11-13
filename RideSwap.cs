using System;
using System.Collections.Generic;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;

namespace WardrobeAndTackOverhaul;

// When a ride or lesson dresses its horse and rider, the activity's saved set is put into the
// game's data just for that call and taken out again right after, so the save never holds it.
[HarmonyPatch]
internal static class RideSwap
{
    // Clothes types the outfit sets cover (HAT, JACKET, PANTS, SHOES); face and hair stay as they are.
    private static readonly int[] OutfitSlots = { (int)ClothesType.HAT, (int)ClothesType.JACKET, (int)ClothesType.PANTS, (int)ClothesType.SHOES };

    private sealed class Undo
    {
        public HorseData Horse;
        public Il2CppStringArray HorseTack;
        public readonly List<(Il2CppStringArray outfit, int slot, string id)> Clothes = new();
    }

    // Set by the prefix, undone by the finalizer, which runs even when the game's method throws.
    private static Undo pending;

    [HarmonyPatch(typeof(SidescrollerHorse), nameof(SidescrollerHorse.SetData))]
    [HarmonyPrefix]
    private static void Before(HorseData __0, PlayerData __1, SidescrollerMode __2)
    {
        pending = null;
        try
        {
            if (!Plugin.Enabled.Value)
                return;
            var activity = Activities.Current();
            if (activity == null)
                return;
            var undo = new Undo();

            var tack = __0 != null ? Store.HorseTack(__0.uuid, activity.Value) : null;
            if (tack != null && __0.tack != null)
            {
                undo.Horse = __0;
                undo.HorseTack = __0.tack;
                var swapped = new Il2CppStringArray(__0.tack.Length);
                for (var i = 0; i < swapped.Length; i++)
                    swapped[i] = i < tack.Length ? tack[i] ?? "" : "";
                __0.tack = swapped;
            }

            // Only the player's own clothes; a lesson student keeps theirs.
            var outfit = Store.Outfit(activity.Value);
            var own = DataManager.i?.data?.player;
            if (outfit != null && __1 != null && own != null && __1.Pointer == own.Pointer && __1.outfits != null)
            {
                foreach (var data in __1.outfits)
                {
                    var ids = data?.outfit;
                    if (ids == null)
                        continue;
                    foreach (var slot in OutfitSlots)
                    {
                        if (slot >= ids.Length || slot >= outfit.Length)
                            continue;
                        undo.Clothes.Add((ids, slot, ids[slot]));
                        ids[slot] = outfit[slot] ?? "";
                    }
                }
            }
            if (undo.Horse != null || undo.Clothes.Count > 0)
                Plugin.Logger.LogInfo($"{activity}: {(undo.Horse != null ? "tack" : "")}{(undo.Horse != null && undo.Clothes.Count > 0 ? " and " : "")}{(undo.Clothes.Count > 0 ? "outfit" : "")} of that activity used for {__0?.name}.");
            pending = undo;
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"Putting on the activity's set failed: {e}");
        }
    }

    [HarmonyPatch(typeof(SidescrollerHorse), nameof(SidescrollerHorse.SetData))]
    [HarmonyFinalizer]
    private static void After()
    {
        var undo = pending;
        pending = null;
        if (undo == null)
            return;
        try
        {
            if (undo.Horse != null)
                undo.Horse.tack = undo.HorseTack;
            foreach (var (outfit, slot, id) in undo.Clothes)
                outfit[slot] = id;
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"Restoring the normal set failed: {e}");
        }
    }
}
