using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;

namespace WardrobeAndTackOverhaul;

// Adds the Save button and the outfit tabs to the wardrobe (rider) and the stable's tack cabinet.
[HarmonyPatch]
internal static class Cabinets
{
    private static readonly Dictionary<IntPtr, OutfitTabs> tabsByCabinet = new();

    internal static readonly int[] OutfitSlots = { (int)ClothesType.HAT, (int)ClothesType.JACKET, (int)ClothesType.PANTS, (int)ClothesType.SHOES };

    private const float ShelfLift = 45f;
    private const float TabGap = 12f;

    // ---- wardrobe ----

    [HarmonyPatch(typeof(ClothesCabinet), nameof(ClothesCabinet.OnOpen))]
    [HarmonyPostfix]
    private static void WardrobeOpened(ClothesCabinet __instance)
    {
        try
        {
            if (!Plugin.Enabled.Value)
                return;
            var tabs = WardrobeTabs(__instance);
            var player = __instance.player;
            if (tabs == null || player == null)
                return;
            var offered = player.currentOutfitType == OutfitType.COMPETITION ? Activities.Competition : Activities.Everyday;
            tabs.Prepare(offered, new WardrobeTarget(player));
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"Adding the outfit tabs to the wardrobe failed: {e}");
        }
    }

    // Prefix: Default must be back before the game does anything on closing.
    [HarmonyPatch(typeof(ClothesCabinet), nameof(ClothesCabinet.OnClose))]
    [HarmonyPrefix]
    private static void WardrobeClosing(ClothesCabinet __instance) => CloseFor(__instance);

    private static OutfitTabs WardrobeTabs(ClothesCabinet cabinet)
    {
        if (tabsByCabinet.TryGetValue(cabinet.Pointer, out var known) && known.Alive)
            return known;
        var root = Root(cabinet.transform);
        var navigation = OutfitTabs.Deep(root, "Navigation");
        var saveTemplate = navigation != null ? OutfitTabs.Deep(navigation, "ButtonBack") : null;
        var tabTemplate = OutfitTabs.Deep(root, "ActionDressCompetition");
        var background = cabinet.transform.Find("Background");
        if (saveTemplate == null || tabTemplate == null || background == null || cabinet.title == null)
        {
            Plugin.Logger.LogWarning("The wardrobe doesn't look as expected (ButtonBack, ActionDressCompetition, Background or heading missing); no outfit tabs there.");
            return null;
        }
        var tabs = new OutfitTabs(cabinet.transform, cabinet.title, saveTemplate.gameObject, tabTemplate.gameObject,
            (visible, button, _) =>
            {
                // Save at the bottom of the paper, centred.
                button.SetParent(background, false);
                button.anchorMin = button.anchorMax = new Vector2(0.5f, 0f);
                button.anchoredPosition = new Vector2(0, button.rect.height / 2 + 18);
                // Back under the cabinet, in front of the shelves, which would otherwise take its clicks.
                button.SetParent(cabinet.transform, true);

                // A column of tabs along the left edge of the paper, like tabs on a folder.
                var y = 120f;
                foreach (var tab in visible)
                {
                    tab.SetParent(background, false);
                    tab.anchorMin = tab.anchorMax = new Vector2(0f, 1f);
                    tab.pivot = new Vector2(1f, 1f);
                    tab.anchoredPosition = new Vector2(8, -y);
                    y += tab.rect.height + TabGap;
                    tab.SetParent(cabinet.transform, true);
                }
                RightEdgeAt(visible, LeftEdge(cabinet.transform));
            });
        // The shoe shelf reaches the bottom of the paper; there is room under the heading, so the shelves move up a little.
        foreach (var shelf in new[] { "Hats", "Jackets", "Pants", "Shoes" })
        {
            var t = cabinet.transform.Find(shelf);
            if (t != null)
                t.localPosition += new Vector3(0, ShelfLift, 0);
        }
        tabsByCabinet[cabinet.Pointer] = tabs;
        return tabs;
    }

    private sealed class WardrobeTarget : IOutfitTarget
    {
        private readonly WardrobePlayer player;
        private readonly int outfitType;

        public WardrobeTarget(WardrobePlayer player)
        {
            this.player = player;
            outfitType = (int)player.currentOutfitType;
        }

        private Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStringArray Data()
        {
            var outfits = DataManager.i?.data?.player?.outfits;
            return outfits != null && outfitType < outfits.Count ? outfits[outfitType]?.outfit : null;
        }

        public bool Valid => player != null && Data() != null;

        // The game shows the other outfit now (the cabinet was not closed in between).
        public bool Switched => player != null && (int)player.currentOutfitType != outfitType;

        public string[] Worn()
        {
            var data = Data();
            var ids = new string[Math.Max(data?.Length ?? 0, 6)];
            for (var i = 0; i < ids.Length; i++)
                ids[i] = data != null && i < data.Length ? data[i] ?? "" : "";
            var worn = player.wornClothes;
            foreach (var slot in OutfitSlots)
                if (worn != null && slot < worn.Length)
                    ids[slot] = worn[slot]?.clothesItem?.id ?? "";
            return ids;
        }

        public void Put(string[] ids)
        {
            var data = Data();
            foreach (var slot in OutfitSlots)
                if (slot < data.Length && slot < ids.Length)
                    GameArrays.Set(data, slot, ids[slot]);
            player.LoadSavedClothes();
        }

        public void PutData(string[] ids)
        {
            var data = Data();
            if (data == null)
                return;
            foreach (var slot in OutfitSlots)
                if (slot < data.Length && slot < ids.Length)
                    GameArrays.Set(data, slot, ids[slot]);
        }

        public string[] Saved(Activity a) => Store.Outfit(a);

        public void Save(Activity a, string[] ids)
        {
            Store.SetOutfit(new[] { a }, ids);
            Plugin.Logger.LogInfo($"Outfit [{string.Join(", ", ids)}] saved for {a}.");
        }
    }

    // ---- tack ----

    [HarmonyPatch(typeof(StableCabinet), nameof(StableCabinet.OnOpen))]
    [HarmonyPostfix]
    private static void TackOpened(StableCabinet __instance)
    {
        try
        {
            if (!Plugin.Enabled.Value || !IsTackCabinet(__instance))
                return;
            var tabs = TackTabs(__instance);
            if (tabs != null && __instance.horse != null)
            {
                tabs.Prepare(Activities.All, new TackTarget(__instance));
                // Our Close below the tabs does what the game's "Save & Close" at the top does.
                __instance.transform.Find("ButtonClose")?.gameObject.SetActive(false);
            }
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"Adding the tack tabs failed: {e}");
        }
    }

    [HarmonyPatch(typeof(StableCabinet), nameof(StableCabinet.OnClose))]
    [HarmonyPrefix]
    private static void TackClosing(StableCabinet __instance) => CloseFor(__instance);

    // The stable's feed, brush and medical cabinets are StableCabinets too; only the tack one gets tabs.
    private static bool IsTackCabinet(StableCabinet cabinet)
    {
        var tack = cabinet.horse?.uiStable?.tackStorage;
        return tack != null && tack.Pointer == cabinet.Pointer;
    }

    private static OutfitTabs TackTabs(StableCabinet cabinet)
    {
        if (tabsByCabinet.TryGetValue(cabinet.Pointer, out var known) && known.Alive)
            return known;
        var template = cabinet.transform.Find("ButtonClose");
        if (template == null)
        {
            Plugin.Logger.LogWarning("The tack cabinet doesn't look as expected (ButtonClose missing); no tack tabs there.");
            return null;
        }
        var t = OutfitTabs.Rect(template);
        void LikeTemplate(RectTransform r, int row, float widen)
        {
            r.SetParent(t.parent, false);
            r.anchorMin = t.anchorMin;
            r.anchorMax = t.anchorMax;
            r.pivot = t.pivot;
            r.sizeDelta = new Vector2(t.sizeDelta.x * widen, t.sizeDelta.y);
            // Right edges line up with the game's button, so wider tabs grow to the left.
            var shift = (t.sizeDelta.x * widen - t.sizeDelta.x) * (1 - t.pivot.x);
            r.anchoredPosition = t.anchoredPosition - new Vector2(shift, row * (t.rect.height + TabGap));
        }
        var close = template.GetComponent<UnityEngine.UI.Button>();
        var halterPage = new HalterPage(cabinet, template.gameObject);
        var tabs = new OutfitTabs(cabinet.transform, null, template.gameObject, template.gameObject,
            (visible, save, closeButton) =>
            {
                // Tabs in a column from where the game's "Save & Close" was, then Save and Close side by side.
                // Default stays one row below the game's (hidden) "Save & Close"; Halter and the rest follow.
                for (var i = 0; i < visible.Count; i++)
                    LikeTemplate(visible[i], i + 1, 1.35f);
                LikeTemplate(save, visible.Count + 1, 1.35f);
                LikeTemplate(closeButton, visible.Count + 1, 1.35f);
                var column = new List<RectTransform>(visible) { save, closeButton };
                RightEdgeAt(column, LeftEdge(cabinet.transform));
                SideBySide(save, closeButton);
            },
            // The game's own button, so closing does exactly what "Save & Close" does (Default goes back first).
            () => close?.onClick.Invoke(),
            ("Halter", halterPage.Show, halterPage.Hide));
        tabsByCabinet[cabinet.Pointer] = tabs;
        return tabs;
    }

    private sealed class TackTarget : IOutfitTarget
    {
        private readonly StableCabinet cabinet;
        private readonly StableHorse horse;
        private readonly HorseData data;

        public TackTarget(StableCabinet cabinet)
        {
            this.cabinet = cabinet;
            horse = cabinet.horse;
            data = horse.Data;
        }

        public bool Valid => cabinet != null && horse != null && data?.tack != null;

        public string[] Worn()
        {
            var worn = horse.wornTack;
            var ids = new string[Math.Max(worn?.Length ?? 0, data.tack.Length)];
            for (var i = 0; i < ids.Length; i++)
                ids[i] = worn != null && i < worn.Length ? worn[i]?.tackItem?.id ?? "" : "";
            return ids;
        }

        public void Put(string[] ids)
        {
            // Loading only puts tack on; empty slots would keep what was on before.
            horse.ClearAllTack(false);
            var tack = data.tack;
            for (var i = 0; i < tack.Length; i++)
                GameArrays.Set(tack, i, i < ids.Length ? ids[i] : "");
            horse.LoadSavedTack(cabinet.tacksByType);
        }

        public void PutData(string[] ids)
        {
            var tack = data?.tack;
            if (tack == null)
                return;
            for (var i = 0; i < tack.Length; i++)
                GameArrays.Set(tack, i, i < ids.Length ? ids[i] : "");
        }

        public string[] Saved(Activity a) => Store.HorseTack(data.uuid, a);

        public void Save(Activity a, string[] ids)
        {
            Store.SetHorseTack(data.uuid, new[] { a }, ids);
            Plugin.Logger.LogInfo($"Tack of {data.name} [{string.Join(", ", ids)}] saved for {a}.");
        }
    }

    // ---- keeping the game's own sets safe ----

    // Leaving the screen with the cabinet still open doesn't always call OnClose.
    [HarmonyPatch(typeof(Cabinet), nameof(Cabinet.OnDisable))]
    [HarmonyPrefix]
    private static void CabinetDisabled(Cabinet __instance) => RestoreFor(__instance);

    [HarmonyPatch(typeof(Cabinet), nameof(Cabinet.Update))]
    [HarmonyPostfix]
    private static void CabinetUpdate(Cabinet __instance)
    {
        if (tabsByCabinet.Count == 0 || !tabsByCabinet.TryGetValue(__instance.Pointer, out var tabs) || !tabs.Alive)
            return;
        tabs.Tick();
        try
        {
            // Normal/Competition Outfit pressed while the wardrobe stays open: the shown set goes back to
            // Default for the outfit it belongs to, and the tabs are set up again for the other outfit.
            if (tabs.Target is WardrobeTarget wardrobe && wardrobe.Switched)
            {
                tabs.Restore();
                var clothes = __instance.TryCast<ClothesCabinet>();
                if (clothes != null)
                    WardrobeOpened(clothes);
            }
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"Following the outfit switch failed: {e}");
        }
    }

    // The game never saves an activity's set as its own: Default goes back first.
    [HarmonyPatch(typeof(DataManager), nameof(DataManager.Save))]
    [HarmonyPrefix]
    private static void BeforeGameSave() => RestoreAllSafely();

    // Another horse in the stable screen: the previous horse gets its own tack back.
    [HarmonyPatch(typeof(UiStable), nameof(UiStable.SetData))]
    [HarmonyPrefix]
    private static void BeforeHorseChange() => RestoreAllSafely();

    private static void RestoreAllSafely()
    {
        try { OutfitTabs.RestoreAll(); }
        catch (Exception e) { Plugin.Logger.LogError($"Putting the default sets back failed: {e}"); }
    }

    private static void CloseFor(Component cabinet)
    {
        RestoreFor(cabinet);
        // The game's own button comes back, so the cabinet is as the game left it.
        if (cabinet != null && cabinet.TryCast<StableCabinet>() != null)
            cabinet.transform.Find("ButtonClose")?.gameObject.SetActive(true);
        try
        {
            if (cabinet != null && tabsByCabinet.TryGetValue(cabinet.Pointer, out var tabs) && tabs.Alive)
                tabs.Hide();
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"Hiding the tabs failed: {e}");
        }
    }

    // Two buttons of the same full width at the same spot become two halves of it.
    private static void SideBySide(RectTransform left, RectTransform right)
    {
        var corners = new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<Vector3>(4);
        left.GetWorldCorners(corners);
        float l = corners[0].x, r = corners[2].x;
        var half = (left.rect.width - TabGap) / 2;
        foreach (var (button, atLeft) in new[] { (left, true), (right, false) })
        {
            button.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, half);
            button.GetWorldCorners(corners);
            var shift = atLeft ? l - corners[0].x : r - corners[2].x;
            button.position += new Vector3(shift, 0, 0);
        }
    }

    private static void RestoreFor(Component cabinet)
    {
        try
        {
            if (cabinet != null && tabsByCabinet.TryGetValue(cabinet.Pointer, out var tabs) && tabs.Alive)
                tabs.Restore();
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"Putting the default set back failed: {e}");
        }
    }

    // Leftmost point of the cabinet's paper or board and the shelf arrows that stick out of it (world space).
    private static float LeftEdge(Transform cabinet)
    {
        var left = float.MaxValue;
        var corners = new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<Vector3>(4);
        void Take(Transform t)
        {
            if (t == null || !t.gameObject.activeInHierarchy)
                return;
            OutfitTabs.Rect(t).GetWorldCorners(corners);
            left = Math.Min(left, corners[0].x);
        }
        Take(cabinet.Find("Background"));
        foreach (var shelf in cabinet.GetComponentsInChildren<UnityEngine.UI.ScrollRect>(false))
            Take(shelf.transform.Find("ButtonLeft"));
        return left;
    }

    // Tabs end a little left of that edge, so they cover no arrow.
    private static void RightEdgeAt(List<RectTransform> tabs, float edge)
    {
        if (edge == float.MaxValue)
            return;
        var corners = new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<Vector3>(4);
        foreach (var tab in tabs)
        {
            tab.GetWorldCorners(corners);
            var gap = (corners[2].x - corners[0].x) * 0.03f;
            tab.position += new Vector3(edge - gap - corners[2].x, 0, 0);
        }
    }

    private static Transform Root(Transform t)
    {
        while (t.parent != null)
            t = t.parent;
        return t;
    }

    // The tack cabinet's "Halter" page: the shelves make way for one button per halter colour.
    private sealed class HalterPage
    {
        private static readonly Color Worn = new(1f, 0.78f, 0.5f);

        private readonly StableCabinet cabinet;
        private readonly GameObject template;
        private readonly List<GameObject> hidden = new();
        private readonly List<(Halters.Kind kind, GameObject button)> buttons = new();
        private TMP_Text title;
        private string titleText;
        // While the page shows, the horse stands as in the stable: tack off, halter and lead rope on.
        private string[] tackStash;
        private bool? wasTacking;
        private readonly List<(GameObject go, bool active)> halterParts = new();

        public HalterPage(StableCabinet cabinet, GameObject template)
        {
            this.cabinet = cabinet;
            this.template = template;
        }

        public void Show()
        {
            var background = cabinet.transform.Find("Background");
            hidden.Clear();
            for (var i = 0; i < cabinet.transform.childCount; i++)
            {
                var child = cabinet.transform.GetChild(i);
                if (!child.gameObject.activeSelf || child.name.StartsWith("WardrobeAndTackOverhaul") ||
                    child.name is "Background" or "ColliderRack" or "TextTitle")
                    continue;
                child.gameObject.SetActive(false);
                hidden.Add(child.gameObject);
            }
            title = cabinet.transform.Find("TextTitle")?.GetComponent<TMP_Text>();
            if (title != null)
            {
                titleText = title.text;
                title.text = "Halter";
            }
            if (buttons.Count == 0 && background != null)
                Build(background);
            Refresh();
            foreach (var (_, button) in buttons)
                button.SetActive(true);
            TackOff();
        }

        private void TackOff()
        {
            try
            {
                var horse = cabinet.horse;
                var tack = horse?.Data?.tack;
                if (horse == null || tack == null)
                    return;
                tackStash = new string[tack.Length];
                for (var i = 0; i < tack.Length; i++)
                    tackStash[i] = tack[i] ?? "";
                wasTacking = horse.isInTackingMode;
                horse.ClearAllTack(false);
                horse.isInTackingMode = false;
                halterParts.Clear();
                foreach (var go in new[] { horse.displayHorse?.halter?.gameObject, horse.leadrope, horse.leadropeKnot?.gameObject })
                    if (go != null)
                    {
                        halterParts.Add((go, go.activeSelf));
                        go.SetActive(true);
                    }
                if (horse.displayHorse?.halter != null)
                    horse.displayHorse.halter.enabled = true;
                horse.UpdateHalterColor();
            }
            catch (Exception e)
            {
                Plugin.Logger.LogError($"Showing the horse with its halter failed: {e}");
            }
        }

        private void TackOn()
        {
            try
            {
                var horse = cabinet.horse;
                if (horse == null)
                    return;
                foreach (var (go, active) in halterParts)
                    if (go != null)
                        go.SetActive(active);
                halterParts.Clear();
                if (wasTacking != null)
                    horse.isInTackingMode = wasTacking.Value;
                wasTacking = null;
                var tack = horse.Data?.tack;
                if (tackStash != null && tack != null)
                {
                    for (var i = 0; i < tack.Length; i++)
                        GameArrays.Set(tack, i, i < tackStash.Length ? tackStash[i] : "");
                    horse.ClearAllTack(false);
                    horse.LoadSavedTack(cabinet.tacksByType);
                }
                tackStash = null;
            }
            catch (Exception e)
            {
                Plugin.Logger.LogError($"Putting the tack back on failed: {e}");
            }
        }

        public void Hide()
        {
            if (tackStash != null)
                TackOn();
            foreach (var (_, button) in buttons)
                if (button != null)
                    button.SetActive(false);
            foreach (var go in hidden)
                if (go != null)
                    go.SetActive(true);
            hidden.Clear();
            if (title != null && titleText != null)
                title.text = titleText;
        }

        private void Build(Transform background)
        {
            var y = 260f;
            foreach (var kind in Halters.All)
            {
                var copy = Object.Instantiate(template, cabinet.transform, false).Cast<GameObject>();
                copy.name = "WardrobeAndTackOverhaul Halter " + kind.Label;
                copy.SetActive(true);
                foreach (var updater in copy.GetComponentsInChildren<LanguageUpdater>(true))
                {
                    updater.enabled = false;
                    Object.Destroy(updater);
                }
                foreach (var image in copy.GetComponentsInChildren<UnityEngine.UI.Image>(true))
                    if (image.gameObject != copy)
                        Object.Destroy(image.gameObject);
                var rect = OutfitTabs.Rect(copy.transform);
                rect.SetParent(background, false);
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(rect.sizeDelta.x * 2.6f, rect.sizeDelta.y * 1.1f);
                rect.anchoredPosition = new Vector2(0, -y);
                y += rect.sizeDelta.y + 26f;
                rect.SetParent(cabinet.transform, true);

                var label = copy.GetComponentInChildren<TMP_Text>(true);
                if (label != null)
                {
                    label.alignment = TextAlignmentOptions.Left;
                    var lr = OutfitTabs.Rect(label.transform);
                    lr.anchorMin = Vector2.zero;
                    lr.anchorMax = Vector2.one;
                    lr.offsetMin = new Vector2(rect.sizeDelta.y + 20, 0);
                    lr.offsetMax = new Vector2(-10, 0);
                }
                var picture = new GameObject("Icon").AddComponent<UnityEngine.UI.Image>();
                picture.rectTransform.SetParent(rect, false);
                picture.rectTransform.anchorMin = picture.rectTransform.anchorMax = new Vector2(0f, 0.5f);
                picture.rectTransform.sizeDelta = new Vector2(rect.sizeDelta.y * 0.9f, rect.sizeDelta.y * 0.9f);
                picture.rectTransform.anchoredPosition = new Vector2(rect.sizeDelta.y * 0.6f, 0);
                picture.raycastTarget = false;
                Halters.ShowIcon(picture, kind.Color);
                var button = copy.GetComponent<UnityEngine.UI.Button>();
                button.onClick = new UnityEngine.UI.Button.ButtonClickedEvent();
                var k = kind;
                button.onClick.AddListener(new Action(() =>
                {
                    try
                    {
                        Halters.Choose(cabinet.horse, k.Id);
                        Refresh();
                    }
                    catch (Exception e)
                    {
                        Plugin.Logger.LogError($"Choosing the halter failed: {e}");
                    }
                }));
                copy.SetActive(false);
                buttons.Add((kind, copy));
            }
        }

        private void Refresh()
        {
            var worn = Store.HalterOf(cabinet.horse?.Data?.uuid) ?? Halters.Blue;
            foreach (var (kind, button) in buttons)
            {
                var owned = Store.OwnsHalter(kind.Id);
                var label = button.GetComponentInChildren<TMP_Text>(true);
                if (label != null)
                    label.text = owned ? kind.Label : $"{kind.Label}  <size=70%>(buy in the store)</size>";
                button.GetComponent<UnityEngine.UI.Button>().interactable = owned;
                var image = button.GetComponent<UnityEngine.UI.Image>();
                if (image != null)
                    image.color = kind.Id == worn ? Worn : Color.white;
            }
        }
    }
}
