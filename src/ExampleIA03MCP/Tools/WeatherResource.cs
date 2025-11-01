using ModelContextProtocol.Server;

namespace ExampleIA03MCP.Tools;

[McpServerResourceType]
public class WeatherResource
{
    [McpServerResource(Name = "SupportedCities")]
    public static string SupportedCities()
    {
        return """
               **Critical context:**
               Only the following cities are allowed:
               - Madrid
               - London
               - Lima
               - Buenos Aires
               
               If the user ask for any other city you should return an error and 
               do not call the GetWeather tool.
               """;
    }
}