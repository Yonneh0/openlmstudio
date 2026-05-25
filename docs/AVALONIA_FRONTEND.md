# Avalonia Frontend Design Guide for OpenLMStudio

> **Target version:** Avalonia 12.0.3 (project actual)
> **Latest docs:** https://v12.docs.avaloniaui.net/
> **API reference:** https://api-docs.avaloniaui.net/
> **Samples:** https://github.com/AvaloniaUI/Avalonia.Samples
> **XAML Playground:** https://play.avaloniaui.net/

---

## 1. Project Structure

### Namespaces and Assembly Organization

- **Root namespace:** `Avalonia`
- **Target framework:** .NET 8
- **Windows rendering:** `Avalonia.Win32`
- **Default rendering backend:** Skia (`Avalonia.Skia`)
- **Alternative backends:** Direct2D1 (`Avalonia.Direct2D1`), Vulkan (`Avalonia.Vulkan`)
- **XAML root element:** `<Window xmlns="https://github.com/avaloniaui"` (not WPF)

### Platform-Specific Assemblies

| Assembly | Purpose |
|----------|---------|
| `Avalonia.Desktop` | Desktop-specific controls and services |
| `Avalonia.Win32` | Windows-specific rendering |
| `Avalonia.Direct2D1` | Direct2D1 rendering backend |
| `Avalonia.Skia` | Skia-based rendering backend (default) |
| `Avalonia.X11` | X11 rendering (Linux) |
| `Avalonia.LinuxFramebuffer` | Linux framebuffer (Raspberry Pi) |
| `Avalonia.iOS` | iOS rendering backend |
| `Avalonia.Android` | Android rendering backend |
| `Avalonia.Browser` | WebAssembly rendering backend |
| `Avalonia.Native` | Native rendering backend |
| `Avalonia.Fonts.Inter` | Inter font family |
| `Avalonia.Themes.Fluent` | Fluent Design theme |
| `Avalonia.Themes.Simple` | Simple theme |
| `Avalonia.Controls.ColorPicker` | Color picker control |
| `Avalonia.Controls.DataGrid` | Data grid control |
| `Avalonia.Diagnostics` | Debug tools (replaces Avalonia.DesignerSupport) |

### Rendering Architecture

Avalonia uses its own rendering engine — **no native control wrappers**. This means:
- Pixel-identical output on every platform
- No platform-specific quirks
- Skia is the default backend
- Google's Flutter team is collaborating to bring **Impeller** rendering to .NET

---

## 2. XAML Fundamentals

### Window Setup

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        x:Class="OpenLMStudio.Desktop.MainWindow"
        x:CompileBindings="True"
        Width="1400" Height="750"
        MinWidth="1000" MinHeight="600"
        Title="OpenLMStudio">
```

**Critical rules:**
- **Window can only have ONE child** — wrap everything in a single panel
- `Width/Height` and `MinWidth/MinHeight` are set on the Window element directly
- `x:Class` maps to the code-behind class
- `x:CompileBindings="True"` enables compiled bindings (faster, type-safe)
- **Avalonia 12+:** Compiled bindings are enabled by default. Use `x:DataType` on `DataTemplate` for type-safe bindings.

### Code-Behind Class

```csharp
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        SetupEventHandlers();
    }

    private void SetupEventHandlers()
    {
        if (SendButton != null)
            SendButton.Click += OnSendClicked;
    }
}
```

### XAML Namespaces

| Namespace | Prefix | Purpose |
|-----------|--------|---------|
| `https://github.com/avaloniaui` | (default) | Core controls |
| `http://schemas.microsoft.com/winfx/2006/xaml` | `x` | XAML extensions |
| `using:CommunityToolkit.Mvvm` | (custom) | MVVM attributes |
| `using:Avalonia.Controls` | (custom) | Controls |
| `using:Avalonia.Controls.Primitives` | (custom) | Primitives |
| `using:Avalonia.Controls.Notifications` | (custom) | Notifications |
| `using:Avalonia.Controls.DataGrid` | (custom) | DataGrid |

---

## 3. Layout Primitives

### DockPanel

```xml
<DockPanel>
    <Button DockPanel.Dock="Top" Content="Menu"/>
    <Button DockPanel.Dock="Left" Content="Sidebar"/>
    <Button DockPanel.Dock="Right" Content="Panel"/>
    <Border DockPanel.Dock="Fill" Background="LightGray">
        <!-- Fills remaining space (default) -->
    </Border>
</DockPanel>
```

**Critical:** If you dock a child to Bottom, the remaining space is NOT automatically Fill — set `DockPanel.Dock="Fill"` on the main content container.

### Grid

```xml
<Grid>
    <Grid.ColumnDefinitions>
        <ColumnDefinition Width="280"/>     <!-- fixed -->
        <ColumnDefinition Width="4*"/>      <!-- proportional -->
        <ColumnDefinition Width="Auto"/>    <!-- content-sized -->
    </Grid.ColumnDefinitions>
    <Grid.RowDefinitions>
        <RowDefinition Height="*"/>
        <RowDefinition Height="24"/>
    </Grid.RowDefinitions>
    <TextBlock Grid.Column="0" Grid.Row="0" Text="Content"/>
</Grid>
```

**Key attached properties:** `Grid.Row`, `Grid.Column`, `Grid.RowSpan`, `Grid.ColumnSpan`

### Canvas

- Children positioned absolutely via `Canvas.Left`, `Canvas.Top`
- Canvas children **do not respect** `Grid.ColumnSpan`
- Useful for overlays (e.g., popups anchored above a status bar)

### StackPanel

- Children stack vertically (default) or horizontally (`Orientation="Horizontal"`)
- Good for lists, headers, and toolbars

### Panel

- Base class for custom layouts. Children are layered on top of each other.
- Use `Panel` when you need z-ordering.

### UniformGrid

- Grid where all cells are the same size
- Use `Rows` and `Columns` properties
- Useful for tile layouts, icon grids

### Viewbox

- Scales content to fit available space
- Use `Stretch` property (Uniform, Fill, None, UniformToFill)
- Use `StretchDirection` to control up/down/both scaling

### ScrollViewer

- Wrap long content areas (message lists, settings) in ScrollViewer with auto scrollbars
- Use `VerticalScrollBarVisibility="Auto"` and `HorizontalScrollBarVisibility="Auto"`

### Border

- Use for visual containers with padding, background, and rounded corners

---

## 4. Window.Styles — The Critical Gotcha

**In Avalonia 11.x+, `Window.Styles` does NOT cascade to child controls.** Styles defined in `Window.Styles` only apply to the Window element itself.

### How to Style Child Controls

**1. Use `Control.Styles` on each control:**
```xml
<Button>
    <Button.Styles>
        <Style Selector="Button:pointerover">
            <Setter Property="Background" Value="#454549"/>
        </Style>
    </Button.Styles>
</Button>
```

**2. Use `Control.Resources` on each control:**
```xml
<Button>
    <Button.Resources>
        <Style Selector="Button:pointerover">
            <Setter Property="Background" Value="#454549"/>
        </Style>
    </Button.Resources>
</Button>
```

**3. Use `Container.Styles` on a parent container:**
```xml
<StackPanel>
    <StackPanel.Styles>
        <Style Selector="Button:hover">
            <Setter Property="Background" Value="#454549"/>
        </Style>
    </StackPanel.Styles>
    <Button Content="Click Me"/>
</StackPanel>
```

**4. Use `x:Key` with `DynamicResource` in `Window.Styles`:**
```xml
<Window.Styles>
    <Style x:Key="buttonHover" Selector="Button:pointerover">
        <Setter Property="Background" Value="#454549"/>
    </Style>
</Window.Styles>

<Button Style="{DynamicResource buttonHover}"/>
```

**5. Use `Styles` on specific controls that have it:**
```xml
<TabControl>
    <TabControl.Styles>
        <Style Selector="TabItem:hover">
            <Setter Property="Background" Value="#454549"/>
        </Style>
    </TabControl.Styles>
</TabControl>
```

---

## 5. Styling System

### Style Selectors (in Window.Styles)

```xml
<Window.Styles>
    <Style Selector="Button.accent">
        <Setter Property="Background" Value="{StaticResource AccentBlue}"/>
        <Setter Property="Foreground" Value="White"/>
    </Style>
</Window.Styles>
```

- Use `Selector` attribute for the selector string
- Use `TargetType` for type-based selectors
- Use `x:Key` for keyed styles (with `DynamicResource`)

### Pseudo-Classes (Selectors)

| Selector | Meaning |
|----------|---------|
| `:pointerover` | mouse hover |
| `:hover` | mouse hover (alias for `:pointerover`) |
| `:pressed` | pressed state |
| `:checked` / `:unchecked` | checked state (ToggleButton, CheckBox, RadioButton) |
| `:indeterminate` | three-state mode when `IsChecked` is `null` |
| `:selected` | selected state (ListBoxItem, TabItem, etc.) |
| `:focus` / `:focused` | focused state |
| `:disabled` | disabled state |
| `:empty` | no content |
| `:first-child`, `:last-child`, `:nth-child(n)` | positional |
| `/template>` | descend into ControlTemplate (e.g., `Button:pointerover /template> Border`) |
| `>` | direct child combinator (e.g., `Button > TextBlock`) |
| ` ` | descendant combinator (e.g., `StackPanel Button`) |

### StaticResource vs DynamicResource

| Extension | Behavior |
|-----------|----------|
| `StaticResource` | Resolved once at compile time (preferred for performance) |
| `DynamicResource` | Resolved at runtime (for themes that change) |

### SolidColorBrush

```xml
<SolidColorBrush x:Key="BgPrimary" Color="#1E1E22"/>
```
Use `StaticResource` binding: `Background="{StaticResource BgPrimary}"`

### Style Properties

- `Background`, `Foreground`
- `BorderBrush`, `BorderThickness`
- `Padding`, `Margin`
- `FontSize`, `FontWeight`, `FontFamily`
- `HorizontalAlignment`, `VerticalAlignment`
- `Width`, `Height`, `MinWidth`, `MinHeight`
- `CornerRadius`
- `Opacity`
- `FlowDirection` (LeftToRight, RightToLeft)

### Classes Attribute

Use `Classes` attribute for style class names:
```xml
<Button Classes="accent" Content="Click Me"/>
```

---

## 6. Common Controls

### TextBlock

- Read-only text display. Use `Text` property.
- Font properties: `FontFamily`, `FontSize`, `FontWeight`, `Foreground`.
- `TextWrapping="Wrap"` or `NoWrap`.
- Use `FormattedText` for rich text runs with different formatting.

### TextBox

- Input control. Use `Text` property.
- `AcceptsReturn="True"` for multiline.
- `PlaceholderText` property for placeholder text (replaces `Watermark` in Avalonia 12+).
- Events: `KeyDown`, `TextChanged`, `LostFocus`.
- `SelectionChanged`, `SelectAll()`.

### PasswordBox

- Password input control.
- `Password` property.
- `Watermark` for placeholder.

### Button

- Use `Click` event in code-behind.
- **Critical:** In Avalonia 11.x+, Button styles in `Window.Styles` do NOT cascade. Apply styles directly to each Button using `Button.Styles`.
- `VerticalContentAlignment="Center"` — centers content vertically within the button.
- `IsDefault` — button activates on Enter key press.
- `IsCancel` — button activates on Escape key press.
- **Click vs PointerPressed:** Always use `Click` to determine whether a user has pressed a button.
- **Keyboard accessibility:** Button is focusable by default and participates in tab navigation.
- **Icon buttons:** For icon-only buttons, set `AutomationProperties.Name` so screen readers can identify the button.
- `Command` property to bind to `ICommand` in your view model.
- `CommandParameter` to pass a parameter to the command.
- `ClickMode` — controls when Click fires: `Release` (default), `Press`, `Hover`.
- `Flyout` — attach a `Flyout` for contextual overlays.

### ToggleButton

- Toggle switch button (two states: checked/unchecked).
- `IsChecked` property (nullable `bool?` for three-state mode).
- `IsThreeState` — set to `true` for three-state support (checked/unchecked/indeterminate).
- **Critical:** In Avalonia 11.x+, use `ToggleButton.Styles` to apply hover styles.
- **Three-state binding:** When `IsThreeState="true"`, `IsChecked` cycles through `true` → `false` → `null`. Style each state independently using `:checked`, `:unchecked`, and `:indeterminate` pseudo classes.

### RadioButton

- For exclusive selection groups (needs `GroupName` for grouping).
- `IsChecked` for two-state binding.
- Use `RadioButtonGroup` for automatic mutual exclusion.

### ComboBox

- Items via child `<TextBlock>` elements or `ItemsSource`.
- `SelectedIndex`, `SelectedItem`, `SelectedValue` for data binding.
- `IsEditable` for editable combobox.
- `IsDropDownOpen` to control open state.

### Slider

- `Minimum`, `Maximum`, `Value`, `TickFrequency`, `IsSnapToTickEnabled`.
- `ValueChanged` event.
- `Orientation` for horizontal/vertical.
- `IsMoveToPointEnabled` for click-to-seek.

### ProgressBar

- `Minimum`, `Maximum`, `Value` for determinate progress.
- `IsIndeterminate="True"` for indeterminate (spinning) progress.
- Style with `ProgressBar.Styles` for custom appearance.

### RepeatButton

- Button that fires `Click` events repeatedly while held down.
- `Delay` for initial delay before repeats start.
- `Interval` for repeat interval.

### DropDownButton

- Button with a dropdown menu.
- `Menu` property for dropdown content.
- `IsOpen` to control dropdown state.

### Icon / PathIcon / FontIcon

- `Icon` property for displaying icons.
- `PathIcon` with vector path data.
- `FontIcon` for font-based icons.
- `Width`, `Height` for sizing.

### Image

- `Source` property for image source (Bitmap, RenderBitmap, etc.).
- `Stretch` property (Uniform, Fill, None, UniformToFill).

### Ellipse / Rectangle / Line

- Drawing primitives.
- `Fill` for brush, `Stroke` for border.
- `Width`, `Height`, `RadiusX`, `RadiusY` for Ellipse/Rectangle.

---

## 7. Navigation Controls

### TabControl / TabItem

- `TabControl` for tabbed navigation.
- `TabItem` with `Header` for tab titles.
- `ItemsSource` for data binding.
- `SelectedItem` for current tab.
- `SelectionChanged` event.
- `TabStripPlacement` for top/left/bottom/right tabs.
- Use `TabItem.Styles` for custom tab styling.

### ListBox / ListView / GridView

- `ListBox` for single/multi-select lists.
- `ListView` for list display with columns.
- `GridView` for column-based display.
- `SelectionMode` for Single/Multiple/Extended.
- `SelectionMode="Toggle"` for click-to-toggle selection.

### DataGrid

- Rich data grid from `Avalonia.Controls.DataGrid`.
- `ItemsSource` for data binding.
- `AutoGenerateColumns` for automatic column creation.
- `Columns` for manual column definition.
- `DataGridTextColumn`, `DataGridCheckBoxColumn`, `DataGridComboBoxColumn`.
- `Sorting`, `SelectionMode`, `CanUserSortColumns`.

### TreeView

- Hierarchical data display.
- `ItemsSource` for data binding.
- `TreeViewItem` for hierarchical children.
- `SelectedItem` for current selection.
- `SelectedItemChanged` event.
- `HierarchicalDataTemplate` for binding.

### Menu / MenuItem

- `Menu` for menu bars.
- `MenuItem` for menu items with `Header`, `Icon`, `Items`.
- `Click` event.
- `IsChecked` for toggle menu items.
- `Separator` for dividers.

### ContextMenu

- `ContextMenu` for right-click menus.
- `ContextMenu.PlacementTarget` to anchor to a control.
- Use `ContextMenu` attached property on any control.

### Flyout

- `Flyout` for contextual overlays (tooltip-like but richer).
- `FlyoutBase.AttachedFlyout` attached property.
- `Flyout.Show()` / `Flyout.Hide()` methods.
- `Placement` for positioning (Top, Bottom, Left, Right, BottomEdge, TopEdge, LeftEdge, RightEdge).
- `LightDismissEnabled` for dismiss-on-outside-click.

### Popup

- `Avalonia.Controls.Primitives.Popup` for true popup semantics.
- Renders outside parent bounds, above window clipping.
- `IsOpen` to show/hide.
- `PlacementTarget` and `Placement` for positioning.
- `LightDismissEnabled` for dismiss-on-outside-click.
- `Offset` for offset from placement target.
- `HorizontalOffset`, `VerticalOffset` for fine-grained positioning.
- **NOT** `Avalonia.Controls.Popup` (doesn't exist).

### Tooltip

- `ToolTip` attached property.
- `ToolTip.Tip` for tooltip content.

---

## 8. Overlay Controls

### ContentDialog

- Modal dialog content overlay.
- `ContentDialog.ShowAsync()` / `ContentDialog.Hide()`.
- `Title`, `Content`, `PrimaryButtonText`, `SecondaryButtonText`.
- `PrimaryButtonCommand`, `SecondaryButtonCommand`.
- `XamlRoot` for XAML-based content.

### MessageBox

- Simple message dialog.
- `MessageBox.Show(message, title, buttons, icon)`.
- `MessageBox.ShowAsync()` for async.
- `MessageBoxButton` enum (OK, OKCancel, YesNo, YesNoCancel, RetryCancel).
- `MessageBoxImage` enum (Error, Warning, Information, None).

### InfoBar / InfoBarMessage

- Information bar for status messages (success, warning, error).
- `InfoBar.IsOpen` to show/hide.
- `InfoBar.MessageSeverity` (Information, Success, Warning, Error).
- `InfoBar.Title`, `InfoBar.Message`.
- `InfoBar.CloseButtonContent` for close button.

### NotificationCard

- Non-intrusive notifications (snackbars).
- `NotificationCard.Show()`.
- `Duration` for auto-dismiss.

---

## 9. Forms and Input Controls

### DataValidationErrors

- `DataValidationErrors.Add()` to add validation errors.
- `DataValidationErrors.Clear()` to remove all.
- `DataValidationErrors.GetErrors()` to get errors.
- `DataValidationErrors.HasErrors` property.

### NumericUpDown

- Numeric input with up/down buttons.
- `Minimum`, `Maximum`, `Value`.
- `Increment` for step size.
- `FormatString` for display format.

### MaskedTextBox

- Text input with mask pattern.
- `Mask`, `PromptChar`, `MaskCompleted`.

### DatePicker

- Date selection control.
- `Date` property.
- `SelectedDate`, `SelectedDateChanged`.

### TimePicker

- Time selection control.
- `Time` property.
- `SelectedTime`, `SelectedTimeChanged`.

---

## 10. Data Binding

### Binding Syntax

```xml
<!-- Standard binding -->
<TextBlock Text="{Binding Path=PropertyName}"/>

<!-- RelativeSource binding -->
<TextBlock Text="{Binding RelativeSource={RelativeSource AncestorType=Window}, Path=DataContext.Title}"/>

<!-- ElementName binding -->
<TextBlock Text="{Binding ElementName=MySlider, Path=Value}"/>

<!-- With converter -->
<TextBlock Text="{Binding Path=Value, Converter={StaticResource IntToStringConverter}}"/>

<!-- TwoWay binding -->
<TextBox Text="{Binding Path=InputText, Mode=TwoWay}"/>
```

### Converters

| Converter | Purpose |
|-----------|---------|
| `IValueConverter` | value conversion |
| `IMultiValueConverter` | multi-value conversion |
| `BooleanToVisibilityConverter` | bool to visibility |
| `StringFormatConverter` | string formatting |

### Compiled Bindings

- `{x:Bind}` for compiled bindings (faster, type-safe)
- Set `x:CompileBindings="True"` on the Window
- Resolved at compile time with type checking

### Data Validation

```csharp
// In ViewModel
[NotifyDataErrorInfo]
public partial class MyViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyDataErrorInfo(Converter = typeof(NotEmptyValidator))]
    private string _name;
}
```

---

## 11. Theming

### ThemeVariant

- `ThemeVariant` for dark/light theme support.
- `ThemeVariant.Default`, `ThemeVariant.Dark`, `ThemeVariant.Light`.
- `RequestedThemeVariant` for setting.

### DynamicResource

- `DynamicResource` for theme-aware resources.
- Use `DynamicResource` when theme can change at runtime.
- Use `StaticResource` for compile-time resources (preferred for performance).

---

## 12. Animation

### Storyboard

```xml
<StackPanel.Resources>
    <Storyboard x:Key="FadeIn">
        <DoubleAnimation Storyboard.TargetProperty="Opacity"
                         From="0" To="1" Duration="0:0:0.3"/>
    </Storyboard>
</StackPanel.Resources>
```

### Easing Functions

- `LinearEasing` — no easing
- `CubicEase` — cubic curve
- `QuadraticEase`, `QuarticEase`, `QuinticEase`
- `SineEase`, `ExponentialEase`
- `ElasticEase` — elastic bounce
- `BackEase` — overshoot and settle
- `CircleEase` — circular curve
- `PowerEase` — power curve
- `SpringEase` — spring bounce

### VisualStateManager

- `VisualStateManager` for state-based animations.
- `VisualState` for individual states.
- `VisualTransition` for state transitions.
- `GoToState()` method.

---

## 13. Event Handling

### Code-Behind

```csharp
private void OnSendClicked(object? sender, RoutedEventArgs e) { ... }

private void OnMessageInputKeyDown(object? sender, KeyEventArgs e)
{
    if (e.Key == Key.Enter && (e.KeyModifiers & KeyModifiers.Control) != 0)
        SendMessage();
}
```

### Pointer Events

```csharp
private void OnGitStatusClicked(object? sender, PointerPressedEventArgs e) { ... }
private void OnChatTabPointerPressed(object? sender, PointerPressedEventArgs e) { ... }
```

### Adding handlers programmatically

```csharp
if (SendButton != null)
    SendButton.Click += OnSendClicked;
```

This is preferred in `SetupEventHandlers()` rather than inline in XAML.

### Input Events

- `KeyDown`, `KeyUp`, `TextInput` events.
- `PointerPressed`, `PointerReleased`, `PointerMoved`.
- `PointerWheelChanged` for scroll wheel.
- `GotFocus`, `LostFocus` for focus events.
- `KeyModifiers` for modifier keys (Ctrl, Shift, Alt).

---

## 14. Control Templates

### ControlTemplate

```xml
<ControlTemplate x:Key="CustomButtonTemplate">
    <Border Background="{TemplateBinding Background}"
            CornerRadius="8"
            BorderBrush="{TemplateBinding BorderBrush}"
            BorderThickness="1">
        <ContentPresenter Content="{TemplateBinding Content}"
                          HorizontalAlignment="Center"
                          VerticalAlignment="Center"/>
    </Border>
</ControlTemplate>
```

- `TemplatedParent` for accessing the templated control.
- Named parts in templates (e.g., `PART_Popup`, `PART_ContentPresenter`).
- `GetTemplateChild()` for accessing parts.

### Control Presenters

| Presenter | Purpose |
|-----------|---------|
| `ContentPresenter` | presenting content |
| `ItemsPresenter` | presenting items |
| `TextPresenter` | presenting text |
| `ScrollContentPresenter` | scrollable content |

---

## 15. Media

### Brush Types

| Brush | Purpose |
|-------|---------|
| `SolidColorBrush` | solid fill |
| `LinearGradientBrush` | linear gradient |
| `RadialGradientBrush` | radial gradient |
| `DrawingBrush` | draw with vector graphics |
| `VisualBrush` | capture a visual as a brush |

### Color

- `Color.FromArgb(a, r, g, b)` for ARGB.
- `Colors` static class for named colors.
- `#AARRGGBB` or `#RRGGBB` hex notation.

### Pen / Stroke

- `Stroke` for border.
- `StrokeThickness` for width.
- `StrokeDashArray` for dashed lines.

---

## 16. Automation and Accessibility

### Automation Peers

- `AutomationPeer` for exposing controls to automation.
- `GetAutomationPeer()` method.

### Automation Providers

- `IProvider` — automation provider interface.
- `IValueProvider` — value provider.
- `IAutomationElementProvider` — element provider.

---

## 17. Threading

### Dispatcher

- `Dispatcher.UIThread` — UI thread dispatcher.
- `DispatcherPriority` for scheduling priority.
- Use `Dispatcher.UIThread.Post()` for async UI updates.

---

## 18. Platform-Specific Controls

### Avalonia.Controls.Chrome

- `WindowChrome` for custom window chrome.
- `TitleBar` for title bar.

### Avalonia.Controls.ApplicationLifetimes

- `ISingleViewApplicationLifetime` — single view lifetime.
- `IFunctionalApplicationLifetime` — functional lifetime.

### Avalonia.Controls.Notifications

- `NotificationType` — notification types (Information, Success, Warning, Error).
- `INotificationService` — notification service.
- `NotificationService` — notification service implementation.

---

## 19. Data Collections

### Types

| Collection | Purpose |
|------------|---------|
| `ObservableCollection<T>` | observable collection |
| `ObservableVector<T>` | observable vector |
| `ReadOnlyObservableCollection<T>` | read-only observable collection |

---

## 20. Layout Tips

1. **Status bars** — Put in Grid Row N with a fixed height, spanning all columns with `Grid.ColumnSpan`.
2. **Popups above status bars** — Use a Canvas in the same Grid.Row, positioned with `Margin="0,0,0,24"` (the status bar height) to stay above it.
3. **DockPanel + Grid** — Use DockPanel for top-level layout (menu bar on top), Grid for the content area.
4. **ScrollViewer** — Always wrap long content areas (message lists, settings) in ScrollViewer with auto scrollbars.
5. **Border** — Use Border for visual containers with padding, background, and rounded corners.
6. **Popup** — Use `Avalonia.Controls.Primitives.Popup` for true popup semantics (outside parent bounds, renders above window clipping). Use `Border` with `IsVisible` for simple overlays within bounds.
7. **Button styles** — Use `Button.Styles` or `Container.Styles` for child control styles — `Window.Styles` does NOT cascade to children.

---

## 21. Common Patterns in OpenLMStudio

### Three-panel layout
```
DockPanel
├── Menu bar (DockPanel.Dock="Top")
└── Grid (3 columns)
    ├── Left sidebar (fixed width)
    ├── Center pane (proportional width)
    └── Right sidebar (fixed width)
```

### Status bar
- Grid.Row=1, Border spanning all columns.

### Tab navigation
- ToggleButton or RadioButton per tab; show/hide StackPanel content via code-behind.

### Settings panels
- StackPanel inside ScrollViewer with DockPanel for label-value rows.

### Message display
- ScrollViewer → StackPanel with Border for user/assistant messages.

### Hover on buttons
- Use `Button.Styles` with `:pointerover` selector for hover effects on buttons.

---

## 22. Sample Layout Pattern

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        x:Class="OpenLMStudio.Desktop.MainWindow"
        x:CompileBindings="True"
        Width="1400" Height="750"
        MinWidth="1000" MinHeight="600"
        Title="OpenLMStudio">

    <DockPanel>
        <!-- Menu bar -->
        <Menu DockPanel.Dock="Top">
            <MenuItem Header="_File">
                <MenuItem Header="_Open" Command="{Binding OpenCommand}"/>
                <MenuItem Header="_Exit" Command="{Binding ExitCommand}"/>
            </MenuItem>
            <MenuItem Header="_View">
                <MenuItem Header="_Sidebar" IsChecked="{Binding ShowSidebar}"/>
            </MenuItem>
        </Menu>

        <!-- Main content -->
        <Grid>
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="280"/>
                <ColumnDefinition Width="*"/>
                <ColumnDefinition Width="240"/>
            </Grid.ColumnDefinitions>

            <!-- Left sidebar -->
            <Border Grid.Column="0" Background="#252526" Padding="8">
                <ListBox ItemsSource="{Binding Models}"
                         SelectedItem="{Binding SelectedModel}">
                    <ListBox.ItemTemplate>
                        <DataTemplate>
                            <StackPanel Orientation="Horizontal" Padding="4">
                                <PathIcon Data="{Binding Icon}" Width="16" Height="16"/>
                                <TextBlock Text="{Binding Name}" Margin="8,0,0,0"/>
                            </StackPanel>
                        </DataTemplate>
                    </ListBox.ItemTemplate>
                </ListBox>
            </Border>

            <!-- Center pane -->
            <Border Grid.Column="1" Background="#1E1E1E">
                <StackPanel>
                    <!-- Chat area -->
                    <ScrollViewer VerticalScrollBarVisibility="Auto"
                                  HorizontalScrollBarVisibility="Auto">
                        <StackPanel ItemsSource="{Binding Messages}">
                            <!-- Message items -->
                        </StackPanel>
                    </ScrollViewer>

                    <!-- Input area -->
                    <Border Background="#2D2D2D" Padding="8">
                        <Grid>
                            <Grid.ColumnDefinitions>
                                <ColumnDefinition Width="*"/>
                                <ColumnDefinition Width="Auto"/>
                            </Grid.ColumnDefinitions>
                            <TextBox Grid.Column="0"
                                     Watermark="Type a message..."
                                     Text="{Binding InputText}"
                                     KeyDown="OnMessageInputKeyDown"/>
                            <Button Grid.Column="1"
                                    Content="Send"
                                    Command="{Binding SendCommand}"
                                    Classes="accent"/>
                        </Grid>
                    </Border>
                </StackPanel>
            </Border>

            <!-- Right sidebar -->
            <Border Grid.Column="2" Background="#252526" Padding="8">
                <StackPanel>
                    <TextBlock Text="Properties" FontWeight="Bold"/>
                    <TextBlock Text="{Binding SelectedModel.Name}"/>
                </StackPanel>
            </Border>

            <!-- Status bar -->
            <Border Grid.Column="0" Grid.ColumnSpan="3"
                    Grid.Row="1" Height="24"
                    Background="#007ACC"
                    VerticalAlignment="Bottom">
                <TextBlock Text="Ready" VerticalAlignment="Center"
                           HorizontalAlignment="Left"
                           Foreground="White"
                           Margin="8,0,0,0"/>
            </Border>
        </Grid>
    </DockPanel>
</Window>
```
