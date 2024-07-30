using System.Diagnostics.CodeAnalysis;
using System.Text;
using CiscoEndpointCertificateDeployer.Data;
using CiscoEndpointCertificateDeployer.Exceptions;
using Renci.SshNet;
using Renci.SshNet.Common;

namespace CiscoEndpointCertificateDeployer.Clients;

public class XacliClient(Endpoint endpoint): IDisposable {

    private const string XACLI_OK = "\r\nOK\r\n";

    private static readonly Encoding UTF8_DESERIALIZING = new UTF8Encoding(true, true);

    private Endpoint endpoint { get; } = endpoint;

    private SshClient? sshClient { get; set; }
    public ShellStream? shell { get; private set; }

    private volatile bool disposed;

    public void logIn() {
        ObjectDisposedException.ThrowIf(disposed, this);

        KeyboardInteractiveConnectionInfo keyboardInteractiveConnectionInfo = new(endpoint.host, endpoint.username);
        keyboardInteractiveConnectionInfo.AuthenticationPrompt += (_, eventArgs) => {
            foreach (AuthenticationPrompt prompt in eventArgs.Prompts) {
                prompt.Response = endpoint.password;
            }
        };

        sshClient = new SshClient(keyboardInteractiveConnectionInfo);
        sshClient.Connect();

        shell              =  sshClient.CreateShellStream("", 80, 24, 80, 24, 1024);
        shell.DataReceived += (_, args) => Console.WriteLine(UTF8_DESERIALIZING.GetString(args.Data).Replace("\r", "[\\r]").Replace("\n", "[\\n]"));
        waitForOkResponse(); //wait for login OK
    }

    public void writeLine(string line) {
        ensureLoggedInAndNotDisposed();
        Console.WriteLine(line);
        shell.WriteLine(line);
    }

    public string waitForOkResponse() {
        return expect(XACLI_OK);
    }

    private string expect(string expectation) {
        ensureLoggedInAndNotDisposed();
        return shell.Expect(expectation) ?? string.Empty;
    }

    /*
    /// <summary>Deadlocks for some reason. Seems undocumented and untested.</summary>
    [Obsolete]
    private Task<string> expectAsync(string expectation, Action<string>? onExpectationFulfilled = default) {
        ensureLoggedInAndNotDisposed();
        onExpectationFulfilled ??= _ => { };
        // Console.WriteLine($"Waiting for {expectation.Trim()}");
        return Task.Factory.FromAsync((actions, callback, o) => shell.BeginExpect(callback, o, actions), result => shell.EndExpect(result),
            new[] { new ExpectAction(expectation, onExpectationFulfilled) }, null);
    }*/

    [MemberNotNull(nameof(shell))]
    private void ensureLoggedInAndNotDisposed() {
        if (disposed) {
            throw new ObjectDisposedException("XacliClient instance has already been disposed and cannot be reused.");
        } else if (shell is null) {
            throw new CiscoException("Log in before calling methods on XacliClient");
        }
    }

    protected virtual void Dispose(bool disposing) {
        if (disposing && !disposed) {
            disposed = true;
            sshClient?.Dispose();
            shell?.Dispose();
        }
    }

    public void Dispose() {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

}