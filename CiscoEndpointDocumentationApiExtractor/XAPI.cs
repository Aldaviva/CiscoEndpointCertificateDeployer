using System;
using System.Collections.Generic;

namespace CiscoEndpointDocumentationApiExtractor {

    internal class XAPI {

        public ICollection<XCommand> commands { get; set; } = new List<XCommand>();
        public ICollection<XConfiguration> configurations { get; set; } = new List<XConfiguration>();
        public ICollection<XStatus> statuses { get; set; } = new List<XStatus>();

    }

    internal abstract class AbstractCommand {

        public IList<string> name { get; set; } = new List<string>();
        public ISet<Product> appliesTo { get; set; } = new HashSet<Product>();
        public ISet<UserRole> requiresUserRole { get; set; } = new HashSet<UserRole>();
        public string description { get; set; } = string.Empty;

    }

    internal enum UserRole {

        ADMIN,
        INTEGRATOR,
        USER,
        AUDIT,
        ROOMCONTROL

    }

    internal enum Product {

        SX80,
        SX20,
        SX10,
        MX700_MX800_MX800D,
        MX200G2_MX300G2,
        CODECPRO,
        CODECPLUS,
        ROOM70G2,
        ROOM70_ROOM55D,
        ROOM55,
        ROOMKIT,
        ROOMKITMINI,
        ROOMPANORAMA_ROOM70PANORAMA,
        DX70_DX80,
        DESKPRO,
        BOARDS

    }

    internal class XConfiguration: AbstractCommand {

        public ICollection<Parameter> parameters { get; set; } = new List<Parameter>();

    }

    internal abstract class Parameter {

        public string name { get; set; } = default!;
        public string description { get; set; } = string.Empty;
        public string? valueSpaceDescription { get; set; }
        public bool required { get; set; }
        public string? defaultValue { get; set; }
        public ISet<Product>? appliesTo { get; set; }

        public abstract DataType type { get; }

    }

    internal enum DataType {

        INTEGER,
        STRING,
        ENUM

    }

    internal class IntParameter: Parameter {

        public int? arrayIndexItemParameterPosition { get; set; }
        public ICollection<IntRange> ranges { get; set; } = new List<IntRange>();
        public override DataType type => DataType.INTEGER;

    }

    internal class EnumParameter: Parameter {

        public ISet<EnumValue> possibleValues { get; set; } = default!;
        public override DataType type => DataType.ENUM;

    }

    internal class EnumValue {

        public string name { get; set; } = default!;
        public string? description { get; set; }

    }

    internal class StringParameter: Parameter {

        public int minimumLength { get; set; }
        public int maximumLength { get; set; }
        public override DataType type => DataType.STRING;

    }

    internal class XCommand: XConfiguration { }

    internal class XStatus: AbstractCommand {

        public ICollection<IntParameter> arrayIndexParameters { get; set; } = new List<IntParameter>();
        public ValueSpace returnValueSpace { get; set; } = default!;

    }

    internal class ValueSpace { }

    internal abstract class ValueSpace<T>: ValueSpace {

        public DataType type { get; set; }

    }

    internal class IntValueSpace: ValueSpace<int> {

        public ICollection<IntRange> ranges = new List<IntRange>();

    }

    internal class EnumValueSpace: ValueSpace<Enum> {

        public ISet<EnumValue> possibleValues { get; set; } = default!;

    }

    internal class StringValueSpace: ValueSpace<string> { }

    internal class IntRange {

        public int minimum { get; set; }
        public int maximum { get; set; }

    }

}