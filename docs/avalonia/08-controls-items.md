# Avalonia 12.0.3 Items Controls Reference

## 1. ItemsControl

```csharp
// Location: src/Avalonia.Controls/ItemsControl.cs
public class ItemsControl : ContentControl, ISelectable, IHeadered
{
    // Items
    public object? Items { get; set; }
    public IList? ItemsSource { get; set; }
    public DataTemplate? ItemTemplate { get; set; }
    public DataTemplateSelector? ItemTemplateSelector { get; set; }
    public ItemsSourceView? ItemsSourceView { get; }
    
    // Selection
    public SelectionMode SelectionMode { get; set; }
    public IList SelectedItems { get; }
    public int SelectedIndex { get; set; }
    public object? SelectedItem { get; set; }
    
    // Item container
    public ItemContainerGenerator ItemContainerGenerator { get; }
    
    // Header
    public object? Header { get; set; }
    public DataTemplate? HeaderTemplate { get; set; }
    
    // Events
    public event EventHandler<SelectionChangedEventArgs>? SelectionChanged;
    public event EventHandler<ContainerPreparedEventArgs>? ContainerPrepared;
    public event EventHandler<ContainerIndexChangedEventArgs>? ContainerIndexChanged;
    public event EventHandler<ContainerClearingEventArgs>? ContainerClearing;
    
    protected override void OnApplyTemplate(ApplyTemplateEventArgs e);
    protected virtual void OnItemsChanged(object? change);
    protected virtual void OnSelectionChanged(SelectionChangedEventArgs e);
    protected virtual void OnLoaded();
    protected virtual void OnUnloaded();
}
```

## 2. ItemCollection

```csharp
// Location: src/Avalonia.Controls/ItemCollection.cs
public class ItemCollection : IList, ICollection, IEnumerable
{
    public object? this[int index] { get; set; }
    public int Count { get; }
    public bool IsReadOnly { get; }
    public bool IsFixedSize { get; }
    
    public void Add(object? item);
    public void AddRange(IEnumerable items);
    public void Remove(object? item);
    public void RemoveAt(int index);
    public void Clear();
    public int IndexOf(object? item);
    public void Insert(int index, object? item);
    public void RemoveRange(int index, int count);
    public void Move(int oldIndex, int newIndex);
    public void Replace(object? oldItem, object? newItem);
    public void ReplaceRange(IEnumerable oldItems, IEnumerable newItems);
}
```

## 3. ItemsSourceView

```csharp
// Location: src/Avalonia.Controls/ItemsSourceView.cs
public class ItemsSourceView : IList, ICollection, IEnumerable, INotifyCollectionChanged, INotifyPropertyChanged
{
    public object? this[int index] { get; }
    public int Count { get; }
    public bool IsReadOnly { get; }
    
    public event NotifyCollectionChangedEventHandler? CollectionChanged;
    public event PropertyChangedEventHandler? PropertyChanged;
    
    public void Refresh();
    public void Refresh(IList? source);
}
```

## 4. ItemContainerGenerator

```csharp
// Location: src/Avalonia.Controls/ItemContainerGenerator.cs
public class ItemContainerGenerator
{
    public int ContainerCount { get; }
    public IList Containers { get; }
    
    public void GenerateContainers();
    public void Clear();
    public void Refresh();
    public UIElement? GetContainerAt(int index);
    public int GetIndexAt(UIElement container);
    public void Move(int oldIndex, int newIndex);
    public void Remove(int index, int count);
    public void Insert(int index, int count);
}
```

## 5. ListBox

```csharp
// Location: src/Avalonia.Controls/ListBox.cs
public class ListBox : ItemsControl
{
    public SelectionMode SelectionMode { get; set; }
    public IList SelectedItems { get; }
    public int SelectedIndex { get; set; }
    public object? SelectedItem { get; set; }
    
    public event EventHandler<SelectionChangedEventArgs>? SelectionChanged;
}
```

## 6. ListBoxItem

```csharp
// Location: src/Avalonia.Controls/ListBoxItem.cs
public class ListBoxItem : ContentControl
{
    public bool IsSelected { get; set; }
    public bool IsSelected { get; }
    public bool IsFocused { get; }
    public bool IsKeyboardFocused { get; }
}
```

## 7. ListView

```csharp
// Location: src/Avalonia.Controls/ListView.cs
public class ListView : ListBox
{
    public bool IsVirtualizing { get; set; }
    public bool IsGroupingEnabled { get; set; }
    public bool IsSortingEnabled { get; set; }
    public bool IsColumnResizingEnabled { get; set; }
    public bool IsColumnReorderingEnabled { get; set; }
    public bool IsColumnHeaderVisible { get; set; }
    public double ColumnHeaderHeight { get; set; }
    
    public event EventHandler<SelectionChangedEventArgs>? SelectionChanged;
}
```

## 8. TreeView

```csharp
// Location: src/Avalonia.Controls/TreeView.cs
public class TreeView : ItemsControl
{
    public SelectionMode SelectionMode { get; set; }
    public IList SelectedItems { get; }
    public int SelectedIndex { get; set; }
    public object? SelectedItem { get; set; }
    public bool IsExpanded { get; set; }
    public bool IsItemExpanded { get; set; }
    public bool IsSelectionActive { get; set; }
    
    public event EventHandler<SelectionChangedEventArgs>? SelectionChanged;
    public event EventHandler<TreeViewExpandedEventArgs>? Expanded;
    public event EventHandler<TreeViewCollapsedEventArgs>? Collapsed;
}
```

## 9. TreeViewItem

```csharp
// Location: src/Avalonia.Controls/TreeViewItem.cs
public class TreeViewItem : ContentControl
{
    public bool IsExpanded { get; set; }
    public bool IsSelected { get; set; }
    public bool HasItems { get; }
    public bool HasHeader { get; }
    public bool HasContent { get; }
    public bool IsLeaf { get; }
    public bool IsRoot { get; }
    public int Level { get; }
    public TreeViewItem? Parent { get; }
    public IList<TreeViewItem> Children { get; }
    
    public void Expand();
    public void Collapse();
    public void Toggle();
}
```

## 10. TabControl

```csharp
// Location: src/Avalonia.Controls/TabControl.cs
public class TabControl : ItemsControl
{
    public int SelectedIndex { get; set; }
    public object? SelectedItem { get; set; }
    public bool IsContentPreserved { get; set; }
    public bool IsTabFill { get; set; }
    public TabStripPlacement TabStripPlacement { get; set; }
    public TabStripHeader? TabStripHeader { get; set; }
    public TabStripFooter? TabStripFooter { get; set; }
    
    public event EventHandler<SelectionChangedEventArgs>? SelectionChanged;
}

public enum TabStripPlacement
{
    Top,
    Bottom,
    Left,
    Right,
}
```

## 11. TabItem

```csharp
// Location: src/Avalonia.Controls/TabItem.cs
public class TabItem : HeaderedContentControl
{
    public bool IsSelected { get; set; }
    public bool IsEnabled { get; set; }
    public bool IsReadOnly { get; set; }
    public TabItem? Parent { get; }
}
```

## 12. Menu

```csharp
// Location: src/Avalonia.Controls/Menu.cs
public class Menu : ItemsControl
{
    public object? Items { get; set; }
    public IList? ItemsSource { get; set; }
    public DataTemplate? ItemTemplate { get; set; }
    public DataTemplateSelector? ItemTemplateSelector { get; set; }
    public bool IsOpen { get; set; }
    public bool IsKeyboardFocused { get; set; }
    
    public event EventHandler<RoutedEventArgs>? Opened;
    public event EventHandler<RoutedEventArgs>? Closed;
}
```

## 13. MenuItem

```csharp
// Location: src/Avalonia.Controls/MenuItem.cs
public class MenuItem : HeaderedItemsControl
{
    public object? Header { get; set; }
    public DataTemplate? HeaderTemplate { get; set; }
    public object? Icon { get; set; }
    public DataTemplate? IconTemplate { get; set; }
    public object? Items { get; set; }
    public IList? ItemsSource { get; set; }
    public DataTemplate? ItemTemplate { get; set; }
    public DataTemplateSelector? ItemTemplateSelector { get; set; }
    public MenuItemToggleType ToggleType { get; set; }
    public bool IsChecked { get; set; }
    public bool IsOpen { get; set; }
    public bool IsSubmenuOpen { get; }
    public bool IsEnabled { get; set; }
    public ICommand? Command { get; set; }
    public object? CommandParameter { get; set; }
    public IInputElement? CommandTarget { get; set; }
    
    public event EventHandler<RoutedEventArgs>? Checked;
    public event EventHandler<RoutedEventArgs>? Unchecked;
    public event EventHandler<RoutedEventArgs>? Opened;
    public event EventHandler<RoutedEventArgs>? Closed;
    public event EventHandler<RoutedEventArgs>? Click;
}

public enum MenuItemToggleType
{
    None,
    CheckBox,
    Radio,
}
```

## 14. ContextMenu

```csharp
// Location: src/Avalonia.Controls/ContextMenu.cs
public class ContextMenu : Menu
{
    public bool IsOpen { get; set; }
    public bool StaysOpen { get; set; }
    public UIElement? PlacementTarget { get; set; }
    public HorizontalAlignment HorizontalAlignment { get; set; }
    public VerticalAlignment VerticalAlignment { get; set; }
    public double HorizontalOffset { get; set; }
    public double VerticalOffset { get; set; }
    
    public void Open();
    public void Close();
}
```

## 15. Separator

```csharp
// Location: src/Avalonia.Controls/Separator.cs
public class Separator : ContentControl
{
    public static readonly AttachedProperty<bool> IsVerticalProperty;
}
```

## 16. Carousel

```csharp
// Location: src/Avalonia.Controls/Carousel.cs
public class Carousel : ItemsControl
{
    public object? Items { get; set; }
    public IList? ItemsSource { get; set; }
    public DataTemplate? ItemTemplate { get; set; }
    public DataTemplateSelector? ItemTemplateSelector { get; set; }
    public int CurrentItem { get; set; }
    public object? SelectedItem { get; }
    public bool IsLoopEnabled { get; set; }
    public bool IsSwipeEnabled { get; set; }
    public bool IsTransitionEnabled { get; set; }
    public TimeSpan TransitionDuration { get; set; }
    public IPageTransition? Transition { get; set; }
    
    public event EventHandler<CurrentItemChangedEventArgs>? CurrentIndexChanged;
    public event EventHandler<SelectionChangedEventArgs>? SelectionChanged;
    
    public void MoveNext();
    public void MovePrevious();
    public void MoveTo(int index);
}
```

## 17. MenuBase

```csharp
// Location: src/Avalonia.Controls/MenuBase.cs
public abstract class MenuBase : ItemsControl
{
    public bool IsOpen { get; set; }
    public bool IsKeyboardFocused { get; set; }
    
    public event EventHandler<RoutedEventArgs>? Opened;
    public event EventHandler<RoutedEventArgs>? Closed;
}
```

## 18. HeaderedItemsControl

```csharp
// Location: src/Avalonia.Controls/HeaderedItemsControl.cs
public class HeaderedItemsControl : ItemsControl, IHeadered
{
    public object? Header { get; set; }
    public DataTemplate? HeaderTemplate { get; set; }
}
```

## 19. ItemsPresenter

```csharp
// Location: src/Avalonia.Controls/ItemsPresenter.cs
public class ItemsPresenter : ContentControl
{
    public object? Items { get; set; }
    public IList? ItemsSource { get; set; }
    public DataTemplate? ItemTemplate { get; set; }
    public DataTemplateSelector? ItemTemplateSelector { get; set; }
    public ItemContainerGenerator? ItemContainerGenerator { get; }
    
    protected override void OnApplyTemplate(ApplyTemplateEventArgs e);
}
```

## 20. Selection Model

```csharp
// Location: src/Avalonia.Controls/Selection/
public interface ISelectable
{
    SelectionMode SelectionMode { get; set; }
    IList SelectedItems { get; }
    int SelectedIndex { get; set; }
    object? SelectedItem { get; set; }
    bool IsSelectionActive { get; }
}

public enum SelectionMode
{
    Single,
    Multiple,
    Extended,
}

public class SelectionChangedEventArgs : RoutedEventArgs
{
    public IList AddedItems { get; }
    public IList RemovedItems { get; }
    public int Index { get; }
}
```

## 21. Key Files Reference

| File | Purpose |
|------|---------|
| `src/Avalonia.Controls/ItemsControl.cs` | ItemsControl |
| `src/Avalonia.Controls/ItemCollection.cs` | ItemCollection |
| `src/Avalonia.Controls/ItemsSourceView.cs` | ItemsSourceView |
| `src/Avalonia.Controls/ItemContainerGenerator.cs` | ItemContainerGenerator |
| `src/Avalonia.Controls/ListBox.cs` | ListBox |
| `src/Avalonia.Controls/ListBoxItem.cs` | ListBoxItem |
| `src/Avalonia.Controls/ListView.cs` | ListView |
| `src/Avalonia.Controls/TreeView.cs` | TreeView |
| `src/Avalonia.Controls/TreeViewItem.cs` | TreeViewItem |
| `src/Avalonia.Controls/TabControl.cs` | TabControl |
| `src/Avalonia.Controls/TabItem.cs` | TabItem |
| `src/Avalonia.Controls/Menu.cs` | Menu |
| `src/Avalonia.Controls/MenuItem.cs` | MenuItem |
| `src/Avalonia.Controls/ContextMenu.cs` | ContextMenu |
| `src/Avalonia.Controls/Separator.cs` | Separator |
| `src/Avalonia.Controls/Carousel.cs` | Carousel |
| `src/Avalonia.Controls/MenuBase.cs` | MenuBase |
| `src/Avalonia.Controls/HeaderedItemsControl.cs` | HeaderedItemsControl |
| `src/Avalonia.Controls/ItemsPresenter.cs` | ItemsPresenter |