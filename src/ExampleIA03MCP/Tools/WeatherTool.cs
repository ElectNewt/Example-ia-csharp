using System.ComponentModel;
using ModelContextProtocol.Server;

namespace ExampleIA03MCP.Tools;

[McpServerToolType]
public class WeatherTool
{
    [McpServerTool, Description("Returns the temperature in degrees.")]
    public static string GetWeather(string city)
    {
        //in a real case this will call an API
        var temperature = Random.Shared.Next(1, 40);
        return $"the temperature in {city} is {temperature}°C";
    }
}