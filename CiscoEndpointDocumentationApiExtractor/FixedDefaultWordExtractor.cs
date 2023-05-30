using System;
using System.Collections.Generic;
using System.Linq;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Util;

namespace CiscoEndpointDocumentationApiExtractor;

/// <summary>
/// <para>Copied from <see cref="DefaultWordExtractor"/> but with two fixes:</para>
/// <para>- update <c>lastY</c> (renamed from <c>y</c>) after each word, instead of only setting it once at the start of the page</para>
/// <para>- when checking if a letter's baseline is too far below the previous letter's baseline, don't use the wrong coordinate system (y=0 is actually at the bottom of the page and increases towards the top of the page, not conversely)</para>
/// </summary>
public class FixedDefaultWordExtractor: IWordExtractor {

    public static readonly FixedDefaultWordExtractor INSTANCE = new();

    protected FixedDefaultWordExtractor() { }

    public IEnumerable<Word> GetWords(IReadOnlyList<Letter> letters) {
        IOrderedEnumerable<Letter> lettersOrder = letters.OrderByDescending(x => x.Location.Y).ThenBy(x => x.Location.X);

        var lettersSoFar = new List<Letter>(10);

        var gapCountsSoFarByFontSize = new Dictionary<double, Dictionary<double, int>>();

        double? lastY      = default; //renamed by Ben
        double? lastX      = default;
        Letter? lastLetter = default;
        foreach (Letter letter in lettersOrder) {
            lastY ??= letter.Location.Y;
            lastX ??= letter.Location.X;

            if (lastLetter == null) {
                if (string.IsNullOrWhiteSpace(letter.Value)) {
                    continue;
                }

                lettersSoFar.Add(letter);
                lastLetter = letter;
                continue;
            }

            if (Math.Abs(letter.Location.Y - lastY.Value) > 0.5) { // fixed by Ben
                if (lettersSoFar.Count > 0) {
                    yield return generateWord(lettersSoFar);
                    lettersSoFar.Clear();
                }

                if (!string.IsNullOrWhiteSpace(letter.Value)) {
                    lettersSoFar.Add(letter);
                }

                lastY      = letter.Location.Y;
                lastX      = letter.Location.X;
                lastLetter = letter;

                continue;
            }

            double letterHeight = Math.Max(lastLetter.GlyphRectangle.Height, letter.GlyphRectangle.Height);

            double gap                        = letter.Location.X - (lastLetter.Location.X + lastLetter.Width);
            bool   nextToLeft                 = letter.Location.X < lastX.Value - 1;
            bool   nextBigSpace               = gap > letterHeight * 0.39;
            bool   nextIsWhiteSpace           = string.IsNullOrWhiteSpace(letter.Value);
            bool   nextFontDiffers            = !string.Equals(letter.FontName, lastLetter.FontName, StringComparison.OrdinalIgnoreCase) && gap > letter.Width * 0.1;
            bool   nextFontSizeDiffers        = Math.Abs(letter.FontSize - lastLetter.FontSize) > 0.1;
            bool   nextTextOrientationDiffers = letter.TextOrientation != lastLetter.TextOrientation;

            bool suspectGap = false;

            if (!nextFontSizeDiffers && letter.FontSize > 0 && gap >= 0) {
                double fontSize = Math.Round(letter.FontSize);
                if (!gapCountsSoFarByFontSize.TryGetValue(fontSize, out Dictionary<double, int>? gapCounts)) {
                    gapCounts                          = new Dictionary<double, int>();
                    gapCountsSoFarByFontSize[fontSize] = gapCounts;
                }

                double gapRounded = Math.Round(gap, 2);
                gapCounts.TryAdd(gapRounded, 0);

                gapCounts[gapRounded]++;

                // More than one type of gap.
                if (gapCounts.Count > 1 && gap > letterHeight * 0.16) {
                    KeyValuePair<double, int> mostCommonGap = gapCounts.MaxBy(x => x.Value);

                    if (gap > mostCommonGap.Key * 5 && mostCommonGap.Value > 1) {
                        suspectGap = true;
                    }
                }
            }

            if (nextToLeft || nextBigSpace || nextIsWhiteSpace || nextFontDiffers || nextFontSizeDiffers || nextTextOrientationDiffers || suspectGap) {
                if (lettersSoFar.Count > 0) {
                    yield return generateWord(lettersSoFar);
                    lettersSoFar.Clear();
                }
            }

            if (!string.IsNullOrWhiteSpace(letter.Value)) {
                lettersSoFar.Add(letter);
            }

            lastLetter = letter;

            lastX = letter.Location.X;
            lastY = letter.Location.Y; // added by Ben
        }

        if (lettersSoFar.Count > 0) {
            yield return generateWord(lettersSoFar);
        }
    }

    private static Word generateWord(IEnumerable<Letter> letters) {
        return new Word(letters.ToList());
    }

}