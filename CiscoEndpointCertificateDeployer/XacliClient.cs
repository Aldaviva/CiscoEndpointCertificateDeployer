using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Renci.SshNet;
using Renci.SshNet.Common;

namespace CiscoEndpointCertificateDeployer {

    public class XacliClient: IDisposable {

        private const string XACLI_OK = "\r\nOK\r\n";

        private Endpoint endpoint { get; }

        private SshClient? sshClient { get; set; }
        public ShellStream? shell { get; private set; }

        private volatile bool disposed;

        public XacliClient(Endpoint endpoint) {
            this.endpoint = endpoint;
        }

        public async Task logIn() {
            if (disposed) {
                throw new ObjectDisposedException("XacliClient instance has already been disposed and cannot be reused.");
            }

            KeyboardInteractiveConnectionInfo keyboardInteractiveConnectionInfo = new(endpoint.host, endpoint.username);
            keyboardInteractiveConnectionInfo.AuthenticationPrompt += (_, eventArgs) => {
                foreach (AuthenticationPrompt prompt in eventArgs.Prompts) {
                    prompt.Response = endpoint.password;
                }
            };

            sshClient = new SshClient(keyboardInteractiveConnectionInfo);
            sshClient.Connect();

            shell = sshClient.CreateShellStream("", 80, 24, 80, 24, 1024);
            await waitForOkResponse(); //wait for login OK
        }

        public void writeLine(string line) {
            ensureLoggedInAndNotDisposed();
            shell.WriteLine(line);
        }

        public async Task<string> waitForOkResponse() {
            return await expect(XACLI_OK);
        }

        private async Task<string> expect(string expectation, Action<string>? onExpectationFulfilled = default) {
            ensureLoggedInAndNotDisposed();
            return await Task.Factory.FromAsync((actions, callback, o) => shell.BeginExpect(callback, o, actions), result => shell.EndExpect(result),
                new[] { new ExpectAction(expectation, onExpectationFulfilled) }, null);
        }

        [MemberNotNull(nameof(shell))]
        private void ensureLoggedInAndNotDisposed() {
            if (disposed) {
                throw new ObjectDisposedException("XacliClient instance has already been disposed and cannot be reused.");
            } else if (shell is null) {
                throw new CiscoException("Log in before calling methods on XacliClient");
            }
        }

        public void Dispose() {
            disposed = true;
            shell?.Dispose();
            sshClient?.Dispose();
        }

    }

}