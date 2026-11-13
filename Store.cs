using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace WardrobeAndTackOverhaul;

// The saved sets of the loaded save game, in "WardrobeAndTackOverhaul.json" next to save.json.
// Written straight away on Save: these are choices, not game progress, so they don't wait for the game to save.
internal static class Store
{
    private const string FileName = "WardrobeAndTackOverhaul.json"; // not "save*.json": the game treats those as backups
    // The mod was called Activity Outfits before its first release; its file is read once and saved under the new name.
    private const string OldFileName = "ActivityOutfits.json";

    public sealed class Data
    {
        // Activity name -> clothes ids per ClothesType (face and hair are left empty and not used).
        public Dictionary<string, string[]> Outfits { get; set; } = new();
        // Horse uuid -> activity name -> tack ids per TackType.
        public Dictionary<string, Dictionary<string, string[]>> Tack { get; set; } = new();
        // Halter colours bought in the store (blue is always owned), and the one each horse wears (by uuid).
        public List<string> HaltersOwned { get; set; } = new();
        public Dictionary<string, string> HalterByHorse { get; set; } = new();
    }

    private static string loadedFor;
    private static Data data;

    private static string Save => DataManager.currentSaveDirectory;
    private static bool InSave => !string.IsNullOrEmpty(Save) && Save != "NoSave";

    private static string PathFor(string save)
    {
        var folder = Path.Combine(DataManager.saveGamePath ?? "", save);
        if (Directory.Exists(folder))
            return Path.Combine(folder, FileName);
        var fallback = Path.Combine(BepInEx.Paths.ConfigPath, "WardrobeAndTackOverhaul", save);
        Directory.CreateDirectory(fallback);
        return Path.Combine(fallback, FileName);
    }

    private static Data Current()
    {
        if (!InSave)
            return null;
        if (loadedFor == Save && data != null)
            return data;
        loadedFor = Save;
        data = new Data();
        var path = PathFor(Save);
        try
        {
            var old = Path.Combine(Path.GetDirectoryName(path) ?? "", OldFileName);
            if (File.Exists(path))
                data = JsonSerializer.Deserialize<Data>(File.ReadAllText(path)) ?? new Data();
            else if (File.Exists(old))
            {
                data = JsonSerializer.Deserialize<Data>(File.ReadAllText(old)) ?? new Data();
                Write();
                Plugin.Logger.LogInfo($"Took over the saved sets from {OldFileName}.");
            }
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"Reading {path} failed, starting with no saved sets: {e.Message}");
        }
        return data;
    }

    private static void Write()
    {
        if (!InSave || data == null)
            return;
        var path = PathFor(Save);
        try
        {
            File.WriteAllText(path, JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"Writing {path} failed: {e.Message}");
        }
    }

    // Dropped when a save is loaded or deleted, so another save never sees these.
    public static void Forget()
    {
        loadedFor = null;
        data = null;
    }

    public static string[] Outfit(Activity a)
    {
        var d = Current();
        return d != null && d.Outfits.TryGetValue(a.ToString(), out var ids) ? ids : null;
    }

    public static string[] HorseTack(string horse, Activity a)
    {
        var d = Current();
        if (d == null || string.IsNullOrEmpty(horse) || !d.Tack.TryGetValue(horse, out var sets))
            return null;
        return sets.TryGetValue(a.ToString(), out var ids) ? ids : null;
    }

    public static bool OwnsHalter(string color)
    {
        var d = Current();
        return color == Halters.Blue || (d != null && d.HaltersOwned.Contains(color));
    }

    public static void AddHalter(string color)
    {
        var d = Current();
        if (d == null || d.HaltersOwned.Contains(color))
            return;
        d.HaltersOwned.Add(color);
        Write();
    }

    // Null when the horse wears the game's own halter colour.
    public static string HalterOf(string horse)
    {
        var d = Current();
        return d != null && !string.IsNullOrEmpty(horse) && d.HalterByHorse.TryGetValue(horse, out var c) ? c : null;
    }

    public static void SetHalter(string horse, string color)
    {
        var d = Current();
        if (d == null || string.IsNullOrEmpty(horse))
            return;
        d.HalterByHorse[horse] = color;
        Write();
    }

    public static void SetOutfit(IEnumerable<Activity> activities, string[] ids)
    {
        var d = Current();
        if (d == null)
            return;
        foreach (var a in activities)
            d.Outfits[a.ToString()] = (string[])ids.Clone();
        Write();
    }

    public static void SetHorseTack(string horse, IEnumerable<Activity> activities, string[] ids)
    {
        var d = Current();
        if (d == null || string.IsNullOrEmpty(horse))
            return;
        if (!d.Tack.TryGetValue(horse, out var sets))
            d.Tack[horse] = sets = new Dictionary<string, string[]>();
        foreach (var a in activities)
            sets[a.ToString()] = (string[])ids.Clone();
        Write();
    }
}
