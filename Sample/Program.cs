using CSxAPI;

Console.WriteLine("Connecting...");
await using ICSxAPIClient xapi = await new CSxAPIClient("whisperblade.aldaviva.com", "ben", Environment.GetEnvironmentVariable("password") ?? "").Connect();

await readEndpointName();
await readUptime();
await dial();

async Task dial() {
    await xapi.Command.Dial(number: "10990@bjn.vc");
}

async Task readUptime() {
    TimeSpan uptime = TimeSpan.FromSeconds(await xapi.Status.SystemUnit.Uptime());
    Console.WriteLine($"Endpoint has been up for {uptime.Days:N0} day(s), {uptime.Hours:N0} hour(s), {uptime.Minutes:N0} minute(s), and {uptime.Seconds:N0} second(s).");
}

async Task readEndpointName() {
    string name = await xapi.Configuration.SystemUnit.Name();
    Console.WriteLine($"Endpoint name: {name}");
}