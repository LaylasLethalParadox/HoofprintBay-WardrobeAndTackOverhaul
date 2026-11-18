using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace WardrobeAndTackOverhaul;

// Halters with lead rope in different colours: bought in the equipment store, chosen per horse in the
// tack cabinet. The game draws one tinted halter (and lead rope) per horse, normally in its stable
// group's colour; a chosen halter replaces that tint.
[HarmonyPatch]
internal static class Halters
{
    public const string Blue = "blue";
    public const int Price = 40;

    public sealed record Kind(string Id, string Label, Color Color);

    public static readonly Kind[] All =
    {
        new(Blue, "Blue", new Color(0.24f, 0.45f, 0.86f)),
        new("red", "Red", new Color(0.80f, 0.18f, 0.18f)),
        new("yellow", "Yellow", new Color(0.95f, 0.78f, 0.20f)),
        new("green", "Green", new Color(0.25f, 0.62f, 0.28f)),
        new("purple", "Purple", new Color(0.56f, 0.32f, 0.74f)),
    };

    // Custom colours bought through the store's colour picker are kept as "#RRGGBB", like the game's own
    // custom coloured items carry their colour in the id.
    public static bool IsCustom(string id) => id != null && id.Length == 7 && id[0] == '#';

    public static Kind Find(string id)
    {
        var fixedKind = All.FirstOrDefault(k => k.Id == id);
        if (fixedKind != null || !IsCustom(id) || !ColorUtility.TryParseHtmlString(id, out var color))
            return fixedKind;
        return new Kind(id, "Custom " + id, color);
    }

    public static string Hex(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);

    // The colours the Halter page offers: the fixed ones, then every custom one bought so far.
    public static IEnumerable<Kind> Offered() =>
        All.Concat(Store.CustomHalters().Select(Find).Where(k => k != null));

    // ---- on the horse ----

    // The game colours halter, lead rope and knot from the horse's stable group colour. For a horse with a
    // chosen halter that colour is swapped in just while the game colours it, then put back.
    private sealed class Swap
    {
        public SerializableColor Entry;
        public float R, G, B, A;
    }

    private static Swap SwapIn(HorseData horse)
    {
        if (!Plugin.Enabled.Value || horse == null)
            return null;
        var kind = Find(Store.HalterOf(horse.uuid));
        var colors = DataManager.i?.data?.stableGroupColors;
        if (kind == null || colors == null || horse.stableGroup < 0 || horse.stableGroup >= colors.Count)
            return null;
        var entry = colors[horse.stableGroup];
        if (entry == null)
            return null;
        var swap = new Swap { Entry = entry, R = entry.r, G = entry.g, B = entry.b, A = entry.a };
        entry.r = kind.Color.r;
        entry.g = kind.Color.g;
        entry.b = kind.Color.b;
        entry.a = 1f;
        return swap;
    }

    private static void SwapOut(Swap swap)
    {
        if (swap?.Entry == null)
            return;
        swap.Entry.r = swap.R;
        swap.Entry.g = swap.G;
        swap.Entry.b = swap.B;
        swap.Entry.a = swap.A;
    }

    [HarmonyPatch(typeof(StableHorse), nameof(StableHorse.UpdateHalterColor))]
    [HarmonyPrefix]
    private static void BeforeStableHalter(StableHorse __instance, out Swap __state)
    {
        __state = null;
        try { __state = SwapIn(__instance?.Data); }
        catch (Exception e) { Plugin.Logger.LogError($"Colouring the halter failed: {e}"); }
    }

    [HarmonyPatch(typeof(StableHorse), nameof(StableHorse.UpdateHalterColor))]
    [HarmonyFinalizer]
    private static void AfterStableHalter(Swap __state) => SwapOut(__state);

    [HarmonyPatch(typeof(DisplayHorse), nameof(DisplayHorse.UpdateHalterColor))]
    [HarmonyPrefix]
    private static void BeforeDisplayHalter(DisplayHorse __instance, out Swap __state)
    {
        __state = null;
        try { __state = SwapIn(__instance?.Data); }
        catch (Exception e) { Plugin.Logger.LogError($"Colouring the halter failed: {e}"); }
    }

    [HarmonyPatch(typeof(DisplayHorse), nameof(DisplayHorse.UpdateHalterColor))]
    [HarmonyFinalizer]
    private static void AfterDisplayHalter(Swap __state) => SwapOut(__state);

    // On a walking trail (Forest Walk) the rider leads the horse with a rope of the sidescroller's own
    // (HorseHolder/RiderOnFoot/Leadrope), which UpdateHalterColor never reaches. Its material is plain
    // white, so the line's own colour is set to the halter's.
    private static Color? walkRope;

    [HarmonyPatch(typeof(SidescrollerHorse), nameof(SidescrollerHorse.SetData))]
    [HarmonyPostfix]
    private static void WalkRopeOnSetData(SidescrollerHorse __instance, HorseData __0, SidescrollerMode __2)
    {
        try
        {
            walkRope = null;
            if (!Plugin.Enabled.Value || __2 != SidescrollerMode.WALKING || __0 == null)
                return;
            var kind = Find(Store.HalterOf(__0.uuid));
            if (kind == null)
                return;
            walkRope = kind.Color;
            var holder = __instance.transform.parent != null ? __instance.transform.parent : __instance.transform;
            ColourRopes(holder);
        }
        catch (Exception e) { Plugin.Logger.LogError($"Colouring the lead rope failed: {e}"); }
    }

    // The rope may set itself up after SetData, so it is coloured again when it starts.
    [HarmonyPatch(typeof(LineRendererRope), nameof(LineRendererRope.Start))]
    [HarmonyPostfix]
    private static void WalkRopeOnStart(LineRendererRope __instance)
    {
        try
        {
            if (walkRope == null || __instance.transform.parent == null || __instance.transform.parent.name != "RiderOnFoot")
                return;
            Colour(__instance);
        }
        catch (Exception e) { Plugin.Logger.LogError($"Colouring the lead rope failed: {e}"); }
    }

    private static void ColourRopes(Transform root)
    {
        foreach (var rope in root.GetComponentsInChildren<LineRendererRope>(true))
            Colour(rope);
    }

    private static void Colour(LineRendererRope rope)
    {
        var line = rope.lineRenderer;
        if (line == null)
            line = rope.GetComponent<LineRenderer>();
        if (line == null || walkRope == null)
            return;
        line.startColor = walkRope.Value;
        line.endColor = walkRope.Value;
    }

    // The halter's own picture from the horse (white straps, black lines, so a tint colours the straps).
    // It is only loaded where horses are drawn, so it is kept from the yard or stable for the store.
    private static Sprite halterSprite;

    public static Sprite Icon()
    {
        if (halterSprite == null)
            Keep(Resources.FindObjectsOfTypeAll<Sprite>().FirstOrDefault(sp => sp.name == "halter-main_2"));
        return halterSprite;
    }

    // Unity unloads pictures no scene uses when the town loads; this one stays for the store.
    private static void Keep(Sprite sprite)
    {
        if (sprite == null || halterSprite != null)
            return;
        sprite.hideFlags |= HideFlags.DontUnloadUnusedAsset;
        if (sprite.texture != null)
            sprite.texture.hideFlags |= HideFlags.DontUnloadUnusedAsset;
        halterSprite = sprite;
    }

    [HarmonyPatch(typeof(DisplayHorse), nameof(DisplayHorse.UpdateHalterColor))]
    [HarmonyPostfix]
    private static void RememberHalterSprite(DisplayHorse __instance)
    {
        try { Keep(__instance?.halter?.sprite); }
        catch { }
    }

    [HarmonyPatch(typeof(Yard), nameof(Yard.Start))]
    [HarmonyPostfix]
    private static void RememberInYard()
    {
        try { Icon(); }
        catch { }
    }

    // A copy of a sprite's pixels; the game's textures can't be read directly, so it goes through a render texture.
    public static Texture2D ReadSprite(Sprite sprite)
    {
        var r = sprite.rect;
        var tex = sprite.texture;
        int x = Mathf.RoundToInt(r.x), y = Mathf.RoundToInt(r.y), w = Mathf.RoundToInt(r.width), h = Mathf.RoundToInt(r.height);
        var rt = new RenderTexture(tex.width, tex.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        Graphics.Blit(tex, rt, Vector2.one, Vector2.zero);
        var previous = RenderTexture.active;
        RenderTexture.active = rt;
        var copy = new Texture2D(w, h, TextureFormat.RGBA32, false);
        copy.ReadPixels(new Rect(x, y, w, h), 0, 0);
        copy.Apply();
        RenderTexture.active = previous;
        rt.Release();
        Object.Destroy(rt);
        return copy;
    }

    // The halter picture has grey straps that the game's tack material brightens; UI images don't have that
    // material, so each colour gets a ready-coloured copy: straps in the colour, outlines stay black.
    private static readonly Dictionary<string, Sprite> tinted = new();

    public static Sprite IconFor(Kind kind)
    {
        if (tinted.TryGetValue(kind.Id, out var known) && known != null)
            return known;
        var source = Icon();
        if (source == null)
            return null;
        try
        {
            var tex = ReadSprite(source);
            var pixels = tex.GetPixels();
            // The straps are the brightest grey in the picture; that grey becomes the full colour.
            var strap = 0f;
            foreach (var c in pixels)
                if (c.a > 0.5f)
                    strap = Math.Max(strap, Gray(c));
            strap = Math.Max(strap, 0.05f);
            for (var i = 0; i < pixels.Length; i++)
            {
                var c = pixels[i];
                var t = Mathf.Clamp01(Gray(c) / strap);
                pixels[i] = new Color(kind.Color.r * t, kind.Color.g * t, kind.Color.b * t, c.a);
            }
            tex.SetPixels(pixels);
            tex.Apply();
            tex.name = "halter-" + kind.Id;
            Object.DontDestroyOnLoad(tex);
            tex.hideFlags = HideFlags.DontUnloadUnusedAsset;
            var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), source.pixelsPerUnit);
            sprite.name = "WardrobeAndTackOverhaul halter " + kind.Id;
            Object.DontDestroyOnLoad(sprite);
            sprite.hideFlags = HideFlags.DontUnloadUnusedAsset;
            tinted[kind.Id] = sprite;
            return sprite;
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"Colouring the halter picture failed: {e}");
            return source;
        }
    }

    private static float Gray(Color c) => 0.299f * c.r + 0.587f * c.g + 0.114f * c.b;

    // The custom colour item's picture in the shop list: straps in rainbow stripes.
    private static Sprite rainbow;

    private static Sprite RainbowIcon()
    {
        if (rainbow != null)
            return rainbow;
        var source = Icon();
        if (source == null)
            return null;
        try
        {
            var tex = ReadSprite(source);
            var pixels = tex.GetPixels();
            var strap = 0.05f;
            foreach (var c in pixels)
                if (c.a > 0.5f)
                    strap = Math.Max(strap, Gray(c));
            for (var y = 0; y < tex.height; y++)
                for (var x = 0; x < tex.width; x++)
                {
                    var i = y * tex.width + x;
                    var c = pixels[i];
                    var t = Mathf.Clamp01(Gray(c) / strap);
                    var hue = Color.HSVToRGB((x + y) / (float)(tex.width + tex.height) * 0.85f, 0.75f, 0.95f);
                    pixels[i] = new Color(hue.r * t, hue.g * t, hue.b * t, c.a);
                }
            tex.SetPixels(pixels);
            tex.Apply();
            tex.name = "halter-rainbow";
            Object.DontDestroyOnLoad(tex);
            tex.hideFlags = HideFlags.DontUnloadUnusedAsset;
            rainbow = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), source.pixelsPerUnit);
            rainbow.name = "WardrobeAndTackOverhaul halter custom";
            Object.DontDestroyOnLoad(rainbow);
            rainbow.hideFlags = HideFlags.DontUnloadUnusedAsset;
            return rainbow;
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"Making the custom halter picture failed: {e}");
            return source;
        }
    }

    public static void ShowIcon(Image image, Kind kind)
    {
        var sprite = kind != null ? IconFor(kind) : Icon();
        if (image == null || sprite == null)
            return;
        image.sprite = sprite;
        image.color = Color.white;
        image.material = null;
        image.preserveAspect = true;
    }

    public static void Choose(StableHorse horse, string id)
    {
        var data = horse?.Data;
        if (data == null)
            return;
        Store.SetHalter(data.uuid, id);
        horse.UpdateHalterColor();
        Plugin.Logger.LogInfo($"{data.name} now wears the {id} halter.");
    }

    // ---- in the equipment store ----

    // Each halter is sold as a real shop item in the ear bonnet list, so choosing, the basket and paying
    // at the counter are the game's own. The game knows no halter tack type, so the items are ear
    // bonnets by type; buying one is caught before it reaches the inventory.
    public const string IdPrefix = "item.tack.halter.wardrobeandtackoverhaul.";
    private const int EarBonnetCategory = (int)TackType.EAR_BONNET;

    private static readonly Dictionary<string, TackItem> items = new();
    private static bool openingEarBonnets;

    // The game's own custom colour items end in this; the store opens its colour picker for them and puts
    // the picked colour into the bought item's id instead ("...#1E1E1E").
    private const string CustomMark = "#customColor";
    public const string CustomId = IdPrefix + CustomMark;
    public const int CustomPrice = 60;

    // While its picker is open the store holds a copy of the item with "#customColor" cut off.
    private static bool IsCustomTemplate(string id) => id == CustomId || id == IdPrefix;

    public static string ColorOf(string itemId)
    {
        if (itemId == null || !itemId.StartsWith(IdPrefix) || IsCustomTemplate(itemId))
            return null;
        var rest = itemId.Substring(IdPrefix.Length);
        return rest.StartsWith("#") ? rest.ToUpperInvariant() : rest;
    }

    public static string ItemName(Kind kind) => $"{kind.Label} Halter with Lead Rope";

    private static TackItem customItem;

    // Offered only once the store sells the game's own custom coloured ear bonnet, and copies its colour slots.
    private static TackItem CustomItem(TackItem gameCustom)
    {
        if (customItem != null && customItem.spriteShelfLayer1 != null)
            return customItem;
        var sprite = RainbowIcon();
        if (sprite == null)
            return null;
        // matchColor stays off: with it the store hue-shifts the picture through its tack material, which
        // turns the ready-coloured halter pictures into the wrong colours.
        customItem = new TackItem(TackType.EAR_BONNET, CustomId, CustomPrice, sprite, null,
            new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<Sprite>(0),
            new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<Sprite>(0),
            new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<Color>(new[] { Color.white }),
            new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<bool>(new[] { false }));
        customItem.availableInShop = true;
        customItem.availableAtStage = gameCustom?.availableAtStage ?? 0;
        return customItem;
    }

    private static TackItem ItemFor(Kind kind)
    {
        var id = IdPrefix + kind.Id;
        if (items.TryGetValue(id, out var known) && known != null && known.spriteShelfLayer1 != null)
            return known;
        var sprite = IconFor(kind);
        if (sprite == null)
            return null;
        var item = new TackItem(TackType.EAR_BONNET, id, Price, sprite, null,
            new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<Sprite>(0),
            new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<Sprite>(0),
            new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<Color>(new[] { Color.white }),
            new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<bool>(new[] { false }));
        item.onlyAvailableOnce = true;
        item.availableInShop = true;
        items[id] = item;
        return item;
    }

    [HarmonyPatch(typeof(EquipmentStore), nameof(EquipmentStore.OpenTackCategory))]
    [HarmonyPrefix]
    private static void BeforeTackCategory(int __0)
    {
        openingEarBonnets = __0 == EarBonnetCategory;
    }

    [HarmonyPatch(typeof(EquipmentStore), nameof(EquipmentStore.OpenTackCategory))]
    [HarmonyFinalizer]
    private static void AfterTackCategory() => openingEarBonnets = false;

    [HarmonyPatch(typeof(UiEquipmentSelection), nameof(UiEquipmentSelection.Open))]
    [HarmonyPrefix]
    private static void AddHaltersToList(UiEquipmentSelection __instance, Il2CppSystem.Collections.Generic.List<BuyableItem> __0)
    {
        try
        {
            if (!openingEarBonnets || !Plugin.Enabled.Value || __0 == null)
                return;
            var basket = __instance.equipmentStore?.itemsInBasket;
            foreach (var kind in All.Where(k => k.Id != Blue && !Store.OwnsHalter(k.Id)))
            {
                var item = ItemFor(kind);
                if (item == null)
                    continue;
                var inBasket = false;
                if (basket != null)
                    foreach (var b in basket)
                        if (b != null && b.id == item.id)
                            inBasket = true;
                if (!inBasket)
                    __0.Add(item);
            }
            TackItem gameCustom = null;
            foreach (var b in __0)
                if (b != null && b.id != null && b.id.EndsWith(CustomMark) && !b.id.StartsWith(IdPrefix))
                    gameCustom = b.TryCast<TackItem>();
            if (gameCustom != null)
            {
                var custom = CustomItem(gameCustom);
                if (custom != null)
                    __0.Add(custom);
            }
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"Adding the halters to the store list failed: {e}");
        }
    }

    // Paid for at the counter: the halter is owned from now on, but it is no ear bonnet for the tack cabinet.
    [HarmonyPatch(typeof(InventoryData), nameof(InventoryData.AddTackToInventory))]
    [HarmonyPrefix]
    private static bool BeforeAddTack(TackItem __0)
    {
        try
        {
            var color = ColorOf(__0?.id);
            if (color == null)
                return true;
            Store.AddHalter(color);
            Plugin.Logger.LogInfo($"Bought the {color} halter.");
            return false;
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"Recording the bought halter failed: {e}");
            return true;
        }
    }

    // The picker's preview: one picture recoloured on every change (dragging would otherwise make a
    // texture per colour).
    private static Texture2D previewTex;
    private static Sprite previewSprite;
    private static Color[] previewBase;
    private static float previewStrap;

    private static Sprite PreviewFor(Color color)
    {
        if (previewSprite == null)
        {
            var source = Icon();
            if (source == null)
                return null;
            previewTex = ReadSprite(source);
            previewTex.name = "halter-preview";
            Object.DontDestroyOnLoad(previewTex);
            previewTex.hideFlags = HideFlags.DontUnloadUnusedAsset;
            previewBase = previewTex.GetPixels();
            previewStrap = 0.05f;
            foreach (var c in previewBase)
                if (c.a > 0.5f)
                    previewStrap = Math.Max(previewStrap, Gray(c));
            previewSprite = Sprite.Create(previewTex, new Rect(0, 0, previewTex.width, previewTex.height), new Vector2(0.5f, 0.5f), source.pixelsPerUnit);
            previewSprite.name = "WardrobeAndTackOverhaul halter preview";
            Object.DontDestroyOnLoad(previewSprite);
            previewSprite.hideFlags = HideFlags.DontUnloadUnusedAsset;
        }
        var pixels = new Color[previewBase.Length];
        for (var i = 0; i < pixels.Length; i++)
        {
            var c = previewBase[i];
            var t = Mathf.Clamp01(Gray(c) / previewStrap);
            pixels[i] = new Color(color.r * t, color.g * t, color.b * t, c.a);
        }
        previewTex.SetPixels(pixels);
        previewTex.Apply();
        return previewSprite;
    }

    [HarmonyPatch(typeof(EquipmentStore), nameof(EquipmentStore.SelectEquipmentToBuy))]
    [HarmonyPatch(typeof(EquipmentStore), nameof(EquipmentStore.OnColorChange))]
    [HarmonyPostfix]
    private static void ShowPickedColour(EquipmentStore __instance)
    {
        try
        {
            if (!IsCustomTemplate(__instance?.itemForColorPicking?.id))
                return;
            var pickers = __instance.colorPickers;
            var image = __instance.colorPickingPreview;
            if (pickers == null || pickers.Length == 0 || pickers[0] == null || image == null)
                return;
            var sprite = PreviewFor(pickers[0].GetSelectedColor());
            if (sprite == null)
                return;
            image.sprite = sprite;
            image.material = null;
            image.color = Color.white;
            image.preserveAspect = true;
            // The game may draw a second layer (lines) on top; the halter picture has its own.
            for (var i = 0; i < image.transform.childCount; i++)
            {
                var layer = image.transform.GetChild(i).GetComponent<Image>();
                if (layer != null)
                    layer.enabled = false;
            }
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"Showing the custom halter colour failed: {e}");
        }
    }

    // The bought item is the game's copy of the custom item with the colour in its id; its picture is
    // still the rainbow one, so it gets the halter in its colour for the basket.
    [HarmonyPatch(typeof(EquipmentStore), nameof(EquipmentStore.AddItemToBasket))]
    [HarmonyPrefix]
    private static void BeforeAddToBasket(BuyableItem __0)
    {
        try
        {
            var kind = Find(ColorOf(__0?.id));
            if (kind == null || !IsCustom(kind.Id))
                return;
            var sprite = IconFor(kind);
            if (sprite != null)
                __0.spriteShelfLayer1 = sprite;
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"Colouring the custom halter in the basket failed: {e}");
        }
    }

    // The game refuses a custom colour that is already owned by looking in the tack inventory, where halters
    // never go, so that check is made here with the game's own message.
    [HarmonyPatch(typeof(EquipmentStore), nameof(EquipmentStore.ConfirmCustomColorItem))]
    [HarmonyPrefix]
    private static bool BeforeConfirmCustom(EquipmentStore __instance)
    {
        try
        {
            if (!IsCustomTemplate(__instance?.itemForColorPicking?.id))
                return true;
            var pickers = __instance.colorPickers;
            if (pickers == null || pickers.Length == 0 || pickers[0] == null)
                return true;
            var hex = Hex(pickers[0].GetSelectedColor());
            Plugin.Logger.LogInfo($"Custom halter colour picked: {hex}");
            if (!Store.OwnsHalter(hex))
                return true;
            NotificationManager.i?.ShowToast(Lang.gs("toast.ui.colorPicker.alreadyExists"), 3f, 0f);
            return false;
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"Checking the custom halter colour failed: {e}");
            return true;
        }
    }

    // The shop shows item names (and the basket its lines) through translation keys made from the item id.
    [HarmonyPatch(typeof(Lang), nameof(Lang.gs))]
    [HarmonyPatch(typeof(Lang), nameof(Lang.getString))]
    [HarmonyPostfix]
    private static void HalterName(string __0, ref string __result)
    {
        try
        {
            if (__0 == null || !__0.Contains(IdPrefix))
                return;
            var rest = __0.Substring(__0.IndexOf(IdPrefix, StringComparison.Ordinal) + IdPrefix.Length);
            var desc = rest.Contains("desc");
            // The game looks custom coloured items up by the id cut off at '#', so their key ends right here.
            if (rest == "" || rest.StartsWith("#") || rest.StartsWith("."))
            {
                __result = desc ? "A halter with matching lead rope in a colour of your choice, for every horse in the stable."
                                : "Custom Color Halter with Lead Rope";
                return;
            }
            var kind = All.FirstOrDefault(k => rest == k.Id || rest.StartsWith(k.Id + "."));
            if (kind == null)
                return;
            __result = desc ? "A halter with matching lead rope, for every horse in the stable." : ItemName(kind);
        }
        catch { }
    }
}
