using CiscoEndpointDocumentationApiExtractor.Extraction;

namespace CiscoEndpointDocumentationApiExtractor.Generation;

public interface InterfaceChild { }

public class InterfaceMethod: InterfaceChild {

    public InterfaceMethod(AbstractCommand command) {
        this.command = command;
    }

    public AbstractCommand command { get; }

    private bool Equals(InterfaceMethod other) {
        return command.Equals(other.command);
    }

    public override bool Equals(object? obj) {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != GetType()) return false;
        return Equals((InterfaceMethod) obj);
    }

    public override int GetHashCode() {
        return command.GetHashCode();
    }

    public static bool operator ==(InterfaceMethod? left, InterfaceMethod? right) {
        return Equals(left, right);
    }

    public static bool operator !=(InterfaceMethod? left, InterfaceMethod? right) {
        return !Equals(left, right);
    }

}

public class Subinterface: InterfaceChild {

    public Subinterface(string interfaceName, string getterName) {
        this.interfaceName = interfaceName;
        this.getterName    = getterName;
    }

    public string interfaceName { get; }
    public string getterName { get; }

    private bool Equals(Subinterface other) {
        return interfaceName == other.interfaceName;
    }

    public override bool Equals(object? obj) {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != GetType()) return false;
        return Equals((Subinterface) obj);
    }

    public override int GetHashCode() {
        return interfaceName.GetHashCode();
    }

    public static bool operator ==(Subinterface? left, Subinterface? right) {
        return Equals(left, right);
    }

    public static bool operator !=(Subinterface? left, Subinterface? right) {
        return !Equals(left, right);
    }

}