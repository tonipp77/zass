using Zass.Core.Commands;
using Zass.Core.Annotations;
using Zass.Interop;

namespace Zass.Core.Collage;

/// <summary>A crop at its original resolution, positioned in physical pixels.</summary>
public enum CollageDecorationKind { CurvedArrow, StraightArrow, ElbowArrow, Text, Stamp, Step, Label }

/// <summary>Immutable geometry relative to the item's bounds; size is in physical pixels.</summary>
public enum CollageStamp { Check, Cross, Prohibited, Plus, Minus, Exclamation, Question }
public enum CollageBadgeShape { Circle, Teardrop }
public enum CollageLabelShape { Box, Speech }
public enum CollageArrowTip { Filled, Open, Double }

public sealed record CollageDecoration(CollageDecorationKind Kind, PhysicalPoint From,
    PhysicalPoint To, ArgbColor Color, double Size, string Text = "",
    bool HasShadow = false, double Opacity = 1, string FontFamily = "Segoe UI",
    bool Bold = false, bool Italic = false, bool TextOutline = false,
    CollageStamp Stamp = CollageStamp.Check, CollageBadgeShape BadgeShape = CollageBadgeShape.Circle,
    CollageLabelShape LabelShape = CollageLabelShape.Box,
    CollageArrowTip ArrowTip = CollageArrowTip.Filled, bool Dashed = false);

public sealed record CollageItem(Guid Id, PhysicalRect Bounds, CollageDecoration? Decoration = null);

/// <summary>Free-layout collage geometry and history, independent of bitmap and UI APIs.</summary>
public sealed class CollageDocument
{
    public const long MaxPixels = 64_000_000;
    public const int MaxDimension = 32_767;
    private CollageItem[] _items = [];
    private readonly UndoRedoManager _history = new();

    public IReadOnlyList<CollageItem> Items => Array.AsReadOnly(_items);

    /// <summary>Crops below decorations, with stable insertion order within each group.</summary>
    public IEnumerable<CollageItem> ItemsInPaintOrder => _items.OrderBy(item => item.Decoration is null ? 0 : 1);
    public bool CanUndo => _history.CanUndo;
    public bool CanRedo => _history.CanRedo;
    public PhysicalRect Bounds => GetBounds(_items);

    public Guid Add(int width, int height)
    {
        if (width <= 0 || height <= 0)
            throw new ArgumentOutOfRangeException(nameof(width));

        // Start below existing crops; users can then place the crop anywhere.
        int top = _items.Length == 0 ? 0 : Bounds.Bottom;
        var item = new CollageItem(Guid.NewGuid(), new PhysicalRect(0, top, width, height));
        Change([.. _items, item]);
        return item.Id;
    }

    public Guid AddDecoration(CollageDecoration decoration, PhysicalRect bounds)
    {
        var item = new CollageItem(Guid.NewGuid(), bounds, decoration);
        Change([.. _items, item]);
        return item.Id;
    }

    public bool CanMove(Guid id, int x, int y)
    {
        if (x < 0 || y < 0 || !_items.Any(i => i.Id == id)) return false;
        return IsValid(_items.Select(i => i.Id == id ? i with { Bounds = i.Bounds with { X = x, Y = y } } : i));
    }

    public void Move(Guid id, int x, int y)
    {
        if (!CanMove(id, x, y)) throw new ArgumentOutOfRangeException(nameof(x));
        Change(_items.Select(i => i.Id == id ? i with { Bounds = i.Bounds with { X = x, Y = y } } : i).ToArray());
    }

    public void Remove(Guid id) => Change(_items.Where(i => i.Id != id).ToArray());
    public void Clear() => Change([]);
    public void Undo() => _history.Undo();
    public void Redo() => _history.Redo();

    private void Change(CollageItem[] next)
    {
        if (!IsValid(next)) throw new ArgumentOutOfRangeException(nameof(next));
        if (_items.SequenceEqual(next)) return;
        _history.Execute(new LayoutCommand(this, _items, next));
    }

    private static bool IsValid(IEnumerable<CollageItem> items)
    {
        CollageItem[] snapshot = items.ToArray();
        if (snapshot.Any(i => i.Bounds.X < 0 || i.Bounds.Y < 0 || i.Bounds.Width <= 0 || i.Bounds.Height <= 0 ||
            (long)i.Bounds.X + i.Bounds.Width > MaxDimension || (long)i.Bounds.Y + i.Bounds.Height > MaxDimension)) return false;
        PhysicalRect bounds = GetBounds(snapshot);
        return (long)bounds.Width * bounds.Height <= MaxPixels;
    }

    private static PhysicalRect GetBounds(IReadOnlyList<CollageItem> items)
    {
        if (items.Count == 0) return default;
        int left = items.Min(i => i.Bounds.X);
        int top = items.Min(i => i.Bounds.Y);
        return new PhysicalRect(left, top, items.Max(i => i.Bounds.Right) - left, items.Max(i => i.Bounds.Bottom) - top);
    }

    private sealed class LayoutCommand(CollageDocument document, CollageItem[] before, CollageItem[] after) : IUndoableCommand
    {
        public void Execute() => document._items = after;
        public void Undo() => document._items = before;
    }
}
