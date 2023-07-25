using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using CiscoEndpointDocumentationApiExtractor.Generation;
using MoreLinq;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Util;

namespace CiscoEndpointDocumentationApiExtractor.Extraction;

internal class Program {

    private const string PDF_FILENAME       = @"..\..\..\Documentation\11.1.pdf";
    private const string EVENT_XML_FILENAME = @"..\..\..\Documentation\event_11.5.xml";

    public static async Task Main(string[] args) {
        // Main2(args);
        // return;
        ExtractedDocumentation docs = new();
        // PdfParser.parsePdf(PDF_FILENAME, docs);
        new EventReader(docs).parseEventXml(EVENT_XML_FILENAME);
        // foreach (DocXEvent xEvent in docs.events.Where(xEvent => xEvent.access == EventAccess.PUBLIC_API)) {
        //     Console.WriteLine($"*es {string.Join(' ', xEvent.name)}");
        // }

        // Console.WriteLine("** end\r\n");
        new Fixes(docs).fix();
        await CsClientWriter.writeClient(docs);
    }

    private static void Main2(string[] args) {
        using PdfDocument pdf = PdfDocument.Open(PDF_FILENAME);

        Page page = pdf.GetPage(62);

        IWordExtractor wordExtractor = DefaultWordExtractor.Instance;
        IReadOnlyList<Letter> lettersWithUnfuckedQuotationMarks = page.Letters
            .Where(letter => PdfReader.isTextOnHalfOfPage(letter, page, true))
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
            // Console.WriteLine(textBlock.Text);
            Console.WriteLine($@"{textBlock.Text}
    character style = {PdfReader.getCharacterStyle(textBlock)}
    typeface = {firstLetter.Font.Name.Split('+', 2).Last()}
    point size = {firstLetter.PointSize:N3}
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
");
        }
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