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
using System.IO;



namespace EditorconfigValidator;



public static class Program {
    const string _cacheDir = "rules";

    private static void log(object? value = null) => System.Console.WriteLine(value ?? String.Empty);

    public static async void Main() {

        // rules
        if (!System.IO.Directory.Exists(_cacheDir)) {
            System.IO.Directory.CreateDirectory(_cacheDir);
        }

        var netCa = await DotNetAnalyzerManager.GetNetAnalyzersAsync(_cacheDir);
        _rules.AddRange( netCa ); // .Select(n => n ));
        System.Console.WriteLine($"retrieved {netCa.Length} rules");
        System.Console.WriteLine(_rules[1] );
        return;

        List<ILineState> status = new();

        string[] lines = """
        root = 1
        [sectionHere]
        root = true
        
        # this is a comment
        Key = Value
        Key = Value ; bad comment
        Argle = Bargle
        """.Split(System.Environment.NewLine);
        int lineNumber = 0;

        Console.WriteLine(String.Join(System.Environment.NewLine, lines.Select((l, i) => $"{i,-5} {l}")));
        Console.WriteLine();


        {
            Console.WriteLine(
                Regex.Match(lines[^1], @"^\s*[^=]+=[^=]+") is { Groups: [{ Value: { } }] });
            if (Regex.Match(lines[^1], @"^\s*([^=]+)\s*=\s*([^=]+)") is { Groups: [{ Value: { } v1 }, { Value: { } v2 }, { Value: { } v3 }] }) {
                Console.WriteLine(v1);
                Console.WriteLine(v2);
                Console.WriteLine(v3);
            }
            Console.WriteLine("\n");
        }

        List<SectionLineState> sections = new List<SectionLineState> { new SectionLineState(-1, LineStatusLevel.Ok, String.Empty) }; // starts with a "root" section. line = -1 to identify it uniquely.
        foreach (var line in lines) {
            ILineState x = line.Trim() switch {
                "" => new EmptyLineState(lineNumber),
                ['#', ..] or [';', ..] => new CommentLineState(lineNumber, line.Trim().TrimStart([';', '#']).Trim()),
                _ when line.Contains("\\\\") => new ErrorLineState(lineNumber, "Double Slashes are not allowed"),
                _ when Regex.IsMatch(line, @"^\s*[^#;].*[#;]") => new ErrorLineState(lineNumber, "Comments are only permitted on dedicated lines"),
                ['[', .. var value, ']'] => new SectionLineState(lineNumber, LineStatusLevel.Ok, value),
                _ when Regex.Match(line, @"^\s*([^=]+?)\s*=\s*([^=]+)") is { Groups: [{ }, { Value: { } key }, { Value: { } value }] } =>
                    (key, value) switch {
                        ("root", _) when sections.Count != 1 => new KeyValueLineState(lineNumber, key, value, "'root' must be in root section.", LineStatusLevel.Error),
                        ("root", "true" or "false") => new KeyValueLineState(lineNumber, key, value),
                        ("root", _) => new KeyValueLineState(lineNumber, key, value, $"Invalid value '{value}' for key 'root'. Must be 'true' or 'false'", LineStatusLevel.Error),
                        _ => _rules.SingleOrDefault(r => r.Key.Equals(key, r.StringComparison)) switch {
                            _ => new KeyValueLineState(lineNumber, key, value, $"Unknown key '{key}' + '{value}'", LineStatusLevel.Error)
                        },
                    },
                _ => new ErrorLineState(lineNumber, "INVALID FORMAT")
            };
            if (x is SectionLineState s) {
                sections.Add(s);
                // log( $"Section = {s}");
            } else {
                sections[^1].Children.Add(x);
            }
            status.Add(x);
            lineNumber++;
        }

        log();

        // display
        foreach (var s in status) {
            Console.WriteLine(">> " + s + "\n");
        }
    }

    private static List<IKeyValueRule> _rules = new List<IKeyValueRule>{
        new KeyValueRule(
            Key: "dotnet_diagnostic.CA0000.something",
            ValueRegex: ".*"
        )
    };

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
    public List<ILineState> Children { get; } = new();
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
) : IKeyValueRule {
    public record AnalyzerProperties(
        string Category, // enum ? "category": "Design"
        bool IsEnabledByDefault,
        string TypeName,
        string[] Languages,
        string[] Tags
    ) { }
    
    static string[] Levels = [
        "warning"
    ];
    
    string IKeyValueRule.Key => Id;
    string IKeyValueRule.ValueRegex { get; } = '(' + String.Join( '|', Levels ) + ')';
    string IKeyValueRule.Name => this.ShortDescription;
    string IKeyValueRule.DefaultValue => DefaultLevel;
    string IKeyValueRule.Description => FullDescription;
    string IKeyValueRule.DocumentationUrl => HelpUri;
    StringComparison IKeyValueRule.StringComparison => StringComparison.OrdinalIgnoreCase;

/*
    public KeyValueRule ToKeyValueRule() {
        return new KeyValueRule(
            Key: this.Id,
            ValueRegex = "()",
            Name: this.ShortDescription,
            DefaultValue = this.DefaultLevel,
            Description = this.FullDescription,
            DocumentationUrl = this.HelpUri
        );
    }
    */
}

public static class DotNetAnalyzerManager {

    const string _code_quality_md_url = "https://raw.githubusercontent.com/dotnet/docs/main/docs/fundamentals/code-analysis/quality-rules/index.md";
    const string _code_quality_json_url_latest_stable = "https://github.com/dotnet/roslyn-analyzers/raw/release/8.0.2xx/src/NetAnalyzers/Microsoft.CodeAnalysis.NetAnalyzers.sarif";
    const string _code_quality_json_url_latest = "https://github.com/dotnet/roslyn-analyzers/raw/main/src/NetAnalyzers/Microsoft.CodeAnalysis.NetAnalyzers.sarif";


    public static async Task<NetAnalyzersSarif[]> GetNetAnalyzersAsync(string? cacheDir) {
        string cacheFileName = "code_quality_rules.json";
        string? cacheFilePath = cacheDir is { } ? Path.Combine(cacheDir, cacheFileName) : null;
        NetAnalyzersSarif[] rules;
        if (cacheFilePath is { } && Path.Exists(cacheFilePath)) {
            Console.WriteLine($"Loading from {cacheFilePath}");
            rules = JsonSerializer.Deserialize<NetAnalyzersSarif[]>(File.OpenRead(cacheFilePath)) ?? throw new JsonException();

        } else {
            HttpClient sharedClient = new() {
                // BaseAddress = new Uri(_code_quality_json_url_latest),
            };
            var jsonDoc = await JsonDocument.ParseAsync(
                await sharedClient.GetStreamAsync(_code_quality_json_url_latest));
            // jsonDoc.RootElement["x"];
            JsonNode jsonNode = await JsonNode.ParseAsync(await sharedClient.GetStreamAsync(_code_quality_json_url_latest)) ?? throw new JsonException();
            /*
            var rulesNode = jsonNode["runs"].AsArray().SelectMany( x => x.)

            .FirstOrDefault()
            ["rules"];
            System.Console.WriteLine( rulesNode );
            var sarifDict = rulesNode.Deserialize<Dictionary<string,NetAnalyzersSarif>>() ;
            return sarifDict.Values.ToArray();
            */
            rules = jsonNode["runs"]?.AsArray().SelectMany(x => (x?["rules"].Deserialize<Dictionary<string, NetAnalyzersSarif>>() ?? throw new JsonException()).Values).ToArray() ?? throw new JsonException();

            if (cacheFilePath is { }) {
                System.IO.File.WriteAllText(cacheFilePath, JsonSerializer.Serialize(rules));
                Console.WriteLine($"Saving to {cacheFilePath}");
            }
        }
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

public interface IKeyValueRule {
    public string Key { get; }
    public string ValueRegex { get; }
    public string? Name { get; }
    public string? DefaultValue { get; }
    public string? Description { get; }
    public string? DocumentationUrl { get; }
    public StringComparison StringComparison { get; }
}

public record KeyValueRule(
    string Key,
    string ValueRegex,
    string? Name = null,
    string? DefaultValue = null,
    string? Description = null,
    string? DocumentationUrl = null
) : IKeyValueRule {
    // public bool IsKeyCaseSensitive { get; init; } = false
    public StringComparison StringComparison { get; } = StringComparison.OrdinalIgnoreCase;
}

file static class Constants {

    // Supported values
    // https://spec.editorconfig.org/
    private const string editorConfigBaseRules = """
    [
        {
            "Key": "indent_style",
            "Description": "Set to tab or space to use hard tabs or soft tabs respectively. The values are case insensitive.",
            "ValueRegex": "(tab|space)"
        },
        {
            "Key": "indent_size",
            "Description": "Set to a whole number defining the number of columns used for each indentation level and the width of soft tabs (when supported). If this equals tab, the indent_size shall be set to the tab size, which should be tab_width (if specified); else, the tab size set by the editor. The values are case insensitive.",
            "ValueRegex": "[0-9]+"
        },
        {
            "Key": "tab_width",
            "Description": "Set to a whole number defining the number of columns used to represent a tab character. This defaults to the value of indent_size and should not usually need to be specified.",
            "ValueRegex": "[0-9]+"
        },    
        {
            "Key": "end_of_line",
            "Description": "Set to lf, cr, or crlf to control how line breaks are represented. The values are case insensitive.",
            "ValueRegex": "(lf|cr|crlf)"
        },
        {
            "Key": "charset",
            "Description": "Set to latin1, utf-8, utf-8-bom, utf-16be or utf-16le to control the character set. Use of utf-8-bom is discouraged.",
            "ValueRegex": "(latin1|utf-8|utf-8-bom|utf-16be|utf-16le)"
        },    
        {
            "Key": "trim_trailing_whitespace",
            "Description": "Set to true to remove all whitespace characters preceding newline characters in the file and false to ensure it doesn’t.",
            "ValueRegex": "(true|false)"
        },
        {
            "Key": "insert_final_newline",
            "Description": "Set to true ensure file ends with a newline when saving and false to ensure it doesn’t.",
            "ValueRegex": "(true|false)"
        }
    ]
    """;
    // root  : Must be specified in the preamble. Set to true to stop the .editorconfig file search on the current file. The value is case insensitive.
    }
