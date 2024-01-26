using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace EditorconfigValidator;

public static class Program {
    public static void Main() {
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
                       rules.SingleOrDefault(r => r.Key.Equals(key, r.StringComparison)) switch {
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

    }

    static List<KeyValueRule> rules = new List<KeyValueRule>{
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
