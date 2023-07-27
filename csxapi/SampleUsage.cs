using CSxAPI.API.Data;

namespace CSxAPI;

/*public class SampleUsage {

    public static async Task Dial() {
        await using ICSxAPIClient xapi = new CSxAPIClient("whisperblade.aldaviva.com", "ben", Environment.GetEnvironmentVariable("password") ?? "")
            { AllowSelfSignedTls = false, ConsoleTracing = false };
        await xapi.Connect().ConfigureAwait(false);

        xapi.Event.Audio.Input.Connectors += data => Console.WriteLine($"VU meter: {data.Microphone.First().Value.VuMeter}");

        xapi.Event.UserInterface.Message.Prompt.Response += data => {
            if (data.FeedbackId == "1") {
                Console.WriteLine($"Option ID: {data.OptionId}");
            }
        };

        xapi.Event.Conference.ParticipantList.ParticipantAdded += value =>
            Console.WriteLine(value.Device[1].EndToEndEncryption?.CertificateChain.Certificate[1].Subject.First().Value.Name);

        await xapi.Configuration.Conference.DefaultCall.Protocol(ConfigurationConferenceDefaultCallProtocol.Sip).ConfigureAwait(false);
        ConfigurationConferenceDefaultCallProtocol defaultCallProtocol = await xapi.Configuration.Conference.DefaultCall.Protocol().ConfigureAwait(false);
        xapi.Configuration.Conference.DefaultCall.ProtocolChanged += value => Console.WriteLine($"Default signaling protocol changed to {value}");

        ConfigurationTimeZone timeZone = ConfigurationTimeZone.America_Los_Angeles;

        await xapi.Command.Dial(number: "10990@bjn.vc", protocol: CommandDialProtocol.Sip, callRate: 6000, callType: CommandDialCallType.Video).ConfigureAwait(false);

        StatusAudioMicrophonesMute muteState = await xapi.Status.Audio.Microphones.Mute().ConfigureAwait(false);
        Console.WriteLine(muteState switch {
            StatusAudioMicrophonesMute.On  => "muted",
            StatusAudioMicrophonesMute.Off => "unmuted"
        });
    }

}*/