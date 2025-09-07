namespace ExampleIA01;
using LLama;
using LLama.Common;
using LLama.Sampling;


public class DirectLlamaExample
{
    public async Task Execute()
    {
        string modelPath = @"C:\Users\ivan\Downloads\tinyllama-1.1b-chat-v1.0.Q8_0.gguf"; // 👈 model path

        var parameters = new ModelParams(modelPath)
        {
            ContextSize = 1024, // The longest length of chat as memory.
            GpuLayerCount = 5 // How many layers to offload to GPU. Please adjust it according to your GPU memory.
        };
        using var model = LLamaWeights.LoadFromFile(parameters);
        using var context = model.CreateContext(parameters);
        var executor = new InteractiveExecutor(context);
        // Add chat histories as prompt to tell AI how to act.
        var chatHistory = new ChatHistory();
        chatHistory.AddMessage(AuthorRole.System, "you are an expert in programmin and C#. You should answer all " +
                                                  "questions in a friendly manner and provide code examples if needed.");
       
        ChatSession session = new(executor, chatHistory);

        InferenceParams inferenceParams = new InferenceParams()
        {
            MaxTokens = 256, // No more than 256 tokens should appear in answer. Remove it if antiprompt is enough for control.
            AntiPrompts = new List<string> { "User:" }, // Stop generation once antiprompts appear.

            SamplingPipeline = new DefaultSamplingPipeline(),
        };

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("The chat session has started;");
        Console.ForegroundColor = ConsoleColor.Blue;
        Console.Write("User: ");
        Console.ForegroundColor = ConsoleColor.Green;
        string userInput = Console.ReadLine() ?? "";

        while (userInput != "exit")
        {
            await foreach ( // Generate the response streamingly.
                           var text
                           in session.ChatAsync(
                               new ChatHistory.Message(AuthorRole.User, userInput),
                               inferenceParams))
            {
                Console.ForegroundColor = ConsoleColor.Blue;
                Console.Write(text);
            }
            Console.ForegroundColor = ConsoleColor.Green;
            userInput = Console.ReadLine() ?? "";
        }
        
    }
}