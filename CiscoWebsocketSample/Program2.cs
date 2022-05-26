using CiscoWebsocketXapi;
using Newtonsoft.Json.Linq;

namespace CiscoWebsocketSample;

public class Program2 {

    // private static readonly Random RANDOM = new();

    public static async Task Main() {
        XApi xapi = new JsonRpcXapi("roomkit.aldaviva.com", "ben", "***REMOVED***", true) { ConsoleTracing = false };
        await xapi.connect();

        int   secondsRemaining = 30;
        bool? userChoseStayOn  = null;

        long subscriptionId = await xapi.Subscribe(new[] { "Event", "UserInterface", "Message", "Prompt", "Response" }, parameters => {
            Console.WriteLine("got feedback");
            string? feedbackId = parameters.SelectToken("UserInterface.Message.Prompt.Response.FeedbackId")?.Value<string>();
            if (feedbackId == "shutdown") {
                userChoseStayOn = parameters.SelectToken("UserInterface.Message.Prompt.Response.OptionId")?.Value<int>() == 2;
            }
        });

        while (secondsRemaining > 0 && userChoseStayOn == null) {
            // await xapi.command(new[] { "UserInterface", "Message", "Prompt", "Display" }, new {
            //     FeedbackId ="shutdown",
            //     Duration= secondsRemaining.ToString() ,
            //     Title= "Shutting down" ,
            //     Text= $"This device will turn off in {secondsRemaining:N0} seconds." ,
            //     "Option.1"= "Shut down now" ,
            //     Option.2= "Remain on" 
            // });

            await xapi.Command(new[] { "UserInterface", "Message", "Prompt", "Display" }, new Dictionary<string, object> {
                { "FeedbackId", "shutdown" },
                { "Duration", secondsRemaining.ToString() },
                { "Title", "Shutting down" },
                { "Text", $"This device will turn off in {secondsRemaining:N0} seconds." },
                { "Option.1", "Shut down now" },
                { "Option.2", "Remain on" }
            });

            await Task.Delay(1000);
            secondsRemaining--;
        }

        await xapi.Command(new[] { "UserInterface", "Message", "Prompt", "Clear" }, new { FeedbackId = "shutdown" });

        await xapi.Unsubscribe(subscriptionId);

        if (userChoseStayOn ?? false) {
            Console.WriteLine("staying on");
        } else {
            Console.WriteLine("shutting down");
        }

        // long uptimeSeconds = await xapi.get<long>("Status", "SystemUnit", "Uptime");
        //
        // TimeSpan uptime = TimeSpan.FromSeconds(uptimeSeconds);
        // Console.WriteLine($"Uptime: {uptime}");
        //
        // IDictionary<string, object> systemUnitState = await xapi.get<Dictionary<string, object>>("Status", "SystemUnit", "State");
        //
        // long o = (long) systemUnitState["NumberOfActiveCalls"];
        // Console.WriteLine($"Active calls: {o:N0}");
        //
        // string sipRegistrationStatus = await xapi.get<string>("Status", "SIP", "Registration", 1, "Status");
        // Console.WriteLine($"SIP registration status: {sipRegistrationStatus}");
        //
        // string[] customMessagePath     = { "Configuration", "UserInterface", "CustomMessage" };
        // string   customMessageExpected = RANDOM.Next().ToString();
        // if (await xapi.set(customMessagePath, customMessageExpected)) {
        //     Console.WriteLine($"Set CustomMessage to {customMessageExpected}");
        // } else {
        //     Console.WriteLine("Failed to set CustomMessage");
        // }
        //
        // string customMessageActual = await xapi.get<string>(customMessagePath);
        // Console.WriteLine($"Custom message: {customMessageActual}");
        //
        // var  macroStatus           = await xapi.command<IDictionary<string, object>>(new[] { "Macros", "Runtime", "Status" });
        // bool isMacroRuntimeRunning = bool.Parse((string) macroStatus["Running"]);
        // Console.WriteLine($"Macro runtime running: {isMacroRuntimeRunning}");
        //
        // long subscriptionId = await xapi.subscribe(new[] { "Event", "UserInterface", "Message", "Alert", "Cleared" }, () => Console.WriteLine("alert cleared"));
        // await xapi.command(new[] { "UserInterface", "Message", "Alert", "Clear" });
        //
        // await xapi.unsubscribe(subscriptionId);
        // Console.WriteLine("Unsubscribed");
    }

}