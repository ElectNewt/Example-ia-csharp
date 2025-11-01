using System.Text;
using Microsoft.SemanticKernel;
using Microsoft.Extensions.AI;
using Npgsql;
using System.Globalization;
using Microsoft.SemanticKernel.TextGeneration;

// Simple RAG demo with Semantic Kernel + Ollama + PostgreSQL (pgvector) for netmentor channel

// 3.2 default configuration, all this should be in config 
var connString = "Host=localhost;Port=5432;Database=rag_db;Username=user;Password=password;";
var ollamaEndpoint = new Uri("http://localhost:11434");
var textModel = "llama3";        
var embedModel = "nomic-embed-text";         

var inputFileName = "data.txt"; 
var inputFilePath = Path.Combine(AppContext.BaseDirectory, inputFileName);

// Build a kernel with Ollama text + embedding services
var builder = Kernel.CreateBuilder();
builder.AddOllamaTextGeneration(modelId: textModel, endpoint: ollamaEndpoint);
builder.AddOllamaEmbeddingGenerator(modelId: embedModel, endpoint: ollamaEndpoint);
var kernel = builder.Build();

var textGen = kernel.GetRequiredService<ITextGenerationService>();
var embedGen = kernel.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();

// 3.3 Connect to Postgres and ensure schema
await using var conn = new NpgsqlConnection(connString);
try
{
    await conn.OpenAsync();
    // Ensure pgvector extension
    await using (var cmd = new NpgsqlCommand("CREATE EXTENSION IF NOT EXISTS vector;", conn))
        await cmd.ExecuteNonQueryAsync();
}
catch (Exception ex)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine("Failed to connect to PostgreSQL or enable pgvector.\nError: " + ex.Message);
    Console.ResetColor();
    return;
}

// Determine embedding dimension from the model
var dimProbe = await embedGen.GenerateAsync("probe");
var dimensionLenght = dimProbe.Vector.Length;

var tableName = "rag_items";
var createSql = $"CREATE TABLE IF NOT EXISTS {tableName} (\n  id TEXT PRIMARY KEY,\n  content TEXT,\n  embedding vector({dimensionLenght})\n);";
await using (var cmd = new NpgsqlCommand(createSql, conn))
    await cmd.ExecuteNonQueryAsync();

//3.4 - Ingest section: read file, chunk, embed and upsert into vector table
Console.WriteLine($"Reading and ingesting '{inputFileName}' into table '{tableName}'...");
var text = await File.ReadAllTextAsync(inputFilePath);
var chunks = ChunkText(text, maxChars: 1000, overlap: 100).ToList();

int i = 0;
foreach (var chunk in chunks)
{
    var id = $"doc-{i++}";
    var emb = await embedGen.GenerateAsync(chunk);
    var embStr = ToPgVectorLiteral(emb.Vector.Span);
    var upsert = $"INSERT INTO {tableName} (id, content, embedding) VALUES (@id, @content, CAST(@emb AS vector({dimensionLenght})))\n                  ON CONFLICT (id) DO UPDATE SET content = EXCLUDED.content, embedding = EXCLUDED.embedding;";
    await using var cmd = new NpgsqlCommand(upsert, conn);
    cmd.Parameters.AddWithValue("id", id);
    cmd.Parameters.AddWithValue("content", chunk);
    cmd.Parameters.AddWithValue("emb", embStr);
    await cmd.ExecuteNonQueryAsync();
}
Console.ForegroundColor = ConsoleColor.Green;
Console.WriteLine($"Ingested {chunks.Count} chunks. Ready for questions.\n");
Console.ResetColor();

// 3.5 - chat
Console.WriteLine("Ask a question (empty line to exit):");
while (true)
{
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.Write("Question> ");
    Console.ResetColor();
    var question = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(question)) break;

    // Retrieve top-k relevant chunks using cosine distance
    var embeddedQuestion = await embedGen.GenerateAsync(question);
    var ebbeddedQuestionString = ToPgVectorLiteral(embeddedQuestion.Vector.Span);

    var searchSql = $"SELECT id, content, (1 - (embedding <=> CAST(@qemb AS vector({dimensionLenght})))) AS similarity\n                      FROM {tableName}\n                      ORDER BY embedding <=> CAST(@qemb AS vector({dimensionLenght}))\n                      LIMIT 4;";
    await using var sCmd = new NpgsqlCommand(searchSql, conn);
    sCmd.Parameters.AddWithValue("qemb", ebbeddedQuestionString);

    var sb = new StringBuilder();
    await using (var reader = await sCmd.ExecuteReaderAsync())
    {
        while (await reader.ReadAsync())
        {
            var content = reader.GetString(reader.GetOrdinal("content"));
            sb.AppendLine("- " + content.Trim());
        }
    }

    var prompt = $@"You are a helpful assistant. Respond strictly based on the provided context. 
If the answer is not in the context, reply that you don’t know.

Context:
{sb}

Question: {question}
Answer:";

    try
    {
        var response = await textGen.GetTextContentAsync(prompt, kernel: kernel);
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("Answer> " + (response?.ToString() ?? "(no answer)"));
        Console.ResetColor();
    }
    catch (Exception ex)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine("Error: " + ex.Message);
        Console.ResetColor();
    }
}

static string ToPgVectorLiteral(ReadOnlySpan<float> vector)
{
    // Format like: [0.1, -0.2, ...] with invariant culture
    var sb = new StringBuilder();
    sb.Append('[');
    for (int i = 0; i < vector.Length; i++)
    {
        if (i > 0) sb.Append(',');
        sb.Append(vector[i].ToString("G9", CultureInfo.InvariantCulture));
    }
    sb.Append(']');
    return sb.ToString();
}

static IEnumerable<string> ChunkText(string text, int maxChars = 1000, int overlap = 100)
{
    if (maxChars <= 0) throw new ArgumentOutOfRangeException(nameof(maxChars));
    if (overlap < 0) throw new ArgumentOutOfRangeException(nameof(overlap));

    // Simple paragraph-aware chunker with character limit and overlap
    var paras = text.Replace("\r\n", "\n").Split("\n\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    var current = new StringBuilder();
    foreach (var p in paras)
    {
        var toAdd = p.Trim();
        if (toAdd.Length == 0) continue;

        if (current.Length + toAdd.Length + 2 > maxChars)
        {
            if (current.Length > 0)
            {
                yield return current.ToString();

                // create overlap from end of previous chunk
                if (overlap > 0)
                {
                    var prev = current.ToString();
                    var tail = prev.Length <= overlap ? prev : prev.Substring(prev.Length - overlap);
                    current.Clear();
                    current.Append(tail);
                }
                else
                {
                    current.Clear();
                }
            }
        }

        if (current.Length > 0) current.AppendLine().AppendLine();
        current.Append(toAdd);
    }

    if (current.Length > 0)
    {
        yield return current.ToString();
    }
}
