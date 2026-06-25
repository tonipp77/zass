namespace Zass.Core.Commands;

/// <summary>
/// A reversible mutation of the annotation layer (Arquitectura §7). Each canvas
/// mutation is one command so that undo/redo operates at the right granularity:
/// a whole freehand stroke or a whole drag is a single command, never per point.
/// </summary>
public interface IUndoableCommand
{
    /// <summary>Applies the change. Also used to redo.</summary>
    void Execute();

    /// <summary>Reverts the change.</summary>
    void Undo();
}
