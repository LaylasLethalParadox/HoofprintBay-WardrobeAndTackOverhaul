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
