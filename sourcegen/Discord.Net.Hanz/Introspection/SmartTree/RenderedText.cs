using System.Collections.Immutable;
using System.Text;

namespace Discord.Net.Hanz.Introspection;

public readonly struct RenderedText
{
    public static readonly RenderedText Empty = new(ImmutableArray<string>.Empty);

    public readonly ImmutableArray<string> Lines;

    public int Width { get; }

    public int Height => Lines.Length;

    public RenderedText(ImmutableArray<string> lines)
    {
        var width = Width = lines.Length == 0
            ? 0
            : lines.Max(x => x.Length);

        Lines = lines
            .Select(line =>
            {
                if (line.Length < width)
                {
                    var delta = width - line.Length;

                    var lPad = (int) Math.Floor(delta / 2d);
                    var rPad = (int) Math.Ceiling(delta / 2d);

                    return $"{string.Empty.Prefix(lPad)}{line}{string.Empty.Prefix(rPad)}";
                }

                return line;
            })
            .ToImmutableArray();
    }

    public RenderedText(IEnumerable<string> lines) : this(lines.ToImmutableArray())
    {
    }

    public override string ToString()
    {
        if (Lines.Length == 0) return string.Empty;

        var sb = new StringBuilder();

        for (int i = 0; i < Lines.Length; i++)
            sb.AppendLine(Lines[i]);

        return sb.ToString();
    }

    public RenderedText GrowWidth(int newWidth)
    {
        if (Width >= newWidth)
            return this;

        var lPad = (int) Math.Floor((newWidth - Width) / 2d);
        var rPad = (int) Math.Ceiling((newWidth - Width) / 2d);

        return new(
            Lines
                .Select(x => $"{string.Empty.PadLeft(lPad)}{x}{string.Empty.PadRight(rPad)}")
        );
    }

    public RenderedText PadX(int offset)
    {
        return new(Lines.Select(x =>
            offset < 0
                ? $"{string.Empty.Prefix(-offset)}{x}"
                : $"{x}{string.Empty.Prefix(offset)}"
        ));
    }

    public static RenderedText VertialBar(int width, int height)
    {
        var bar = new string(' ', width - 1).Insert((int) Math.Floor(width / 2d), "\u2502");

        return new(Enumerable.Repeat(bar, height));
    }

    public static RenderedText CreateJoinBar(
        int width,
        IEnumerable<int> lowerPoints,
        IEnumerable<int> upperPoints)
    {
        var bar = new StringBuilder(new string('\u2500', width));

        var lowerPointsArray = lowerPoints.ToArray();
        var upperPointsArray = upperPoints.ToArray();

        if (lowerPointsArray.Length == 0 && upperPointsArray.Length == 0)
            return new RenderedText(ImmutableArray.Create(bar.ToString()));

        foreach (var lowerPoint in lowerPointsArray)
        {
            bar[lowerPoint] = '\u252c';
        }

        foreach (var upperPoint in upperPointsArray)
        {
            bar[upperPoint] = '\u2534';
        }

        var lowerLeft = lowerPointsArray.Length == 0 ? int.MaxValue : lowerPointsArray[0];
        var upperLeft = upperPointsArray.Length == 0 ? int.MaxValue : upperPointsArray[0];

        var leftIndex = Math.Min(lowerLeft, upperLeft);

        var leftChar = lowerLeft == upperLeft
            ? '\u251c'
            : lowerLeft > upperLeft
                ? '\u2514'
                : '\u250c';

        bar.Remove(0, leftIndex + 1).Insert(0, $"{string.Empty.PadLeft(leftIndex)}{leftChar}");

        var lowerRight = lowerPointsArray.Length == 0
            ? int.MinValue
            : lowerPointsArray[lowerPointsArray.Length - 1];
        var upperRight = upperPointsArray.Length == 0
            ? int.MinValue
            : upperPointsArray[upperPointsArray.Length - 1];

        var rightIndex = Math.Max(lowerRight, upperRight);

        var rightChar = lowerRight == upperRight
            ? '\u2524'
            : lowerRight > upperRight
                ? '\u2510'
                : '\u2518';

        var count = bar.Length - rightIndex;

        bar.Remove(rightIndex, count).Insert(rightIndex, $"{rightChar}{string.Empty.PadLeft(count - 1)}");

        return new RenderedText(ImmutableArray.Create(bar.ToString()));
    }

    public static RenderedText Stack(RenderedText top, RenderedText bottom)
    {
        return new RenderedText(top.Lines.AddRange(bottom.Lines));
    }

    public static RenderedText JoinChildren(RenderedText parent, RenderedText[] children)
    {
        if (children.Length == 0)
            return parent;

        var alignedChildren = AlignHorizontally(children);

        var leftPoint = (int) Math.Floor(children[0].Width / 2d);
        var rightPoint =
            children
                .Take(children.Length - 1)
                .Sum(x => x.Width)
            + (int) Math.Floor(children[children.Length - 1].Width / 2d);

        var joinPoint = (rightPoint + leftPoint) / 2;

        var parentCenter = (int) Math.Floor(parent.Width / 2d);

        if (parentCenter < joinPoint)
        {
            parent = parent.PadX(parentCenter - joinPoint);
            alignedChildren = alignedChildren.PadX(joinPoint - parentCenter);
        }
        else if (parentCenter > joinPoint)
        {
            alignedChildren = alignedChildren.PadX(parentCenter - joinPoint);
            parent = parent.PadX(joinPoint - parentCenter);
        }

        var joinBar = CreateJoinBar(
            Math.Max(alignedChildren.Width, parent.Width),
            [joinPoint],
            children.Select((x, i) => children.Take(i).Sum(x => x.Width) + (int) Math.Floor(x.Width / 2d))
        );

        return new RenderedText(
            parent.Lines
                .Concat(joinBar.Lines)
                .Concat(alignedChildren.Lines)
            //.Prepend($"J: {Math.Max(alignedChildren.Width, parent.Width)} -> {joinBar.Width}")
        );
    }

    public static RenderedText JoinParents(
        RenderedText[] parents,
        RenderedText child)
    {
        if (parents.Length == 0)
            return child;

        if (parents.Length == 1)
            return Stack(parents[0], child);

        var alignedParents = AlignHorizontally(parents);

        var leftPoint = (int) Math.Floor(parents[0].Width / 2d);
        var rightPoint =
            parents
                .Take(parents.Length - 1)
                .Sum(x => x.Width)
            + (int) Math.Floor(parents[parents.Length - 1].Width / 2d);

        var joinPoint = (rightPoint + leftPoint) / 2;
        var parentsPointOffset = 0;

        var parentChildDelta = alignedParents.Width - child.Width;

        var childCenter = (int) Math.Floor(child.Width / 2d);
        var offset = childCenter - joinPoint;

        var joinBar = CreateJoinBar(
            alignedParents.Width,
            [joinPoint],
            parents.Select((x, i) =>
                parents
                    .Take(i)
                    .Sum(x => x.Width)
                + (int) Math.Floor(x.Width / 2d)
            )
        );

        if (childCenter < joinPoint)
        {
            child = child.PadX(offset);
            child = child.PadX(parentChildDelta - Math.Abs(offset));
        }
        else if (childCenter > joinPoint)
        {
            alignedParents = alignedParents.PadX(-offset - (parentChildDelta / 2));
            joinBar = joinBar.PadX(-offset - (parentChildDelta / 2));
        }

        return new RenderedText(
            alignedParents
                .Lines
                //.Append($"J: {alignedParents.Width}x{child.Width} -> {joinBar.Width}")
                //.Concat(parents.Select((x, i) => $"{i}) {x.Width}"))
                .Concat(joinBar.Lines)
                .Concat(child.Lines)
        );
    }

    public static RenderedText AlignHorizontally(RenderedText[] texts)
    {
        if (texts.Length == 0)
            return Empty;

        var maxHeight = texts.Max(x => x.Height);

        var lines = new List<string>();

        for (var y = 0; y < maxHeight; y++)
        {
            var section = new StringBuilder();

            for (var x = 0; x < texts.Length; x++)
            {
                var text = texts[x];

                var textYPos = text.Height - maxHeight + y;

                if (textYPos < 0)
                {
                    section.Append(string.Empty.PadLeft(text.Width));
                    continue;
                }

                section.Append(text.Lines[textYPos]);
            }

            lines.Add(section.ToString());
        }

        return new(lines);
    }
}