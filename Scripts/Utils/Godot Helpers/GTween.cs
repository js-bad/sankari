namespace Sankari;

public class GTween 
{
    private Tween Tween { get; }
    private Node Target { get; }

    public GTween(Node target)
    {
        this.Target = target;
        Tween = this.Target.GetTree().CreateTween();
        Tween.Stop();
    }

	public void Callback(Action action) => Tween.TweenCallback(action);

    /// <summary>
    /// Hover over the property in the editor to get the string value of that property.
    /// </summary>
    public void InterpolateProperty
    (
        NodePath property, 
        Variant finalValue, 
        float duration
    ) => Tween.TweenProperty(Target, property, finalValue, duration);

    public async Task AnimatePlatform
    (
        Vector2 initialValue, 
        Vector2 finalValue, 
        float width,
        float duration,
        int startDelay,
        Tween.TransitionType transType = Tween.TransitionType.Cubic,
        Tween.EaseType easeType = Tween.EaseType.InOut
    ) 
    {
        // tween.Repeat = true; // TODO: Godot 4 conversion
        InterpolateProperty("position", finalValue, duration);
        InterpolateProperty("position", initialValue, duration);
        await Task.Delay(startDelay * 1000);
        Start();
    }

    //public void IsActive() => tween.IsActive(); // TODO: Godot 4 conversion
    public void Start() => Tween.Play();
    public void Pause() => Tween.Stop();
}
