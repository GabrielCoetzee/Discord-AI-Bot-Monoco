using System.ComponentModel;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;

namespace MonocoBot.Tools;

public class CodeRunnerTools
{
    [Description("Executes a C# code snippet and returns the output. " +
        "Available namespaces: System, System.Linq, System.Collections.Generic, System.Text, System.IO, System.Text.Json, System.Net.Http. " +
        "The value of the last expression is returned. Console.WriteLine also works. " +
        "Has a 30-second timeout for safety.")]
    public async Task<string> RunCSharpCode([Description("The C# code to execute. The last expression value is returned. Example: 'Enumerable.Range(1, 10).Sum()'")] string code)
    {
        try
        {
            var options = ScriptOptions.Default
                .AddReferences(
                    typeof(object).Assembly,
                    typeof(Enumerable).Assembly,
                    typeof(System.Text.Json.JsonSerializer).Assembly,
                    typeof(HttpClient).Assembly)
                .AddImports(
                    "System",
                    "System.Linq",
                    "System.Collections.Generic",
                    "System.Text",
                    "System.IO",
                    "System.Text.Json",
                    "System.Net.Http");

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

            var output = new StringWriter();
            var originalOut = Console.Out;
            Console.SetOut(output);

            try
            {
                var result = await CSharpScript.EvaluateAsync<object>(code, options, cancellationToken: cts.Token);
                var consoleOutput = output.ToString();

                var response = "";

                if (!string.IsNullOrEmpty(consoleOutput))
                    response += $"Console Output:\n{consoleOutput}\n";

                if (result is not null)
                    response += $"Result: {result}";

                return string.IsNullOrEmpty(response) ? "Code executed successfully (no output)." : response.Trim();
            }
            finally
            {
                Console.SetOut(originalOut);
            }
        }
        catch (OperationCanceledException)
        {
            return "Error: Code execution timed out after 30 seconds.";
        }
        catch (CompilationErrorException ex)
        {
            return $"Compilation Error:\n{string.Join("\n", ex.Diagnostics)}";
        }
        catch (Exception ex)
        {
            return $"Runtime Error: {ex.Message}";
        }
    }
}
