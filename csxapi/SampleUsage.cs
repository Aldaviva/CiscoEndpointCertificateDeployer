namespace CSxAPI;

public class SampleUsage {

    private readonly ICommands _commands = new Commands();
    private readonly IConfigurations _configuration = new Configuration();
    private readonly IStatuses _status = new Status();
    private readonly IEvents _events;

    public async Task Dial() {
        _events.Audio.Input.Connectors += data => Console.WriteLine($"VU meter: {data.Microphone.First().Value.VuMeter}");

        _events.UserInterface.Message.Prompt.Response += data => {
            if (data.FeedbackId == "1") {
                Console.WriteLine($"Option ID: {data.OptionId}");
            }
        };

        _events.Conference.ParticipantList.ParticipantAdded += value =>
            Console.WriteLine(value.Device[1].EndToEndEncryption?.CertificateChain.Certificate[1].Subject.First().Value.Name);

        // await _configuration.Conference.DefaultCall.Protocol(ConfigurationConferenceDefaultCallProtocol.Sip).ConfigureAwait(false);
        // ConfigurationConferenceDefaultCallProtocol defaultCallProtocol = await _configuration.Conference.DefaultCall.Protocol().ConfigureAwait(false);
        // _configuration.Conference.DefaultCall.ProtocolChanged += value => Console.WriteLine($"Default signaling protocol changed to {value}");
        //
        // ConfigurationTimeZone z = ConfigurationTimeZone.America_Los_Angeles;
        //
        // await _commands.Dial(number: "10990@bjn.vc", protocol: CommandDialProtocol.Sip, callRate: 6000, callType: CommandDialCallType.Video).ConfigureAwait(false);
        //
        // StatusAudioMicrophonesMute muteState = await _status.Audio.Microphones.Mute().ConfigureAwait(false);
        // Console.WriteLine(muteState switch {
        //     StatusAudioMicrophonesMute.On => "muted",
        //     StatusAudioMicrophonesMute.Off => "unmuted"
        // });


    }

}