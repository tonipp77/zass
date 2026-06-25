using Zass.Core.Annotations;

namespace Zass.Core.Commands;

/// <summary>Adds an annotation to the layer; undo removes it.</summary>
public sealed class AddAnnotationCommand : IUndoableCommand
{
    private readonly IAnnotationLayer _layer;
    private readonly Annotation _annotation;

    public AddAnnotationCommand(IAnnotationLayer layer, Annotation annotation)
    {
        _layer = layer;
        _annotation = annotation;
    }

    public void Execute() => _layer.Add(_annotation);

    public void Undo() => _layer.Remove(_annotation);
}
