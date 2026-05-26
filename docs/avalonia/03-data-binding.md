# Avalonia 12.0.3 Data Binding Reference

## 1. Binding Basics

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

## 2. BindingMode

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

## 3. RelativeSource

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

## 4. MultiBinding

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

## 5. IValueConverter

```csharp
// Location: src/Avalonia.Base/Data/
public interface IValueConverter
{
    object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture);
    object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture);
}

// Common converters
public class BooleanToVisibilityConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture);
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture);
}

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
public class LogConverter : IValueConverter { }
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

## 6. DataTemplates

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

## 7. IGlobalDataTemplates

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

## 8. Compiled Binding (Avalonia 12)

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

## 9. Observable Collections

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

## 10. ObservableAsPropertyHelper

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

## 11. Bind Extension Methods

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

## 12. CompositeDisposable

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

## 13. BindingValue

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

## 14. IndexerBinding

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

## 15. BindingExpressionBase

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

## 16. DataContext

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

## 17. Key Files Reference

| File | Purpose |
|------|---------|
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
| `src/Avalonia.Base/PropertyStore/ValueStore.cs` | ValueStore |
| `src/Avalonia.Controls/IGlobalDataTemplates.cs` | IGlobalDataTemplates |