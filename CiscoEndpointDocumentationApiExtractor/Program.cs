using System;
using System.Collections.Generic;
using System.Linq;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

// using BitMiracle.Docotic.Pdf;

namespace CiscoEndpointDocumentationApiExtractor; 

internal class Program {

    private static void Main(string[] args) {
        const string PDF_FILENAME = @"c:\Users\Ben\Documents\Work\Blue Jeans\Cisco in-room controls for Verizon\collaboration-endpoint-software-api-reference-guide-ce915.pdf";

        using PdfDocument pdf = PdfDocument.Open(PDF_FILENAME);

        Page page = pdf.GetPage(234);

        IEnumerable<Word> pageText = page.GetWords();

        foreach (Word textBlock in pageText.Where(data => isTextOnHalfOfPage(data, page, true))) {
            Letter firstLetter = textBlock.Letters[0];
            Console.WriteLine($@"{textBlock.Text}
    typeface = {firstLetter.Font.Name.Split('+', 2).Last()}
    font size = {firstLetter.FontSize}
    italic = {firstLetter.Font.IsItalic}
    bold = {firstLetter.Font.IsBold}
    weight = {firstLetter.Font.Weight:N}
    position = ({firstLetter.Location.X:N}, {firstLetter.Location.Y:N})
    baseline = {firstLetter.StartBaseLine.Y:N}
    bounds bottom = {textBlock.BoundingBox.Bottom:N}
    height (bounds) = {textBlock.BoundingBox.Height:N}
    height (transformed) = {firstLetter.PointSize:N}
    capline = {firstLetter.StartBaseLine.Y + textBlock.BoundingBox.Height:N}
    topline = {firstLetter.StartBaseLine.Y + firstLetter.PointSize:N}
    color = {firstLetter.Color}
");
        }
    }

    /*private static void Main2(string[] args) {
        const string PDF_FILENAME = @"c:\Users\Ben\Desktop\collaboration-endpoint-software-api-reference-guide-ce915.pdf";

        using PdfDocument pdf = new(PDF_FILENAME);

        PdfPage page = pdf.Pages[230 - 1];

        PdfCollection<PdfTextData> pageText = page.GetWords();

        foreach (PdfTextData textBlock in pageText.Where(data => isTextOnHalfOfPage(data, page, true))) {
            Console.WriteLine($@"{textBlock.GetText()}
typeface = {textBlock.Font.Name}
font size = {textBlock.FontSize}
underline = {textBlock.Font.Underline}
italic = {textBlock.Font.Italic}
bold = {textBlock.Font.Bold}
position = ({textBlock.Position.X:N}, {textBlock.Position.Y:N})
topsidebearing = {textBlock.Bounds.Top + textBlock.Font.TopSideBearing / 1000:N} //this seems like the closest we're going to get to a baseline
bounds bottom = {textBlock.Bounds.Bottom:N}
height (topsidebearing) = {textBlock.Font.TopSideBearing / 1000:N}
height (boundings) = {textBlock.Bounds.Height:N}
height (size) = {textBlock.Size.Height:N}
height (transformed) = {textBlock.TransformationMatrix.M22 * textBlock.FontSize:N} //this is a nice point size value
");
        }
    }*/

    internal static bool isTextOnHalfOfPage(Word text, Page page, bool isOnLeft) {
        const int POINTS_PER_INCH = 72;
        Letter    firstLetter     = text.Letters[0];

        return firstLetter.Location.Y > POINTS_PER_INCH * 0.5
            && firstLetter.Location.Y < page.Height - POINTS_PER_INCH * 1.25
            && (firstLetter.Location.X < page.Width / 2) ^ !isOnLeft;
    }

    /*private static bool isTextOnHalfOfPage2(PdfTextData text, PdfPage page, bool isOnLeft) {
        return text.Position.Y > page.Resolution * 1.25
            && text.Position.Y < page.Height - page.Resolution * 0.5
            && (text.Position.X < page.Width / 2) ^ !isOnLeft;
    }*/

}