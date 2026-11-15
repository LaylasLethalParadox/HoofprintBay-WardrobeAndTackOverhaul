using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;

namespace WardrobeAndTackOverhaul;

[BepInPlugin(Guid, Name, Version)]
[BepInDependency(ModManagerGuid, BepInDependency.DependencyFlags.SoftDependency)]
public class Plugin : BasePlugin
{
    public const string Guid = "hoofprintbay.wardrobeandtackoverhaul";
    public const string Name = "Wardrobe and tack overhaul";
    public const string Version = "1.0.1";
    internal const string ModManagerGuid = "hoofprintbay.modmanager";

    internal static ManualLogSource Logger;
    internal static ConfigEntry<bool> Enabled;

    public override void Load()
    {
        Logger = Log;
        Enabled = Config.Bind("General", "Enabled", true,
            "Rides and lessons use the outfit and tack saved for them on the tabs in the wardrobe and in Edit Tack.");

        var harmony = new Harmony(Guid);
        harmony.PatchAll(typeof(RideSwap));
        harmony.PatchAll(typeof(Cabinets));
        harmony.PatchAll(typeof(Halters));
        harmony.PatchAll(typeof(SaveSync));
        if (IL2CPPChainloader.Instance.Plugins.ContainsKey(ModManagerGuid))
            ModManagerLink.Register();
        Log.LogInfo($"{Name} {Version} loaded.");
    }
}

// The saved sets belong to one save game: forget them when another is loaded or this one deleted.
[HarmonyPatch]
internal static class SaveSync
{
    [HarmonyPatch(typeof(DataManager), nameof(DataManager.LoadFromSaveFromFile))]
    [HarmonyPatch(typeof(DataManager), nameof(DataManager.LoadSaveFromJsonOrBinary))]
    [HarmonyPatch(typeof(DataManager), nameof(DataManager.LoadSaveFromProtectedFile))]
    [HarmonyPatch(typeof(DataManager), nameof(DataManager.DeleteSaveFile))]
    [HarmonyPrefix]
    private static void BeforeLoadOrDelete()
    {
        try { Store.Forget(); }
        catch (Exception e) { Plugin.Logger.LogError($"Forgetting the saved sets failed: {e}"); }
    }
}
