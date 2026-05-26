# Avalonia 12.0.3 Input & Events Reference

## 1. UIElement

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

## 2. IInputElement

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

## 3. RoutedEvent

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

## 4. RoutingStrategy

```csharp
// Location: src/Avalonia.RoutedEvents/
public enum RoutingStrategy
{
    Direct,      // Only the target element
    Tunneling,   // From root to target (Preview* events)
    Bubbling,    // From target to root (Click, etc.)
}
```

## 5. RoutedEventArgs

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

public class RoutedEventArgs<T> : RoutedEventArgs
{
    public T Data { get; }
}
```

## 6. KeyEventArgs

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
    None,
    Tab,
    Enter,
    Shift,
    Control,
    Alt,
    CapsLock,
    Escape,
    Space,
    Back,
    PageUp,
    PageDown,
    End,
    Home,
    Left,
    Up,
    Right,
    Down,
    Insert,
    Delete,
    F1, F2, F3, F4, F5, F6, F7, F8, F9, F10, F11, F12,
    D0, D1, D2, D3, D4, D5, D6, D7, D8, D9,
    A, B, C, D, E, F, G, H, I, J, K, L, M, N, O, P, Q, R, S, T, U, V, W, X, Y, Z,
    NumPad0, NumPad1, NumPad2, NumPad3, NumPad4, NumPad5, NumPad6, NumPad7, NumPad8, NumPad9,
    Multiply, Add, Subtract, Divide, Decimal,
    LShift, RShift, LControl, RControl, LAlt, RAlt,
    LWin, RWin,
    // ... and more
}

public enum KeyModifiers
{
    None = 0,
    Alt = 1,
    Control = 2,
    Shift = 4,
    Windows = 8,
    LeftShift = 16,
    RightShift = 32,
    LeftCtrl = 64,
    RightCtrl = 128,
    LeftAlt = 256,
    RightAlt = 512,
    LeftWin = 1024,
    RightWin = 2048,
}
```

## 7. PointerEventArgs

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

public class PointerPressedEventArgs : PointerEventArgs
{
    public PointerPressedEventArgs(PointerPoint currentPoint, PointerPoint? previousPoint);
}

public class PointerReleasedEventArgs : PointerEventArgs
{
    public PointerReleasedEventArgs(PointerPoint currentPoint, PointerPoint? previousPoint);
}

public class PointerWheelChangedEventArgs : PointerEventArgs
{
    public PointerWheelChangedEventArgs(PointerPoint currentPoint, PointerPoint? previousPoint);
    public Vector Delta { get; }
}
```

## 8. PointerPoint

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

public enum PointerDeviceType
{
    Mouse,
    Touch,
    Pen,
    TouchPad,
    TouchScreen,
}

public class PointerDevice
{
    public PointerDeviceType DeviceType { get; }
    public string? Name { get; }
}
```

## 9. IKeyHandler

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

## 10. VirtualInput

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

## 11. HotkeyManager

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

## 12. GestureRecognizer

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

public enum GestureType
{
    Tap,
    DoubleTap,
    LongPress,
    Swipe,
    Pinch,
    Rotate,
    Drag,
}
```

## 13. Media - Brush

```csharp
// Location: src/Avalonia.Base/Media/
public abstract class Brush : AvaloniaObject
{
    public static readonly StyledProperty<double> OpacityProperty;
    public double Opacity { get; set; }
}

public class SolidColorBrush : Brush
{
    public static readonly StyledProperty<Color> ColorProperty;
    public Color Color { get; set; }
}

public class LinearGradientBrush : Brush
{
    public Point StartPoint { get; set; }
    public Point EndPoint { get; set; }
    public IList<GradientStop> Stops { get; }
}

public class RadialGradientBrush : Brush
{
    public Point GradientOriginOffset { get; set; }
    public Point Center { get; set; }
    public double RadiusX { get; set; }
    public double RadiusY { get; set; }
    public IList<GradientStop> Stops { get; }
}

public class ImageBrush : Brush
{
    public Bitmap? Source { get; set; }
    public Stretch Stretch { get; set; }
    public Rect SourceRect { get; set; }
    public Rect Viewport { get; set; }
    public bool ViewportUnits { get; set; }
}

public class DrawingBrush : Brush
{
    public Drawing? Drawing { get; set; }
    public Stretch Stretch { get; set; }
}

public class TransformedBrush : Brush
{
    public Transform Transform { get; set; }
    public Brush? Brush { get; set; }
}
```

## 14. Media - Pen

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

## 15. Media - Geometry

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

public class PathGeometry : Geometry
{
    public IList<PathFigure> Figures { get; }
}

public class PathFigure : AvaloniaObject
{
    public Point StartPoint { get; set; }
    public IList<PathSegment> Segments { get; }
    public bool IsFilled { get; set; }
    public bool IsClosed { get; set; }
}

public abstract class PathSegment : AvaloniaObject
{
    public abstract Point EndPoint { get; }
    public abstract void AddSegment(PathFigure figure);
}

public class ArcSegment : PathSegment
{
    public Point Point { get; set; }
    public Size Size { get; set; }
    public double RotationAngle { get; set; }
    public bool IsLargeArc { get; set; }
    public SweepDirection SweepDirection { get; set; }
}

public class BezierSegment : PathSegment
{
    public Point Point1 { get; set; }
    public Point Point2 { get; set; }
    public override Point EndPoint => Point2;
}

public class LineSegment : PathSegment
{
    public Point Point { get; set; }
    public override Point EndPoint => Point;
}

public class PolyBezierSegment : PathSegment
{
    public IList<Point> Points { get; }
    public override Point EndPoint => Points[^1];
}

public class PolyLineSegment : PathSegment
{
    public IList<Point> Points { get; }
    public override Point EndPoint => Points[^1];
}

public class PolyQuadraticBezierSegment : PathSegment
{
    public IList<Point> Points { get; }
    public override Point EndPoint => Points[^1];
}

public class QuadraticBezierSegment : PathSegment
{
    public Point Point1 { get; set; }
    public Point Point2 { get; set; }
    public override Point EndPoint => Point2;
}

public class RectangleGeometry : Geometry
{
    public Rect Rect { get; set; }
    public double RadiusX { get; set; }
    public double RadiusY { get; set; }
}

public class EllipseGeometry : Geometry
{
    public Rect Rect { get; set; }
    public Point Center { get; set; }
}

public class LineGeometry : Geometry
{
    public Point StartPoint { get; set; }
    public Point EndPoint { get; set; }
}

public class CombinedGeometry : Geometry
{
    public Geometry Geometry1 { get; set; }
    public Geometry Geometry2 { get; set; }
    public CombinedGeometryMode GeometryCombineMode { get; set; }
}

public enum CombinedGeometryMode
{
    Union,
    Intersect,
    Exclude,
}
```

## 16. Media - Effects

```csharp
// Location: src/Avalonia.Base/Media/Effects/
public abstract class Effect : AvaloniaObject
{
    public abstract Effect Clone();
}

public class BlurEffect : Effect
{
    public double Radius { get; set; }
    public bool EnableFastPath { get; set; }
    public override Effect Clone();
}

public class DropShadowEffect : Effect
{
    public Color Color { get; set; }
    public double BlurRadius { get; set; }
    public double ShadowDepth { get; set; }
    public bool ShadowDepthIsInPixels { get; set; }
    public override Effect Clone();
}

public class GlowEffect : Effect
{
    public Color Color { get; set; }
    public double Radius { get; set; }
    public override Effect Clone();
}

public class EmbossEffect : Effect
{
    public double LightAngle { get; set; }
    public double LightDistance { get; set; }
    public double LightHeight { get; set; }
    public override Effect Clone();
}

public class BevelEffect : Effect
{
    public double BevelWidth { get; set; }
    public double BevelHeight { get; set; }
    public override Effect Clone();
}
```

## 17. Media - Transforms

```csharp
// Location: src/Avalonia.Base/Media/Transforms/
public abstract class Transform : AvaloniaObject
{
    public abstract Transform Clone();
    public abstract Rect TransformBounds(Rect bounds);
    public abstract Point Transform(Point point);
    public abstract Vector TransformVector(Vector vector);
}

public class TranslateTransform : Transform
{
    public double X { get; set; }
    public double Y { get; set; }
    public override Transform Clone();
    public override Rect TransformBounds(Rect bounds);
    public override Point Transform(Point point);
    public override Vector TransformVector(Vector vector);
}

public class ScaleTransform : Transform
{
    public double ScaleX { get; set; }
    public double ScaleY { get; set; }
    public override Transform Clone();
    public override Rect TransformBounds(Rect bounds);
    public override Point Transform(Point point);
    public override Vector TransformVector(Vector vector);
}

public class RotateTransform : Transform
{
    public double Angle { get; set; }
    public override Transform Clone();
    public override Rect TransformBounds(Rect bounds);
    public override Point Transform(Point point);
    public override Vector TransformVector(Vector vector);
}

public class SkewTransform : Transform
{
    public double AngleX { get; set; }
    public double AngleY { get; set; }
    public override Transform Clone();
    public override Rect TransformBounds(Rect bounds);
    public override Point Transform(Point point);
    public override Vector TransformVector(Vector vector);
}

public class MatrixTransform : Transform
{
    public Matrix Matrix { get; set; }
    public override Transform Clone();
    public override Rect TransformBounds(Rect bounds);
    public override Point Transform(Point point);
    public override Vector TransformVector(Vector vector);
}

public class TransformGroup : Transform
{
    public IList<Transform> Children { get; }
    public override Transform Clone();
    public override Rect TransformBounds(Rect bounds);
    public override Point Transform(Point point);
    public override Vector TransformVector(Vector vector);
}

public class Rotate3DTransform : Transform
{
    public Vector3D Axis { get; set; }
    public double Angle { get; set; }
    public override Transform Clone();
    public override Rect TransformBounds(Rect bounds);
    public override Point Transform(Point point);
    public override Vector TransformVector(Vector vector);
}
```

## 18. Media - Matrix

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

## 19. Key Files Reference

| File | Purpose |
|------|---------|
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