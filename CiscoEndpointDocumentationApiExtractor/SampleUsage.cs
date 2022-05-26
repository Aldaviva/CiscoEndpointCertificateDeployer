using System;
using System.Threading.Tasks;

namespace CiscoEndpointDocumentationApiExtractor;

public class SampleUsage {

    public static async Task Main() {
        await using XApi xApi = new Txas("roomkit.aldaviva.com", "ben", "password");

        int defaultVolume = await xApi.xConfiguration.audio.defaultVolume();
        Console.WriteLine($"default volume is {defaultVolume:N0}.");

        await xApi.xConfiguration.audio.defaultVolume(0);
        Console.WriteLine("Set default volume to 0.");

        Txas.Enums.StatusAudioSelectedDevice statusAudioSelectedDevice = await xApi.xStatus.audio.selectedDevice();

        await xApi.xCommand.audio.speakerCheck();
    }

}