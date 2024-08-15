using Dalamud.Interface.Windowing;
using ImGuiNET;

namespace SimpleOutfits.Helpers;

public abstract class SimpleWindow : Window {
    private string windowId;
    private string windowTitle;

    public string Title {
        get => windowTitle;
        set {
            windowTitle = value;
            WindowName = $"{windowTitle}###{windowId}";
        }
    }

    protected SimpleWindow(string id, string title = "", ImGuiWindowFlags flags = ImGuiWindowFlags.None, bool forceMainWindow = false) : base($"{title}###{id}", flags, forceMainWindow) {
        windowTitle = title;
        windowId = id;
    }
}
