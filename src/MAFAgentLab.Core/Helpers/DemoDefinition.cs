using Microsoft.Extensions.Configuration;

namespace MAFAgentLab.Core.Helpers;

/// <summary>Describes a demo and the behavior required to execute it.</summary>
public sealed record DemoDefinition(
    string Id,
    string Title,
    string Pattern,
    string ExamplePrompt,
    Func<DemoContext, Task> Run,
    Func<IConfiguration, IReadOnlyList<string>> GetMissingRequirements);
