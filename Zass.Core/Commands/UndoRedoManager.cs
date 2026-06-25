namespace Zass.Core.Commands;

/// <summary>
/// Two-stack undo/redo history for one overlay session (Arquitectura §7.1).
/// Executing a fresh command clears the redo stack. The history is created empty
/// when the overlay opens and discarded when it closes.
/// </summary>
public sealed class UndoRedoManager
{
    private readonly Stack<IUndoableCommand> _undo = new();
    private readonly Stack<IUndoableCommand> _redo = new();

    /// <summary>Raised after any operation that may change <see cref="CanUndo"/>/<see cref="CanRedo"/>.</summary>
    public event EventHandler? StateChanged;

    public bool CanUndo => _undo.Count > 0;

    public bool CanRedo => _redo.Count > 0;

    /// <summary>Runs a new command, pushes it on the undo stack, and clears redo.</summary>
    public void Execute(IUndoableCommand command)
    {
        command.Execute();
        _undo.Push(command);
        _redo.Clear();
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Undoes the most recent command, if any. No-op when empty.</summary>
    public void Undo()
    {
        if (_undo.Count == 0)
        {
            return;
        }

        IUndoableCommand command = _undo.Pop();
        command.Undo();
        _redo.Push(command);
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Redoes the most recently undone command, if any. No-op when empty.</summary>
    public void Redo()
    {
        if (_redo.Count == 0)
        {
            return;
        }

        IUndoableCommand command = _redo.Pop();
        command.Execute();
        _undo.Push(command);
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Discards all history (e.g. when the overlay closes).</summary>
    public void Clear()
    {
        bool had = _undo.Count > 0 || _redo.Count > 0;
        _undo.Clear();
        _redo.Clear();
        if (had)
        {
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
