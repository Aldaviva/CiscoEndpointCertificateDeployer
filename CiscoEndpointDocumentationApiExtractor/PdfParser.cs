﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using MoreLinq;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Outline;

namespace CiscoEndpointDocumentationApiExtractor;

public static class PdfParser {

    private const int DPI = 72;

    public static void Main() {
        try {
            const string PDF_FILENAME = @"c:\Users\Ben\Documents\Work\Blue Jeans\Cisco in-room controls for Verizon\collaboration-endpoint-software-api-reference-guide-ce915.pdf";

            Stopwatch stopwatch = Stopwatch.StartNew();
            XAPI      xapi      = parsePdf(PDF_FILENAME);
            Console.WriteLine($"Parsed PDF in {stopwatch.Elapsed:g}");

            foreach (XCommand xCommand in xapi.commands) {
                Console.WriteLine($@"
{string.Join(' ', xCommand.name)}
    Applies to: {string.Join(", ", xCommand.appliesTo)}
    Requires user role: {string.Join(", ", xCommand.requiresUserRole)}
    Body: {xCommand.description}
    Parameters:");
                foreach (Parameter parameter in xCommand.parameters) {
                    Console.WriteLine($@"
        {parameter.name}:
            {parameter.valueSpaceDescription}
            Type: {parameter.type}
            Default value: {parameter.defaultValue}
            Required: {parameter.required}
            Description: {parameter.description}");

                    switch (parameter) {
                        case StringParameter param:
                            Console.WriteLine($@"           Length: [{param.minimumLength}, {param.maximumLength}]");
                            break;
                        case IntParameter param:
                            Console.WriteLine($@"           Range: [{param.ranges.Min(range => range.minimum)}, {param.ranges.Max(range => range.maximum)}");
                            if (param.arrayIndexItemParameterPosition is not null) {
                                Console.WriteLine($@"           Position in name: {param.arrayIndexItemParameterPosition}");
                            }

                            break;
                        case EnumParameter param:
                            Console.WriteLine(@"           Possible values:");
                            foreach (EnumValue possibleValue in param.possibleValues) {
                                Console.WriteLine($@"               {possibleValue.name}: {possibleValue.description}");
                            }

                            break;
                    }
                }
            }
        } catch (ParsingException e) {
            Letter firstLetter = e.word.Letters[0];
            Console.WriteLine($"Failed to parse page {e.page.Number}: {e.Message} (word: {e.word.Text}, character style: {e.characterStyle}, parser state: {e.state}, position: " +
                $"({firstLetter.StartBaseLine.X / DPI:N}\", {(e.page.Height - firstLetter.StartBaseLine.Y) / DPI:N}\"))");
            Console.WriteLine($"Font: {firstLetter.PointSize:N2}pt {firstLetter.FontName}");
        }
    }

    private static XAPI parsePdf(string filename) {
        using PdfDocument pdf  = PdfDocument.Open(filename);
        XAPI              xapi = new();

        parseSection(pdf, xapi.configurations);
        parseSection(pdf, xapi.commands);
        parseSection(pdf, xapi.statuses);

        return xapi;
    }

    private static void parseSection<T>(PdfDocument pdf, ICollection<T> xapiDestinationCollection) where T: AbstractCommand, new() {
        string startBookmark, endBookmark;

        if (typeof(T) == typeof(XConfiguration)) {
            startBookmark = "xConfiguration commands";
            endBookmark   = "xCommand commands";
        } else if (typeof(T) == typeof(XCommand)) {
            startBookmark = "xCommand commands";
            endBookmark   = "xStatus commands";
        } else if (typeof(T) == typeof(XStatus)) {
            startBookmark = "xStatus commands";
            endBookmark   = "Appendices";
        } else {
            throw new ArgumentOutOfRangeException(nameof(xapiDestinationCollection), typeof(T), "Unknown command type");
        }

        Range commandPages = getPagesForSection(pdf, startBookmark, endBookmark);

        IEnumerable<(Word word, Page page)> wordsOnPages = getWordsOnPages(pdf, commandPages);

        ParserState state                = ParserState.START;
        double?     previousWordBaseline = default;

        ISet<string> requiredParameters = new HashSet<string>();
        string?      parameterName;
        Parameter?   parameter;
        string?      parameterDescription;
        string?      partialProductName;
        int          parameterUsageIndex;
        EnumValue?   enumParameterValue;

        resetMethodParsingState();

        void resetMethodParsingState() {
            partialProductName  = default;
            parameterUsageIndex = 0;
            requiredParameters.Clear();
            resetParameterParsingState();
        }

        void resetParameterParsingState() {
            parameter            = default;
            parameterName        = default;
            parameterDescription = default;
            enumParameterValue   = default;
        }

        T command = new();
        xapiDestinationCollection.Add(command);

        foreach ((Word word, Page page) in wordsOnPages) {
            CharacterStyle characterStyle = getCharacterStyle(word);

            Console.WriteLine($"Parsing {word.Text}\t(character style = {characterStyle}, parser state = {state})");

            switch (characterStyle) {
                case CharacterStyle.METHOD_FAMILY_HEADING:
                    //skip, not useful information, we'll get the method name from the METHOD_NAME_HEADING below
                    break;
                case CharacterStyle.METHOD_NAME_HEADING:
                    // ReSharper disable once MergeIntoPattern broken null checking if you apply this suggestion
                    if (state == ParserState.USAGE_DEFAULT_VALUE && parameter is StringParameter && parameter.defaultValue is not null) {
                        parameter.defaultValue = parameter.defaultValue.TrimEnd('"');
                    }

                    if (state != ParserState.VERSION_AND_PRODUCTS_COVERED_PREAMBLE && state != ParserState.METHOD_NAME_HEADING) {
                        // finished last method, moving to next method
                        command = new T();
                        xapiDestinationCollection.Add(command); //FIXME
                        resetMethodParsingState();
                    }

                    state = ParserState.METHOD_NAME_HEADING;
                    command.name.Add(word.Text);
                    break;
                case CharacterStyle.PRODUCT_NAME:
                    switch (state) {
                        case ParserState.APPLIES_TO or ParserState.APPLIES_TO_PRODUCTS:
                            state = ParserState.APPLIES_TO_PRODUCTS;
                            string productName = (partialProductName ?? string.Empty) + word.Text;
                            if (parseProduct(productName) is { } product) {
                                command.appliesTo.Add(product);
                                partialProductName = null;
                            } else if (word.Text is not ("All" or "products")) {
                                partialProductName = productName;
                                // throw new ParsingException(word, state, characterStyle, page, "product name was not 'All', 'products', or a recognized product name");
                            }

                            break;
                        case ParserState.USAGE_PARAMETER_VALUE_SPACE or ParserState.USAGE_PARAMETER_VALUE_SPACE_APPLIES_TO:
                            state = ParserState.USAGE_PARAMETER_VALUE_SPACE_APPLIES_TO;
                            if (parameter is not null) {
                                parameter.valueSpaceDescription = appendWord(parameter.valueSpaceDescription, word, previousWordBaseline);
                            } else {
                                // This entire parameter, not just some of its values, only applies to specific products (xCommand Audio Volume Decrease Device:)
                                parameterDescription = appendWord(parameterDescription, word, previousWordBaseline);
                                // throw new ParsingException(word, state, characterStyle, page, "no parameter to append value space description to");
                            }

                            break;
                        case ParserState.USAGE_PARAMETER_VALUE_DESCRIPTION:
                            if (word.Text != "[" && word.Text != "]") {
                                // skip delimiters
                            } else if (parseProduct(word.Text) is { } product2 && (parameter as IntParameter)?.ranges.LastOrDefault() is { } lastRange) {
                                lastRange.appliesTo.Add(product2);
                            }

                            break;
                        case ParserState.USAGE_DEFAULT_VALUE when parameter is not null:
                            parameter.defaultValue = appendWord(parameter.defaultValue, word, previousWordBaseline);
                            break;
                        default:
                            throw new ParsingException(word, state, characterStyle, page, "unexpected state for character style");
                    }

                    break;
                case CharacterStyle.USER_ROLE:
                    if (state is ParserState.REQUIRES_USER_ROLE or ParserState.REQUIRES_USER_ROLE_ROLES) {
                        state = ParserState.REQUIRES_USER_ROLE_ROLES;

                        // sometimes Cisco forgets to put the space between enum values, like xCommand Call Disconnect's Requires User Role list
                        foreach (string roleName in word.Text.TrimEnd(',').Split(',')) {
                            if (parseEnum<UserRole>(roleName) is { } role) {
                                command.requiresUserRole.Add(role);
                            } else {
                                throw new ParsingException(word, state, characterStyle, page, "role was not a recognized user role");
                            }
                        }
                    } else {
                        throw new ParsingException(word, state, characterStyle, page, "unexpected state for character style");
                    }

                    break;
                case CharacterStyle.USAGE_HEADING:
                    if (word.Text == "USAGE:") {
                        state = ParserState.USAGE_EXAMPLE;
                    } else {
                        throw new ParsingException(word, state, characterStyle, page, "unexpected word for character style");
                    }

                    break;
                case CharacterStyle.USAGE_EXAMPLE:
                    if (state == ParserState.USAGE_EXAMPLE) {
                        if (command is XConfiguration xConfiguration && Regex.Match(word.Text, @"^(?<prefix>\w*)\[(?<name>\w+)\]$") is { Success: true } match) {
                            IntParameter indexParameter = new() {
                                arrayIndexItemParameterPosition = parameterUsageIndex,
                                required                        = true,
                                name                            = match.Groups["name"].Value,
                                namePrefix                      = match.Groups["prefix"].Value.EmptyToNull()
                            };
                            xConfiguration.parameters.Add(indexParameter);
                            requiredParameters.Add(indexParameter.name);
                        }

                        parameterUsageIndex++;
                    } else {
                        throw new ParsingException(word, state, characterStyle, page, "unexpected state for character style");
                    }

                    break;
                case CharacterStyle.PARAMETER_NAME:
                    switch (state) {
                        case ParserState.USAGE_EXAMPLE:
                            if (!word.Text.EndsWith(']')) {
                                requiredParameters.Add(word.Text);
                            }

                            // otherwise it's an optional parameter like [Channel: Channel]
                            parameterUsageIndex++;
                            break;
                        case ParserState.USAGE_PARAMETER_NAME or ParserState.USAGE_PARAMETER_VALUE_DESCRIPTION when command is XConfiguration xConfiguration &&
                            xConfiguration.parameters.FirstOrDefault(p => word.Text == p.name + ':' && (p as IntParameter)?.arrayIndexItemParameterPosition is not null) is { } _positionalParam:
                            parameter = _positionalParam;
                            state     = ParserState.USAGE_PARAMETER_DESCRIPTION;
                            break;
                        case ParserState.USAGE_PARAMETER_NAME or ParserState.USAGE_PARAMETER_VALUE_DESCRIPTION or ParserState.USAGE_DEFAULT_VALUE or ParserState.USAGE_PARAMETER_VALUE_SPACE or
                            ParserState.USAGE_PARAMETER_VALUE_SPACE_APPLIES_TO or ParserState.USAGE_PARAMETER_DESCRIPTION:
                            // ReSharper disable once MergeIntoPattern - broken null checking if you apply this suggestion
                            if (parameter is StringParameter _param && _param.defaultValue is not null) {
                                _param.defaultValue = _param.defaultValue.TrimEnd('"');
                            }

                            resetParameterParsingState();
                            parameterName = word.Text.TrimEnd(':');
                            state         = ParserState.USAGE_PARAMETER_VALUE_SPACE;
                            break;
                        default:
                            throw new ParsingException(word, state, characterStyle, page, "unexpected state for character style");
                    }

                    break;
                case CharacterStyle.PARAMETER_VALUESPACE:
                    switch (state) {
                        case ParserState.USAGE_PARAMETER_VALUE_SPACE or ParserState.USAGE_PARAMETER_VALUE_SPACE_APPLIES_TO:
                            Regex numericRangePattern = new(@"^\((?<min>-?\d+)\.\.(?<max>-?\d+)\)$");
                            switch (word.Text) {
                                case "String" when parameter is null:
                                    if (parameterName is null) {
                                        throw new ParsingException(word, state, characterStyle, page, "found parameter value space without a previously-parsed parameter name");
                                    }

                                    parameter = new StringParameter {
                                        name        = parameterName,
                                        required    = requiredParameters.Contains(parameterName),
                                        description = parameterDescription is not null ? parameterDescription + '\n' : string.Empty
                                    };

                                    parameterName = null;
                                    break;
                                case "Integer" when parameter is null:
                                    if (parameterName is null) {
                                        throw new ParsingException(word, state, characterStyle, page, "found parameter value space without a previously-parsed parameter name");
                                    }

                                    parameter = new IntParameter {
                                        name        = parameterName,
                                        required    = requiredParameters.Contains(parameterName),
                                        description = parameterDescription is not null ? parameterDescription + '\n' : string.Empty
                                    };

                                    parameterName = null;
                                    break;
                                case { } enumList when parameter is null:
                                    if (parameterName is null) {
                                        throw new ParsingException(word, state, characterStyle, page, "found parameter value space without a previously-parsed parameter name");
                                    }

                                    parameter = new EnumParameter {
                                        name        = parameterName,
                                        required    = requiredParameters.Contains(parameterName),
                                        description = parameterDescription is not null ? parameterDescription + '\n' : string.Empty,
                                        possibleValues = Enumerable.ToHashSet(enumList.Split('/', StringSplitOptions.RemoveEmptyEntries)
                                            .Select(value => new EnumValue { name = value }))
                                    };

                                    parameterName = null;
                                    break;
                                case { } valueSpace when parameter is StringParameter _param && numericRangePattern.Match(valueSpace) is { Success: true } match:
                                    // It's an integer encoded as a string!
                                    _param.minimumLength = match.Groups["min"].Length;
                                    _param.maximumLength = match.Groups["max"].Length;
                                    state                = ParserState.USAGE_PARAMETER_VALUE_SPACE_APPLIES_TO;
                                    break;
                                case { } valueSpace when parameter is StringParameter _param && Regex.Match(valueSpace, @"^\((?<min>-?\d+),(?<max>-?\d+)\)$") is { Success: true } match:
                                    try {
                                        _param.minimumLength = int.Parse(match.Groups["min"].Value);
                                        _param.maximumLength = int.Parse(match.Groups["max"].Value);
                                    } catch (FormatException) {
                                        throw new ParsingException(word, state, characterStyle, page,
                                            $"Failed to parse uncommon comma-style string parameter length range \"{valueSpace}\" as ({match.Groups["min"].Value}, {match.Groups["max"].Value})");
                                    }

                                    state = ParserState.USAGE_PARAMETER_VALUE_SPACE_APPLIES_TO;
                                    break;
                                case { } valueSpace when parameter is StringParameter _param && valueSpace.StartsWith('(') && valueSpace.EndsWith(','):
                                    try {
                                        _param.minimumLength = int.Parse(valueSpace[1..^1]);
                                    } catch (FormatException) {
                                        throw new ParsingException(word, state, characterStyle, page,
                                            $"Failed to parse string parameter length lower bound \"{valueSpace}\" as integer {valueSpace[1..^1]}");
                                    }

                                    break;
                                case { } valueSpace when parameter is StringParameter _param && valueSpace.EndsWith(')'):
                                    try {
                                        _param.maximumLength = int.Parse(valueSpace[..^1]);
                                    } catch (FormatException) {
                                        throw new ParsingException(word, state, characterStyle, page,
                                            $"Failed to parse string parameter length upper bound \"{valueSpace}\" as integer {valueSpace[..^1]}");
                                    }

                                    state = ParserState.USAGE_PARAMETER_VALUE_SPACE_APPLIES_TO;
                                    break;
                                case { } valueSpace when parameter is IntParameter _param && numericRangePattern.Match(valueSpace) is { Success: true } match:
                                    _param.ranges.Add(new IntRange { minimum = int.Parse(match.Groups["min"].Value), maximum = int.Parse(match.Groups["max"].Value) });
                                    // state = ParserState.USAGE_PARAMETER_VALUE_SPACE_APPLIES_TO; //sometimes might be followed by more text
                                    break;
                                case { } enumList when parameter is EnumParameter _param:
                                    //second line of wrapped enum values
                                    IEnumerable<EnumValue> additionalValues = enumList.Split('/', StringSplitOptions.RemoveEmptyEntries)
                                        .Select(value => new EnumValue { name = value });
                                    foreach (EnumValue additionalValue in additionalValues) {
                                        _param.possibleValues.Add(additionalValue);
                                    }

                                    break;
                                default:
                                    //ignore additional text after the value space that clarifies when it applies
                                    break;
                            }

                            if (parameter != null) {
                                parameter.valueSpaceDescription = parameter.valueSpaceDescription is null ? word.Text : appendWord(parameter.valueSpaceDescription, word, previousWordBaseline);
                            }

                            break;
                        case ParserState.USAGE_DEFAULT_VALUE when parameter is not null:
                            parameter.defaultValue = appendWord(parameter.defaultValue, word, previousWordBaseline);
                            break;
                        default:
                            throw new ParsingException(word, state, characterStyle, page, "unexpected state for character style");
                    }

                    break;
                case CharacterStyle.PARAMETER_VALUE_TERM:
                    switch (parameter) {
                        case EnumParameter param:
                            enumParameterValue = param.possibleValues.FirstOrDefault(value => value.name == word.Text.TrimEnd(':'));
                            state              = ParserState.USAGE_PARAMETER_VALUE_DESCRIPTION;
                            break;
                        case { } param:
                            param.description = appendWord(param.description, word, previousWordBaseline);
                            state             = ParserState.USAGE_PARAMETER_DESCRIPTION;
                            break;
                        // throw new ParsingException(word, state, characterStyle, page,
                        //     $"found parameter enum term character style for non-enum parameter {parameter?.name ?? "(null param)"} of type {parameter?.type.ToString() ?? "null"}");
                    }

                    break;
                case CharacterStyle.BODY:
                    switch (state) {
                        case ParserState.START:
                            state = ParserState.VERSION_AND_PRODUCTS_COVERED_PREAMBLE;
                            break;
                        case ParserState.VERSION_AND_PRODUCTS_COVERED_PREAMBLE:
                            //skip
                            break;
                        case ParserState.METHOD_NAME_HEADING or ParserState.APPLIES_TO:
                            state = ParserState.APPLIES_TO;
                            break;
                        case ParserState.APPLIES_TO_PRODUCTS:
                            state = ParserState.REQUIRES_USER_ROLE;
                            break;
                        case ParserState.USAGE_EXAMPLE:
                            if (word.Text == "where") {
                                state = ParserState.USAGE_PARAMETER_NAME;
                            } else {
                                throw new ParsingException(word, state, characterStyle, page, "unexpected word for state and character style");
                            }

                            break;
                        case ParserState.USAGE_PARAMETER_DESCRIPTION or ParserState.USAGE_PARAMETER_VALUE_SPACE_APPLIES_TO or ParserState.USAGE_PARAMETER_VALUE_SPACE or ParserState
                            .USAGE_PARAMETER_VALUE_DESCRIPTION when isDifferentParagraph(getPreviousWordBaselineDifference(word, previousWordBaseline)) && word.Text == "Default":
                            state              = ParserState.USAGE_DEFAULT_VALUE_HEADING;
                            enumParameterValue = null;
                            break;
                        case ParserState.USAGE_PARAMETER_DESCRIPTION or ParserState.USAGE_PARAMETER_VALUE_SPACE_APPLIES_TO or ParserState.USAGE_PARAMETER_VALUE_SPACE or ParserState
                                .USAGE_PARAMETER_VALUE_DESCRIPTION
                            when isDifferentParagraph(getPreviousWordBaselineDifference(word, previousWordBaseline)) && word.Text == "Range:" && parameter is IntParameter:
                            state = ParserState.USAGE_PARAMETER_VALUE_SPACE;
                            break;
                        case ParserState.USAGE_PARAMETER_DESCRIPTION or ParserState.USAGE_PARAMETER_VALUE_SPACE_APPLIES_TO or ParserState.USAGE_PARAMETER_VALUE_SPACE:
                            if (parameter is not null) {
                                if (parameter is IntParameter intParameter && Regex.Match(word.Text, @"^(?<min>-?\d+)\.\.(?<max>-?\d+)$") is { Success: true } match) {
                                    intParameter.ranges.Add(new IntRange { minimum = int.Parse(match.Groups["min"].Value), maximum = int.Parse(match.Groups["max"].Value) });
                                    state = ParserState.USAGE_PARAMETER_VALUE_DESCRIPTION;
                                } else {
                                    parameter.description = appendWord(parameter.description, word, previousWordBaseline);
                                    state                 = ParserState.USAGE_PARAMETER_DESCRIPTION;
                                }

                            } else if (word.Text == ":" && state == ParserState.USAGE_PARAMETER_VALUE_SPACE) {
                                //the colon after the parameter name, usually it's part of the word with PARAMETER_NAME character style but sometimes it's tokenized as a separate word
                                //skip
                            } else {
                                throw new ParsingException(word, state, characterStyle, page, "no current parameter to append description to");
                            }

                            break;
                        case ParserState.USAGE_PARAMETER_VALUE_DESCRIPTION:
                            if (enumParameterValue is not null) {
                                enumParameterValue.description = appendWord(enumParameterValue.description, word, previousWordBaseline);
                            } else if ((parameter as IntParameter)?.ranges.LastOrDefault() is { } lastRange) {
                                lastRange.description = appendWord(lastRange.description, word, previousWordBaseline);
                            }

                            break;
                        case ParserState.USAGE_DEFAULT_VALUE_HEADING:
                            if (word.Text == "value:") {
                                state = ParserState.USAGE_DEFAULT_VALUE;
                            } else {
                                throw new ParsingException(word, state, characterStyle, page, "unexpected word for state and character style");
                            }

                            break;
                        case ParserState.USAGE_DEFAULT_VALUE when parameter is not null:
                            switch (parameter) {
                                case IntParameter param:
                                    param.defaultValue = word.Text;
                                    break;
                                case EnumParameter param:
                                    param.defaultValue = word.Text;
                                    break;
                                case StringParameter param:
                                    param.defaultValue = param.defaultValue is null ? word.Text.TrimStart('"') : word.Text;
                                    break;
                            }

                            break;
                        case ParserState.REQUIRES_USER_ROLE_ROLES or ParserState.DESCRIPTION:
                            state               = ParserState.DESCRIPTION;
                            command.description = appendWord(command.description, word, previousWordBaseline);
                            break;
                        default:
                            break;
                    }

                    break;
                default:
                    throw new ArgumentOutOfRangeException(characterStyle.ToString());
            }

            previousWordBaseline = word.Letters[0].StartBaseLine.Y;
        }
    }

    private static string appendWord(string? head, Word tail, double? previousWordBaseline) {
        double baselineDifference = getPreviousWordBaselineDifference(tail, previousWordBaseline);
        bool   isDifferentLine    = baselineDifference > 3;
        string wordSeparator;

        if (string.IsNullOrWhiteSpace(head)) {
            wordSeparator = string.Empty;
        } else if (isDifferentLine && head.EndsWith('-')) {
            head          = head.TrimEnd('-');
            wordSeparator = string.Empty;
        } else if (isDifferentParagraph(baselineDifference)) {
            wordSeparator = "\n";
        } else {
            wordSeparator = " ";
        }

        return (head ?? string.Empty) + wordSeparator + tail.Text;
    }

    private static double getPreviousWordBaselineDifference(Word tail, double? previousWordBaseline) =>
        previousWordBaseline is not null ? Math.Abs(tail.Letters[0].StartBaseLine.Y - (double) previousWordBaseline) : 0;

    private static bool isDifferentParagraph(double baselineDifference) => baselineDifference > 10;

    private static IEnumerable<(Word word, Page page)> getWordsOnPages(PdfDocument pdf, Range pageIndices) {
        IComparer<Word> wordPositionComparer = new WordPositionComparer();
        int[]           pageNumbers          = Enumerable.Range(0, pdf.NumberOfPages).ToArray()[pageIndices];
        foreach (int pageNumber in pageNumbers) {
            foreach (bool readLeftSide in new[] { true, false }) {
                Page              page  = pdf.GetPage(pageNumber);
                IEnumerable<Word> words = page.GetWords().Where(word => Program.isTextOnHalfOfPage(word, page, readLeftSide)).OrderBy(word => word, wordPositionComparer);

                foreach (Word word in words) {
                    yield return (word, page);
                }
            }
        }
    }

    private static Range getPagesForSection(PdfDocument doc, string previousBookmarkName, string nextBookmarkName) {
        doc.TryGetBookmarks(out Bookmarks bookmarks);
        IEnumerable<DocumentBookmarkNode> bookmarksInSection = bookmarks.GetNodes()
            .Where(node => node.Level == 0)
            .OfType<DocumentBookmarkNode>()
            .OrderBy(node => node.PageNumber).SkipUntil(node => node.Title == previousBookmarkName).TakeUntil(node => node.Title == nextBookmarkName)
            .ToList();
        return bookmarksInSection.First().PageNumber..bookmarksInSection.Last().PageNumber;
    }

    private static CharacterStyle getCharacterStyle(Word word) =>
        word.Letters[0] switch {
            { PointSize: 14.0 }                                                                              => CharacterStyle.METHOD_FAMILY_HEADING,
            { PointSize: 10.0 }                                                                              => CharacterStyle.METHOD_NAME_HEADING,
            { PointSize: >= 6 and <= 6.5 }                                                                   => CharacterStyle.PRODUCT_NAME,
            { PointSize: 7.0 }                                                                               => CharacterStyle.USER_ROLE,
            { PointSize: 8.0, FontName: var fontName } when fontName.EndsWith("CiscoSans")                   => CharacterStyle.USAGE_HEADING,
            { PointSize: 8.8, FontName: var fontName } when fontName.EndsWith("CourierNewPSMT")              => CharacterStyle.USAGE_EXAMPLE,
            { PointSize: 8.8, FontName: var fontName } when fontName.EndsWith("CourierNewPS-ItalicMT")       => CharacterStyle.PARAMETER_NAME,
            { PointSize: 8.0, FontName: var fontName } when fontName.EndsWith("CiscoSans-ExtraLightOblique") => CharacterStyle.PARAMETER_VALUESPACE,
            { PointSize: 8.0, FontName: var fontName } when fontName.EndsWith("CiscoSans-Oblique")           => CharacterStyle.PARAMETER_VALUE_TERM,
            _                                                                                                => CharacterStyle.BODY
        };

    private static T? parseEnum<T>(string text) where T: struct, Enum => Enum.IsDefined(typeof(T), text) && Enum.TryParse(text, true, out T result) ? result : null;

    private static Product? parseProduct(string text) => parseEnum<Product>(text.Replace('/', '_'));

}

internal class WordPositionComparer: IComparer<Word> {

    public int Compare(Word? a, Word? b) {
        if (a is null) {
            return -1;
        } else if (b is null) {
            return 1;
        }

        Letter aLetter = a.Letters[0];
        double aY      = aLetter.Location.Y + aLetter.PointSize;

        Letter bLetter = b.Letters[0];
        double bY      = bLetter.Location.Y + bLetter.PointSize;

        int verticalComparison = aY.CompareTo(bY);
        if (verticalComparison != 0) {
            return -verticalComparison; //start from the top of the page, where the Y position is greatest
        } else {
            double aX = aLetter.Location.X;
            double bX = bLetter.Location.X;
            return aX.CompareTo(bX);
        }
    }

}

internal class ParsingException: Exception {

    public Word word { get; }
    public ParserState state { get; }
    public CharacterStyle characterStyle { get; }
    public Page page { get; }

    public ParsingException(Word word, ParserState state, CharacterStyle characterStyle, Page page, string message): base(message) {
        this.word           = word;
        this.state          = state;
        this.characterStyle = characterStyle;
        this.page           = page;
    }

}

internal enum CharacterStyle {

    METHOD_FAMILY_HEADING,
    METHOD_NAME_HEADING,
    PRODUCT_NAME,
    USER_ROLE,
    USAGE_HEADING,
    USAGE_EXAMPLE,
    PARAMETER_NAME,
    PARAMETER_VALUESPACE,
    PARAMETER_VALUE_TERM,
    BODY

}

internal enum ParserState {

    START,
    VERSION_AND_PRODUCTS_COVERED_PREAMBLE,
    METHOD_NAME_HEADING,
    APPLIES_TO,
    APPLIES_TO_PRODUCTS,
    REQUIRES_USER_ROLE,
    REQUIRES_USER_ROLE_ROLES,
    DESCRIPTION,
    USAGE_EXAMPLE,                          //the xCommand or similar example invocation, directly below the "USAGE:" header
    USAGE_PARAMETER_NAME,                   //the name of the parameter underneath the "where"
    USAGE_PARAMETER_DESCRIPTION,            //the regular description of the parameter, underneath the valuespace summary and above the value space descriptions
    USAGE_PARAMETER_VALUE_SPACE,            //the italic text for each parameter that says whether it's an Integer, String, or the slash-separated enum values
    USAGE_PARAMETER_VALUE_DESCRIPTION,      //the text to the right of the bold enum value name that describes the parameter value
    USAGE_PARAMETER_VALUE_SPACE_APPLIES_TO, //says which products a value space applies to, as italic text to the right of the valuespace
    USAGE_DEFAULT_VALUE_HEADING,            //the xConfiguration "Default value:" header
    USAGE_DEFAULT_VALUE                     //the xConfiguration default valuespace

}