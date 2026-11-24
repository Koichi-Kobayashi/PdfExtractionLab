using System.Text;
using DiffPlex;

namespace PdfExtractionLab.Core;

public enum ComparisonMode
{
    完全一致,
    改行コードを無視,
    横空白を無視,
    NFC,
}

public record Difference(
    string Kind,
    int LeftOffset,
    int RightOffset,
    string Removed,
    string Added
);

public static class TextComparison
{
    private sealed class Scalars : IChunker
    {
        public IReadOnlyList<string> Chunk(string text)
        {
            var units = new List<string>();
            for (int offset = 0; offset < text.Length; )
            {
                int length = char.IsSurrogatePair(text, offset) ? 2 : 1;
                units.Add(text.Substring(offset, length));
                offset += length;
            }
            return units;
        }
    }

    public static string Prepare(string s, ComparisonMode mode) =>
        mode switch
        {
            ComparisonMode.改行コードを無視 => s.ReplaceLineEndings("\n"),
            ComparisonMode.横空白を無視 => string.Concat(
                s.Where(c =>
                    c != '\t'
                    && char.GetUnicodeCategory(c)
                        != System.Globalization.UnicodeCategory.SpaceSeparator
                )
            ),
            ComparisonMode.NFC => s.Normalize(NormalizationForm.FormC),
            _ => s,
        };

    public static Difference[] Compare(string left, string right, ComparisonMode mode)
    {
        left = Prepare(left, mode);
        right = Prepare(right, mode);
        if (left == right)
            return [];
        var diff = new Differ().CreateDiffs(left, right, false, false, new Scalars());
        return diff
            .DiffBlocks.Select(b => new Difference(
                b.DeleteCountA == 0 ? "追加"
                    : b.InsertCountB == 0 ? "欠落"
                    : "置換",
                b.DeleteStartA,
                b.InsertStartB,
                Visible(diff.PiecesOld.Skip(b.DeleteStartA).Take(b.DeleteCountA)),
                Visible(diff.PiecesNew.Skip(b.InsertStartB).Take(b.InsertCountB))
            ))
            .ToArray();
    }

    private static string Visible(IEnumerable<string> units) =>
        string.Concat(
                units.Select(unit =>
                    unit.Length == 1 && char.IsSurrogate(unit[0])
                        ? $"〔U+{(int)unit[0]:X4}〕"
                        : unit
                )
            )
            .Replace(" ", "␣")
            .Replace("　", "〔全角空白〕")
            .Replace("\r", "␍")
            .Replace("\n", "␊")
            .Replace("\t", "⇥");
}
