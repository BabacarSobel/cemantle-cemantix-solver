using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;

namespace PedantixPedantleSolver.Selenium;

class Program
{
    static async Task Main()
    {
        Console.WriteLine("Welcome to the Cemantix/Cemantle Solver!");

        Console.WriteLine("Select the game you want to solve:");
        Console.WriteLine("1. Cemantix (French)");
        Console.WriteLine("2. Cemantle (English)");
        var gameChoice = Console.ReadLine();

        var (website, inputFieldId, submitButtonId, guessedRowId) = gameChoice switch
        {
            "1" => ("https://cemantix.certitudes.org", "cemantix-guess", "cemantix-guess-btn", "cemantix-guessed"),
            "2" => ("https://cemantle.certitudes.org", "cemantle-guess", "cemantle-guess-btn", "cemantle-guessed"),
            _ => throw new ArgumentException("Invalid choice. Please select 1 or 2.")
        };

        Console.WriteLine("Choose the word list to use:");
        var availableFiles = Directory.GetFiles("./WordLists", "*.txt");
        for (var i = 0; i < availableFiles.Length; i++)
        {
            Console.WriteLine($"{i + 1}. {Path.GetFileName(availableFiles[i])}");
        }

        var fileChoice = Console.ReadLine();
        if (!int.TryParse(fileChoice, out var fileIndex) || fileIndex < 1 || fileIndex > availableFiles.Length)
        {
            Console.WriteLine("Invalid choice. Exiting.");
            return;
        }

        var filePath = availableFiles[fileIndex - 1];
        Console.WriteLine($"You selected: {Path.GetFileName(filePath)} for {((gameChoice == "1") ? "Cemantix" : "Cemantle")}.");
        await SubmitNamesFromFile(filePath, website, inputFieldId, submitButtonId, guessedRowId);
    }

    private static Task SubmitNamesFromFile(string filePath, string url, string inputFieldId, string submitButtonId, string guessedRowId)
    {
        var driver = new ChromeDriver();

        try
        {
            driver.Navigate().GoToUrl(url);

            try
            {
                var consentButton = driver.FindElement(By.CssSelector(".fc-button.fc-cta-consent.fc-primary-button"));
                consentButton.Click();
                Thread.Sleep(2000);
            }
            catch (NoSuchElementException) { }

            try
            {
                var dialogCloseButton = driver.FindElement(By.Id("dialog-close"));
                dialogCloseButton.Click();
                Thread.Sleep(2000);
            }
            catch (NoSuchElementException) { }

            var names = File.ReadAllText(filePath).Split(new[] { ' ', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var name in names)
            {
                try
                {
                    var inputField = driver.FindElement(By.Id(inputFieldId));
                    inputField.Clear();
                    inputField.SendKeys(name);

                    var submitButton = driver.FindElement(By.Id(submitButtonId));
                    submitButton.Click();

                    Thread.Sleep(2000);

                    try
                    {
                        var guessedRow = driver.FindElement(By.Id(guessedRowId));
                        var firstTd = guessedRow.FindElement(By.CssSelector("td.number.close.popup"));

                        if (firstTd.Text.Trim() == "100.00")
                        {
                            Console.WriteLine($"[SUCCESS] Eureka! The correct word is '{name}'.");
                            Console.WriteLine("The program will now exit.");
                            break; // Exit the loop
                        }
                    }
                    catch (NoSuchElementException)
                    {
                        Console.WriteLine($"[INFO] Word '{name}' is not the correct guess.");
                    }
                }
                catch (NoSuchElementException ex)
                {
                    Console.WriteLine($"[ERROR] Failed to process '{name}': {ex.Message}");
                }
            }
        }
        finally
        {
            driver.Quit();
        }

        return Task.CompletedTask;
    }
}
