# Avalonia 12.0.3 Window & Popup Controls Reference

## 1. TopLevel

```csharp
// Location: src/Avalonia.Controls/TopLevel.cs
public abstract class TopLevel : Control, ITopLevel
{
    // Application reference
    public Application? Application { get; }
    
    // Window reference
    public Window? Window { get; }
    
    // Popup reference
    public Popup? Popup { get; }
    
    // Sizing
    public double Width { get; set; }
    public double Height { get; set; }
    public double MinWidth { get; set; }
    public double MinHeight { get; set; }
    public double MaxWidth { get; set; }
    public double MaxHeight { get; set; }
    
    // Position
    public double Left { get; set; }
    public double Top { get; set; }
    
    // Focus
    public IInputElement? FocusManager { get; }
    public IInputElement? GetFocusManager();
    public void SetFocusManager(IInputElement? focusManager);
    
    // Screens
    public IScreens Screens { get; }
    
    // Effective viewport
    public Rect EffectiveViewport { get; }
    public event EventHandler<EffectiveViewportChangedEventArgs>? EffectiveViewportChanged;
    
    // Window resizing
    public event EventHandler<WindowResizedEventArgs>? Resized;
    
    // Methods
    public abstract void Show();
    public abstract void Hide();
    public abstract void Close();
}
```

## 2. Window

```csharp
// Location: src/Avalonia.Controls/Window.cs
public class Window : TopLevel, IWindow
{
    // Title
    public string? Title { get; set; }
    
    // Window state
    public WindowState WindowState { get; set; }
    public WindowStartupLocation WindowStartupLocation { get; set; }
    public WindowTransparencyLevel WindowTransparencyLevel { get; set; }
    
    // Icon
    public WindowIcon? Icon { get; set; }
    
    // Owner
    public Window? Owner { get; set; }
    
    // Resize
    public bool CanResize { get; set; }
    public ResizeMode ResizeMode { get; set; }
    public bool CanMinimize { get; set; }
    public bool CanMaximize { get; set; }
    
    // Edge
    public WindowEdge Edge { get; }
    
    // Events
    public event EventHandler<WindowClosingEventArgs>? Closing;
    public event EventHandler<WindowClosedEventArgs>? Closed;
    public event EventHandler<WindowOpenedEventArgs>? Opened;
    public event EventHandler<WindowResizedEventArgs>? Resized;
    
    // Methods
    public void Show();
    public void Show(Window owner);
    public void ShowDialog(Window owner);
    public void Hide();
    public void Close();
    public void Activate();
    public void Minimize();
    public void Maximize();
    public void Restore();
}
```

## 3. WindowBase

```csharp
// Location: src/Avalonia.Controls/WindowBase.cs
public abstract class WindowBase : TopLevel
{
    public abstract string? Title { get; set; }
    public abstract WindowState WindowState { get; set; }
    public abstract WindowStartupLocation WindowStartupLocation { get; set; }
    public abstract WindowTransparencyLevel WindowTransparencyLevel { get; set; }
    public abstract WindowIcon? Icon { get; set; }
    public abstract Window? Owner { get; set; }
    public abstract bool CanResize { get; set; }
    public abstract ResizeMode ResizeMode { get; set; }
    public abstract bool CanMinimize { get; set; }
    public abstract bool CanMaximize { get; set; }
    public abstract WindowEdge Edge { get; }
    
    public abstract void Show();
    public abstract void Show(Window owner);
    public abstract void ShowDialog(Window owner);
    public abstract void Hide();
    public abstract void Close();
    public abstract void Activate();
    public abstract void Minimize();
    public abstract void Maximize();
    public abstract void Restore();
}
```

## 4. WindowState

```csharp
// Location: src/Avalonia.Controls/WindowState.cs
public enum WindowState
{
    Normal,
    Minimized,
    Maximized,
}
```

## 5. WindowStartupLocation

```csharp
// Location: src/Avalonia.Controls/WindowStartupLocation.cs
public enum WindowStartupLocation
{
    Manual,
    CenterOwner,
    CenterScreen,
}
```

## 6. WindowTransparencyLevel

```csharp
// Location: src/Avalonia.Controls/WindowTransparencyLevel.cs
public enum WindowTransparencyLevel
{
    Transparent,
    Acrylic,
    Mica,
    BackgroundBlur,
    TabbedBlur,
}
```

## 7. WindowEdge

```csharp
// Location: src/Avalonia.Controls/WindowEdge.cs
public enum WindowEdge
{
    None,
    Left,
    Right,
    Top,
    Bottom,
    TopLeft,
    TopRight,
    BottomLeft,
    BottomRight,
}
```

## 8. WindowIcon

```csharp
// Location: src/Avalonia.Controls/WindowIcon.cs
public class WindowIcon
{
    public Bitmap? Source { get; set; }
    public Uri? Uri { get; set; }
    public string? Path { get; set; }
}
```

## 9. WindowClosingEventArgs

```csharp
// Location: src/Avalonia.Controls/WindowClosingEventArgs.cs
public class WindowClosingEventArgs : CancelEventArgs
{
    public Window Window { get; }
    public bool Handled { get; set; }
}
```

## 10. WindowClosedEventArgs

```csharp
// Location: src/Avalonia.Controls/WindowClosedEventArgs.cs
public class WindowClosedEventArgs : EventArgs
{
    public Window Window { get; }
}
```

## 11. WindowOpenedEventArgs

```csharp
// Location: src/Avalonia.Controls/WindowOpenedEventArgs.cs
public class WindowOpenedEventArgs : EventArgs
{
    public Window Window { get; }
}
```

## 12. WindowResizedEventArgs

```csharp
// Location: src/Avalonia.Controls/WindowResizedEventArgs.cs
public class WindowResizedEventArgs : EventArgs
{
    public Window Window { get; }
    public Rect Bounds { get; }
    public Size Size { get; }
}
```

## 13. Popup

```csharp
// Location: src/Avalonia.Controls/Popup.cs
public class Popup : TopLevel, IPopup
{
    // Content
    public UIElement? Child { get; set; }
    
    // Placement
    public PlacementMode PlacementMode { get; set; }
    public UIElement? PlacementTarget { get; set; }
    public double HorizontalOffset { get; set; }
    public double VerticalOffset { get; set; }
    public double MinWidth { get; set; }
    public double MinHeight { get; set; }
    public double MaxWidth { get; set; }
    public double MaxHeight { get; set; }
    public HorizontalAlignment HorizontalAlignment { get; set; }
    public VerticalAlignment VerticalAlignment { get; set; }
    public bool StaysOpen { get; set; }
    public bool AllowsInteraction { get; set; }
    
    // Events
    public event EventHandler<PopupOpenedEventArgs>? Opened;
    public event EventHandler<PopupClosedEventArgs>? Closed;
    public event EventHandler<PopupOpenedEventArgs>? Opening;
    public event EventHandler<PopupClosedEventArgs>? Closing;
    
    // Methods
    public void Open();
    public void Close();
}

public enum PlacementMode
{
    MousePoint,
    MousePosition,
    Mouse,
    Absolute,
    Relative,
    RelativePoint,
    Center,
    TargetRect,
}
```

## 14. Flyout

```csharp
// Location: src/Avalonia.Controls/Flyout.cs
public class Flyout : ContentControl
{
    public object? Content { get; set; }
    public DataTemplate? ContentTemplate { get; set; }
    public FlyoutPlacementMode Placement { get; set; }
    public bool IsOpen { get; set; }
    public bool StaysOpen { get; set; }
    public bool IsLightDismissEnabled { get; set; }
    
    public event EventHandler<FlyoutOpenedEventArgs>? Opened;
    public event EventHandler<FlyoutClosedEventArgs>? Closed;
}

public enum FlyoutPlacementMode
{
    Top,
    Bottom,
    Left,
    Right,
    Full,
}
```

## 15. FlyoutBase

```csharp
// Location: src/Avalonia.Controls/FlyoutBase.cs
public abstract class FlyoutBase
{
    public static Flyout? GetFlyout(UIElement element);
    public static void SetFlyout(UIElement element, Flyout? value);
    public static Flyout? GetMenuFlyout(UIElement element);
    public static void SetMenuFlyout(UIElement element, Flyout? value);
}
```

## 16. OverlayPopup

```csharp
// Location: src/Avalonia.Controls/OverlayPopup.cs
public class OverlayPopup : ContentControl
{
    public bool IsOpen { get; set; }
    public object? Content { get; set; }
    public DataTemplate? ContentTemplate { get; set; }
    public bool StaysOpen { get; set; }
    public bool IsLightDismissEnabled { get; set; }
    
    public event EventHandler<OverlayPopupOpenedEventArgs>? Opened;
    public event EventHandler<OverlayPopupClosedEventArgs>? Closed;
}
```

## 17. ItemPicker

```csharp
// Location: src/Avalonia.Controls/ItemPicker.cs
public class ItemPicker : ContentControl
{
    public IList Items { get; }
    public object? SelectedItem { get; set; }
    public int SelectedIndex { get; set; }
    public bool IsOpen { get; set; }
    public DataTemplate? ItemTemplate { get; set; }
    public DataTemplateSelector? ItemTemplateSelector { get; set; }
    
    public event EventHandler<ItemPickerOpenedEventArgs>? Opened;
    public event EventHandler<ItemPickerClosedEventArgs>? Closed;
}
```

## 18. NativeMenu

```csharp
// Location: src/Avalonia.Controls/NativeMenu.cs
public class NativeMenu
{
    public IList<NativeMenuItem> Items { get; }
    public static NativeMenu? GetNativeMenu(UIElement element);
    public static void SetNativeMenu(UIElement element, NativeMenu? value);
}

public abstract class NativeMenuItem
{
    public string? Header { get; set; }
    public string? InputGesture { get; set; }
    public NativeMenuItemType Type { get; }
}

public class NativeMenuItemSeparator : NativeMenuItem { }

public class NativeMenuItemBase : NativeMenuItem
{
    public NativeMenu? Submenu { get; set; }
    public ICommand? Command { get; set; }
    public object? CommandParameter { get; set; }
    public bool IsChecked { get; set; }
    public bool IsVisible { get; set; }
}

public class NativeMenuBar
{
    public IList<NativeMenuItem> Items { get; }
}

public class NativeMenuBarPresenter : ContentControl
{
    public NativeMenu? Menu { get; set; }
}
```

## 19. TrayIcon

```csharp
// Location: src/Avalonia.Controls/TrayIcon.cs
public class TrayIcon : ContentControl
{
    public Bitmap? Icon { get; set; }
    public string? ToolTip { get; set; }
    public Flyout? Flyout { get; set; }
    public NativeMenu? Menu { get; set; }
    
    public event EventHandler<TrayIconClickedEventArgs>? Clicked;
}
```

## 20. TopLevelHost

```csharp
// Location: src/Avalonia.Controls/TopLevelHost.cs
public class TopLevelHost : ContentControl
{
    public static readonly AttachedProperty<ITopLevel?> TopLevelProperty;
    public static ITopLevel? GetTopLevel(UIElement element);
    public static void SetTopLevel(UIElement element, ITopLevel? value);
    
    public IList<ITopLevel> TopLevels { get; }
    
    protected override void OnApplyTemplate(ApplyTemplateEventArgs e);
}

public static class TopLevelHostExtensions
{
    public static void Show(this ITopLevel topLevel, Window? owner = null);
    public static void Hide(this ITopLevel topLevel);
    public static void Close(this ITopLevel topLevel);
}
```

## 21. Key Files Reference

| File | Purpose |
|------|---------|
| `src/Avalonia.Controls/TopLevel.cs` | TopLevel base |
| `src/Avalonia.Controls/Window.cs` | Window |
| `src/Avalonia.Controls/WindowBase.cs` | WindowBase |
| `src/Avalonia.Controls/WindowState.cs` | WindowState |
| `src/Avalonia.Controls/WindowStartupLocation.cs` | WindowStartupLocation |
| `src/Avalonia.Controls/WindowTransparencyLevel.cs` | WindowTransparencyLevel |
| `src/Avalonia.Controls/WindowEdge.cs` | WindowEdge |
| `src/Avalonia.Controls/WindowIcon.cs` | WindowIcon |
| `src/Avalonia.Controls/WindowClosingEventArgs.cs` | WindowClosingEventArgs |
| `src/Avalonia.Controls/WindowClosedEventArgs.cs` | WindowClosedEventArgs |
| `src/Avalonia.Controls/WindowOpenedEventArgs.cs` | WindowOpenedEventArgs |
| `src/Avalonia.Controls/WindowResizedEventArgs.cs` | WindowResizedEventArgs |
| `src/Avalonia.Controls/Popup.cs` | Popup |
| `src/Avalonia.Controls/Flyout.cs` | Flyout |
| `src/Avalonia.Controls/FlyoutBase.cs` | FlyoutBase |
| `src/Avalonia.Controls/OverlayPopup.cs` | OverlayPopup |
| `src/Avalonia.Controls/ItemPicker.cs` | ItemPicker |
| `src/Avalonia.Controls/NativeMenu.cs` | NativeMenu |
| `src/Avalonia.Controls/TrayIcon.cs` | TrayIcon |
| `src/Avalonia.Controls/TopLevelHost.cs` | TopLevelHost |