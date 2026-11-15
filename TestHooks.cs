#if TESTHOOKS
using System;
using System.Linq;

namespace WardrobeAndTackOverhaul;

// Only in builds made with -p:TestHooks=true, for the test bridge's "call".
public static class TestHooks
{
    public static void ListItems()
    {
        foreach (var c in InventoryLibrary.i.GetAllClothes())
            Plugin.Logger.LogInfo($"clothes {c.type} {c.id}");
        foreach (var t in InventoryLibrary.i.GetAllTacks())
            Plugin.Logger.LogInfo($"tack {t.type} {t.id}");
    }

    // Every tinted picture of the horse on screen, to see which ones carry the halter colour.
    public static void ListTints()
    {
        foreach (var horse in UnityEngine.Object.FindObjectsByType<StableHorse>(UnityEngine.FindObjectsSortMode.None))
        {
            Plugin.Logger.LogInfo($"halter {horse.displayHorse?.halter?.color}");
            foreach (var r in horse.GetComponentsInChildren<UnityEngine.SpriteRenderer>(true))
                if (r.color != UnityEngine.Color.white)
                    Plugin.Logger.LogInfo($"tint {r.name} {r.color} sprite={r.sprite?.name} active={r.gameObject.activeInHierarchy}");
            foreach (var r in horse.GetComponentsInChildren<UnityEngine.Renderer>(true))
                if (r.name.ToLowerInvariant().Contains("lead"))
                {
                    Plugin.Logger.LogInfo($"lead {r.name} {r.GetIl2CppType().Name} mat={r.sharedMaterial?.name}");
                    var m = r.sharedMaterial;
                    if (m != null)
                        foreach (var name in new[] { "_Color", "_TintColor", "_BaseColor", "_MainColor", "_HalterColor", "_LeadropeColor", "_Tint", "_ColorTint", "_RendererColor", "_Color1", "_ColorA" })
                            if (m.HasProperty(name))
                                Plugin.Logger.LogInfo($"  prop {name} {m.GetColor(name)}");
                    Plugin.Logger.LogInfo($"  shader={m?.shader?.name} main={m?.mainTexture?.name}");
                }
        }
    }

    // Saves the halter picture (its rect in the texture) to a png.
    public static void DumpHalter(string path)
    {
        var sprite = Halters.Icon();
        if (sprite == null) { Plugin.Logger.LogInfo("no halter sprite"); return; }
        var tex = Halters.ReadSprite(sprite);
        System.IO.File.WriteAllBytes(path, UnityEngine.ImageConversion.EncodeToPNG(tex));
        Plugin.Logger.LogInfo($"halter {sprite.name} tex={sprite.texture.name} {sprite.texture.width}x{sprite.texture.height} rect={sprite.rect} packed={sprite.packed}");
    }

    // The game's own outfits as they are in memory right now.
    public static void DumpOutfits(string label)
    {
        var outfits = DataManager.i?.data?.player?.outfits;
        if (outfits == null) { Plugin.Logger.LogInfo($"[outfits {label}] none"); return; }
        for (var i = 0; i < outfits.Count; i++)
        {
            var o = outfits[i]?.outfit;
            Plugin.Logger.LogInfo($"[outfits {label}] {i}: " + (o == null ? "null" : string.Join(", ", o)));
        }
    }

    // A full collection of the game's (il2cpp) heap, so freed strings show up at once.
    public static void ForceGc()
    {
        Il2CppInterop.Runtime.IL2CPP.il2cpp_gc_collect(2);
        Il2CppInterop.Runtime.IL2CPP.il2cpp_gc_collect(2);
        Plugin.Logger.LogInfo("[dbg] GC collected");
    }

    public static void DumpTack(string horseName, string label)
    {
        foreach (var h in DataManager.i.data.horses)
            if (h.name == horseName)
                Plugin.Logger.LogInfo($"[tack {label}] {h.name}: " + (h.tack == null ? "null" : string.Join(", ", h.tack)));
    }

    // Test copies only: dress the rider's own outfit (0 normal, 1 competition) again after damage.
    public static void SetGameOutfit(string type, string ids)
    {
        var outfit = DataManager.i.data.player.outfits[int.Parse(type)].outfit;
        var values = ids.Split(',');
        for (var i = 0; i < outfit.Length && i < values.Length; i++)
            GameArrays.Set(outfit, i, values[i]);
    }

    public static void SetEnabled(string on) => Plugin.Enabled.Value = bool.Parse(on);

    public static void DumpStore() =>
        Plugin.Logger.LogInfo("[store] " + System.IO.File.ReadAllText(System.IO.Path.Combine(DataManager.saveGamePath, DataManager.currentSaveDirectory, "WardrobeAndTackOverhaul.json")).Replace('\n', ' ').Replace("  ", ""));

    // Ends the running ride at the finish line, as if it was ridden to the end, so the result and prestige screens show.
    public static void FinishRide()
    {
        var ride = UnityEngine.Object.FindObjectsByType<Sidescroller>(UnityEngine.FindObjectsSortMode.None).FirstOrDefault();
        if (ride == null) { Plugin.Logger.LogInfo("[dbg] no ride"); return; }
        ride.FinishCourse(true, true, "");
        Plugin.Logger.LogInfo("[dbg] ride finished");
    }

    // The prestige lines of the result screen.
    public static void DumpPrestige()
    {
        var ride = UnityEngine.Object.FindObjectsByType<Sidescroller>(UnityEngine.FindObjectsSortMode.None).FirstOrDefault();
        if (ride == null) return;
        Plugin.Logger.LogInfo($"[prestige] clothes='{ride.textPrestigeRiderClothes?.text}' dirt='{ride.textPrestigeHorseDirt?.text}' placement='{ride.textPrestigeCompPlacement?.text}'");
    }

    // ids: comma separated, one per slot.
    public static void SetOutfit(string activity, string ids) =>
        Store.SetOutfit(new[] { Enum.Parse<Activity>(activity) }, ids.Split(','));

    public static void SetTack(string horseName, string activity, string ids)
    {
        foreach (var h in DataManager.i.data.horses)
            if (h.name == horseName)
                Store.SetHorseTack(h.uuid, new[] { Enum.Parse<Activity>(activity) }, ids.Split(','));
    }
}
#endif
