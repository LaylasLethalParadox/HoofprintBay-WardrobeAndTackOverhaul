using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace WardrobeAndTackOverhaul;

// What a set of tabs dresses: the rider's outfit or one horse's tack.
internal interface IOutfitTarget
{
    bool Valid { get; }
    string[] Worn();                 // what is on now
    void Put(string[] ids);          // into the game's data, and shown
    void PutData(string[] ids);      // into the game's data only (the cabinet may be gone)
    string[] Saved(Activity a);      // null when the activity has no set of its own
    void Save(Activity a, string[] ids);
}

// A "Default" tab (the game's own outfit or tack) plus one tab per activity, and a Save button.
// Picking an activity tab loads its saved set (or Default's as a start) into the game's data so the
// game's own dressing works on it; Default is put back before the cabinet closes or the game saves.
internal sealed class OutfitTabs
{
    private static readonly List<OutfitTabs> all = new();
    private static readonly Color Selected = new(1f, 0.78f, 0.5f);

    private readonly Transform cabinet;
    private readonly TMP_Text heading;
    private readonly Button saveButton;
    private readonly TMP_Text saveLabel;
    private readonly GameObject closeButton;
    // Optional first tab that isn't an outfit set (the tack cabinet's "Halter"): enter/leave show its own page.
    private readonly GameObject extraTab;
    private readonly Image extraImage;
    private readonly Action enterExtra, leaveExtra;
    private bool extraOn;
    private readonly List<(Activity? activity, GameObject tab, Image image)> tabs = new();
    private IOutfitTarget target;
    private Activity? current;
    private string[] stash; // Default's set while an activity tab is shown
    private string headingText;
    private float savedUntil;

    public bool Alive => cabinet != null && saveButton != null;
    public IOutfitTarget Target => target;

    // arrangeTabs places the visible tabs (Default first), the Save button and, when close is given, a Close button.
    public OutfitTabs(Transform cabinet, TMP_Text heading, GameObject saveTemplate, GameObject tabTemplate,
        Action<List<RectTransform>, RectTransform, RectTransform> arrangeTabs, Action close = null,
        (string label, Action enter, Action leave)? extra = null)
    {
        this.cabinet = cabinet;
        this.heading = heading;
        this.arrangeTabs = arrangeTabs;

        var save = MakeButton(saveTemplate, cabinet, "WardrobeAndTackOverhaul Save", "Save", Save);
        saveButton = save.GetComponent<Button>();
        saveLabel = save.GetComponentInChildren<TMP_Text>(true);
        if (close != null)
            closeButton = MakeButton(saveTemplate, cabinet, "WardrobeAndTackOverhaul Close", "Close", close);

        if (extra != null)
        {
            enterExtra = extra.Value.enter;
            leaveExtra = extra.Value.leave;
            extraTab = MakeButton(tabTemplate, cabinet, "WardrobeAndTackOverhaul Tab " + extra.Value.label, extra.Value.label, EnterExtra);
            extraImage = extraTab.GetComponent<Image>();
        }
        foreach (var activity in new Activity?[] { null }.Concat(Activities.All.Select(a => (Activity?)a)))
        {
            var label = activity == null ? "Default" : Activities.Label(activity.Value);
            var tab = MakeButton(tabTemplate, cabinet, "WardrobeAndTackOverhaul Tab " + label, label, () => Select(activity));
            tabs.Add((activity, tab, tab.GetComponent<Image>()));
        }
        all.Add(this);
    }

    // Shown with Default selected; offered = the activity tabs for this outfit (null hides everything).
    public void Prepare(Activity[] offered, IOutfitTarget newTarget)
    {
        Restore();
        target = newTarget;
        // The game sets the heading after opening, so it is read when Default is left.
        headingText = null;
        var show = offered != null && newTarget != null;
        saveButton.gameObject.SetActive(show);
        if (closeButton != null)
            closeButton.SetActive(show);
        LeaveExtra();
        if (extraTab != null)
            extraTab.SetActive(show);
        foreach (var (activity, tab, _) in tabs)
            tab.SetActive(show && (activity == null || offered.Contains(activity.Value)));
        Layout();
        UpdateLook();
    }

    // Our buttons go when the cabinet closes; some of the game's screens keep the cabinet's parent visible.
    public void Hide()
    {
        LeaveExtra();
        if (extraTab != null)
            extraTab.SetActive(false);
        if (saveButton != null)
            saveButton.gameObject.SetActive(false);
        if (closeButton != null)
            closeButton.SetActive(false);
        foreach (var (_, tab, _) in tabs)
            if (tab != null)
                tab.SetActive(false);
    }

    // Default back into the game's data. Safe to call any time, also after the cabinet is gone.
    public void Restore()
    {
        // The Halter page has the tack off; it goes back on first.
        LeaveExtra();
        if (current == null)
        {
            UpdateLook();
            return;
        }
        try
        {
            if (stash != null && target != null)
            {
                if (Alive && target.Valid)
                    target.Put(stash);
                else
                    target.PutData(stash);
            }
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"Putting the default set back failed: {e}");
        }
        current = null;
        stash = null;
        UpdateLook();
    }

    public static void RestoreAll()
    {
        // Dead ones too: a cabinet destroyed on an activity tab must not leave that set in the game's data.
        foreach (var t in all)
            t.Restore();
        all.RemoveAll(t => !t.Alive);
    }

    // Called every frame by the cabinet: ends the "Saved" note.
    public void Tick()
    {
        if (savedUntil > 0 && Time.unscaledTime > savedUntil)
        {
            savedUntil = 0;
            if (saveLabel != null)
                saveLabel.text = "Save";
        }
    }

    private void Select(Activity? activity)
    {
        try
        {
            var wasExtra = extraOn;
            LeaveExtra();
            if (target == null || !target.Valid || (activity == current && !wasExtra))
            {
                UpdateLook();
                return;
            }
            if (current == null)
            {
                stash = target.Worn();
                if (heading != null)
                    headingText = heading.text;
            }
            var ids = activity == null ? stash : target.Saved(activity.Value) ?? stash;
            target.Put(ids);
            current = activity;
            if (activity == null)
                stash = null;
            UpdateLook();
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"Switching to the {activity?.ToString() ?? "default"} set failed: {e}");
        }
    }

    private void Save()
    {
        try
        {
            if (target == null || !target.Valid || extraOn)
                return;
            // Default is the game's own set, which the game keeps by itself.
            if (current != null)
                target.Save(current.Value, target.Worn());
            savedUntil = Time.unscaledTime + 1.5f;
            if (saveLabel != null)
                saveLabel.text = "Saved";
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"Saving the set failed: {e}");
        }
    }

    // Only the visible tabs are arranged, so there are no gaps.
    private readonly Action<List<RectTransform>, RectTransform, RectTransform> arrangeTabs;

    private void Layout()
    {
        var visible = tabs.Where(t => t.tab.activeSelf).Select(t => Rect(t.tab.transform)).ToList();
        if (extraTab != null && extraTab.activeSelf)
            visible.Insert(Math.Min(1, visible.Count), Rect(extraTab.transform)); // right under Default
        arrangeTabs(visible, Rect(saveButton.transform), closeButton != null ? Rect(closeButton.transform) : null);
    }

    // The extra page shows the game's own set underneath, so Default goes back first.
    private void EnterExtra()
    {
        try
        {
            if (extraOn)
                return;
            Restore();
            extraOn = true;
            enterExtra?.Invoke();
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"Opening the extra page failed: {e}");
        }
        UpdateLook();
    }

    private void LeaveExtra()
    {
        if (!extraOn)
            return;
        extraOn = false;
        try { leaveExtra?.Invoke(); }
        catch (Exception e) { Plugin.Logger.LogError($"Closing the extra page failed: {e}"); }
    }

    private void UpdateLook()
    {
        foreach (var (activity, _, image) in tabs)
            if (image != null)
                image.color = activity == current && !extraOn ? Selected : Color.white;
        if (extraImage != null)
            extraImage.color = extraOn ? Selected : Color.white;
        if (saveButton != null)
            saveButton.interactable = !extraOn;
        if (heading != null && headingText != null)
            heading.text = current == null ? headingText : $"{headingText}: {Activities.Label(current.Value)}";
    }

    // ---- building ----

    private static GameObject MakeButton(GameObject template, Transform parent, string name, string text, Action click)
    {
        var copy = Object.Instantiate(template, parent, false).Cast<GameObject>();
        copy.name = name;
        copy.SetActive(true);
        // LanguageUpdater would look our texts up as translation keys and show "Missing: ...".
        foreach (var updater in copy.GetComponentsInChildren<LanguageUpdater>(true))
        {
            updater.enabled = false;
            Object.Destroy(updater);
        }
        foreach (var tip in copy.GetComponentsInChildren<Tooltip>(true))
            Object.Destroy(tip);
        // The template's icon and Escape key aren't wanted on ours.
        foreach (var image in copy.GetComponentsInChildren<Image>(true))
            if (image.gameObject != copy)
                Object.Destroy(image.gameObject);
        foreach (var esc in copy.GetComponentsInChildren<ClickOnEscape>(true))
            Object.Destroy(esc);
        var rect = Rect(copy.transform);
        rect.pivot = new Vector2(0.5f, 0.5f);
        var label = copy.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
        {
            label.text = text;
            label.alignment = TextAlignmentOptions.Center;
            label.enableAutoSizing = true;
            label.fontSizeMax = label.fontSize;
            label.fontSizeMin = label.fontSize * 0.6f;
            var lr = Rect(label.transform);
            lr.anchorMin = Vector2.zero;
            lr.anchorMax = Vector2.one;
            lr.offsetMin = new Vector2(10, 0);
            lr.offsetMax = new Vector2(-10, 0);
        }
        var button = copy.GetComponent<Button>();
        button.onClick = new Button.ButtonClickedEvent();
        button.onClick.AddListener(new Action(click));
        return copy;
    }

    internal static Transform Deep(Transform parent, string name)
    {
        for (var i = 0; i < parent.childCount; i++)
        {
            var child = parent.GetChild(i);
            if (child.name == name)
                return child;
            var found = Deep(child, name);
            if (found != null)
                return found;
        }
        return null;
    }

    internal static RectTransform Rect(Transform t) => t.Cast<RectTransform>();
}
