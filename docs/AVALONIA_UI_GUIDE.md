# Avalonia UI Guide for OpenLMStudio

**Version:** Avalonia 11.2.5 (project actual version)

## Project Structure
- **Namespace:** `Avalonia` (root)
- **Target:** .NET 8, Win32 (`Avalonia.Win32`)
- **XAML Root:** `<Window xmlns="https://github.com/avaloniaui"` (not WPF)

## Layout Primitives

### DockPanel
- Use `DockPanel.Dock="Top/Bottom/Left/Right"` to dock children
- Last child fills remaining space (default `Fill`)
- **Critical:** If you dock a child to Bottom, the remaining space is NOT automatically Fill — set `DockPanel.Dock="Fill"` on the main content container.

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
- **Grid.Row** and **Grid.ColumnSpan** are the key attached properties.
- Use `Grid.RowDefinitions` with `RowDefinition` for sizing.
- To place something outside the grid (like a status bar spanning all columns), put it in `Grid.Row` and `Grid.ColumnSpan="3"`.

### Canvas
- Children are positioned absolutely via `Canvas.Left`, `Canvas.Top`.
- Canvas children **do not respect** `Grid.ColumnSpan`.
- Useful for overlays (e.g., popups anchored above a status bar).

### StackPanel
- Children stack vertically (default) or horizontally (`Orientation="Horizontal"`).
- Good for lists, headers, and toolbars.

### Panel
- Base class for custom layouts. Children are layered on top of each other.
- Use `Panel` when you need z-ordering.

### UniformGrid
- Grid where all cells are the same size.
- Use `Rows` and `Columns` properties.
- Useful for tile layouts, icon grids.

### Viewbox
- Scales content to fit available space.
- Use `Stretch` property (Uniform, Fill, None, UniformToFill).
- Use `StretchDirection` to control up/down/both scaling.

## Common Controls

### TextBlock
- Read-only text display. Use `Text` property.
- Font properties: `FontFamily`, `FontSize`, `FontWeight`, `Foreground`.
- `TextWrapping="Wrap"` or `NoWrap`.
- Use `FormattedText` for rich text runs with different formatting.

### TextBox
- Input control. Use `Text` property.
- `AcceptsReturn="True"` for multiline.
- `Padding`, `BorderThickness`, `BorderBrush` for styling.
- Events: `KeyDown`, `TextChanged`, `LostFocus`.
- `SelectionChanged`, `SelectAll()`.
- `Watermark` property for placeholder text.

### PasswordBox
- Password input control.
- `Password` property.
- `Watermark` for placeholder.

### Button
- Use `Click` event in code-behind.
- **In Avalonia 11.x+, Button styles in `Window.Styles` do NOT cascade to buttons.** You must apply styles directly to each Button using `Button.Styles`, or define styles on a parent container that the button can inherit from.
- `Padding`, `Margin`, `HorizontalAlignment`, `VerticalAlignment`.
- `VerticalContentAlignment="Center"` — centers content vertically within the button (useful for preventing text clipping in short buttons).
- `FontFamily`, `FontSize`, `FontWeight` — control text appearance.
- `IsDefault` — button activates on Enter key press.
- `IsCancel` — button activates on Escape key press.
- Use `Classes` attribute for style class names (e.g., `Classes="accent"`).
- **Click vs PointerPressed:** Always use `Click` to determine whether a user has pressed a button, not `PointerPressed`. `Click` is the high-level event specific to `Button`, while `PointerPressed` is a low-level input event that `Button` handles internally (setting `IsHandled` to `true`).
- **Keyboard accessibility:** Button is focusable by default and participates in tab navigation. Users activate it by pressing Space or Enter.
- **Icon buttons:** For icon-only buttons, set `AutomationProperties.Name` so screen readers can identify the button.
- `Command` property to bind to `ICommand` in your view model.
- `CommandParameter` to pass a parameter to the command.
- `ClickMode` — controls when Click fires:
  - `Release` (default) — fires on pointer release
  - `Press` — fires on pointer press
  - `Hover` — fires when pointer enters the button
- `Flyout` — attach a `Flyout` for contextual overlays.

### ToggleButton
- Toggle switch button (two states: checked/unchecked).
- `IsChecked` property (nullable `bool?` for three-state mode).
- `IsThreeState` — set to `true` for three-state support (checked/unchecked/indeterminate).
- `:checked` and `:unchecked` pseudo-selectors.
- `:indeterminate` — pseudo-class for three-state mode when `IsChecked` is `null`.
- `:pointerover` for hover (must be on Button.Styles or a parent container's Styles).
- `:pressed` for press state.
- **Critical:** In Avalonia 11.x+, use `ToggleButton.Styles` to apply hover styles — `Window.Styles` does not apply to ToggleButton children.
- `VerticalContentAlignment="Center"` — centers content vertically within the button.
- `ClickMode` — same as Button (Release, Press, Hover).
- Inherits from `Avalonia.Controls.Primitives.ToggleButton`.
- Base class for `CheckBox` and other toggle-style controls.
- **Three-state binding:** When `IsThreeState="true"`, `IsChecked` cycles through `true` → `false` → `null`. Style each state independently using `:checked`, `:unchecked`, and `:indeterminate` pseudo classes.

### RadioButton
- For exclusive selection groups (needs `GroupName` for grouping).
- `IsChecked` for two-state binding.
- Use `RadioButtonGroup` for automatic mutual exclusion.

### ComboBox
- Items via child `<TextBlock>` elements or `ItemsSource`.
- `SelectionChanged` event.
- `Foreground`, `Background` for styling.
- `SelectedIndex`, `SelectedItem`, `SelectedValue` for data binding.
- `IsEditable` for editable combobox.
- `IsDropDownOpen` to control open state.

### Slider
- `Minimum`, `Maximum`, `Value`, `TickFrequency`, `IsSnapToTickEnabled`.
- `ValueChanged` event.
- Display current value in a bound TextBlock.
- `Orientation` for horizontal/vertical.
- `IsMoveToPointEnabled` for click-to-seek.

### ProgressBar
- `Minimum`, `Maximum`, `Value` for determinate progress.
- `IsIndeterminate="True"` for indeterminate (spinning) progress.
- `Minimum`, `Maximum`, `Value` properties.
- Use in `StackPanel` or `Grid` for progress display.
- Style with `ProgressBar.Styles` for custom appearance.

### RepeatButton
- Button that fires `Click` events repeatedly while held down.
- `Delay` for initial delay before repeats start.
- `Interval` for repeat interval.

### DropDownButton
- Button with a dropdown menu.
- `Menu` property for dropdown content.
- `IsOpen` to control dropdown state.

### Icon / PathIcon
- `Icon` property for displaying icons.
- `PathIcon` with vector path data.
- `FontIcon` for font-based icons.
- `Width`, `Height` for sizing.

### Image
- `Source` property for image source (Bitmap, RenderBitmap, etc.).
- `Stretch` property (Uniform, Fill, None, UniformToFill).
- `StretchDirection` property.

### Ellipse / Rectangle / Line
- Drawing primitives.
- `Fill` for brush, `Stroke` for border.
- `Width`, `Height`, `RadiusX`, `RadiusY` for Ellipse/Rectangle.

### Viewbox
- Scales content to fit available space.
- `Stretch` and `StretchDirection` properties.

## Navigation Controls

### TabControl / TabItem
- `TabControl` for tabbed navigation.
- `TabItem` with `Header` for tab titles.
- `ItemsSource` for data binding.
- `SelectedItem` for current tab.
- `SelectionChanged` event.
- Use `TabItem.Styles` for custom tab styling.
- `TabStripPlacement` for top/left/bottom/right tabs.

### ListBox / ListView / GridView
- `ListBox` for single/multi-select lists.
- `ListView` for list display with columns.
- `GridView` for column-based display.
- `ItemsSource` for data binding.
- `SelectedItem`, `SelectedItems` for selection.
- `SelectionMode` for Single/Multiple/Extended.
- `SelectionMode="Toggle"` for click-to-toggle selection.

### DataGrid
- Rich data grid from `Avalonia.Controls.DataGrid`.
- `ItemsSource` for data binding.
- `AutoGenerateColumns` for automatic column creation.
- `Columns` for manual column definition.
- `DataGridTextColumn`, `DataGridCheckBoxColumn`, `DataGridComboBoxColumn`.
- `Sorting`, `SelectionMode`, `CanUserSortColumns`.
- `Avalonia.Controls.DataGrid` namespace.

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
- **NOT** `Avalonia.Controls.Primitives.PopupContentPresenter` (used internally).

### Tooltip
- `ToolTip` attached property.
- `ToolTip.Tip` for tooltip content.
- `ToolTipService` for custom tooltips.

## Overlay Controls

### ContentDialog
- Modal dialog content overlay.
- `ContentDialog.ShowAsync()` / `ContentDialog.Hide()`.
- `Title`, `Content`, `PrimaryButtonText`, `SecondaryButtonText`.
- `PrimaryButtonCommand`, `SecondaryButtonCommand`.
- `XamlRoot` for XAML-based content.
- `Avalonia.Dialogs` namespace.

### MessageBox
- Simple message dialog.
- `MessageBox.Show(message, title, buttons, icon)`.
- `MessageBox.ShowAsync()` for async.
- `MessageBoxButton` enum (OK, OKCancel, YesNo, YesNoCancel, RetryCancel).
- `MessageBoxImage` enum (Error, Warning, Information, None).

### NotificationCard
- Non-intrusive notifications (snackbars).
- `NotificationCard.Show()`.
- `Duration` for auto-dismiss.
- `Avalonia.Controls.Notifications` namespace.

### InfoBar / InfoBarMessage
- Information bar for status messages (success, warning, error).
- `InfoBar.IsOpen` to show/hide.
- `InfoBar.MessageSeverity` (Information, Success, Warning, Error).
- `InfoBar.Title`, `InfoBar.Message`.
- `InfoBar.CloseButtonContent` for close button.
- `Avalonia.Controls.Notifications` namespace.

## Forms

### Form
- Form layout control.
- `ItemsSource` for form items.
- `Label` for form labels.
- `Avalonia.Controls` namespace.

### DataValidationErrors
- `DataValidationErrors.Add()` to add validation errors.
- `DataValidationErrors.Clear()` to remove all.
- `DataValidationErrors.GetErrors()` to get errors.
- `DataValidationErrors.HasErrors` property.
- `Avalonia.Data` namespace.

### ValidationErrors
- Attached property for validation.
- `ValidationErrors.GetErrors()`, `ValidationErrors.SetErrors()`.
- `Avalonia.Data` namespace.

### NumericUpDown
- Numeric input with up/down buttons.
- `Minimum`, `Maximum`, `Value`.
- `Increment` for step size.
- `FormatString` for display format.
- `Avalonia.Controls` namespace.

### MaskedTextBox
- Text input with mask pattern.
- `Mask`, `PromptChar`, `MaskCompleted`.
- `Avalonia.Controls` namespace.

### DatePicker
- Date selection control.
- `Date` property.
- `SelectedDate`, `SelectedDateChanged`.
- `Avalonia.Controls` namespace.

### TimePicker
- Time selection control.
- `Time` property.
- `SelectedTime`, `SelectedTimeChanged`.
- `Avalonia.Controls` namespace.

## Media

### Media Overview (163 items)
- **Avalonia.Media** — Drawing, text, and media primitives.
- **Avalonia.Media.Fonts** — Font management (7 items).
- **Avalonia.Media.Imaging** — Image rendering (6 items).
- **Avalonia.Media.Immutable** — Immutable media types (11 items).
- **Avalonia.Media.TextFormatting** — Rich text formatting (32 items).
- **Avalonia.Media.TextFormatting.Unicode** — Unicode text (13 items).
- **Avalonia.Media.Transformation** — Transforms (9 items).

### Brush Types
- `SolidColorBrush` — solid fill.
- `LinearGradientBrush` — linear gradient.
- `RadialGradientBrush` — radial gradient.
- `DrawingBrush` — draw with vector graphics.
- `VisualBrush` — capture a visual as a brush.

### Pen / Stroke
- `Stroke` for border.
- `StrokeThickness` for width.
- `StrokeDashArray` for dashed lines.

### Color
- `Color.FromArgb(a, r, g, b)` for ARGB.
- `Colors` static class for named colors.
- `#AARRGGBB` or `#RRGGBB` hex notation.

## Input

### Input Overview (84 items)
- **Avalonia.Input** — Core input handling.
- **Avalonia.Input.GestureRecognizers** — Touch/gesture support (3 items).
- **Avalonia.Input.Platform** — Platform-specific input (5 items).
- **Avalonia.Input.Raw** — Raw input (2 items).
- **Avalonia.Input.TextInput** — Text input (9 items).

### Key Events
- `KeyDown`, `KeyUp`, `TextInput` events.
- `KeyEventArgs` for key events.
- `TextInputEventArgs` for text input.
- `PointerPressedEventArgs`, `PointerReleasedEventArgs` for pointer events.

### Mouse Events
- `PointerPressed`, `PointerReleased`, `PointerMoved`.
- `PointerWheelChanged` for scroll wheel.
- `PointerCapture`, `PointerReleased`.

### Keyboard Events
- `KeyDown`, `KeyUp`, `TextInput`.
- `Key` enum for key values.
- `KeyModifiers` for modifier keys (Ctrl, Shift, Alt).

### Input Events
- `Click` for button clicks.
- `PointerPressed` for pointer input.
- `PointerWheelChanged` for scroll wheel.
- `GotFocus`, `LostFocus` for focus events.

### Selection Events
- `SelectionChanged` for selection changes.
- `SelectedValue`, `SelectedIndex`, `SelectedItem`.

### Input Events
- `InputMethod` for IME input.
- `InputMethod.GetIsEnabled()`, `InputMethod.SetIsEnabled()`.

## Styling

### Window.Styles vs Control.Styles
**CRITICAL:** In Avalonia 11.x+, `Window.Styles` does NOT cascade to child controls. Styles defined in `Window.Styles` only apply to the Window itself.

To style child controls:
1. **Use `Control.Styles`** — Each control has its own `Styles` collection (e.g., `Button.Styles`).
2. **Use `Control.Resources`** — Each control has a `Resources` dictionary (e.g., `Button.Resources`).
3. **Use `Container.Styles`** — Containers like `StackPanel`, `Grid`, `DockPanel` have `Styles` that cascade to children.
4. **Use `x:Key` with `DynamicResource`** — Define a keyed style in `Window.Styles` and reference it.

### Style Selectors (in Window.Styles)
```xml
<Window.Styles>
    <Style Selector="Button.accent">
        <Setter Property="Background" Value="{StaticResource AccentBlue}"/>
        <Setter Property="Foreground" Value="White"/>
    </Style>
</Window.Styles>
```
- Use `Selector` attribute for the selector string.
- Use `TargetType` for type-based selectors.
- Use `x:Key` for keyed styles (with `DynamicResource`).

### Control.Styles
```xml
<Button>
    <Button.Styles>
        <Style Selector="Button:pointerover">
            <Setter Property="Background" Value="#454549"/>
        </Style>
    </Button.Styles>
</Button>
```

### Container.Styles
```xml
<StackPanel>
    <StackPanel.Styles>
        <Style Selector="Button:hover">
            <Setter Property="Background" Value="#454549"/>
        </Style>
    </StackPanel.Styles>
</StackPanel>
```

### Control.Resources
```xml
<Button>
    <Button.Resources>
        <Style Selector="Button:pointerover">
            <Setter Property="Background" Value="#454549"/>
        </Style>
    </Button.Resources>
</Button>
```

### Pseudo-Classes (Selectors)
- `:pointerover` — mouse hover
- `:hover` — mouse hover (alias for `:pointerover`)
- `:pressed` — pressed state
- `:checked` / `:unchecked` — checked state (for ToggleButton, CheckBox, RadioButton)
- `:selected` — selected state (for ListBoxItem, TabItem, etc.)
- `:focus` / `:focused` — focused state
- `:disabled` — disabled state
- `:empty` — no content
- `:first-child`, `:last-child`, `:nth-child(n)` — positional
- `/template>` — descend into ControlTemplate (e.g., `Button:pointerover /template> Border`)
- `>` — direct child combinator (e.g., `Button > TextBlock`)
- ` ` — descendant combinator (e.g., `StackPanel Button`)

### StaticResource vs DynamicResource
- `StaticResource` — resolved once at compile time (preferred for performance).
- `DynamicResource` — resolved at runtime (for themes that change).

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

## Event Handling

### Code-Behind
```csharp
private void OnSendClicked(object? sender, RoutedEventArgs e) { ... }
private void OnMessageInputKeyDown(object? sender, KeyEventArgs e) {
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

## Animation

### Animation Overview (44 items)
- **Avalonia.Animation** — Animation system.
- **Avalonia.Animation.Easings** — Easing functions.

### Storyboard
- `Storyboard` for timeline-based animation.
- `DoubleAnimation`, `ColorAnimation`, `ThicknessAnimation`, `VectorAnimation`.
- `Duration` for animation duration.
- `Easing` for easing functions.
- `RepeatBehavior` for repeat patterns.
- `Begin()`, `Stop()` methods.

### Easing Functions
- `LinearEasing` — no easing.
- `CubicEase` — cubic curve.
- `QuadraticEase`, `QuarticEase`, `QuinticEase`.
- `SineEase`, `ExponentialEase`.
- `ElasticEase` — elastic bounce.
- `BackEase` — overshoot and settle.
- `CircleEase` — circular curve.
- `PowerEase` — power curve.
- `SpringEase` — spring bounce.

### VisualStateManager
- `VisualStateManager` for state-based animations.
- `VisualState` for individual states.
- `VisualTransition` for state transitions.
- `GoToState()` method.
- `Avalonia.Controls.Primitives` namespace.

## Data Binding

### Data Overview (25 items)
- **Avalonia.Data** — Data binding system.
- **Avalonia.Data.Converters** — Value converters (11 items).
- **Avalonia.Data.Core** — Core binding (11 items).
- **Avalonia.Data.Core.Plugins** — Binding plugins (11 items).

### Binding
- `{Binding Path=PropertyName}` for binding.
- `RelativeSource` for relative binding.
- `ElementName` for element binding.
- `Converter` for value conversion.
- `ConverterParameter` for converter parameters.
- `Mode` for OneWay, TwoWay, OneTime.

### DataValidationErrors
- `DataValidationErrors.Add()` to add validation errors.
- `DataValidationErrors.Clear()` to remove all.
- `DataValidationErrors.GetErrors()` to get errors.
- `DataValidationErrors.HasErrors` property.
- `Avalonia.Data` namespace.

### Converters
- `IValueConverter` — value conversion.
- `IMultiValueConverter` — multi-value conversion.
- `BooleanToVisibilityConverter` — bool to visibility.
- `StringFormatConverter` — string formatting.
- `Avalonia.Data.Converters` namespace.

## Theming

### ThemeVariant
- `ThemeVariant` for dark/light theme support.
- `ThemeVariant.Default`, `ThemeVariant.Dark`, `ThemeVariant.Light`.
- `RequestedThemeVariant` for setting.
- `Avalonia.Styling` namespace.

### ThemeVariantScope
- `ThemeVariantScope` for scoped theming.
- `UseThemeVariant()` method.
- `Avalonia.Styling` namespace.

### DynamicResource
- `DynamicResource` for theme-aware resources.
- `StaticResource` for compile-time resources.
- Use `DynamicResource` when theme can change at runtime.

### Styles
- `Styles` collection on all controls.
- `Styles.Add()` to add styles.
- `Styles.Clear()` to remove all.
- `Styles.FirstOrDefault()` to find a style.
- `Avalonia.Styling` namespace.

## Layout

### Layout Overview (10 items)
- **Avalonia.Layout** — Core layout system.

### ILayoutProvider
- `ILayoutProvider` for custom layout providers.
- `ILayoutProviderFactory` for creating providers.
- `Avalonia.Layout` namespace.

### Size Constraints
- `Width`, `Height` for size.
- `MinWidth`, `MinHeight` for minimum size.
- `MaxWidth`, `MaxHeight` for maximum size.
- `DesiredSize`, `RenderSize` for layout measurements.
- `Avalonia.Layout` namespace.

## Markup/Extensions

### Markup/XAML
- **Avalonia.Markup.Xaml** — XAML support.
- **Avalonia.Markup.Xaml.MarkupExtensions** — Markup extensions (14 items).
- **Avalonia.Markup.Xaml.MarkupExtensions.CompiledBindings** — Compiled bindings (3 items).

### Markup Extensions
- `{Binding}` — data binding.
- `{StaticResource}` — static resource reference.
- `{DynamicResource}` — dynamic resource reference.
- `{x:Bind}` — compiled binding (Avalonia 11+).
- `{x:Type}` — type reference.
- `{x:Null}` — null value.
- `{x:Static}` — static member reference.
- `Avalonia.Markup.Xaml.MarkupExtensions` namespace.

### Compiled Bindings
- `{x:Bind}` for compiled bindings (faster, type-safe).
- `Avalonia.Markup.Xaml.MarkupExtensions.CompiledBindings` namespace.

## Platform-Specific Assemblies

### Avalonia.Desktop
- Desktop-specific controls and services.
- `Avalonia.Desktop` namespace.

### Avalonia.Win32
- Windows-specific rendering.
- `Avalonia.Win32` namespace.

### Avalonia.Direct2D1
- Direct2D1 rendering backend.
- `Avalonia.Direct2D1` namespace.

### Avalonia.Skia
- Skia-based rendering backend.
- `Avalonia.Skia` namespace.

### Avalonia.X11
- X11 rendering backend (Linux).
- `Avalonia.X11` namespace.

### Avalonia.LinuxFramebuffer
- Linux framebuffer rendering.
- `Avalonia.LinuxFramebuffer` namespace.

### Avalonia.iOS
- iOS rendering backend.
- `Avalonia.iOS` namespace.

### Avalonia.Android
- Android rendering backend.
- `Avalonia.Android` namespace.

### Avalonia.Browser
- WebAssembly rendering backend.
- `Avalonia.Browser` namespace.
- `Avalonia.Browser.Blazor` for Blazor integration.

### Avalonia.Native
- Native rendering backend.
- `Avalonia.Native` namespace.

### Avalonia.Fonts.Inter
- Inter font family.
- `Avalonia.Fonts.Inter` namespace.

### Avalonia.Themes.Fluent
- Fluent Design theme.
- `Avalonia.Themes.Fluent` namespace.

### Avalonia.Themes.Simple
- Simple theme.
- `Avalonia.Themes.Simple` namespace.

### Avalonia.Controls.ColorPicker
- Color picker control.
- `Avalonia.Controls.ColorPicker` namespace.

### Avalonia.Controls.DataGrid
- Data grid control.
- `Avalonia.Controls.DataGrid` namespace.

## Platform-Specific Controls

### Avalonia.Controls.Chrome
- `WindowChrome` for custom window chrome.
- `TitleBar` for title bar.
- `Avalonia.Controls.Chrome` namespace.

### Avalonia.Controls.ApplicationLifetimes
- `ISingleViewApplicationLifetime` — single view lifetime.
- `IFunctionalApplicationLifetime` — functional lifetime.
- `Avalonia.Controls.ApplicationLifetimes` namespace.

### Avalonia.Controls.Notifications
- `NotificationType` — notification types (Information, Success, Warning, Error).
- `INotificationService` — notification service.
- `NotificationService` — notification service implementation.
- `Avalonia.Controls.Notifications` namespace.

## Control Templates

### ControlTemplate
- `ControlTemplate` for custom control appearance.
- `ControlPresenter` for presenting control content.
- `TemplatedParent` for accessing the templated control.
- `Avalonia.Controls.Templates` namespace.

### Template Parts
- Named parts in templates (e.g., `PART_Popup`, `PART_ContentPresenter`).
- `GetTemplateChild()` for accessing parts.
- `Avalonia.Controls.Templates` namespace.

### Template Bindings
- `TemplateBinding` for binding to templated parent.
- `x:Bind` for compiled binding in templates.
- `Avalonia.Markup.Xaml.MarkupExtensions` namespace.

## Control Presenters

### ContentPresenter
- `ContentPresenter` for presenting content.
- `Content` property for content.
- `ContentTemplate` for content template.
- `Avalonia.Controls.Presenters` namespace.

### ItemsPresenter
- `ItemsPresenter` for presenting items.
- `Items` property for items.
- `Avalonia.Controls.Presenters` namespace.

### TextPresenter
- `TextPresenter` for presenting text.
- `Text` property for text.
- `Avalonia.Controls.Presenters` namespace.

### ScrollContentPresenter
- `ScrollContentPresenter` for scrollable content.
- `VerticalScrollBarVisibility`, `HorizontalScrollBarVisibility`.
- `Avalonia.Controls.Presenters` namespace.

## Controls.Primitives

### Controls.Primitives (48 items)
- **Avalonia.Controls.Primitives** — Primitive controls.
- **Avalonia.Controls.Primitives.PopupPositioning** — Popup positioning (7 items).
- **Avalonia.Controls.Primitives** namespace.

### PopupPositioning
- `PopupPositioner` for positioning popups.
- `PopupPositioningMode` for positioning modes.
- `Avalonia.Controls.Primitives.PopupPositioning` namespace.

### Primitives
- `ContentControl` — content wrapper.
- `ItemsControl` — items container.
- `Selector` — selectable control.
- `ToggleButton` — toggle button.
- `Avalonia.Controls.Primitives` namespace.

## Automation

### Automation (16 items)
- **Avalonia.Automation** — UI automation.
- **Avalonia.Automation.Peers** — Automation peers (27 items).
- **Avalonia.Automation.Provider** — Automation providers (12 items).
- **Avalonia.Automation** namespace.

### Automation Peers
- `AutomationPeer` for exposing controls to automation.
- `GetAutomationPeer()` method.
- `Avalonia.Automation.Peers` namespace.

### Automation Providers
- `IProvider` — automation provider interface.
- `IValueProvider` — value provider.
- `IAutomationElementProvider` — element provider.
- `Avalonia.Automation.Provider` namespace.

## Data Collections

### Collections (12 items)
- **Avalonia.Collections** — Collection types.
- **Avalonia.Collections** namespace.

### Types
- `ObservableCollection` — observable collection.
- `ObservableVector` — observable vector.
- `Avalonia.Collections` namespace.

## Metadata

### Metadata (22 items)
- **Avalonia.Metadata** — Metadata attributes.
- **Avalonia.Metadata** namespace.

### Attributes
- `ContentAttribute` — content property.
- `PropertyAttribute` — property metadata.
- `TypeConverterAttribute` — type converter.
- `Avalonia.Metadata` namespace.

## Utilities

### Utilities (22 items)
- **Avalonia.Utilities** — Utility classes.
- **Avalonia.Utilities** namespace.

### Types
- `WeakReference` — weak reference.
- `ValueTuple` — value tuples.
- `Avalonia.Utilities` namespace.

## Threading

### Threading (19 items)
- **Avalonia.Threading** — Threading support.
- **Avalonia.Threading** namespace.

### Types
- `Dispatcher` — dispatcher for UI thread.
- `Dispatcher.UIThread` — UI thread dispatcher.
- `DispatcherPriority` — dispatcher priority.
- `Avalonia.Threading` namespace.

## Logging

### Logging (5 items)
- **Avalonia.Logging** — Logging system.
- **Avalonia.Logging** namespace.

### Types
- `LogEvent` — log event.
- `ILogger` — logger interface.
- `Avalonia.Logging` namespace.

## Logical Tree

### LogicalTree (8 items)
- **Avalonia.LogicalTree** — Logical tree traversal.
- **Avalonia.LogicalTree** namespace.

### Types
- `ILogical` — logical tree interface.
- `ILogicalTreeHandler` — logical tree handler.
- `Avalonia.LogicalTree` namespace.

## Rendering

### Rendering (4 items)
- **Avalonia.Rendering** — Rendering system.
- **Avalonia.Rendering** namespace.

### Composition
- **Avalonia.Rendering.Composition** — Composition rendering (37 items).
- **Avalonia.Rendering.Composition.Animations** — Composition animations (9 items).
- **Avalonia.Rendering.Composition.Transport** — Composition transport (2 items).
- **Avalonia.Rendering.SceneGraph** — Scene graph (1 item).

## Visual Tree

### VisualTree (4 items)
- **Avalonia.VisualTree** — Visual tree.
- **Avalonia.VisualTree** namespace.

### Types
- `IVisual` — visual interface.
- `IVisualTreeHandler` — visual tree handler.
- `Avalonia.VisualTree` namespace.

## Vulkan

### Vulkan (19 items)
- **Avalonia.Vulkan** — Vulkan rendering.
- **Avalonia.Vulkan** namespace.

### Types
- `VulkanDevice` — Vulkan device.
- `VulkanSwapChain` — Vulkan swap chain.
- `Avalonia.Vulkan` namespace.

## Dialogs

### Dialogs (5 items)
- **Avalonia.Dialogs** — Dialog system.
- **Avalonia.Dialogs.Internal** — Internal dialog support (9 items).
- **Avalonia.Dialogs** namespace.

### Types
- `ContentDialog` — content dialog.
- `MessageBox` — message box.
- `Avalonia.Dialogs` namespace.

## Designer Support

### Designer Support
- **Avalonia.DesignerSupport** — Designer support (1 item).
- **Avalonia.DesignerSupport.Remote** — Remote designer (1 item).
- **Avalonia.DesignerSupport.Remote.HtmlTransport** — HTML transport (5 items).

## OpenGL

### OpenGL (18 items)
- **Avalonia.OpenGL** — OpenGL support.
- **Avalonia.OpenGL.Controls** — OpenGL controls (1 item).
- **Avalonia.OpenGL.Egl** — EGL support (14 items).
- **Avalonia.OpenGL.Features** — OpenGL features (1 item).
- **Avalonia.OpenGL.Surfaces** — OpenGL surfaces (4 items).

## Platform.Storage

### Platform.Storage (19 items)
- **Avalonia.Platform.Storage** — Storage platform.
- **Avalonia.Platform.Storage** namespace.

### Types
- `IStorageProvider` — storage provider.
- `IStorageFile` — storage file.
- `IStorageFolder` — storage folder.
- `Avalonia.Platform.Storage` namespace.

## Reactive

### Reactive (1 item)
- **Avalonia.Reactive** — Reactive support.
- **Avalonia.Reactive** namespace.

## Media (163 items)

### Avalonia.Media
- Core media types (163 items).
- `Avalonia.Media` namespace.

### Avalonia.Media.Fonts
- Font management (7 items).
- `Avalonia.Media.Fonts` namespace.

### Avalonia.Media.Imaging
- Image rendering (6 items).
- `Avalonia.Media.Imaging` namespace.

### Avalonia.Media.Immutable
- Immutable media types (11 items).
- `Avalonia.Media.Immutable` namespace.

### Avalonia.Media.TextFormatting
- Rich text formatting (32 items).
- `Avalonia.Media.TextFormatting` namespace.

### Avalonia.Media.TextFormatting.Unicode
- Unicode text formatting (13 items).
- `Avalonia.Media.TextFormatting.Unicode` namespace.

### Avalonia.Media.Transformation
- Transformations (9 items).
- `Avalonia.Media.Transformation` namespace.

## Metadata

### Avalonia.Metadata (22 items)
- Metadata attributes (22 items).
- `Avalonia.Metadata` namespace.

## Platform

### Avalonia.Platform (41 items)
- Platform services (41 items).
- `Avalonia.Platform` namespace.

## Visuals.Platform

### Avalonia.Visuals.Platform (1 item)
- Visuals platform (1 item).
- `Avalonia.Visuals.Platform` namespace.

## Controls (206 items)

### Avalonia.Controls
- Core controls (206 items).
- `Avalonia.Controls` namespace.

### Avalonia.Controls.ApplicationLifetimes (15 items)
- Application lifetimes (15 items).
- `Avalonia.Controls.ApplicationLifetimes` namespace.

### Avalonia.Controls.Automation.Peers (9 items)
- Automation peers (9 items).
- `Avalonia.Controls.Automation.Peers` namespace.

### Avalonia.Controls.Chrome (2 items)
- Window chrome (2 items).
- `Avalonia.Controls.Chrome` namespace.

### Avalonia.Controls.Converters (8 items)
- Converters (8 items).
- `Avalonia.Controls.Converters` namespace.

### Avalonia.Controls.Diagnostics (2 items)
- Diagnostics (2 items).
- `Avalonia.Controls.Diagnostics` namespace.

### Avalonia.Controls.Documents (10 items)
- Documents (10 items).
- `Avalonia.Controls.Documents` namespace.

### Avalonia.Controls.Embedding (1 item)
- Embedding (1 item).
- `Avalonia.Controls.Embedding` namespace.

### Avalonia.Controls.Generators (3 items)
- Generators (3 items).
- `Avalonia.Controls.Generators` namespace.

### Avalonia.Controls.Metadata (2 items)
- Metadata (2 items).
- `Avalonia.Controls.Metadata` namespace.

### Avalonia.Controls.Mixins (2 items)
- Mixins (2 items).
- `Avalonia.Controls.Mixins` namespace.

### Avalonia.Controls.Notifications (8 items)
- Notifications (8 items).
- `Avalonia.Controls.Notifications` namespace.

### Avalonia.Controls.Platform (18 items)
- Platform (18 items).
- `Avalonia.Controls.Platform` namespace.

### Avalonia.Controls.Platform.Surfaces (3 items)
- Surfaces (3 items).
- `Avalonia.Controls.Platform.Surfaces` namespace.

### Avalonia.Controls.Presenters (4 items)
- Presenters (4 items).
- `Avalonia.Controls.Presenters` namespace.

### Avalonia.Controls.Primitives (48 items)
- Primitives (48 items).
- `Avalonia.Controls.Primitives` namespace.

### Avalonia.Controls.Primitives.PopupPositioning (7 items)
- Popup positioning (7 items).
- `Avalonia.Controls.Primitives.PopupPositioning` namespace.

### Avalonia.Controls.Remote (3 items)
- Remote (3 items).
- `Avalonia.Controls.Remote` namespace.

### Avalonia.Controls.Selection (10 items)
- Selection (10 items).
- `Avalonia.Controls.Selection` namespace.

### Avalonia.Controls.Shapes (9 items)
- Shapes (9 items).
- `Avalonia.Controls.Shapes` namespace.

### Avalonia.Controls.Templates (21 items)
- Templates (21 items).
- `Avalonia.Controls.Templates` namespace.

### Avalonia.Controls.Utils (3 items)
- Utils (3 items).
- `Avalonia.Controls.Utils` namespace.

## Data (25 items)

### Avalonia.Data
- Data binding (25 items).
- `Avalonia.Data` namespace.

### Avalonia.Data.Converters (11 items)
- Value converters (11 items).
- `Avalonia.Data.Converters` namespace.

### Avalonia.Data.Core (11 items)
- Core binding (11 items).
- `Avalonia.Data.Core` namespace.

### Avalonia.Data.Core.Plugins (11 items)
- Binding plugins (11 items).
- `Avalonia.Data.Core.Plugins` namespace.

## Dialogs

### Avalonia.Dialogs (5 items)
- Dialog system (5 items).
- `Avalonia.Dialogs` namespace.

### Avalonia.Dialogs.Internal (9 items)
- Internal dialogs (9 items).
- `Avalonia.Dialogs.Internal` namespace.

## Diagnostics

### Avalonia.Diagnostics (2 items)
- Diagnostics (2 items).
- `Avalonia.Diagnostics` namespace.

## Designer Support

### Avalonia.DesignerSupport (1 item)
- Designer support (1 item).
- `Avalonia.DesignerSupport` namespace.

### Avalonia.DesignerSupport.Remote (1 item)
- Remote designer (1 item).
- `Avalonia.DesignerSupport.Remote` namespace.

### Avalonia.DesignerSupport.Remote.HtmlTransport (5 items)
- HTML transport (5 items).
- `Avalonia.DesignerSupport.Remote.HtmlTransport` namespace.

## Input (84 items)

### Avalonia.Input
- Core input (84 items).
- `Avalonia.Input` namespace.

### Avalonia.Input.GestureRecognizers (3 items)
- Gesture recognizers (3 items).
- `Avalonia.Input.GestureRecognizers` namespace.

### Avalonia.Input.Platform (5 items)
- Platform input (5 items).
- `Avalonia.Input.Platform` namespace.

### Avalonia.Input.Raw (2 items)
- Raw input (2 items).
- `Avalonia.Input.Raw` namespace.

### Avalonia.Input.TextInput (9 items)
- Text input (9 items).
- `Avalonia.Input.TextInput` namespace.

## Interactivity

### Avalonia.Interactivity (9 items)
- Interactivity (9 items).
- `Avalonia.Interactivity` namespace.

## Layout (10 items)

### Avalonia.Layout
- Core layout (10 items).
- `Avalonia.Layout` namespace.

## Logging

### Avalonia.Logging (5 items)
- Logging (5 items).
- `Avalonia.Logging` namespace.

## LogicalTree

### Avalonia.LogicalTree (8 items)
- Logical tree (8 items).
- `Avalonia.LogicalTree` namespace.

## Markup/XAML

### Avalonia.Markup.Xaml (14 items)
- XAML support (14 items).
- `Avalonia.Markup.Xaml` namespace.

### Avalonia.Markup.Xaml.Converters (8 items)
- XAML converters (8 items).
- `Avalonia.Markup.Xaml.Converters` namespace.

### Avalonia.Markup.Xaml.Diagnostics (1 item)
- XAML diagnostics (1 item).
- `Avalonia.Markup.Xaml.Diagnostics` namespace.

### Avalonia.Markup.Xaml.MarkupExtensions (14 items)
- Markup extensions (14 items).
- `Avalonia.Markup.Xaml.MarkupExtensions` namespace.

### Avalonia.Markup.Xaml.MarkupExtensions.CompiledBindings (3 items)
- Compiled bindings (3 items).
- `Avalonia.Markup.Xaml.MarkupExtensions.CompiledBindings` namespace.

### Avalonia.Markup.Xaml.Styling (3 items)
- XAML styling (3 items).
- `Avalonia.Markup.Xaml.Styling` namespace.

### Avalonia.Markup.Xaml.Templates (7 items)
- XAML templates (7 items).
- `Avalonia.Markup.Xaml.Templates` namespace.

### Avalonia.Markup.Xaml.XamlIl.Runtime (6 items)
- XAML IL runtime (6 items).
- `Avalonia.Markup.Xaml.XamlIl.Runtime` namespace.

## Media (163 items)

### Avalonia.Media
- Core media (163 items).
- `Avalonia.Media` namespace.

### Avalonia.Media.Fonts (7 items)
- Fonts (7 items).
- `Avalonia.Media.Fonts` namespace.

### Avalonia.Media.Imaging (6 items)
- Imaging (6 items).
- `Avalonia.Media.Imaging` namespace.

### Avalonia.Media.Immutable (11 items)
- Immutable media (11 items).
- `Avalonia.Media.Immutable` namespace.

### Avalonia.Media.TextFormatting (32 items)
- Text formatting (32 items).
- `Avalonia.Media.TextFormatting` namespace.

### Avalonia.Media.TextFormatting.Unicode (13 items)
- Unicode text (13 items).
- `Avalonia.Media.TextFormatting.Unicode` namespace.

### Avalonia.Media.Transformation (9 items)
- Transformations (9 items).
- `Avalonia.Media.Transformation` namespace.

## Metadata

### Avalonia.Metadata (22 items)
- Metadata (22 items).
- `Avalonia.Metadata` namespace.

## OpenGL

### Avalonia.OpenGL (18 items)
- OpenGL (18 items).
- `Avalonia.OpenGL` namespace.

### Avalonia.OpenGL.Controls (1 item)
- OpenGL controls (1 item).
- `Avalonia.OpenGL.Controls` namespace.

### Avalonia.OpenGL.Egl (14 items)
- EGL (14 items).
- `Avalonia.OpenGL.Egl` namespace.

### Avalonia.OpenGL.Features (1 item)
- OpenGL features (1 item).
- `Avalonia.OpenGL.Features` namespace.

### Avalonia.OpenGL.Surfaces (4 items)
- OpenGL surfaces (4 items).
- `Avalonia.OpenGL.Surfaces` namespace.

## Platform

### Avalonia.Platform (41 items)
- Platform (41 items).
- `Avalonia.Platform` namespace.

### Avalonia.Platform.Storage (19 items)
- Storage (19 items).
- `Avalonia.Platform.Storage` namespace.

## Reactive

### Avalonia.Reactive (1 item)
- Reactive (1 item).
- `Avalonia.Reactive` namespace.

## Rendering

### Avalonia.Rendering (4 items)
- Rendering (4 items).
- `Avalonia.Rendering` namespace.

### Avalonia.Rendering.Composition (37 items)
- Composition (37 items).
- `Avalonia.Rendering.Composition` namespace.

### Avalonia.Rendering.Composition.Animations (9 items)
- Composition animations (9 items).
- `Avalonia.Rendering.Composition.Animations` namespace.

### Avalonia.Rendering.Composition.Transport (2 items)
- Composition transport (2 items).
- `Avalonia.Rendering.Composition.Transport` namespace.

### Avalonia.Rendering.SceneGraph (1 item)
- Scene graph (1 item).
- `Avalonia.Rendering.SceneGraph` namespace.

## Styling

### Avalonia.Styling (24 items)
- Styling (24 items).
- `Avalonia.Styling` namespace.

## Threading

### Avalonia.Threading (19 items)
- Threading (19 items).
- `Avalonia.Threading` namespace.

## Utilities

### Avalonia.Utilities (22 items)
- Utilities (22 items).
- `Avalonia.Utilities` namespace.

## VisualTree

### Avalonia.VisualTree (4 items)
- Visual tree (4 items).
- `Avalonia.VisualTree` namespace.

## Vulkan

### Avalonia.Vulkan (19 items)
- Vulkan (19 items).
- `Avalonia.Vulkan` namespace.

## Window.Styles — The Critical Gotcha

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

### Pseudo-Classes (Selectors)

- `:pointerover` — mouse hover
- `:hover` — mouse hover (alias for `:pointerover`)
- `:pressed` — pressed state
- `:checked` / `:unchecked` — checked state (ToggleButton, CheckBox, RadioButton)
- `:selected` — selected state (ListBoxItem, TabItem, etc.)
- `:focus` / `:focused` — focused state
- `:disabled` — disabled state
- `:empty` — no content
- `:first-child`, `:last-child`, `:nth-child(n)` — positional
- `/template>` — descend into ControlTemplate (e.g., `Button:pointerover /template> Border`)
- `>` — direct child combinator (e.g., `Button > TextBlock`)
- ` ` — descendant combinator (e.g., `StackPanel Button`)

## Window Setup

### Basic Window
```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        x:Class="OpenLMStudio.Desktop.MainWindow"
        Width="1400" Height="750"
        MinWidth="1000" MinHeight="600">
```
- **Window can only have ONE child** — wrap everything in a single panel.
- `Width/Height` and `MinWidth/MinHeight` are set on the Window element directly.
- `x:Class` maps to the code-behind class.

### Code-Behind Class
```csharp
public partial class MainWindow : Window
{
    public MainWindow(...) { InitializeComponent(); ... }
}
```

## Layout Tips

1. **Status bars** — Put in Grid Row N with a fixed height, spanning all columns with `Grid.ColumnSpan`.
2. **Popups above status bars** — Use a Canvas in the same Grid.Row, positioned with `Margin="0,0,0,24"` (the status bar height) to stay above it.
3. **DockPanel + Grid** — Use DockPanel for top-level layout (menu bar on top), Grid for the content area.
4. **ScrollViewer** — Always wrap long content areas (message lists, settings) in ScrollViewer with auto scrollbars.
5. **Border** — Use Border for visual containers with padding, background, and rounded corners.
6. **Popup** — Use `Avalonia.Controls.Primitives.Popup` for true popup semantics (outside parent bounds, renders above window clipping). Use `Border` with `IsVisible` for simple overlays within bounds.
7. **Button styles** — Use `Button.Styles` or `Container.Styles` for child control styles — `Window.Styles` does NOT cascade to children.

## Common Patterns in OpenLMStudio

- **Three-panel layout:** DockPanel → Grid (3 columns). Left sidebar (fixed), center pane (proportional), right sidebar (fixed).
- **Status bar:** Grid.Row=1, Border spanning all columns.
- **Tab navigation:** ToggleButton or RadioButton per tab; show/hide StackPanel content via code-behind.
- **Settings panels:** StackPanel inside ScrollViewer with DockPanel for label-value rows.
- **Message display:** ScrollViewer → StackPanel with Border for user/assistant messages.
- **Hover on buttons:** Use `Button.Styles` with `:pointerover` selector for hover effects on buttons.

## Resources
- API Reference: https://api-docs.avaloniaui.net/
- Main Docs: https://docs.avaloniaui.net/ (Avalonia 12 docs)
- v11 Docs: https://v11.docs.avaloniaui.net/
- Current version: 11.2.5
- Avalonia 12 Breaking Changes: https://docs.avaloniaui.net/docs/avalonia12-breaking-changes
