using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace CiscoEndpointDocumentationApiExtractor.Extraction;

public class ExtractedDocumentation {

    public ICollection<DocXCommand> commands { get; set; } = new List<DocXCommand>();
    public ICollection<DocXConfiguration> configurations { get; set; } = new List<DocXConfiguration>();
    public ICollection<DocXStatus> statuses { get; set; } = new List<DocXStatus>();
    public ICollection<DocXEvent> events { get; set; } = new List<DocXEvent>();

}

public interface IPathNamed {

    public IList<string> name { get; set; }
    public IList<string> nameWithoutBrackets { get; }

}

public abstract class AbstractCommand: IPathNamed {

    public IList<string> name { get; set; } = new List<string>();
    public virtual IList<string> nameWithoutBrackets => name;
    public ISet<Product> appliesTo { get; set; } = new HashSet<Product>();
    public ISet<UserRole> requiresUserRole { get; set; } = new HashSet<UserRole>();
    public string description { get; set; } = string.Empty;

    public override string ToString() {
        return string.Join(" ", name);
    }

}

public enum UserRole {

    ADMIN,
    INTEGRATOR,
    USER,
    AUDIT,
    ROOMCONTROL

}

[SuppressMessage("ReSharper", "InconsistentNaming")]
public enum Product {

    Board,
    BoardPro,
    CodecEQ,
    CodecPlus,
    CodecPro,
    DeskPro,
    DeskMini,
    Desk,
    Room55,
    Room70,
    Room55D,
    Room70G2,
    RoomBar,
    RoomKit,
    RoomKitMini,
    RoomPanorama,
    Room70Panorama

}

public class DocXConfiguration: AbstractCommand {

    public ICollection<Parameter> parameters { get; set; } = new List<Parameter>();

    public override IList<string> nameWithoutBrackets =>
        // name.Where((s, i) => !parameters.Any(parameter => parameter is IntParameter { indexOfParameterInName: { } paramIndex } && paramIndex == i)).ToList();
        name.Select((s, i) => parameters.Any(parameter => parameter is IntParameter { indexOfParameterInName: { } paramIndex } && paramIndex == i) ? "N" : s).ToList();

}

public abstract class Parameter {

    public string name { get; set; } = default!;
    public string description { get; set; } = string.Empty;
    public string? valueSpaceDescription { get; set; }
    public bool required { get; set; }
    public string? defaultValue { get; set; }
    public ISet<Product>? appliesTo { get; set; }

    public abstract DataType type { get; }

}

public enum DataType {

    INTEGER,
    STRING,
    ENUM

}

public class IntParameter: Parameter {

    public int? indexOfParameterInName { get; set; }
    public ICollection<IntRange> ranges { get; set; } = new List<IntRange>();
    public override DataType type => DataType.INTEGER;
    public string? namePrefix { get; set; }

}

internal class EnumParameter: Parameter, EnumValues {

    public ISet<EnumValue> possibleValues { get; set; } = default!;
    public override DataType type => DataType.ENUM;

}

public class EnumValue {

    public EnumValue(string name) {
        this.name = name;
    }

    public string name { get; set; }
    public string? description { get; set; }

    protected bool Equals(EnumValue other) {
        return string.Equals(name, other.name, StringComparison.InvariantCultureIgnoreCase);
    }

    public override bool Equals(object? obj) {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != GetType()) return false;
        return Equals((EnumValue) obj);
    }

    public override int GetHashCode() {
        return StringComparer.InvariantCultureIgnoreCase.GetHashCode(name);
    }

    public static bool operator ==(EnumValue? left, EnumValue? right) {
        return Equals(left, right);
    }

    public static bool operator !=(EnumValue? left, EnumValue? right) {
        return !Equals(left, right);
    }

}

internal class StringParameter: Parameter {

    public int minimumLength { get; set; }
    public int maximumLength { get; set; }
    public override DataType type => DataType.STRING;

}

public class DocXCommand: DocXConfiguration { }

public class DocXStatus: AbstractCommand {

    public ICollection<IntParameter> arrayIndexParameters { get; } = new List<IntParameter>();
    public ValueSpace returnValueSpace { get; set; } = default!;

    public override IList<string> nameWithoutBrackets =>
        // name.Where((s, i) => !arrayIndexParameters.Any(parameter => parameter is { indexOfParameterInName: { } paramIndex } && paramIndex == i)).ToList();
        name.Select((s, i) => arrayIndexParameters.Any(parameter => parameter is { indexOfParameterInName: { } paramIndex } && paramIndex == i) ? "N" : s).ToList();

}

public abstract class ValueSpace {

    public abstract DataType type { get; }
    public string? description { get; set; }

}

internal class IntValueSpace: ValueSpace {

    public ICollection<IntRange> ranges = new List<IntRange>();
    public override DataType type => DataType.INTEGER;

    /// <summary>
    /// How the null value for this int valuespace is serialized.
    /// For example, xStatus Network [n] VLAN Voice VlanId returns an integer in the range [1, 4094], or the string "Off" if the VLAN Voice Mode is not enabled.
    /// If set, this string will get translated to null when reading this status. In most cases, this property should be null.
    /// </summary>
    public string? optionalValue { get; set; }

}

internal interface EnumValues {

    ISet<EnumValue> possibleValues { get; set; }

}

internal class EnumValueSpace: ValueSpace, EnumValues {

    public ISet<EnumValue> possibleValues { get; set; } = default!;
    public override DataType type => DataType.ENUM;

}

internal class StringValueSpace: ValueSpace {

    public override DataType type => DataType.STRING;

}

public class IntRange {

    public int minimum { get; set; }
    public int maximum { get; set; }
    public string? description { get; set; }
    public ISet<Product> appliesTo { get; set; } = new HashSet<Product>();

}

public class DocXEvent: IEventParent, IPathNamed {

    public IList<string> name { get; set; } = new List<string>();
    public IList<string> nameWithoutBrackets => name;

    // TODO parameters are actually not just a flat list, they can be arbitrarily nested (up to 6 layers deep in practice)
    // see "csxapi todo.txt"
    // This is a problem because not only do we have to generate a very large graph of objects at compile time and again at runtime,
    // but we also must handle numeric indices in the result path somehow, which aren't 0-indexed and are sparse, so we can't just use a List
    // Maybe an IDictionary<int, object> would be better, because then consumers can use Indexer accessors or get all the entries if they want to enumerate them
    // public ICollection<Parameter> parameters { get; set; } = new List<Parameter>();

    public IList<EventChild> children { get; set; } = new List<EventChild>();

    public ISet<UserRole> requiresUserRole { get; set; } = new HashSet<UserRole>();
    public EventAccess access { get; set; }

}

public enum EventAccess {

    PUBLIC_API,
    PUBLIC_API_PREVIEW,
    INTERNAL,
    INTERNAL_RESTRICTED

}

public static class EventAccessParser {

    public static EventAccess parse(string serialized) => serialized.ToLowerInvariant() switch {
        "public-api"          => EventAccess.PUBLIC_API,
        "public-api-preview"  => EventAccess.PUBLIC_API_PREVIEW,
        "internal"            => EventAccess.INTERNAL,
        "internal-restricted" => EventAccess.INTERNAL_RESTRICTED,
    };

}

public abstract class EventChild: IPathNamed {

    public IList<string> name { get; set; }
    public IList<string> nameWithoutBrackets => name;

}

public interface IEventParent: IPathNamed {

    IList<EventChild> children { get; set; }

}

public class ListContainer: EventChild, IEventParent {

    public IList<EventChild> children { get; set; } = new List<EventChild>();

}

public class ObjectContainer: EventChild, IEventParent {

    public IList<EventChild> children { get; set; } = new List<EventChild>();
    public bool required { get; set; } = true;

}

public abstract class ValueChild: EventChild {

    public abstract DataType type { get; }
    public abstract bool required { get; set; }

}

public class StringChild: ValueChild {

    public override DataType type => DataType.STRING;
    public override bool required { get; set; }

}

public class IntChild: ValueChild {

    public override DataType type => DataType.INTEGER;
    public override bool required { get; set; }

    /// <summary>
    /// This is not a named property inside an event.
    /// Instead, the entire event body is just this value. For example,
    ///
    /// <c>
    /// {
    ///     "Standby": {
    ///         "SecondsToStandby": 30,
    ///         "id": 1
    ///     }
    /// }
    /// </c>
    /// Used by <c>Standby/SecondsToStandby</c> and <c>RoomReset/SecondsToReset</c>.
    /// </summary>
    public bool implicitAnonymousSingleton { get; set; } = false;

}

public class EnumChild: ValueChild {

    public override DataType type => DataType.ENUM;
    public override bool required { get; set; }
    public ISet<EnumValue> possibleValues { get; set; } = default!;

}