# Avalonia 12.0.3 Content Controls Reference

## 1. ContentControl

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

## 2. TextBlock

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

## 3. TextBox

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

## 4. PasswordBox

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

## 5. Image

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

## 6. Label

```csharp
// Location: src/Avalonia.Controls/Label.cs
public class Label : ContentControl
{
    public UIElement? Target { get; set; }
    public object? Content { get; set; }
    public DataTemplate? ContentTemplate { get; set; }
}
```

## 7. Expander

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

public enum ExpandDirection
{
    Down,
    Up,
    Left,
    Right,
}
```

## 8. ProgressBar

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

public enum ProgressBarStyle
{
    Default,
    Determinate,
    Indeterminate,
}
```

## 9. ProgressRing

```csharp
// Location: src/Avalonia.Controls/ProgressRing.cs
public class ProgressRing : ContentControl
{
    public bool IsActive { get; set; }
    
    protected override void OnApplyTemplate(ApplyTemplateEventArgs e);
}
```

## 10. LayoutTransformControl

```csharp
// Location: src/Avalonia.Controls/LayoutTransformControl.cs
public class LayoutTransformControl : ContentControl
{
    public Transform LayoutTransform { get; set; }
}
```

## 11. TransitioningContentControl

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

## 12. FlipView

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

## 13. InfoBanner

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

public enum InfoBannerType
{
    Information,
    Warning,
    Error,
    Success,
}
```

## 14. SelectableTextBlock

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

## 15. AvaloniaEditor

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

## 16. Key Files Reference

| File | Purpose |
|------|---------|
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