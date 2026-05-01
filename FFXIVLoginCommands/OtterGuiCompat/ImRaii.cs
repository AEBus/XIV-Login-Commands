using Dalamud.Bindings.ImGui;

namespace OtterGui.Raii;

// Lightweight compatibility layer so this plugin can use OtterGui-style RAII calls
// without requiring the full OtterGui project in CI builds.
public static class ImRaii
{
    public static Dalamud.Interface.Utility.Raii.ImRaii.TabBarDisposable TabBar(ImU8String label)
        => Dalamud.Interface.Utility.Raii.ImRaii.TabBar(label);

    public static Dalamud.Interface.Utility.Raii.ImRaii.TabBarDisposable TabBar(ImU8String label, ImGuiTabBarFlags flags)
        => Dalamud.Interface.Utility.Raii.ImRaii.TabBar(label, flags);

    public static Dalamud.Interface.Utility.Raii.ImRaii.TabItemDisposable TabItem(ImU8String label)
        => Dalamud.Interface.Utility.Raii.ImRaii.TabItem(label);

    public static Dalamud.Interface.Utility.Raii.ImRaii.TableDisposable Table(ImU8String table, int numColumns, ImGuiTableFlags flags)
        => Dalamud.Interface.Utility.Raii.ImRaii.Table(table, numColumns, flags);

    public static Dalamud.Interface.Utility.Raii.ImRaii.IdDisposable PushId(int id, bool enabled = true)
        => Dalamud.Interface.Utility.Raii.ImRaii.PushId(id, enabled);
}
