# Avalonia 12.0.3 Advanced Patterns Reference

## 1. ItemsPresenter

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

## 2. ISelectable

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

public enum SelectionMode
{
    Single,
    Multiple,
    Extended,
}

public class SelectionChangedEventArgs : RoutedEventArgs
{
    public IList AddedItems { get; }
    public IList RemovedItems { get; }
    public int Index { get; }
}
```

## 3. IHeadered

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

## 4. IColumnDefinition / IRowDefinition

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

## 5. IOverlayWindow

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

## 6. IClipboard

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

## 7. IDialogService

```csharp
// Location: src/Avalonia.Controls/
public interface IDialogService
{
    Task<bool?> ShowDialogAsync(string title, string message, string? okButton = null, string? cancelButton = null);
    Task ShowAsync(string title, string message, string? okButton = null);
    Task<bool?> ConfirmAsync(string message, string? okButton = null, string? cancelButton = null);
}
```

## 8. IStorageService

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

## 9. Custom Control Development

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
    
    public static void SetIsEnabled(Control target, bool value)
    {
        target.SetValue(IsEnabledProperty, value);
    }
    
    public static bool GetIsEnabled(Control target)
    {
        return target.GetValue(IsEnabledProperty);
    }
    
    // 4. Override methods
    protected override void OnApplyTemplate(ApplyTemplateEventArgs e)
    {
        base.OnApplyTemplate(e);
        // Called when template is applied
    }
    
    protected override void OnTemplateChanged(ControlTemplate? oldTemplate, ControlTemplate? newTemplate)
    {
        base.OnTemplateChanged(oldTemplate, newTemplate);
        // Called when template changes
    }
    
    protected override void OnAttachedToLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnAttachedToLogicalTree(e);
        // Called when attached to logical tree
    }
    
    protected override void OnDetachedFromLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromLogicalTree(e);
        // Called when detached from logical tree
    }
    
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        // Called when DataContext changes
    }
    
    protected override void OnLoaded()
    {
        base.OnLoaded();
        // Called when control is loaded
    }
    
    protected override void OnUnloaded()
    {
        base.OnUnloaded();
        // Called when control is unloaded
    }
    
    // 5. Handle template parts
    protected override void OnTemplateApplied(TemplateAppliedEventArgs e)
    {
        base.OnTemplateApplied(e);
        // e.NameScope - get template parts
        // e.OldScope - old template parts
    }
}
```

## 10. ControlTemplate

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

public class TemplateAppliedEventArgs : EventArgs
{
    public INameScope NameScope { get; }
    public INameScope OldScope { get; }
}

public class ApplyTemplateEventArgs : EventArgs
{
    public Control Control { get; }
}
```

## 11. IStyleable

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

## 12. IDataTemplate

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

## 13. IResourceProvider

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

## 14. ResourceDictionary

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

## 15. DynamicResourceExtension

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

## 16. StaticResourceExtension

```csharp
// Location: src/Avalonia.Markup.Xaml/
public class StaticResourceExtension : MarkupExtension
{
    public string? ResourceKey { get; set; }
    public object? Value { get; }
    
    public override object? ProvideValue(IServiceProvider serviceProvider);
}
```

## 17. XAML Markup Extensions

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

## 19. Platform Implementations

```csharp
// Windows (Avalonia.Win32)
public class Win32Platform : IPlatform
{
    public static Win32Platform Create();
}

// macOS (Avalonia.Native)
public class NativePlatform : IPlatform
{
    public static NativePlatform Create();
}

// X11 (Avalonia.X11)
public class X11Platform : IPlatform
{
    public static X11Platform Create();
}

// Skia (Avalonia.Skia)
public class SkiaPlatform : IPlatform
{
    public static SkiaPlatform Create();
}

// Browser (Avalonia.Browser)
public class BrowserPlatform : IPlatform
{
    public static BrowserPlatform Create();
}

// Headless (Avalonia.Headless)
public class HeadlessPlatform : IPlatform
{
    public static HeadlessPlatform Create();
}

// Metal (Avalonia.Metal)
public class MetalPlatform : IPlatform
{
    public static MetalPlatform Create();
}

// Vulkan (Avalonia.Vulkan)
public class VulkanPlatform : IPlatform
{
    public static VulkanPlatform Create();
}
```

## 20. Platform Interfaces

```csharp
// Location: src/Avalonia.Base/Platform/
// IPlatformBitmapDecoder
public interface IPlatformBitmapDecoder
{
    Bitmap Decode(Stream stream);
}

// IPlatformCursor
public interface IPlatformCursor
{
    Cursor Create(CursorShape shape);
    Cursor Create(Bitmap bitmap, Point hotSpot);
}

// IPlatformDragDropAdapter
public interface IPlatformDragDropAdapter
{
    void DoDragDrop(UIElement source, IDataObject data);
}

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
public interface IPlatformIconLoader
{
    Icon? Load(string path);
}

// IPlatformInputMethod
public interface IPlatformInputMethod
{
    void Show();
    void Hide();
    void SetPosition(Rect position);
}

// IPlatformNativeMenuExporter
public interface IPlatformNativeMenuExporter
{
    NativeMenu? Export(UIElement element, NativeMenu menu);
}

// IPlatformScreen
public interface IPlatformScreen
{
    Rect Bounds { get; }
    double Scale { get; }
    string? Name { get; }
}

// IPlatformScreens
public interface IPlatformScreens
{
    IReadOnlyList<IPlatformScreen> Screens { get; }
    IPlatformScreen? Primary { get; }
}

// IPlatformSettings
public interface IPlatformSettings
{
    string? GetUserAgent();
    string? GetLocale();
    string? GetTimeZone();
}

// IPlatformUriHandler
public interface IPlatformUriHandler
{
    void OpenUri(Uri uri);
}

// IPlatformClipboard
public interface IPlatformClipboard
{
    Task<string?> GetTextAsync();
    Task SetTextAsync(string text);
}

// IPlatformDragSource
public interface IPlatformDragSource
{
    void StartDrag(UIElement source, IDataObject data);
}

// IPlatformDropTarget
public interface IPlatformDropTarget
{
    void AddDropTarget(UIElement target);
    void RemoveDropTarget(UIElement target);
}

// IPlatformOpenUriHandler
public interface IPlatformOpenUriHandler
{
    void OpenUri(Uri uri);
}

// IPlatformVisualHost
public interface IPlatformVisualHost
{
    void Show(Visual visual);
    void Hide(Visual visual);
}

// IPlatformHwndHost
public interface IPlatformHwndHost
{
    IntPtr Handle { get; }
    void Show();
    void Hide();
}

// IPlatformHwndSource
public interface IPlatformHwndSource
{
    IntPtr Handle { get; }
    Rect Bounds { get; }
    void Show();
    void Hide();
}

// IPlatformHwndTarget
public interface IPlatformHwndTarget
{
    void Resize(Size size);
    void Invalidate();
}

// IPlatformRenderTarget
public interface IPlatformRenderTarget
{
    void Draw(IDrawingContextImpl context);
}

// IPlatformBitmapImpl
public interface IPlatformBitmapImpl
{
    Size Size { get; }
    void Draw(IDrawingContextImpl context);
}

// IPlatformBitmapEncoder
public interface IPlatformBitmapEncoder
{
    Bitmap Encode(Stream stream);
}

// IPlatformBitmapDecoder
public interface IPlatformBitmapDecoder
{
    Bitmap Decode(Stream stream);
}

// IPlatformCursorImpl
public interface IPlatformCursorImpl
{
    Cursor Create(CursorShape shape);
    Cursor Create(Bitmap bitmap, Point hotSpot);
}

// IPlatformDragDropAdapterImpl
public interface IPlatformDragDropAdapterImpl
{
    void DoDragDrop(UIElement source, IDataObject data);
}

// IPlatformFileSystemImpl
public interface IPlatformFileSystemImpl
{
    bool Exists(string path);
    bool IsDirectory(string path);
    Stream Open(string path, FileMode mode);
}

// IPlatformIconLoaderImpl
public interface IPlatformIconLoaderImpl
{
    Icon? Load(string path);
}

// IPlatformInputMethodImpl
public interface IPlatformInputMethodImpl
{
    void Show();
    void Hide();
    void SetPosition(Rect position);
}

// IPlatformNativeMenuExporterImpl
public interface IPlatformNativeMenuExporterImpl
{
    NativeMenu? Export(UIElement element, NativeMenu menu);
}

// IPlatformScreenImpl
public interface IPlatformScreenImpl
{
    Rect Bounds { get; }
    double Scale { get; }
    string? Name { get; }
}

// IPlatformScreensImpl
public interface IPlatformScreensImpl
{
    IReadOnlyList<IPlatformScreen> Screens { get; }
    IPlatformScreen? Primary { get; }
}

// IPlatformSettingsImpl
public interface IPlatformSettingsImpl
{
    string? GetUserAgent();
    string? GetLocale();
    string? GetTimeZone();
}

// IPlatformUriHandlerImpl
public interface IPlatformUriHandlerImpl
{
    void OpenUri(Uri uri);
}

// IPlatformVisualHostImpl
public interface IPlatformVisualHostImpl
{
    void Show(Visual visual);
    void Hide(Visual visual);
}

// IPlatformHwndHostImpl
public interface IPlatformHwndHostImpl
{
    IntPtr Handle { get; }
    void Show();
    void Hide();
}

// IPlatformHwndSourceImpl
public interface IPlatformHwndSourceImpl
{
    IntPtr Handle { get; }
    Rect Bounds { get; }
    void Show();
    void Hide();
}

// IPlatformHwndTargetImpl
public interface IPlatformHwndTargetImpl
{
    void Resize(Size size);
    void Invalidate();
}

// IPlatformRenderTargetImpl
public interface IPlatformRenderTargetImpl
{
    void Draw(IDrawingContextImpl context);
}
```

## 21. Key Files Reference

| File | Purpose |
|------|---------|
| `src/Avalonia.Controls/ItemsPresenter.cs` | ItemsPresenter |
| `src/Avalonia.Controls/Selection/ISelectable.cs` | ISelectable |
| `src/Avalonia.Controls/HeaderedContentControl.cs` | HeaderedContentControl |
| `src/Avalonia.Controls/HeaderedItemsControl.cs` | HeaderedItemsControl |
| `src/Avalonia.Controls/IColumnDefinition.cs` | IColumnDefinition |
| `src/Avalonia.Controls/IRowDefinition.cs` | IRowDefinition |
| `src/Avalonia.Controls/IOverlayWindow.cs` | IOverlayWindow |
| `src/Avalonia.Input/IClipboard.cs` | IClipboard |
| `src/Avalonia.Controls/IDialogService.cs` | IDialogService |
| `src/Avalonia.Storage/IStorageService.cs` | IStorageService |
| `src/Avalonia.Controls/ControlTemplate.cs` | ControlTemplate |
| `src/Avalonia.Base/Styling/IStyleable.cs` | IStyleable |
| `src/Avalonia.Base/Styling/ISupportsNestedStyle.cs` | ISupportsNestedStyle |
| `src/Avalonia.Base/Styling/IAttachedObject.cs` | IAttachedObject |
| `src/Avalonia.Base/Data/IDataTemplate.cs` | IDataTemplate |
| `src/Avalonia.Base/Data/IDataTemplateSelector.cs` | IDataTemplateSelector |
| `src/Avalonia.Base/Data/IHierarchicalDataTemplate.cs` | IHierarchicalDataTemplate |
| `src/Avalonia.Controls/IGlobalDataTemplates.cs` | IGlobalDataTemplates |
| `src/Avalonia.Base/Styling/IResourceProvider.cs` | IResourceProvider |
| `src/Avalonia.Base/Styling/IResourceNode.cs` | IResourceNode |
| `src/Avalonia.Base/Styling/IResourceHost.cs` | IResourceHost |
| `src/Avalonia.Base/Styling/ResourceDictionary.cs` | ResourceDictionary |
| `src/Avalonia.Markup.Xaml/DynamicResourceExtension.cs` | DynamicResourceExtension |
| `src/Avalonia.Markup.Xaml/StaticResourceExtension.cs` | StaticResourceExtension |
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
| `src/Avalonia.Base/Platform/IPlatform.cs` | IPlatform |
| `src/Avalonia.Win32/Win32Platform.cs` | Win32 platform |
| `src/Avalonia.Native/NativePlatform.cs` | macOS platform |
| `src/Avalonia.X11/X11Platform.cs` | X11 platform |
| `src/Avalonia.Skia/SkiaPlatform.cs` | Skia platform |
| `src/Avalonia.Browser/BrowserPlatform.cs` | Browser platform |
| `src/Avalonia.Headless/HeadlessPlatform.cs` | Headless platform |
| `src/Avalonia.Metal/MetalPlatform.cs` | Metal platform |
| `src/Avalonia.Vulkan/VulkanPlatform.cs` | Vulkan platform |