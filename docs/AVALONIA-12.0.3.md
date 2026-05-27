# Avalonia 12.0.3 Complete Architecture Reference

> **Version:** 12.0.3  
> **Purpose:** Comprehensive RAG document covering architecture, property system, data binding, layout, controls, styling, animation, input events, advanced patterns, and platform abstraction.

---

## Table of Contents

1. [Type Hierarchy](#1-type-hierarchy)
2. [AvaloniaObject & Property System](#2-avaloniaobject--property-system)
3. [Data Binding](#3-data-binding)
4. [Layout System](#4-layout-system)
5. [Controls](#5-controls)
   - [5.1 Decorators & Shapes](#51-decorators--shapes)
   - [5.2 Content Controls](#52-content-controls)
   - [5.3 Input Controls](#53-input-controls)
   - [5.4 Items Controls](#54-items-controls)
   - [5.5 Window & Popup](#55-window--popup)
6. [Styling & Theming](#6-styling--theming)
7. [Animation](#7-animation)
8. [Input & Events](#8-input--events)
9. [Advanced Patterns](#9-advanced-patterns)
10. [Platform Abstraction](#10-platform-abstraction)
11. [Consolidated Key Files Reference](#11-consolidated-key-files-reference)

---

## 1. Type Hierarchy

Avalonia uses a layered class hierarchy mirroring WPF's model.

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

---

## 2. AvaloniaObject & Property System

### 2.1 AvaloniaObject (Base Class)

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

### 2.2 AvaloniaProperty Types

#### AvaloniaProperty<T> (Base)

```csharp
// Location: src/Avalonia.Base/AvaloniaProperty.cs
public abstract class AvaloniaProperty : INamed
{
    public string Name { get; }
    public Type PropertyType { get; }
    public Type OwnerType { get; }
    public abstract bool IsDirect { get; }
    public abstract bool IsReadOnly { get; }
    
    // Routing
    public abstract object? RouteGetValue(AvaloniaObject owner);
    public abstract void RouteSetValue(AvaloniaObject owner, object? value, BindingPriority priority);
    public abstract void RouteSetCurrentValue(AvaloniaObject owner, object? value);
    public abstract void RouteBind(AvaloniaObject owner, IObservable<object?> source, BindingPriority priority);
    
    // Callbacks
    public void NotifyChanged(AvaloniaPropertyChangedEventArgs args);
}

// Generic version
// Location: src/Avalonia.Base/AvaloniaProperty`1.cs
public abstract class AvaloniaProperty<T> : AvaloniaProperty
{
    public new Type PropertyType => typeof(T);
    public abstract T DefaultValue { get; }
    public abstract T GetDefault(AvaloniaObject owner);
    
    // Callbacks
    public void NotifyChanged(AvaloniaPropertyChangedEventArgs<T> args);
}
```

#### StyledProperty<T>

For properties that can be styled via CSS-like selectors.

```csharp
// Location: src/Avalonia.Base/StyledProperty.cs
public class StyledProperty<T> : AvaloniaProperty<T>
{
    public static StyledProperty<T> Register<TOwner>(
        string name,
        T? defaultValue = default,
        bool inherits = false,
        BindingPriority defaultBindingPriority = BindingPriority.LocalValue,
        Func<T, bool>? validate = null,
        Func<AvaloniaObject, T, T>? coerce = null,
        bool enableDataValidation = false,
        Func<AvaloniaObject, T, T>? notifying = null)
        where TOwner : StyledElement;
    
    public static StyledProperty<T> RegisterAttached<TOwner, TTarget>(
        string name,
        T? defaultValue = default,
        bool inherits = false,
        BindingPriority defaultBindingPriority = BindingPriority.LocalValue,
        Func<T, bool>? validate = null,
        Func<AvaloniaObject, T, T>? coerce = null,
        bool enableDataValidation = false,
        Func<AvaloniaObject, T, T>? notifying = null)
        where TOwner : class
        where TTarget : StyledElement;
    
    // Attached property helpers
    public static void Set<T>(AvaloniaObject target, T value);
    public static T Get<T>(AvaloniaObject target);
    
    // Metadata
    public StyledPropertyMetadata<T> Metadata { get; }
    public bool Inherited { get; }
    public BindingPriority DefaultBindingPriority { get; }
    public bool EnableDataValidation { get; }
}
```

#### DirectProperty<T>

For properties backed by a field.

```csharp
// Location: src/Avalonia.Base/DirectProperty.cs
public class DirectProperty<T> : AvaloniaProperty<T>
{
    public static DirectProperty<T> Register<TOwner, TProp>(
        string name,
        Func<TOwner, TProp> getter,
        Action<TOwner, TProp> setter,
        TProp defaultValue = default,
        bool enableDataValidation = false)
        where TOwner : AvaloniaObject;
    
    // Getter/Setter
    public TProp Get(AvaloniaObject owner);
    public void Set(AvaloniaObject owner, TProp value);
    public void InvokeSetter(AvaloniaObject owner, TProp value);
}
```

#### AttachedProperty<T>

For properties that can be set on any object.

```csharp
// Location: src/Avalonia.Base/AttachedProperty.cs
public class AttachedProperty<T> : StyledProperty<T>
{
    public static AttachedProperty<T> Register<TOwner, TTarget>(
        string name,
        Func<TTarget, T> getter,
        Action<TTarget, T> setter,
        T defaultValue = default,
        bool inherits = false,
        Func<T, bool>? validate = null,
        Func<AvaloniaObject, T, T>? coerce = null)
        where TOwner : class
        where TTarget : class;
    
    // Helper methods
    public static T Get<TTarget>(TTarget target) where TTarget : class;
    public static void Set<TTarget>(TTarget target, T value) where TTarget : class;
}
```

### 2.3 BindingPriority

```csharp
// Location: src/Avalonia.Base/PropertyStore/
public enum BindingPriority
{
    Animation = 0,       // Highest priority - set by animations
    Transient,           // Temporary values (e.g., hover)
    LocalValue,          // Set by code or markup
    StyleValue,          // Set by styles
    ThemeValue,          // Set by themes
    TemplatedParentTheme,
    Inherited,           // Lowest priority - inherited from parent
    Unset,               // No value set
}
```

### 2.4 ValueStore System

```csharp
// Location: src/Avalonia.Base/PropertyStore/
public class ValueStore
{
    public ValueStore(AvaloniaObject owner);
    
    // Get/Set values
    public object? GetValue(AvaloniaProperty property);
    public T GetValue<T>(StyledProperty<T> property);
    public void SetValue(StyledProperty<T> property, T value, BindingPriority priority);
    public void SetCurrentValue(StyledProperty<T> property, T value);
    public void ClearValue(StyledProperty<T> property);
    public void ClearValue(AvaloniaProperty property);
    public bool IsSet(AvaloniaProperty property);
    public bool IsAnimating(AvaloniaProperty property);
    public void CoerceValue(AvaloniaProperty property);
    public Optional<T> GetBaseValue<T>(StyledProperty<T> property);
    
    // Binding
    public IDisposable AddBinding<T>(
        StyledProperty<T> property,
        IObservable<object?> source,
        BindingPriority priority);
    public IDisposable AddBinding<T>(
        StyledProperty<T> property,
        IObservable<T> source,
        BindingPriority priority);
    public IDisposable AddBinding<T>(
        StyledProperty<T> property,
        IObservable<BindingValue<T>> source,
        BindingPriority priority);
    
    // Styling
    public void BeginStyling();
    public void EndStyling();
    public void RemoveFrames(FrameType type);
    public void RemoveFrames(IReadOnlyList<IStyle> styles);
    
    // Debug
    public AvaloniaPropertyValue GetDiagnostic(AvaloniaProperty property);
}

public enum FrameType
{
    Animation = 0,
    Transient,
    LocalValue,
    Style,
    Theme,
    TemplatedParentTheme,
    Inherited,
}
```

### 2.5 AvaloniaPropertyChangedEventArgs

```csharp
// Location: src/Avalonia.Base/AvaloniaPropertyChangedEventArgs.cs
public class AvaloniaPropertyChangedEventArgs
{
    public AvaloniaObject Sender { get; }
    public AvaloniaProperty Property { get; }
    public object? OldValue { get; }
    public object? NewValue { get; }
    public BindingPriority Priority { get; }
    public bool IsEffectiveValueChange { get; }
}

// Generic version
public class AvaloniaPropertyChangedEventArgs<T> : AvaloniaPropertyChangedEventArgs
{
    public T OldValue { get; }
    public BindingValue<T> NewValue { get; }
    public new T Property { get; }
    
    public bool HasNewValue => NewValue.HasValue;
    public T EffectiveValue => NewValue.Value;
}
```

### 2.6 Property Registration Patterns

#### Standard Styled Property

```csharp
public static readonly StyledProperty<string?> TextProperty =
    AvaloniaProperty.Register<TextBlock, string?>(
        nameof(Text),
        defaultValue: "",
        inherits: false,
        defaultBindingMode: BindingMode.OneWay,
        validate: ValidateText,
        coerce: null,
        enableDataValidation: false,
        notifying: OnTextNotifying);

private static bool ValidateText(string? value) => value != null;

private static string? OnTextNotifying(AvaloniaObject obj, string? value) => value;

public string? Text
{
    get => GetValue(TextProperty);
    set => SetValue(TextProperty, value);
}
```

#### Direct Property

```csharp
public static readonly DirectProperty<MyControl, int> CountProperty =
    AvaloniaProperty.RegisterDirect<MyControl, int>(
        nameof(Count),
        o => o.Count,
        (o, v) => o.Count = v);

private int _count;

public int Count
{
    get => _count;
    set => SetAndRaise(CountProperty, ref _count, value);
}
```

#### Attached Property

```csharp
public static readonly AttachedProperty<int> MyAttachedProperty =
    AvaloniaProperty.RegisterAttached<MyAttached, Control, int>(
        nameof(MyAttachedProperty),
        defaultValue: 0,
        inherits: false);

public static void SetMyAttachedProperty(Control target, int value)
    => target.SetValue(MyAttachedProperty, value);

public static int GetMyAttachedProperty(Control target)
    => target.GetValue(MyAttachedProperty);
```

#### Property with Affects Metadata

```csharp
// Affects the parent's layout
[AffectsParentLayout<MyControl>(nameof(ChildProperty))]

// Affects the parent's measure
[AffectsParentMeasure<MyControl>(nameof(ChildProperty))]

// Affects rendering
[AffectsRender<MyControl>(nameof(FillProperty))]

// Affects the element's own layout
[AffectsMeasure<MyControl>(nameof(WidthProperty))]
```

### 2.7 Inheritance System

```csharp
// Location: src/Avalonia.Base/AvaloniaObject.cs
public class AvaloniaObject
{
    protected AvaloniaObject? InheritanceParent { get; set; }
    
    // Inherited properties are automatically propagated
    // when the InheritanceParent changes
}

// ISetInheritanceParent interface
public interface ISetInheritanceParent
{
    void SetParent(AvaloniaObject? parent);
}
```

### 2.8 Coercion System

```csharp
// CoerceValueCallback is called when a property's value changes
// and the coerce function returns a different value
public class MyControl : Control
{
    private static T? CoerceMyProperty(AvaloniaObject obj, T? value)
    {
        if (obj is MyControl control)
            return Math.Max(0, value);
        return value;
    }
    
    public static readonly StyledProperty<int> MyProperty =
        AvaloniaProperty.Register<MyControl, int>(
            nameof(MyProperty),
            defaultValue: 0,
            coerce: CoerceMyProperty);
}
```

### 2.9 Data Validation

```csharp
// Location: src/Avalonia.Controls/DataValidationErrors.cs
public class DataValidationErrors : AvaloniaObject
{
    public static readonly AttachedProperty<IList<Exception>?> ErrorsProperty =
        AvaloniaProperty.RegisterAttached<DataValidationErrors, Control, IList<Exception>?>(
            nameof(Errors),
            defaultValue: null);
    
    public static void SetErrors(Control target, IList<Exception> errors);
    public static IList<Exception>? GetErrors(Control target);
    public static void SetErrors(Control target, Exception error);
    public static void ClearErrors(Control target);
    public static bool GetIsError(Control target);
    public static void SetIsError(Control target, bool value);
}

// Enable data validation in property registration
public static readonly StyledProperty<string?> TextProperty =
    AvaloniaProperty.Register<TextBox, string?>(
        nameof(Text),
        enableDataValidation: true);
```

### 2.10 Classes (Pseudo-Classes)

```csharp
// Location: src/Avalonia.Base/Classes.cs
public class Classes : IPseudoClasses, INotifyCollectionChanged
{
    public bool this[string className] { get; }
    public ICollection<string> Classes { get; }
    public int Count { get; }
    
    public void Add(string className);
    public void Remove(string className);
    public void Clear();
    public void Set(string className, bool value);
    public bool TryGet(string className, out bool value);
    
    public event NotifyCollectionChangedEventHandler? CollectionChanged;
    public event EventHandler? Changed;
}

// Pseudo-class access
protected IPseudoClasses PseudoClasses { get; }
PseudoClasses["pressed"] = true;
PseudoClasses["hover"] = false;
```

### 2.11 IResourceProvider

```csharp
// Location: src/Avalonia.Base/Styling/
public interface IResourceProvider
{
    IResourceDictionary Resources { get; }
    IResourceProvider? Parent { get; }
}

// Resource lookup chain
public interface IResourceNode
{
    bool HasResources { get; }
    bool TryGetResource(object key, ThemeVariant? theme, out object? value);
}
```

---

## 3. Data Binding

### 3.1 Binding Basics

```csharp
// Location: src/Avalonia.Base/Data/
public class Binding : BindingBase
{
    public Binding();
    public Binding(string path);
    public Binding(string path, BindingMode mode);
    public Binding(string path, BindingMode mode, object? source);
    public Binding(string path, BindingMode mode, object? source, IValueConverter? converter);
    
    // Properties
    public string? Path { get; set; }
    public BindingMode Mode { get; set; }
    public IValueConverter? Converter { get; set; }
    public object? Source { get; set; }
    public RelativeSource? RelativeSource { get; set; }
    public object? FallbackValue { get; set; }
    public object? TargetNullValue { get; set; }
    public object? ConverterParameter { get; set; }
    public string? StringFormat { get; set; }
    public bool IsAsync { get; set; }
    public int UpdateSourceTrigger { get; set; }
    public object? XPath { get; set; }
    
    // Methods
    public override BindingExpressionBase CreateInstance(
        AvaloniaObject target,
        AvaloniaProperty property,
        object? anchor);
}
```

### 3.2 BindingMode

```csharp
// Location: src/Avalonia.Base/Data/
public enum BindingMode
{
    OneWay,        // Source → Target (default)
    TwoWay,        // Source ↔ Target
    OneTime,       // One-time evaluation
    OneWayToSource, // Target → Source
}
```

### 3.3 RelativeSource

```csharp
// Location: src/Avalonia.Base/Data/
public abstract class RelativeSource : MarkupExtension
{
    public static RelativeSource Self { get; }
    public static RelativeSource TemplatedParent { get; }
    public static RelativeSource PreviousData { get; }
    public static RelativeSource FindAncestor(int ancestorLevel);
    public static RelativeSource FindAncestor<T>() where T : class;
    public static RelativeSource PreviousData(int previousDataLevel);
    
    public int AncestorLevel { get; }
    public Type? AncestorType { get; }
    public string? AncestorTypeName { get; }
}
```

### 3.4 MultiBinding

```csharp
// Location: src/Avalonia.Base/Data/
public class MultiBinding : BindingBase
{
    public MultiBinding();
    public MultiBinding(string path);
    
    public IList<BindingBase> Bindings { get; }
    public IValueConverter? Converter { get; set; }
    public object? ConverterParameter { get; set; }
    public string? StringFormat { get; set; }
    public object? FallbackValue { get; set; }
    public object? TargetNullValue { get; set; }
}
```

### 3.5 IValueConverter

```csharp
// Location: src/Avalonia.Base/Data/
public interface IValueConverter
{
    object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture);
    object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture);
}

// Common converters
public class BooleanToVisibilityConverter : IValueConverter { }
public class InverseBooleanConverter : IValueConverter { }
public class StringConverters : IValueConverter { }
public class NullableConverter : IValueConverter { }
public class EnumDataProvider : IValueConverter { }
public class ThicknessConverter : IValueConverter { }
public class CornerRadiusConverter : IValueConverter { }
public class VectorConverter : IValueConverter { }
public class PointConverter : IValueConverter { }
public class SizeConverter : IValueConverter { }
public class RectConverter : IValueConverter { }
public class MatrixConverter : IValueConverter { }
public class ThicknessToDoubleConverter : IValueConverter { }
public class VisibilityConverter : IValueConverter { }
public class StringFormatConverter : IValueConverter { }
public class NotConverter : IValueConverter { }
public class AndConverter : IValueConverter { }
public class OrConverter : IValueConverter { }
public class EqualConverter : IValueConverter { }
public class NotEqualConverter : IValueConverter { }
public class GreaterThanConverter : IValueConverter { }
public class GreaterThanOrEqualConverter : IValueConverter { }
public class LessThanConverter : IValueConverter { }
public class LessThanOrEqualConverter : IValueConverter { }
public class AddConverter : IValueConverter { }
public class SubtractConverter : IValueConverter { }
public class MultiplyConverter : IValueConverter { }
public class DivideConverter : IValueConverter { }
public class ModulusConverter : IValueConverter { }
public class PowerConverter : IValueConverter { }
public class SquareRootConverter : IValueConverter { }
public class AbsConverter : IValueConverter { }
public class CeilingConverter : IValueConverter { }
public class FloorConverter : IValueConverter { }
public class RoundConverter : IValueConverter { }
public class TruncateConverter : IValueConverter { }
public class SignConverter : IValueConverter { }
public class LogConverter : IValueConverter { }
public class Log10Converter : IValueConverter { }
public class ExpConverter : IValueConverter { }
public class Exp10Converter : IValueConverter { }
public class SinConverter : IValueConverter { }
public class CosConverter : IValueConverter { }
public class TanConverter : IValueConverter { }
public class AsinConverter : IValueConverter { }
public class AcosConverter : IValueConverter { }
public class AtanConverter : IValueConverter { }
public class SinhConverter : IValueConverter { }
public class CoshConverter : IValueConverter { }
public class TanhConverter : IValueConverter { }
public class DegreesToRadiansConverter : IValueConverter { }
public class RadiansToDegreesConverter : IValueConverter { }
```

### 3.6 DataTemplates

```csharp
// Location: src/Avalonia.Base/Data/
public interface IDataTemplate
{
    bool Match(object? data);
    object? Load(object? data);
    void Unload(object? content);
}

public abstract class DataTemplate : MarkupExtension, IDataTemplate
{
    public DataTemplate();
    public Type? DataType { get; set; }
    public object? Content { get; set; }
    public object? Template { get; set; }
    public object? Resources { get; set; }
    
    public abstract bool Match(object? data);
    public abstract object? Load(object? data);
    public abstract void Unload(object? content);
    public override object? ProvideValue(IServiceProvider serviceProvider);
}

public class DataTemplateSelector : DataTemplate
{
    public DataTemplate? SelectTemplate(object? item, object? container);
    public DataTemplate? SelectTemplate(object? item, object? container, int index);
}

public class HierarchicalDataTemplate : DataTemplate
{
    public IEnumerable? ItemsSourcePath { get; set; }
    public DataTemplate? ItemTemplate { get; set; }
}
```

### 3.7 IGlobalDataTemplates

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

// Usage
Application.Current.DataTemplates.Add(new DataTemplate { DataType = typeof(MyViewModel), Content = new MyView() });
```

### 3.8 Compiled Binding (Avalonia 12)

```csharp
// Location: src/Avalonia.Markup.Xaml/
// x:DataType for compiled bindings
// x:Bind markup extension

public class BindExtension : MarkupExtension
{
    public string? Path { get; set; }
    public Type? DataType { get; set; }
    public BindingMode Mode { get; set; }
    public object? Source { get; set; }
    public IValueConverter? Converter { get; set; }
    public object? ConverterParameter { get; set; }
    public string? StringFormat { get; set; }
    public RelativeSource? RelativeSource { get; set; }
    
    public override object? ProvideValue(IServiceProvider serviceProvider);
}

// x:DataType in XAML
// <TextBox Text="{x:Bind ViewModel.Name, Mode=TwoWay}" x:DataType="vm:MyViewModel" />

// Generated binding expressions
public class BindingExpression<T> : BindingExpressionBase
{
    public T Value { get; }
    public T FallbackValue { get; }
    public T TargetNullValue { get; }
    public bool HasError { get; }
    public Exception? Error { get; }
    public BindingValueType ValueType { get; }
}
```

### 3.9 Observable Collections

```csharp
// Location: src/Avalonia.Base/Collections/
public class ObservableCollection<T> : IList<T>, ICollection<T>, IEnumerable<T>, 
    IEnumerable, INotifyCollectionChanged, INotifyPropertyChanged
{
    public event NotifyCollectionChangedEventHandler? CollectionChanged;
    public event PropertyChangedEventHandler? PropertyChanged;
    
    public void Add(T item);
    public void AddRange(IEnumerable<T> items);
    public void Clear();
    public bool Remove(T item);
    public void RemoveRange(IEnumerable<T> items);
    public void Move(int oldIndex, int newIndex);
    public void Replace(T oldItem, T newItem);
    public void ReplaceRange(IEnumerable<T> oldItems, IEnumerable<T> newItems);
}

// Read-only collection
public class ReadOnlyObservableCollection<T> : ObservableCollection<T>
{
    public ReadOnlyObservableCollection(ObservableCollection<T> source);
}
```

### 3.10 ObservableAsPropertyHelper

```csharp
// Location: src/Avalonia.Base/Reactive/
public class ObservableAsPropertyHelper<T> : IObservable<T>
{
    public T Value { get; }
    public bool IsValueLoaded { get; }
    
    public void SetValueAndRaise<TControl>(
        TControl control,
        ref T field,
        StyledProperty<T> property,
        bool raiseOnCurrentThread = true)
        where TControl : StyledElement;
    
    public void SetValueAndRaise<TControl>(
        TControl control,
        ref T field,
        DirectPropertyBase<T> property,
        bool raiseOnCurrentThread = true)
        where TControl : AvaloniaObject;
    
    public void SetValueAndRaise<TControl>(
        TControl control,
        ref T field,
        Action<T> setter,
        bool raiseOnCurrentThread = true)
        where TControl : object;
}

// Extension methods
public static class ObservableExtensions
{
    public static ObservableAsPropertyHelper<T> ToObservableAsPropertyHelper<T>(
        this IObservable<T> source,
        T defaultValue,
        bool raiseOnCurrentThread = true);
}
```

### 3.11 Bind Extension Methods

```csharp
// Location: src/Avalonia.Base/Data/
public static class BindExtensions
{
    public static IDisposable Bind<TControl, TProperty>(
        this TControl control,
        Expression<Func<TControl, TProperty>> propertyExpression,
        IObservable<TProperty> source)
        where TControl : StyledElement;
    
    public static IDisposable Bind<TControl, TProperty>(
        this TControl control,
        Expression<Func<TControl, TProperty>> propertyExpression,
        IObservable<BindingValue<TProperty>> source)
        where TControl : StyledElement;
    
    public static IDisposable Bind(
        this StyledElement control,
        StyledProperty property,
        IObservable<object?> source);
    
    public static IDisposable Bind(
        this StyledElement control,
        DirectPropertyBase property,
        IObservable<object?> source);
}
```

### 3.12 CompositeDisposable

```csharp
// Location: src/Avalonia.Base/Reactive/
public class CompositeDisposable : IDisposable, IEnumerable<IDisposable>
{
    public int Count { get; }
    public bool IsDisposed { get; }
    
    public void Add(IDisposable disposable);
    public void Remove(IDisposable disposable);
    public void Clear();
    public void Dispose();
    public IEnumerator<IDisposable> GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator();
}

// Extension method
public static class DisposableExtensions
{
    public static CompositeDisposable Add(this CompositeDisposable disposables, IDisposable disposable);
    public static IDisposable Subscribe<T>(this IObservable<T> source, Action<T> onNext);
    public static IDisposable Subscribe<T>(this IObservable<T> source, Action<T> onNext, Action<Exception> onError);
    public static IDisposable Subscribe<T>(this IObservable<T> source, Action<T> onNext, Action onError, Action onCompleted);
}
```

### 3.13 BindingValue

```csharp
// Location: src/Avalonia.Base/Data/
public struct BindingValue<T>
{
    public T Value { get; }
    public bool HasValue { get; }
    public BindingValueType Type { get; }
    public Exception? Error { get; }
    public object? FallbackValue { get; }
    
    public static BindingValue<T> UnsetValue { get; }
    public static BindingValue<T> WithValue(T value);
    public static BindingValue<T> WithFallback(T fallback);
    public static BindingValue<T> WithError(Exception error);
    public static BindingValue<T> WithErrorAndFallback(T fallback, Exception error);
}

public enum BindingValueType
{
    UnsetValue,
    Value,
    BindingError,
    BindingErrorWithFallback,
    DataValidationError,
    DataValidationErrorWithFallback,
}
```

### 3.14 IndexerBinding

```csharp
// Location: src/Avalonia.Base/Data/
public class IndexerBinding : BindingBase
{
    public IndexerBinding(AvaloniaObject target, IndexerDescriptor descriptor);
    
    public AvaloniaObject Target { get; }
    public IndexerDescriptor Descriptor { get; }
}

public class IndexerDescriptor
{
    public string IndexerName { get; }
    public IList<object?> IndexValues { get; }
}
```

### 3.15 BindingExpressionBase

```csharp
// Location: src/Avalonia.Base/Data/
public abstract class BindingExpressionBase
{
    public AvaloniaObject Target { get; }
    public AvaloniaProperty Property { get; }
    public BindingBase Binding { get; }
    public object? Anchor { get; }
    public BindingPriority Priority { get; }
    
    public abstract void Refresh();
    public abstract void UpdateSource();
    public abstract void Detach();
    public abstract IDisposable Subscribe(IObserver<object?> observer);
}

// Typed version
public class BindingExpression<T> : BindingExpressionBase
{
    public T Value { get; }
    public T FallbackValue { get; }
    public T TargetNullValue { get; }
    public bool HasError { get; }
    public Exception? Error { get; }
    public BindingValueType ValueType { get; }
}
```

### 3.16 DataContext

```csharp
// Location: src/Avalonia.Base/IDataContextProvider.cs
public interface IDataContextProvider
{
    object? DataContext { get; }
}

// DataContext is inherited by default
public static readonly StyledProperty<object?> DataContextProperty =
    AvaloniaProperty.Register<StyledElement, object?>(
        nameof(DataContext),
        defaultValue: null,
        inherits: true,
        defaultBindingMode: BindingMode.OneWay);
```

---

## 4. Layout System

### 4.1 ILayout Interface

```csharp
// Location: src/Avalonia.Base/Layout/
public interface ILayout : IVisual
{
    double Width { get; set; }
    double Height { get; set; }
    double MinWidth { get; set; }
    double MaxWidth { get; set; }
    double MinHeight { get; set; }
    double MaxHeight { get; set; }
    Thickness Margin { get; set; }
    Thickness Padding { get; set; }
    Orientation Orientation { get; set; }
    double DesiredSize { get; }
    Rect Bounds { get; }
    Rect LayoutBounds { get; }
    Rect EffectiveBounds { get; }
}
```

### 4.2 Layout Operations

```csharp
// Location: src/Avalonia.Base/Layout/
public interface ILayoutManager
{
    void Measure(Size availableSize);
    void Arrange(Rect finalRect);
    void InvalidateMeasure();
    Size Measure();
    Size Arrange(Rect rect);
}

public static class LayoutExtensions
{
    public static void Measure(this ILayout element, Size availableSize);
    public static void Arrange(this ILayout element, Rect finalRect);
    public static void InvalidateMeasure(this ILayout element);
    public static Size Measure(this ILayout element);
    public static Size Arrange(this ILayout element, Rect rect);
}
```

### 4.3 Measure/Arrange Cycle

```csharp
// Location: src/Avalonia.Base/Layout/
public abstract class Panel : Decorator, ILayoutManager
{
    protected abstract Size MeasureOverride(Size availableSize);
    protected abstract Size ArrangeOverride(Size finalSize);
    
    // Children collection
    public PanelChildren Children { get; }
    
    // Layout events
    public event EventHandler<LayoutEventArgs>? LayoutUpdated;
}

// PanelChildren
public class PanelChildren : IList<Control>, ICollection<Control>, IEnumerable<Control>,
    IEnumerable, INotifyCollectionChanged
{
    public event NotifyCollectionChangedEventHandler? CollectionChanged;
    public void Add(Control child);
    public void Remove(Control child);
    public void Clear();
    public void Insert(int index, Control child);
    public void RemoveAt(int index);
    public Control this[int index] { get; set; }
}
```

### 4.4 Grid

```csharp
// Location: src/Avalonia.Controls/Grid.cs
public class Grid : Panel
{
    // Column/Row definitions
    public ColumnDefinitions ColumnDefinitions { get; set; }
    public RowDefinitions RowDefinitions { get; set; }
    
    // Spacing
    public double ColumnSpacing { get; set; }
    public double RowSpacing { get; set; }
    
    // Grid lines
    public bool ShowGridLines { get; set; }
    
    // Shared size scope
    public bool IsSharedSizeScope { get; set; }
    
    // Attached properties
    public static int GetColumn(Control element);
    public static void SetColumn(Control element, int value);
    public static int GetRow(Control element);
    public static void SetRow(Control element, int value);
    public static int GetColumnSpan(Control element);
    public static void SetColumnSpan(Control element, int value);
    public static int GetRowSpan(Control element);
    public static void SetRowSpan(Control element, int value);
    public static bool GetIsSharedSizeScope(Control element);
    public static void SetIsSharedSizeScope(Control element, bool value);
    
    // Layout
    protected override Size MeasureOverride(Size constraint);
    protected override Size ArrangeOverride(Size arrangeSize);
    
    // Actual sizes
    public double GetFinalColumnDefinitionWidth(int columnIndex);
    public double GetFinalRowDefinitionHeight(int rowIndex);
}

// ColumnDefinitions
public class ColumnDefinitions : DefinitionList<ColumnDefinition> { }

// RowDefinitions
public class RowDefinitions : DefinitionList<RowDefinition> { }

// ColumnDefinition
public class ColumnDefinition : DefinitionBase
{
    public GridLength Width { get; set; }
    public double ActualWidth { get; }
    public static readonly GridLength WidthProperty;
}

// RowDefinition
public class RowDefinition : DefinitionBase
{
    public GridLength Height { get; set; }
    public double ActualHeight { get; }
    public static readonly GridLength HeightProperty;
}

// DefinitionList
public class DefinitionList<T> : IList<T>, ICollection<T>, IEnumerable<T>, IEnumerable
    where T : DefinitionBase
{
    public T this[int index] { get; set; }
    public int Count { get; }
    public void Add(T definition);
    public void Remove(T definition);
    public void Clear();
    public int IndexOf(T definition);
    public void Insert(int index, T definition);
    public void RemoveAt(int index);
    public bool IsDirty { get; set; }
}
```

### 4.5 StackPanel

```csharp
// Location: src/Avalonia.Controls/StackPanel.cs
public class StackPanel : Panel
{
    public Orientation Orientation { get; set; }
    public double Spacing { get; set; }
    
    protected override Size MeasureOverride(Size availableSize);
    protected override Size ArrangeOverride(Size finalSize);
}
```

### 4.6 WrapPanel

```csharp
// Location: src/Avalonia.Controls/WrapPanel.cs
public class WrapPanel : Panel
{
    public Orientation Orientation { get; set; }
    public double ItemWidth { get; set; }
    public double ItemHeight { get; set; }
    public WrapOrientation WrapOrientation { get; set; }
    
    protected override Size MeasureOverride(Size availableSize);
    protected override Size ArrangeOverride(Size finalSize);
}

public enum WrapOrientation { Horizontal, Vertical }
```

### 4.7 DockPanel

```csharp
// Location: src/Avalonia.Controls/DockPanel.cs
public class DockPanel : Panel
{
    public bool LastChildFill { get; set; }
    
    // Attached property
    public static Dock GetDock(UIElement element);
    public static void SetDock(UIElement element, Dock value);
    
    protected override Size MeasureOverride(Size availableSize);
    protected override Size ArrangeOverride(Size finalSize);
}

public enum Dock { Left, Top, Right, Bottom }
```

### 4.8 UniformGrid

```csharp
// Location: src/Avalonia.Controls/UniformGrid.cs
public class UniformGrid : Panel
{
    public int Rows { get; set; }
    public int Columns { get; set; }
    public int FirstColumn { get; set; }
    public int StartingRow { get; set; }
    
    protected override Size MeasureOverride(Size availableSize);
    protected override Size ArrangeOverride(Size finalSize);
}
```

### 4.9 RelativePanel

```csharp
// Location: src/Avalonia.Controls/RelativePanel.cs
public class RelativePanel : Panel
{
    // Attached properties for positioning
    public static bool GetAlignTopWith(UIElement element);
    public static void SetAlignTopWith(UIElement element, bool value);
    public static bool GetAlignBottomWith(UIElement element);
    public static void SetAlignBottomWith(UIElement element, bool value);
    public static bool GetAlignLeftWith(UIElement element);
    public static void SetAlignLeftWith(UIElement element, bool value);
    public static bool GetAlignRightWith(UIElement element);
    public static void SetAlignRightWith(UIElement element, bool value);
    public static bool GetAlignCenterWith(UIElement element);
    public static void SetAlignCenterWith(UIElement element, bool value);
    public static bool GetPlaceAbove(UIElement element);
    public static void SetPlaceAbove(UIElement element, bool value);
    public static bool GetPlaceBelow(UIElement element);
    public static void SetPlaceBelow(UIElement element, bool value);
    public static bool GetLeftOf(UIElement element);
    public static void SetLeftOf(UIElement element, bool value);
    public static bool GetRightOf(UIElement element);
    public static void SetRightOf(UIElement element, bool value);
    public static bool GetAbove(UIElement element);
    public static void SetAbove(UIElement element, bool value);
    public static bool GetBelow(UIElement element);
    public static void SetBelow(UIElement element, bool value);
    
    // Relative panel references
    public static UIElement? GetAlignTopWithTarget(UIElement element);
    public static UIElement? GetAlignBottomWithTarget(UIElement element);
    public static UIElement? GetAlignLeftWithTarget(UIElement element);
    public static UIElement? GetAlignRightWithTarget(UIElement element);
    public static UIElement? GetAlignCenterWithTarget(UIElement element);
    public static UIElement? GetPlaceAboveTarget(UIElement element);
    public static UIElement? GetPlaceBelowTarget(UIElement element);
    public static UIElement? GetLeftOfTarget(UIElement element);
    public static UIElement? GetRightOfTarget(UIElement element);
    public static UIElement? GetAboveTarget(UIElement element);
    public static UIElement? GetBelowTarget(UIElement element);
    
    protected override Size MeasureOverride(Size availableSize);
    protected override Size ArrangeOverride(Size finalSize);
}
```

### 4.10 Canvas

```csharp
// Location: src/Avalonia.Controls/Canvas.cs
public class Canvas : Panel
{
    // Attached properties
    public static double GetLeft(UIElement element);
    public static void SetLeft(UIElement element, double value);
    public static double GetTop(UIElement element);
    public static void SetTop(UIElement element, double value);
    public static double GetRight(UIElement element);
    public static void SetRight(UIElement element, double value);
    public static double GetBottom(UIElement element);
    public static void SetBottom(UIElement element, double value);
    
    protected override Size MeasureOverride(Size availableSize);
    protected override Size ArrangeOverride(Size finalSize);
}
```

### 4.11 Viewbox

```csharp
// Location: src/Avalonia.Controls/Viewbox.cs
public class Viewbox : Decorator
{
    public Stretch Stretch { get; set; }
    public StretchDirection StretchDirection { get; set; }
    
    protected override Size MeasureOverride(Size availableSize);
    protected override Size ArrangeOverride(Size finalSize);
}

public enum Stretch { Fill, Uniform, UniformToFill, None }
public enum StretchDirection { UpAndDown, Up, Down }
```

### 4.12 Border

```csharp
// Location: src/Avalonia.Controls/Border.cs
public class Border : Decorator
{
    public Brush? Background { get; set; }
    public Brush? BorderBrush { get; set; }
    public Thickness BorderThickness { get; set; }
    public CornerRadius CornerRadius { get; set; }
    public double Padding { get; set; }
    
    protected override Size MeasureOverride(Size availableSize);
    protected override Size ArrangeOverride(Size finalSize);
}
```

### 4.13 ScrollViewer

```csharp
// Location: src/Avalonia.Controls/ScrollViewer.cs
public class ScrollViewer : Decorator
{
    public ScrollOrientation ScrollOrientation { get; set; }
    public ScrollBarVisibility VerticalScrollBarVisibility { get; set; }
    public ScrollBarVisibility HorizontalScrollBarVisibility { get; set; }
    public Size Extent { get; }
    public Size Viewport { get; }
    public Point Offset { get; set; }
    public bool CanContentScroll { get; set; }
    public bool CanHorizontallyScroll { get; set; }
    public bool CanVerticallyScroll { get; set; }
    public double HorizontalOffset { get; }
    public double VerticalOffset { get; }
    public double HorizontalScrollBarMaximum { get; }
    public double HorizontalScrollBarMinimum { get; }
    public double VerticalScrollBarMaximum { get; }
    public double VerticalScrollBarMinimum { get; }
    
    public void LineUp();
    public void LineDown();
    public void LineLeft();
    public void LineRight();
    public void PageUp();
    public void PageDown();
    public void PageLeft();
    public void PageRight();
    public void ScrollToTop();
    public void ScrollToBottom();
    public void ScrollToLeft();
    public void ScrollToRight();
    public void ScrollToHorizontalOffset(double offset);
    public void ScrollToVerticalOffset(double offset);
    
    protected override Size MeasureOverride(Size availableSize);
    protected override Size ArrangeOverride(Size finalSize);
}

public enum ScrollOrientation { Both, Vertical, Horizontal }
public enum ScrollBarVisibility { Disabled, Enabled, Auto, Visible }
```

### 4.14 VirtualizingStackPanel

```csharp
// Location: src/Avalonia.Controls/VirtualizingStackPanel.cs
public class VirtualizingStackPanel : VirtualizingPanel
{
    public Orientation Orientation { get; set; }
    public double Spacing { get; set; }
    
    protected override Size MeasureOverride(Size availableSize);
    protected override Size ArrangeOverride(Size finalSize);
}

// VirtualizingPanel
public abstract class VirtualizingPanel : Panel
{
    public static readonly AttachedProperty<bool> IsItemsHostProperty;
    public static bool GetIsItemsHost(UIElement element);
    public static void SetIsItemsHost(UIElement element, bool value);
}
```

### 4.15 GridSplitter

```csharp
// Location: src/Avalonia.Controls/GridSplitter.cs
public class GridSplitter : Control
{
    public GridLength Width { get; set; }
    public GridLength Height { get; set; }
    public Dock Dock { get; set; }
    public ResizeDirection ResizeDirection { get; set; }
    public ResizeBehavior ResizeBehavior { get; set; }
    
    protected override Size MeasureOverride(Size availableSize);
    protected override Size ArrangeOverride(Size finalSize);
}

public enum ResizeDirection { Rows, Columns, Both }
public enum ResizeBehavior { PreviousAndCurrent, CurrentAndNext, Previous, Next, Current }
```

### 4.16 GridLength

```csharp
// Location: src/Avalonia.Controls/GridLength.cs
public struct GridLength : IEquatable<GridLength>, IFormattable
{
    public double Value { get; }
    public GridUnitType GridUnitType { get; }
    public bool IsStar { get; }
    public bool IsAuto { get; }
    public bool IsAbsolute { get; }
    
    public static GridLength Auto { get; }
    public static GridLength Star { get; }
    public static GridLength Pixel { get; }
    
    public GridLength(double value, GridUnitType gridUnitType);
    public static GridLength Parse(string input);
    public string ToString(IFormatProvider? provider);
    public string FormatToString();
}

public enum GridUnitType { Pixel, Auto, Star }
```

### 4.17 LayoutHelper & LayoutInformation

```csharp
// Location: src/Avalonia.Base/Layout/
public static class LayoutHelper
{
    public static Size ClampSize(Size size, double minWidth, double maxWidth, double minHeight, double maxHeight);
    public static Size Expand(Size size, Thickness margin, Thickness padding);
    public static Size Contract(Size size, Thickness margin, Thickness padding);
    public static Rect ArrangeWithConstraints(Rect bounds, ILayout child);
    public static Size MeasureChild(ILayout child, Size availableSize);
}

public static class LayoutInformation
{
    public static Size GetDesiredSize(UIElement element);
    public static Rect GetLayoutBounds(UIElement element);
    public static Rect GetEffectiveBounds(UIElement element);
    public static Size GetLayoutSlot(UIElement element, UIElement parent);
    public static Size GetLayoutSlot(UIElement element, Panel panel);
}
```

---

## 5. Controls

### 5.1 Decorators & Shapes

#### Decorator Base Class

```csharp
// Location: src/Avalonia.Controls/Decorator.cs
public class Decorator : Control
{
    // Decorators have a single child with no layout management
    public UIElement? Child { get; set; }
    
    protected override Size MeasureOverride(Size availableSize);
    protected override Size ArrangeOverride(Size finalSize);
}
```

#### Border

```csharp
// Location: src/Avalonia.Controls/Border.cs
public class Border : Decorator
{
    public Brush? Background { get; set; }
    public Brush? BorderBrush { get; set; }
    public Thickness BorderThickness { get; set; }
    public CornerRadius CornerRadius { get; set; }
    protected override void OnRender(DrawingContext context);
}
```

#### BoxView

```csharp
// Location: src/Avalonia.Controls/BoxView.cs
public class BoxView : Decorator
{
    public Color Color { get; set; }
    public Brush? Background { get; set; }
    protected override void OnRender(DrawingContext context);
}
```

#### Viewbox & Viewbox2D

```csharp
// Location: src/Avalonia.Controls/Viewbox.cs
public class Viewbox : Decorator
{
    public Stretch Stretch { get; set; }
    public StretchDirection StretchDirection { get; set; }
    protected override Size MeasureOverride(Size availableSize);
    protected override Size ArrangeOverride(Size finalSize);
}

// Location: src/Avalonia.Controls/Viewbox2D.cs
public class Viewbox2D : Decorator
{
    public Stretch Stretch { get; set; }
    public StretchDirection StretchDirection { get; set; }
    protected override Size MeasureOverride(Size availableSize);
    protected override Size ArrangeOverride(Size finalSize);
}

// Location: src/Avalonia.Controls/Viewbox2DWithScrolling.cs
public class Viewbox2DWithScrolling : Decorator
{
    public Stretch Stretch { get; set; }
    public StretchDirection StretchDirection { get; set; }
    public ScrollBarVisibility VerticalScrollBarVisibility { get; set; }
    public ScrollBarVisibility HorizontalScrollBarVisibility { get; set; }
}
```

#### Shape

```csharp
// Location: src/Avalonia.Base/Media/Shape.cs
public abstract class Shape : FrameworkElement
{
    public Brush? Fill { get; set; }
    public Brush? Stroke { get; set; }
    public double StrokeThickness { get; set; }
    public PenLineCap StrokeLineCap { get; set; }
    public PenLineJoin StrokeLineJoin { get; set; }
    public double[]? StrokeDashArray { get; set; }
    public double StrokeDashOffset { get; set; }
    public double StrokeMiterLimit { get; set; }
    
    protected override void OnRender(DrawingContext context);
    protected abstract Geometry DefiningGeometry { get; }
}
```

#### Rectangle, Ellipse, Line, Polygon, Polyline

```csharp
// Location: src/Avalonia.Base/Media/
public class Rectangle : Shape { public CornerRadius CornerRadius { get; set; } }
public class Ellipse : Shape { }
public class Line : Shape { public Point StartPoint { get; set; } public Point EndPoint { get; set; } }
public class Polygon : Shape { public PointCollection Points { get; set; } public Stretch Stretch { get; set; } }
public class Polyline : Shape { public PointCollection Points { get; set; } public Stretch Stretch { get; set; } }
```

#### IconElement & PathIcon

```csharp
// Location: src/Avalonia.Controls/IconElement.cs
public abstract class IconElement : FrameworkElement
{
    public Geometry Geometry { get; set; }
    public Brush? Foreground { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public double MinWidth { get; set; }
    public double MinHeight { get; set; }
    public double MaxWidth { get; set; }
    public double MaxHeight { get; set; }
    protected override void OnRender(DrawingContext context);
}

// Location: src/Avalonia.Controls/PathIcon.cs
public class PathIcon : IconElement
{
    public static readonly StyledProperty<Geometry> GeometryProperty;
    public Geometry Geometry { get; set; }
}
```

#### ExperimentalAcrylicBorder

```csharp
// Location: src/Avalonia.Controls/ExperimentalAcrylicBorder.cs
public class ExperimentalAcrylicBorder : Decorator
{
    public AcrylicMaterial? Material { get; set; }
    public AcrylicPlatformCompensationLevels CompensationLevels { get; set; }
    protected override void OnRender(DrawingContext context);
}

public class AcrylicMaterial
{
    public double NoiseOpacity { get; set; }
    public double FallbackColorOpacity { get; set; }
    public Color BaseColor { get; set; }
    public Color TintColor { get; set; }
    public double TintOpacity { get; set; }
    public Uri? Source { get; set; }
}

public class AcrylicPlatformCompensationLevels
{
    public double Noise { get; set; }
    public double Fallback { get; set; }
    public double Tint { get; set; }
}
```

### 5.2 Content Controls

#### ContentControl

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

#### TextBlock

```csharp
// Location: src/Avalonia.Controls/TextBlock.cs
public class TextBlock : ContentControl
{
    public string? Text { get; set; }
    public IList<Inline>? Inlines { get; }
    public FontFamily? FontFamily { get; set; }
    public double FontSize { get; set; }
    public FontWeight FontWeight { get; set; }
    public FontStyle FontStyle { get; set; }
    public TextAlignment TextAlignment { get; set; }
    public TextWrapping TextWrapping { get; set; }
    public double LineHeight { get; set; }
    public Brush? Foreground { get; set; }
    public Thickness Padding { get; set; }
    public Thickness Margin { get; set; }
    public double Opacity { get; set; }
    public double OpacityMask { get; set; }
    public Brush? Background { get; set; }
    public Brush? BorderBrush { get; set; }
    public Thickness BorderThickness { get; set; }
    
    public event EventHandler<TextChangedEventArgs>? TextChanged;
    public event EventHandler<TextSelectionChangedEventArgs>? TextSelectionChanged;
}
```

#### TextBox

```csharp
// Location: src/Avalonia.Controls/TextBox.cs
public class TextBox : ContentControl
{
    public string? Text { get; set; }
    public string? Watermark { get; set; }
    public int MaxLength { get; set; }
    public TextWrapping TextWrapping { get; set; }
    public bool IsReadOnly { get; set; }
    public int SelectionStart { get; set; }
    public int SelectionLength { get; set; }
    public Rect SelectionRect { get; }
    public bool IsTextSelectionEnabled { get; set; }
    public bool IsReadOnlyCaretVisible { get; set; }
    public double LineHeight { get; set; }
    public FontFamily? FontFamily { get; set; }
    public double FontSize { get; set; }
    public FontWeight FontWeight { get; set; }
    public FontStyle FontStyle { get; set; }
    public TextAlignment TextAlignment { get; set; }
    public TextDecoration TextDecoration { get; set; }
    public Brush? Foreground { get; set; }
    public Brush? Background { get; set; }
    public Brush? BorderBrush { get; set; }
    public Thickness BorderThickness { get; set; }
    public Thickness Padding { get; set; }
    
    public event EventHandler<TextChangedEventArgs>? TextChanged;
    public event EventHandler<TextSelectionChangedEventArgs>? SelectionChanged;
    public event EventHandler<TextInputEventArgs>? TextInput;
    
    public void Select(int start, int length);
    public void SelectAll();
    public void Clear();
    public void Copy();
    public void Cut();
    public void Paste();
    public void Undo();
    public void Redo();
}
```

#### PasswordBox

```csharp
// Location: src/Avalonia.Controls/PasswordBox.cs
public class PasswordBox : ContentControl
{
    public string Password { get; set; }
    public string Watermark { get; set; }
    public char PasswordChar { get; set; }
    public bool IsPasswordRevealEnabled { get; set; }
    public FontFamily? FontFamily { get; set; }
    public double FontSize { get; set; }
    public FontWeight FontWeight { get; set; }
    public TextAlignment TextAlignment { get; set; }
    public Brush? Foreground { get; set; }
    public Brush? Background { get; set; }
    public Brush? BorderBrush { get; set; }
    public Thickness BorderThickness { get; set; }
    
    public event EventHandler<PasswordChangedEventArgs>? PasswordChanged;
}
```

#### Image

```csharp
// Location: src/Avalonia.Controls/Image.cs
public class Image : ContentControl
{
    public Bitmap? Source { get; set; }
    public Stretch Stretch { get; set; }
    public StretchDirection StretchDirection { get; set; }
    public double Opacity { get; set; }
    public Rect Extent { get; }
    public Rect Viewport { get; }
    public event EventHandler<Exception>? LoadFailed;
}
```

#### Label

```csharp
// Location: src/Avalonia.Controls/Label.cs
public class Label : ContentControl
{
    public UIElement? Target { get; set; }
    public object? Content { get; set; }
    public DataTemplate? ContentTemplate { get; set; }
}
```

#### Expander

```csharp
// Location: src/Avalonia.Controls/Expander.cs
public class Expander : HeaderedContentControl
{
    public object? Header { get; set; }
    public DataTemplate? HeaderTemplate { get; set; }
    public object? Content { get; set; }
    public DataTemplate? ContentTemplate { get; set; }
    public bool IsExpanded { get; set; }
    public ExpandDirection ExpandDirection { get; set; }
    public bool HeaderIsExpanded { get; }
    protected override void OnApplyTemplate(ApplyTemplateEventArgs e);
}

public enum ExpandDirection { Down, Up, Left, Right }
```

#### ProgressBar

```csharp
// Location: src/Avalonia.Controls/ProgressBar.cs
public class ProgressBar : ContentControl
{
    public double Value { get; set; }
    public double Minimum { get; set; }
    public double Maximum { get; set; }
    public bool IsIndeterminate { get; set; }
    public ProgressBarStyle ProgressBarStyle { get; set; }
    public double MinimumWidth { get; set; }
    public double MaximumWidth { get; set; }
    public double MinimumHeight { get; set; }
    public double MaximumHeight { get; set; }
    
    public event EventHandler<ProgressBarValueChangedEventArgs>? ValueChanged;
}

public enum ProgressBarStyle { Default, Determinate, Indeterminate }
```

#### ProgressRing

```csharp
// Location: src/Avalonia.Controls/ProgressRing.cs
public class ProgressRing : ContentControl
{
    public bool IsActive { get; set; }
    protected override void OnApplyTemplate(ApplyTemplateEventArgs e);
}
```

#### LayoutTransformControl

```csharp
// Location: src/Avalonia.Controls/LayoutTransformControl.cs
public class LayoutTransformControl : ContentControl
{
    public Transform LayoutTransform { get; set; }
}
```

#### TransitioningContentControl

```csharp
// Location: src/Avalonia.Controls/TransitioningContentControl.cs
public class TransitioningContentControl : ContentControl
{
    public IPageTransition? Transition { get; set; }
    public TimeSpan TransitionDuration { get; set; }
    public int OpenedTransitionCount { get; }
    public int ClosedTransitionCount { get; }
    protected override void OnApplyTemplate(ApplyTemplateEventArgs e);
}
```

#### FlipView

```csharp
// Location: src/Avalonia.Controls/FlipView.cs
public class FlipView : ItemsControl
{
    public object? SelectedItem { get; set; }
    public int SelectedIndex { get; set; }
    public bool IsLoopEnabled { get; set; }
    protected override void OnApplyTemplate(ApplyTemplateEventArgs e);
}
```

#### InfoBanner

```csharp
// Location: src/Avalonia.Controls/InfoBanner.cs
public class InfoBanner : ContentControl
{
    public InfoBannerType Type { get; set; }
    public object? Content { get; set; }
    public DataTemplate? ContentTemplate { get; set; }
    public bool IsClosable { get; set; }
    public bool IsCloseButtonVisible { get; set; }
    public event EventHandler<InfoBannerClosedEventArgs>? Closed;
}

public enum InfoBannerType { Information, Warning, Error, Success }
```

#### SelectableTextBlock

```csharp
// Location: src/Avalonia.Controls/SelectableTextBlock.cs
public class SelectableTextBlock : TextBlock
{
    public bool IsTextSelectionEnabled { get; set; }
    public bool IsReadOnlyCaretVisible { get; set; }
    public bool IsEnabled { get; set; }
    public void Select(int start, int length);
    public void SelectAll();
    public void Copy();
    public void Cut();
    public void Paste();
}
```

#### AvaloniaEditor

```csharp
// Location: src/Avalonia.Controls/AvaloniaEditor.cs
public class AvaloniaEditor : TextBox
{
    public string? Text { get; set; }
    public bool IsReadOnly { get; set; }
    public bool IsWordWrapEnabled { get; set; }
    public int TabSize { get; set; }
    public bool ShowLineNumbers { get; set; }
    public bool ShowColumnNumbers { get; set; }
    public bool ShowWhitespace { get; set; }
    public bool ShowVirtualSpace { get; set; }
    public bool ShowFoldMargin { get; set; }
    public int FoldMarginWidth { get; set; }
    public bool IsSyntaxHighlightingEnabled { get; set; }
    public string? Language { get; set; }
    
    public event EventHandler<TextChangedEventArgs>? TextChanged;
    public event EventHandler<TextSelectionChangedEventArgs>? SelectionChanged;
    public event EventHandler<TextInputEventArgs>? TextInput;
}
```

### 5.3 Input Controls

#### Button

```csharp
// Location: src/Avalonia.Controls/Button.cs
public class Button : ButtonBase
{
    public object? Content { get; set; }
    public DataTemplate? ContentTemplate { get; set; }
    public DataTemplateSelector? ContentTemplateSelector { get; set; }
    public ICommand? Command { get; set; }
    public object? CommandParameter { get; set; }
    public IInputElement? CommandTarget { get; set; }
    
    public event EventHandler<RoutedEventHandler>? Click;
    public event EventHandler<PointerPressedEventArgs>? PointerPressed;
    public event EventHandler<PointerReleasedEventArgs>? PointerReleased;
    
    public void ExecuteCommand();
}
```

#### ButtonBase

```csharp
// Location: src/Avalonia.Controls/ButtonBase.cs
public abstract class ButtonBase : ContentControl
{
    public bool IsPressed { get; }
    public RoutedEvent<RoutedEventArgs> ClickEvent { get; }
    protected override void OnPointerPressed(PointerPressedEventArgs e);
    protected override void OnPointerReleased(PointerReleasedEventArgs e);
    protected abstract void OnClick();
}
```

#### RepeatButton

```csharp
// Location: src/Avalonia.Controls/RepeatButton.cs
public class RepeatButton : ButtonBase
{
    public double Interval { get; set; }
    public double Delay { get; set; }
    public event EventHandler<RoutedEventHandler>? Click;
}
```

#### ToggleButton

```csharp
// Location: src/Avalonia.Controls/ToggleButton.cs
public class ToggleButton : ToggleButtonBase
{
    public bool IsChecked { get; set; }
    public ICommand? Command { get; set; }
    public object? CommandParameter { get; set; }
    
    public event EventHandler<RoutedEventHandler>? Checked;
    public event EventHandler<RoutedEventHandler>? Unchecked;
    public event EventHandler<RoutedEventHandler>? Indeterminate;
}
```

#### ToggleButtonBase

```csharp
// Location: src/Avalonia.Controls/ToggleButtonBase.cs
public abstract class ToggleButtonBase : ButtonBase
{
    public bool IsChecked { get; set; }
    public ICommand? Command { get; set; }
    public object? CommandParameter { get; set; }
    
    public event EventHandler<RoutedEventArgs>? Checked;
    public event EventHandler<RoutedEventArgs>? Unchecked;
    public event EventHandler<RoutedEventArgs>? Indeterminate;
    protected override void OnClick();
}
```

#### CheckBox

```csharp
// Location: src/Avalonia.Controls/CheckBox.cs
public class CheckBox : ToggleButton
{
    public object? Content { get; set; }
    public DataTemplate? ContentTemplate { get; set; }
    public ICommand? Command { get; set; }
    public object? CommandParameter { get; set; }
}
```

#### RadioButton

```csharp
// Location: src/Avalonia.Controls/RadioButton.cs
public class RadioButton : ToggleButton
{
    public string? GroupName { get; set; }
    public object? Content { get; set; }
    public DataTemplate? ContentTemplate { get; set; }
    public ICommand? Command { get; set; }
    public object? CommandParameter { get; set; }
    protected override void OnClick();
}
```

#### ComboBox

```csharp
// Location: src/Avalonia.Controls/ComboBox.cs
public class ComboBox : ItemsControl
{
    public object? SelectedItem { get; set; }
    public int SelectedIndex { get; set; }
    public bool IsDropDownOpen { get; set; }
    public bool IsEditable { get; set; }
    public string? Text { get; set; }
    public string? Watermark { get; set; }
    public int MaxDropDownHeight { get; set; }
    public int MinDropDownHeight { get; set; }
    public int MaxDropDownWidth { get; set; }
    public int MinDropDownWidth { get; set; }
    public bool IsReadOnly { get; set; }
    public bool IsTextSearchEnabled { get; set; }
    public string? TextSearchPath { get; set; }
    
    public event EventHandler<SelectionChangedEventArgs>? SelectionChanged;
    public event EventHandler<ComboBoxOpenedEventArgs>? Opened;
    public event EventHandler<ComboBoxClosedEventArgs>? Closed;
    public event EventHandler<ComboBoxTextChangedEventArgs>? TextChanged;
    
    public void Open();
    public void Close();
}
```

#### ComboBoxItem

```csharp
// Location: src/Avalonia.Controls/ComboBoxItem.cs
public class ComboBoxItem : ContentControl
{
    public bool IsSelected { get; }
}
```

#### Slider

```csharp
// Location: src/Avalonia.Controls/Slider.cs
public class Slider : RangeBase
{
    public double Value { get; set; }
    public double Minimum { get; set; }
    public double Maximum { get; set; }
    public double TickFrequency { get; set; }
    public bool IsSnapToTickEnabled { get; set; }
    public Orientation Orientation { get; set; }
    public bool IsDirectionReversed { get; set; }
    public TickPlacement TickPlacement { get; set; }
    public IList<double>? TickPositions { get; }
    public Brush? Fill { get; set; }
    public Brush? ThumbFill { get; set; }
    public Brush? TrackFill { get; set; }
    public Brush? TickBarFill { get; set; }
    public Thickness ThumbBorderThickness { get; set; }
    public CornerRadius ThumbCornerRadius { get; set; }
    public Thickness TrackBorderThickness { get; set; }
    public CornerRadius TrackCornerRadius { get; set; }
    
    public event EventHandler<RangeBaseValueChangedEventArgs>? ValueChanged;
}

public enum TickPlacement { None, BottomOrLeft, TopOrRight, Both }
```

#### RangeSlider

```csharp
// Location: src/Avalonia.Controls/RangeSlider.cs
public class RangeSlider : Slider
{
    public double LowerValue { get; set; }
    public double UpperValue { get; set; }
    public IList<double>? TickPositions { get; }
    
    public event EventHandler<RangeBaseValueChangedEventArgs>? LowerValueChanged;
    public event EventHandler<RangeBaseValueChangedEventArgs>? UpperValueChanged;
}
```

#### Rating

```csharp
// Location: src/Avalonia.Controls/Rating.cs
public class Rating : ContentControl
{
    public double Value { get; set; }
    public int ItemCount { get; set; }
    public bool IsReadOnly { get; set; }
    public bool AllowPartialRatings { get; set; }
    public RatingItemTemplateSelector? ItemTemplateSelector { get; set; }
    public DataTemplate? ItemTemplate { get; set; }
    public Brush? FilledIcon { get; set; }
    public Brush? EmptyIcon { get; set; }
    public Brush? HalfIcon { get; set; }
    public double ItemSize { get; set; }
    public double ItemSpacing { get; set; }
    
    public event EventHandler<RatingChangedEventArgs>? RatingChanged;
}
```

#### RatingItem

```csharp
// Location: src/Avalonia.Controls/RatingItem.cs
public class RatingItem : ContentControl
{
    public int Index { get; }
    public bool IsFilled { get; }
    public bool IsHalfFilled { get; }
    public bool IsSelected { get; }
}
```

#### NumericUpDown

```csharp
// Location: src/Avalonia.Controls/NumericUpDown.cs
public class NumericUpDown : ContentControl
{
    public double Value { get; set; }
    public double Minimum { get; set; }
    public double Maximum { get; set; }
    public double Increment { get; set; }
    public int DecimalPlaces { get; set; }
    public string? Prefix { get; set; }
    public string? Suffix { get; set; }
    public bool IsReadOnly { get; set; }
    public bool IsIncrementByMouseWheel { get; set; }
    public bool IsSnapToInterval { get; set; }
    public bool ShowButtonSpinner { get; set; }
    
    public event EventHandler<NumericUpDownValueChangedEventArgs>? ValueChanged;
}
```

#### DecimalUpDown

```csharp
// Location: src/Avalonia.Controls/DecimalUpDown.cs
public class DecimalUpDown : NumericUpDown
{
    public int DecimalPlaces { get; set; }
    public string? Prefix { get; set; }
    public string? Suffix { get; set; }
}
```

#### DecimalNumPicker

```csharp
// Location: src/Avalonia.Controls/DecimalNumPicker.cs
public class DecimalNumPicker : NumericUpDown
{
    public int DecimalPlaces { get; set; }
    public string? Prefix { get; set; }
    public string? Suffix { get; set; }
}
```

#### MaskedTextBox

```csharp
// Location: src/Avalonia.Controls/MaskedTextBox.cs
public class MaskedTextBox : TextBox
{
    public string? Mask { get; set; }
    public string? PromptChar { get; set; }
    public string? Text { get; set; }
    public bool IncludeLiterals { get; set; }
    public bool SkipLiterals { get; set; }
    public MaskedTextProvider? MaskedText { get; }
    
    public event EventHandler<MaskedTextChangedEventArgs>? MaskedTextChanged;
}
```

#### Calendar

```csharp
// Location: src/Avalonia.Controls/Calendar.cs
public class Calendar : ContentControl
{
    public DateTime? SelectedDate { get; set; }
    public IList<DateTime> SelectedDates { get; }
    public DateTime DisplayDate { get; set; }
    public DateTime DisplayDateEnd { get; set; }
    public DateTime DisplayDateStart { get; set; }
    public CalendarSelectionMode SelectionMode { get; set; }
    public DateTime? BlackoutDates { get; }
    public bool IsTodayHighlighted { get; set; }
    public CalendarDayItemTemplateSelector? DayItemTemplateSelector { get; set; }
    public DataTemplate? DayItemTemplate { get; set; }
    public DataTemplate? HeaderTemplate { get; set; }
    
    public event EventHandler<CalendarSelectedDateChangedEventArgs>? SelectedDateChanged;
    public event EventHandler<CalendarSelectionChangedEventArgs>? SelectionChanged;
}

public enum CalendarSelectionMode { SingleDate, SingleRange, Multiple }
```

#### DatePicker

```csharp
// Location: src/Avalonia.Controls/DatePicker.cs
public class DatePicker : ContentControl
{
    public DateTime? SelectedDate { get; set; }
    public string? Watermark { get; set; }
    public string? DisplayDateStart { get; set; }
    public string? DisplayDateEnd { get; set; }
    public bool IsDropDownOpen { get; set; }
    public bool IsTodayHighlighted { get; set; }
    public string? StringFormat { get; set; }
    
    public event EventHandler<DatePickerValueChangedEventArgs>? SelectedDateChanged;
    public event EventHandler<DatePickerOpenedEventArgs>? Opened;
    public event EventHandler<DatePickerClosedEventArgs>? Closed;
}
```

#### TimePicker

```csharp
// Location: src/Avalonia.Controls/TimePicker.cs
public class TimePicker : ContentControl
{
    public TimeSpan? SelectedTime { get; set; }
    public string? Watermark { get; set; }
    public bool IsDropDownOpen { get; set; }
    public string? StringFormat { get; set; }
    public int MinuteIncrement { get; set; }
    public int HourIncrement { get; set; }
    
    public event EventHandler<TimePickerValueChangedEventArgs>? SelectedTimeChanged;
    public event EventHandler<TimePickerOpenedEventArgs>? Opened;
    public event EventHandler<TimePickerClosedEventArgs>? Closed;
}
```

#### CalendarDatePicker

```csharp
// Location: src/Avalonia.Controls/CalendarDatePicker.cs
public class CalendarDatePicker : ContentControl
{
    public DateTime? SelectedDate { get; set; }
    public string? Watermark { get; set; }
    public bool IsDropDownOpen { get; set; }
    public string? StringFormat { get; set; }
    
    public event EventHandler<CalendarDatePickerValueChangedEventArgs>? SelectedDateChanged;
    public event EventHandler<CalendarDatePickerOpenedEventArgs>? Opened;
    public event EventHandler<CalendarDatePickerClosedEventArgs>? Closed;
}
```

#### DateTimePicker

```csharp
// Location: src/Avalonia.Controls/DateTimePicker.cs
public class DateTimePicker : ContentControl
{
    public DateTime? SelectedDateTime { get; set; }
    public string? Watermark { get; set; }
    public bool IsDropDownOpen { get; set; }
    public string? StringFormat { get; set; }
    
    public event EventHandler<DateTimePickerValueChangedEventArgs>? SelectedDateTimeChanged;
}
```

#### TickBar

```csharp
// Location: src/Avalonia.Controls/TickBar.cs
public class TickBar : ContentControl
{
    public TickPlacement Placement { get; set; }
    public double TickFrequency { get; set; }
    public IList<double>? TickPositions { get; }
}
```

#### ButtonSpinner

```csharp
// Location: src/Avalonia.Controls/ButtonSpinner.cs
public class ButtonSpinner : ContentControl
{
    public object? Content { get; set; }
    public DataTemplate? ContentTemplate { get; set; }
    public bool IsDirectionReversed { get; set; }
    public bool ShowButtonSpinner { get; set; }
    
    public event EventHandler<RoutedEventHandler>? Click;
}
```

#### DropDownButton

```csharp
// Location: src/Avalonia.Controls/DropDownButton.cs
public class DropDownButton : ContentControl
{
    public object? Content { get; set; }
    public DataTemplate? ContentTemplate { get; set; }
    public bool IsDropDownOpen { get; set; }
    
    public event EventHandler<DropDownOpenedEventArgs>? Opened;
    public event EventHandler<DropDownClosedEventArgs>? Closed;
}
```

#### SplitButton

```csharp
// Location: src/Avalonia.Controls/SplitButton.cs
public class SplitButton : ContentControl
{
    public object? Content { get; set; }
    public DataTemplate? ContentTemplate { get; set; }
    public bool IsDropDownOpen { get; set; }
    
    public event EventHandler<SplitButtonClickedEventArgs>? Clicked;
    public event EventHandler<DropDownOpenedEventArgs>? Opened;
    public event EventHandler<DropDownClosedEventArgs>? Closed;
}
```

### 5.4 Items Controls

#### ItemsControl

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
    
    // Header
    public object? Header { get; set; }
    public DataTemplate? HeaderTemplate { get; set; }
    
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

#### ItemCollection

```csharp
// Location: src/Avalonia.Controls/ItemCollection.cs
public class ItemCollection : IList, ICollection, IEnumerable
{
    public object? this[int index] { get; set; }
    public int Count { get; }
    public bool IsReadOnly { get; }
    public bool IsFixedSize { get; }
    
    public void Add(object? item);
    public void AddRange(IEnumerable items);
    public void Remove(object? item);
    public void RemoveAt(int index);
    public void Clear();
    public int IndexOf(object? item);
    public void Insert(int index, object? item);
    public void RemoveRange(int index, int count);
    public void Move(int oldIndex, int newIndex);
    public void Replace(object? oldItem, object? newItem);
    public void ReplaceRange(IEnumerable oldItems, IEnumerable newItems);
}
```

#### ItemsSourceView

```csharp
// Location: src/Avalonia.Controls/ItemsSourceView.cs
public class ItemsSourceView : IList, ICollection, IEnumerable, INotifyCollectionChanged, INotifyPropertyChanged
{
    public object? this[int index] { get; }
    public int Count { get; }
    public bool IsReadOnly { get; }
    
    public event NotifyCollectionChangedEventHandler? CollectionChanged;
    public event PropertyChangedEventHandler? PropertyChanged;
    
    public void Refresh();
    public void Refresh(IList? source);
}
```

#### ItemContainerGenerator

```csharp
// Location: src/Avalonia.Controls/ItemContainerGenerator.cs
public class ItemContainerGenerator
{
    public int ContainerCount { get; }
    public IList Containers { get; }
    
    public void GenerateContainers();
    public void Clear();
    public void Refresh();
    public UIElement? GetContainerAt(int index);
    public int GetIndexAt(UIElement container);
    public void Move(int oldIndex, int newIndex);
    public void Remove(int index, int count);
    public void Insert(int index, int count);
}
```

#### ListBox

```csharp
// Location: src/Avalonia.Controls/ListBox.cs
public class ListBox : ItemsControl
{
    public SelectionMode SelectionMode { get; set; }
    public IList SelectedItems { get; }
    public int SelectedIndex { get; set; }
    public object? SelectedItem { get; set; }
    
    public event EventHandler<SelectionChangedEventArgs>? SelectionChanged;
}
```

#### ListBoxItem

```csharp
// Location: src/Avalonia.Controls/ListBoxItem.cs
public class ListBoxItem : ContentControl
{
    public bool IsSelected { get; set; }
    public bool IsFocused { get; }
    public bool IsKeyboardFocused { get; }
}
```

#### ListView

```csharp
// Location: src/Avalonia.Controls/ListView.cs
public class ListView : ListBox
{
    public bool IsVirtualizing { get; set; }
    public bool IsGroupingEnabled { get; set; }
    public bool IsSortingEnabled { get; set; }
    public bool IsColumnResizingEnabled { get; set; }
    public bool IsColumnReorderingEnabled { get; set; }
    public bool IsColumnHeaderVisible { get; set; }
    public double ColumnHeaderHeight { get; set; }
    
    public event EventHandler<SelectionChangedEventArgs>? SelectionChanged;
}
```

#### TreeView

```csharp
// Location: src/Avalonia.Controls/TreeView.cs
public class TreeView : ItemsControl
{
    public SelectionMode SelectionMode { get; set; }
    public IList SelectedItems { get; }
    public int SelectedIndex { get; set; }
    public object? SelectedItem { get; set; }
    public bool IsExpanded { get; set; }
    public bool IsItemExpanded { get; set; }
    public bool IsSelectionActive { get; set; }
    
    public event EventHandler<SelectionChangedEventArgs>? SelectionChanged;
    public event EventHandler<TreeViewExpandedEventArgs>? Expanded;
    public event EventHandler<TreeViewCollapsedEventArgs>? Collapsed;
}
```

#### TreeViewItem

```csharp
// Location: src/Avalonia.Controls/TreeViewItem.cs
public class TreeViewItem : ContentControl
{
    public bool IsExpanded { get; set; }
    public bool IsSelected { get; set; }
    public bool HasItems { get; }
    public bool HasHeader { get; }
    public bool HasContent { get; }
    public bool IsLeaf { get; }
    public bool IsRoot { get; }
    public int Level { get; }
    public TreeViewItem? Parent { get; }
    public IList<TreeViewItem> Children { get; }
    
    public void Expand();
    public void Collapse();
    public void Toggle();
}
```

#### TabControl

```csharp
// Location: src/Avalonia.Controls/TabControl.cs
public class TabControl : ItemsControl
{
    public int SelectedIndex { get; set; }
    public object? SelectedItem { get; set; }
    public bool IsContentPreserved { get; set; }
    public bool IsTabFill { get; set; }
    public TabStripPlacement TabStripPlacement { get; set; }
    public TabStripHeader? TabStripHeader { get; set; }
    public TabStripFooter? TabStripFooter { get; set; }
    
    public event EventHandler<SelectionChangedEventArgs>? SelectionChanged;
}

public enum TabStripPlacement { Top, Bottom, Left, Right }
```

#### TabItem

```csharp
// Location: src/Avalonia.Controls/TabItem.cs
public class TabItem : HeaderedContentControl
{
    public bool IsSelected { get; set; }
    public bool IsEnabled { get; set; }
    public bool IsReadOnly { get; set; }
    public TabItem? Parent { get; }
}
```

#### Menu

```csharp
// Location: src/Avalonia.Controls/Menu.cs
public class Menu : ItemsControl
{
    public object? Items { get; set; }
    public IList? ItemsSource { get; set; }
    public DataTemplate? ItemTemplate { get; set; }
    public DataTemplateSelector? ItemTemplateSelector { get; set; }
    public bool IsOpen { get; set; }
    public bool IsKeyboardFocused { get; set; }
    
    public event EventHandler<RoutedEventArgs>? Opened;
    public event EventHandler<RoutedEventArgs>? Closed;
}
```

#### MenuItem

```csharp
// Location: src/Avalonia.Controls/MenuItem.cs
public class MenuItem : HeaderedItemsControl
{
    public object? Header { get; set; }
    public DataTemplate? HeaderTemplate { get; set; }
    public object? Icon { get; set; }
    public DataTemplate? IconTemplate { get; set; }
    public object? Items { get; set; }
    public IList? ItemsSource { get; set; }
    public DataTemplate? ItemTemplate { get; set; }
    public DataTemplateSelector? ItemTemplateSelector { get; set; }
    public MenuItemToggleType ToggleType { get; set; }
    public bool IsChecked { get; set; }
    public bool IsOpen { get; set; }
    public bool IsSubmenuOpen { get; }
    public bool IsEnabled { get; set; }
    public ICommand? Command { get; set; }
    public object? CommandParameter { get; set; }
    public IInputElement? CommandTarget { get; set; }
    
    public event EventHandler<RoutedEventArgs>? Checked;
    public event EventHandler<RoutedEventArgs>? Unchecked;
    public event EventHandler<RoutedEventArgs>? Opened;
    public event EventHandler<RoutedEventArgs>? Closed;
    public event EventHandler<RoutedEventArgs>? Click;
}

public enum MenuItemToggleType { None, CheckBox, Radio }
```

#### ContextMenu

```csharp
// Location: src/Avalonia.Controls/ContextMenu.cs
public class ContextMenu : Menu
{
    public bool IsOpen { get; set; }
    public bool StaysOpen { get; set; }
    public UIElement? PlacementTarget { get; set; }
    public HorizontalAlignment HorizontalAlignment { get; set; }
    public VerticalAlignment VerticalAlignment { get; set; }
    public double HorizontalOffset { get; set; }
    public double VerticalOffset { get; set; }
    
    public void Open();
    public void Close();
}
```

#### Separator

```csharp
// Location: src/Avalonia.Controls/Separator.cs
public class Separator : ContentControl
{
    public static readonly AttachedProperty<bool> IsVerticalProperty;
}
```

#### Carousel

```csharp
// Location: src/Avalonia.Controls/Carousel.cs
public class Carousel : ItemsControl
{
    public object? Items { get; set; }
    public IList? ItemsSource { get; set; }
    public DataTemplate? ItemTemplate { get; set; }
    public DataTemplateSelector? ItemTemplateSelector { get; set; }
    public int CurrentItem { get; set; }
    public object? SelectedItem { get; }
    public bool IsLoopEnabled { get; set; }
    public bool IsSwipeEnabled { get; set; }
    public bool IsTransitionEnabled { get; set; }
    public TimeSpan TransitionDuration { get; set; }
    public IPageTransition? Transition { get; set; }
    
    public event EventHandler<CurrentItemChangedEventArgs>? CurrentIndexChanged;
    public event EventHandler<SelectionChangedEventArgs>? SelectionChanged;
    
    public void MoveNext();
    public void MovePrevious();
    public void MoveTo(int index);
}
```

#### MenuBase

```csharp
// Location: src/Avalonia.Controls/MenuBase.cs
public abstract class MenuBase : ItemsControl
{
    public bool IsOpen { get; set; }
    public bool IsKeyboardFocused { get; set; }
    
    public event EventHandler<RoutedEventArgs>? Opened;
    public event EventHandler<RoutedEventArgs>? Closed;
}
```

#### HeaderedItemsControl

```csharp
// Location: src/Avalonia.Controls/HeaderedItemsControl.cs
public class HeaderedItemsControl : ItemsControl, IHeadered
{
    public object? Header { get; set; }
    public DataTemplate? HeaderTemplate { get; set; }
}
```

#### ItemsPresenter

```csharp
// Location: src/Avalonia.Controls/ItemsPresenter.cs
public class ItemsPresenter : ContentControl
{
    public object? Items { get; set; }
    public IList? ItemsSource { get; set; }
    public DataTemplate? ItemTemplate { get; set; }
    public DataTemplateSelector? ItemTemplateSelector { get; set; }
    public ItemContainerGenerator? ItemContainerGenerator { get; }
    
    protected override void OnApplyTemplate(ApplyTemplateEventArgs e);
}
```

#### Selection Model

```csharp
// Location: src/Avalonia.Controls/Selection/
public interface ISelectable
{
    SelectionMode SelectionMode { get; set; }
    IList SelectedItems { get; }
    int SelectedIndex { get; set; }
    object? SelectedItem { get; set; }
    bool IsSelectionActive { get; }
}

public enum SelectionMode { Single, Multiple, Extended }

public class SelectionChangedEventArgs : RoutedEventArgs
{
    public IList AddedItems { get; }
    public IList RemovedItems { get; }
    public int Index { get; }
}
```

### 5.5 Window & Popup

#### TopLevel

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

#### Window

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

#### WindowBase

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

#### WindowState

```csharp
// Location: src/Avalonia.Controls/WindowState.cs
public enum WindowState { Normal, Minimized, Maximized }
```

#### WindowStartupLocation

```csharp
// Location: src/Avalonia.Controls/WindowStartupLocation.cs
public enum WindowStartupLocation { Manual, CenterOwner, CenterScreen }
```

#### WindowTransparencyLevel

```csharp
// Location: src/Avalonia.Controls/WindowTransparencyLevel.cs
public enum WindowTransparencyLevel { Transparent, Acrylic, Mica, BackgroundBlur, TabbedBlur }
```

#### WindowEdge

```csharp
// Location: src/Avalonia.Controls/WindowEdge.cs
public enum WindowEdge { None, Left, Right, Top, Bottom, TopLeft, TopRight, BottomLeft, BottomRight }
```

#### WindowIcon

```csharp
// Location: src/Avalonia.Controls/WindowIcon.cs
public class WindowIcon
{
    public Bitmap? Source { get; set; }
    public Uri? Uri { get; set; }
    public string? Path { get; set; }
}
```

#### Window Closing/Closed/Opened/Resized Events

```csharp
// Location: src/Avalonia.Controls/
public class WindowClosingEventArgs : CancelEventArgs { public Window Window { get; } public bool Handled { get; set; } }
public class WindowClosedEventArgs : EventArgs { public Window Window { get; } }
public class WindowOpenedEventArgs : EventArgs { public Window Window { get; } }
public class WindowResizedEventArgs : EventArgs { public Window Window { get; } public Rect Bounds { get; } public Size Size { get; } }
```

#### Popup

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

public enum PlacementMode { MousePoint, MousePosition, Mouse, Absolute, Relative, RelativePoint, Center, TargetRect }
```

#### Flyout

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

public enum FlyoutPlacementMode { Top, Bottom, Left, Right, Full }
```

#### FlyoutBase

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

#### OverlayPopup

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

#### ItemPicker

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

#### NativeMenu

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

#### TrayIcon

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

#### TopLevelHost

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

---

## 6. Styling & Theming

### 6.1 Styles

```csharp
// Location: src/Avalonia.Base/Styling/
public class Styles : IList<IStyle>, ICollection<IStyle>, IEnumerable<IStyle>, IEnumerable
{
    public IStyle this[int index] { get; set; }
    public int Count { get; }
    public bool IsReadOnly { get; }
    
    public void Add(IStyle style);
    public void Remove(IStyle style);
    public void Clear();
    public int IndexOf(IStyle style);
    public void Insert(int index, IStyle style);
    public void RemoveAt(int index);
    public void AddRange(IEnumerable<IStyle> styles);
    public bool Contains(IStyle style);
    public void CopyTo(IStyle[] array, int arrayIndex);
}
```

### 6.2 Stylesheet

```csharp
// Location: src/Avalonia.Base/Styling/Stylesheet.cs
public class Stylesheet : IStyle
{
    public string? Name { get; set; }
    public int Priority { get; set; }
    public bool CanApply { get; }
    public IStyle? Parent { get; }
    
    public bool Apply(StyledElement element);
    public void Remove(StyledElement element);
}
```

### 6.3 StyleSelector

```csharp
// Location: src/Avalonia.Base/Styling/StyleSelector.cs
public class StyleSelector : IStyle
{
    public IList<IStyle> Styles { get; }
    public int Priority { get; set; }
    public bool CanApply { get; }
    public IStyle? Parent { get; }
    
    public virtual IStyle? SelectStyle(IStyle? currentStyle, object? item, object? container, int index);
    public bool Apply(StyledElement element);
    public void Remove(StyledElement element);
}
```

### 6.4 Style

```csharp
// Location: src/Avalonia.Base/Styling/Style.cs
public class Style : IStyle
{
    public string? Name { get; set; }
    public int Priority { get; set; }
    public bool CanApply { get; }
    public IStyle? Parent { get; }
    
    // Selectors
    public Type? Selector { get; set; }
    public string? SelectorString { get; set; }
    public IList<SetterBase> Setters { get; }
    public IList<IStyle> Children { get; }
    public IList<IDataTemplate> DataTemplates { get; }
    
    public bool Apply(StyledElement element);
    public void Remove(StyledElement element);
}
```

### 6.5 Setter

```csharp
// Location: src/Avalonia.Base/Styling/Setter.cs
public class Setter : SetterBase
{
    public AvaloniaProperty Property { get; set; }
    public object? Value { get; set; }
    public object? TargetNullValue { get; set; }
    public IValueConverter? Converter { get; set; }
    public object? ConverterParameter { get; set; }
    public RelativeSource? RelativeSource { get; set; }
    public bool IsDynamic { get; }
    
    public void Apply(StyledElement element);
    public void Remove(StyledElement element);
}

// Typed setter
public class Setter<T> : Setter { public new T Value { get; set; } }

// Multi-setter
public class MultiSetter : SetterBase
{
    public IList<SetterBase> Setters { get; }
    public void Apply(StyledElement element);
    public void Remove(StyledElement element);
}
```

### 6.6 CSS-like Selectors

```csharp
// Location: src/Avalonia.Base/Styling/
// Supported CSS-like selectors:

// Class selector: .my-class
public class ClassSelector : Selector { public string ClassName { get; set; } }

// ID selector: #my-id
public class IdSelector : Selector { public string Id { get; set; } }

// Pseudo-class selectors:
// :hover - element is hovered
// :focus - element has focus
// :pressed - element is pressed
// :disabled - element is disabled
// :checked - element is checked
// :unchecked - element is not checked
// :first - first child
// :last - last child
// :child>of - direct child of
// :descendant>of - descendant of
// :ancestor - is ancestor
// :not - negation
// :nth(n) - nth child
// :nth-last(n) - nth from last
// :odd - odd child
// :even - even child
// :empty - no children
// :first-child - first child
// :last-child - last child
// :only-child - only child
// :only-of-type - only of type
// :first-of-type - first of type
// :last-of-type - last of type
// :root - root element
// :focus-within - has focused descendant
// :focus-visible - focus visible
// :disabled - disabled
// :enabled - enabled
// :read-only - read only
// :read-write - read write
// :required - required
// :optional - optional
// :valid - valid
// :invalid - invalid
// :in-range - in range
// :out-of-range - out of range
// :placeholder-shown - placeholder shown
// :fullscreen - fullscreen
// :modal - modal
// :target - target
// :visited - visited
// :link - link
// :local-link - local link
// :any-link - any link
// :defined - defined
// :lang - language
// :is - is
// :where - where
// :has - has
// :nth-child - nth child
// :nth-last-child - nth last child
// :nth-of-type - nth of type
// :nth-last-of-type - nth last of type
// :scope - scope
// :host - host
// :host-context - host context
// :dir - direction
// :dir(ltr) - left to right
// :dir(rtl) - right to left
// :lang(en) - language
// :lang(en-US) - language with region
// :has(.child) - has child
// :not(.child) - not child
// :is(.child) - is child
// :where(.child) - where child
// :nth-child(2n) - nth child
// :nth-child(2n+1) - nth child with offset
// :nth-child(-n+3) - nth child negative
// :nth-last-child(2n) - nth last child
// :nth-last-child(2n+1) - nth last child with offset
// :nth-last-child(-n+3) - nth last child negative
// :nth-of-type(2n) - nth of type
// :nth-of-type(2n+1) - nth of type with offset
// :nth-of-type(-n+3) - nth of type negative
// :nth-last-of-type(2n) - nth last of type
// :nth-last-of-type(2n+1) - nth last of type with offset
// :nth-last-of-type(-n+3) - nth last of type negative
```

---

## 7. Animation

### 7.1 Animatable

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

### 7.2 IAnimation

```csharp
// Location: src/Avalonia.Base/Animation/
public interface IAnimation
{
    Type TargetPropertyType { get; }
    double Duration { get; }
    IEasing? Easing { get; }
    FillMode FillMode { get; }
    double PlaybackRate { get; }
    int IterationCount { get; }
    
    IAnimationInstance CreateInstance();
    bool CanAnimate(IAnimationInstance instance);
}
```

### 7.3 Animation

```csharp
// Location: src/Avalonia.Base/Animation/Animation.cs
public class Animation : IAnimation
{
    public Type TargetPropertyType { get; }
    public double Duration { get; set; }
    public IEasing? Easing { get; set; }
    public FillMode FillMode { get; set; }
    public double PlaybackRate { get; set; }
    public int IterationCount { get; set; }
    
    public IList<KeyFrame> KeyFrames { get; }
    public IAnimationInstance CreateInstance();
    public bool CanAnimate(IAnimationInstance instance);
}
```

### 7.4 AnimationInstance

```csharp
// Location: src/Avalonia.Base/Animation/
public abstract class IAnimationInstance
{
    public abstract void Start();
    public abstract void Stop();
    public abstract void Pause();
    public abstract void Resume();
    public abstract void Seek(double offset);
    public abstract void Skip(double offset);
    public abstract void Restart();
    public abstract void SetPlaybackRate(double rate);
    public abstract void SetFillMode(FillMode mode);
    public abstract void SetIterationCount(int count);
    public abstract void SetDuration(double duration);
    public abstract void SetEasing(IEasing easing);
    public abstract void SetTargetProperty(AvaloniaProperty property);
    public abstract void SetTargetObject(AvaloniaObject target);
}
```

### 7.5 KeyFrame

```csharp
// Location: src/Avalonia.Base/Animation/
public abstract class KeyFrame
{
    public double Offset { get; set; }
    public abstract object? Value { get; }
}

public class DoubleKeyFrame : KeyFrame { public double Value { get; set; } public override object? Value => Value; }
public class ColorKeyFrame : KeyFrame { public Color Value { get; set; } public override object? Value => Value; }
public class ObjectKeyFrame : KeyFrame { public object? Value { get; set; } public override object? Value => Value; }
```

### 7.6 IEasing

```csharp
// Location: src/Avalonia.Base/Animation/
public interface IEasing
{
    double Easing(double x);
}

public abstract class Easing : IEasing { public abstract double Easing(double x); }
```

### 7.7 Easing Functions

```csharp
// Location: src/Avalonia.Base/Animation/Easing/
// Linear easing
public class LinearEasing : Easing { public override double Easing(double x) => x; }

// Cubic easing
public class CubicEaseIn : Easing { public override double Easing(double x) => x * x * x; }
public class CubicEaseOut : Easing { public override double Easing(double x) => (--x) * x * x + 1; }
public class CubicEaseInOut : Easing { public override double Easing(double x) => x < 0.5 ? 4 * x * x * x : 1 - Math.Pow(-2 * x + 2, 3) / 2; }

// Bounce easing
public class BounceEaseIn : Easing { public override double Easing(double x) => x * x; }
public class BounceEaseOut : Easing { public override double Easing(double x) => 1 - Math.Pow(1 - x, 3); }
public class BounceEaseInOut : Easing { public override double Easing(double x) => x < 0.5 ? 4 * x * x * x : 1 - Math.Pow(-2 * x + 2, 3) / 2; }

// Circular easing
public class CircularEaseIn : Easing { public override double Easing(double x) => 1 - Math.Sqrt(1 - x * x); }
public class CircularEaseOut : Easing { public override double Easing(double x) => Math.Sqrt(x * (2 - x)); }
public class CircularEaseInOut : Easing { public override double Easing(double x) => x < 0.5 ? (1 - Math.Sqrt(1 - 4 * x * x)) / 2 : (Math.Sqrt(-2 * x * x + 4 * x) + 1) / 2; }

// Elastic easing
public class ElasticEaseIn : Easing { public override double Easing(double x) => Math.Pow(2, 10 * (x - 1)) * Math.Cos(20 * Math.PI / 3 * (x - 1)); }
public class ElasticEaseOut : Easing { public override double Easing(double x) => Math.Pow(2, -10 * x) * Math.Cos(20 * Math.PI / 3 * x) + 1; }
public class ElasticEaseInOut : Easing { public override double Easing(double x) => x < 0.5 ? Math.Pow(2, 20 * x - 10) * Math.Cos(20 * Math.PI / 3 * (x - 0.5)) / 2 : Math.Pow(2, -20 * x + 10) * Math.Cos(20 * Math.PI / 3 * (x - 0.5)) / 2 + 1; }

// Back easing
public class BackEaseIn : Easing { public override double Easing(double x) => (x - 1) * (x - 1) * ((2.70158 + 1) * (x - 1) + 2.70158) + 1; }
public class BackEaseOut : Easing { public override double Easing(double x) => x * x * ((2.70158 + 1) * x - 2.70158) + 1; }
public class BackEaseInOut : Easing { public override double Easing(double x) => x < 0.5 ? Math.Pow(2, 20 * x - 10) * Math.Cos(20 * Math.PI / 3 * (x - 0.5)) / 2 : Math.Pow(2, -20 * x + 10) * Math.Cos(20 * Math.PI / 3 * (x - 0.5)) / 2 + 1; }

// Exponential easing
public class ExponentialEaseIn : Easing { public override double Easing(double x) => x == 0 ? 0 : Math.Pow(2, 10 * (x - 1)); }
public class ExponentialEaseOut : Easing { public override double Easing(double x) => x == 1 ? 1 : 1 - Math.Pow(2, -10 * x); }
public class ExponentialEaseInOut : Easing { public override double Easing(double x) => x == 0 ? 0 : x == 1 ? 1 : x < 0.5 ? Math.Pow(2, 20 * x - 10) / 2 : (2 - Math.Pow(2, -20 * x + 10)) / 2; }

// Spring easing
public class SpringEasing : Easing { public override double Easing(double x) => Math.Pow(2, -10 * x) * Math.Sin(40 * Math.PI * x / 3) + 1; }

// KeySpline easing (for Bezier curves)
public class KeySplineEasing : Easing
{
    public Point Control1 { get; set; }
    public Point Control2 { get; set; }
    public override double Easing(double x);
}
```

### 7.8 FillMode

```csharp
// Location: src/Avalonia.Base/Animation/
public enum FillMode
{
    None,      // Remove values when animation ends
    Forward,   // Apply final values when animation ends
    Backward,  // Apply initial values when animation ends
    Both,      // Apply both initial and final values
}
```

### 7.9 Transition

```csharp
// Location: src/Avalonia.Base/Transitions/
public class Transition
{
    public double Duration { get; set; }
    public IEasing? Easing { get; set; }
    public AvaloniaProperty Property { get; set; }
    public object? From { get; set; }
    public object? To { get; set; }
}

public class TransitionInstance
{
    public double Duration { get; }
    public IEasing? Easing { get; }
    public bool IsRunning { get; }
    
    public void Start();
    public void Stop();
    public void Pause();
    public void Resume();
}

public class Transitions : List<Transition>
{
    public void Add(Transition transition);
    public void AddRange(IEnumerable<Transition> transitions);
    public void Remove(Transition transition);
    public void Clear();
}
```

### 7.10 IPageTransition

```csharp
// Location: src/Avalonia.Base/Transitions/
public interface IPageTransition
{
    TimeSpan Duration { get; }
    IEasing? Easing { get; }
    
    void Transition(UIElement from, UIElement to, Action<Rect> arrange);
    void Cancel();
}
```

### 7.11 Animator

```csharp
// Location: src/Avalonia.Base/Animation/
public interface IAnimator
{
    void Start(IAnimationInstance instance);
    void Stop(IAnimationInstance instance);
    void Pause(IAnimationInstance instance);
    void Resume(IAnimationInstance instance);
    void Seek(IAnimationInstance instance, double offset);
    void Skip(IAnimationInstance instance, double offset);
    void Restart(IAnimationInstance instance);
    void SetPlaybackRate(IAnimationInstance instance, double rate);
    void SetFillMode(IAnimationInstance instance, FillMode mode);
    void SetIterationCount(IAnimationInstance instance, int count);
    void SetDuration(IAnimationInstance instance, double duration);
    void SetEasing(IAnimationInstance instance, IEasing easing);
}

public class AnimatorDrivenTransition : IPageTransition
{
    public TimeSpan Duration { get; }
    public IEasing? Easing { get; }
    
    public void Transition(UIElement from, UIElement to, Action<Rect> arrange);
    public void Cancel();
}

public class AnimatorTransitionObservable : IObservable<IAnimationInstance>
{
    public IDisposable Subscribe(IObserver<IAnimationInstance> observer);
}
```

### 7.12 CompositePageTransition

```csharp
// Location: src/Avalonia.Base/Transitions/
public class CompositePageTransition : IPageTransition
{
    public IList<IPageTransition> Transitions { get; }
    public TimeSpan Duration { get; }
    public IEasing? Easing { get; }
    
    public void Add(IPageTransition transition);
    public void Remove(IPageTransition transition);
    public void Clear();
    public void Transition(UIElement from, UIElement to, Action<Rect> arrange);
    public void Cancel();
}
```

### 7.13 CrossFade

```csharp
// Location: src/Avalonia.Base/Transitions/
public class CrossFade : IPageTransition
{
    public TimeSpan Duration { get; }
    public IEasing? Easing { get; }
    
    public void Transition(UIElement from, UIElement to, Action<Rect> arrange);
    public void Cancel();
}
```

### 7.14 PageSlide

```csharp
// Location: src/Avalonia.Base/Transitions/
public class PageSlide : IPageTransition
{
    public TimeSpan Duration { get; }
    public IEasing? Easing { get; }
    public Vector Direction { get; set; }
    
    public void Transition(UIElement from, UIElement to, Action<Rect> arrange);
    public void Cancel();
}
```

### 7.15 PageTransitionItem

```csharp
// Location: src/Avalonia.Controls/PageTransitionItem.cs
public class PageTransitionItem : ContentControl
{
    public IPageTransition? Transition { get; set; }
    public TimeSpan TransitionDuration { get; set; }
    public int OpenedTransitionCount { get; }
    public int ClosedTransitionCount { get; }
    
    protected override void OnApplyTemplate(ApplyTemplateEventArgs e);
}
```

### 7.16 ConnectedAnimation

```csharp
// Location: src/Avalonia.Animation/
public class ConnectedAnimation : IPageTransition
{
    public string? Id { get; set; }
    public TimeSpan Duration { get; }
    public IEasing? Easing { get; }
    public object? Content { get; set; }
    public DataTemplate? ContentTemplate { get; set; }
    
    public void Transition(UIElement from, UIElement to, Action<Rect> arrange);
    public void Cancel();
}

public class ConnectedAnimationConfiguration
{
    public static ConnectedAnimationConfiguration Default { get; }
    public TimeSpan Duration { get; set; }
    public IEasing? Easing { get; set; }
    public bool IsLooping { get; set; }
}

public class ConnectedAnimationService
{
    public static ConnectedAnimation Get(string id);
    public static void Place(string id, ConnectedAnimation animation);
    public static void Remove(string id);
    public static bool TryGet(string id, out ConnectedAnimation? animation);
}
```

### 7.17 Clock

```csharp
// Location: src/Avalonia.Base/Animation/
public class Clock : IClock
{
    public DateTime StartTime { get; }
    public TimeSpan Elapsed { get; }
    public bool IsPaused { get; }
    public double Rate { get; }
    
    public void Start();
    public void Pause();
    public void Resume();
    public void Seek(TimeSpan offset);
    public void Skip(TimeSpan offset);
    public void Restart();
    public void SetRate(double rate);
}

public abstract class ClockBase : Clock { protected virtual void OnClockChanged(); }
public interface IClock
{
    DateTime StartTime { get; }
    TimeSpan Elapsed { get; }
    bool IsPaused { get; }
    double Rate { get; }
    void Start();
    void Pause();
    void Resume();
    void Seek(TimeSpan offset);
    void Skip(TimeSpan offset);
    void Restart();
    void SetRate(double rate);
}
public interface IGlobalClock : IClock { }
```

### 7.18 Custom Animator

```csharp
// Location: src/Avalonia.Base/Animation/
public interface ICustomAnimator
{
    void Start(IAnimationInstance instance);
    void Stop(IAnimationInstance instance);
    void Pause(IAnimationInstance instance);
    void Resume(IAnimationInstance instance);
    void Seek(IAnimationInstance instance, double offset);
    void Skip(IAnimationInstance instance, double offset);
    void Restart(IAnimationInstance instance);
    void SetPlaybackRate(IAnimationInstance instance, double rate);
    void SetFillMode(IAnimationInstance instance, FillMode mode);
    void SetIterationCount(IAnimationInstance instance, int count);
    void SetDuration(IAnimationInstance instance, double duration);
    void SetEasing(IAnimationInstance instance, IEasing easing);
}

public class Spring
{
    public double Stiffness { get; set; }
    public double Damping { get; set; }
    public double Mass { get; set; }
    public double InitialVelocity { get; set; }
}

public class Cue
{
    public double Offset { get; set; }
    public object? Value { get; set; }
}

public class DisposeAnimationInstanceSubject : IDisposable { public void Dispose(); }
```

---

## 8. Input & Events

### 8.1 UIElement

```csharp
// Location: src/Avalonia.Controls/UIElement.cs
public class UIElement : Visual, IInputElement
{
    // Input events
    public event EventHandler<KeyEventArgs>? KeyDown;
    public event EventHandler<KeyEventArgs>? KeyUp;
    public event EventHandler<PointerEventArgs>? PointerMoved;
    public event EventHandler<PointerPressedEventArgs>? PointerPressed;
    public event EventHandler<PointerReleasedEventArgs>? PointerReleased;
    public event EventHandler<PointerEventArgs>? PointerEntered;
    public event EventHandler<PointerEventArgs>? PointerExited;
    public event EventHandler<PointerWheelChangedEventArgs>? PointerWheelChanged;
    public event EventHandler<TextInputEventArgs>? TextInput;
    
    // Methods
    protected virtual void OnKeyDown(KeyEventArgs e);
    protected virtual void OnKeyUp(KeyEventArgs e);
    protected virtual void OnPointerMoved(PointerEventArgs e);
    protected virtual void OnPointerPressed(PointerPressedEventArgs e);
    protected virtual void OnPointerReleased(PointerReleasedEventArgs e);
    protected virtual void OnPointerEntered(PointerEventArgs e);
    protected virtual void OnPointerExited(PointerEventArgs e);
    protected virtual void OnPointerWheelChanged(PointerWheelChangedEventArgs e);
    protected virtual void OnTextInput(TextInputEventArgs e);
}
```

### 8.2 IInputElement

```csharp
// Location: src/Avalonia.Input/
public interface IInputElement
{
    bool IsHitTestVisible { get; set; }
    bool IsVisible { get; set; }
    Rect Bounds { get; }
    Rect VisualBounds { get; }
    
    bool HitTest(Point point);
    bool HitTest(Rect bounds);
}
```

### 8.3 RoutedEvent

```csharp
// Location: src/Avalonia.RoutedEvents/
public class RoutedEvent : INamed
{
    public string Name { get; }
    public Type OwnerType { get; }
    public RoutingStrategy RoutingStrategy { get; }
    
    public static RoutedEvent Register(string name, Type ownerType, RoutingStrategy routingStrategy);
    public static RoutedEvent Register<TOwner>(string name, RoutingStrategy routingStrategy);
}

public class RoutedEvent<T> : RoutedEvent
{
    public static RoutedEvent<T> Register(string name, Type ownerType, RoutingStrategy routingStrategy);
}
```

### 8.4 RoutingStrategy

```csharp
// Location: src/Avalonia.RoutedEvents/
public enum RoutingStrategy
{
    Direct,      // Only the target element
    Tunneling,   // From root to target (Preview* events)
    Bubbling,    // From target to root (Click, etc.)
}
```

### 8.5 RoutedEventArgs

```csharp
// Location: src/Avalonia.RoutedEvents/
public class RoutedEventArgs : EventArgs
{
    public object? Source { get; }
    public RoutedEvent? RoutedEvent { get; }
    public object? OriginalSource { get; }
    public bool Handled { get; set; }
    public int HandledEventsToo { get; set; }
    
    public void MarkRoutedEvent(RoutedEvent? routedEvent);
}

public class RoutedEventArgs<T> : RoutedEventArgs { public T Data { get; } }
```

### 8.6 KeyEventArgs

```csharp
// Location: src/Avalonia.Input/
public class KeyEventArgs : RoutedEventArgs
{
    public Key Key { get; }
    public KeyModifiers Modifiers { get; }
    public IKeyHandler? KeyHandler { get; }
    public IInputElement? Source { get; }
    
    public void MarkHandled();
}

public enum Key
{
    None, Tab, Enter, Shift, Control, Alt, CapsLock, Escape, Space, Back,
    PageUp, PageDown, End, Home, Left, Up, Right, Down, Insert, Delete,
    F1, F2, F3, F4, F5, F6, F7, F8, F9, F10, F11, F12,
    D0, D1, D2, D3, D4, D5, D6, D7, D8, D9,
    A, B, C, D, E, F, G, H, I, J, K, L, M, N, O, P, Q, R, S, T, U, V, W, X, Y, Z,
    NumPad0, NumPad1, NumPad2, NumPad3, NumPad4, NumPad5, NumPad6, NumPad7, NumPad8, NumPad9,
    Multiply, Add, Subtract, Divide, Decimal,
    LShift, RShift, LControl, RControl, LAlt, RAlt, LWin, RWin,
}

public enum KeyModifiers
{
    None = 0, Alt = 1, Control = 2, Shift = 4, Windows = 8,
    LeftShift = 16, RightShift = 32, LeftCtrl = 64, RightCtrl = 128,
    LeftAlt = 256, RightAlt = 512, LeftWin = 1024, RightWin = 2048,
}
```

### 8.7 PointerEventArgs

```csharp
// Location: src/Avalonia.Input/
public class PointerEventArgs : RoutedEventArgs
{
    public PointerPoint CurrentPoint { get; }
    public PointerPoint? PreviousPoint { get; }
    public IList<PointerPoint>? Points { get; }
    public PointerPoint? GetPoint(UIElement? relativeTo);
    public void MarkHandled();
}

public class PointerPressedEventArgs : PointerEventArgs { public PointerPressedEventArgs(PointerPoint currentPoint, PointerPoint? previousPoint); }
public class PointerReleasedEventArgs : PointerEventArgs { public PointerReleasedEventArgs(PointerPoint currentPoint, PointerPoint? previousPoint); }
public class PointerWheelChangedEventArgs : PointerEventArgs { public PointerWheelChangedEventArgs(PointerPoint currentPoint, PointerPoint? previousPoint); public Vector Delta { get; } }
```

### 8.8 PointerPoint

```csharp
// Location: src/Avalonia.Input/
public class PointerPoint
{
    public PointerDeviceType PointerDeviceType { get; }
    public Point Position { get; }
    public Rect Bounds { get; }
    public uint Pressure { get; }
    public uint Buttons { get; }
    public uint HorizontalWheelValue { get; }
    public uint VerticalWheelValue { get; }
    public uint TiltX { get; }
    public uint TiltY { get; }
    public uint Twist { get; }
    public uint Contact { get; }
    public uint ContactRect { get; }
    public uint ContactRectX { get; }
    public uint ContactRectY { get; }
    public uint ContactRectWidth { get; }
    public uint ContactRectHeight { get; }
    public uint ContactRectRight { get; }
    public uint ContactRectBottom { get; }
}

public enum PointerDeviceType { Mouse, Touch, Pen, TouchPad, TouchScreen }

public class PointerDevice
{
    public PointerDeviceType DeviceType { get; }
    public string? Name { get; }
}
```

### 8.9 IKeyHandler & IPointerHandler

```csharp
// Location: src/Avalonia.Input/
public interface IKeyHandler
{
    void OnKeyDown(Key key, KeyModifiers modifiers);
    void OnKeyUp(Key key, KeyModifiers modifiers);
}

public interface IPointerHandler
{
    void OnPointerPressed(PointerPressedEventArgs e);
    void OnPointerReleased(PointerReleasedEventArgs e);
    void OnPointerMoved(PointerEventArgs e);
    void OnPointerEntered(PointerEventArgs e);
    void OnPointerExited(PointerEventArgs e);
    void OnPointerWheelChanged(PointerWheelChangedEventArgs e);
}
```

### 8.10 VirtualInput

```csharp
// Location: src/Avalonia.Input/
public class VirtualInput
{
    public static void SimulateKeyDown(Key key);
    public static void SimulateKeyUp(Key key);
    public static void SimulateKeyPress(Key key);
    public static void SimulatePointerMove(Point point);
    public static void SimulatePointerPress(Point point);
    public static void SimulatePointerRelease(Point point);
    public static void SimulatePointerWheel(Vector delta);
}

public class VirtualKeyboardDevice
{
    public static void SimulateKeyDown(Key key);
    public static void SimulateKeyUp(Key key);
    public static void SimulateKeyPress(Key key);
}

public class VirtualMouseDevice
{
    public static void SimulateMove(Point point);
    public static void SimulatePress(Point point);
    public static void SimulateRelease(Point point);
    public static void SimulateWheel(Vector delta);
}

public class VirtualTouchDevice
{
    public static void SimulateMove(int touchId, Point point);
    public static void SimulatePress(int touchId, Point point);
    public static void SimulateRelease(int touchId, Point point);
}
```

### 8.11 HotkeyManager

```csharp
// Location: src/Avalonia.Input/
public class HotkeyManager
{
    public static Hotkey? GetHotkey(UIElement element);
    public static void SetHotkey(UIElement element, Hotkey? value);
    public static void RegisterHotkey(Hotkey hotkey, Action? action);
    public static void UnregisterHotkey(Hotkey hotkey);
    public static bool TryGetHotkey(string input, out Hotkey? hotkey);
}

public class Hotkey
{
    public Key Key { get; }
    public KeyModifiers Modifiers { get; }
    public string? Name { get; }
    
    public Hotkey(Key key, KeyModifiers modifiers);
    public Hotkey(string input);
    public bool Matches(Key key, KeyModifiers modifiers);
}
```

### 8.12 GestureRecognizer

```csharp
// Location: src/Avalonia.Input/
public class GestureRecognizer
{
    public event EventHandler<GestureEventArgs>? Gesture;
    
    public void Start();
    public void Stop();
    public void Cancel();
}

public class GestureEventArgs : EventArgs
{
    public GestureType Type { get; }
    public Point Position { get; }
    public Vector Delta { get; }
}

public enum GestureType { Tap, DoubleTap, LongPress, Swipe, Pinch, Rotate, Drag }
```

### 8.13 Media - Brush

```csharp
// Location: src/Avalonia.Base/Media/
public abstract class Brush : AvaloniaObject
{
    public static readonly StyledProperty<double> OpacityProperty;
    public double Opacity { get; set; }
}

public class SolidColorBrush : Brush { public static readonly StyledProperty<Color> ColorProperty; public Color Color { get; set; } }
public class LinearGradientBrush : Brush { public Point StartPoint { get; set; } public Point EndPoint { get; set; } public IList<GradientStop> Stops { get; } }
public class RadialGradientBrush : Brush { public Point GradientOriginOffset { get; set; } public Point Center { get; set; } public double RadiusX { get; set; } public double RadiusY { get; set; } public IList<GradientStop> Stops { get; } }
public class ImageBrush : Brush { public Bitmap? Source { get; set; } public Stretch Stretch { get; set; } public Rect SourceRect { get; set; } public Rect Viewport { get; set; } public bool ViewportUnits { get; set; } }
public class DrawingBrush : Brush { public Drawing? Drawing { get; set; } public Stretch Stretch { get; set; } }
public class TransformedBrush : Brush { public Transform Transform { get; set; } public Brush? Brush { get; set; } }
```

### 8.14 Media - Pen

```csharp
// Location: src/Avalonia.Base/Media/
public class Pen : AvaloniaObject
{
    public Brush? Brush { get; set; }
    public double Thickness { get; set; }
    public PenLineCap LineCap { get; set; }
    public PenLineJoin LineJoin { get; set; }
    public double[]? DashArray { get; set; }
    public double DashOffset { get; set; }
    public double MiterLimit { get; set; }
}
```

### 8.15 Media - Geometry

```csharp
// Location: src/Avalonia.Base/Media/
public abstract class Geometry : AvaloniaObject
{
    public abstract Rect Bounds { get; }
    public abstract bool FillContains(Point point);
    public abstract bool StrokeContains(Point point, double thickness);
    public abstract Rect FillRect { get; }
    public abstract Rect StrokeRect { get; }
    public abstract double Area { get; }
    public abstract double Length { get; }
}

public class PathGeometry : Geometry { public IList<PathFigure> Figures { get; } }
public class PathFigure : AvaloniaObject { public Point StartPoint { get; set; } public IList<PathSegment> Segments { get; } public bool IsFilled { get; set; } public bool IsClosed { get; set; } }
public abstract class PathSegment : AvaloniaObject { public abstract Point EndPoint { get; } public abstract void AddSegment(PathFigure figure); }

public class ArcSegment : PathSegment { public Point Point { get; set; } public Size Size { get; set; } public double RotationAngle { get; set; } public bool IsLargeArc { get; set; } public SweepDirection SweepDirection { get; set; } }
public class BezierSegment : PathSegment { public Point Point1 { get; set; } public Point Point2 { get; set; } public override Point EndPoint => Point2; }
public class LineSegment : PathSegment { public Point Point { get; set; } public override Point EndPoint => Point; }
public class PolyBezierSegment : PathSegment { public IList<Point> Points { get; } public override Point EndPoint => Points[^1]; }
public class PolyLineSegment : PathSegment { public IList<Point> Points { get; } public override Point EndPoint => Points[^1]; }
public class PolyQuadraticBezierSegment : PathSegment { public IList<Point> Points { get; } public override Point EndPoint => Points[^1]; }
public class QuadraticBezierSegment : PathSegment { public Point Point1 { get; set; } public Point Point2 { get; set; } public override Point EndPoint => Point2; }
public class RectangleGeometry : Geometry { public Rect Rect { get; set; } public double RadiusX { get; set; } public double RadiusY { get; set; } }
public class EllipseGeometry : Geometry { public Rect Rect { get; set; } public Point Center { get; set; } }
public class LineGeometry : Geometry { public Point StartPoint { get; set; } public Point EndPoint { get; set; } }
public class CombinedGeometry : Geometry { public Geometry Geometry1 { get; set; } public Geometry Geometry2 { get; set; } public CombinedGeometryMode GeometryCombineMode { get; set; } }

public enum CombinedGeometryMode { Union, Intersect, Exclude }
```

### 8.16 Media - Effects

```csharp
// Location: src/Avalonia.Base/Media/Effects/
public abstract class Effect : AvaloniaObject { public abstract Effect Clone(); }

public class BlurEffect : Effect { public double Radius { get; set; } public bool EnableFastPath { get; set; } public override Effect Clone(); }
public class DropShadowEffect : Effect { public Color Color { get; set; } public double BlurRadius { get; set; } public double ShadowDepth { get; set; } public bool ShadowDepthIsInPixels { get; set; } public override Effect Clone(); }
public class GlowEffect : Effect { public Color Color { get; set; } public double Radius { get; set; } public override Effect Clone(); }
public class EmbossEffect : Effect { public double LightAngle { get; set; } public double LightDistance { get; set; } public double LightHeight { get; set; } public override Effect Clone(); }
public class BevelEffect : Effect { public double BevelWidth { get; set; } public double BevelHeight { get; set; } public override Effect Clone(); }
```

### 8.17 Media - Transforms

```csharp
// Location: src/Avalonia.Base/Media/Transforms/
public abstract class Transform : AvaloniaObject
{
    public abstract Transform Clone();
    public abstract Rect TransformBounds(Rect bounds);
    public abstract Point Transform(Point point);
    public abstract Vector TransformVector(Vector vector);
}

public class TranslateTransform : Transform { public double X { get; set; } public double Y { get; set; } public override Transform Clone(); public override Rect TransformBounds(Rect bounds); public override Point Transform(Point point); public override Vector TransformVector(Vector vector); }
public class ScaleTransform : Transform { public double ScaleX { get; set; } public double ScaleY { get; set; } public override Transform Clone(); public override Rect TransformBounds(Rect bounds); public override Point Transform(Point point); public override Vector TransformVector(Vector vector); }
public class RotateTransform : Transform { public double Angle { get; set; } public override Transform Clone(); public override Rect TransformBounds(Rect bounds); public override Point Transform(Point point); public override Vector TransformVector(Vector vector); }
public class SkewTransform : Transform { public double AngleX { get; set; } public double AngleY { get; set; } public override Transform Clone(); public override Rect TransformBounds(Rect bounds); public override Point Transform(Point point); public override Vector TransformVector(Vector vector); }
public class MatrixTransform : Transform { public Matrix Matrix { get; set; } public override Transform Clone(); public override Rect TransformBounds(Rect bounds); public override Point Transform(Point point); public override Vector TransformVector(Vector vector); }
public class TransformGroup : Transform { public IList<Transform> Children { get; } public override Transform Clone(); public override Rect TransformBounds(Rect bounds); public override Point Transform(Point point); public override Vector TransformVector(Vector vector); }
public class Rotate3DTransform : Transform { public Vector3D Axis { get; set; } public double Angle { get; set; } public override Transform Clone(); public override Rect TransformBounds(Rect bounds); public override Point Transform(Point point); public override Vector TransformVector(Vector vector); }
```

### 8.18 Media - Matrix

```csharp
// Location: src/Avalonia.Base/Media/
public struct Matrix
{
    public double M11, M12, M21, M22, OffsetX, OffsetY;
    
    public static Matrix Identity { get; }
    public static Matrix Translation(double x, double y);
    public static Matrix Scale(double scaleX, double scaleY);
    public static Matrix Rotation(double angle);
    public static Matrix Skew(double angleX, double angleY);
    public static Matrix Multiply(Matrix a, Matrix b);
    public static Point Transform(Point point);
    public static Vector TransformVector(Vector vector);
    public static Rect TransformBounds(Rect bounds);
    public static Matrix Invert(Matrix matrix);
}
```

---

## 9. Advanced Patterns

### 9.1 ItemsPresenter

```csharp
// Location: src/Avalonia.Controls/ItemsPresenter.cs
public class ItemsPresenter : ContentControl
{
    public object? Items { get; set; }
    public IList? ItemsSource { get; set; }
    public DataTemplate? ItemTemplate { get; set; }
    public DataTemplateSelector? ItemTemplateSelector { get; set; }
    public ItemContainerGenerator? ItemContainerGenerator { get; }
    protected override void OnApplyTemplate(ApplyTemplateEventArgs e);
}
```

### 9.2 ISelectable

```csharp
// Location: src/Avalonia.Controls/Selection/
public interface ISelectable
{
    SelectionMode SelectionMode { get; set; }
    IList SelectedItems { get; }
    int SelectedIndex { get; set; }
    object? SelectedItem { get; set; }
    bool IsSelectionActive { get; }
}

public enum SelectionMode { Single, Multiple, Extended }

public class SelectionChangedEventArgs : RoutedEventArgs
{
    public IList AddedItems { get; }
    public IList RemovedItems { get; }
    public int Index { get; }
}
```

### 9.3 IHeadered

```csharp
// Location: src/Avalonia.Controls/
public interface IHeadered
{
    object? Header { get; set; }
    DataTemplate? HeaderTemplate { get; set; }
}

public class HeaderedContentControl : ContentControl, IHeadered
{
    public object? Header { get; set; }
    public DataTemplate? HeaderTemplate { get; set; }
}

public class HeaderedItemsControl : ItemsControl, IHeadered
{
    public object? Header { get; set; }
    public DataTemplate? HeaderTemplate { get; set; }
}
```

### 9.4 IColumnDefinition / IRowDefinition

```csharp
// Location: src/Avalonia.Controls/
public interface IColumnDefinition
{
    double Width { get; set; }
    double ActualWidth { get; }
    GridLength Width { get; }
    GridUnitType GridUnitType { get; }
}

public interface IRowDefinition
{
    double Height { get; set; }
    double ActualHeight { get; }
    GridLength Height { get; }
    GridUnitType GridUnitType { get; }
}
```

### 9.5 IOverlayWindow

```csharp
// Location: src/Avalonia.Controls/
public interface IOverlayWindow
{
    UIElement? Content { get; set; }
    bool IsOpen { get; set; }
    bool StaysOpen { get; set; }
    bool IsLightDismissEnabled { get; set; }
    void Show();
    void Hide();
}
```

### 9.6 IClipboard

```csharp
// Location: src/Avalonia.Input/
public interface IClipboard
{
    Task<string?> GetTextAsync();
    Task SetTextAsync(string text);
    Task SetDataAsync(string format, object data);
    Task<object?> GetDataAsync(string format);
    Task<string[]> GetFormatsAsync();
    Task ClearAsync();
}
```

### 9.7 IDialogService

```csharp
// Location: src/Avalonia.Controls/
public interface IDialogService
{
    Task<bool?> ShowDialogAsync(string title, string message, string? okButton = null, string? cancelButton = null);
    Task ShowAsync(string title, string message, string? okButton = null);
    Task<bool?> ConfirmAsync(string message, string? okButton = null, string? cancelButton = null);
}
```

### 9.8 IStorageService

```csharp
// Location: src/Avalonia.Storage/
public interface IStorageService
{
    Task<string> GetStoragePathAsync();
    Task<string> GetDocumentsPathAsync();
    Task<string> GetDesktopPathAsync();
    Task<string> GetDownloadsPathAsync();
    Task<string> GetPicturesPathAsync();
    Task<string> GetMusicPathAsync();
    Task<string> GetVideosPathAsync();
    Task<string> GetTempPathAsync();
    Task<IStorageFile> CreateFileAsync(string path);
    Task<IStorageFile> OpenFileAsync(string path, FileMode mode);
    Task<IStorageFolder> GetFolderAsync(string path);
    Task DeleteFileAsync(string path);
    Task DeleteFolderAsync(string path);
    Task<bool> ExistsAsync(string path);
}

public interface IStorageFile
{
    string Path { get; }
    Task WriteTextAsync(string text);
    Task<string> ReadTextAsync();
    Task WriteBytesAsync(byte[] bytes);
    Task<byte[]> ReadBytesAsync();
    Task CopyToAsync(string destination);
    Task MoveToAsync(string destination);
    Task DeleteAsync();
}

public interface IStorageFolder
{
    string Path { get; }
    Task<IStorageFile> CreateFileAsync(string name);
    Task<IStorageFolder> CreateFolderAsync(string name);
    Task<IStorageFile> OpenFileAsync(string name, FileMode mode);
    Task DeleteAsync();
    Task<IReadOnlyList<IStorageFile>> GetFilesAsync();
    Task<IReadOnlyList<IStorageFolder>> GetFoldersAsync();
}
```

### 9.9 Custom Control Development

```csharp
// Location: src/Avalonia.Controls/
// Creating a custom control:

public class MyCustomControl : Control
{
    // 1. Define properties
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<MyCustomControl, string?>(
            nameof(Text),
            defaultValue: "Hello");
    
    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }
    
    // 2. Define direct properties
    public static readonly DirectProperty<MyCustomControl, int> CountProperty =
        AvaloniaProperty.RegisterDirect<MyCustomControl, int>(
            nameof(Count),
            o => o.Count);
    
    private int _count;
    public int Count
    {
        get => _count;
        set => SetAndRaise(CountProperty, ref _count, value);
    }
    
    // 3. Define attached properties
    public static readonly AttachedProperty<bool> IsEnabledProperty =
        AvaloniaProperty.RegisterAttached<MyCustomControl, Control, bool>(
            nameof(IsEnabled),
            defaultValue: true);
    
    public static void SetIsEnabled(Control target, bool value) => target.SetValue(IsEnabledProperty, value);
    public static bool GetIsEnabled(Control target) => target.GetValue(IsEnabledProperty);
    
    // 4. Override methods
    protected override void OnApplyTemplate(ApplyTemplateEventArgs e) => base.OnApplyTemplate(e);
    protected override void OnTemplateChanged(ControlTemplate? oldTemplate, ControlTemplate? newTemplate) => base.OnTemplateChanged(oldTemplate, newTemplate);
    protected override void OnAttachedToLogicalTree(LogicalTreeAttachmentEventArgs e) => base.OnAttachedToLogicalTree(e);
    protected override void OnDetachedFromLogicalTree(LogicalTreeAttachmentEventArgs e) => base.OnDetachedFromLogicalTree(e);
    protected override void OnDataContextChanged(EventArgs e) => base.OnDataContextChanged(e);
    protected override void OnLoaded() => base.OnLoaded();
    protected override void OnUnloaded() => base.OnUnloaded();
    
    // 5. Handle template parts
    protected override void OnTemplateApplied(TemplateAppliedEventArgs e)
    {
        base.OnTemplateApplied(e);
        // e.NameScope - get template parts
        // e.OldScope - old template parts
    }
}
```

### 9.10 ControlTemplate

```csharp
// Location: src/Avalonia.Controls/
public class ControlTemplate
{
    public string? Name { get; set; }
    public IList<Setter> Setters { get; }
    public IList<Control> Children { get; }
    public ITemplate<Control> CreateInstance { get; }
    
    public Control Instantiate(INameScope nameScope);
    public Control Instantiate(INameScope nameScope, object? dataContext);
}

public class TemplateAppliedEventArgs : EventArgs { public INameScope NameScope { get; } public INameScope OldScope { get; } }
public class ApplyTemplateEventArgs : EventArgs { public Control Control { get; } }
```

### 9.11 IStyleable

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

### 9.12 IDataTemplate

```csharp
// Location: src/Avalonia.Base/Data/
public interface IDataTemplate
{
    bool Match(object? data);
    object? Load(object? data);
    void Unload(object? content);
}

public interface IDataTemplateSelector
{
    DataTemplate? SelectTemplate(object? item, object? container);
}

public interface IHierarchicalDataTemplate : IDataTemplate
{
    IEnumerable? ItemsSourcePath { get; }
    DataTemplate? ItemTemplate { get; }
}

public interface IGlobalDataTemplates : IEnumerable<IDataTemplate>, IEnumerable
{
    IDataTemplate? this[object item] { get; }
    bool TryGet(object item, out IDataTemplate? template);
    void Add(IDataTemplate template);
    void Remove(IDataTemplate template);
    void Clear();
}
```

### 9.13 IResourceProvider

```csharp
// Location: src/Avalonia.Base/Styling/
public interface IResourceProvider
{
    IResourceDictionary Resources { get; }
    IResourceProvider? Parent { get; }
}

public interface IResourceNode
{
    bool HasResources { get; }
    bool TryGetResource(object key, ThemeVariant? theme, out object? value);
}

public interface IResourceHost : IResourceNode
{
    IResourceDictionary Resources { get; set; }
    void AddOwner(IResourceHost host);
    void RemoveOwner(IResourceHost host);
}
```

### 9.14 ResourceDictionary

```csharp
// Location: src/Avalonia.Base/Styling/
public class ResourceDictionary : IResourceDictionary, IDictionary
{
    public object? this[object key] { get; set; }
    public bool HasResources { get; }
    public bool IsInitialized { get; }
    
    public void Add(object key, object value);
    public void Remove(object key);
    public bool TryGetResource(object key, ThemeVariant? theme, out object? value);
    public void AddOwner(IResourceHost host);
    public void RemoveOwner(IResourceHost host);
    
    // IDictionary
    public int Count { get; }
    public bool IsReadOnly { get; }
    public ICollection Keys { get; }
    public ICollection Values { get; }
    public void Clear();
    public bool Contains(object key);
    public void CopyTo(Array array, int index);
    public IEnumerator GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator();
}

// MergedDictionaries
public class MergedDictionaries : List<IResourceDictionary>
{
    public void Add(IResourceDictionary dictionary);
    public void Remove(IResourceDictionary dictionary);
    public void Clear();
}
```

### 9.15 DynamicResourceExtension

```csharp
// Location: src/Avalonia.Markup.Xaml/
public class DynamicResourceExtension : MarkupExtension
{
    public string? ResourceKey { get; set; }
    public object? FallbackValue { get; set; }
    public bool IsDynamic { get; }
    public override object? ProvideValue(IServiceProvider serviceProvider);
}
```

### 9.16 StaticResourceExtension

```csharp
// Location: src/Avalonia.Markup.Xaml/
public class StaticResourceExtension : MarkupExtension
{
    public string? ResourceKey { get; set; }
    public object? Value { get; }
    public override object? ProvideValue(IServiceProvider serviceProvider);
}
```

### 9.17 XAML Markup Extensions

```csharp
// Location: src/Avalonia.Markup.Xaml/
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
    public BindingMode Mode { get; set; }
    public object? Source { get; set; }
    public IValueConverter? Converter { get; set; }
    public object? ConverterParameter { get; set; }
    public string? StringFormat { get; set; }
    public RelativeSource? RelativeSource { get; set; }
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

---

## 10. Platform Abstraction

### 10.1 IPlatform

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

// Platform implementations:
// - Windows: src/Avalonia.Win32/
// - macOS: src/Avalonia.Native/
// - X11: src/Avalonia.X11/
// - Skia: src/Avalonia.Skia/
// - Browser (WASM): src/Avalonia.Browser/
// - Headless: src/Avalonia.Headless/
// - Metal: src/Avalonia.Metal/
// - Vulkan: src/Avalonia.Vulkan/
```

### 10.2 Platform Implementations

```csharp
// Windows (Avalonia.Win32)
public class Win32Platform : IPlatform { public static Win32Platform Create(); }

// macOS (Avalonia.Native)
public class NativePlatform : IPlatform { public static NativePlatform Create(); }

// X11 (Avalonia.X11)
public class X11Platform : IPlatform { public static X11Platform Create(); }

// Skia (Avalonia.Skia)
public class SkiaPlatform : IPlatform { public static SkiaPlatform Create(); }

// Browser (Avalonia.Browser)
public class BrowserPlatform : IPlatform { public static BrowserPlatform Create(); }

// Headless (Avalonia.Headless)
public class HeadlessPlatform : IPlatform { public static HeadlessPlatform Create(); }

// Metal (Avalonia.Metal)
public class MetalPlatform : IPlatform { public static MetalPlatform Create(); }

// Vulkan (Avalonia.Vulkan)
public class VulkanPlatform : IPlatform { public static VulkanPlatform Create(); }
```

### 10.3 Platform Interfaces

```csharp
// Location: src/Avalonia.Base/Platform/
// IPlatformBitmapDecoder
public interface IPlatformBitmapDecoder { Bitmap Decode(Stream stream); }

// IPlatformCursor
public interface IPlatformCursor { Cursor Create(CursorShape shape); Cursor Create(Bitmap bitmap, Point hotSpot); }

// IPlatformDragDropAdapter
public interface IPlatformDragDropAdapter { void DoDragDrop(UIElement source, IDataObject data); }

// IPlatformFileSystem
public interface IPlatformFileSystem
{
    bool Exists(string path);
    bool IsDirectory(string path);
    Stream Open(string path, FileMode mode);
    void Copy(string source, string destination);
    void Delete(string path);
    void Move(string source, string destination);
    void CreateDirectory(string path);
    void DeleteDirectory(string path);
    IReadOnlyList<string> GetFiles(string path);
    IReadOnlyList<string> GetDirectories(string path);
}

// IPlatformIconLoader
public interface IPlatformIconLoader { Icon? Load(string path); }

// IPlatformInputMethod
public interface IPlatformInputMethod { void Show(); void Hide(); void SetPosition(Rect position); }

// IPlatformNativeMenuExporter
public interface IPlatformNativeMenuExporter { NativeMenu? Export(UIElement element, NativeMenu menu); }

// IPlatformScreen
public interface IPlatformScreen { Rect Bounds { get; } double Scale { get; } string? Name { get; } }

// IPlatformScreens
public interface IPlatformScreens { IReadOnlyList<IPlatformScreen> Screens { get; } IPlatformScreen? Primary { get; } }

// IPlatformSettings
public interface IPlatformSettings { string? GetUserAgent(); string? GetLocale(); string? GetTimeZone(); }

// IPlatformUriHandler
public interface IPlatformUriHandler { void OpenUri(Uri uri); }

// IPlatformClipboard
public interface IPlatformClipboard { Task<string?> GetTextAsync(); Task SetTextAsync(string text); }

// IPlatformDragSource
public interface IPlatformDragSource { void StartDrag(UIElement source, IDataObject data); }

// IPlatformDropTarget
public interface IPlatformDropTarget { void AddDropTarget(UIElement target); void RemoveDropTarget(UIElement target); }

// IPlatformOpenUriHandler
public interface IPlatformOpenUriHandler { void OpenUri(Uri uri); }

// IPlatformVisualHost
public interface IPlatformVisualHost { void Show(Visual visual); void Hide(Visual visual); }

// IPlatformHwndHost
public interface IPlatformHwndHost { IntPtr Handle { get; } void Show(); void Hide(); }

// IPlatformHwndSource
public interface IPlatformHwndSource { IntPtr Handle { get; } Rect Bounds { get; } void Show(); void Hide(); }

// IPlatformHwndTarget
public interface IPlatformHwndTarget { void Resize(Size size); void Invalidate(); }

// IPlatformRenderTarget
public interface IPlatformRenderTarget { void Draw(IDrawingContextImpl context); }

// IPlatformBitmapImpl
public interface IPlatformBitmapImpl { Size Size { get; } void Draw(IDrawingContextImpl context); }

// IPlatformBitmapEncoder
public interface IPlatformBitmapEncoder { Bitmap Encode(Stream stream); }

// IPlatformBitmapDecoder
public interface IPlatformBitmapDecoder { Bitmap Decode(Stream stream); }

// IPlatformCursorImpl
public interface IPlatformCursorImpl { Cursor Create(CursorShape shape); Cursor Create(Bitmap bitmap, Point hotSpot); }

// IPlatformDragDropAdapterImpl
public interface IPlatformDragDropAdapterImpl { void DoDragDrop(UIElement source, IDataObject data); }

// IPlatformFileSystemImpl
public interface IPlatformFileSystemImpl { bool Exists(string path); bool IsDirectory(string path); Stream Open(string path, FileMode mode); }

// IPlatformIconLoaderImpl
public interface IPlatformIconLoaderImpl { Icon? Load(string path); }

// IPlatformInputMethodImpl
public interface IPlatformInputMethodImpl { void Show(); void Hide(); void SetPosition(Rect position); }

// IPlatformNativeMenuExporterImpl
public interface IPlatformNativeMenuExporterImpl { NativeMenu? Export(UIElement element, NativeMenu menu); }

// IPlatformScreenImpl
public interface IPlatformScreenImpl { Rect Bounds { get; } double Scale { get; } string? Name { get; } }

// IPlatformScreensImpl
public interface IPlatformScreensImpl { IReadOnlyList<IPlatformScreen> Screens { get; } IPlatformScreen? Primary { get; } }

// IPlatformSettingsImpl
public interface IPlatformSettingsImpl { string? GetUserAgent(); string? GetLocale(); string? GetTimeZone(); }

// IPlatformUriHandlerImpl
public interface IPlatformUriHandlerImpl { void OpenUri(Uri uri); }

// IPlatformVisualHostImpl
public interface IPlatformVisualHostImpl { void Show(Visual visual); void Hide(Visual visual); }

// IPlatformHwndHostImpl
public interface IPlatformHwndHostImpl { IntPtr Handle { get; } void Show(); void Hide(); }

// IPlatformHwndSourceImpl
public interface IPlatformHwndSourceImpl { IntPtr Handle { get; } Rect Bounds { get; } void Show(); void Hide(); }

// IPlatformHwndTargetImpl
public interface IPlatformHwndTargetImpl { void Resize(Size size); void Invalidate(); }

// IPlatformRenderTargetImpl
public interface IPlatformRenderTargetImpl { void Draw(IDrawingContextImpl context); }
```

---

## 11. Consolidated Key Files Reference

| File | Purpose |
|------|---------|
| `src/Avalonia.Base/AvaloniaObject.cs` | Core property system |
| `src/Avalonia.Base/AvaloniaProperty.cs` | AvaloniaProperty base |
| `src/Avalonia.Base/AvaloniaProperty\`1.cs` | Generic AvaloniaProperty |
| `src/Avalonia.Base/StyledProperty.cs` | StyledProperty |
| `src/Avalonia.Base/DirectProperty.cs` | DirectProperty |
| `src/Avalonia.Base/AttachedProperty.cs` | AttachedProperty |
| `src/Avalonia.Base/StyledElement.cs` | StyledElement |
| `src/Avalonia.Base/Visual.cs` | Visual tree |
| `src/Avalonia.Base/Animation/` | Animation system |
| `src/Avalonia.Base/Animation/Easing/` | All easing functions |
| `src/Avalonia.Base/PropertyStore/ValueStore.cs` | ValueStore |
| `src/Avalonia.Base/Classes.cs` | Classes/Pseudo-classes |
| `src/Avalonia.Base/Styling/` | Styling interfaces |
| `src/Avalonia.Base/Data/` | Data binding |
| `src/Avalonia.Base/Data/Binding.cs` | Binding class |
| `src/Avalonia.Base/Data/BindingBase.cs` | Binding base |
| `src/Avalonia.Base/Data/BindingExpression.cs` | Binding expressions |
| `src/Avalonia.Base/Data/RelativeSource.cs` | RelativeSource |
| `src/Avalonia.Base/Data/MultiBinding.cs` | MultiBinding |
| `src/Avalonia.Base/Data/IValueConverter.cs` | IValueConverter |
| `src/Avalonia.Base/Data/DataTemplates.cs` | DataTemplates |
| `src/Avalonia.Base/Data/CompiledBinding.cs` | Compiled bindings (x:Bind) |
| `src/Avalonia.Base/Collections/ObservableCollection.cs` | ObservableCollection |
| `src/Avalonia.Base/Reactive/ObservableAsPropertyHelper.cs` | ObservableAsPropertyHelper |
| `src/Avalonia.Base/Reactive/CompositeDisposable.cs` | CompositeDisposable |
| `src/Avalonia.Base/Layout/` | Layout interfaces and helpers |
| `src/Avalonia.Base/Layout/LayoutHelper.cs` | LayoutHelper |
| `src/Avalonia.Base/Layout/LayoutInformation.cs` | LayoutInformation |
| `src/Avalonia.Base/Media/Shape.cs` | Shape base |
| `src/Avalonia.Base/Media/Rectangle.cs` | Rectangle |
| `src/Avalonia.Base/Media/Ellipse.cs` | Ellipse |
| `src/Avalonia.Base/Media/Line.cs` | Line |
| `src/Avalonia.Base/Media/Polygon.cs` | Polygon |
| `src/Avalonia.Base/Media/Polyline.cs` | Polyline |
| `src/Avalonia.Base/Media/Brush.cs` | Brush base |
| `src/Avalonia.Base/Media/SolidColorBrush.cs` | SolidColorBrush |
| `src/Avalonia.Base/Media/LinearGradientBrush.cs` | LinearGradientBrush |
| `src/Avalonia.Base/Media/RadialGradientBrush.cs` | RadialGradientBrush |
| `src/Avalonia.Base/Media/ImageBrush.cs` | ImageBrush |
| `src/Avalonia.Base/Media/DrawingBrush.cs` | DrawingBrush |
| `src/Avalonia.Base/Media/TransformedBrush.cs` | TransformedBrush |
| `src/Avalonia.Base/Media/Pen.cs` | Pen |
| `src/Avalonia.Base/Media/Geometry.cs` | Geometry base |
| `src/Avalonia.Base/Media/PathGeometry.cs` | PathGeometry |
| `src/Avalonia.Base/Media/PathFigure.cs` | PathFigure |
| `src/Avalonia.Base/Media/PathSegment.cs` | PathSegment |
| `src/Avalonia.Base/Media/ArcSegment.cs` | ArcSegment |
| `src/Avalonia.Base/Media/BezierSegment.cs` | BezierSegment |
| `src/Avalonia.Base/Media/LineSegment.cs` | LineSegment |
| `src/Avalonia.Base/Media/PolyBezierSegment.cs` | PolyBezierSegment |
| `src/Avalonia.Base/Media/PolyLineSegment.cs` | PolyLineSegment |
| `src/Avalonia.Base/Media/PolyQuadraticBezierSegment.cs` | PolyQuadraticBezierSegment |
| `src/Avalonia.Base/Media/QuadraticBezierSegment.cs` | QuadraticBezierSegment |
| `src/Avalonia.Base/Media/RectangleGeometry.cs` | RectangleGeometry |
| `src/Avalonia.Base/Media/EllipseGeometry.cs` | EllipseGeometry |
| `src/Avalonia.Base/Media/LineGeometry.cs` | LineGeometry |
| `src/Avalonia.Base/Media/CombinedGeometry.cs` | CombinedGeometry |
| `src/Avalonia.Base/Media/Effects/` | Effects |
| `src/Avalonia.Base/Media/Transforms/` | Transforms |
| `src/Avalonia.Base/Media/Matrix.cs` | Matrix |
| `src/Avalonia.Base/Platform/` | Platform abstraction |
| `src/Avalonia.Base/Platform/IPlatform.cs` | IPlatform |
| `src/Avalonia.Base/Threading/` | Dispatcher system |
| `src/Avalonia.Base/Animation/Animatable.cs` | Animatable base |
| `src/Avalonia.Base/Animation/IAnimation.cs` | IAnimation interface |
| `src/Avalonia.Base/Animation/Animation.cs` | Animation class |
| `src/Avalonia.Base/Animation/IAnimationInstance.cs` | IAnimationInstance |
| `src/Avalonia.Base/Animation/KeyFrame.cs` | KeyFrame base |
| `src/Avalonia.Base/Animation/DoubleKeyFrame.cs` | DoubleKeyFrame |
| `src/Avalonia.Base/Animation/ColorKeyFrame.cs` | ColorKeyFrame |
| `src/Avalonia.Base/Animation/ObjectKeyFrame.cs` | ObjectKeyFrame |
| `src/Avalonia.Base/Animation/IEasing.cs` | IEasing interface |
| `src/Avalonia.Base/Animation/FillMode.cs` | FillMode enum |
| `src/Avalonia.Base/Animation/PlaybackDirection.cs` | PlaybackDirection |
| `src/Avalonia.Base/Animation/PlayState.cs` | PlayState |
| `src/Avalonia.Base/Animation/IterationCount.cs` | IterationCount |
| `src/Avalonia.Base/Transitions/Transition.cs` | Transition |
| `src/Avalonia.Base/Transitions/TransitionInstance.cs` | TransitionInstance |
| `src/Avalonia.Base/Transitions/Transitions.cs` | Transitions |
| `src/Avalonia.Base/Transitions/IPageTransition.cs` | IPageTransition |
| `src/Avalonia.Base/Transitions/CrossFade.cs` | CrossFade |
| `src/Avalonia.Base/Transitions/PageSlide.cs` | PageSlide |
| `src/Avalonia.Base/Transitions/CompositePageTransition.cs` | CompositePageTransition |
| `src/Avalonia.Base/Animation/IAnimator.cs` | IAnimator |
| `src/Avalonia.Base/Animation/AnimatorDrivenTransition.cs` | AnimatorDrivenTransition |
| `src/Avalonia.Base/Animation/AnimatorTransitionObservable.cs` | AnimatorTransitionObservable |
| `src/Avalonia.Base/Animation/Clock.cs` | Clock |
| `src/Avalonia.Base/Animation/ClockBase.cs` | ClockBase |
| `src/Avalonia.Base/Animation/IClock.cs` | IClock |
| `src/Avalonia.Base/Animation/IGlobalClock.cs` | IGlobalClock |
| `src/Avalonia.Base/Animation/ICustomAnimator.cs` | ICustomAnimator |
| `src/Avalonia.Base/Animation/Spring.cs` | Spring |
| `src/Avalonia.Base/Animation/Cue.cs` | Cue |
| `src/Avalonia.Base/Animation/DisposeAnimationInstanceSubject.cs` | DisposeAnimationInstanceSubject |
| `src/Avalonia.Controls/ContentControl.cs` | ContentControl |
| `src/Avalonia.Controls/TextBlock.cs` | TextBlock |
| `src/Avalonia.Controls/TextBox.cs` | TextBox |
| `src/Avalonia.Controls/PasswordBox.cs` | PasswordBox |
| `src/Avalonia.Controls/Image.cs` | Image |
| `src/Avalonia.Controls/Label.cs` | Label |
| `src/Avalonia.Controls/Expander.cs` | Expander |
| `src/Avalonia.Controls/ProgressBar.cs` | ProgressBar |
| `src/Avalonia.Controls/ProgressRing.cs` | ProgressRing |
| `src/Avalonia.Controls/LayoutTransformControl.cs` | LayoutTransformControl |
| `src/Avalonia.Controls/TransitioningContentControl.cs` | TransitioningContentControl |
| `src/Avalonia.Controls/FlipView.cs` | FlipView |
| `src/Avalonia.Controls/InfoBanner.cs` | InfoBanner |
| `src/Avalonia.Controls/SelectableTextBlock.cs` | SelectableTextBlock |
| `src/Avalonia.Controls/AvaloniaEditor.cs` | AvaloniaEditor |
| `src/Avalonia.Controls/Button.cs` | Button |
| `src/Avalonia.Controls/ButtonBase.cs` | ButtonBase |
| `src/Avalonia.Controls/RepeatButton.cs` | RepeatButton |
| `src/Avalonia.Controls/ToggleButton.cs` | ToggleButton |
| `src/Avalonia.Controls/ToggleButtonBase.cs` | ToggleButtonBase |
| `src/Avalonia.Controls/CheckBox.cs` | CheckBox |
| `src/Avalonia.Controls/RadioButton.cs` | RadioButton |
| `src/Avalonia.Controls/ComboBox.cs` | ComboBox |
| `src/Avalonia.Controls/ComboBoxItem.cs` | ComboBoxItem |
| `src/Avalonia.Controls/Slider.cs` | Slider |
| `src/Avalonia.Controls/RangeSlider.cs` | RangeSlider |
| `src/Avalonia.Controls/Rating.cs` | Rating |
| `src/Avalonia.Controls/RatingItem.cs` | RatingItem |
| `src/Avalonia.Controls/NumericUpDown.cs` | NumericUpDown |
| `src/Avalonia.Controls/DecimalUpDown.cs` | DecimalUpDown |
| `src/Avalonia.Controls/DecimalNumPicker.cs` | DecimalNumPicker |
| `src/Avalonia.Controls/MaskedTextBox.cs` | MaskedTextBox |
| `src/Avalonia.Controls/Calendar.cs` | Calendar |
| `src/Avalonia.Controls/DatePicker.cs` | DatePicker |
| `src/Avalonia.Controls/TimePicker.cs` | TimePicker |
| `src/Avalonia.Controls/CalendarDatePicker.cs` | CalendarDatePicker |
| `src/Avalonia.Controls/DateTimePicker.cs` | DateTimePicker |
| `src/Avalonia.Controls/TickBar.cs` | TickBar |
| `src/Avalonia.Controls/ButtonSpinner.cs` | ButtonSpinner |
| `src/Avalonia.Controls/DropDownButton.cs` | DropDownButton |
| `src/Avalonia.Controls/SplitButton.cs` | SplitButton |
| `src/Avalonia.Controls/ItemsControl.cs` | ItemsControl |
| `src/Avalonia.Controls/ItemCollection.cs` | ItemCollection |
| `src/Avalonia.Controls/ItemsSourceView.cs` | ItemsSourceView |
| `src/Avalonia.Controls/ItemContainerGenerator.cs` | ItemContainerGenerator |
| `src/Avalonia.Controls/ListBox.cs` | ListBox |
| `src/Avalonia.Controls/ListBoxItem.cs` | ListBoxItem |
| `src/Avalonia.Controls/ListView.cs` | ListView |
| `src/Avalonia.Controls/TreeView.cs` | TreeView |
| `src/Avalonia.Controls/TreeViewItem.cs` | TreeViewItem |
| `src/Avalonia.Controls/TabControl.cs` | TabControl |
| `src/Avalonia.Controls/TabItem.cs` | TabItem |
| `src/Avalonia.Controls/Menu.cs` | Menu |
| `src/Avalonia.Controls/MenuItem.cs` | MenuItem |
| `src/Avalonia.Controls/ContextMenu.cs` | ContextMenu |
| `src/Avalonia.Controls/Separator.cs` | Separator |
| `src/Avalonia.Controls/Carousel.cs` | Carousel |
| `src/Avalonia.Controls/MenuBase.cs` | MenuBase |
| `src/Avalonia.Controls/HeaderedItemsControl.cs` | HeaderedItemsControl |
| `src/Avalonia.Controls/ItemsPresenter.cs` | ItemsPresenter |
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
| `src/Avalonia.Controls/Decorator.cs` | Decorator base |
| `src/Avalonia.Controls/Border.cs` | Border |
| `src/Avalonia.Controls/BoxView.cs` | BoxView |
| `src/Avalonia.Controls/Canvas.cs` | Canvas |
| `src/Avalonia.Controls/Viewbox.cs` | Viewbox |
| `src/Avalonia.Controls/Viewbox2D.cs` | Viewbox2D |
| `src/Avalonia.Controls/Viewbox2DWithScrolling.cs` | Viewbox2DWithScrolling |
| `src/Avalonia.Controls/IconElement.cs` | IconElement |
| `src/Avalonia.Controls/PathIcon.cs` | PathIcon |
| `src/Avalonia.Controls/ExperimentalAcrylicBorder.cs` | ExperimentalAcrylicBorder |
| `src/Avalonia.Controls/Grid.cs` | Grid implementation |
| `src/Avalonia.Controls/StackPanel.cs` | StackPanel |
| `src/Avalonia.Controls/WrapPanel.cs` | WrapPanel |
| `src/Avalonia.Controls/DockPanel.cs` | DockPanel |
| `src/Avalonia.Controls/UniformGrid.cs` | UniformGrid |
| `src/Avalonia.Controls/RelativePanel.cs` | RelativePanel |
| `src/Avalonia.Controls/ScrollViewer.cs` | ScrollViewer |
| `src/Avalonia.Controls/VirtualizingStackPanel.cs` | VirtualizingStackPanel |
| `src/Avalonia.Controls/GridSplitter.cs` | GridSplitter |
| `src/Avalonia.Controls/GridLength.cs` | GridLength |
| `src/Avalonia.Controls/DefinitionBase.cs` | DefinitionBase |
| `src/Avalonia.Controls/DefinitionList.cs` | DefinitionList |
| `src/Avalonia.Controls/UIElement.cs` | UIElement base |
| `src/Avalonia.Input/IInputElement.cs` | IInputElement |
| `src/Avalonia.RoutedEvents/RoutedEvent.cs` | RoutedEvent |
| `src/Avalonia.RoutedEvents/RoutingStrategy.cs` | RoutingStrategy |
| `src/Avalonia.RoutedEvents/RoutedEventArgs.cs` | RoutedEventArgs |
| `src/Avalonia.Input/KeyEventArgs.cs` | KeyEventArgs |
| `src/Avalonia.Input/PointerEventArgs.cs` | PointerEventArgs |
| `src/Avalonia.Input/PointerPoint.cs` | PointerPoint |
| `src/Avalonia.Input/PointerDevice.cs` | PointerDevice |
| `src/Avalonia.Input/IKeyHandler.cs` | IKeyHandler |
| `src/Avalonia.Input/IPointerHandler.cs` | IPointerHandler |
| `src/Avalonia.Input/VirtualInput.cs` | VirtualInput |
| `src/Avalonia.Input/VirtualKeyboardDevice.cs` | VirtualKeyboardDevice |
| `src/Avalonia.Input/VirtualMouseDevice.cs` | VirtualMouseDevice |
| `src/Avalonia.Input/VirtualTouchDevice.cs` | VirtualTouchDevice |
| `src/Avalonia.Input/HotkeyManager.cs` | HotkeyManager |
| `src/Avalonia.Input/Hotkey.cs` | Hotkey |
| `src/Avalonia.Input/GestureRecognizer.cs` | GestureRecognizer |
| `src/Avalonia.Controls/IGlobalDataTemplates.cs` | IGlobalDataTemplates |
| `src/Avalonia.Markup.Xaml/MarkupExtension.cs` | MarkupExtension base |
| `src/Avalonia.Markup.Xaml/TypeExtension.cs` | x:Type |
| `src/Avalonia.Markup.Xaml/StaticExtension.cs` | x:Static |
| `src/Avalonia.Markup.Xaml/ArgumentsExtension.cs` | x:Arguments |
| `src/Avalonia.Markup.Xaml/BindExtension.cs` | x:Bind |
| `src/Avalonia.Markup.Xaml/NameExtension.cs` | x:Name |
| `src/Avalonia.Markup.Xaml/KeyExtension.cs` | x:Key |
| `src/Avalonia.Markup.Xaml/SharedExtension.cs` | x:Shared |
| `src/Avalonia.Markup.Xaml/ArrayExtension.cs` | x:Array |
| `src/Avalonia.Markup.Xaml/DictionaryExtension.cs` | x:Dictionary |
| `src/Avalonia.Markup.Xaml/DynamicResourceExtension.cs` | DynamicResourceExtension |
| `src/Avalonia.Markup.Xaml/StaticResourceExtension.cs` | StaticResourceExtension |
| `src/Avalonia.Controls/ControlTemplate.cs` | ControlTemplate |
| `src/Avalonia.Base/Styling/IStyleable.cs` | IStyleable |
| `src/Avalonia.Base/Styling/ISupportsNestedStyle.cs` | ISupportsNestedStyle |
| `src/Avalonia.Base/Styling/IAttachedObject.cs` | IAttachedObject |
| `src/Avalonia.Base/Styling/IResourceProvider.cs` | IResourceProvider |
| `src/Avalonia.Base/Styling/IResourceNode.cs` | IResourceNode |
| `src/Avalonia.Base/Styling/IResourceHost.cs` | IResourceHost |
| `src/Avalonia.Base/Styling/ResourceDictionary.cs` | ResourceDictionary |
| `src/Avalonia.Base/Data/IDataTemplate.cs` | IDataTemplate |
| `src/Avalonia.Base/Data/IDataTemplateSelector.cs` | IDataTemplateSelector |
| `src/Avalonia.Base/Data/IHierarchicalDataTemplate.cs` | IHierarchicalDataTemplate |
| `src/Avalonia.Controls/Selection/ISelectable.cs` | ISelectable |
| `src/Avalonia.Controls/HeaderedContentControl.cs` | HeaderedContentControl |
| `src/Avalonia.Controls/IColumnDefinition.cs` | IColumnDefinition |
| `src/Avalonia.Controls/IRowDefinition.cs` | IRowDefinition |
| `src/Avalonia.Controls/IOverlayWindow.cs` | IOverlayWindow |
| `src/Avalonia.Input/IClipboard.cs` | IClipboard |
| `src/Avalonia.Controls/IDialogService.cs` | IDialogService |
| `src/Avalonia.Storage/IStorageService.cs` | IStorageService |
| `src/Avalonia.Win32/Win32Platform.cs` | Win32 platform |
| `src/Avalonia.Native/NativePlatform.cs` | macOS platform |
| `src/Avalonia.X11/X11Platform.cs` | X11 platform |
| `src/Avalonia.Skia/SkiaPlatform.cs` | Skia platform |
| `src/Avalonia.Browser/BrowserPlatform.cs` | Browser platform |
| `src/Avalonia.Headless/HeadlessPlatform.cs` | Headless platform |
| `src/Avalonia.Metal/MetalPlatform.cs` | Metal platform |
| `src/Avalonia.Vulkan/VulkanPlatform.cs` | Vulkan platform |

---

*End of AVALONIA-12.0.3 Architecture Reference*
