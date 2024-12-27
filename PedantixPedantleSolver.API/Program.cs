using System.Text.Json;

namespace PedantixPedantleSolver.API;

class Program
{
    static async Task Main()
    {
        Console.WriteLine("Welcome to the Cemantix/Cemantle Solver!");

        Console.WriteLine("Select the game you want to solve:");
        Console.WriteLine("1. Cemantix (French)");
        Console.WriteLine("2. Cemantle (English)");
        var gameChoice = Console.ReadLine();

        var website = gameChoice switch
        {
            "1" => "https://cemantix.certitudes.org",
            "2" => "https://cemantle.certitudes.org",
            _ => throw new ArgumentException("Invalid choice. Please select 1 or 2.")
        };

        Console.WriteLine("Choose the word list to use:");
        var availableFiles = Directory.GetFiles("./WordLists", "*.txt");
        for (int i = 0; i < availableFiles.Length; i++)
        {
            Console.WriteLine($"{i + 1}. {Path.GetFileName(availableFiles[i])}");
        }

        var fileChoice = Console.ReadLine();
        if (!int.TryParse(fileChoice, out int fileIndex) || fileIndex < 1 || fileIndex > availableFiles.Length)
        {
            Console.WriteLine("Invalid choice. Exiting.");
            return;
        }

        var filePath = availableFiles[fileIndex - 1];
        Console.WriteLine($"You selected: {Path.GetFileName(filePath)} for {((gameChoice == "1") ? "Cemantix" : "Cemantle")}.");

        Console.WriteLine("Starting the solver...");
        await SubmitNamesUsingHttpCLient(filePath, website);
    }

    private static readonly char[] Separator = new[] { ' ', '\n', '\r' };

    static async Task SubmitNamesUsingHttpCLient(string filePath, string website)
    {
        Console.WriteLine($"Loading names from file: {filePath}");
        HttpClient client = new HttpClient();
        
        var names = (await File.ReadAllTextAsync(filePath))
            .Split(Separator, StringSplitOptions.RemoveEmptyEntries)
            .OrderBy(_ => Guid.NewGuid())
            .ToArray();
        Console.WriteLine($"Loaded {names.Length} names to test.");

        var cts = new CancellationTokenSource();
        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = 20,
            CancellationToken = cts.Token
        };

        int successfulAttempts = 0;
        int errors = 0;
        int foundSolution = 0;

        try
        {
            await Parallel.ForEachAsync(names, parallelOptions, async (name, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (Interlocked.CompareExchange(ref foundSolution, 1, 1) == 1)
                {
                    return;
                }

                var values = new Dictionary<string, string>
                {
                    { "word", name }
                };

                var content = new FormUrlEncodedContent(values);
                HttpRequestMessage requestMessage = new HttpRequestMessage
                {
                    Method = HttpMethod.Post,
                    Content = content,
                    RequestUri = new Uri(website + "/score")
                };
                requestMessage.Headers.Add("origin", website);

                try
                {
                    var response = await client.SendAsync(requestMessage, cancellationToken);
                    var responseString = await response.Content.ReadAsStringAsync(cancellationToken);

                    if (string.IsNullOrWhiteSpace(responseString))
                    {
                        Console.WriteLine($"[ERROR] Empty response for '{name}'. Skipping.");
                        Interlocked.Increment(ref errors);
                        return;
                    }

                    var data = JsonSerializer.Deserialize<Data>(responseString);

                    if (data != null)
                    {
                        if (data.score > 0.45m)
                        {
                            Console.WriteLine($"[INFO] Tried '{name}' with score: {data.score}");
                            Interlocked.Increment(ref successfulAttempts);
                        }

                        if (data.percentile > 999M)
                        {
                            if (Interlocked.CompareExchange(ref foundSolution, 1, 0) == 0)
                            {
                                Console.WriteLine($"[SUCCESS] Eureka! Found the solution: {name} with percentile: {data.percentile}");
                                cts.Cancel();
                            }
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR] Error processing '{name}': {ex.Message}");
                    Interlocked.Increment(ref errors);
                }
            });
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("Solver stopped because a solution was found.");
        }

        Console.WriteLine("\n=== Summary ===");
        Console.WriteLine($"Total names tested: {names.Length}");
        Console.WriteLine($"Successful attempts: {successfulAttempts}");
        Console.WriteLine($"Errors: {errors}");
        Console.WriteLine("Solver finished.");
    }

    public class Data
    {
        public int num { get; set; }
        public decimal percentile { get; set; }
        public decimal score { get; set; }
        public int solvers { get; set; }
    }
}
