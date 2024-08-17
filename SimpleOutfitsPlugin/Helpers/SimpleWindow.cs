using Dalamud.Interface.Windowing;
using ImGuiNET;

namespace SimpleOutfitsPlugin.Helpers;

public abstract class SimpleWindow : Window {
    private readonly string _windowId;
    private string _windowTitle;

    private bool isCollapsed;
    

    public string Title {
        get => _windowTitle;
        set {
            _windowTitle = value;
            WindowName = $"{_windowTitle}###{_windowId}";
        }
    }

    protected SimpleWindow(string id, string title = "", ImGuiWindowFlags flags = ImGuiWindowFlags.None, bool forceMainWindow = false) : base($"{title}###{id}", flags, forceMainWindow) {
        _windowTitle = title;
        _windowId = id;
    }

    public void UncollapseOrToggle() {
        if (isCollapsed) {
            isCollapsed = false;
            Collapsed = false;
            IsOpen = true;
        } else {
            Toggle();
        }
    }

    public override void Update() {
        isCollapsed = true;
    }

    public sealed override void Draw() {
        isCollapsed = false;
        Collapsed = null;
        DrawContents();
    }

    public abstract void DrawContents();

}
