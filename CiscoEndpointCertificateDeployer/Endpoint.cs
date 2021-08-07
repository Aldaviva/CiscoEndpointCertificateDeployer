namespace CiscoEndpointCertificateDeployer {

    public class Endpoint {

        public string host { get; set; }
        public string username { get; set; }
        public string password { get; set; }

        public Endpoint(string host, string username, string password) {
            this.host     = host;
            this.username = username;
            this.password = password;
        }

    }

}