# Avalonia 12.0.3 Layout System Reference

## 1. ILayout Interface

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

## 2. Layout Operations

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

## 3. Measure/Arrange Cycle

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

## 4. Grid

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
public class ColumnDefinitions : DefinitionList<ColumnDefinition>
{
    public ColumnDefinitions();
    public new ColumnDefinition this[int index] { get; set; }
}

// RowDefinitions
public class RowDefinitions : DefinitionList<RowDefinition>
{
    public RowDefinitions();
    public new RowDefinition this[int index] { get; set; }
}

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

## 5. StackPanel

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

## 6. WrapPanel

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

public enum WrapOrientation
{
    Horizontal,
    Vertical,
}
```

## 7. DockPanel

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

public enum Dock
{
    Left,
    Top,
    Right,
    Bottom,
}
```

## 8. UniformGrid

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

## 9. RelativePanel

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

## 10. Canvas

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

## 11. Viewbox

```csharp
// Location: src/Avalonia.Controls/Viewbox.cs
public class Viewbox : Decorator
{
    public Stretch Stretch { get; set; }
    public StretchDirection StretchDirection { get; set; }
    
    protected override Size MeasureOverride(Size availableSize);
    protected override Size ArrangeOverride(Size finalSize);
}

public enum Stretch
{
    Fill,
    Uniform,
    UniformToFill,
    None,
}

public enum StretchDirection
{
    UpAndDown,
    Up,
    Down,
}
```

## 12. Border

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

## 13. ScrollViewer

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

public enum ScrollOrientation
{
    Both,
    Vertical,
    Horizontal,
}

public enum ScrollBarVisibility
{
    Disabled,
    Enabled,
    Auto,
    Visible,
}
```

## 14. VirtualizingStackPanel

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

## 15. GridSplitter

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

public enum ResizeDirection
{
    Rows,
    Columns,
    Both,
}

public enum ResizeBehavior
{
    PreviousAndCurrent,
    CurrentAndNext,
    Previous,
    Next,
    Current,
}
```

## 16. GridLength

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

public enum GridUnitType
{
    Pixel,
    Auto,
    Star,
}
```

## 17. LayoutHelper

```csharp
// Location: src/Avalonia.Base/Layout/LayoutHelper.cs
public static class LayoutHelper
{
    public static Size ClampSize(Size size, double minWidth, double maxWidth, double minHeight, double maxHeight);
    public static Size Expand(Size size, Thickness margin, Thickness padding);
    public static Size Contract(Size size, Thickness margin, Thickness padding);
    public static Rect ArrangeWithConstraints(Rect bounds, ILayout child);
    public static Size MeasureChild(ILayout child, Size availableSize);
}
```

## 18. LayoutInformation

```csharp
// Location: src/Avalonia.Base/Layout/LayoutInformation.cs
public static class LayoutInformation
{
    public static Size GetDesiredSize(UIElement element);
    public static Rect GetLayoutBounds(UIElement element);
    public static Rect GetEffectiveBounds(UIElement element);
    public static Size GetLayoutSlot(UIElement element, UIElement parent);
    public static Size GetLayoutSlot(UIElement element, Panel panel);
}
```

## 19. Key Files Reference

| File | Purpose |
|------|---------|
| `src/Avalonia.Base/Layout/` | Layout interfaces and helpers |
| `src/Avalonia.Controls/Grid.cs` | Grid implementation |
| `src/Avalonia.Controls/StackPanel.cs` | StackPanel |
| `src/Avalonia.Controls/WrapPanel.cs` | WrapPanel |
| `src/Avalonia.Controls/DockPanel.cs` | DockPanel |
| `src/Avalonia.Controls/UniformGrid.cs` | UniformGrid |
| `src/Avalonia.Controls/RelativePanel.cs` | RelativePanel |
| `src/Avalonia.Controls/Canvas.cs` | Canvas |
| `src/Avalonia.Controls/Viewbox.cs` | Viewbox |
| `src/Avalonia.Controls/Border.cs` | Border |
| `src/Avalonia.Controls/ScrollViewer.cs` | ScrollViewer |
| `src/Avalonia.Controls/VirtualizingStackPanel.cs` | VirtualizingStackPanel |
| `src/Avalonia.Controls/GridSplitter.cs` | GridSplitter |
| `src/Avalonia.Controls/GridLength.cs` | GridLength |
| `src/Avalonia.Controls/DefinitionBase.cs` | DefinitionBase |
| `src/Avalonia.Controls/DefinitionList.cs` | DefinitionList |