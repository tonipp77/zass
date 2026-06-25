namespace Zass.Core.Annotations;

/// <summary>
/// In-memory <see cref="IAnnotationLayer"/> backed by a list. Pure model state,
/// no UI; the presentation layer mirrors it onto the WPF canvas.
/// </summary>
public sealed class AnnotationLayer : IAnnotationLayer
{
    private readonly List<Annotation> _items = new();

    public IReadOnlyList<Annotation> Items => _items;

    public void Add(Annotation annotation) => _items.Add(annotation);

    public void Insert(int index, Annotation annotation)
    {
        int clamped = Math.Clamp(index, 0, _items.Count);
        _items.Insert(clamped, annotation);
    }

    public bool Remove(Annotation annotation) => _items.Remove(annotation);

    public int IndexOf(Annotation annotation) => _items.IndexOf(annotation);

    /// <summary>
    /// Returns the topmost annotation hit by <paramref name="point"/>, or null.
    /// Walks from top (last) to bottom so the visually frontmost wins (§6.2).
    /// </summary>
    public Annotation? HitTest(PhysicalPoint point, double tolerance = Annotation.DefaultHitTolerance)
    {
        for (int i = _items.Count - 1; i >= 0; i--)
        {
            if (_items[i].HitTest(point, tolerance))
            {
                return _items[i];
            }
        }

        return null;
    }
}
