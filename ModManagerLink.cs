using System.Runtime.CompilerServices;
using ModManager;

namespace WardrobeAndTackOverhaul;

// Our own page in the Mod Manager's window: the same "Enabled" switch as the config entry, but
// without the "Reset to defaults" button Mod Manager adds under config entries.
// Kept in its own class so ModManager.dll is only loaded when Mod Manager is installed.
internal static class ModManagerLink
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void Register() => ModSettings.Register(Plugin.Guid, Build, showConfigEntries: false);

    private static void Build(ISettingsPage page)
    {
        page.Toggle("Enabled", () => Plugin.Enabled.Value, on => Plugin.Enabled.Value = on);
        page.Hint(Plugin.Enabled.Description.Description);
    }
}
