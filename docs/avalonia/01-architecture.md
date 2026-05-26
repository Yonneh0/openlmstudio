# Avalonia 12.0.3 Architecture Reference

## 1. Type Hierarchy

Avalonia uses a layered class hierarchy that mirrors WPF's model but with some differences.

```
AvaloniaObject                    // Base class with property support (WPF's DependencyObject)
├── Animatable                    // Animation support
│   ├── StyledElement             // DataContext, Classes, Styles, Resources, Theme
│   │   ├── Visual                // Visual tree, composition
│   │   │   ├── Control           // ControlTemplate, TemplateApplied
│   │   │   │   ├── ContentControl    // Single content child
│   │   │   │   │   ├── TextBlock
│   │   │   │   │   ├── TextBox
│   │   │   │   │   ├── PasswordBox
│   │   │   │   │   ├── Image
│   │   │   │   │   ├── Label
│   │   │   │   │   ├── Expander
│   │   │   │   │   ├── ProgressBar
│   │   │   │   │   ├── ProgressRing
│   │   │   │   │   ├── LayoutTransformControl
│   │   │   │   │   ├── TransitioningContentControl
│   │   │   │   │   ├── FlipView
│   │   │   │   │   ├── InfoBanner
│   │   │   │   │   ├── SelectableTextBlock
│   │   │   │   │   └── AvaloniaEditor
│   │   │   │   ├── ItemsControl    // Items / ItemsSource
│   │   │   │   │   ├── ListBox
│   │   │   │   │   ├── ListBoxItem
│   │   │   │   │   ├── ListView
│   │   │   │   │   ├── TreeView
│   │   │   │   │   ├── TreeViewItem
│   │   │   │   │   ├── TabControl
│   │   │   │   │   ├── TabItem
│   │   │   │   │   ├── Menu
│   │   │   │   │   ├── MenuItem
│   │   │   │   │   ├── Carousel
│   │   │   │   │   ├── MenuBase
│   │   │   │   │   ├── HeaderedItemsControl
│   │   │   │   │   └── ItemsPresenter
│   │   │   │   ├── Panel           // Multiple children
│   │   │   │   │   ├── Grid
│   │   │   │   │   ├── StackPanel
│   │   │   │   │   ├── WrapPanel
│   │   │   │   │   ├── DockPanel
│   │   │   │   │   ├── UniformGrid
│   │   │   │   │   ├── RelativePanel
│   │   │   │   │   ├── Canvas
│   │   │   │   │   ├── Viewbox
│   │   │   │   │   ├── Viewbox2D
│   │   │   │   │   ├── Viewbox2DWithScrolling
│   │   │   │   │   ├── Border
│   │   │   │   │   ├── ScrollViewer
│   │   │   │   │   ├── VirtualizingStackPanel
│   │   │   │   │   ├── VirtualizingCarouselPanel
│   │   │   │   │   ├── GridSplitter
│   │   │   │   │   └── LayoutTransformControl
│   │   │   │   ├── Decorator       // Single child, no layout
│   │   │   │   │   ├── BoxView
│   │   │   │   │   ├── IconElement
│   │   │   │   │   ├── PathIcon
│   │   │   │   │   └── ExperimentalAcrylicBorder
│   │   │   │   ├── Shape           // Vector drawing
│   │   │   │   │   ├── Rectangle
│   │   │   │   │   ├── Ellipse
│   │   │   │   │   ├── Line
│   │   │   │   │   ├── Polygon
│   │   │   │   │   └── Polyline
│   │   │   │   ├── ButtonBase
│   │   │   │   │   ├── Button
│   │   │   │   │   ├── RepeatButton
│   │   │   │   │   └── ToggleButton
│   │   │   │   ├── ToggleButtonBase
│   │   │   │   │   ├── CheckBox
│   │   │   │   │   ├── RadioButton
│   │   │   │   │   └── ToggleButton
│   │   │   │   ├── ComboBox
│   │   │   │   ├── ComboBoxItem
│   │   │   │   ├── Slider
│   │   │   │   ├── RangeSlider
│   │   │   │   ├── Rating
│   │   │   │   ├── RatingItem
│   │   │   │   ├── NumericUpDown
│   │   │   │   ├── DecimalUpDown
│   │   │   │   ├── DecimalNumPicker
│   │   │   │   ├── MaskedTextBox
│   │   │   │   ├── Calendar
│   │   │   │   ├── DatePicker
│   │   │   │   ├── TimePicker
│   │   │   │   ├── CalendarDatePicker
│   │   │   │   ├── DateTimePicker
│   │   │   │   ├── TickBar
│   │   │   │   ├── ButtonSpinner
│   │   │   │   ├── DropDownButton
│   │   │   │   ├── SplitButton
│   │   │   │   ├── GroupBox
│   │   │   │   ├── HyperlinkButton
│   │   │   │   ├── Label
│   │   │   │   └── Separator
│   │   │   ├── TopLevel            // Application, Window, Popup
│   │   │   │   ├── Window
│   │   │   │   ├── Popup
│   │   │   │   ├── Flyout
│   │   │   │   ├── FlyoutBase
│   │   │   │   ├── OverlayPopup
│   │   │   │   ├── ItemPicker
│   │   │   │   ├── NativeMenu
│   │   │   │   ├── NativeMenuItem
│   │   │   │   ├── NativeMenuBar
│   │   │   │   ├── NativeMenuBarPresenter
│   │   │   │   ├── TrayIcon
│   │   │   │   ├── TopLevelHost
│   │   │   │   └── TopLevelHost.Decorations
│   │   │   └── UIElement           // Input events
│   │   └── StyledElementExtensions // Fluent property access
│   └── Animatable
└── AvaloniaObject
```

## 2. AvaloniaObject (Base Class)

The foundation of Avalonia's property system, analogous to WPF's `DependencyObject`.

```csharp
// Location: src/Avalonia.Base/AvaloniaObject.cs
public class AvaloniaObject : IAvaloniaObjectDebug, INotifyPropertyChanged
{
    // Core methods
    public object? GetValue(AvaloniaProperty property);
    public T GetValue<T>(StyledProperty<T> property);
    public T GetValue<T>(DirectPropertyBase<T> property);
    public void SetValue(AvaloniaProperty property, object? value, BindingPriority priority = BindingPriority.LocalValue);
    public void SetValue<T>(StyledProperty<T> property, T value, BindingPriority priority = BindingPriority.LocalValue);
    public void SetValue<T>(DirectPropertyBase<T> property, T value);
    public void SetCurrentValue(AvaloniaProperty property, object? value);
    public void SetCurrentValue<T>(StyledProperty<T> property, T value);
    public void ClearValue(AvaloniaProperty property);
    public void ClearValue<T>(StyledProperty<T> property);
    public void ClearValue<T>(DirectPropertyBase<T> property);
    public void CoerceValue(AvaloniaProperty property);
    public bool IsSet(AvaloniaProperty property);
    public bool IsAnimating(AvaloniaProperty property);
    public Optional<T> GetBaseValue<T>(StyledProperty<T> property);
    
    // Binding
    public BindingExpressionBase Bind(AvaloniaProperty property, BindingBase binding);
    public IDisposable Bind(AvaloniaProperty property, IObservable<object?> source, BindingPriority priority = BindingPriority.LocalValue);
    public IDisposable Bind<T>(StyledProperty<T> property, IObservable<object?> source, BindingPriority priority = BindingPriority.LocalValue);
    public IDisposable Bind<T>(StyledProperty<T> property, IObservable<T> source, BindingPriority priority = BindingPriority.LocalValue);
    public IDisposable Bind<T>(StyledProperty<T> property, IObservable<BindingValue<T>> source, BindingPriority priority = BindingPriority.LocalValue);
    public IDisposable Bind<T>(DirectPropertyBase<T> property, IObservable<object?> source);
    public IDisposable Bind<T>(DirectPropertyBase<T> property, IObservable<T> source);
    public IDisposable Bind<T>(DirectPropertyBase<T> property, IObservable<BindingValue<T>> source);
    
    // Indexer access
    public object? this[AvaloniaProperty property] { get; set; }
    public BindingBase this[IndexerDescriptor binding] { get; set; }
    
    // Dispatcher / threading
    public Dispatcher Dispatcher { get; }
    public bool CheckAccess();
    public void VerifyAccess();
    
    // Inheritance
    protected AvaloniaObject? InheritanceParent { get; set; }
    
    // Events
    public event EventHandler<AvaloniaPropertyChangedEventArgs>? PropertyChanged;
    event PropertyChangedEventHandler? INotifyPropertyChanged.PropertyChanged;
    
    // Internal
    protected virtual void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change);
    protected virtual void UpdateDataValidation(AvaloniaProperty property, BindingValueType state, Exception? error);
    protected bool SetAndRaise<T>(DirectPropertyBase<T> property, ref T field, T value);
}
```

## 3. Animatable

Adds animation support to `AvaloniaObject`.

```csharp
// Location: src/Avalonia.Base/Animation/
public class Animatable : AvaloniaObject
{
    // Start animation
    public void BeginAnimation(AvaloniaProperty property, IAnimation animation);
    public void BeginAnimation<T>(StyledProperty<T> property, IAnimation animation);
    public void BeginAnimation<T>(DirectPropertyBase<T> property, IAnimation animation);
    
    // Stop animation
    public void StopAnimation(AvaloniaProperty property);
    public void StopAnimations();
    
    // Check animation state
    public bool IsAnimating(AvaloniaProperty property);
}
```

## 4. StyledElement

Adds data context, classes, styles, resources, and theme support.

```csharp
// Location: src/Avalonia.Base/StyledElement.cs
public class StyledElement : Animatable, 
    IDataContextProvider, ILogical, IThemeVariantHost, 
    IResourceHost, IStyleHost, ISetLogicalParent, 
    ISetInheritanceParent, ISupportInitialize, INamed
{
    // Properties
    public string? Name { get; set; }
    public Classes Classes { get; }
    public object? DataContext { get; set; }
    public bool IsInitialized { get; }
    public Styles Styles { get; }
    public Type StyleKey { get; }
    protected virtual Type StyleKeyOverride { get; }
    public IResourceDictionary Resources { get; set; }
    public AvaloniaObject? TemplatedParent { get; }
    public ControlTheme? Theme { get; set; }
    public ThemeVariant ActualThemeVariant { get; }
    protected internal IAvaloniaList<ILogical> LogicalChildren { get; }
    public StyledElement? Parent { get; }
    protected IPseudoClasses PseudoClasses { get; }
    
    // ILogical
    bool ILogical.IsAttachedToLogicalTree { get; }
    ILogical? ILogical.LogicalParent { get; }
    IAvaloniaReadOnlyList<ILogical> ILogical.LogicalChildren { get; }
    
    // IResourceHost
    bool IResourceNode.HasResources { get; }
    bool IResourceHost.TryGetResource(object key, ThemeVariant? theme, out object? value);
    
    // IStyleHost
    bool IStyleHost.IsStylesInitialized { get; }
    IStyleHost? IStyleHost.StylingParent { get; }
    
    // ISupportInitialize
    void BeginInit();
    void EndInit();
    
    // Events
    public event EventHandler<LogicalTreeAttachmentEventArgs>? AttachedToLogicalTree;
    public event EventHandler<LogicalTreeAttachmentEventArgs>? DetachedFromLogicalTree;
    public event EventHandler? DataContextChanged;
    public event EventHandler? Initialized;
    public event EventHandler<ResourcesChangedEventArgs>? ResourcesChanged;
    public event EventHandler? ActualThemeVariantChanged;
    
    // Methods
    public bool ApplyStyling();
    protected virtual void OnAttachedToLogicalTree(LogicalTreeAttachmentEventArgs e);
    protected virtual void OnDetachedFromLogicalTree(LogicalTreeAttachmentEventArgs e);
    protected virtual void OnDataContextChanged(EventArgs e);
    protected virtual void OnInitialized();
}
```

## 5. Visual

Adds visual tree and composition support.

```csharp
// Location: src/Avalonia.Base/Visual.cs
public class Visual : StyledElement
{
    // Visual tree
    public IReadOnlyList<Visual> VisualChildren { get; }
    public Visual? Parent { get; }
    
    // Composition
    public CompositionTarget? CompositionTarget { get; }
    public VisualLayerManager? VisualLayerManager { get; }
    
    // Methods
    public Visual? FindParent<T>() where T : Visual;
    public IEnumerable<Visual> GetVisualAncestors();
    public IEnumerable<Visual> GetVisualDescendants();
    public Visual? GetVisualParent();
    public void AddVisualChild(Visual child);
    public void RemoveVisualChild(Visual child);
    
    // Rendering
    protected virtual void OnCompositionTargetRendering(object? sender, EventArgs e);
    protected virtual void OnRender(RenderingContext context);
}
```

## 6. Control

The base class for lookless controls with templates.

```csharp
// Location: src/Avalonia.Controls/Control.cs
public class Control : Visual, IControl
{
    // Control template
    public ControlTemplate? Template { get; set; }
    public ControlTemplate? TemplateWithoutContent { get; }
    public string? TemplateKey { get; }
    
    // Events
    public event EventHandler<ControlTemplateAppliedEventArgs>? TemplateApplied;
    
    // Methods
    protected virtual void OnApplyTemplate(ApplyTemplateEventArgs e);
    protected virtual void OnTemplateChanged(ControlTemplate? oldTemplate, ControlTemplate? newTemplate);
    protected virtual void OnLoaded();
    protected virtual void OnUnloaded();
}
```

## 7. ContentControl

A control with single content.

```csharp
// Location: src/Avalonia.Controls/ContentControl.cs
public class ContentControl : Control, IContentControl
{
    public object? Content { get; set; }
    public DataTemplate? ContentTemplate { get; set; }
    public DataTemplateSelector? ContentTemplateSelector { get; set; }
    public object? ContentTemplateContext { get; set; }
    
    protected override void OnApplyTemplate(ApplyTemplateEventArgs e);
    protected virtual void OnContentChanged(object? oldValue, object? newValue);
}
```

## 8. ItemsControl

A control with a collection of items.

```csharp
// Location: src/Avalonia.Controls/ItemsControl.cs
public class ItemsControl : ContentControl, ISelectable, IHeadered
{
    // Items
    public object? Items { get; set; }
    public IList? ItemsSource { get; set; }
    public DataTemplate? ItemTemplate { get; set; }
    public DataTemplateSelector? ItemTemplateSelector { get; set; }
    public ItemsSourceView? ItemsSourceView { get; }
    
    // Selection
    public SelectionMode SelectionMode { get; set; }
    public IList SelectedItems { get; }
    public int SelectedIndex { get; set; }
    public object? SelectedItem { get; set; }
    
    // Item container
    public ItemContainerGenerator ItemContainerGenerator { get; }
    
    // Events
    public event EventHandler<SelectionChangedEventArgs>? SelectionChanged;
    public event EventHandler<ContainerPreparedEventArgs>? ContainerPrepared;
    public event EventHandler<ContainerIndexChangedEventArgs>? ContainerIndexChanged;
    public event EventHandler<ContainerClearingEventArgs>? ContainerClearing;
    
    protected override void OnApplyTemplate(ApplyTemplateEventArgs e);
    protected virtual void OnItemsChanged(object? change);
    protected virtual void OnSelectionChanged(SelectionChangedEventArgs e);
    protected virtual void OnLoaded();
    protected virtual void OnUnloaded();
}
```

## 9. Panel

A control with multiple children that manages layout.

```csharp
// Location: src/Avalonia.Base/Layout/
public abstract class Panel : Decorator, ILayoutManager
{
    // Children
    public PanelChildren Children { get; }
    
    // Layout methods
    protected abstract Size MeasureOverride(Size availableSize);
    protected abstract Size ArrangeOverride(Size finalSize);
    
    // Layout events
    public event EventHandler<LayoutEventArgs>? LayoutUpdated;
    
    // Methods
    public void InvalidateMeasure();
    public Size Measure();
    public Size Arrange(Rect rect);
}
```

## 10. TopLevel

The base for top-level visual elements (Window, Popup, etc.).

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

## 11. Window

A top-level window.

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

## 12. Popup

A floating popup window.

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
```

## 13. Application Lifecycle

```csharp
// Location: src/Avalonia.Controls/Application.cs
public class Application : StyledElement, IApplication
{
    // Singleton access
    public static Application? Current { get; }
    public static Application CurrentInstance => Current ?? throw new InvalidOperationException("No Application set.");
    
    // Configuration
    public string? Name { get; set; }
    public Styles Styles { get; }
    public IResourceDictionary? Resources { get; set; }
    public IGlobalDataTemplates? DataTemplates { get; }
    
    // Startup
    public static Application Current => CurrentInstance;
    public void Run();
    public void Run(Application app);
    
    // Events
    public event EventHandler<StartupEventArgs>? Startup;
    public event EventHandler<ExitEventArgs>? Exit;
    public event EventHandler<Exception>? UnhandledException;
    
    // Methods
    public void Initialize();
    public void Shutdown();
    public void Shutdown(int exitCode);
}
```

## 14. Dispatcher System

```csharp
// Location: src/Avalonia.Base/Threading/
public class Dispatcher : IDispatcher
{
    // Static access
    public static Dispatcher? CurrentDispatcher { get; }
    public static Dispatcher UIThread { get; }
    
    // Priority
    public enum Priority
    {
        Background = 0,
        Input = 1,
        Render = 2,
        Timer = 3,
        ContextIdle = 4,
        DataBinding = 5,
        Layout = 6,
        LayoutUpdated = 7,
        Rendering = 8,
        Send = 9,
    }
    
    // Scheduling
    public IDisposable Post(Action action, Priority priority = Priority.Background);
    public IDisposable Post<T>(Action<T> action, T arg, Priority priority = Priority.Background);
    public Task InvokeAsync(Action action, Priority priority = Priority.Background);
    public Task InvokeAsync<T>(Action<T> action, T arg, Priority priority = Priority.Background);
    public T Invoke<T>(Func<T> func, Priority priority = Priority.Background);
    public T Invoke<T, TArg>(Func<TArg, T> func, TArg arg, Priority priority = Priority.Background);
    
    // Threading
    public bool CheckAccess();
    public void VerifyAccess();
    public DispatcherOperation Create(Action action, Priority priority = Priority.Background);
    public DispatcherOperation Create<T>(Action<T> action, T arg, Priority priority = Priority.Background);
    
    // Cancellation
    public void Cancel(DispatcherOperation operation);
    public void CancelAll();
}
```

## 15. Visual Tree vs Logical Tree

### Visual Tree
- Represents the visual rendering hierarchy
- Contains all visual elements
- Traversed via `Visual.GetVisualParent()`, `Visual.GetVisualDescendants()`, `Visual.GetVisualAncestors()`
- Used for rendering and composition

### Logical Tree
- Represents the structural/semantic hierarchy
- Contains logical elements (controls, data templates, etc.)
- Traversed via `ILogical.LogicalParent`, `ILogical.LogicalChildren`
- Used for data binding, styling, and resource lookup

```csharp
// Key interfaces
public interface ILogical
{
    ILogical? LogicalParent { get; }
    IAvaloniaReadOnlyList<ILogical> LogicalChildren { get; }
    bool IsAttachedToLogicalTree { get; }
}

public interface ILogicalRoot : ILogical
{
    ILogicalRoot? LogicalRoot { get; }
}

// Extension methods
public static class LogicalTreeExtensions
{
    public static T? GetLogicalParent<T>(this ILogical element) where T : ILogical;
    public static IEnumerable<ILogical> GetLogicalChildren(this ILogical element);
    public static IEnumerable<ILogical> GetLogicalDescendants(this ILogical element);
    public static ILogical? FindLogicalParent(this ILogical element, Type type);
    public static ILogical? FindLogicalParent<T>(this ILogical element);
    public static ILogical? FindLogicalParent(this ILogical element, Func<ILogical, bool> predicate);
    public static ILogical? FindLogicalParent(this ILogical element, string name);
    public static ILogical? FindLogicalChild(this ILogical element, string name);
    public static ILogical? FindLogicalAncestor(this ILogical element, Type type);
    public static ILogical? FindLogicalAncestor<T>(this ILogical element);
    public static ILogical? FindLogicalAncestor(this ILogical element, Func<ILogical, bool> predicate);
    public static ILogical? FindLogicalAncestor(this ILogical element, string name);
}
```

## 16. IGlobalDataTemplates

```csharp
// Location: src/Avalonia.Controls/IGlobalDataTemplates.cs
public interface IGlobalDataTemplates : IEnumerable<IDataTemplate>, IEnumerable
{
    IDataTemplate? this[object item] { get; }
    bool TryGet(object item, out IDataTemplate? template);
    void Add(IDataTemplate template);
    void Remove(IDataTemplate template);
    void Clear();
}

// Global data templates registry
public interface IGlobalDataTemplatesRegistry
{
    IGlobalDataTemplates GlobalDataTemplates { get; }
}
```

## 17. IStyleable

```csharp
// Location: src/Avalonia.Base/Styling/
public interface IStyleable
{
    Type StyleKey { get; }
    Type StyleKeyOverride { get; }
}

public interface ISupportsNestedStyle<T> where T : IStyle
{
    IReadOnlyList<T> Children { get; }
}

public interface IAttachedObject
{
    object? AttachedObject { get; }
    void OnAttached(object? attachedObject);
    void OnDetached(object? attachedObject);
}
```

## 18. Platform Abstraction Layer

```csharp
// Location: src/Avalonia.Base/Platform/
public interface IPlatform
{
    IPlatformGraphics Graphics { get; }
    IPlatformBitmapDecoder BitmapDecoder { get; }
    IPlatformCursor Cursor { get; }
    IPlatformDragDropAdapter DragDropAdapter { get; }
    IPlatformFileSystem FileSystem { get; }
    IPlatformIconLoader IconLoader { get; }
    IPlatformInputMethod InputMethod { get; }
    IPlatformNativeMenuExporter NativeMenuExporter { get; }
    IScreens Screens { get; }
    IPlatformSettings Settings { get; }
    IPlatformUriHandler UriHandler { get; }
    IPlatformClipboard Clipboard { get; }
    IPlatformDragSource DragSource { get; }
    IPlatformDropTarget DropTarget { get; }
    IPlatformOpenUriHandler OpenUriHandler { get; }
    IPlatformVisualHost VisualHost { get; }
    IPlatformHwndHost HwndHost { get; }
    IPlatformHwndSource HwndSource { get; }
    IPlatformHwndTarget HwndTarget { get; }
    IPlatformRenderTarget RenderTarget { get; }
}

// Platform implementations
// - Windows: src/Avalonia.Win32/
// - macOS: src/Avalonia.Native/
// - X11: src/Avalonia.X11/
// - Skia: src/Avalonia.Skia/
// - Browser (WASM): src/Avalonia.Browser/
// - Headless: src/Avalonia.Headless/
// - Metal: src/Avalonia.Metal/
// - Vulkan: src/Avalonia.Vulkan/
```

## 19. Resource System

```csharp
// Location: src/Avalonia.Base/Styling/
public interface IResourceDictionary : IEnumerable, IDictionary
{
    object? this[object key] { get; set; }
    bool HasResources { get; }
    void Add(object key, object value);
    void Remove(object key);
    bool TryGetResource(object key, ThemeVariant? theme, out object? value);
    void AddOwner(IResourceHost host);
    void RemoveOwner(IResourceHost host);
}

// Markup extensions
public abstract class MarkupExtension
{
    public abstract object? ProvideValue(IServiceProvider serviceProvider);
}

// x:Type
public class TypeExtension : MarkupExtension
{
    public Type? Type { get; set; }
    public string? TypeName { get; set; }
    public override object? ProvideValue(IServiceProvider serviceProvider);
}

// x:Static
public class StaticExtension : MarkupExtension
{
    public Type? Type { get; set; }
    public string? Member { get; set; }
    public object? Value { get; }
    public override object? ProvideValue(IServiceProvider serviceProvider);
}

// x:Arguments
public class ArgumentsExtension : MarkupExtension
{
    public IList<object?> Arguments { get; }
    public override object? ProvideValue(IServiceProvider serviceProvider);
}

// x:Bind
public class BindExtension : MarkupExtension
{
    public string? Path { get; set; }
    public Type? DataType { get; set; }
    public override object? ProvideValue(IServiceProvider serviceProvider);
}

// x:Name
public class NameExtension : MarkupExtension
{
    public string? Name { get; set; }
    public override object? ProvideValue(IServiceProvider serviceProvider);
}

// x:Key
public class KeyExtension : MarkupExtension
{
    public object? Key { get; set; }
    public override object? ProvideValue(IServiceProvider serviceProvider);
}

// x:Shared
public class SharedExtension : MarkupExtension
{
    public bool Shared { get; set; }
    public override object? ProvideValue(IServiceProvider serviceProvider);
}

// x:Array
public class ArrayExtension : MarkupExtension
{
    public Type? Type { get; set; }
    public IList? Values { get; }
    public override object? ProvideValue(IServiceProvider serviceProvider);
}

// x:Dictionary
public class DictionaryExtension : MarkupExtension
{
    public IDictionary? Entries { get; }
    public override object? ProvideValue(IServiceProvider serviceProvider);
}
```

## 20. Key Files Reference

| File | Purpose |
|------|---------|
| `src/Avalonia.Base/AvaloniaObject.cs` | Base class with property system |
| `src/Avalonia.Base/StyledElement.cs` | Styled element with DataContext, styles, resources |
| `src/Avalonia.Base/Visual.cs` | Visual tree