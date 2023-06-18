namespace csxapi;

public class SampleUsage {

    private readonly ICommands _commands = new Commands();

    public async Task Dial() {
        await _commands.Dial(number: "10990@bjn.vc", protocol: DialProtocol.Sip, callRate: 6000, callType: DialCallType.Video).ConfigureAwait(false);
    }

}