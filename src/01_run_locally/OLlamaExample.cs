using OllamaSharp;

namespace ExampleIA01;

public class OLlamaExample
{
    public async Task Execute()
    {
        Uri uri = new Uri("http://localhost:11434");
        OllamaApiClient ollama = new OllamaApiClient(uri);

        // select a model which should be used for further operations
        ollama.SelectedModel = "llama3";
        
        Chat chat = new Chat(ollama);

        while (true)
        {
            Console.Write(">>");
            string? message = Console.ReadLine();
            await foreach (string answerToken in chat.SendAsync(message))
                Console.Write(answerToken);
            
            Console.WriteLine();
        }
    }
}