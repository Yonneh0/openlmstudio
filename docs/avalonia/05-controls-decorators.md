# Avalonia 12.0.3 Decorator Controls Reference

## 1. Decorator Base Class

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

## 2. Border

```csharp
// Location: src/Avalonia.Controls/Border.cs
public class Border : Decorator
{
    public Brush? Background { get; set; }
    public Brush? BorderBrush { get; set; }
    public Thickness BorderThickness { get; set; }
    public CornerRadius CornerRadius { get; set; }
    
    // Visual representation
    protected override void OnRender(DrawingContext context);
}
```

## 3. BoxView

```csharp
// Location: src/Avalonia.Controls/BoxView.cs
public class BoxView : Decorator
{
    public Color Color { get; set; }
    public Brush? Background { get; set; }
    
    protected override void OnRender(DrawingContext context);
}
```

## 4. Canvas

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

## 5. Viewbox

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

## 6. Viewbox2D

```csharp
// Location: src/Avalonia.Controls/Viewbox2D.cs
public class Viewbox2D : Decorator
{
    public Stretch Stretch { get; set; }
    public StretchDirection StretchDirection { get; set; }
    
    protected override Size MeasureOverride(Size availableSize);
    protected override Size ArrangeOverride(Size finalSize);
}
```

## 7. Viewbox2DWithScrolling

```csharp
// Location: src/Avalonia.Controls/Viewbox2DWithScrolling.cs
public class Viewbox2DWithScrolling : Decorator
{
    public Stretch Stretch { get; set; }
    public StretchDirection StretchDirection { get; set; }
    public ScrollBarVisibility VerticalScrollBarVisibility { get; set; }
    public ScrollBarVisibility HorizontalScrollBarVisibility { get; set; }
}
```

## 8. Shape

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

## 9. Rectangle

```csharp
// Location: src/Avalonia.Base/Media/Rectangle.cs
public class Rectangle : Shape
{
    public CornerRadius CornerRadius { get; set; }
    protected override Geometry DefiningGeometry { get; }
}
```

## 10. Ellipse

```csharp
// Location: src/Avalonia.Base/Media/Ellipse.cs
public class Ellipse : Shape
{
    protected override Geometry DefiningGeometry { get; }
}
```

## 11. Line

```csharp
// Location: src/Avalonia.Base/Media/Line.cs
public class Line : Shape
{
    public Point StartPoint { get; set; }
    public Point EndPoint { get; set; }
    protected override Geometry DefiningGeometry { get; }
}
```

## 12. Polygon

```csharp
// Location: src/Avalonia.Base/Media/Polygon.cs
public class Polygon : Shape
{
    public PointCollection Points { get; set; }
    public Stretch Stretch { get; set; }
    protected override Geometry DefiningGeometry { get; }
}
```

## 13. Polyline

```csharp
// Location: src/Avalonia.Base/Media/Polyline.cs
public class Polyline : Shape
{
    public PointCollection Points { get; set; }
    public Stretch Stretch { get; set; }
    protected override Geometry DefiningGeometry { get; }
}
```

## 14. IconElement

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
```

## 15. PathIcon

```csharp
// Location: src/Avalonia.Controls/PathIcon.cs
public class PathIcon : IconElement
{
    public static readonly StyledProperty<Geometry> GeometryProperty;
    public Geometry Geometry { get; set; }
}
```

## 16. ExperimentalAcrylicBorder

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
```

## 17. AcrylicPlatformCompensationLevels

```csharp
// Location: src/Avalonia.Controls/AcrylicPlatformCompensationLevels.cs
public class AcrylicPlatformCompensationLevels
{
    public double Noise { get; set; }
    public double Fallback { get; set; }
    public double Tint { get; set; }
}
```

## 18. Shape Properties Reference

| Property | Type | Description |
|----------|------|-------------|
| Fill | Brush | Fill brush |
| Stroke | Brush | Stroke brush |
| StrokeThickness | double | Stroke width |
| StrokeLineCap | PenLineCap | Line cap style |
| StrokeLineJoin | PenLineJoin | Line join style |
| StrokeDashArray | double[] | Dash pattern |
| StrokeDashOffset | double | Dash offset |
| StrokeMiterLimit | double | Miter limit |

## 19. Key Files Reference

| File | Purpose |
|------|---------|
| `src/Avalonia.Controls/Decorator.cs` | Decorator base |
| `src/Avalonia.Controls/Border.cs` | Border |
| `src/Avalonia.Controls/BoxView.cs` | BoxView |
| `src/Avalonia.Controls/Canvas.cs` | Canvas |
| `src/Avalonia.Controls/Viewbox.cs` | Viewbox |
| `src/Avalonia.Controls/Viewbox2D.cs` | Viewbox2D |
| `src/Avalonia.Controls/Viewbox2DWithScrolling.cs` | Viewbox2DWithScrolling |
| `src/Avalonia.Base/Media/Shape.cs` | Shape base |
| `src/Avalonia.Base/Media/Rectangle.cs` | Rectangle |
| `src/Avalonia.Base/Media/Ellipse.cs` | Ellipse |
| `src/Avalonia.Base/Media/Line.cs` | Line |
| `src/Avalonia.Base/Media/Polygon.cs` | Polygon |
| `src/Avalonia.Base/Media/Polyline.cs` | Polyline |
| `src/Avalonia.Controls/IconElement.cs` | IconElement |
| `src/Avalonia.Controls/PathIcon.cs` | PathIcon |
| `src/Avalonia.Controls/ExperimentalAcrylicBorder.cs` | ExperimentalAcrylicBorder |