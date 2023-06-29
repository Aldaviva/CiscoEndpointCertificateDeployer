using CSxAPI.Enums;

namespace CSxAPI;

public class SampleUsage {

    private readonly ICommands _commands = new Commands();
    private readonly IConfigurations _configuration = new Configuration();
    private readonly IStatuses _status = new Status();

    public async Task Dial() {
        await _configuration.Conference.DefaultCall.Protocol(ConfigurationConferenceDefaultCallProtocol.Sip).ConfigureAwait(false);
        ConfigurationConferenceDefaultCallProtocol defaultCallProtocol = await _configuration.Conference.DefaultCall.Protocol().ConfigureAwait(false);
        _configuration.Conference.DefaultCall.ProtocolChanged += value => Console.WriteLine($"Default signaling protocol changed to {value}");

        ConfigurationTimeZone z = ConfigurationTimeZone.America_Los_Angeles;

        await _commands.Dial(number: "10990@bjn.vc", protocol: CommandDialProtocol.Sip, callRate: 6000, callType: CommandDialCallType.Video).ConfigureAwait(false);

        StatusAudioMicrophonesMute muteState = await _status.Audio.Microphones.Mute().ConfigureAwait(false);
        Console.WriteLine(muteState switch {
            StatusAudioMicrophonesMute.On => "muted",
            StatusAudioMicrophonesMute.Off => "unmuted"
        });
    }

}