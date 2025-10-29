// See https://aka.ms/new-console-template for more information

using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

var serverExePath = @"C:\Repos\ExampleIAWithCSharp\src\ExampleIA03MCP\bin\Release\net10.0\ExampleIA03MCP.exe";

var transport = new StdioClientTransport(new StdioClientTransportOptions
{
    Name = "WeatherClient",
    Command = serverExePath,
    Arguments = Array.Empty<string>()
});

await using var client = await McpClient.CreateAsync(transport);

var tools = await client.ListToolsAsync();
var prompts = await client.ListPromptsAsync();
var resources = await client.ListResourcesAsync();

Console.WriteLine("Tools: " + string.Join(", ", tools.Select(t => t.Name)));
Console.WriteLine("Prompts: " + string.Join(", ", prompts.Select(p => p.Name)));
Console.WriteLine("Resources: " + string.Join(", ", resources.Select(r => r.Uri)));
Console.WriteLine("-------");

var toolResult = await client.CallToolAsync(
    "get_weather",
    new Dictionary<string, object?> { ["city"] = "Madrid" },
    cancellationToken: CancellationToken.None
);

var weatherLine = toolResult.Content
    .OfType<TextContentBlock>()
    .FirstOrDefault()?.Text ?? "";

Console.WriteLine(weatherLine);