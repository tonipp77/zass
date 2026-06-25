namespace Zass.Core.Annotations;

/// <summary>
/// The mutable collection of annotations for one overlay session. Commands
/// operate against this abstraction so the undo/redo core stays decoupled from
/// the WPF canvas, which will adapt to it in a later increment.
/// </summary>
public interface IAnnotationLayer
{
    /// <summary>Current annotations in paint order (index 0 = bottom).</summary>
    IReadOnlyList<Annotation> Items { get; }

    /// <summary>Appends an annotation on top.</summary>
    void Add(Annotation annotation);

    /// <summary>Inserts an annotation at a specific index (used to restore z-order on undo).</summary>
    void Insert(int index, Annotation annotation);

    /// <summary>Removes an annotation. Returns true if it was present.</summary>
    bool Remove(Annotation annotation);

    /// <summary>Index of the annotation, or -1 if absent.</summary>
    int IndexOf(Annotation annotation);
}
