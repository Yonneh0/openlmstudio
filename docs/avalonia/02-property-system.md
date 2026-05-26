# Avalonia 12.0.3 Property System Reference

## 1. Overview

Avalonia's property system is built on `AvaloniaObject` and provides a rich set of features including:
- Property change notifications
- Data binding
- Styling
- Animation
- Property inheritance
- Coercion
- Value priority system
- Data validation

## 2. AvaloniaProperty Types

### 2.1 AvaloniaProperty<T> (Base)

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

### 2.2 StyledProperty<T>

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

### 2.3 DirectProperty<T>

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

### 2.4 AttachedProperty<T>

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

## 3. AvaloniaObject Property Methods

```csharp
// Location: src/Avalonia.Base/AvaloniaObject.cs
public class AvaloniaObject : IAvaloniaObjectDebug, INotifyPropertyChanged
{
    // GetValue
    public object? GetValue(AvaloniaProperty property);
    public T GetValue<T>(StyledProperty<T> property);
    public T GetValue<T>(DirectPropertyBase<T> property);
    public Optional<T> GetBaseValue<T>(StyledProperty<T> property);
    
    // SetValue
    public IDisposable? SetValue(
        AvaloniaProperty property,
        object? value,
        BindingPriority priority = BindingPriority.LocalValue);
    public IDisposable? SetValue<T>(
        StyledProperty<T> property,
        T value,
        BindingPriority priority = BindingPriority.LocalValue);
    public void SetValue<T>(
        DirectPropertyBase<T> property,
        T value);
    
    // SetCurrentValue - changes effective value without changing value source
    public void SetCurrentValue(AvaloniaProperty property, object? value);
    public void SetCurrentValue<T>(StyledProperty<T> property, T value);
    
    // ClearValue
    public void ClearValue(AvaloniaProperty property);
    public void ClearValue<T>(StyledProperty<T> property);
    public void ClearValue<T>(DirectPropertyBase<T> property);
    
    // CoerceValue
    public void CoerceValue(AvaloniaProperty property);
    
    // IsSet - checks if property has a local value or binding
    public bool IsSet(AvaloniaProperty property);
    
    // IsAnimating
    public bool IsAnimating(AvaloniaProperty property);
    
    // Bind
    public BindingExpressionBase Bind(AvaloniaProperty property, BindingBase binding);
    public IDisposable Bind(
        AvaloniaProperty property,
        IObservable<object?> source,
        BindingPriority priority = BindingPriority.LocalValue);
    public IDisposable Bind<T>(
        StyledProperty<T> property,
        IObservable<object?> source,
        BindingPriority priority = BindingPriority.LocalValue);
    public IDisposable Bind<T>(
        StyledProperty<T> property,
        IObservable<T> source,
        BindingPriority priority = BindingPriority.LocalValue);
    public IDisposable Bind<T>(
        StyledProperty<T> property,
        IObservable<BindingValue<T>> source,
        BindingPriority priority = BindingPriority.LocalValue);
    
    // Indexer access
    public object? this[AvaloniaProperty property] { get; set; }
    public BindingBase this[IndexerDescriptor binding] { get; set; }
    
    // Events
    public event EventHandler<AvaloniaPropertyChangedEventArgs>? PropertyChanged;
    event PropertyChangedEventHandler? INotifyPropertyChanged.PropertyChanged;
    
    // Inheritance
    protected AvaloniaObject? InheritanceParent { get; set; }
    
    // Dispatcher / threading
    public Dispatcher Dispatcher { get; }
    public bool CheckAccess();
    public void VerifyAccess();
    
    // Internal
    protected virtual void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change);
    protected virtual void UpdateDataValidation(
        AvaloniaProperty property,
        BindingValueType state,
        Exception? error);
    protected bool SetAndRaise<T>(
        DirectPropertyBase<T> property,
        ref T field,
        T value);
}
```

## 4. BindingPriority

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

## 5. ValueStore System

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

## 6. AvaloniaPropertyChangedEventArgs

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

## 7. Property Registration Patterns

### 7.1 Standard Styled Property

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

private static bool ValidateText(string? value)
{
    return value != null;
}

private static string? OnTextNotifying(AvaloniaObject obj, string? value)
{
    return value;
}

public string? Text
{
    get => GetValue(TextProperty);
    set => SetValue(TextProperty, value);
}
```

### 7.2 Direct Property

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

### 7.3 Attached Property

```csharp
public static readonly AttachedProperty<int> MyAttachedProperty =
    AvaloniaProperty.RegisterAttached<MyAttached, Control, int>(
        nameof(MyAttachedProperty),
        defaultValue: 0,
        inherits: false);

public static void SetMyAttachedProperty(Control target, int value)
{
    target.SetValue(MyAttachedProperty, value);
}

public static int GetMyAttachedProperty(Control target)
{
    return target.GetValue(MyAttachedProperty);
}
```

### 7.4 Property with Affects Metadata

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

## 8. Inheritance System

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

## 9. Coercion System

```csharp
// CoerceValueCallback is called when a property's value changes
// and the coerce function returns a different value
public class MyControl : Control
{
    private static T? CoerceMyProperty(AvaloniaObject obj, T? value)
    {
        if (obj is MyControl control)
        {
            // Custom coercion logic
            return Math.Max(0, value);
        }
        return value;
    }
    
    public static readonly StyledProperty<int> MyProperty =
        AvaloniaProperty.Register<MyControl, int>(
            nameof(MyProperty),
            defaultValue: 0,
            coerce: CoerceMyProperty);
}
```

## 10. Data Validation

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

## 11. Classes (Pseudo-Classes)

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

## 12. IResourceProvider

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

## 13. Key Files Reference

| File | Purpose |
|------|---------|
| `src/Avalonia.Base/AvaloniaObject.cs` | Core property system |
| `src/Avalonia.Base/AvaloniaProperty.cs` | AvaloniaProperty base |
| `src/Avalonia.Base/AvaloniaProperty`1.cs` | Generic AvaloniaProperty |
| `src/Avalonia.Base/StyledProperty.cs` | StyledProperty |
| `src/Avalonia.Base/DirectProperty.cs` | DirectProperty |
| `src/Avalonia.Base/AttachedProperty.cs` | AttachedProperty |
| `src/Avalonia.Base/StyledElement.cs` | StyledElement |
| `src/Avalonia.Base/PropertyStore/ValueStore.cs` | ValueStore |
| `src/Avalonia.Base/PropertyStore/` | Property store internals |
| `src/Avalonia.Base/Classes.cs` | Classes/Pseudo-classes |
| `src/Avalonia.Base/Styling/` | Styling interfaces |