# Avalonia 12.0.3 Animation Reference

## 1. Animatable

```csharp
// Location: src/Avalonia.Base/Animation/
public class Animatable : AvaloniaObject
{
    // Start animation
    public void BeginAnimation(AvaloniaProperty property, IAnimation animation);
    public void BeginAnimation<T>(StyledProperty<T> property, IAnimation animation);
    public void BeginAnimation<T>(DirectPropertyBase<T> property, IAnimation animation);
    
    // Stop animation
    public void StopAnimation(AvaloniaProperty property);
    public void StopAnimations();
    
    // Check animation state
    public bool IsAnimating(AvaloniaProperty property);
}
```

## 2. IAnimation

```csharp
// Location: src/Avalonia.Base/Animation/
public interface IAnimation
{
    Type TargetPropertyType { get; }
    double Duration { get; }
    IEasing? Easing { get; }
    FillMode FillMode { get; }
    double PlaybackRate { get; }
    int IterationCount { get; }
    
    IAnimationInstance CreateInstance();
    bool CanAnimate(IAnimationInstance instance);
}
```

## 3. Animation

```csharp
// Location: src/Avalonia.Base/Animation/Animation.cs
public class Animation : IAnimation
{
    public Type TargetPropertyType { get; }
    public double Duration { get; set; }
    public IEasing? Easing { get; set; }
    public FillMode FillMode { get; set; }
    public double PlaybackRate { get; set; }
    public int IterationCount { get; set; }
    
    public IList<KeyFrame> KeyFrames { get; }
    public IAnimationInstance CreateInstance();
    public bool CanAnimate(IAnimationInstance instance);
}
```

## 4. AnimationInstance

```csharp
// Location: src/Avalonia.Base/Animation/
public abstract class IAnimationInstance
{
    public abstract void Start();
    public abstract void Stop();
    public abstract void Pause();
    public abstract void Resume();
    public abstract void Seek(double offset);
    public abstract void Skip(double offset);
    public abstract void Restart();
    public abstract void SetPlaybackRate(double rate);
    public abstract void SetFillMode(FillMode mode);
    public abstract void SetIterationCount(int count);
    public abstract void SetDuration(double duration);
    public abstract void SetEasing(IEasing easing);
    public abstract void SetTargetProperty(AvaloniaProperty property);
    public abstract void SetTargetObject(AvaloniaObject target);
}
```

## 5. KeyFrame

```csharp
// Location: src/Avalonia.Base/Animation/
public abstract class KeyFrame
{
    public double Offset { get; set; }
    public abstract object? Value { get; }
}

public class DoubleKeyFrame : KeyFrame
{
    public double Value { get; set; }
    public override object? Value => Value;
}

public class ColorKeyFrame : KeyFrame
{
    public Color Value { get; set; }
    public override object? Value => Value;
}

public class ObjectKeyFrame : KeyFrame
{
    public object? Value { get; set; }
    public override object? Value => Value;
}
```

## 6. IEasing

```csharp
// Location: src/Avalonia.Base/Animation/
public interface IEasing
{
    double Easing(double x);
}

public abstract class Easing : IEasing
{
    public abstract double Easing(double x);
}
```

## 7. Easing Functions

```csharp
// Location: src/Avalonia.Base/Animation/Easing/
// Linear easing
public class LinearEasing : Easing { public override double Easing(double x) => x; }

// Cubic easing
public class CubicEaseIn : Easing { public override double Easing(double x) => x * x * x; }
public class CubicEaseOut : Easing { public override double Easing(double x) => (--x) * x * x + 1; }
public class CubicEaseInOut : Easing { public override double Easing(double x) => x < 0.5 ? 4 * x * x * x : 1 - Math.Pow(-2 * x + 2, 3) / 2; }

// Bounce easing
public class BounceEaseIn : Easing { public override double Easing(double x) => x * x; }
public class BounceEaseOut : Easing { public override double Easing(double x) => 1 - Math.Pow(1 - x, 3); }
public class BounceEaseInOut : Easing { public override double Easing(double x) => x < 0.5 ? 4 * x * x * x : 1 - Math.Pow(-2 * x + 2, 3) / 2; }

// Circular easing
public class CircularEaseIn : Easing { public override double Easing(double x) => 1 - Math.Sqrt(1 - x * x); }
public class CircularEaseOut : Easing { public override double Easing(double x) => Math.Sqrt(x * (2 - x)); }
public class CircularEaseInOut : Easing { public override double Easing(double x) => x < 0.5 ? (1 - Math.Sqrt(1 - 4 * x * x)) / 2 : (Math.Sqrt(-2 * x * x + 4 * x) + 1) / 2; }

// Elastic easing
public class ElasticEaseIn : Easing { public override double Easing(double x) => Math.Pow(2, 10 * (x - 1)) * Math.Cos(20 * Math.PI / 3 * (x - 1)); }
public class ElasticEaseOut : Easing { public override double Easing(double x) => Math.Pow(2, -10 * x) * Math.Cos(20 * Math.PI / 3 * x) + 1; }
public class ElasticEaseInOut : Easing { public override double Easing(double x) => x < 0.5 ? Math.Pow(2, 20 * x - 10) * Math.Cos(20 * Math.PI / 3 * (x - 0.5)) / 2 : Math.Pow(2, -20 * x + 10) * Math.Cos(20 * Math.PI / 3 * (x - 0.5)) / 2 + 1; }

// Back easing
public class BackEaseIn : Easing { public override double Easing(double x) => (x - 1) * (x - 1) * ((2.70158 + 1) * (x - 1) + 2.70158) + 1; }
public class BackEaseOut : Easing { public override double Easing(double x) => x * x * ((2.70158 + 1) * x - 2.70158) + 1; }
public class BackEaseInOut : Easing { public override double Easing(double x) => x < 0.5 ? Math.Pow(2, 20 * x - 10) * Math.Cos(20 * Math.PI / 3 * (x - 0.5)) / 2 : Math.Pow(2, -20 * x + 10) * Math.Cos(20 * Math.PI / 3 * (x - 0.5)) / 2 + 1; }

// Exponential easing
public class ExponentialEaseIn : Easing { public override double Easing(double x) => x == 0 ? 0 : Math.Pow(2, 10 * (x - 1)); }
public class ExponentialEaseOut : Easing { public override double Easing(double x) => x == 1 ? 1 : 1 - Math.Pow(2, -10 * x); }
public class ExponentialEaseInOut : Easing { public override double Easing(double x) => x == 0 ? 0 : x == 1 ? 1 : x < 0.5 ? Math.Pow(2, 20 * x - 10) / 2 : (2 - Math.Pow(2, -20 * x + 10)) / 2; }

// Spring easing
public class SpringEasing : Easing { public override double Easing(double x) => Math.Pow(2, -10 * x) * Math.Sin(40 * Math.PI * x / 3) + 1; }

// KeySpline easing (for Bezier curves)
public class KeySplineEasing : Easing
{
    public Point Control1 { get; set; }
    public Point Control2 { get; set; }
    public override double Easing(double x);
}
```

## 8. FillMode

```csharp
// Location: src/Avalonia.Base/Animation/
public enum FillMode
{
    None,      // Remove values when animation ends
    Forward,   // Apply final values when animation ends
    Backward,  // Apply initial values when animation ends
    Both,      // Apply both initial and final values
}
```

## 9. Transition

```csharp
// Location: src/Avalonia.Base/Transitions/
public class Transition
{
    public double Duration { get; set; }
    public IEasing? Easing { get; set; }
    public AvaloniaProperty Property { get; set; }
    public object? From { get; set; }
    public object? To { get; set; }
}

public class TransitionInstance
{
    public double Duration { get; }
    public IEasing? Easing { get; }
    public bool IsRunning { get; }
    
    public void Start();
    public void Stop();
    public void Pause();
    public void Resume();
}

public class Transitions : List<Transition>
{
    public void Add(Transition transition);
    public void AddRange(IEnumerable<Transition> transitions);
    public void Remove(Transition transition);
    public void Clear();
}
```

## 10. IPageTransition

```csharp
// Location: src/Avalonia.Base/Transitions/
public interface IPageTransition
{
    TimeSpan Duration { get; }
    IEasing? Easing { get; }
    
    void Transition(UIElement from, UIElement to, Action<Rect> arrange);
    void Cancel();
}
```

## 11. Animator

```csharp
// Location: src/Avalonia.Base/Animation/
public interface IAnimator
{
    void Start(IAnimationInstance instance);
    void Stop(IAnimationInstance instance);
    void Pause(IAnimationInstance instance);
    void Resume(IAnimationInstance instance);
    void Seek(IAnimationInstance instance, double offset);
    void Skip(IAnimationInstance instance, double offset);
    void Restart(IAnimationInstance instance);
    void SetPlaybackRate(IAnimationInstance instance, double rate);
    void SetFillMode(IAnimationInstance instance, FillMode mode);
    void SetIterationCount(IAnimationInstance instance, int count);
    void SetDuration(IAnimationInstance instance, double duration);
    void SetEasing(IAnimationInstance instance, IEasing easing);
}

public class AnimatorDrivenTransition : IPageTransition
{
    public TimeSpan Duration { get; }
    public IEasing? Easing { get; }
    
    public void Transition(UIElement from, UIElement to, Action<Rect> arrange);
    public void Cancel();
}

public class AnimatorTransitionObservable : IObservable<IAnimationInstance>
{
    public IDisposable Subscribe(IObserver<IAnimationInstance> observer);
}
```

## 12. CompositePageTransition

```csharp
// Location: src/Avalonia.Base/Transitions/
public class CompositePageTransition : IPageTransition
{
    public IList<IPageTransition> Transitions { get; }
    public TimeSpan Duration { get; }
    public IEasing? Easing { get; }
    
    public void Add(IPageTransition transition);
    public void Remove(IPageTransition transition);
    public void Clear();
    public void Transition(UIElement from, UIElement to, Action<Rect> arrange);
    public void Cancel();
}
```

## 13. CrossFade

```csharp
// Location: src/Avalonia.Base/Transitions/
public class CrossFade : IPageTransition
{
    public TimeSpan Duration { get; }
    public IEasing? Easing { get; }
    
    public void Transition(UIElement from, UIElement to, Action<Rect> arrange);
    public void Cancel();
}
```

## 14. PageSlide

```csharp
// Location: src/Avalonia.Base/Transitions/
public class PageSlide : IPageTransition
{
    public TimeSpan Duration { get; }
    public IEasing? Easing { get; }
    public Vector Direction { get; set; }
    
    public void Transition(UIElement from, UIElement to, Action<Rect> arrange);
    public void Cancel();
}
```

## 15. PageTransitionItem

```csharp
// Location: src/Avalonia.Controls/PageTransitionItem.cs
public class PageTransitionItem : ContentControl
{
    public IPageTransition? Transition { get; set; }
    public TimeSpan TransitionDuration { get; set; }
    public int OpenedTransitionCount { get; }
    public int ClosedTransitionCount { get; }
    
    protected override void OnApplyTemplate(ApplyTemplateEventArgs e);
}
```

## 16. ConnectedAnimation

```csharp
// Location: src/Avalonia.Animation/
public class ConnectedAnimation : IPageTransition
{
    public string? Id { get; set; }
    public TimeSpan Duration { get; }
    public IEasing? Easing { get; }
    public object? Content { get; set; }
    public DataTemplate? ContentTemplate { get; set; }
    
    public void Transition(UIElement from, UIElement to, Action<Rect> arrange);
    public void Cancel();
}

public class ConnectedAnimationConfiguration
{
    public static ConnectedAnimationConfiguration Default { get; }
    public TimeSpan Duration { get; set; }
    public IEasing? Easing { get; set; }
    public bool IsLooping { get; set; }
}

public class ConnectedAnimationService
{
    public static ConnectedAnimation Get(string id);
    public static void Place(string id, ConnectedAnimation animation);
    public static void Remove(string id);
    public static bool TryGet(string id, out ConnectedAnimation? animation);
}
```

## 17. Clock

```csharp
// Location: src/Avalonia.Base/Animation/
public class Clock : IClock
{
    public DateTime StartTime { get; }
    public TimeSpan Elapsed { get; }
    public bool IsPaused { get; }
    public double Rate { get; }
    
    public void Start();
    public void Pause();
    public void Resume();
    public void Seek(TimeSpan offset);
    public void Skip(TimeSpan offset);
    public void Restart();
    public void SetRate(double rate);
}

public abstract class ClockBase : Clock
{
    protected virtual void OnClockChanged();
}

public interface IClock
{
    DateTime StartTime { get; }
    TimeSpan Elapsed { get; }
    bool IsPaused { get; }
    double Rate { get; }
    
    void Start();
    void Pause();
    void Resume();
    void Seek(TimeSpan offset);
    void Skip(TimeSpan offset);
    void Restart();
    void SetRate(double rate);
}

public interface IGlobalClock
{
    DateTime StartTime { get; }
    TimeSpan Elapsed { get; }
    bool IsPaused { get; }
    double Rate { get; }
    
    void Start();
    void Pause();
    void Resume();
    void Seek(TimeSpan offset);
    void Skip(TimeSpan offset);
    void Restart();
    void SetRate(double rate);
}
```

## 18. Custom Animator

```csharp
// Location: src/Avalonia.Base/Animation/
public interface ICustomAnimator
{
    void Start(IAnimationInstance instance);
    void Stop(IAnimationInstance instance);
    void Pause(IAnimationInstance instance);
    void Resume(IAnimationInstance instance);
    void Seek(IAnimationInstance instance, double offset);
    void Skip(IAnimationInstance instance, double offset);
    void Restart(IAnimationInstance instance);
    void SetPlaybackRate(IAnimationInstance instance, double rate);
    void SetFillMode(IAnimationInstance instance, FillMode mode);
    void SetIterationCount(IAnimationInstance instance, int count);
    void SetDuration(IAnimationInstance instance, double duration);
    void SetEasing(IAnimationInstance instance, IEasing easing);
}

public class Spring
{
    public double Stiffness { get; set; }
    public double Damping { get; set; }
    public double Mass { get; set; }
    public double InitialVelocity { get; set; }
}

public class Cue
{
    public double Offset { get; set; }
    public object? Value { get; set; }
}

public class DisposeAnimationInstanceSubject : IDisposable
{
    public void Dispose();
}
```

## 19. Easings

```csharp
// Location: src/Avalonia.Base/Animation/Easing/
// All easing functions:
// - LinearEasing
// - CubicEaseIn
// - CubicEaseOut
// - CubicEaseInOut
// - BounceEaseIn
// - BounceEaseOut
// - BounceEaseInOut
// - CircularEaseIn
// - CircularEaseOut
// - CircularEaseInOut
// - ElasticEaseIn
// - ElasticEaseOut
// - ElasticEaseInOut
// - BackEaseIn
// - BackEaseOut
// - BackEaseInOut
// - ExponentialEaseIn
// - ExponentialEaseOut
// - ExponentialEaseInOut
// - SpringEasing
// - KeySplineEasing
```

## 20. Transitions

```csharp
// Location: src/Avalonia.Base/Transitions/
// All transition implementations:
// - CrossFade
// - PageSlide
// - AnimatorDrivenTransition
// - CompositePageTransition
// - ConnectedAnimation
// - PageTransitionItem
```

## 21. Key Files Reference

| File | Purpose |
|------|---------|
| `src/Avalonia.Base/Animation/Animatable.cs` | Animatable base |
| `src/Avalonia.Base/Animation/IAnimation.cs` | IAnimation interface |
| `src/Avalonia.Base/Animation/Animation.cs` | Animation class |
| `src/Avalonia.Base/Animation/IAnimationInstance.cs` | IAnimationInstance |
| `src/Avalonia.Base/Animation/KeyFrame.cs` | KeyFrame base |
| `src/Avalonia.Base/Animation/DoubleKeyFrame.cs` | DoubleKeyFrame |
| `src/Avalonia.Base/Animation/ColorKeyFrame.cs` | ColorKeyFrame |
| `src/Avalonia.Base/Animation/ObjectKeyFrame.cs` | ObjectKeyFrame |
| `src/Avalonia.Base/Animation/IEasing.cs` | IEasing interface |
| `src/Avalonia.Base/Animation/Easing/` | All easing functions |
| `src/Avalonia.Base/Animation/FillMode.cs` | FillMode enum |
| `src/Avalonia.Base/Animation/PlaybackDirection.cs` | PlaybackDirection |
| `src/Avalonia.Base/Animation/PlayState.cs` | PlayState |
| `src/Avalonia.Base/Animation/IterationCount.cs` | IterationCount |
| `src/Avalonia.Base/Transitions/Transition.cs` | Transition |
| `src/Avalonia.Base/Transitions/TransitionInstance.cs` | TransitionInstance |
| `src/Avalonia.Base/Transitions/Transitions.cs` | Transitions |
| `src/Avalonia.Base/Transitions/IPageTransition.cs` | IPageTransition |
| `src/Avalonia.Base/Transitions/CrossFade.cs` | CrossFade |
| `src/Avalonia.Base/Transitions/PageSlide.cs` | PageSlide |
| `src/Avalonia.Base/Transitions/CompositePageTransition.cs` | CompositePageTransition |
| `src/Avalonia.Base/Animation/IAnimator.cs` | IAnimator |
| `src/Avalonia.Base/Animation/AnimatorDrivenTransition.cs` | AnimatorDrivenTransition |
| `src/Avalonia.Base/Animation/AnimatorTransitionObservable.cs` | AnimatorTransitionObservable |
| `src/Avalonia.Base/Animation/Clock.cs` | Clock |
| `src/Avalonia.Base/Animation/ClockBase.cs` | ClockBase |
| `src/Avalonia.Base/Animation/IClock.cs` | IClock |
| `src/Avalonia.Base/Animation/IGlobalClock.cs` | IGlobalClock |
| `src/Avalonia.Base/Animation/ICustomAnimator.cs` | ICustomAnimator |
| `src/Avalonia.Base/Animation/Spring.cs` | Spring |
| `src/Avalonia.Base/Animation/Cue.cs` | Cue |
| `src/Avalonia.Base/Animation/DisposeAnimationInstanceSubject.cs` | DisposeAnimationInstanceSubject |
| `src/Avalonia.Animation/ConnectedAnimation.cs` | ConnectedAnimation |
| `src/Avalonia.Animation/ConnectedAnimationConfiguration.cs` | ConnectedAnimationConfiguration |
| `src/Avalonia.Animation/ConnectedAnimationService.cs` | ConnectedAnimationService |