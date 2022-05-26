using System.Collections.Generic;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Xml.Serialization;

namespace CiscoEndpointDocumentationApiExtractor;

public partial class Txas {

    public Commands.XConfiguration xConfiguration => new(this);

    public Commands.XStatus xStatus => new(this);

    public Commands.XCommand xCommand => new(this);

    public interface Commands {

        public class XConfiguration {

            private readonly Txas xApi;

            public XConfiguration(Txas xApi) {
                this.xApi = xApi;
            }

            public AudioConfiguration audio => new(xApi);

        }

        public class AudioConfiguration {

            private readonly Txas xApi;

            public AudioConfiguration(Txas xApi) {
                this.xApi = xApi;
            }

            public Task<int> defaultVolume() {
                return xApi.getConfigurationOrStatus<int>(new[] { "Configuration", "Audio", "DefaultVolume" });
            }

            public Task defaultVolume(int defaultVolume) {
                return xApi.setConfiguration(new[] { "Configuration", "Audio", "DefaultVolume" }, defaultVolume);
            }

        }

        public class XStatus {

            private readonly Txas xApi;

            public XStatus(Txas xApi) {
                this.xApi = xApi;
            }

            public AudioStatus audio => new(xApi);

        }

        public class AudioStatus {

            private readonly Txas xApi;

            public AudioStatus(Txas xApi) {
                this.xApi = xApi;
            }

            public Task<Enums.StatusAudioSelectedDevice> selectedDevice() {
                return xApi.getConfigurationOrStatus<Enums.StatusAudioSelectedDevice>(new[] { "Status", "Audio", "SelectedDevice" });
            }

        }

        public class XCommand {

            private readonly Txas xApi;

            public XCommand(Txas xApi) {
                this.xApi = xApi;
            }

            public AudioCommands audio => new(xApi);

        }

        public class AudioCommands {

            private readonly Txas xApi;

            public AudioCommands(Txas xApi) {
                this.xApi = xApi;
            }

            public Task<XElement> speakerCheck(int measurementLength = 1, int volume = 1) {
                return xApi.callMethod(new[] { "Command", "Audio", "SpeakerCheck" }, new Dictionary<string, object> {
                    { "MeasurementLength", measurementLength },
                    { "Volume", volume }
                });
            }

        }

    }

    public interface Enums {

        public enum StatusAudioSelectedDevice {

            [XmlEnum("Internal")] Internal,
            HeadsetUSB,
            HeadsetAnalog,
            HeadsetBluetooth,
            HandsetUSB

        }

    }

}