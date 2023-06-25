using System;
using System.Collections.Generic;
using System.Linq;

namespace CiscoEndpointDocumentationApiExtractor.Extraction;

public class Fixes {

    private readonly ExtractedDocumentation documentation;

    public Fixes(ExtractedDocumentation documentation) {
        this.documentation = documentation;
    }

    public void fix() {
        setConfigurationValueSpace("xConfiguration Video Input AirPlay Mode", "Off", "On");
        setConfigurationValueSpace("xConfiguration Video Input Connector [n] CameraControl Mode", "Off", "On");
        setConfigurationValueSpace("xConfiguration Video Input Connector [n] CEC Mode", "Off", "On");
        setConfigurationValueSpace("xConfiguration Video Input Connector [n] HDCP Mode", "Off", "On");
        setConfigurationValueSpace("xConfiguration Video Input Connector [n] InputSourceType", "PC", "camera", "document_camera", "mediaplayer", "whiteboard", "other");
        setConfigurationValueSpace("xConfiguration Video Input Connector [n] PreferredResolution", "1920_1080_60", "2560_1440_60", "3840_2160_30", "3840_2160_60");
        setConfigurationValueSpace("xConfiguration Video Input Connector [n] Visibility", "Always", "IfSignal", "Never");
        setConfigurationValueSpace("xConfiguration Video Output Connector [n] CEC Mode", "Off", "On");
        setConfigurationValueSpace("xConfiguration Video Output Connector [n] HDCPPolicy", "Off", "On");
        setConfigurationValueSpace("xConfiguration Video Output Connector [n] Resolution", "Auto", "1920_1080_50", "1920_1080_60", "1920_1200_50", "1920_1200_60", "2560_1440_60", "3840_2160_30",
            "3840_2160_60");

        foreach (DocXStatus naStatus in documentation.statuses.Where(status => status.description == "Not applicable in this release.")) {
            naStatus.returnValueSpace = new StringValueSpace();
        }

        foreach (DocXStatus multiParameterStatus in documentation.statuses.Where(status => status.arrayIndexParameters.Count >= 2)) {
            int nIndex = 1;
            foreach (IntParameter parameter in multiParameterStatus.arrayIndexParameters.Where(parameter => parameter.name == "n")) {
                if (nIndex > 1) {
                    parameter.name += nIndex;
                }
                nIndex++;
            }
        }
    }

    private void setConfigurationValueSpace(string path, params string[] values) {
        if (findCommand<DocXConfiguration>(path) is { } configuration && configuration.parameters.LastOrDefault() is EnumParameter parameter) {
            parameter.possibleValues.Clear();
            foreach (string newValue in values) {
                parameter.possibleValues.Add(new EnumValue(newValue));
            }
        } else {
            Console.WriteLine($"Fixes: could not find {path}, so not applying this fix");
        }
    }

    private T? findCommand<T>(string path) where T: AbstractCommand {
        IList<string> nameQuery = path.Split(" ");
        IList<T> collection = typeof(T) switch {
            var t when t == typeof(DocXCommand)       => (List<T>) documentation.commands,
            var t when t == typeof(DocXConfiguration) => (List<T>) documentation.configurations,
            var t when t == typeof(DocXStatus)        => (List<T>) documentation.statuses,
            // _                                         => null
        };

        return collection.FirstOrDefault(command => command.name.SequenceEqual(nameQuery));
    }

}