using System;

using System.Linq;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Threading.Tasks;


using CSharpShellCore;
using System.Text.Json.Nodes;
using EditorconfigValidator;



namespace EditorconfigValidator;



public static class Program {

    public static async void Main() {
        List<ILineState> status = new();

        string[] lines = """
        
        [sectionHere]
        
        # this is a comment
        Key = Value
        Key = Value ; bad comment
        """.Split(System.Environment.NewLine);
        int lineNumber = 0;

        {
            Console.WriteLine(
                Regex.Match(lines[^1], @"^\s*[^=]+=[^=]+") is { Groups: [{ Value: { } }] });
            if (Regex.Match(lines[^1], @"^\s*([^=]+)\s*=\s*([^=]+)") is { Groups: [{ Value: { } v1 }, { Value: { } v2 }, { Value: { } v3 }] }) {
                Console.WriteLine(v1);
                Console.WriteLine(v2);
                Console.WriteLine(v3);
            }
            Console.WriteLine();
        }
        foreach (var line in lines) {
            ILineState x = line.Trim() switch {
                "" => new EmptyLineState(lineNumber),
                ['#', ..] or [';', ..] => new CommentLineState(lineNumber, line.Trim().TrimStart([';', '#']).Trim()),
                _ when line.Contains("\\\\") => new ErrorLineState(lineNumber, "Double Slashes are not allowed"),
                _ when Regex.IsMatch(line, @"^\s*[^#;].*[#;]") => new ErrorLineState(lineNumber, "Comments are only permitted on dedicated lines"),
                ['[', .. var value, ']'] => new SectionLineState(lineNumber, LineStatusLevel.Ok, value),
                _ when Regex.Match(lines[^1], @"^\s*([^=]+)\s*=\s*([^=]+)") is { Groups: [{ }, { Value: { } key }, { Value: { } value }] } =>
                       _rules.SingleOrDefault(r => r.Key.Equals(key, r.StringComparison)) switch {
                           _ => new KeyValueLineState(lineNumber, key, value, "Unknown key", LineStatusLevel.Error)
                       },
                _ => new ErrorLineState(lineNumber, "INVALID FORMAT")
            };
            status.Add(x);
            lineNumber++;

            /*
            "^\s*$" { $ok = "Empty" }
              "\\" { $err = "Double backslash" }
              "^\s*[#;]" { $ok = "Comment" }
              "^\s*\[.+\]\s*$" { $ok = "Section" }
              "^\s*[^#;].*[#;]" { $err = "Comment not at beginning of line" }
              "^\s*[^=]+=[^=]+" { "key = value" }
              default { $err = "INVALID FORMAT" }
              */

        }

        // display
        foreach (var s in status) {
            Console.WriteLine(s);
        }
        
        var rules = await DotNetAnalyzerManager.GetAsync();
        System.Console.WriteLine( $"retrieved {rules.Length} rules" );

    }

    private static List<KeyValueRule> _rules = new List<KeyValueRule>{
        new KeyValueRule(
            Key: "dotnet_diagnostic.CA0000.something",
            ValueRegex: ".*"
        )
    };

}


public record KeyValueRule(
    string Key,
    string ValueRegex,
    string? Name = null,
    string? DefaultValue = null,
    string? Description = null,
    string? DocumentationUrl = null
) {
    // public bool IsKeyCaseSensitive { get; init; } = false
    public StringComparison StringComparison { get; } = StringComparison.OrdinalIgnoreCase;
}

public interface ILineState {
    public int LineNumber { get; }
    public LineStatusLevel Status { get; }
    // public string? Message { get; }
}

public record KeyValueLineState(
    int LineNumber,
    string Key,
    string? Value,
    string? Message = null,
    LineStatusLevel Status = LineStatusLevel.Ok
) : ILineState {
    // public string? Message { get; init; } = null;
}

public record SectionLineState(
    int LineNumber,
    LineStatusLevel Status,
    string SectionText
) : ILineState {
    //    public string? Message { get; init; } = null;
}

public record CommentLineState(
    int LineNumber,
    string CommentText
) : ILineState {
    public LineStatusLevel Status => LineStatusLevel.Ok;
    //    public string? Message { get; init; } = null;
}


public record EmptyLineState(
    int LineNumber
) : ILineState {
    public LineStatusLevel Status => LineStatusLevel.Ok;
    //    public string? Message => null;
}

public record ErrorLineState(
    int LineNumber,
    string Message,
    LineStatusLevel Status = LineStatusLevel.Error
) : ILineState { }

public enum LineStatusLevel {
    Ok,
    Info,
    Error
}

public record NetAnalyzersSarif(
    string Id,
    string ShortDescription,
    string FullDescription,
    string DefaultLevel, // enum ? (warning)
    string HelpUri,
    NetAnalyzersSarif.AnalyzerProperties Properties
) {
    public record AnalyzerProperties(
        string Category, // enum ? "category": "Design"
        bool IsEnabledByDefault,
        string TypeName,
        string[] Languages,
        string[] Tags
    ) { }
}

public static class DotNetAnalyzerManager {

    const string _code_quality_md_url = "https://raw.githubusercontent.com/dotnet/docs/main/docs/fundamentals/code-analysis/quality-rules/index.md";
    const string _code_quality_json_url_latest_stable = "https://github.com/dotnet/roslyn-analyzers/raw/release/8.0.2xx/src/NetAnalyzers/Microsoft.CodeAnalysis.NetAnalyzers.sarif";
    const string _code_quality_json_url_latest = "https://github.com/dotnet/roslyn-analyzers/raw/main/src/NetAnalyzers/Microsoft.CodeAnalysis.NetAnalyzers.sarif";


    public static async Task<NetAnalyzersSarif[]> GetAsync() {
        HttpClient sharedClient = new() {
            // BaseAddress = new Uri(_code_quality_json_url_latest),
        };
        var jsonDoc = await JsonDocument.ParseAsync(
            await sharedClient.GetStreamAsync(_code_quality_json_url_latest) );
        // jsonDoc.RootElement["x"];
        JsonNode jsonNode = await JsonNode.ParseAsync( await sharedClient.GetStreamAsync(_code_quality_json_url_latest) );
        /*
        var rulesNode = jsonNode["runs"].AsArray().SelectMany( x => x.)
        
        .FirstOrDefault()
        ["rules"];
        System.Console.WriteLine( rulesNode );
        var sarifDict = rulesNode.Deserialize<Dictionary<string,NetAnalyzersSarif>>() ;
        return sarifDict.Values.ToArray();
        */
        var rules = jsonNode["runs"].AsArray().SelectMany( x => x["rules"].Deserialize<Dictionary<string,NetAnalyzersSarif>>().Values ).ToArray();
        return rules;
    }

    /*
       {
           "$schema": "http://json.schemastore.org/sarif-1.0.0",
  "version": "1.0.0",
  "runs": [
    {
      "tool": {
        "name": "Microsoft.CodeAnalysis.CSharp.NetAnalyzers",
        "version": "9.0.0",
        "language": "en-US"
      },
      "rules": {
        "CA1032": {
          "id": "CA1032",
          "shortDescription": "Implement standard exception constructors",
          "fullDescription": "Failure to provide the full set of constructors can make it difficult to correctly handle exceptions.",
          "defaultLevel": "warning",
          "helpUri": "https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1032",
          "properties": {
            "category": "Design",
            "isEnabledByDefault": false,
            "typeName": "CSharpImplementStandardExceptionConstructorsAnalyzer",
            "languages": [
              "C#"
            ],
            "tags": [
              "PortedFromFxCop",
              "Telemetry",
              "EnabledRuleInAggressiveMode"
            ]
          }
        },
     */
}
