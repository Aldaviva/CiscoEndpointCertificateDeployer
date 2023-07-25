using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace CiscoEndpointDocumentationApiExtractor.Extraction;

public class EventReader {

    private readonly ExtractedDocumentation docs;

    public EventReader(ExtractedDocumentation docs) {
        this.docs = docs;
    }

    public void parseEventXml(string xmlFilename) {
        XDocument doc = XDocument.Load(xmlFilename);

        foreach (XElement secondLevelElements in doc.Root!.Elements()) {
            visit(secondLevelElements, new[] { "xEvent" });
        }
    }

    private void visit(XElement el, IList<string> path) {
        path = path.Append(el.Name.LocalName).ToList();

        if (attributeEquals(el, "event", "True")) {
            DocXEvent xEvent = new() {
                name             = path,
                access           = EventAccessParser.parse(el.Attribute("access")!.Value),
                requiresUserRole = (el.Attribute("role")?.Value.Split(";").Select(rawRole => Enum.Parse<UserRole>(rawRole, true)) ?? Enumerable.Empty<UserRole>()).ToHashSet(),
            };

            if (xEvent.access == EventAccess.PUBLIC_API) {
                docs.events.Add(xEvent);

                foreach (XElement childEl in el.Elements()) {
                    visit(childEl, xEvent);
                }
            }
        } else {
            foreach (XElement childEl in el.Elements()) {
                visit(childEl, path);
            }
        }
    }

    private static void visit(XElement el, IEventParent parent) {
        IList<string> name     = parent.name.Append(el.Attribute("className")?.Value ?? el.Name.LocalName).ToList();
        bool   required = !attributeEquals(el, "optional", "True");

        if (attributeEquals(el, "type", "literal") && el.HasElements) {
            parent.children.Add(new EnumChild {
                name           = name,
                required       = required,
                possibleValues = el.Elements("Value").Select(valueEl => new EnumValue(valueEl.Value)).ToHashSet()
            });
        } else if (attributeEquals(el, "type", "string") || (attributeEquals(el, "type", "literal") && !el.HasElements)) {
            parent.children.Add(new StringChild {
                name     = name,
                required = required
            });
        } else if (attributeEquals(el, "type", "int")) {
            parent.children.Add(new IntChild {
                name                       = name,
                required                   = required,
                implicitAnonymousSingleton = attributeEquals(el, "onlyTextNode", "true")
            });
        } else if (attributeEquals(el, "multiple", "True")) {
            ListContainer listContainer = new() { name = name };
            parent.children.Add(listContainer);

            foreach (XElement childEl in el.Elements()) {
                visit(childEl, listContainer);
            }
        } else /*if (attributeEquals(el, "basenode", "True"))*/ {
            ObjectContainer objectContainer = new() {
                name     = name,
                required = required
            };
            parent.children.Add(objectContainer);

            foreach (XElement childEl in el.Elements()) {
                visit(childEl, objectContainer);
            }
        }
    }

    private static bool attributeEquals(XElement el, string attributeName, string? comparisonValue) {
        return string.Equals(el.Attribute(attributeName)?.Value, comparisonValue, StringComparison.InvariantCultureIgnoreCase);
    }

}