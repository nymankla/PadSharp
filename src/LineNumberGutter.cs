using System.Globalization;
using Avalonia;
using Avalonia.Media;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Rendering;

namespace PadSharp;

/// <summary>The margin left of the text that shows line numbers; the caret's line is lit in the accent colour.</summary>
internal sealed class LineNumberGutter : AbstractMargin
{
    private const double PaddingLeft = 14;
    private const double PaddingRight = 14;
    private const int MinDigits = 2;

    private double fontSize = Look.PtToDip(Look.DefaultFontSizePt);

    /// <summary>The font numbers are drawn in; matches the editor's.</summary>
    public FontFamily NumberFont
    {
        get;
        set
        {
            field = value;
            InvalidateMeasure();
            InvalidateVisual();
        }
    } = Look.EditorFontFor(Look.DefaultFontName);

    /// <summary>The 1-based line holding the caret.</summary>
    public int CurrentLine
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                InvalidateVisual();
            }
        }
    } = 1;

    public double NumberFontSize
    {
        get => fontSize;
        set
        {
            fontSize = value;
            InvalidateMeasure();
            InvalidateVisual();
        }
    }

    public override void Render(DrawingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.FillRectangle(Look.GutterBack, new Rect(Bounds.Size));
        TextView? view = TextView;
        if (view is null || !view.VisualLinesValid)
        {
            return;
        }

        var typeface = new Typeface(NumberFont);
        foreach (VisualLine line in view.VisualLines)
        {
            int lineNumber = line.FirstDocumentLine.LineNumber;
            string number = lineNumber.ToString(CultureInfo.InvariantCulture);
            var text = new FormattedText(number, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                typeface, fontSize, lineNumber == CurrentLine ? Look.GutterActiveFore : Look.GutterFore);
            double y = line.GetTextLineVisualYPosition(line.TextLines[0], VisualYPosition.TextTop) - view.VerticalOffset;
            context.DrawText(text, new Point(Bounds.Width - PaddingRight - text.Width, y));
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        int lines = Document?.LineCount ?? 1;
        int digits = Math.Max(MinDigits, lines.ToString(CultureInfo.InvariantCulture).Length);
        var sample = new FormattedText(new string('9', digits), CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            new Typeface(NumberFont), fontSize, Look.GutterFore);
        return new Size(sample.Width + PaddingLeft + PaddingRight, 0);
    }

    protected override void OnTextViewChanged(TextView? oldTextView, TextView? newTextView)
    {
        if (oldTextView is not null)
        {
            oldTextView.VisualLinesChanged -= OnVisualLinesChanged;
        }

        base.OnTextViewChanged(oldTextView, newTextView);
        if (newTextView is not null)
        {
            newTextView.VisualLinesChanged += OnVisualLinesChanged;
        }

        InvalidateVisual();
    }

    protected override void OnDocumentChanged(AvaloniaEdit.Document.TextDocument? oldDocument, AvaloniaEdit.Document.TextDocument? newDocument)
    {
        if (oldDocument is not null)
        {
            oldDocument.LineCountChanged -= OnLineCountChanged;
        }

        base.OnDocumentChanged(oldDocument, newDocument);
        if (newDocument is not null)
        {
            newDocument.LineCountChanged += OnLineCountChanged;
        }

        InvalidateMeasure();
    }

    private void OnVisualLinesChanged(object? sender, EventArgs e) => InvalidateVisual();

    private void OnLineCountChanged(object? sender, EventArgs e) => InvalidateMeasure();
}
