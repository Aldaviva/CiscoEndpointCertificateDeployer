using CSxAPI.Enums;
using TimeZone = CSxAPI.Enums.TimeZone;

namespace CSxAPI;

public class SampleUsage {

    private readonly ICommands _commands = new Commands();
    private readonly IConfigurations _configuration = new Configuration();

    public async Task Dial() {
        await _configuration.Conference.DefaultCall.Protocol(ConferenceDefaultCallProtocol.Sip).ConfigureAwait(false);
        string defaultCallProtocol = await _configuration.Conference.DefaultCall.Protocol().ConfigureAwait(false);

        TimeZone z = TimeZone.America_Los_Angeles;

        await _commands.Dial(number: "10990@bjn.vc", protocol: DialProtocol.Sip, callRate: 6000, callType: DialCallType.Video).ConfigureAwait(false);
    }

}