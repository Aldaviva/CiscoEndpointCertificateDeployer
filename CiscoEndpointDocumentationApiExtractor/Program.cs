﻿using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using MoreLinq;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Util;

namespace CiscoEndpointDocumentationApiExtractor;

internal class Program {

    private static void Main(string[] args) {
        const string PDF_FILENAME = @"c:\Users\Ben\Documents\Work\Blue Jeans\Cisco in-room controls for Verizon\api-reference-guide-roomos-111.pdf";

        using PdfDocument pdf = PdfDocument.Open(PDF_FILENAME);

        Page page = pdf.GetPage(361);

        IWordExtractor wordExtractor = DefaultWordExtractor.Instance;
        IReadOnlyList<Letter> lettersWithUnfuckedQuotationMarks = page.Letters
            .Where(letter => isTextOnHalfOfPage(letter, page, false))
            /*.Select(letter => letter switch {
                { Value: "\"", PointSize: 9.6, FontName: var fontName } when fontName.EndsWith("CourierNewPSMT") => new Letter(
                    letter.Value,
                    letter.GlyphRectangle,
                    new PdfPoint(letter.StartBaseLine.X, Math.Round(letter.StartBaseLine.Y, 4)),
                    new PdfPoint(letter.EndBaseLine.X, Math.Round(letter.EndBaseLine.Y, 4)),
                    letter.Width,
                    letter.FontSize,
                    letter.Font,
                    letter.Color,
                    8.8,
                    letter.TextSequence),
                _ => letter
            })*/.ToImmutableList();
        IEnumerable<Word> pageText = wordExtractor.GetWords(lettersWithUnfuckedQuotationMarks);

        // IComparer<Word> wordPositionComparer = new WordPositionComparer();
        foreach (Word textBlock in pageText) {
            Letter firstLetter = textBlock.Letters[0];
            Console.WriteLine(textBlock.Text);
            /*Console.WriteLine($@"{textBlock.Text}
    typeface = {firstLetter.Font.Name.Split('+', 2).Last()}
    point size = {firstLetter.PointSize}
    italic = {firstLetter.Font.IsItalic}
    bold = {firstLetter.Font.IsBold}
    weight = {firstLetter.Font.Weight:N}
    position = ({firstLetter.Location.X:N}, {firstLetter.Location.Y:N})
    baseline = {firstLetter.StartBaseLine.Y:N3}
    bounds bottom = {textBlock.BoundingBox.Bottom:N}
    height (bounds) = {textBlock.BoundingBox.Height:N}
    height (transformed) = {firstLetter.PointSize:N}
    capline = {firstLetter.StartBaseLine.Y + textBlock.BoundingBox.Height:N}
    topline = {firstLetter.StartBaseLine.Y + firstLetter.PointSize:N}
    color = {firstLetter.Color}
    text sequence = {firstLetter.TextSequence:N0}
");*/
        }
    }

    internal static bool isTextOnHalfOfPage(Word word, Page page, bool isOnLeft) => isTextOnHalfOfPage(word.Letters[0], page, isOnLeft);

    internal static bool isTextOnHalfOfPage(Letter letter, Page page, bool isOnLeft) {

        const int POINTS_PER_INCH = 72;

        const double LEFT_MARGIN   = 5.0 / 8.0 * POINTS_PER_INCH;
        const double TOP_MARGIN    = 1.0 * POINTS_PER_INCH;
        const double BOTTOM_MARGIN = POINTS_PER_INCH * 0.5;

        return letter.Location.Y > BOTTOM_MARGIN
            && letter.Location.Y < page.Height - TOP_MARGIN
            && (letter.Location.X < (page.Width - LEFT_MARGIN) / 2 + LEFT_MARGIN) ^ !isOnLeft
            && letter.Location.X > LEFT_MARGIN;
    }

}

public class TextSequenceWordExtractor: IWordExtractor {

    public IEnumerable<Word> GetWords2(IReadOnlyList<Letter> letters) =>
        letters.GroupBy(letter => letter.TextSequence)
            .SelectMany(grouping => grouping.Split(letter => string.IsNullOrWhiteSpace(letter.Value))
                .Where(lettersInWord => lettersInWord.Any())
                .Select(lettersInWord => new Word(lettersInWord.ToList())));

    public IEnumerable<Word> GetWords(IReadOnlyList<Letter> letters) {
        int? previousSequenceId = null;
        var  currentWordLetters = new List<Letter>();

        foreach (Letter letter in letters) {
            bool isWhitespace = string.IsNullOrWhiteSpace(letter.Value);

            bool startNewWord = /*isWhitespace ||*/
                previousSequenceId.HasValue && previousSequenceId != letter.TextSequence &&
                currentWordLetters is not [{ Value: "\"", PointSize: 9.6 }] &&
                letter is not { Value: "\"", PointSize: 9.6 } /*||
                (currentWordLetters.LastOrDefault() is { } previousLetter && Math.Abs(previousLetter.StartBaseLine.Y - letter.StartBaseLine.Y) > letter.GlyphRectangle.Height / 2)*/;

            if (startNewWord && currentWordLetters.Any()) {
                yield return new Word(currentWordLetters);
                currentWordLetters.Clear();
            }

            if (!isWhitespace) {
                currentWordLetters.Add(letter);
            }

            previousSequenceId = letter.TextSequence;
        }

        if (currentWordLetters.Any()) {
            yield return new Word(currentWordLetters);
        }

    }

}