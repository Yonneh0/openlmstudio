# Avalonia API Reference (Avalonia 12)

> **Generated from**: [docs.avaloniaui.net](https://docs.avaloniaui.net) and [AvaloniaUI/Avalonia](https://github.com/AvaloniaUI/Avalonia)
> **Version**: Avalonia 12 (compiled bindings, new clipboard API)
> **Target**: .NET 8+ (cross-platform)

---

## Table of Contents

- [Core Framework](#core-framework)
- [Namespaces](#namespaces)
- [Property System](#property-system)
- [Data Binding](#data-binding)
- [Events](#events)
- [Animation](#animation)
- [Controls Reference](#controls-reference)
- [Services](#services)
- [Platform Integration](#platform-integration)
- [XAML Markup Extensions](#xaml-markup-extensions)
- [Breaking Changes in Avalonia 12](#breaking-changes-in-avalonia-12)

---

## Core Framework

### Application

```csharp
namespace Avalonia
{
    public class Application
    {
        public static Application Current { get; }
        public static Application CurrentInstance { get; }
        public static Application LoadAndStart();
        public static void Start<TApp>(string[] args) where TApp : Application, new();
        public void Run();
        public void Start(string[] args);
        public void Shutdown();
        public void Startup(StartupEventArgs args);
    }
}
```

### Application Life Cycle

```csharp
namespace Avalonia
{
    public class StartupEventArgs : EventArgs
    {
        public string[] Args { get; }
        public Application Application { get; }
    }
}
```

### Dispatcher

```csharp
namespace Avalonia.Threading
{
    public class Dispatcher
    {
        public static Dispatcher Current { get; }
        public static bool CheckAccess();
        public static bool InvokeRequired => !CheckAccess();
        public Task InvokeAsync(Action action);
        public Task InvokeAsync(Func<Task> action);
        public T Invoke<T>(Func<T> func);
    }
}
```

---

## Namespaces

| Namespace | Description |
|-----------|-------------|
| `Avalonia` | Core framework types |
| `Avalonia.Animation` | Animation API |
| `Avalonia.Controls` | Built-in controls |
| `Avalonia.Controls.Primitives` | Control base classes and primitives |
| `Avalonia.Data` | Data binding infrastructure |
| `Avalonia.Data.Converters` | Value converters |
| `Avalonia.Input` | Input handling |
| `Avalonia.Interactivity` | Routed events |
| `Avalonia.Layout` | Layout system |
| `Avalonia.Markup` | XAML markup extensions |
| `Avalonia.Markup.Xaml` | XAML loading |
| `Avalonia.Media` | Drawing, brushes, effects |
| `Avalonia.Media.Text` | Text rendering |
| `Avalonia.Platform` | Platform abstraction layer |
| `Avalonia.Styling` | Styles and themes |
| `Avalonia.Themes.Fluent` | Fluent theme |
| `Avalonia.VirtualInput` | Virtual input devices |

---

## Property System

### AvaloniaObject

```csharp
namespace Avalonia
{
    public abstract class AvaloniaObject : ISetLogicalParent, IInheritanceParent,
        IPropertyParent, IServiceProvider
    {
        public T GetValue<T>(AvaloniaProperty<T> property);
        public void SetValue<T>(AvaloniaProperty<T> property, T value);
        public bool HasValue(AvaloniaProperty property);
        public void ClearValue(AvaloniaProperty property);
        public bool IsReadOnly(AvaloniaProperty property);
        public void BeginAnimation<T>(AvaloniaProperty<T> property, IAnimation animation);
        public void StopAnimation(AvaloniaProperty property);
        public void Bind<T>(AvaloniaProperty<T> property, IObservable<T> source);
        public event EventHandler<AvaloniaPropertyChangedEventArgs<T>>? PropertyChanged;
    }
}
```

### AvaloniaProperty

```csharp
namespace Avalonia
{
    public abstract class AvaloniaProperty
    {
        public string Name { get; }
        public Type PropertyType { get; }
        public object DefaultValue { get; }
        public Type OwnerType { get; }
        public object? DefaultValueForType(Type type);
    }

    public class AvaloniaProperty<T> : AvaloniaProperty { }

    public class AttachedProperty<T> : AvaloniaProperty<T>
    {
        public static AttachedProperty<T> RegisterAttached<TOwner, TType>(string name,
            Func<TOwner, TType> defaultValueSelector);
    }

    public class StyledProperty<T> : AvaloniaProperty<T>
    {
        public static StyledProperty<T> Register<TOwner>(string name, T defaultValue,
            bool inherits = false, BindingMode bindingMode = BindingMode.OneWay);
    }
}
```

### Attached Properties

```csharp
namespace Avalonia
{
    public static class AttachedProperties
    {
        public static T GetAttached<T>(AvaloniaObject obj, AttachedProperty<T> property);
        public static void SetAttached<T>(AvaloniaObject obj, AttachedProperty<T> property, T value);
        public static bool HasAttached<T>(AvaloniaObject obj, AttachedProperty<T> property);
        public static void ClearAttached<T>(AvaloniaObject obj, AttachedProperty<T> property);
    }
}
```

### StyledProperty<T>

```csharp
namespace Avalonia.Styling
{
    public static class StyledProperty<T>
    {
        public static StyledProperty<T> Register<TOwner>(string name, T defaultValue,
            bool inherits = false, BindingMode bindingMode = BindingMode.OneWay);
    }
}
```

### Property Change Notification

```csharp
namespace Avalonia
{
    public class AvaloniaPropertyChangedEventArgs
    {
        public AvaloniaProperty Property { get; }
        public object OldValue { get; }
        public object NewValue { get; }
        public object Sender { get; }
        public bool HasOldValue { get; }
        public bool HasNewValue { get; }
    }

    public class AvaloniaPropertyChangedEventArgs<T> : AvaloniaPropertyChangedEventArgs
    {
        public new T OldValue { get; }
        public new T NewValue { get; }
    }
}
```

---

## Data Binding

### INotifyPropertyChanged

```csharp
namespace System.ComponentModel
{
    public interface INotifyPropertyChanged
    {
        event PropertyChangedEventHandler? PropertyChanged;
    }
}
```

### Compiled Bindings (Avalonia 12)

```csharp
namespace Avalonia.Data
{
    public class CompiledBinding
    {
        public static Binding Bind<TTarget, TSource, TProperty>(
            Expression<Func<TSource, TProperty>> propertySelector,
            BindingMode mode = BindingMode.OneWay,
            IValueConverter? converter = null);
    }
}
```

### Binding

```csharp
namespace Avalonia.Data
{
    public class Binding
    {
        public Binding(string path, BindingMode mode = BindingMode.OneWay,
            IValueConverter? converter = null, object? source = null,
            string? relativeSource = null);
        public string? Path { get; }
        public BindingMode Mode { get; }
        public IValueConverter? Converter { get; }
        public object? Source { get; }
        public RelativeSource? RelativeSource { get; }
        public object? FallbackValue { get; }
        public object? TargetNullValue { get; }
        public bool IsAsync { get; }
        public int Priority { get; }
    }

    public enum BindingMode
    {
        OneWay,
        TwoWay,
        OneTime,
        OneWayToSource,
    }
}
```

### RelativeSource

```csharp
namespace Avalonia.Data
{
    public abstract class RelativeSource
    {
        public static RelativeSource Self { get; }
        public static RelativeSource FindAncestor<TAncestor>();
        public static RelativeSource TemplatedParent { get; }
        public static RelativeSource PreviousData { get; }
    }
}
```

### ObservableObject (CommunityToolkit.Mvvm)

```csharp
namespace CommunityToolkit.Mvvm.ComponentModel
{
    [INotifyPropertyChanged]
    public partial class ObservableObject : INotifyPropertyChanged
    {
        protected void SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null);
        protected bool SetProperty<T>(ref T field, T value, out T oldValue,
            [CallerMemberName] string? propertyName = null);
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null);
    }
}
```

### BindableBase

```csharp
namespace Avalonia
{
    public abstract class BindableBase : AvaloniaObject, INotifyPropertyChanged
    {
        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null);
        protected bool SetProperty<T>(ref T field, T value, out T oldValue,
            [CallerMemberName] string? propertyName = null);
    }
}
```

### IValueConverter

```csharp
namespace Avalonia.Data.Converters
{
    public interface IValueConverter
    {
        object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture);
        object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture);
    }

    public class BooleanToVisibilityConverter : IValueConverter { }
    public class StringConverters
    {
        public static readonly IValueConverter ToInt32;
        public static readonly IValueConverter ToDouble;
        public static readonly IValueConverter ToString;
    }
}
```

### MultiBinding

```csharp
namespace Avalonia.Data
{
    public class MultiBinding : Binding
    {
        public MultiBinding(Binding[] bindings, IValueConverter? converter = null);
        public Binding[] Bindings { get; }
    }
}
```

---

## Events

### RoutedEvent

```csharp
namespace Avalonia.Interactivity
{
    public abstract class RoutedEvent
    {
        public string Name { get; }
        public RoutingStrategy Strategy { get; }
        public Type HandlerType { get; }
        public Type OwnerType { get; }
        public static RoutedEvent Register<T>(string name, RoutingStrategy strategy,
            Type handlerType, Type ownerType);
    }

    public class RoutedEvent<T> : RoutedEvent where T : RoutedEvent<T>
    {
        public static RoutedEvent<T> Register(string name, RoutingStrategy strategy,
            Type handlerType, Type ownerType);
    }
}

public enum RoutingStrategy
{
    Direct,
    Tunneling,
    Bubbling,
}
```

### UIElement

```csharp
namespace Avalonia.Controls
{
    public abstract class UIElement : Visual, IInputElement, ILogical, IVisual,
        IStyleable, IServiceProvider
    {
        public static readonly RoutedEvent<KeyDownEvent> KeyDownEvent;
        public static readonly RoutedEvent<KeyUpEvent> KeyUpEvent;
        public static readonly RoutedEvent<TouchDownEvent> TouchDownEvent;
        public static readonly RoutedEvent<PointerMovedEvent> PointerMovedEvent;
        public static readonly RoutedEvent<PointerPressedEvent> PointerPressedEvent;
        public static readonly RoutedEvent<PointerReleasedEvent> PointerReleasedEvent;
        public static readonly RoutedEvent<PointerEnteredEvent> PointerEnteredEvent;
        public static readonly RoutedEvent<PointerExitedEvent> PointerExitedEvent;

        public event EventHandler<KeyEventArgs>? KeyDown;
        public event EventHandler<KeyEventArgs>? KeyUp;
        public event EventHandler<TouchEventArgs>? TouchDown;
        public event EventHandler<PointerEventArgs>? PointerMoved;
        public event EventHandler<PointerPressedEventArgs>? PointerPressed;
        public event EventHandler<PointerReleasedEventArgs>? PointerReleased;
        public event EventHandler<PointerEventArgs>? PointerEntered;
        public event EventHandler<PointerEventArgs>? PointerExited;

        public void AddHandler<T>(RoutedEvent<T> routedEvent, Delegate handler,
            RoutingStrategy strategy, bool handledEventsToo) where T : RoutedEvent<T>;
        public void RemoveHandler<T>(RoutedEvent<T> routedEvent, Delegate handler);
        public bool RaiseEvent<T>(T e) where T : RoutedEventArgs;
    }
}
```

### Visual

```csharp
namespace Avalonia.Visuals
{
    public abstract class Visual : IVisual, ILayoutRoot, IServiceProvider
    {
        public Visual? Parent { get; }
        public Visual? FindParent<T>() where T : Visual;
        public Visual? FindDescendant<T>();
        public IEnumerable<Visual> GetVisualChildren();
        public IEnumerable<Visual> GetVisualAncestors();
        public void AttachParent(Visual? parent);
        public void DetachParent();
    }
}
```

---

## Animation

### Animatable

```csharp
namespace Avalonia.Animation
{
    public abstract class Animatable : AvaloniaObject
    {
        public IAnimation? GetAnimation(AvaloniaProperty property);
        public void BeginAnimation(AvaloniaProperty property, IAnimation animation);
        public void StopAnimation(AvaloniaProperty property);
        public void StopAnimations();
    }
}
```

### Animation

```csharp
namespace Avalonia.Animation
{
    public class Animation : IAnimation
    {
        public TimeSpan Duration { get; set; }
        public Easing? Easing { get; set; }
        public FillMode FillMode { get; set; }
        public PlaybackRate PlaybackRate { get; set; }
        public IEnumerable<AnimationKeyFrame> KeyFrames { get; set; }
        public Task Run(IAnimatable animatable, IRuleSelector selector,
            CancellationToken cancellationToken = default);
    }

    public enum FillMode
    {
        None, Forward, Backward, Both,
    }
}
```

### Easing

```csharp
namespace Avalonia.Animation.Easings
{
    public abstract class Easing
    {
        public abstract double Easing(double t);
        public static readonly Easing Linear;
        public static readonly Easing CubicEaseIn;
        public static readonly Easing CubicEaseOut;
        public static readonly Easing CubicEaseInOut;
        public static readonly Easing BounceEaseIn;
        public static readonly Easing BounceEaseOut;
        public static readonly Easing BounceEaseInOut;
        public static readonly Easing CircularEaseIn;
        public static readonly Easing CircularEaseOut;
        public static readonly Easing CircularEaseInOut;
        public static readonly Easing ElasticEaseIn;
        public static readonly Easing ElasticEaseOut;
        public static readonly Easing ElasticEaseInOut;
        public static readonly Easing BackEaseIn;
        public static readonly Easing BackEaseOut;
        public static readonly Easing BackEaseInOut;
        public static readonly Easing ExponentialEaseIn;
        public static readonly Easing ExponentialEaseOut;
        public static readonly Easing ExponentialEaseInOut;
    }
}
```

---

## Controls Reference

### Decorator Controls

| Control | Base Class | Description |
|---------|-----------|-------------|
| `Border` | Decorator | Draws a border, background, and corner radius |
| `BoxView` | Decorator | A simple filled rectangle |
| `Canvas` | Panel | Positions children by absolute coordinates |
| `Grid` | Panel | Flexible grid-based layout |
| `StackPanel` | Panel | Linear stacking of children |
| `DockPanel` | Panel | Dock-based layout |
| `WrapPanel` | Panel | Wraps children to next line when overflowing |
| `UniformGrid` | Panel | Equal-sized grid cells |
| `ScrollViewer` | ContentControl | Scrollable container |
| `ContentControl` | ContentControl | Single content host |

### Content Controls

| Control | Base Class | Description |
|---------|-----------|-------------|
| `Button` | ButtonBase | Clickable button |
| `ToggleButton` | ToggleButtonBase | Two-state toggle button |
| `CheckBox` | ToggleButtonBase | Checkbox with label |
| `RadioButton` | ToggleButtonBase | Radio button |
| `Image` | ImageBase | Image display |
| `TextBlock` | TextElement | Text display |
| `TextBox` | TextBoxBase | Single-line text input |
| `PasswordBox` | PasswordBoxBase | Password input |
| `Label` | ContentControl | Label for input controls |
| `Expander` | HeaderedContentControl | Collapsible content panel |
| `ProgressBar` | RangeBase | Progress indicator |
| `ProgressBarRing` | Control | Circular progress |
| `ProgressRing` | Control | Indeterminate progress |
| `Rectangle` | Shape | Filled rectangle |
| `Ellipse` | Shape | Filled ellipse |
| `Line` | Shape | Line segment |
| `Polygon` | Shape | Filled polygon |
| `Polyline` | Shape | Open polyline |
| `Viewbox` | ContentControl | Scales content |
| `ViewBox2D` | ContentControl | 2D viewport |
| `ViewBox2DWithScrolling` | ContentControl | 2D viewport with scrolling |

### Headered Controls

| Control | Base Class | Description |
|---------|-----------|-------------|
| `TabControl` | HeaderedItemsControl | Tab container |
| `TabItem` | HeaderedContentControl | Tab item |
| `TreeView` | ItemsControl | Tree view |
| `TreeViewItem` | HeaderedContentControl | Tree view item |
| `ListBox` | ItemsControl | Selectable list |
| `ListBoxItem` | ContentControl | List box item |
| `ListView` | ItemsControl | List with virtualization |
| `MenuItem` | HeaderedContentControl | Menu item |
| `Menu` | ItemsControl | Menu bar |
| `ContextMenu` | Menu | Context menu |
| `Separator` | Control | Visual separator |

### Selection Controls

| Control | Base Class | Description |
|---------|-----------|-------------|
| `ComboBox` | SelectingItemsControl | Drop-down selector |
| `Slider` | RangeBase | Horizontal slider |
| `RangeSlider` | RangeBase | Dual-range slider |
| `Rating` | Control | Star rating |
| `RatingItem` | Control | Rating star |
| `NumericUpDown` | RangeBase | Numeric input |
| `DecimalUpDown` | RangeBase | Decimal input |
| `DecimalNumPicker` | Control | Decimal picker |
| `Calendar` | CalendarBase | Calendar selector |
| `DatePicker` | DatePickerBase | Date picker |
| `TimePicker` | TimePickerBase | Time picker |

### Window Controls

| Control | Base Class | Description |
|---------|-----------|-------------|
| `Window` | WindowBase | Top-level window |
| `WindowBase` | WindowBaseBase | Window base class |
| `Popup` | PopupBase | Popup window |
| `OverlayPopup` | PopupBase | Overlay popup |
| `ItemPicker` | PopupBase | Item picker popup |
| `Flyout` | FlyoutBase | Flyout panel |
| `FlyoutBase` | FlyoutBase | Flyout base |

### Primitive Controls

| Control | Base Class | Description |
|---------|-----------|-------------|
| `ScrollBar` | RangeBase | Scroll bar |
| `Thumb` | ThumbBase | Draggable thumb |
| `Track` | TrackBase | Scroll track |
| `MetroThumb` | ThumbBase | Metro-style thumb |
| `MetroTrack` | TrackBase | Metro-style track |
| `GridSplitter` | Control | Grid splitter |
| `Watermarking` | Control | Watermark behavior |
| `Transition` | Control | Transition |
| `TransitioningContentControl` | ContentControl | Content with transition |
| `FlipView` | SelectingItemsControl | Flip view |
| `InfoBanner` | HeaderedContentControl | Information banner |
| `Tooltip` | ContentControl | Tooltip |
| `HyperlinkButton` | ButtonBase | Hyperlink button |
| `AvaloniaEditor` | TextBoxBase | Code editor |

### Tree Data Grid

| Control | Base Class | Description |
|---------|-----------|-------------|
| `TreeDataGrid` | Control | Tree data grid |
| `TreeDataGridBoundColumn` | TreeDataGridColumn | Bound column |
| `TreeDataGridCheckBoxColumn` | TreeDataGridColumn | Checkbox column |
| `TreeDataGridColumn` | TreeDataGridColumnBase | Column base |
| `TreeDataGridColumns` | TreeDataGridColumnsBase | Columns collection |
| `TreeDataGridHierarchicalExpanderColumn` | TreeDataGridColumn | Hierarchical expander column |
| `TreeDataGridRowEventArgs` | TreeDataGridRowEventArgsBase | Row event args |
| `TreeDataGridRowDragEventArgs` | TreeDataGridRowDragEventArgsBase | Row drag event args |
| `TreeDataGridRowDragStartedEventArgs` | TreeDataGridRowDragStartedEventArgsBase | Row drag started |
| `TreeDataGridRowDropPosition` | TreeDataGridRowDropPositionBase | Row drop position |
| `TreeDataGridRowHeaderColumn` | TreeDataGridColumn | Row header column |
| `TreeDataGridRows` | TreeDataGridRowsBase | Rows collection |
| `TreeDataGridSelectionMode` | enum | Selection mode |
| `TreeDataGridSource` | TreeDataGridSourceBase | Data source |
| `TreeDataGridSourceExtensions` | static | Source extensions |
| `TreeDataGridTemplateColumn` | TreeDataGridColumn | Template column |
| `TreeDataGridTextColumn` | TreeDataGridColumn | Text column |
| `FlatTreeDataGridSource` | TreeDataGridSource | Flat source |
| `HierarchicalTreeDataGridSource` | TreeDataGridSource | Hierarchical source |
| `TreeDataGridCell` | TreeDataGridCellBase | Cell |
| `TreeDataGridCellsPresenter` | TreeDataGridCellsPresenterBase | Cells presenter |
| `TreeDataGridCheckboxCell` | TreeDataGridCell | Checkbox cell |
| `TreeDataGridColumnHeader` | TreeDataGridColumnHeaderBase | Column header |
| `TreeDataGridColumnHeadersPresenter` | TreeDataGridColumnHeadersPresenterBase | Headers presenter |
| `TreeDataGridElementFactory` | TreeDataGridElementFactoryBase | Element factory |
| `TreeDataGridExpanderCell` | TreeDataGridExpanderCellBase | Expander cell |
| `TreeDataGridPresenterBase` | TreeDataGridPresenterBaseBase | Presenter base |
| `TreeDataGridRow` | TreeDataGridRowBase | Row |
| `TreeDataGridRowHeaderCell` | TreeDataGridRowHeaderCellBase | Row header cell |
| `TreeDataGridRowsPresenter` | TreeDataGridRowsPresenterBase | Rows presenter |
| `TreeDataGridTemplateCell` | TreeDataGridTemplateCellBase | Template cell |
| `TreeDataGridTextCell` | TreeDataGridTextCellBase | Text cell |
| `TreeDataGridCellSelectionModel` | TreeDataGridCellSelectionModelBase | Cell selection model |
| `TreeDataGridRowSelectionModel` | TreeDataGridRowSelectionModelBase | Row selection model |
| `ITreeDataGridCellModel` | interface | Cell model |
| `ITreeDataGridRowModel` | interface | Row model |
| `CheckBoxColumnCreateOptions` | CheckBoxColumnCreateOptionsBase | Checkbox options |
| `ColumnCreateOptions` | ColumnCreateOptionsBase | Column options |
| `HierarchicalExpanderColumnCreateOptions` | HierarchicalExpanderColumnCreateOptionsBase | Expander options |
| `HierarchicalExpanderTextColumnCreateOptions` | HierarchicalExpanderTextColumnCreateOptionsBase | Text expander options |
| `TemplateColumnCreateOptions` | TemplateColumnCreateOptionsBase | Template options |
| `TextColumnCreateOptions` | TextColumnCreateOptionsBase | Text column options |
| `IndentConverter` | IValueConverter | Indent converter |

---

## Services

### Clipboard

```csharp
namespace Avalonia.Clipboard
{
    public interface IClipboard
    {
        void SetText(string text);
        string? GetText();
        void SetData(object key, object? data);
        object? GetData(object key);
    }
}
```

### Dialogs

```csharp
namespace Avalonia.Dialogs
{
    public interface IDialogService
    {
        Task<MessageBoxResult> ShowMessageAsync(string title, string message);
        Task<bool?> ShowQuestionAsync(string title, string message);
    }
}
```

### Storage

```csharp
namespace Avalonia.Storage
{
    public interface IStorageService
    {
        Task<string> GetApplicationStoragePath();
        Task<Stream> OpenFileAsync(string path, FileMode mode);
        Task SaveFileAsync(string path, Stream content);
    }
}
```

---

## Platform Integration

### Windows

```csharp
namespace Avalonia.Win32
{
    public class Win32Platform
    {
        public static PlatformManager Initialize();
    }
}
```

### macOS

```csharp
namespace Avalonia.MacOS
{
    public class MacPlatform
    {
        public static PlatformManager Initialize();
    }
}
```

### Linux

```csharp
namespace Avalonia.Linux
{
    public class LinuxPlatform
    {
        public static PlatformManager Initialize();
    }
}
```

### iOS

```csharp
namespace Avalonia.iOS
{
    public class iOSPlatform
    {
        public static PlatformManager Initialize();
    }
}
```

### Android

```csharp
namespace Avalonia.Android
{
    public class AndroidPlatform
    {
        public static PlatformManager Initialize();
    }
}
```

### WebAssembly

```csharp
namespace Avalonia.WebAssembly
{
    public class WasmPlatform
    {
        public static PlatformManager Initialize();
    }
}
```

---

## XAML Markup Extensions

| Extension | Namespace | Description |
|-----------|-----------|-------------|
| `Binding` | `Avalonia.Data` | Data binding |
| `RelativeBinding` | `Avalonia.Data` | Binding with RelativeSource |
| `MultiBinding` | `Avalonia.Data` | Multi-value binding |
| `DynamicResource` | `Avalonia.Styling` | Dynamic resource lookup |
| `StaticResource` | `Avalonia.Styling` | Static resource lookup |
| `CompiledBinding` | `Avalonia.Data` | Compiled binding (Avalonia 12) |
| `x:Bind` | `Avalonia.Markup.Xaml` | Compiled binding syntax |
| `x:Type` | `Avalonia.Markup` | Type reference |
| `x:Static` | `Avalonia.Markup` | Static member reference |
| `x:Null` | `Avalonia.Markup` | Null value |
| `x:Arguments` | `Avalonia.Markup` | Constructor arguments |
| `x:TypeArguments` | `Avalonia.Markup` | Generic type arguments |
| `x:Array` | `Avalonia.Markup` | Array literal |
| `x:Dictionary` | `Avalonia.Markup` | Dictionary literal |
| `x:Name` | `Avalonia.Markup` | Object reference |
| `x:Key` | `Avalonia.Markup` | Resource key |
| `x:Shared` | `Avalonia.Markup` | Resource sharing |

---

## Breaking Changes in Avalonia 12

### Key Changes

1. **Compiled Bindings by Default**
   - Bindings are now compiled at build time
   - Faster binding performance
   - Compile-time binding errors
   - Use `x:DataType` to enable compiled bindings

2. **New Clipboard API**
   - `IClipboard` interface updated
   - `Clipboard.SetText()` replaced with `Clipboard.SetTextAsync()`
   - New `IDataObject` support

3. **Updated Window Decorations**
   - New window title bar implementation
   - Platform-native decorations by default
   - `Window.TitleBar` API changes

4. **Removed Deprecated APIs**
   - `IAccessibleProvider` replaced with `IAccessibilityProvider`
   - `IPlatformGraphics` replaced with `IPlatformGraphicsFactory`
   - Various property renames

5. **Property System Updates**
   - `StyledProperty<T>` inheritance changes
   - `AttachedProperty<T>` registration updates
   - `AvaloniaObject` inheritance model improvements

6. **Animation API**
   - `IAnimation` interface changes
   - New `Easing` system
   - `Animation` class updates

7. **XAML Compiler**
   - `AvaloniaXamlIlLoader` changes
   - XAML compilation improvements
   - Resource dictionary updates

---

## API Compatibility

Avalonia provides API compatibility guarantees between releases:

- **Breaking changes** are documented in release notes
- **Non-breaking changes** are additive only
- **Deprecated APIs** are kept for at least one major version
- **Platform-specific APIs** may vary by platform

See [API Compatibility](https://docs.avaloniaui.net/docs/avalonia12-breaking-changes) for details.

---

## See Also

- [Avalonia GitHub](https://github.com/AvaloniaUI/Avalonia)
- [docs.avaloniaui.net](https://docs.avaloniaui.net)
- [Avalonia 12 Breaking Changes](https://docs.avaloniaui.net/docs/avalonia12-breaking-changes)