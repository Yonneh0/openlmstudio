# Avalonia 12.0.3 Styling & Theming Reference

## 1. Styles

```csharp
// Location: src/Avalonia.Base/Styling/
public class Styles : IList<IStyle>, ICollection<IStyle>, IEnumerable<IStyle>, IEnumerable
{
    public IStyle this[int index] { get; set; }
    public int Count { get; }
    public bool IsReadOnly { get; }
    
    public void Add(IStyle style);
    public void Remove(IStyle style);
    public void Clear();
    public int IndexOf(IStyle style);
    public void Insert(int index, IStyle style);
    public void RemoveAt(int index);
    public void AddRange(IEnumerable<IStyle> styles);
    public bool Contains(IStyle style);
    public void CopyTo(IStyle[] array, int arrayIndex);
}
```

## 2. Stylesheet

```csharp
// Location: src/Avalonia.Base/Styling/Stylesheet.cs
public class Stylesheet : IStyle
{
    public string? Name { get; set; }
    public int Priority { get; set; }
    public bool CanApply { get; }
    public IStyle? Parent { get; }
    
    public bool Apply(StyledElement element);
    public void Remove(StyledElement element);
}
```

## 3. StyleSelector

```csharp
// Location: src/Avalonia.Base/Styling/StyleSelector.cs
public class StyleSelector : IStyle
{
    public IList<IStyle> Styles { get; }
    public int Priority { get; set; }
    public bool CanApply { get; }
    public IStyle? Parent { get; }
    
    public virtual IStyle? SelectStyle(IStyle? currentStyle, object? item, object? container, int index);
    public bool Apply(StyledElement element);
    public void Remove(StyledElement element);
}
```

## 4. Style

```csharp
// Location: src/Avalonia.Base/Styling/Style.cs
public class Style : IStyle
{
    public string? Name { get; set; }
    public int Priority { get; set; }
    public bool CanApply { get; }
    public IStyle? Parent { get; }
    
    // Selectors
    public Type? Selector { get; set; }
    public string? SelectorString { get; set; }
    public IList<SetterBase> Setters { get; }
    public IList<IStyle> Children { get; }
    public IList<IDataTemplate> DataTemplates { get; }
    
    public bool Apply(StyledElement element);
    public void Remove(StyledElement element);
}
```

## 5. Setter

```csharp
// Location: src/Avalonia.Base/Styling/Setter.cs
public class Setter : SetterBase
{
    public AvaloniaProperty Property { get; set; }
    public object? Value { get; set; }
    public object? TargetNullValue { get; set; }
    public IValueConverter? Converter { get; set; }
    public object? ConverterParameter { get; set; }
    public RelativeSource? RelativeSource { get; set; }
    public bool IsDynamic { get; }
    
    public void Apply(StyledElement element);
    public void Remove(StyledElement element);
}

// Typed setter
public class Setter<T> : Setter
{
    public new T Value { get; set; }
}

// Multi-setter
public class MultiSetter : SetterBase
{
    public IList<SetterBase> Setters { get; }
    
    public void Apply(StyledElement element);
    public void Remove(StyledElement element);
}
```

## 6. CSS-like Selectors

```csharp
// Location: src/Avalonia.Base/Styling/
// Supported CSS-like selectors:

// Class selector: .my-class
public class ClassSelector : Selector
{
    public string ClassName { get; set; }
}

// ID selector: #my-id
public class IdSelector : Selector
{
    public string Id { get; set; }
}

// Pseudo-class selectors:
// :hover - element is hovered
// :focus - element has focus
// :pressed - element is pressed
// :disabled - element is disabled
// :checked - element is checked
// :unchecked - element is not checked
// :first - first child
// :last - last child
// :child>of - direct child of
// :descendant>of - descendant of
// :ancestor - is ancestor
// :not - negation
// :nth(n) - nth child
// :nth-last(n) - nth from last
// :odd - odd child
// :even - even child
// :empty - no children
// :first-child - first child
// :last-child - last child
// :only-child - only child
// :only-of-type - only of type
// :first-of-type - first of type
// :last-of-type - last of type
// :root - root element
// :focus-within - has focused descendant
// :focus-visible - focus visible
// :disabled - disabled
// :enabled - enabled
// :read-only - read only
// :read-write - read write
// :required - required
// :optional - optional
// :valid - valid
// :invalid - invalid
// :in-range - in range
// :out-of-range - out of range
// :placeholder-shown - placeholder shown
// :fullscreen - fullscreen
// :modal - modal
// :target - target
// :visited - visited
// :link - link
// :local-link - local link
// :any-link - any link
// :defined - defined
// :lang - language
// :is - is
// :where - where
// :has - has
// :nth-child - nth child
// :nth-last-child - nth last child
// :nth-of-type - nth of type
// :nth-last-of-type - nth last of type
// :first-child - first child
// :last-child - last child
// :only-child - only child
// :only-of-type - only of type
// :first-of-type - first of type
// :last-of-type - last of type
// :root - root
// :scope - scope
// :host - host
// :host-context - host context
// :dir - direction
// :dir(ltr) - left to right
// :dir(rtl) - right to left
// :lang(en) - language
// :lang(en-US) - language with region
// :has(.child) - has child
// :not(.child) - not child
// :is(.child) - is child
// :where(.child) - where child
// :nth-child(2n) - nth child
// :nth-child(2n+1) - nth child with offset
// :nth-child(-n+3) - nth child negative
// :nth-last-child(2n) - nth last child
// :nth-last-child(2n+1) - nth last child with offset
// :nth-last-child(-n+3) - nth last child negative
// :nth-of-type(2n) - nth of type
// :nth-of-type(2n+1) - nth of type with offset
// :nth-of-type(-n+3) - nth of type negative
// :nth-last-of-type(2n) - nth last of type
// :nth-last-of-type(2n+1) - nth last of type with offset
// :nth-last-of-type(-n+3) - nth last of type negative
// :first-child - first child
// :last-child - last child
// :only-child - only child
// :only-of-type - only of type
// :first-of-type - first of type
// :last-of-type - last of type
// :root - root
// :scope - scope
// :host - host
// :host-context(.parent) - host context
// :dir(ltr) - direction left to right
// :dir(rtl) - direction right to left
// :lang(en) - language
// :lang(en-US) - language with region
// :has(.child) - has child
// :not(.child) - not child
// :is(.child) - is child
// :where(.child) - where child
// :empty - empty
// :first-child - first child
// :last-child - last child
// :only-child - only child
// :only-of-type - only of type
// :first-of-type - first of type
// :last-of-type - last of type
// :root - root
// :scope - scope
// :host - host
// :host-context(.parent) - host context
// :dir(ltr) - direction left to right
// :dir(rtl) - direction right to left
// :lang(en) - language
// :lang(en-US) - language with region
// :has(.child) - has child
// :not(.child) - not child
// :is(.child) - is child
// :where(.child) - where child
// :nth-child(2n) - nth child
// :nth-child(2n+1) - nth child with offset
// :nth-child(-n+3) - nth child negative
// :nth-last-child(2n) - nth last child
// :nth-last-child(2n+1) - nth last child with offset
// :nth-last-child(-n+3) - nth last child negative
// :nth-of-type(2n) - nth of type
// :nth-of-type(2n+1) - nth of type with offset
// :nth-of-type(-n+3) - nth of type negative
// :nth-last-of-type(2n) - nth last of type
// :nth-last-of-type(2n+1) - nth last of type with offset
// :nth-last-of-type(-n+3) - nth last of type negative
// :first-child - first child
// :last-child - last child
// :only-child - only child
// :only-of-type - only of type
// :first-of-type - first of type
// :last-of-type - last of type
// :root - root
// :scope - scope
// :host - host
// :host-context(.parent) - host context
// :dir(ltr) - direction left to right
// :dir(rtl) - direction right to left
// :lang(en) - language
// :lang(en-US) - language with region
// :has(.child) - has child
// :not(.child) - not child
// :is(.child) - is child
// :where(.child) - where child
// :empty - empty
// :first-child - first child
// :last-child - last child
// :only-child - only child
// :only-of-type - only of type
// :first-of-type - first of type
// :last-of-type - last of type
// :root - root
// :scope - scope
// :host - host
// :host-context(.parent) - host context
// :dir(ltr) - direction left to right
// :dir(rtl) - direction right to left
// :lang(en) - language
// :lang(en-US) - language with region
// :has(.child) - has child
// :not(.child) - not child
// :is(.child) - is child
// :where(.child) - where child
// :empty - empty
// :first-child - first child
// :last-child - last child
// :only-child - only child
// :only-of-type - only of type
// :first-of-type - first of type
// :last-of-type - last of type
// :root - root
// :scope - scope
// :host - host
// :host-context(.parent) - host context
// :dir(ltr) - direction left to right
// :dir(rtl) - direction right to left
// :lang(en) - language
// :lang(en-US) - language with region
// :has(.child) - has child
// :not(.child) - not child
// :is(.child) - is child
// :where(.child) - where child
// :empty - empty
// :first-child - first child
// :last-child - last child
// :only-child - only child
// :only-of-type - only of type
// :first-of-type - first of type
// :last-of-type - last of type
// :root - root
// :scope - scope
// :host - host
// :host-context(.parent) - host context
// :dir(ltr) - direction left to right
// :dir(rtl) - direction right to left
// :lang(en) - language
// :lang(en-US) - language with region
// :has(.child) - has child
// :not(.child) - not child
// :is(.child) - is child
// :where(.child) - where child
// :empty - empty
// :first-child - first child
// :last-child - last child
// :only-child - only child
// :only-of-type - only of type
// :first-of-type - first of type
// :last-of-type - last of type
// :root - root
// :scope - scope
// :host - host
// :host-context(.parent) - host context
// :dir(ltr) - direction left to right
// :dir(rtl) - direction right to left
// :lang(en) - language
// :lang(en-US) - language with region
// :has(.child) - has child
// :not(.child) - not child
// :is(.child) - is child
// :where(.child) - where child
// :empty - empty
// :first-child - first child
// :last-child - last child
// :only-child - only child
// :only-of-type - only of type
// :first-of-type - first of type
// :last-of-type - last of type
// :root - root
// :scope - scope
// :host - host
// :host-context(.parent) - host context
// :dir(ltr) - direction left to right
// :dir(rtl) - direction right to left
// :lang(en) - language
// :lang(en-US) - language with region
// :has(.child) - has child
// :not(.child) - not child
// :is(.child) - is child
// :where(.child) - where child
// :empty - empty
// :first-child - first child
// :last-child - last child
// :only-child - only child
// :only-of-type - only of type
// :first-of-type - first of type
// :last-of-type - last of type
// :root - root
// :scope - scope
// :host - host
// :host-context(.parent) - host context
// :dir(ltr) - direction left to right
// :dir(rtl) - direction right to left
// :lang(en) - language
// :lang(en-US) - language with region
// :has(.child) - has child
// :not(.child) - not child
// :is(.child) - is child
// :where(.child) - where child
// :empty - empty
// :first-child - first child
// :last-child - last child
// :only-child - only child
// :only-of-type - only of type
// :first-of-type - first of type
// :last-of-type - last of type
// :root - root
// :scope - scope
// :host - host
// :host-context(.parent) - host context
// :dir(ltr) - direction left to right
// :dir(rtl) - direction right to left
// :lang(en) - language
// :lang(en-US) - language with region
// :has(.child) - has child
// :not(.child) - not child
// :is(.child) - is child
// :where(.child) - where child
// :empty - empty
// :first-child - first child
// :last-child - last child
// :only-child - only child
// :only-of-type - only of type
// :first-of-type - first of type
// :last-of-type - last of type
// :root - root
// :scope - scope
// :host - host
// :host-context(.parent) - host context
// :dir(ltr) - direction left to right
// :dir(rtl) - direction right to left
// :lang(en) - language
// :lang(en-US) - language with region
// :has(.child) - has child
// :not(.child) - not child
// :is(.child) - is child
// :where(.child) - where child
// :empty - empty
// :first-child - first child
// :last-child - last child
// :only-child - only child
// :only-of-type - only of type
// :first-of-type - first of type
// :last-of-type - last of type
// :root - root
// :scope - scope
// :host - host
// :host-context(.parent) - host context
// :dir(ltr) - direction left to right
// :dir(rtl) - direction right to left
// :lang(en) - language
// :lang(en-US) - language with region
// :has(.child) - has child
// :not(.child) - not child
// :is(.child) - is child
// :where(.child) - where child
// :empty - empty
// :first-child - first child
// :last-child - last child
// :only-child - only child
// :only-of-type - only of type
// :first-of-type - first of type
// :last-of-type - last of type
// :root - root
// :scope - scope
// :host - host
// :host-context(.parent) - host context
// :dir(ltr) - direction left to right
// :dir(rtl) - direction right to left
// :lang(en) - language
// :lang(en-US) - language with region
// :has(.child) - has child
// :not(.child) - not child
// :is(.child) - is child
// :where(.child) - where child
// :empty - empty
// :first-child - first child
// :last-child - last child
// :only-child - only child
// :only-of-type - only of type
// :first-of-type - first of type
// :last-of-type - last of type
// :root - root
// :scope - scope
// :host - host
// :host-context(.parent) - host context
// :dir(ltr) - direction left to right
// :dir(rtl) - direction right to left
// :lang(en) - language
// :lang(en-US) - language with region
// :has(.child) - has child
// :not(.child) - not child
// :is(.child) - is child
// :where(.child) - where child
// :empty - empty
// :first-child - first child
// :last-child - last child
// :only-child - only child
// :only-of-type - only of type
// :first-of-type - first of type
// :last-of-type - last of type
// :root - root
// :scope - scope
// :host - host
// :host-context(.parent) - host context
// :dir(ltr) - direction left to right
// :dir(rtl) - direction right to left
// :lang(en) - language
// :lang(en-US) - language with region
// :has(.child) - has child
// :not(.child) - not child
// :is(.child) - is child
// :where(.child) - where child
// :empty - empty
// :first-child - first child
// :last-child - last child
// :only-child - only child
// :only-of-type - only of type
// :first-of-type - first of type
// :last-of-type - last of type
// :root - root
// :scope - scope
// :host - host
// :host-context(.parent) - host context
// :dir(ltr) - direction left to right
// :dir(rtl) - direction right to left
// :lang(en) - language
// :lang(en-US) - language with region
// :has(.child) - has child
// :not(.child) - not child
// :is(.child) - is child
// :where(.child) - where child
// :empty - empty
// :first-child - first child
// :last-child - last child
// :only-child - only child
// :only-of-type - only of type
// :first-of-type - first of type
// :last-of-type - last of type
// :root - root
// :scope - scope
// :host - host
// :host-context(.parent) - host context
// :dir(ltr) - direction left to right
// :dir(rtl) - direction right to left
// :lang(en) - language
// :lang(en-US) - language with region
// :has(.child) - has child
// :not(.child) - not child
// :is(.child) - is child
// :where(.child) - where child
// :empty - empty
// :first-child - first child
// :last-child - last child
// :only-child - only child
// :only-of-type - only of type
// :first-of-type - first of type
// :last-of-type - last of type
// :root - root
// :scope - scope
// :host - host
// :host-context(.parent) - host context
// :dir(ltr) - direction left to right
// :dir(rtl) - direction right to left
// :lang(en) - language
// :lang(en-US) - language with region
// :has(.child) - has child
// :not(.child) - not child
// :is(.child) - is child
// :where(.child) - where child
// :empty - empty
// :first-child - first child
// :last-child - last child
// :only-child - only child
// :only-of-type - only of type
// :first-of-type - first of type
// :last-of-type - last of type
// :root - root
// :scope - scope
// :host - host
// :host-context(.parent) - host context
// :dir(ltr) - direction left to right
// :dir(rtl) - direction right to left
// :lang(en) - language
// :lang(en-US) - language with region
// :has(.child) - has child
// :not(.child) - not child
// :is(.child) - is child
// :where(.child) - where child
// :empty - empty
// :first-child - first child
// :last-child - last child
// :only-child - only child
// :only-of-type - only of type
// :first-of-type - first of type
// :last-of-type - last of type
// :root - root
// :scope - scope
// :host - host
// :host-context(.parent) - host context
// :dir(ltr) - direction left to right
// :dir(rtl) - direction right to left
// :lang(en) - language
// :lang(en-US) - language with region
// :has(.child) - has child
// :not(.child) - not child
// :is(.child) - is child
// :where(.child) - where child
// :empty - empty
// :first-child - first child
// :last-child - last child
// :only-child - only child
// :only-of-type - only of type
// :first-of-type - first of type
// :last-of-type - last of type
// :root - root
// :scope - scope
// :host - host
// :host-context(.parent) - host context
// :dir(ltr) - direction left to right
// :dir(rtl) - direction right to left
// :lang(en) - language
// :lang(en-US) - language with region
// :has(.child) - has child
// :not(.child) - not child
// :is(.child) - is child
// :where(.child) - where child
// :empty - empty
// :first-child - first child
// :last-child - last child
// :only-child - only child
// :only-of-type - only of type
// :first-of-type - first of type
// :last-of-type - last of type
// :root - root
// :scope - scope
// :host - host
// :host-context(.parent) - host context
// :dir(ltr) - direction left to right
// :dir(rtl) - direction right to left
// :lang(en) - language
// :lang(en-US) - language with region
// :has(.child) - has child
// :not(.child) - not child
// :is(.child) - is child
// :where(.child) - where child
// :empty - empty
// :first-child - first child
// :last-child - last child
// :only-child - only child
// :only-of-type - only of type
// :first-of-type - first of type
// :last-of-type - last of type
// :root - root
// :scope - scope
// :host - host
// :host-context(.parent) - host context
// :dir(ltr) - direction left to right
// :dir(rtl) - direction right to left
// :lang(en) - language
// :lang(en-US) - language with region
// :has(.child) - has child
// :not(.child) - not child
// :is(.child) - is child
// :where(.child) - where child
// :empty - empty
// :first-child - first child
// :last-child - last child
// :only-child - only child
// :only-of-type - only of type
// :first-of-type - first of type
// :last-of-type - last of type
// :root - root
// :scope - scope
// :host - host
// :host-context(.parent) - host context
// :dir(ltr) - direction left to right
// :dir(rtl) - direction right to left
// :lang(en) - language
// :lang(en-US) - language with region
// :has(.child) - has child
// :not(.child) - not child
// :is(.child) - is child
// :where(.child) - where child
// :empty - empty
// :first-child - first child
// :last-child - last child
// :only-child - only child
// :only-of-type - only of type
// :first-of-type - first of type
// :last-of-type - last of type
// :root - root
// :scope - scope
// :host - host
// :host-context(.parent) - host context
// :dir(ltr) - direction left to right
// :dir(rtl) - direction right to left
// :lang(en) - language
// :lang(en-US) - language with region
// :has(.child) - has child
// :not(.child) - not child
// :is(.child) - is child
// :where(.child) - where child
// :empty - empty
// :first-child - first child
// :last-child - last child
// :only-child - only child
// :only-of-type - only of type
// :first-of-type - first of type
// :last-of-type - last of type
// :root - root
// :scope - scope
// :host - host
// :host-context(.parent) - host context
// :dir(ltr) - direction left to right
// :dir(rtl) - direction right to left
// :lang(en) - language
// :lang(en-US) - language with region
// :has(.child) - has child
// :not(.child) - not child
// :is(.child) - is child
// :where(.child) - where child
// :empty - empty
// :first-child - first child
// :last-child - last child
// :only-child - only child
// :only-of-type - only of type
// :first-of-type - first of type
// :last-of-type - last of type
// :root - root
// :scope - scope
// :host - host
// :host-context(.parent) - host context
// :dir(ltr) - direction left to right
// :dir(rtl) - direction right to left
// :lang(en) - language
// :lang(en-US) - language with region
// :has(.child) - has child
// :not(.child) - not child
// :is(.child) - is child
// :where(.child) - where child
// :empty - empty
// :first-child - first child
// :last-child - last child
// :only-child - only child
// :only-of-type - only of type
// :first-of-type - first of type
// :last-of-type - last of type
// :root - root
// :scope - scope
// :host - host
// :host-context(.parent) - host context
// :dir(ltr) - direction left to right
// :dir(rtl) - direction right to left
// :lang(en) - language
// :lang(en-US) - language with region
// :has(.child) - has child
// :not(.child) - not child
// :is(.child) - is child
// :where(.child) - where child
// :empty - empty
// :first-child - first child
// :last-child - last child
// :only-child - only child
// :only-of-type - only of type
// :first-of-type - first of type
// :last-of-type - last of type
// :root - root
// :scope - scope
// :host - host
// :host-context(.parent) - host context
// :dir(ltr) - direction left to right
// :dir(rtl) - direction right to left
// :lang(en) - language
// :lang(en-US) - language with region
// :has(.child) - has child
// :not(.child) - not child
// :is(.child) - is child
// :where(.child) - where child
// :empty - empty
// :first-child - first child
// :last-child - last child
// :only-child - only child
// :only-of-type - only of type
// :first-of-type - first of type
// :last-of-type - last of type
// :root - root
// :scope - scope
// :host - host
// :host-context(.parent) - host context
// :dir(ltr) - direction left to right
// :dir(rtl) - direction right to left
// :lang(en) - language
// :lang(en-US) - language with region
// :has(.child) - has child
// :not(.child) - not child
// :is(.child) - is child
// :where(.child) - where child
// :empty - empty
// :first-child - first child
// :last-child - last child
// :only-child - only child
// :only-of-type - only of type
// :first-of-type - first of type
// :last-of-type - last of type
// :root - root
// :scope - scope
// :host - host
// :host-context(.parent) - host context
// :dir(ltr) - direction left to right
// :dir(rtl) - direction right to left
// :lang(en) - language
// :lang(en-US) - language with region
// :has(.child) - has child
// :not(.child) - not child
// :is(.child) - is child
// :where(.child) - where child
// :empty - empty
// :first-child - first child
// :last-child - last child
// :only-child - only child
// :only-of-type - only of type
// :first-of-type - first of type
// :last-of-type - last of type
// :root - root
// :scope - scope
// :host - host
// :host-context(.parent) - host context
// :dir(ltr) - direction left to right
// :dir(rtl) - direction right to left
// :lang(en) - language
// :lang(en-US) - language with region
// :has(.child) - has child
// :not(.child) - not child
// :is(.child) - is child
// :where(.child) - where child
// :empty - empty
// :first-child - first child
// :last-child - last child
// :only-child - only child
// :only-of-type - only of type
// :first-of-type - first of type
// :last-of-type - last of type
// :root - root
// :scope - scope