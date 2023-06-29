using System;
using System.Collections.Generic;
using System.Linq;

namespace CiscoEndpointDocumentationApiExtractor.Extraction;

public class ExtractedDocumentation {

    public ICollection<DocXCommand> commands { get; set; } = new List<DocXCommand>();
    public ICollection<DocXConfiguration> configurations { get; set; } = new List<DocXConfiguration>();
    public ICollection<DocXStatus> statuses { get; set; } = new List<DocXStatus>();

}

public abstract class AbstractCommand {

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

internal class EnumValue {

    public EnumValue(string name) {
        this.name = name;
    }

    public string name { get; set; } = default!;
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