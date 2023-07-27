using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using MoreLinq;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Graphics.Colors;
using UglyToad.PdfPig.Outline;
using UglyToad.PdfPig.Util;

namespace CiscoEndpointDocumentationApiExtractor.Extraction;

public static class PdfReader {

    private const int DPI = 72;

    private static readonly ISet<string> XSTATUS_DESCRIPTION_VALUE_SPACE_HEADING_WORDS = new HashSet<string> { "Value", "space", "of", "the", "result", "returned:" };
    private static readonly IColor       PRODUCT_NAME_COLOR                            = new RGBColor(0.035m, 0.376m, 0.439m);

    public static void Main() {
        // Console.WriteLine(string.Join("\n", guessEnumRange("Microphone.1/../Microphone.4/Line.1/Line.2/HDMI.2".Split('/')).Select(value => value.name)));
        // return;

        try {
            const string PDF_FILENAME = @"c:\Users\Ben\Documents\Work\Blue Jeans\Cisco in-room controls for Verizon\api-reference-guide-roomos-111.pdf";

            Stopwatch              stopwatch = Stopwatch.StartNew();
            ExtractedDocumentation xapi      = new();
            parsePdf(PDF_FILENAME, xapi);
            Console.WriteLine($"Parsed PDF in {stopwatch.Elapsed:g}");

            /*IEnumerable<IGrouping<string, AbstractCommand>> duplicates = xapi.statuses
                .Concat<AbstractCommand>(xapi.configurations)
                .Concat(xapi.commands)
                .GroupBy(command => string.Join(' ', command.name.Skip(1)))
                .Where(grouping => grouping.Count() > 1);

            foreach (IGrouping<string, AbstractCommand> duplicate in duplicates) {
                foreach (AbstractCommand command in duplicate) {
                    Console.WriteLine(string.Join(' ', command.name));
                }

                Console.WriteLine();
            }*/

            // foreach (AbstractCommand command in ) {
            //     string name = ;
            //
            // }

            /*foreach (DocXConfiguration command in xapi.commands.Concat(xapi.configurations)) {
                Console.WriteLine($@"
{command.GetType().Name} {string.Join(' ', command.name)}
    Applies to: {string.Join(", ", command.appliesTo)}
    Requires user role: {string.Join(", ", command.requiresUserRole)}
    Body: {command.description}
    Parameters:");
                foreach (Parameter parameter in command.parameters) {
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
                            if (param.ranges.Any()) {
                                Console.WriteLine($@"           Range: [{param.ranges.Min(range => range.minimum)}, {param.ranges.Max(range => range.maximum)}");
                            }

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

            foreach (DocXStatus status in xapi.statuses) {
                Console.WriteLine($@"
{status.GetType().Name} {string.Join(' ', status.name)}
    Applies to: {string.Join(", ", status.appliesTo)}
    Requires user role: {string.Join(", ", status.requiresUserRole)}
    Body: {status.description}
    Return value space:");

                switch (status.returnValueSpace) {
                    case IntValueSpace intValueSpace:
                        Console.Write("       Integer");
                        if (intValueSpace.ranges.Any()) {
                            Console.Write($" - Range: [{intValueSpace.ranges.Min(range => range.minimum)}, {intValueSpace.ranges.Max(range => range.maximum)}");
                        }

                        Console.WriteLine();
                        break;
                    case StringValueSpace stringValueSpace:
                        Console.WriteLine("       String");
                        break;
                    case EnumValueSpace enumValueSpace:
                        Console.WriteLine($"       Enum - {string.Join('/', enumValueSpace.possibleValues.Select(value => value.name))}");
                        break;
                }
            }*/

        } catch (ParsingException e) {
            Letter firstLetter = getFirstNonQuotationMarkLetter(e.word.Letters);
            Console.WriteLine($"Failed to parse page {e.page.Number}: {e.Message} (word: {e.word.Text}, character style: {e.characterStyle}, parser state: {e.state}, position: " +
                $"({firstLetter.StartBaseLine.X / DPI:N}\", {(e.page.Height - firstLetter.StartBaseLine.Y) / DPI:N}\"))");
            Console.WriteLine($"Font: {firstLetter.PointSize:N2}pt {firstLetter.FontName}");
        }
    }

    public static void parsePdf(string filename, ExtractedDocumentation xapi) {
        using PdfDocument pdf = PdfDocument.Open(filename);
        try {
            Console.WriteLine("Parsing xConfiguration section of PDF");
            parseSection(pdf, xapi.configurations);

            Console.WriteLine("Parsing xCommand section of PDF");
            parseSection(pdf, xapi.commands);

            Console.WriteLine("Parsing xStatus section of PDF");
            parseSection(pdf, xapi.statuses);
        } catch (ParsingException e) {
            Letter firstLetter = getFirstNonQuotationMarkLetter(e.word.Letters);
            Console.WriteLine($"Failed to parse page {e.page.Number}: {e.Message} (word: {e.word.Text}, character style: {e.characterStyle}, parser state: {e.state}, position: " +
                $"({firstLetter.StartBaseLine.X / DPI:N}\", {(e.page.Height - firstLetter.StartBaseLine.Y) / DPI:N}\"))");
            Console.WriteLine($"Font: {firstLetter.PointSize:N2}pt {firstLetter.FontName}");
            throw;
        }
    }

    private static void parseSection<T>(PdfDocument pdf, ICollection<T> xapiDestinationCollection) where T: AbstractCommand, new() {
        Range commandPages;

        if (typeof(T) == typeof(DocXConfiguration)) {
            commandPages = getPagesForSection(pdf, "xConfiguration commands", "xCommand commands");
        } else if (typeof(T) == typeof(DocXCommand)) {
            commandPages = getPagesForSection(pdf, "xCommand commands", "xStatus commands");
        } else if (typeof(T) == typeof(DocXStatus)) {
            commandPages = getPagesForSection(pdf, "xStatus commands", "Configuration");
            commandPages = commandPages.Start..(commandPages.End.Value -
                2); // Exclude the Chapter 6: Command overview page, which doesn't have its own bookmark despite having a top-level navigation button
        } else {
            throw new ArgumentOutOfRangeException(nameof(xapiDestinationCollection), typeof(T), "Unknown command type");
        }

        IEnumerable<(Word word, Page page)> wordsOnPages = getWordsOnPages(pdf, commandPages);

        ParserState state                = ParserState.START;
        double?     previousWordBaseline = default;
        Word?       previousWord         = default;

        ISet<string> requiredParameters = new HashSet<string>();
        string?      parameterName;
        Parameter?   parameter;
        string?      parameterDescription;
        string?      partialProductName;
        int          parameterUsageIndex;
        EnumValue?   enumValue;
        string?      statusValueSpace;
        string       enumListDelimiter;
        string?      partialEnumValue;

        resetMethodParsingState();

        void resetMethodParsingState() {
            partialProductName  = default;
            parameterUsageIndex = 0;
            statusValueSpace    = default;
            requiredParameters.Clear();
            resetParameterParsingState();
        }

        void resetParameterParsingState() {
            parameter            = default;
            parameterName        = default;
            parameterDescription = default;
            enumValue            = default;
            enumListDelimiter    = "/";
            partialEnumValue     = default;
        }

        T command = new();

        foreach ((Word word, Page page) in wordsOnPages) {
            CharacterStyle characterStyle = getCharacterStyle(word);

            // Console.WriteLine($"Parsing {word.Text}\t(character style = {characterStyle}, parser state = {state})");

            if (command is DocXStatus status && statusValueSpace is not null && characterStyle != CharacterStyle.VALUESPACE_OR_DISCLAIMER) {
                status.returnValueSpace = statusValueSpace switch {
                    "Integer" => new IntValueSpace(),
                    "String"  => new StringValueSpace(),

                    _ when Regex.Match(statusValueSpace, @"^Integer \((?<min>-?\d+)\.\.(?<max>-?\d+)\)$") is { Success: true } match => new IntValueSpace
                        { ranges = new List<IntRange> { new() { minimum = int.Parse(match.Groups["min"].Value), maximum = int.Parse(match.Groups["max"].Value) } } },
                    _ when Regex.Match(statusValueSpace, @"^(?<min>-?\d+)\.\.(?<max>-?\d+)$") is { Success: true } match => new IntValueSpace
                        { ranges = new List<IntRange> { new() { minimum = int.Parse(match.Groups["min"].Value), maximum = int.Parse(match.Groups["max"].Value) } } },

                    _ when statusValueSpace.Split(", ") is { Length: > 1 } split                                    => new EnumValueSpace { possibleValues = parseEnumValueSpacePossibleValues(split) },
                    _ when statusValueSpace.Split('/') is { Length: > 1 } split && !statusValueSpace.Contains("..") => new EnumValueSpace { possibleValues = parseEnumValueSpacePossibleValues(split) },
                    _ when statusValueSpace.Split('/') is { Length: > 1 } split && split.Contains("..")             => new EnumValueSpace { possibleValues = guessEnumRange(split) },
                    _ when Regex.Match(statusValueSpace, @"^Off/(?<min>-?\d+)\.\.(?<max>-?\d+)$") is { Success: true } match => new IntValueSpace
                        { optionalValue = "Off", ranges = new List<IntRange> { new() { minimum = int.Parse(match.Groups["min"].Value), maximum = int.Parse(match.Groups["max"].Value) } } },

                    _ => new EnumValueSpace { possibleValues = new HashSet<EnumValue> { new(statusValueSpace) } }
                    // _ => throw new ParsingException(word, state, characterStyle, page, "Could not parse xStatus returned value space " + statusValueSpace)
                };

                statusValueSpace = default;
            }

            // Omit duplicate methods
            // Sometimes Cisco documents the same exact method twice in the same PDF. Examples:
            //     xStatus Video Output Connector [n] Connected
            //     xStatus MediaChannels DirectShare [n] Channel [n] Netstat LastIntervalReceived
            if (state == ParserState.METHOD_NAME_HEADING && characterStyle != CharacterStyle.METHOD_NAME_HEADING &&
                xapiDestinationCollection.Any(cmd => cmd != command && cmd.name.Skip(1).SequenceEqual(command.name.Skip(1)))) {
                xapiDestinationCollection.Remove(command);
            }

            switch (characterStyle) {
                case CharacterStyle.METHOD_FAMILY_HEADING:
                    if (state == ParserState.USAGE_DEFAULT_VALUE && parameter is StringParameter && parameter.defaultValue is not null) {
                        parameter.defaultValue = parameter.defaultValue.TrimEnd('"');
                    }

                    resetMethodParsingState();

                    state = ParserState.START;
                    //skip, not useful information, we'll get the method name from the METHOD_NAME_HEADING below
                    break;
                case CharacterStyle.METHOD_NAME_HEADING:
                    // ReSharper disable once MergeIntoPattern broken null checking if you apply this suggestion
                    if (state == ParserState.USAGE_DEFAULT_VALUE && parameter is StringParameter && parameter.defaultValue is not null) {
                        parameter.defaultValue = parameter.defaultValue.TrimEnd('"');
                    }

                    if (state != ParserState.METHOD_NAME_HEADING) {
                        // finished previous method, moving to next method
                        command = new T();
                        xapiDestinationCollection.Add(command);
                        resetMethodParsingState();
                    }

                    if (state == ParserState.METHOD_NAME_HEADING && command is DocXStatus xStatus && Regex.Match(word.Text, @"\[(?<name>[a-z])\]") is { Success: true } match2) {
                        IntParameter indexParameter = new() {
                            indexOfParameterInName = command.name.Count,
                            required               = true,
                            name                   = match2.Groups["name"].Value // can be duplicated even in one method
                        };
                        xStatus.arrayIndexParameters.Add(indexParameter);
                        requiredParameters.Add(indexParameter.name);
                    }

                    state = ParserState.METHOD_NAME_HEADING;
                    command.name.Add(word.Text);
                    break;
                case CharacterStyle.PRODUCT_NAME:
                    switch (state) {
                        case ParserState.METHOD_NAME_HEADING when word.Text == "Applies":
                            state = ParserState.APPLIES_TO;
                            break;
                        case ParserState.APPLIES_TO when word.Text == "to:":
                            state = ParserState.APPLIES_TO_PRODUCTS;
                            break;
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
                        case ParserState.VALUESPACE or ParserState.USAGE_PARAMETER_VALUE_SPACE_APPLIES_TO:
                            state = ParserState.USAGE_PARAMETER_VALUE_SPACE_APPLIES_TO;
                            if (parameter is not null) {
                                parameter.valueSpaceDescription = appendWord(parameter.valueSpaceDescription, word, previousWordBaseline);
                            } else {
                                // This entire parameter, not just some of its values, only applies to specific products (xCommand Audio Volume Decrease Device:)
                                parameterDescription = appendWord(parameterDescription, word, previousWordBaseline);
                                // throw new ParsingException(word, state, characterStyle, page, "no parameter to append value space description to");
                            }

                            break;
                        case ParserState.VALUESPACE_TERM_DEFINITION:
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
                case CharacterStyle.USAGE_HEADING:
                    if (word.Text == "USAGE:") {
                        state = ParserState.USAGE_EXAMPLE;
                        if (command is DocXCommand xCommand && xCommand.description.Contains("multiline")) {
                            xCommand.parameters.Add(new StringParameter { name = "body", required = true });
                        }
                    } else {
                        throw new ParsingException(word, state, characterStyle, page, "unexpected word for character style");
                    }

                    break;
                case CharacterStyle.USAGE_EXAMPLE:
                    if (state == ParserState.USAGE_EXAMPLE) {
                        switch (command) {
                            case DocXConfiguration xConfiguration when Regex.Match(word.Text, @"^(?<prefix>\w*)\[(?<name>\w+)\]$") is { Success: true } match: {
                                IntParameter indexParameter = new() {
                                    indexOfParameterInName = parameterUsageIndex,
                                    required               = true,
                                    name                   = match.Groups["name"].Value,
                                    namePrefix             = match.Groups["prefix"].Value.EmptyToNull()
                                };
                                xConfiguration.parameters.Add(indexParameter);
                                requiredParameters.Add(indexParameter.name);
                                break;
                            }
                            case DocXConfiguration xConfiguration when Regex.Match(word.Text, @"^\[(?<min>-?\d+)\.\.(?<max>-?\d+)\]$") is { Success: true } match: {
                                IntParameter channelParameter = new() {
                                    indexOfParameterInName = parameterUsageIndex,
                                    required               = true,
                                    name                   = previousWord!.Text,
                                    ranges                 = { new IntRange { minimum = int.Parse(match.Groups["min"].Value), maximum = int.Parse(match.Groups["max"].Value) } }
                                };
                                xConfiguration.parameters.Add(channelParameter);
                                requiredParameters.Add(channelParameter.name);

                                break;
                            }
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
                                requiredParameters.Add(word.Text.Trim('"'));
                            }

                            // otherwise it's an optional parameter like [Channel: Channel]
                            parameterUsageIndex++;
                            break;
                        case ParserState.USAGE_PARAMETER_NAME or ParserState.VALUESPACE_TERM_DEFINITION when command is DocXConfiguration xConfiguration &&
                            xConfiguration.parameters.FirstOrDefault(p => word.Text == p.name + ':' && (p as IntParameter)?.indexOfParameterInName is not null) is { } _positionalParam:
                            parameter = _positionalParam;
                            state     = ParserState.USAGE_PARAMETER_DESCRIPTION;
                            break;
                        case ParserState.USAGE_PARAMETER_NAME or ParserState.VALUESPACE_TERM_DEFINITION or ParserState.USAGE_DEFAULT_VALUE or ParserState.VALUESPACE or
                            ParserState.USAGE_PARAMETER_VALUE_SPACE_APPLIES_TO or ParserState.USAGE_PARAMETER_DESCRIPTION:
                            // ReSharper disable once MergeIntoPattern - broken null checking if you apply this suggestion
                            if (parameter is StringParameter _param && _param.defaultValue is not null) {
                                _param.defaultValue = _param.defaultValue.TrimEnd('"');
                            }

                            resetParameterParsingState();
                            parameterName = word.Text.TrimEnd(':');
                            state         = ParserState.VALUESPACE;
                            break;
                        default:
                            throw new ParsingException(word, state, characterStyle, page, "unexpected state for character style");
                    }

                    break;
                case CharacterStyle.VALUESPACE_OR_DISCLAIMER:
                    switch (state) {
                        case ParserState.APPLIES_TO_PRODUCTS or ParserState.REQUIRES_USER_ROLE:
                            // ignore the "Not available for the Webex Devices Cloud xAPI service on personal mode devices" disclaimer
                            state = ParserState.REQUIRES_USER_ROLE;
                            break;
                        case ParserState.VALUESPACE when command is DocXStatus:
                            statusValueSpace = appendWord(statusValueSpace, word, previousWordBaseline);
                            break;
                        case ParserState.VALUESPACE or ParserState.USAGE_PARAMETER_VALUE_SPACE_APPLIES_TO:
                            Regex numericRangePattern = new(@"^(?<openparen>\()?(?<min>-?\d+)\.\.(?<max>-?\d+)(?<-openparen>\))?(?(openparen)(?!))$");
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

                                    (command as DocXConfiguration)?.parameters.Add(parameter);
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

                                    (command as DocXConfiguration)?.parameters.Add(parameter);
                                    parameterName = null;
                                    break;
                                case { } valueSpace when parameter is null && numericRangePattern.Match(valueSpace) is { Success: true } match:
                                    if (parameterName is null) {
                                        throw new ParsingException(word, state, characterStyle, page, "found parameter value space without a previously-parsed parameter name");
                                    }

                                    parameter = new IntParameter {
                                        name        = parameterName,
                                        required    = requiredParameters.Contains(parameterName),
                                        description = parameterDescription is not null ? parameterDescription + '\n' : string.Empty,
                                        ranges = {
                                            new IntRange {
                                                minimum = int.Parse(match.Groups["min"].Value),
                                                maximum = int.Parse(match.Groups["max"].Value)
                                            }
                                        }
                                    };

                                    (command as DocXConfiguration)?.parameters.Add(parameter);
                                    parameterName = null;

                                    break;
                                case { } enumList when parameter is null:
                                    if (parameterName is null) {
                                        throw new ParsingException(word, state, characterStyle, page, "found parameter value space without a previously-parsed parameter name");
                                    }

                                    if (enumList.EndsWith(',')) {
                                        enumListDelimiter = ",";
                                    }

                                    parameter = new EnumParameter {
                                        name           = parameterName,
                                        required       = requiredParameters.Contains(parameterName),
                                        description    = parameterDescription is not null ? parameterDescription + '\n' : string.Empty,
                                        possibleValues = parseEnumValueSpacePossibleValues(enumList, enumListDelimiter)
                                    };

                                    (command as DocXConfiguration)?.parameters.Add(parameter);
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
                                    if (enumListDelimiter == "," && (enumList.EndsWith('_') || enumList.EndsWith('/'))) {
                                        partialEnumValue += enumList;
                                    } else {
                                        IEnumerable<EnumValue> additionalValues = parseEnumValueSpacePossibleValues((partialEnumValue ?? "") + enumList, enumListDelimiter);
                                        foreach (EnumValue additionalValue in additionalValues) {
                                            _param.possibleValues.Add(additionalValue);
                                        }

                                        partialEnumValue = null;
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
                        case ParserState.DESCRIPTION_VALUE_SPACE_HEADING:

                            break;
                        default:
                            throw new ParsingException(word, state, characterStyle, page, "unexpected state for character style");
                    }

                    break;
                case CharacterStyle.VALUESPACE_TERM:
                    if ((parameter as EnumValues ?? (command as DocXStatus)?.returnValueSpace as EnumValues) is { } enumValues) {
                        enumValue = enumValues.possibleValues.FirstOrDefault(value => value.name == word.Text.TrimEnd(':'));
                        state     = ParserState.VALUESPACE_TERM_DEFINITION;
                    } else if (parameter is not null) {
                        parameter.description = appendWord(parameter.description, word, previousWordBaseline);
                        state                 = ParserState.USAGE_PARAMETER_DESCRIPTION;
                    } else {
                        // throw new ParsingException(word, state, characterStyle, page, $"found parameter enum term character style for non-enum parameter {parameter?.name ?? "(null param)"} of type {parameter?.type.ToString() ?? "null"}");
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
                        case ParserState.METHOD_NAME_HEADING when word.Text == "Applies":
                            state = ParserState.APPLIES_TO;
                            break;
                        case ParserState.APPLIES_TO when word.Text == "to:":
                            state = ParserState.APPLIES_TO_PRODUCTS;
                            break;
                        case ParserState.APPLIES_TO when word.Text == "Requires":
                            state = ParserState.REQUIRES_USER_ROLE;
                            break;
                        case ParserState.APPLIES_TO_PRODUCTS when word.Text == "Requires":
                            state = ParserState.REQUIRES_USER_ROLE;
                            break;
                        case ParserState.APPLIES_TO_PRODUCTS:
                            command.description = appendWord(command.description, word, previousWordBaseline);
                            state               = ParserState.DESCRIPTION;
                            break;
                        case ParserState.REQUIRES_USER_ROLE when word.Text is "role:":
                            state = ParserState.REQUIRES_USER_ROLE_ROLES;
                            break;
                        case ParserState.REQUIRES_USER_ROLE_ROLES when !isDifferentParagraph(word, previousWordBaseline):
                            foreach (string roleName in word.Text.TrimEnd(',').Split(',')) {
                                if (parseEnum<UserRole>(roleName) is { } role) {
                                    command.requiresUserRole.Add(role);
                                } else {
                                    throw new ParsingException(word, state, characterStyle, page, "role was not a recognized user role");
                                }
                            }

                            break;
                        case ParserState.USAGE_EXAMPLE:
                            if (word.Text == "where") {
                                state = ParserState.USAGE_PARAMETER_NAME;
                            } else {
                                throw new ParsingException(word, state, characterStyle, page, "unexpected word for state and character style");
                            }

                            break;
                        case ParserState.VALUESPACE or ParserState.VALUESPACE_DESCRIPTION or ParserState.VALUESPACE_TERM_DEFINITION
                            when isDifferentParagraph(word, previousWordBaseline) && word.Text == "Example:" && command is DocXStatus:
                            state = ParserState.USAGE_EXAMPLE;
                            break;
                        case ParserState.USAGE_PARAMETER_DESCRIPTION or ParserState.USAGE_PARAMETER_VALUE_SPACE_APPLIES_TO or ParserState.VALUESPACE or ParserState
                                .VALUESPACE_TERM_DEFINITION
                            when isDifferentParagraph(word, previousWordBaseline) && command.GetType() == typeof(DocXConfiguration) && word.Text == "Default":

                            state     = ParserState.USAGE_DEFAULT_VALUE_HEADING;
                            enumValue = null;
                            break;
                        case ParserState.USAGE_PARAMETER_DESCRIPTION or ParserState.USAGE_PARAMETER_VALUE_SPACE_APPLIES_TO or ParserState.VALUESPACE or ParserState
                                .VALUESPACE_TERM_DEFINITION
                            when isDifferentParagraph(word, previousWordBaseline) && command.GetType() == typeof(DocXConfiguration) && word.Text == "Range:" &&
                            parameter is IntParameter:

                            state = ParserState.VALUESPACE;
                            break;
                        case ParserState.USAGE_PARAMETER_DESCRIPTION or ParserState.USAGE_PARAMETER_VALUE_SPACE_APPLIES_TO or ParserState.VALUESPACE:
                            if (parameter is not null) {
                                if (parameter is IntParameter intParameter && Regex.Match(word.Text, @"^(?<min>-?\d+)\.\.(?<max>-?\d+)$") is { Success: true } match) {
                                    intParameter.ranges.Add(new IntRange { minimum = int.Parse(match.Groups["min"].Value), maximum = int.Parse(match.Groups["max"].Value) });
                                    state = ParserState.VALUESPACE_TERM_DEFINITION;
                                } else {
                                    parameter.description = appendWord(parameter.description, word, previousWordBaseline);
                                    state                 = ParserState.USAGE_PARAMETER_DESCRIPTION;
                                }

                            } else if (word.Text == ":" && state == ParserState.VALUESPACE) {
                                //the colon after the parameter name, usually it's part of the word with PARAMETER_NAME character style but sometimes it's tokenized as a separate word
                                //skip
                            } else if (command is DocXStatus _status) {
                                _status.returnValueSpace.description = appendWord(_status.returnValueSpace.description, word, previousWordBaseline);
                                state                                = ParserState.VALUESPACE_DESCRIPTION;
                            } else {
                                throw new ParsingException(word, state, characterStyle, page, "no current parameter to append description to");
                            }

                            break;
                        case ParserState.VALUESPACE_TERM_DEFINITION:
                            if (enumValue is not null) {
                                enumValue.description = appendWord(enumValue.description, word, previousWordBaseline);
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
                            if (word.Text == "Value" && isDifferentParagraph(word, previousWordBaseline) && command is DocXStatus) {
                                state = ParserState.DESCRIPTION_VALUE_SPACE_HEADING;
                            } else {
                                state               = ParserState.DESCRIPTION;
                                command.description = appendWord(command.description, word, previousWordBaseline);
                            }

                            break;
                        case ParserState.DESCRIPTION_VALUE_SPACE_HEADING when command is DocXStatus && !isDifferentParagraph(word, previousWordBaseline):
                            if (word.Text == "returned:") {
                                state = ParserState.VALUESPACE;
                            } else if (!XSTATUS_DESCRIPTION_VALUE_SPACE_HEADING_WORDS.Contains(word.Text)) {
                                throw new ParsingException(word, state, characterStyle, page, "xStatus description contained a paragraph that started looking like it was the \"Value space of " +
                                    $"the result returned:\" heading, but it wasn't because it contained the word {word.Text}. Please implement a buffer for this situation to put this paragraph in " +
                                    "the description.");
                            } else {
                                //skip word, we don't care about the "Value space of the result returned:" text
                            }

                            break;
                        default:
                            break;
                    }

                    break;
                default:
                    throw new ArgumentOutOfRangeException(characterStyle.ToString());
            }

            previousWordBaseline = word.Letters[0].StartBaseLine.Y;
            previousWord         = word;
        }
    }

    private static ISet<EnumValue> guessEnumRange(IList<string> split) {
        split = new List<string>(split); // in case it was fixed-size from string[]
        IEnumerable<string> allValues = split;

        int     ellipsisIndex = split.IndexOf("..");
        string  lowerBound    = split[ellipsisIndex - 1];
        string  upperBound    = split[ellipsisIndex + 1];
        string? boundPrefix   = null, boundSuffix = null;
        int     boundIndex;

        for (boundIndex = 0; boundIndex < Math.Min(lowerBound.Length, upperBound.Length); boundIndex++) {
            if (lowerBound[boundIndex] != upperBound[boundIndex]) {
                boundPrefix = lowerBound[..boundIndex];

                for (boundIndex = 1; Math.Min(lowerBound.Length, upperBound.Length) - boundIndex > boundPrefix.Length - 1; boundIndex++) {
                    if (lowerBound[^boundIndex] != upperBound[^boundIndex]) {
                        boundSuffix = lowerBound[^(boundIndex - 1)..];
                        break;
                    }
                }

                break;
            }
        }

        if (boundPrefix != null && boundSuffix != null) {
            int lower = int.Parse(lowerBound[boundPrefix.Length..^boundSuffix.Length]);
            int upper = int.Parse(upperBound[boundPrefix.Length..^boundSuffix.Length]);

            split.RemoveAt(ellipsisIndex);
            IEnumerable<string> intermediateValues = Enumerable.Range(lower + 1, upper - lower - 1).Select(i => $"{boundPrefix}{i:N0}{boundSuffix}");
            allValues = split.Insert(intermediateValues, ellipsisIndex);
        }

        return Enumerable.ToHashSet(allValues.Select(s => new EnumValue(s)));
    }

    private static ISet<EnumValue> parseEnumValueSpacePossibleValues(string enumList, string delimiter = "/") =>
        parseEnumValueSpacePossibleValues(enumList.TrimEnd(')').Split(delimiter, StringSplitOptions.RemoveEmptyEntries));

    private static ISet<EnumValue> parseEnumValueSpacePossibleValues(IEnumerable<string> enumList) => Enumerable.ToHashSet(enumList.Select(value => new EnumValue(value)));

    private static string appendWord(string? head, Word tail, double? previousWordBaseline) {
        double baselineDifference = getBaselineDifference(tail, previousWordBaseline);
        bool   isDifferentLine    = baselineDifference > 3;
        string wordSeparator;

        if (string.IsNullOrWhiteSpace(head)) {
            wordSeparator = string.Empty;
        } else if (isDifferentLine && (head.EndsWith('-') || head.EndsWith('/'))) {
            head          = head.TrimEnd('-', '/');
            wordSeparator = string.Empty;
        } else if (isDifferentParagraph(baselineDifference)) {
            wordSeparator = "\n";
        } else {
            wordSeparator = " ";
        }

        return (head ?? string.Empty) + wordSeparator + tail.Text;
    }

    private static double getBaselineDifference(Word tail, double? previousWordBaseline) {
        return previousWordBaseline is not null ? Math.Abs(tail.Letters[0].StartBaseLine.Y - (double) previousWordBaseline) : 0;
    }

    private static bool isDifferentParagraph(double baselineDifference) => baselineDifference > 10;
    private static bool isDifferentParagraph(Word   word, double? previousWordBaseline) => isDifferentParagraph(getBaselineDifference(word, previousWordBaseline));

    private static IEnumerable<(Word word, Page page)> getWordsOnPages(PdfDocument pdf, Range pageIndices) {
        int[] pageNumbers = Enumerable.Range(0, pdf.NumberOfPages).ToArray()[pageIndices];
        foreach (int pageNumber in pageNumbers) {
            foreach (bool readLeftSide in new[] { true, false }) {
                Page           page          = pdf.GetPage(pageNumber);
                IWordExtractor wordExtractor = FixedDefaultWordExtractor.INSTANCE;
                IReadOnlyList<Letter> lettersWithUnfuckedQuotationMarks = page.Letters
                    .Where(letter => isTextOnHalfOfPage(letter, page, readLeftSide))
                    .Select(letter => new Letter(
                        letter.Value,
                        letter.GlyphRectangle,
                        // when Cisco made the monospaced quotation marks bigger, loss of floating-point precision lowered the baseline enough to mess up the letter order relied upon by the DefaultWordExtractor
                        new PdfPoint(letter.StartBaseLine.X, Math.Round(letter.StartBaseLine.Y, 3)),
                        new PdfPoint(letter.EndBaseLine.X, Math.Round(letter.EndBaseLine.Y, 3)),
                        letter.Width,
                        letter.FontSize,
                        letter.Font,
                        letter.Color,
                        letter is { Value: "\"", PointSize: 9.6, FontName: var fontName } && fontName.EndsWith("CourierNewPSMT") ? 8.8 : letter.PointSize,
                        letter.TextSequence)
                    ).ToImmutableList();
                IEnumerable<Word> words = wordExtractor.GetWords(lettersWithUnfuckedQuotationMarks);

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

    internal static CharacterStyle getCharacterStyle(Word word) {
        return getFirstNonQuotationMarkLetter(word.Letters) switch {
            { PointSize: 16.0 }                                                                                                  => CharacterStyle.METHOD_FAMILY_HEADING,
            { PointSize: 10.0 }                                                                                                  => CharacterStyle.METHOD_NAME_HEADING,
            { FontName: var font, Color: var color } when font.EndsWith("CiscoSansTT-Oblique") && color.Equals(PRODUCT_NAME_COLOR) => CharacterStyle.PRODUCT_NAME,
            { PointSize: 8.0, FontName: var font } when font.EndsWith("CiscoSansTT")                                               => CharacterStyle.USAGE_HEADING,
            { PointSize: 8.8 or 9.6, FontName: var font } when font.EndsWith("CourierNewPSMT")                                   => CharacterStyle.USAGE_EXAMPLE,
            { PointSize: 8.8, FontName: var font } when font.EndsWith("CourierNewPS-ItalicMT")                                   => CharacterStyle.PARAMETER_NAME,
            { PointSize: 8.0, FontName: var font } when font.EndsWith("CiscoSansTTLight-Oblique")                             => CharacterStyle.VALUESPACE_OR_DISCLAIMER,
            { PointSize: 8.0, FontName: var font } when font.EndsWith("CiscoSansTT-Oblique")                                       => CharacterStyle.VALUESPACE_TERM,
            _                                                                                                                    => CharacterStyle.BODY
        };
    }

    private static T? parseEnum<T>(string text) where T: struct, Enum => Enum.IsDefined(typeof(T), text) && Enum.TryParse(text, true, out T result) ? result : null;

    private static Product? parseProduct(string text) => parseEnum<Product>(text.Replace('/', '_'));

    internal static Letter getFirstNonQuotationMarkLetter(IReadOnlyList<Letter> letters) {
        return letters.SkipWhile(letter => letter.Value == "\"").FirstOrDefault(letters[0]);
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
    USAGE_HEADING,
    USAGE_EXAMPLE,
    PARAMETER_NAME,
    VALUESPACE_OR_DISCLAIMER,
    VALUESPACE_TERM,
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
    USAGE_EXAMPLE,                          //the xCommand or similar example invocation, directly below the "USAGE:" heading
    USAGE_PARAMETER_NAME,                   //the name of the parameter underneath the "where"
    USAGE_PARAMETER_DESCRIPTION,            //the regular description of the parameter, underneath the valuespace summary and above the value space descriptions
    VALUESPACE,                             //the italic text for each parameter that says whether it's an Integer, String, or the slash-separated enum values
    VALUESPACE_TERM_DEFINITION,             //the text to the right of the bold enum value name that describes the parameter value
    USAGE_PARAMETER_VALUE_SPACE_APPLIES_TO, //says which products a value space applies to, as italic text to the right of the valuespace
    USAGE_DEFAULT_VALUE_HEADING,            //the xConfiguration "Default value:" heading
    USAGE_DEFAULT_VALUE,                    //the xConfiguration default valuespace
    DESCRIPTION_VALUE_SPACE_HEADING,        //the xStatus "Value space of the result returned:" heading
    VALUESPACE_DESCRIPTION                  //the xStatus explanation of the valuespace below the italic text, which can include bold terms and definitions or just free text

}