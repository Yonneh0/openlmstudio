# Avalonia UI Guide for OpenLMStudio

**Version:** Avalonia 11.3.12

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

## Common Controls

### TextBlock
- Read-only text display. Use `Text` property.
- Font properties: `FontFamily`, `FontSize`, `FontWeight`, `Foreground`.
- `TextWrapping="Wrap"` or `NoWrap`.

### TextBox
- Input control. Use `Text` property.
- `AcceptsReturn="True"` for multiline.
- `Padding`, `BorderThickness`, `BorderBrush` for styling.
- Events: `KeyDown`, `TextChanged`, `LostFocus`.

### Button
- Use `Click` event in code-behind.
- Background/Foreground via styles or direct properties.
- `Padding`, `Margin`, `HorizontalAlignment`, `VerticalAlignment`.

### ToggleButton / RadioButton
- `ToggleButton` for switchable UI (tabs, toggles).
- `RadioButton` for exclusive selection groups (needs `GroupName` for grouping).
- `IsChecked` for two-state binding.

### ComboBox
- Items via child `<TextBlock>` elements or `ItemsSource`.
- `SelectionChanged` event.
- `Foreground`, `Background` for styling.

### Slider
- `Minimum`, `Maximum`, `Value`, `TickFrequency`, `IsSnapToTickEnabled`.
- `ValueChanged` event.
- Display current value in a bound TextBlock.

### ScrollViewer
- Wrap content in `ScrollViewer` with `VerticalScrollBarVisibility="Auto"` or `Disabled`.
- `HorizontalScrollBarVisibility` also available.

### Border
- `CornerRadius="4"` for rounded corners.
- `Background`, `BorderBrush`, `BorderThickness`, `Padding`.
- Commonly used as a visual container.

### Popup
- Use `Avalonia.Controls.Primitives.Popup` for true popup semantics (outside parent bounds, renders above window clipping).
- Pattern: `<Popup x:Name="..." PlacementTarget="{Binding ElementName=Target}" Placement="Bottom" IsLightDismissEnabled="True">` + code-behind toggle with `SetValue(Popup.IsOpenProperty, ...)`.
- **DO NOT use `Avalonia.Controls.Popup`** — it doesn't exist. The correct namespace is `Avalonia.Controls.Primitives`.
- For simple overlays within bounds, use `Border` with `IsVisible` property + `SetValue(Border.IsVisibleProperty, ...)`.

### Canvas overlay
- Place a `Canvas` inside a Grid cell to position children absolutely relative to that cell.
- Children use `Canvas.Left` and `Canvas.Top`.

## Styling

### XAML Style Selectors
```xml
<Window.Styles>
    <Style Selector="Button.accent">
        <Setter Property="Background" Value="{StaticResource AccentBlue}"/>
        <Setter Property="Foreground" Value="White"/>
    </Style>
</Window.Styles>
```

### StaticResource vs DynamicResource
- `StaticResource` — resolved once at compile time (preferred for performance).
- `DynamicResource` — resolved at runtime (for themes that change).

### SolidColorBrush
```xml
<SolidColorBrush x:Key="BgPrimary" Color="#1E1E22"/>
```
Use `StaticResource` binding: `Background="{StaticResource BgPrimary}"`

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
6. **No Popup control** — Use Border with IsVisible for showing/hiding overlays.

## Common Patterns in OpenLMStudio

- **Three-panel layout:** DockPanel → Grid (3 columns). Left sidebar (fixed), center pane (proportional), right sidebar (fixed).
- **Status bar:** Grid.Row=1, Border spanning all columns.
- **Tab navigation:** ToggleButton or RadioButton per tab; show/hide StackPanel content via code-behind.
- **Settings panels:** StackPanel inside ScrollViewer with DockPanel for label-value rows.
- **Message display:** ScrollViewer → StackPanel with Border for user/assistant messages.

## Resources
- API Reference: https://api-docs.avaloniaui.net/
- Main Docs: https://docs.avaloniaui.net/
- Current version: 11.3.12