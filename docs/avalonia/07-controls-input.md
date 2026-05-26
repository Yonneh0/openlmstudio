# Avalonia 12.0.3 Input Controls Reference

## 1. Button

```csharp
// Location: src/Avalonia.Controls/Button.cs
public class Button : ButtonBase
{
    public object? Content { get; set; }
    public DataTemplate? ContentTemplate { get; set; }
    public DataTemplateSelector? ContentTemplateSelector { get; set; }
    public ICommand? Command { get; set; }
    public object? CommandParameter { get; set; }
    public IInputElement? CommandTarget { get; set; }
    
    public event EventHandler<RoutedEventHandler>? Click;
    public event EventHandler<PointerPressedEventArgs>? PointerPressed;
    public event EventHandler<PointerReleasedEventArgs>? PointerReleased;
    
    public void ExecuteCommand();
}
```

## 2. ButtonBase

```csharp
// Location: src/Avalonia.Controls/ButtonBase.cs
public abstract class ButtonBase : ContentControl
{
    public bool IsPressed { get; }
    public RoutedEvent<RoutedEventArgs> ClickEvent { get; }
    
    protected override void OnPointerPressed(PointerPressedEventArgs e);
    protected override void OnPointerReleased(PointerReleasedEventArgs e);
    protected abstract void OnClick();
}
```

## 3. RepeatButton

```csharp
// Location: src/Avalonia.Controls/RepeatButton.cs
public class RepeatButton : ButtonBase
{
    public double Interval { get; set; }
    public double Delay { get; set; }
    
    public event EventHandler<RoutedEventHandler>? Click;
}
```

## 4. ToggleButton

```csharp
// Location: src/Avalonia.Controls/ToggleButton.cs
public class ToggleButton : ToggleButtonBase
{
    public bool IsChecked { get; set; }
    public ICommand? Command { get; set; }
    public object? CommandParameter { get; set; }
    
    public event EventHandler<RoutedEventHandler>? Checked;
    public event EventHandler<RoutedEventHandler>? Unchecked;
    public event EventHandler<RoutedEventHandler>? Indeterminate;
}
```

## 5. ToggleButtonBase

```csharp
// Location: src/Avalonia.Controls/ToggleButtonBase.cs
public abstract class ToggleButtonBase : ButtonBase
{
    public bool IsChecked { get; set; }
    public ICommand? Command { get; set; }
    public object? CommandParameter { get; set; }
    
    public event EventHandler<RoutedEventArgs>? Checked;
    public event EventHandler<RoutedEventArgs>? Unchecked;
    public event EventHandler<RoutedEventArgs>? Indeterminate;
    
    protected override void OnClick();
}
```

## 6. CheckBox

```csharp
// Location: src/Avalonia.Controls/CheckBox.cs
public class CheckBox : ToggleButton
{
    public object? Content { get; set; }
    public DataTemplate? ContentTemplate { get; set; }
    public ICommand? Command { get; set; }
    public object? CommandParameter { get; set; }
}
```

## 7. RadioButton

```csharp
// Location: src/Avalonia.Controls/RadioButton.cs
public class RadioButton : ToggleButton
{
    public string? GroupName { get; set; }
    public object? Content { get; set; }
    public DataTemplate? ContentTemplate { get; set; }
    public ICommand? Command { get; set; }
    public object? CommandParameter { get; set; }
    
    protected override void OnClick();
}
```

## 8. ComboBox

```csharp
// Location: src/Avalonia.Controls/ComboBox.cs
public class ComboBox : ItemsControl
{
    public object? SelectedItem { get; set; }
    public int SelectedIndex { get; set; }
    public bool IsDropDownOpen { get; set; }
    public bool IsEditable { get; set; }
    public string? Text { get; set; }
    public string? Watermark { get; set; }
    public int MaxDropDownHeight { get; set; }
    public int MinDropDownHeight { get; set; }
    public int MaxDropDownWidth { get; set; }
    public int MinDropDownWidth { get; set; }
    public bool IsReadOnly { get; set; }
    public bool IsTextSearchEnabled { get; set; }
    public string? TextSearchPath { get; set; }
    
    public event EventHandler<SelectionChangedEventArgs>? SelectionChanged;
    public event EventHandler<ComboBoxOpenedEventArgs>? Opened;
    public event EventHandler<ComboBoxClosedEventArgs>? Closed;
    public event EventHandler<ComboBoxTextChangedEventArgs>? TextChanged;
    
    public void Open();
    public void Close();
}
```

## 9. ComboBoxItem

```csharp
// Location: src/Avalonia.Controls/ComboBoxItem.cs
public class ComboBoxItem : ContentControl
{
    public bool IsSelected { get; }
}
```

## 10. Slider

```csharp
// Location: src/Avalonia.Controls/Slider.cs
public class Slider : RangeBase
{
    public double Value { get; set; }
    public double Minimum { get; set; }
    public double Maximum { get; set; }
    public double TickFrequency { get; set; }
    public bool IsSnapToTickEnabled { get; set; }
    public Orientation Orientation { get; set; }
    public bool IsDirectionReversed { get; set; }
    public TickPlacement TickPlacement { get; set; }
    public IList<double>? TickPositions { get; }
    public Brush? Fill { get; set; }
    public Brush? ThumbFill { get; set; }
    public Brush? TrackFill { get; set; }
    public Brush? TickBarFill { get; set; }
    public Thickness ThumbBorderThickness { get; set; }
    public CornerRadius ThumbCornerRadius { get; set; }
    public Thickness TrackBorderThickness { get; set; }
    public CornerRadius TrackCornerRadius { get; set; }
    
    public event EventHandler<RangeBaseValueChangedEventArgs>? ValueChanged;
}

public enum TickPlacement
{
    None,
    BottomOrLeft,
    TopOrRight,
    Both,
}
```

## 11. RangeSlider

```csharp
// Location: src/Avalonia.Controls/RangeSlider.cs
public class RangeSlider : Slider
{
    public double LowerValue { get; set; }
    public double UpperValue { get; set; }
    public IList<double>? TickPositions { get; }
    
    public event EventHandler<RangeBaseValueChangedEventArgs>? LowerValueChanged;
    public event EventHandler<RangeBaseValueChangedEventArgs>? UpperValueChanged;
}
```

## 12. Rating

```csharp
// Location: src/Avalonia.Controls/Rating.cs
public class Rating : ContentControl
{
    public double Value { get; set; }
    public int ItemCount { get; set; }
    public bool IsReadOnly { get; set; }
    public bool AllowPartialRatings { get; set; }
    public RatingItemTemplateSelector? ItemTemplateSelector { get; set; }
    public DataTemplate? ItemTemplate { get; set; }
    public Brush? FilledIcon { get; set; }
    public Brush? EmptyIcon { get; set; }
    public Brush? HalfIcon { get; set; }
    public double ItemSize { get; set; }
    public double ItemSpacing { get; set; }
    
    public event EventHandler<RatingChangedEventArgs>? RatingChanged;
}
```

## 13. RatingItem

```csharp
// Location: src/Avalonia.Controls/RatingItem.cs
public class RatingItem : ContentControl
{
    public int Index { get; }
    public bool IsFilled { get; }
    public bool IsHalfFilled { get; }
    public bool IsSelected { get; }
}
```

## 14. NumericUpDown

```csharp
// Location: src/Avalonia.Controls/NumericUpDown.cs
public class NumericUpDown : ContentControl
{
    public double Value { get; set; }
    public double Minimum { get; set; }
    public double Maximum { get; set; }
    public double Increment { get; set; }
    public int DecimalPlaces { get; set; }
    public string? Prefix { get; set; }
    public string? Suffix { get; set; }
    public bool IsReadOnly { get; set; }
    public bool IsIncrementByMouseWheel { get; set; }
    public bool IsSnapToInterval { get; set; }
    public bool ShowButtonSpinner { get; set; }
    
    public event EventHandler<NumericUpDownValueChangedEventArgs>? ValueChanged;
}
```

## 15. DecimalUpDown

```csharp
// Location: src/Avalonia.Controls/DecimalUpDown.cs
public class DecimalUpDown : NumericUpDown
{
    public int DecimalPlaces { get; set; }
    public string? Prefix { get; set; }
    public string? Suffix { get; set; }
}
```

## 16. DecimalNumPicker

```csharp
// Location: src/Avalonia.Controls/DecimalNumPicker.cs
public class DecimalNumPicker : NumericUpDown
{
    public int DecimalPlaces { get; set; }
    public string? Prefix { get; set; }
    public string? Suffix { get; set; }
}
```

## 17. MaskedTextBox

```csharp
// Location: src/Avalonia.Controls/MaskedTextBox.cs
public class MaskedTextBox : TextBox
{
    public string? Mask { get; set; }
    public string? PromptChar { get; set; }
    public string? Text { get; set; }
    public bool IncludeLiterals { get; set; }
    public bool SkipLiterals { get; set; }
    public MaskedTextProvider? MaskedText { get; }
    
    public event EventHandler<MaskedTextChangedEventArgs>? MaskedTextChanged;
}
```

## 18. Calendar

```csharp
// Location: src/Avalonia.Controls/Calendar.cs
public class Calendar : ContentControl
{
    public DateTime? SelectedDate { get; set; }
    public IList<DateTime> SelectedDates { get; }
    public DateTime DisplayDate { get; set; }
    public DateTime DisplayDateEnd { get; set; }
    public DateTime DisplayDateStart { get; set; }
    public CalendarSelectionMode SelectionMode { get; set; }
    public DateTime? BlackoutDates { get; }
    public bool IsTodayHighlighted { get; set; }
    public CalendarDayItemTemplateSelector? DayItemTemplateSelector { get; set; }
    public DataTemplate? DayItemTemplate { get; set; }
    public DataTemplate? HeaderTemplate { get; set; }
    
    public event EventHandler<CalendarSelectedDateChangedEventArgs>? SelectedDateChanged;
    public event EventHandler<CalendarSelectionChangedEventArgs>? SelectionChanged;
}

public enum CalendarSelectionMode
{
    SingleDate,
    SingleRange,
    Multiple,
}
```

## 19. DatePicker

```csharp
// Location: src/Avalonia.Controls/DatePicker.cs
public class DatePicker : ContentControl
{
    public DateTime? SelectedDate { get; set; }
    public string? Watermark { get; set; }
    public string? DisplayDateStart { get; set; }
    public string? DisplayDateEnd { get; set; }
    public bool IsDropDownOpen { get; set; }
    public bool IsTodayHighlighted { get; set; }
    public string? StringFormat { get; set; }
    
    public event EventHandler<DatePickerValueChangedEventArgs>? SelectedDateChanged;
    public event EventHandler<DatePickerOpenedEventArgs>? Opened;
    public event EventHandler<DatePickerClosedEventArgs>? Closed;
}
```

## 20. TimePicker

```csharp
// Location: src/Avalonia.Controls/TimePicker.cs
public class TimePicker : ContentControl
{
    public TimeSpan? SelectedTime { get; set; }
    public string? Watermark { get; set; }
    public bool IsDropDownOpen { get; set; }
    public string? StringFormat { get; set; }
    public int MinuteIncrement { get; set; }
    public int HourIncrement { get; set; }
    
    public event EventHandler<TimePickerValueChangedEventArgs>? SelectedTimeChanged;
    public event EventHandler<TimePickerOpenedEventArgs>? Opened;
    public event EventHandler<TimePickerClosedEventArgs>? Closed;
}
```

## 21. CalendarDatePicker

```csharp
// Location: src/Avalonia.Controls/CalendarDatePicker.cs
public class CalendarDatePicker : ContentControl
{
    public DateTime? SelectedDate { get; set; }
    public string? Watermark { get; set; }
    public bool IsDropDownOpen { get; set; }
    public string? StringFormat { get; set; }
    
    public event EventHandler<CalendarDatePickerValueChangedEventArgs>? SelectedDateChanged;
    public event EventHandler<CalendarDatePickerOpenedEventArgs>? Opened;
    public event EventHandler<CalendarDatePickerClosedEventArgs>? Closed;
}
```

## 22. DateTimePicker

```csharp
// Location: src/Avalonia.Controls/DateTimePicker.cs
public class DateTimePicker : ContentControl
{
    public DateTime? SelectedDateTime { get; set; }
    public string? Watermark { get; set; }
    public bool IsDropDownOpen { get; set; }
    public string? StringFormat { get; set; }
    
    public event EventHandler<DateTimePickerValueChangedEventArgs>? SelectedDateTimeChanged;
}
```

## 23. TickBar

```csharp
// Location: src/Avalonia.Controls/TickBar.cs
public class TickBar : ContentControl
{
    public TickPlacement Placement { get; set; }
    public double TickFrequency { get; set; }
    public IList<double>? TickPositions { get; }
}
```

## 24. ButtonSpinner

```csharp
// Location: src/Avalonia.Controls/ButtonSpinner.cs
public class ButtonSpinner : ContentControl
{
    public object? Content { get; set; }
    public DataTemplate? ContentTemplate { get; set; }
    public bool IsDirectionReversed { get; set; }
    public bool ShowButtonSpinner { get; set; }
    
    public event EventHandler<RoutedEventHandler>? Click;
}
```

## 25. DropDownButton

```csharp
// Location: src/Avalonia.Controls/DropDownButton.cs
public class DropDownButton : ContentControl
{
    public object? Content { get; set; }
    public DataTemplate? ContentTemplate { get; set; }
    public bool IsDropDownOpen { get; set; }
    
    public event EventHandler<DropDownOpenedEventArgs>? Opened;
    public event EventHandler<DropDownClosedEventArgs>? Closed;
}
```

## 26. SplitButton

```csharp
// Location: src/Avalonia.Controls/SplitButton.cs
public class SplitButton : ContentControl
{
    public object? Content { get; set; }
    public DataTemplate? ContentTemplate { get; set; }
    public bool IsDropDownOpen { get; set; }
    
    public event EventHandler<SplitButtonClickedEventArgs>? Clicked;
    public event EventHandler<DropDownOpenedEventArgs>? Opened;
    public event EventHandler<DropDownClosedEventArgs>? Closed;
}
```

## 27. Key Files Reference

| File | Purpose |
|------|---------|
| `src/Avalonia.Controls/Button.cs` | Button |
| `src/Avalonia.Controls/ButtonBase.cs` | ButtonBase |
| `src/Avalonia.Controls/RepeatButton.cs` | RepeatButton |
| `src/Avalonia.Controls/ToggleButton.cs` | ToggleButton |
| `src/Avalonia.Controls/ToggleButtonBase.cs` | ToggleButtonBase |
| `src/Avalonia.Controls/CheckBox.cs` | CheckBox |
| `src/Avalonia.Controls/RadioButton.cs` | RadioButton |
| `src/Avalonia.Controls/ComboBox.cs` | ComboBox |
| `src/Avalonia.Controls/ComboBoxItem.cs` | ComboBoxItem |
| `src/Avalonia.Controls/Slider.cs` | Slider |
| `src/Avalonia.Controls/RangeSlider.cs` | RangeSlider |
| `src/Avalonia.Controls/Rating.cs` | Rating |
| `src/Avalonia.Controls/RatingItem.cs` | RatingItem |
| `src/Avalonia.Controls/NumericUpDown.cs` | NumericUpDown |
| `src/Avalonia.Controls/DecimalUpDown.cs` | DecimalUpDown |
| `src/Avalonia.Controls/DecimalNumPicker.cs` | DecimalNumPicker |
| `src/Avalonia.Controls/MaskedTextBox.cs` | MaskedTextBox |
| `src/Avalonia.Controls/Calendar.cs` | Calendar |
| `src/Avalonia.Controls/DatePicker.cs` | DatePicker |
| `src/Avalonia.Controls/TimePicker.cs` | TimePicker |
| `src/Avalonia.Controls/CalendarDatePicker.cs` | CalendarDatePicker |
| `src/Avalonia.Controls/DateTimePicker.cs` | DateTimePicker |
| `src/Avalonia.Controls/TickBar.cs` | TickBar |
| `src/Avalonia.Controls/ButtonSpinner.cs` | ButtonSpinner |
| `src/Avalonia.Controls/DropDownButton.cs` | DropDownButton |
| `src/Avalonia.Controls/SplitButton.cs` | SplitButton |