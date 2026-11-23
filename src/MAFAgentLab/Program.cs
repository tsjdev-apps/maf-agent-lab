using System.Globalization;
using System.Text;
using MAFAgentLab.Console;
using Microsoft.Extensions.Configuration;
using Spectre.Console;

namespace MAFAgentLab;

/// <summary>Provides the application entry point and global process configuration.</summary>
public static class Program
{
    /// <summary>Configures the console application and runs its interactive workflow.</summary>
    public static async Task<int> Main(string[] args)
    {
        CultureInfo english = CultureInfo.GetCultureInfo("en-US");
        CultureInfo.CurrentCulture = english;
        CultureInfo.CurrentUICulture = english;
        CultureInfo.DefaultThreadCurrentCulture = english;
        CultureInfo.DefaultThreadCurrentUICulture = english;
        System.Console.InputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        System.Console.OutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        IConfiguration configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
            .AddUserSecrets(typeof(Program).Assembly, optional: true)
            .AddEnvironmentVariables()
            .Build();

        try
        {
            await ConsoleApplication.RunAsync(configuration, args);
            return 0;
        }
        catch (OperationCanceledException)
        {
            return 130;
        }
        catch (Exception error)
        {
            AnsiConsole.WriteException(error, ExceptionFormats.ShortenEverything);
            return 1;
        }
    }
}
