# Avalonia Design Guide (Avalonia 12)

> **Generated from**: [docs.avaloniaui.net](https://docs.avaloniaui.net) and [AvaloniaUI/Avalonia](https://github.com/AvaloniaUI/Avalonia)
> **Version**: Avalonia 12
> **Target**: Cross-platform UI development with .NET

---

## Table of Contents

- [What is Avalonia?](#what-is-avalonia)
- [Platform Support](#platform-support)
- [Core Concepts](#core-concepts)
- [Layout System](#layout-system)
- [Styling System](#styling-system)
- [Data Binding](#data-binding)
- [Data Templates](#data-templates)
- [Controls Reference](#controls-reference)
- [Custom Controls](#custom-controls)
- [Animation](#animation)
- [Input and Interaction](#input-and-interaction)
- [Graphics and Animation](#graphics-and-animation)
- [Window and Popup Controls](#window-and-popup-controls)
- [Accessibility](#accessibility)
- [Platform-Specific Integration](#platform-specific-integration)
- [Testing](#testing)
- [Deployment](#deployment)
- [WPF Migration](#wpf-migration)
- [Best Practices](#best-practices)

---

## What is Avalonia?

Avalonia is an open-source, cross-platform UI framework for building applications with .NET. It uses its own rendering engine to draw controls, so your app looks and behaves the same on every platform.

**Key capabilities:**

- **Cross-platform rendering** — Avalonia's own rendering engine produces pixel-identical output on every platform. Skia is the default backend, with Impeller coming from Google's Flutter team.
- **XAML and code-behind** — Describe your UI declaratively with XAML or build it entirely in code.
- **Styling system** — A CSS-inspired styling system with selectors, style classes, pseudoclasses, and control themes.
- **Data binding** — Compiled bindings checked at build time, full MVVM support, and integration with CommunityToolkit.Mvvm.
- **Rich control library** — 60+ built-in controls including DataGrid, TreeView, TabControl, Calendar, and more.
- **Accessibility** — Built-in support for screen readers and keyboard navigation across platforms.
- **DevTools** — Press F12 at runtime to inspect the visual tree, properties, styles, and layout.

---

## Platform Support

| Platform | Versions |
|----------|----------|
| **Windows** | 10, 11 |
| **macOS** | Apple Silicon and Intel |
| **Desktop Linux** | X11 and Wayland |
| **Embedded Linux** | Framebuffer on Raspberry Pi and similar devices |
| **iOS** | iOS 13+ |
| **Android** | API 21+ |
| **WebAssembly** | All modern browsers |

---

## Core Concepts

### The Visual Tree

Avalonia uses a visual tree to represent the UI hierarchy. Every visual element inherits from `Visual` and can have children and parents.

```csharp
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
```

### The Logical Tree

The logical tree represents the structural hierarchy of elements, which is important for:
- Resource lookup
- Data context inheritance
- Styling
- Name scope

### The XAML Compiler

Avalonia's XAML compiler (AvaloniaXamlIlLoader) compiles XAML into C# code at build time, providing:
- Compile-time error checking
- Faster startup times
- Compiled bindings (Avalonia 12+)
- Resource optimization

---

## Layout System

### Layout Panels

Avalonia provides several layout panels for arranging children:

#### StackPanel

Stacks children in a single line (horizontally or vertically):

```xml
<StackPanel Orientation="Vertical">
    <TextBlock Text="Item 1" />
    <TextBlock Text="Item 2" />
    <TextBlock Text="Item 3" />
</StackPanel>
```

#### Grid

Flexible grid-based layout with defined rows and columns:

```xml
<Grid>
    <Grid.RowDefinitions>
        <RowDefinition Height="Auto" />
        <RowDefinition Height="*" />
        <RowDefinition Height="2*" />
    </Grid.RowDefinitions>
    <Grid.ColumnDefinitions>
        <ColumnDefinition Width="*" />
        <ColumnDefinition Width="2*" />
    </Grid.ColumnDefinitions>
    <TextBlock Grid.Row="0" Grid.Column="0" Text="Header" />
    <ContentControl Grid.Row="1" Grid.Column="0" Content="{Binding Content}" />
    <ContentControl Grid.Row="2" Grid.Column="1" Content="{Binding Detail}" />
</Grid>
```

#### DockPanel

Docks children to edges (top, bottom, left, right):

```xml
<DockPanel>
    <Button DockPanel.Dock="Top" Content="Menu" />
    <Button DockPanel.Dock="Left" Content="Sidebar" />
    <ContentControl Content="{Binding MainContent}" />
</DockPanel>
```

#### WrapPanel

Wraps children to the next line when they overflow:

```xml
<WrapPanel>
    <Button Content="Button 1" />
    <Button Content="Button 2" />
    <Button Content="Button 3" />
    <Button Content="Button 4" />
</WrapPanel>
```

#### Canvas

Positions children by absolute coordinates:

```xml
<Canvas>
    <Button Canvas.Left="50" Canvas.Top="100" Content="Positioned" />
    <TextBlock Canvas.Left="100" Canvas.Top="150" Text="Label" />
</Canvas>
```

### Layout Properties

| Property | Description |
|----------|-------------|
| `Margin` | Space outside the element |
| `Padding` | Space inside the element |
| `HorizontalAlignment` | Horizontal alignment (Left, Center, Right, Stretch) |
| `VerticalAlignment` | Vertical alignment (Top, Center, Bottom, Stretch) |
| `MinWidth`, `MaxWidth` | Minimum and maximum width |
| `MinHeight`, `MaxHeight` | Minimum and maximum height |
| `Width`, `Height` | Explicit size |
| `Opacity` | Transparency (0 to 1) |

### GridSplitter

Allows users to resize grid rows and columns interactively:

```xml
<Grid>
    <Grid.ColumnDefinitions>
        <ColumnDefinition Width="*" />
        <ColumnDefinition Width="Auto" />
        <ColumnDefinition Width="2*" />
    </Grid.ColumnDefinitions>
    <ContentControl Grid.Column="0" Content="{Binding LeftPanel}" />
    <GridSplitter Grid.Column="1" Width="5" HorizontalAlignment="Center" />
    <ContentControl Grid.Column="2" Content="{Binding RightPanel}" />
</Grid>
```

### ScrollViewer

Provides scrollable content:

```xml
<ScrollViewer VerticalScrollBarVisibility="Auto" HorizontalScrollBarVisibility="Auto">
    <StackPanel>
        <!-- Long content -->
    </StackPanel>
</ScrollViewer>
```

---

## Styling System

### CSS-Inspired Styling

Avalonia's styling system is inspired by CSS, making it familiar to web developers:

#### Style Classes

```xml
<StyleSelector>
    <Style Selector="Button.primary">
        <Setter Property="Background" Value="#0078D4" />
        <Setter Property="Foreground" Value="White" />
    </Style>
</StyleSelector>
```

```xml
<Button Classes="primary">Click Me</Button>
```

#### Pseudoclasses

```xml
<Style Selector="Button:hover">
    <Setter Property="Background" Value="#106EBE" />
</Style>
<Style Selector="Button:pressed">
    <Setter Property="Background" Value="#005A9E" />
</Style>
<Style Selector="Button:disabled">
    <Setter Property="Opacity" Value="0.5" />
</Style>
<Style Selector="TextBox:focus">
    <Setter Property="BorderBrush" Value="#0078D4" />
</Style>
```

#### Control Themes

```xml
<Styles xmlns="https://github.com/avaloniaui">
    <Style Selector="^:nonamespace(Theme)">
        <Setter Property="Background" Value="{DynamicResource SystemControlPageBackgroundAltHighBrush}" />
        <Setter Property="Foreground" Value="{DynamicResource SystemControlForegroundBaseHighBrush}" />
    </Style>
</Styles>
```

### Stylesheets

Avalonia supports a CSS-like stylesheet syntax:

```xml
<Stylesheet>
    <Rule Selector="Button">
        <Setter Property="Background" Value="#0078D4" />
        <Setter Property="Foreground" Value="White" />
        <Setter Property="Padding" Value="8,4" />
    </Rule>
    <Rule Selector="Button:hover">
        <Setter Property="Background" Value="#106EBE" />
    </Rule>
    <Rule Selector="TextBox">
        <Setter Property="Padding" Value="4" />
    </Rule>
</Stylesheet>
```

### Style Selectors

| Selector | Description |
|----------|-------------|
| `#name` | By name |
| `.class` | By class |
| `:type(type)` | By type |
| `:child>of(type)` | Direct child |
| `:descendant>of(type)` | Any descendant |
| `:ancestor(type)` | By ancestor |
| `:not(selector)` | Negation |
| `:first` | First child |
| `:last` | Last child |
| `:nth(n)` | Nth child |
| `:hover` | Hover state |
| `:focus` | Focus state |
| `:pressed` | Pressed state |
| `:disabled` | Disabled state |
| `:empty` | Empty element |
| `:nonempty` | Non-empty element |
| `:checked` | Checked state |
| `:unchecked` | Unchecked state |

### Dynamic Resources

```xml
<StackPanel.Resources>
    <SolidColorBrush x:Key="PrimaryBrush" Color="#0078D4" />
</StackPanel.Resources>
<Button Background="{DynamicResource PrimaryBrush}" />
```

---

## Data Binding

### Basic Binding

```xml
<TextBox Text="{Binding UserName}" />
<TextBlock Text="{Binding Greeting}" />
```

### Compiled Bindings (Avalonia 12)

Avalonia 12 introduces compiled bindings by default:

```xml
<Window x:Class="MyApp.MainWindow"
        xmlns="https://github.com/avaloniaui"
        x:DataType="vm:MainWindowViewModel">
    <TextBox Text="{Binding UserName}" />
    <TextBlock Text="{Binding Greeting}" />
</Window>
```

### Binding Modes

| Mode | Description |
|------|-------------|
| `OneWay` | Source to target (default) |
| `TwoWay` | Bidirectional |
| `OneTime` | One-time evaluation |
| `OneWayToSource` | Target to source |

### RelativeSource Binding

```xml
<TextBox Text="{Binding RelativeSource={RelativeSource TemplatedParent}}" />
<TextBox Text="{Binding RelativeSource={RelativeSource Self}}" />
<TextBox Text="{Binding RelativeSource={RelativeSource FindAncestor, AncestorType={x:Type Window}}}" />
<TextBox Text="{Binding RelativeSource={RelativeSource PreviousData}}" />
```

### MultiBinding

```xml
<TextBlock>
    <TextBlock.Text>
        <MultiBinding StringFormat="{}{0} ({1})">
            <Binding Path="FirstName" />
            <Binding Path="LastName" />
        </MultiBinding>
    </TextBlock.Text>
</TextBlock>
```

### Value Converters

```csharp
public class InverseBooleanConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is true ? false : true;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is true ? false : true;
    }
}
```

### Commands

```csharp
public class RelayCommand : ICommand
{
    private readonly Action _execute;
    private readonly Func<bool> _canExecute;

    public RelayCommand(Action execute, Func<bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute ?? (() => true);
    }

    public bool CanExecute(object? parameter) => _canExecute();
    public void Execute(object? parameter) => _execute();
    public event EventHandler? CanExecuteChanged;
}
```

---

## Data Templates

### Basic Data Template

```xml
<DataTemplate DataType="vm:PersonViewModel">
    <StackPanel>
        <TextBlock Text="{Binding Name}" />
        <TextBlock Text="{Binding Age}" />
    </StackPanel>
</DataTemplate>
```

### Data Template Selector

```csharp
public class PersonTemplateSelector : DataTemplateSelector
{
    public DataTemplate? AdultTemplate { get; set; }
    public DataTemplate? ChildTemplate { get; set; }

    public override DataTemplate? SelectTemplate(object? item, object? container)
    {
        return item is PersonViewModel p && p.Age >= 18 ? AdultTemplate : ChildTemplate;
    }
}
```

### Hierarchical Data Template

```xml
<HierarchicalDataTemplate DataType="vm:FolderViewModel"
                          ItemsSource="{Binding Children}">
    <TextBlock Text="{Binding Name}" />
</HierarchicalDataTemplate>
```

---

## Controls Reference

### Layout Controls

| Control | Description |
|---------|-------------|
| `Border` | Draws a border, background, and corner radius |
| `BoxView` | A simple filled rectangle |
| `Canvas` | Positions children by absolute coordinates |
| `Grid` | Flexible grid-based layout |
| `StackPanel` | Linear stacking of children |
| `DockPanel` | Dock-based layout |
| `WrapPanel` | Wraps children to next line |
| `UniformGrid` | Equal-sized grid cells |
| `ScrollViewer` | Scrollable container |
| `ContentControl` | Single content host |

### Content Controls

| Control | Description |
|---------|-------------|
| `Button` | Clickable button |
| `ToggleButton` | Two-state toggle button |
| `CheckBox` | Checkbox with label |
| `RadioButton` | Radio button |
| `Image` | Image display |
| `TextBlock` | Text display |
| `TextBox` | Single-line text input |
| `PasswordBox` | Password input |
| `Label` | Label for input controls |
| `Expander` | Collapsible content panel |
| `ProgressBar` | Progress indicator |
| `ProgressRing` | Indeterminate progress |
| `Rectangle` | Filled rectangle |
| `Ellipse` | Filled ellipse |
| `Viewbox` | Scales content |

### Headered Controls

| Control | Description |
|---------|-------------|
| `TabControl` | Tab container |
| `TabItem` | Tab item |
| `TreeView` | Tree view |
| `TreeViewItem` | Tree view item |
| `ListBox` | Selectable list |
| `ListView` | List with virtualization |
| `MenuItem` | Menu item |
| `Menu` | Menu bar |
| `ContextMenu` | Context menu |

### Selection Controls

| Control | Description |
|---------|-------------|
| `ComboBox` | Drop-down selector |
| `Slider` | Horizontal slider |
| `RangeSlider` | Dual-range slider |
| `Rating` | Star rating |
| `NumericUpDown` | Numeric input |
| `Calendar` | Calendar selector |
| `DatePicker` | Date picker |
| `TimePicker` | Time picker |

### Window Controls

| Control | Description |
|---------|-------------|
| `Window` | Top-level window |
| `Popup` | Popup window |
| `OverlayPopup` | Overlay popup |
| `Flyout` | Flyout panel |

### Primitive Controls

| Control | Description |
|---------|-------------|
| `ScrollBar` | Scroll bar |
| `Thumb` | Draggable thumb |
| `Track` | Scroll track |
| `GridSplitter` | Grid splitter |
| `Tooltip` | Tooltip |
| `AvaloniaEditor` | Code editor |

### Tree Data Grid

| Control | Description |
|---------|-------------|
| `TreeDataGrid` | Tree data grid |
| `TreeDataGridBoundColumn` | Bound column |
| `TreeDataGridCheckBoxColumn` | Checkbox column |
| `TreeDataGridColumn` | Column base |
| `TreeDataGridRows` | Rows collection |
| `TreeDataGridSource` | Data source |
| `TreeDataGridTemplateColumn` | Template column |
| `TreeDataGridTextColumn` | Text column |

---

## Custom Controls

### Creating a Custom Control

```csharp
public class MyControl : Control
{
    public static readonly StyledProperty<string> TextProperty =
        TextProperty.Register<MyControl>("Text", defaultValue: "");

    public static readonly StyledProperty<bool> IsEnabledProperty =
        ToggleButtonBase.IsEnabledProperty.Register<MyControl>();

    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    static MyControl()
    {
        AffectsRender<MyControl>(TextProperty);
        AffectsParentLayout<MyControl>(TextProperty);
    }
}
```

### Theming a Custom Control

```xml
<!-- themes/generic.xaml -->
<Styles>
    <Style Selector="custom:MyControl">
        <Setter Property="Background" Value="#F0F0F0" />
        <Setter Property="Foreground" Value="#333333" />
        <Setter Property="Padding" Value="8" />
        <Setter Property="Template">
            <ControlTemplate>
                <Border Background="{TemplateBinding Background}"
                        Padding="{TemplateBinding Padding}">
                    <TextBlock Text="{TemplateBinding Text}"
                               Foreground="{TemplateBinding Foreground}" />
                </Border>
            </ControlTemplate>
        </Setter>
    </Style>
</Styles>
```

### Attached Properties

```csharp
public static readonly AttachedProperty<string> TooltipProperty =
    AttachedProperty<string>.RegisterAttached<MyControl, string>("Tooltip");

public static void SetTooltip(DependencyObject obj, string value)
{
    obj.SetValue(TooltipProperty, value);
}

public static string GetTooltip(DependencyObject obj)
{
    return obj.GetValue(TooltipProperty);
}
```

### Style Classes for Custom Controls

```csharp
public class MyControl : Control
{
    public static readonly AttachedProperty<string> StyleClassProperty =
        AttachedProperty<string>.RegisterAttached<MyControl, string>("StyleClass");

    static MyControl()
    {
        StyleClassProperty.Changed.Subscribe(OnStyleClassChanged);
    }

    private static void OnStyleClassChanged(AvaloniaPropertyChangedEventArgs<string> args)
    {
        var control = args.Sender as MyControl;
        control?.UpdateStyleClasses(args.NewValue.Value);
    }
}
```

---

## Animation

### Creating Animations

```xml
<Animation Duration="0:0:0.5" Easing="CubicEaseInOut">
    <KeyFrame>
        <KeyFrame.Value>
            <DoubleAnimation Target="Opacity" From="0" To="1" />
        </KeyFrame.Value>
    </KeyFrame>
</Animation>
```

### Animation in Code

```csharp
var animation = new Animation
{
    Duration = TimeSpan.FromSeconds(0.5),
    Easing = new CubicEaseInEasing(),
    KeyFrames =
    {
        new DoubleKeyFrame { Value = 0, Offset = 0 },
        new DoubleKeyFrame { Value = 1, Offset = 1 },
    }
};

myControl.BeginAnimation(OpacityProperty, animation);
```

### Easing Functions

| Easing | Description |
|--------|-------------|
| `Linear` | Linear interpolation |
| `CubicEaseIn` | Accelerates in |
| `CubicEaseOut` | Decelerates out |
| `CubicEaseInOut` | Accelerates in, decelerates out |
| `BounceEaseIn` | Bouncing effect in |
| `BounceEaseOut` | Bouncing effect out |
| `BounceEaseInOut` | Bouncing effect both |
| `CircularEaseIn` | Circular acceleration |
| `ElasticEaseIn` | Elastic bounce |
| `BackEaseIn` | Overshoot in |
| `BackEaseOut` | Overshoot out |
| `ExponentialEaseIn` | Exponential acceleration |

---

## Input and Interaction

### Pointer Events

| Event | Description |
|-------|-------------|
| `PointerPressed` | Pointer pressed |
| `PointerReleased` | Pointer released |
| `PointerMoved` | Pointer moved |
| `PointerEntered` | Pointer entered element |
| `PointerExited` | Pointer exited element |
| `PointerWheelChanged` | Mouse wheel changed |

### Key Events

| Event | Description |
|-------|-------------|
| `KeyDown` | Key pressed |
| `KeyUp` | Key released |
| `TextInput` | Text input |

### Gesture Recognition

```csharp
public class MyControl : Control
{
    private readonly GestureRecognizer _gestureRecognizer;

    public MyControl()
    {
        _gestureRecognizer = new GestureRecognizer();
        _gestureRecognizer.GestureRecognized += OnGestureRecognized;
    }

    private void OnGestureRecognized(object? sender, GestureEventArgs e)
    {
        // Handle gesture
    }
}
```

---

## Graphics and Animation

### Brushes

| Brush | Description |
|-------|-------------|
| `SolidColorBrush` | Solid color |
| `LinearGradientBrush` | Linear gradient |
| `RadialGradientBrush` | Radial gradient |
| `ImageBrush` | Image fill |
| `DrawingBrush` | Drawing fill |
| `TransformedBrush` | Transformed brush |

### Transforms

| Transform | Description |
|-----------|-------------|
| `TranslateTransform` | Translation |
| `ScaleTransform` | Scaling |
| `RotateTransform` | Rotation |
| `SkewTransform` | Skewing |
| `MatrixTransform` | Matrix transformation |
| `TransformGroup` | Combined transforms |

### Effects

| Effect | Description |
|--------|-------------|
| `BlurEffect` | Blur |
| `DropShadowEffect` | Drop shadow |
| `GlowEffect` | Glow |
| `EmbossEffect` | Emboss |
| `BevelEffect` | Bevel |

---

## Window and Popup Controls

### Window

```xml
<Window x:Class="MyApp.MainWindow"
        xmlns="https://github.com/avaloniaui"
        Title="My Application"
        Width="800"
        Height="600"
        MinWidth="400"
        MinHeight="300"
        WindowStartupLocation="CenterScreen">
    <ContentControl Content="{Binding MainView}" />
</Window>
```

### Popup

```xml
<Popup IsOpen="{Binding IsPopupOpen}"
       PlacementTarget="{Binding ElementName=MyButton}"
       Placement="Bottom">
    <Border Background="White" BorderBrush="#333" BorderThickness="1">
        <TextBlock Text="Popup content" />
    </Border>
</Popup>
```

### Flyout

```xml
<Flyout Placement="Bottom" IsOpen="{Binding IsFlyoutOpen}">
    <StackPanel>
        <TextBlock Text="Flyout content" />
        <Button Content="Close" Command="{Binding CloseFlyoutCommand}" />
    </StackPanel>
</Flyout>
```

---

## Accessibility

### Built-in Support

Avalonia provides built-in accessibility support:

- **Screen readers** — Automatic text announcements
- **Keyboard navigation** — Tab order and focus management
- **High contrast** — Platform-aware high contrast themes
- **Focus visual** — Visible focus indicators

### Setting Accessibility Properties

```xml
<Button Content="Click Me"
        AutomationProperties.Name="Click button"
        AutomationProperties.HelpText="Click to submit"
        AutomationProperties.AutomationId="myButton" />
```

---

## Platform-Specific Integration

### Windows

- Native window decorations
- Win32 interop
- Windows-specific input handling

### macOS

- Native menu bar
- Apple Silicon support
- macOS-specific window controls

### Linux

- X11 and Wayland support
- Platform-native decorations
- GTK integration options

### iOS

- Native navigation
- Touch handling
- iOS-specific controls

### Android

- Native activity lifecycle
- Touch handling
- Android-specific controls

### WebAssembly

- Browser integration
- Canvas rendering
- Web-specific input handling

---

## Testing

### Unit Testing

```csharp
public class MyControlTests
{
    [Fact]
    public void MyControl_Should_Set_Property()
    {
        var control = new MyControl();
        control.Text = "Hello";
        Assert.Equal("Hello", control.Text);
    }
}
```

### Visual Testing

```csharp
public class MyControlVisualTests
{
    [Fact]
    public void MyControl_Should_Render_Correctly()
    {
        var control = new MyControl();
        var visual = control.GetVisual();
        Assert.NotNull(visual);
    }
}
```

### UI Testing with TestHost

```csharp
public class MyControlUITests
{
    [Fact]
    public async Task MyControl_Should_Click()
    {
        var control = new MyControl();
        var window = new Window { Content = control };

        await window.Show();
        await control.RaisePointerPressed();
        Assert.True(control.IsClicked);
    }
}
```

---

## Deployment

### Windows

- MSIX packaging
- ClickOnce deployment
- Installer creation

### macOS

- DMG packaging
- App Store submission
- Notarization

### Linux

- AppImage
- Flatpak
- Snap

### iOS

- IPA packaging
- App Store submission
- Enterprise distribution

### Android

- APK/AAB packaging
- Google Play submission
- Enterprise distribution

### WebAssembly

- Static hosting
- CDN deployment
- Azure Static Web Apps

---

## WPF Migration

### WPF to Avalonia Cheat Sheet

| WPF | Avalonia | Notes |
|-----|----------|-------|
| `Window` | `Window` | Similar API |
| `Grid` | `Grid` | Same row/column system |
| `StackPanel` | `StackPanel` | Identical |
| `DockPanel` | `DockPanel` | Identical |
| `WrapPanel` | `WrapPanel` | Identical |
| `Canvas` | `Canvas` | Identical |
| `Button` | `Button` | Identical |
| `TextBox` | `TextBox` | Identical |
| `ListBox` | `ListBox` | Identical |
| `ListView` | `ListView` | Similar |
| `TreeView` | `TreeView` | Identical |
| `TabControl` | `TabControl` | Identical |
| `ComboBox` | `ComboBox` | Identical |
| `CheckBox` | `CheckBox` | Identical |
| `RadioButton` | `RadioButton` | Identical |
| `Image` | `Image` | Identical |
| `TextBlock` | `TextBlock` | Identical |
| `Border` | `Border` | Identical |
| `ContentControl` | `ContentControl` | Identical |
| `ItemsControl` | `ItemsControl` | Identical |
| `DataGrid` | `DataGrid` | Avalonia has its own |
| `Slider` | `Slider` | Identical |
| `ProgressBar` | `ProgressBar` | Identical |
| `Expander` | `Expander` | Identical |
| `Popup` | `Popup` | Identical |
| `ContextMenu` | `ContextMenu` | Identical |
| `ToolTip` | `Tooltip` | Identical |
| `Style` | `Style` | Similar selectors |
| `Trigger` | `Trigger` | Similar syntax |
| `DataTemplate` | `DataTemplate` | Identical |
| `ControlTemplate` | `ControlTemplate` | Identical |
| `Binding` | `Binding` | Similar syntax |
| `Converter` | `IValueConverter` | Identical |
| ` ICommand` | `ICommand` | Identical |
| `INotifyPropertyChanged` | `INotifyPropertyChanged` | Identical |
| `Dispatcher` | `Dispatcher` | Similar API |
| `Application` | `Application` | Similar API |
| `VisualTreeHelper` | `VisualTreeHelper` | Similar API |
| `DependencyObject` | `AvaloniaObject` | Similar API |
| `DependencyProperty` | `StyledProperty<T>` | Similar API |
| `AttachedProperty` | `AttachedProperty<T>` | Similar API |
| `WPF Brushes` | `Avalonia.Media.Brushes` | Similar API |
| `WPF Transforms` | `Avalonia.Media.Transforms` | Similar API |
| `WPF Effects` | `Avalonia.Media.Effects` | Similar API |
| `WPF Animations` | `Avalonia.Animation` | Similar API |
| `WPF Input` | `Avalonia.Input` | Similar API |
| `WPF Layout` | `Avalonia.Layout` | Similar API |
| `WPF Styling` | `Avalonia.Styling` | Similar API |
| `WPF DataBinding` | `Avalonia.Data` | Similar API |
| `WPF XAML` | `Avalonia.Markup.Xaml` | Similar API |

---

## Best Practices

### 1. Use Compiled Bindings

Avalonia 12+ uses compiled bindings by default. Specify `x:DataType` for optimal performance:

```xml
<Window x:DataType="vm:MainWindowViewModel">
    <TextBox Text="{Binding UserName}" />
</Window>
```

### 2. Use Style Classes for Reusable Styles

```xml
<Styles>
    <Style Selector="Button.primary">
        <Setter Property="Background" Value="#0078D4" />
    </Style>
    <Style Selector="Button.secondary">
        <Setter Property="Background" Value="#606060" />
    </Style>
</Styles>
```

### 3. Use Pseudoclasses for Interactive States

```xml
<Style Selector="Button:hover">
    <Setter Property="Background" Value="#106EBE" />
</Style>
<Style Selector="Button:pressed">
    <Setter Property="Background" Value="#005A9E" />
</Style>
```

### 4. Use Data Templates for Item Display

```xml
<ItemsControl ItemsSource="{Binding Items}">
    <ItemsControl.ItemTemplate>
        <DataTemplate>
            <StackPanel>
                <TextBlock Text="{Binding Name}" />
                <TextBlock Text="{Binding Description}" />
            </StackPanel>
        </DataTemplate>
    </ItemsControl.ItemTemplate>
</ItemsControl>
```

### 5. Use Grid for Complex Layouts

```xml
<Grid>
    <Grid.RowDefinitions>
        <RowDefinition Height="Auto" />
        <RowDefinition Height="*" />
        <RowDefinition Height="Auto" />
    </Grid.RowDefinitions>
    <Grid.ColumnDefinitions>
        <ColumnDefinition Width="*" />
        <ColumnDefinition Width="Auto" />
        <ColumnDefinition Width="*" />
    </Grid.ColumnDefinitions>
</Grid>
```

### 6. Use StackPanel for Simple Linear Layouts

```xml
<StackPanel Orientation="Vertical">
    <TextBlock Text="Item 1" />
    <TextBlock Text="Item 2" />
</StackPanel>
```

### 7. Use DockPanel for Edge-Docked Layouts

```xml
<DockPanel>