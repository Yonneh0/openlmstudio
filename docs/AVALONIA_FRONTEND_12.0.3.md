# Avalonia 12.0.3 Frontend RAG Reference

> **Scope:** Design considerations, layout system, control types, and implementation patterns for frontend development.

---

## 1. Design Considerations

### 1.1 Architecture Philosophy

Avalonia follows a **WPF-inspired** but **cross-platform** architecture:
- **Lookless controls** — appearance is defined by ControlTemplates, not hardcoded to platform
- **Two tree model** — Visual Tree (rendering) vs Logical Tree (styling/binding/resource lookup)
- **CSS-like styling** — supports both XAML styles and CSS syntax
- **Platform abstraction** — same UI code runs on Windows, macOS, Linux, Browser, Mobile

### 1.2 Visual Tree vs Logical Tree

| Aspect | Visual Tree | Logical Tree |
|--------|-------------|--------------|
| **Purpose** | Rendering & composition | Styling, data binding, resources |
| **Contains** | All visual elements (including template parts) | Structural elements (controls, data templates) |
| **Traversal** | `GetVisualParent()`, `GetVisualDescendants()` | `LogicalParent`, `LogicalChildren` |
| **Used for** | Hit testing, rendering, animations | Resource lookup, data context inheritance, styling |

```csharp
// Visual tree example
var parent = myControl.GetVisualParent<Panel>();
var descendants = myControl.GetVisualDescendants().OfType<Button>();

// Logical tree example
var logicalParent = myControl.LogicalParent;
var dataContext = myControl.DataContext; // inherited logically
```

### 1.3 Property Value Priority System

Properties resolve values by priority (highest to lowest):

```
Animation → Transient → LocalValue → StyleValue → ThemeValue → TemplatedParentTheme → Inherited → Unset
```

**Key implications:**
- `SetCurrentValue()` changes effective value without breaking bindings/styles
- `ClearValue()` removes local value, revealing style/theme value
- `SetValue()` with `BindingPriority.LocalValue` overrides styles

### 1.4 Styling Cascade

1. **Base value** — default from property registration
2. **Inherited** — from parent (if `inherits: true`)
3. **Theme value** — from active theme
4. **Style value** — from matching styles
5. **Local value** — set directly in code or markup
6. **Transient** — temporary (hover, focus)
7. **Animation** — highest priority, temporary

---

## 2. Layout System

### 2.1 Layout Model

Avalonia uses a **two-pass layout**:
1. **Measure** — children report desired size to parent
2. **Arrange** — parent assigns final bounds, children positioned

```csharp
public abstract class Panel : Decorator, ILayoutManager
{
    protected abstract Size MeasureOverride(Size availableSize);
    protected abstract Size ArrangeOverride(Size finalSize);
}
```

### 2.2 Layout Panels

| Panel | Behavior | Use Case |
|-------|----------|----------|
| **Grid** | Rows/Columns with star/auto/pixel sizing | Complex forms, dashboards |
| **StackPanel** | Vertical or horizontal stacking | Toolbars, lists |
| **WrapPanel** | Wraps children when space runs out | Tag clouds, responsive grids |
| **DockPanel** | Children dock to edges, last fills | Main window layouts |
| **UniformGrid** | Equal-sized cells | Photo galleries, tile layouts |
| **RelativePanel** | Position relative to other elements | Adaptive layouts, complex positioning |
| **Canvas** | Absolute positioning with Left/Top/Right/Bottom | Custom drawing, game UI |
| **Viewbox** | Scales single child | Logo containers, responsive images |
| **ScrollViewer** | Provides scrolling for overflow content | Long lists, tables |
| **VirtualizingStackPanel** | Virtualizes items for performance | Large lists, data grids |

### 2.3 Grid Layout (Detailed)

```csharp
// Column/Row definitions
public class Grid : Panel
{
    public ColumnDefinitions ColumnDefinitions { get; }
    public RowDefinitions RowDefinitions { get; }
    public double ColumnSpacing { get; set; }
    public double RowSpacing { get; set; }
    
    // Attached properties
    public static int GetColumn(Control element);
    public static void SetColumn(Control element, int value);
    public static int GetRow(Control element);
    public static void SetRow(Control element, int value);
    public static int GetColumnSpan(Control element);
    public static void SetColumnSpan(Control element, int value);
    public static int GetRowSpan(Control element);
    public static void SetRowSpan(Control element, int value);
}

// GridLength types
public struct GridLength
{
    public static GridLength Auto { get; }   // Size to content
    public static GridLength Star { get; }    // Proportional (e.g., "2*")
    public static GridLength Pixel { get; }   // Fixed pixels
}
```

**XAML example:**
```xml
<Grid ColumnSpacing="10" RowSpacing="10">
    <Grid.ColumnDefinitions>
        <ColumnDefinition Width="Auto"/>
        <ColumnDefinition Width="*"/>
        <ColumnDefinition Width="2*"/>
    </Grid.ColumnDefinitions>
    <Grid.RowDefinitions>
        <RowDefinition Height="Auto"/>
        <RowDefinition Height="*"/>
    </Grid.RowDefinitions>
    
    <TextBlock Grid.Column="0" Grid.Row="0" Text="Label"/>
    <TextBox Grid.Column="1" Grid.Row="0"/>
    <ContentControl Grid.Column="2" Grid.Row="0"/>
    <Border Grid.Column="0" Grid.Row="1" Grid.ColumnSpan="3"/>
</Grid>
```

### 2.4 Layout Properties

All controls inherit these from `ILayout`:

| Property | Type | Description |
|----------|------|-------------|
| `Width` | double | Desired width |
| `Height` | double | Desired height |
| `MinWidth` | double | Minimum width constraint |
| `MaxWidth` | double | Maximum width constraint |
| `MinHeight` | double | Minimum height constraint |
| `MaxHeight` | double | Maximum height constraint |
| `Margin` | Thickness | Outer spacing |
| `Padding` | Thickness | Inner spacing |
| `HorizontalAlignment` | enum | Left, Center, Right, Stretch |
| `VerticalAlignment` | enum | Top, Center, Bottom, Stretch |

### 2.5 Layout Events

```csharp
public event EventHandler<LayoutEventArgs>? LayoutUpdated;

// Invalidate to trigger re-layout
control.InvalidateMeasure();
control.InvalidateArrange();
```

---

## 3. Control Types

### 3.1 Control Hierarchy

```
AvaloniaObject (base, property system)
└── Animatable (animations)
    └── StyledElement (DataContext, styles, resources)
        └── Visual (visual tree)
            └── Control (templates)
                ├── ContentControl (single child)
                │   ├── TextBlock, TextBox, Image, Label, etc.
                ├── ItemsControl (collection of items)
                │   ├── ListBox, ListView, TreeView, TabControl, Menu
                ├── Panel (multiple children, layout)
                │   ├── Grid, StackPanel, DockPanel, Canvas, etc.
                ├── Decorator (single child, no layout)
                │   ├── Border, BoxView, Viewbox
                ├── Shape (vector drawing)
                │   ├── Rectangle, Ellipse, Line, Polygon, Polyline
                └── ButtonBase / ToggleButtonBase (input)
                    ├── Button, CheckBox, RadioButton, ComboBox, Slider
            └── UIElement (input events)
            └── TopLevel (Window, Popup, Flyout)
```

### 3.2 Content Controls (Single Child)

| Control | Purpose | Key Properties |
|---------|---------|----------------|
| **TextBlock** | Read-only text | `Text`, `Inlines`, `FontFamily`, `FontSize` |
| **TextBox** | Editable text | `Text`, `Watermark`, `IsReadOnly`, `SelectionStart` |
| **PasswordBox** | Password input | `Password`, `Watermark`, `IsPasswordRevealEnabled` |
| **Image** | Bitmap display | `Source`, `Stretch`, `StretchDirection` |
| **Label** | Labeled content | `Target`, `Content`, `ContentTemplate` |
| **Expander** | Collapsible content | `Header`, `IsExpanded`, `ExpandDirection` |
| **ProgressBar** | Progress indicator | `Value`, `Minimum`, `Maximum`, `IsIndeterminate` |
| **ProgressRing** | Circular progress | `IsActive` |
| **Viewbox** | Scaled content | `Stretch`, `StretchDirection` |
| **Border** | Decorated container | `Background`, `BorderBrush`, `BorderThickness`, `CornerRadius` |

### 3.3 Items Controls (Collections)

| Control | Purpose | Key Features |
|---------|---------|--------------|
| **ItemsControl** | Base collection | `Items`, `ItemsSource`, `ItemTemplate` |
| **ListBox** | Selectable list | `SelectedItem`, `SelectedItems`, `SelectionMode` |
| **ListView** | Column-based list | `IsVirtualizing`, `IsGroupingEnabled`, `IsSortingEnabled` |
| **TreeView** | Hierarchical data | `IsExpanded`, `HasItems`, `Children` |
| **TabControl** | Tabbed pages | `SelectedIndex`, `TabStripPlacement`, `IsContentPreserved` |
| **Menu** | Navigation menu | `Items`, `IsOpen`, `IsKeyboardFocused` |
| **MenuItem** | Menu item | `Header`, `Icon`, `Command`, `IsChecked` |
| **ContextMenu** | Context menu | `StaysOpen`, `PlacementTarget` |
| **Carousel** | Sliding items | `CurrentItem`, `IsLoopEnabled`, `IsSwipeEnabled` |
| **ComboBox** | Dropdown selection | `SelectedItem`, `IsDropDownOpen`, `IsEditable` |

### 3.4 Input Controls

| Control | Purpose | Key Properties |
|---------|---------|----------------|
| **Button** | Click action | `Command`, `CommandParameter`, `IsPressed` |
| **RepeatButton** | Repeated click | `Interval`, `Delay` |
| **ToggleButton** | On/off state | `IsChecked`, `Checked`, `Unchecked` events |
| **CheckBox** | Boolean toggle | `IsChecked`, `Content` |
| **RadioButton** | Mutually exclusive | `GroupName`, `IsChecked` |
| **Slider** | Range selection | `Value`, `Minimum`, `Maximum`, `TickFrequency` |
| **RangeSlider** | Double range | `LowerValue`, `UpperValue` |
| **Rating** | Star rating | `Value`, `ItemCount`, `AllowPartialRatings` |
| **NumericUpDown** | Numeric input | `Value`, `Increment`, `DecimalPlaces` |
| **DatePicker** | Date selection | `SelectedDate`, `IsDropDownOpen` |
| **TimePicker** | Time selection | `SelectedTime`, `MinuteIncrement` |
| **Calendar** | Calendar widget | `SelectedDate`, `SelectionMode` |

### 3.5 Window & Popup Types

| Type | Purpose | Key Methods |
|------|---------|-------------|
| **Window** | Top-level window | `Show()`, `ShowDialog()`, `Close()`, `Minimize()`, `Maximize()` |
| **Popup** | Floating overlay | `Open()`, `Close()`, `PlacementMode` |
| **Flyout** | Slide-out panel | `IsOpen`, `Placement` (Top/Bottom/Left/Right) |
| **OverlayPopup** | Modal overlay | `IsOpen`, `StaysOpen`, `IsLightDismissEnabled` |
| **TrayIcon** | System tray icon | `Icon`, `ToolTip`, `Flyout`, `Menu` |

---

## 4. Implementation Details

### 4.1 Property System

#### Property Registration Patterns

```csharp
// 1. Styled Property (styling, inheritance, defaults)
public static readonly StyledProperty<string?> TextProperty =
    AvaloniaProperty.Register<TextBlock, string?>(
        nameof(Text),
        defaultValue: "",
        inherits: false,
        validate: ValidateText,
        coerce: CoerceText);

public string? Text
{
    get => GetValue(TextProperty);
    set => SetValue(TextProperty, value);
}

// 2. Direct Property (backed by field, immediate notification)
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

// 3. Attached Property (set on any element)
public static readonly AttachedProperty<int> MyAttachedProperty =
    AvaloniaProperty.RegisterAttached<MyAttached, Control, int>(
        nameof(MyAttachedProperty),
        defaultValue: 0);

public static void SetMyAttachedProperty(Control target, int value)
    => target.SetValue(MyAttachedProperty, value);

public static int GetMyAttachedProperty(Control target)
    => target.GetValue(MyAttachedProperty);
```

#### Property Methods

| Method | Behavior |
|--------|----------|
| `GetValue()` | Gets effective value (resolves priority) |
| `SetValue()` | Sets local value, breaks lower-priority values |
| `SetCurrentValue()` | Changes effective value without breaking bindings/styles |
| `ClearValue()` | Removes local value, reveals style/theme value |
| `CoerceValue()` | Forces coerce callback to run |
| `IsSet()` | Checks if property has local value or binding |

### 4.2 Data Binding

#### Binding Basics

```csharp
// Simple binding
var binding = new Binding { Path = "UserName", Mode = BindingMode.TwoWay };
myTextBox.Bind(TextBox.TextProperty, binding);

// Compiled binding (Avalonia 12)
// XAML: <TextBox Text="{x:Bind ViewModel.Name, Mode=TwoWay}" x:DataType="vm:MyViewModel" />

// MultiBinding
var multiBinding = new MultiBinding
{
    Converter = new ThicknessConverter(),
    Bindings = new List<BindingBase>
    {
        new Binding("Padding.Left"),
        new Binding("Padding.Right"),
    }
};
```

#### Binding Modes

| Mode | Direction | Use Case |
|------|-----------|----------|
| `OneWay` | Source → Target | Display data |
| `TwoWay` | Source ↔ Target | Editable data |
| `OneTime` | Source → Target (once) | Static display |
| `OneWayToSource` | Target → Source | Output-only binding |

#### RelativeSource

```csharp
// Self reference
RelativeSource.Self

// Templated parent (inside ControlTemplate)
RelativeSource.TemplatedParent

// Find ancestor by type
RelativeSource.FindAncestor<Window>()
RelativeSource.FindAncestor<TabControl>()

// Previous data (in items controls)
RelativeSource.PreviousData
```

#### DataTemplates

```csharp
// Type-based template selection
public class DataTemplate
{
    public Type? DataType { get; set; }  // Matches data type
    public object? Content { get; set; }  // Static content
    public object? Template { get; set; } // ControlTemplate reference
}

// Hierarchical template (for nested data)
public class HierarchicalDataTemplate : DataTemplate
{
    public IEnumerable? ItemsSourcePath { get; set; }
    public DataTemplate? ItemTemplate { get; set; }
}

// Custom template selector
public class MyTemplateSelector : DataTemplateSelector
{
    public DataTemplate? StudentTemplate { get; set; }
    public DataTemplate? TeacherTemplate { get; set; }
    
    public override DataTemplate? SelectTemplate(object? item, object? container)
    {
        return item is Person p && p.IsTeacher ? TeacherTemplate : StudentTemplate;
    }
}
```

### 4.3 Styling System

#### Style Application Order

1. **Base style** (from `StyleKey`)
2. **Explicit styles** (in `Styles` collection)
3. **Theme styles** (from active theme)
4. **CSS styles** (if CSS styling enabled)

#### Style Selectors

```csharp
// Type selector
new Style { Selector = typeof(Button) }

// Class selector
new Style { SelectorString = ".my-class" }

// Pseudo-class selectors
":hover"    // element is hovered
":focus"    // element has focus
":pressed"  // element is pressed
":disabled" // element is disabled
":checked"  // element is checked
":first-child"
":last-child"
":nth-child(2n)"
":not(.disabled)"

// Combined selector
new Style { SelectorString = "Button:hover, Button.pressed" }
```

#### Setter Types

```csharp
// Property setter
new Setter { Property = TextBox.TextProperty, Value = "Default" }

// Typed setter
new Setter<string> { Property = TextBox.TextProperty, Value = "Default" }

// Multi-setter
new MultiSetter
{
    Setters = new List<SetterBase>
    {
        new Setter { Property = Control.BackgroundProperty, Value = Brushes.LightGray },
        new Setter { Property = Control.BorderBrushProperty, Value = Brushes.Black },
    }
}
```

#### Resource System

```csharp
// Resource dictionary
var resources = new ResourceDictionary
{
    ["PrimaryColor"] = Colors.Blue,
    ["MyTemplate"] = new DataTemplate { DataType = typeof(MyModel) }
};

// Dynamic resource (updates when resource changes)
new DynamicResourceExtension { ResourceKey = "PrimaryColor" }

// Static resource (resolved once)
new StaticResourceExtension { ResourceKey = "PrimaryColor" }

// Merged dictionaries
var merged = new MergedDictionaries
{
    new ResourceDictionary { Source = new Uri("pack://application:,,,/Themes/Dark.xaml") },
    new ResourceDictionary { Source = new Uri("pack://application:,,,/Themes/Controls.xaml") },
};
```

### 4.4 Event System

#### Routed Events

```csharp
// Event routing strategies
public enum RoutingStrategy
{
    Direct,      // Only the target element
    Tunneling,   // Root → Target (Preview* events)
    Bubbling,    // Target → Root (Click, etc.)
}

// Register routed event
public static readonly RoutedEvent<RoutedEventArgs> ClickEvent =
    RoutedEvent.Register<Button, RoutedEventArgs>(
        nameof(ClickEvent),
        RoutingStrategy.Bubbling);

// Handle events
button.ClickEvent.AddHandler((sender, e) => { /* handle */ });
button.AddHandler(UIElement.PointerPressedEvent, OnPointerPressed);
```

#### Input Events

| Event | Type | Routing | Description |
|-------|------|---------|-------------|
| `KeyDown` | KeyEventArgs | Bubbling | Key pressed |
| `KeyUp` | KeyEventArgs | Bubbling | Key released |
| `PointerPressed` | PointerPressedEventArgs | Bubbling | Mouse/touch down |
| `PointerReleased` | PointerReleasedEventArgs | Bubbling | Mouse/touch up |
| `PointerMoved` | PointerEventArgs | Bubbling | Pointer moving |
| `PointerEntered` | PointerEventArgs | Bubbling | Pointer entered element |
| `PointerExited` | PointerEventArgs | Bubbling | Pointer left element |
| `PointerWheelChanged` | PointerWheelChangedEventArgs | Bubbling | Mouse wheel |
| `TextInput` | TextInputEventArgs | Bubbling | Text input |

#### Key Modifiers

```csharp
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
}

// Check modifiers in event
if ((e.KeyModifiers & KeyModifiers.Control) != 0)
{
    // Ctrl is pressed
}
```

### 4.5 ControlTemplate System

#### Template Application

```csharp
public class Control : Visual, IControl
{
    public ControlTemplate? Template { get; set; }
    public ControlTemplate? TemplateWithoutContent { get; }
    public string? TemplateKey { get; }
    
    public event EventHandler<ControlTemplateAppliedEventArgs>? TemplateApplied;
    
    protected override void OnApplyTemplate(ApplyTemplateEventArgs e);
    protected override void OnTemplateChanged(ControlTemplate? oldTemplate, ControlTemplate? newTemplate);
}

// Template parts
protected override void OnTemplateApplied(TemplateAppliedEventArgs e)
{
    base.OnTemplateApplied(e);
    
    // Find template parts by name
    var myBorder = e.NameScope.FindName<MyBorder>("PART_Background");
    var myButton = e.NameScope.FindName<Button>("PART_Button");
    
    // Wire up events
    myButton.Click += OnButtonClick;
}
```

#### ControlTemplate Structure

```csharp
public class ControlTemplate
{
    public string? Name { get; set; }
    public IList<Setter> Setters { get; }
    public IList<Control> Children { get; }
    public ITemplate<Control> CreateInstance { get; }
    
    public Control Instantiate(INameScope nameScope);
    public Control Instantiate(INameScope nameScope, object? dataContext);
}
```

### 4.6 Animation System

#### Basic Animation

```csharp
// Begin animation on property
myControl.BeginAnimation(TextBox.OpacityProperty, new Animation
{
    Duration = TimeSpan.FromSeconds(0.5),
    Easing = new CubicEaseInOut(),
    FillMode = FillMode.Forward,
    KeyFrames = new List<KeyFrame>
    {
        new DoubleKeyFrame { Offset = 0.0, Value = 0.0 },
        new DoubleKeyFrame { Offset = 1.0, Value = 1.0 },
    }
});

// Stop animation
myControl.StopAnimation(TextBox.OpacityProperty);
myControl.StopAnimations(); // All animations
```

#### Easing Functions

| Easing | Formula | Effect |
|--------|---------|--------|
| `LinearEasing` | `x` | Constant speed |
| `CubicEaseIn` | `x³` | Accelerates |
| `CubicEaseOut` | `1 - (1-x)³` | Decelerates |
| `CubicEaseInOut` | Combined | Smooth accel/decel |
| `ElasticEaseIn/Out/InOut` | Oscillating | Bouncy |
| `BackEaseIn/Out/InOut` | Overshoot | Pulls back then forward |
| `SpringEasing` | Damped oscillation | Springy |

#### Page Transitions

```csharp
// Cross fade
var transition = new CrossFade { Duration = TimeSpan.FromSeconds(0.3) };

// Slide
var slide = new PageSlide 
{ 
    Duration = TimeSpan.FromSeconds(0.4),
    Direction = new Vector(1, 0) // Right to left
};

// Connected animation (material design style)
var connected = ConnectedAnimationService.Get("myAnimation");
connected.Transition(fromElement, toElement, arrange);
```

### 4.7 Threading & Dispatcher

```csharp
// Dispatcher priorities
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

// Post work to dispatcher
Dispatcher.UIThread.Post(() => { /* update UI */ });
Dispatcher.UIThread.Post(myAction, Priority.Render);

// Invoke and wait
var result = Dispatcher.UIThread.Invoke(() => myControl.Text);

// Check/verify access
if (Dispatcher.UIThread.CheckAccess())
{
    // On UI thread
}
else
{
    Dispatcher.UIThread.Invoke(() => { /* switch to UI thread */ });
}
```

### 4.8 Custom Control Development

#### Step-by-Step Pattern

```csharp
public class MyCustomControl : Control
{
    // 1. Register properties
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<MyCustomControl, string?>(
            nameof(Text),
            defaultValue: "Hello");
    
    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }
    
    // 2. Register direct properties
    public static readonly DirectProperty<MyCustomControl, int> CountProperty =
        AvaloniaProperty.RegisterDirect<MyCustomControl, int>(
            nameof(Count),
            o => o.Count,
            (o, v) => o.Count = v);
    
    private int _count;
    public int Count
    {
        get => _count;
        set => SetAndRaise(CountProperty, ref _count, value);
    }
    
    // 3. Override lifecycle methods
    protected override void OnApplyTemplate(ApplyTemplateEventArgs e)
    {
        base.OnApplyTemplate(e);
        // Template is now applied, find parts
    }
    
    protected override void OnTemplateApplied(TemplateAppliedEventArgs e)
    {
        base.OnTemplateApplied(e);
        // Wire up template parts
    }
    
    protected override void OnAttachedToLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnAttachedToLogicalTree(e);
        // Control is now in logical tree
    }
    
    protected override void OnDetachedFromLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromLogicalTree(e);
        // Clean up
    }
    
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        // DataContext changed
    }
    
    protected override void OnLoaded()
    {
        base.OnLoaded();
        // Control is loaded into visual tree
    }
    
    protected override void OnUnloaded()
    {
        base.OnUnloaded();
        // Control is unloaded
    }
}
```

#### XAML Styling

```xml
<!-- Style definition -->
<Styles>
    <Style Selector="Button">
        <Setter Property="Background" Value="LightGray"/>
        <Setter Property="Padding" Value="10,5"/>
    </Style>
    
    <Style Selector="Button:hover">
        <Setter Property="Background" Value="LightBlue"/>
    </Style>
    
    <Style Selector="Button:pressed">
        <Setter Property="Background" Value="Blue"/>
    </Style>
    
    <Style Selector="Button.my-class">
        <Setter Property="Foreground" Value="White"/>
    </Style>
</Styles>
```

### 4.9 Data Validation

```csharp
// Enable validation on property
public static readonly StyledProperty<string?> TextProperty =
    AvaloniaProperty.Register<TextBox, string?>(
        nameof(Text),
        enableDataValidation: true);

// Add errors
DataValidationErrors.SetErrors(myControl, new Exception("Invalid value"));
DataValidationErrors.SetErrors(myControl, new List<Exception> { error1, error2 });

// Check error state
bool isError = DataValidationErrors.GetIsError(myControl);
IList<Exception>? errors = DataValidationErrors.GetErrors(myControl);

// Clear errors
DataValidationErrors.ClearErrors(myControl);
```

### 4.10 Resource Lookup Chain

```
1. Element's Resources dictionary
2. Parent's Resources (logical tree walk)
3. Application.Resources
4. Theme.Resources (active theme variant)
5. Global resources
```

```csharp
// Try get resource
if (myControl.TryGetResource("MyKey", null, out var value))
{
    // Resource found
}

// Resource inheritance
myControl.Resources["ChildResource"] = "child value";
var resolved = myControl.GetValue(ResourceProperty); // "child value"
```

---

## 5. Quick Reference

### Layout Properties Cheat Sheet

| Property | Default | Notes |
|----------|---------|-------|
| `Width` | `double.NaN` | NaN = auto-size |
| `Height` | `double.NaN` | NaN = auto-size |
| `MinWidth` | 0 | |
| `MaxWidth` | `double.MaxValue` | |
| `MinHeight` | 0 | |
| `MaxHeight` | `double.MaxValue` | |
| `Margin` | `Thickness.Zero` | Left, Top, Right, Bottom |
| `Padding` | `Thickness.Zero` | Inner spacing |
| `HorizontalAlignment` | `Stretch` | Left, Center, Right, Stretch |
| `VerticalAlignment` | `Stretch` | Top, Center, Bottom, Stretch |

### Control Selection Guide

| Scenario | Recommended Control |
|----------|-------------------|
| Display text | `TextBlock` |
| Edit text | `TextBox` |
| Show image | `Image` |
| Single content | `ContentControl` |
| List of items | `ListBox` / `ListView` |
| Hierarchical data | `TreeView` |
| Tabbed interface | `TabControl` |
| Dropdown selection | `ComboBox` |
| Range selection | `Slider` / `RangeSlider` |
| Date/time pick | `DatePicker` / `TimePicker` |
| Navigation menu | `Menu` / `MenuItem` |
| Floating overlay | `Popup` / `Flyout` |
| Main window | `Window` |

### Styling Cheat Sheet

| Selector | Syntax | Example |
|----------|--------|---------|
| Type | `Button` | All buttons |
| Class | `.my-class` | Elements with class |
| ID | `#my-id` | Named element |
| Pseudo | `:hover`, `:focus`, `:pressed` | State-based |
| Descendant | `A B` | B inside A |
| Child | `A > B` | B direct child of A |
| Combined | `A, B` | Union |
| Nested | `A:hover` | A in hover state |

---

*End of AVALONIA_FRONTEND_12.0.3 Frontend RAG Reference*
