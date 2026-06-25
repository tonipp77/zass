using Zass.Core.Annotations;

namespace Zass.Core.Commands;

/// <summary>
/// Moves an annotation by a total delta. Per Arquitectura §7.2 a drag produces a
/// single command built from the net displacement, not one per intermediate step.
/// </summary>
public sealed class MoveAnnotationCommand : IUndoableCommand
{
    private readonly Annotation _annotation;
    private readonly double _dx;
    private readonly double _dy;

    public MoveAnnotationCommand(Annotation annotation, double dx, double dy)
    {
        _annotation = annotation;
        _dx = dx;
        _dy = dy;
    }

    public void Execute() => _annotation.Move(_dx, _dy);

    public void Undo() => _annotation.Move(-_dx, -_dy);
}
