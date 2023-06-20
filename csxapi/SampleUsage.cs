namespace CSxAPI;

public class SampleUsage {

    private readonly ICommands _commands = new Commands();

    public async Task Dial() {
        // await _configuration.Conference.DefaultCall.Protocol(ConferenceDefaultCallProtocol.Sip);
        // ConferenceDefaultCallProtocol defaultCallProtocol = await _configuration.Conference.DefaultCall.Protocol();

        TimeZoneZone z = TimeZoneZone.AfricaⳆAbidjan;

        await _commands.Dial(number: "10990@bjn.vc", protocol: DialProtocol.Sip, callRate: 6000, callType: DialCallType.Video).ConfigureAwait(false);
    }

}