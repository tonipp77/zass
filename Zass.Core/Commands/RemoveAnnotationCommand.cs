using Zass.Core.Annotations;

namespace Zass.Core.Commands;

/// <summary>
/// Removes an annotation from the layer; undo re-inserts it at its original index
/// so the z-order is preserved.
/// </summary>
public sealed class RemoveAnnotationCommand : IUndoableCommand
{
    private readonly IAnnotationLayer _layer;
    private readonly Annotation _annotation;
    private int _index = -1;

    public RemoveAnnotationCommand(IAnnotationLayer layer, Annotation annotation)
    {
        _layer = layer;
        _annotation = annotation;
    }

    public void Execute()
    {
        _index = _layer.IndexOf(_annotation);
        _layer.Remove(_annotation);
    }

    public void Undo()
    {
        if (_index >= 0)
        {
            _layer.Insert(_index, _annotation);
        }
        else
        {
            _layer.Add(_annotation);
        }
    }
}
