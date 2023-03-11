namespace CiscoEndpointCertificateDeployer.Exceptions;

public class CiscoException: Exception {

    public CiscoException(string message, Exception? innerException = null): base(message, innerException) { }

    public class AuthenticationFailed: CiscoException {

        public AuthenticationFailed(string message): base(message) { }

    }

    public class WrongOsMajorVersionDeployer: CiscoException {

        public WrongOsMajorVersionDeployer(string message): base(message) { }

    }

}