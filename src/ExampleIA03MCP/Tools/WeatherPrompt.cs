using ModelContextProtocol.Server;

namespace ExampleIA03MCP.Tools;

[McpServerPromptType]
public class WeatherPrompt
{
    [McpServerPrompt(Name = "FormatReport")]
    public static string MakeWeatherFriendly()
    {
        return """
               from the next information on the weather provided by the server.
               {{input_clima}}

               Convert it into 

               Example:
               Input: "the temperature in Madrid is 25°C"
               Output: "Madrid,25,°C"
               """;
    }
}