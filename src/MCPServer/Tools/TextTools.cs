using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.RegularExpressions;

namespace MCPServer.Tools;

[McpServerToolType]
public class TextTools
{
    [McpServerTool(Name = "search_text"), Description("Search for a pattern in text and return matching lines.")]
    public static string SearchText(string text, string pattern, bool caseSensitive = false)
    {
        try
        {
            var lines = text.Split('\n');
            var matches = new List<string>();
            var comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].Contains(pattern, comparison))
                {
                    matches.Add($"Line {i + 1}: {lines[i].Trim()}");
                }
            }

            if (matches.Count == 0)
                return $"No matches found for pattern '{pattern}' in the provided text";

            return $"Found {matches.Count} matches for pattern '{pattern}':\n{string.Join("\n", matches)}";
        }
        catch (Exception ex)
        {
            return $"Error searching text: {ex.Message}";
        }
    }

    [McpServerTool(Name = "replace_text"), Description("Replace all occurrences of a pattern with replacement text.")]
    public static string ReplaceText(string text, string searchPattern, string replacement, bool caseSensitive = false)
    {
        try
        {
            string result;
            if (caseSensitive)
            {
                result = text.Replace(searchPattern, replacement);
            }
            else
            {
                result = Regex.Replace(text, Regex.Escape(searchPattern), replacement, RegexOptions.IgnoreCase);
            }

            int count = (text.Length - result.Length + replacement.Length) / searchPattern.Length;
            return $"Replaced {count} occurrences of '{searchPattern}' with '{replacement}'.\n\nResult:\n{result}";
        }
        catch (Exception ex)
        {
            return $"Error replacing text: {ex.Message}";
        }
    }

    [McpServerTool(Name = "extract_lines"), Description("Extract specific lines from text by line numbers.")]
    public static string ExtractLines(string text, string lineNumbers)
    {
        try
        {
            var lines = text.Split('\n');
            var extractedLines = new List<string>();
            
            // Parse line numbers (e.g., "1,3,5-8")
            var parts = lineNumbers.Split(',');
            var lineIndices = new HashSet<int>();

            foreach (var part in parts)
            {
                var trimmed = part.Trim();
                if (trimmed.Contains('-'))
                {
                    // Range (e.g., "5-8")
                    var rangeParts = trimmed.Split('-');
                    if (rangeParts.Length == 2 && 
                        int.TryParse(rangeParts[0], out int start) && 
                        int.TryParse(rangeParts[1], out int end))
                    {
                        for (int i = start; i <= end; i++)
                        {
                            lineIndices.Add(i);
                        }
                    }
                }
                else
                {
                    // Single line number
                    if (int.TryParse(trimmed, out int lineNum))
                    {
                        lineIndices.Add(lineNum);
                    }
                }
            }

            foreach (var lineIndex in lineIndices.OrderBy(x => x))
            {
                if (lineIndex >= 1 && lineIndex <= lines.Length)
                {
                    extractedLines.Add($"Line {lineIndex}: {lines[lineIndex - 1]}");
                }
            }

            if (extractedLines.Count == 0)
                return "No valid lines found for the specified line numbers";

            return $"Extracted {extractedLines.Count} lines:\n{string.Join("\n", extractedLines)}";
        }
        catch (Exception ex)
        {
            return $"Error extracting lines: {ex.Message}";
        }
    }

    [McpServerTool(Name = "word_count"), Description("Count words, characters, and lines in text.")]
    public static string WordCount(string text)
    {
        try
        {
            var lines = text.Split('\n');
            var words = text.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            var characters = text.Length;
            var charactersNoSpaces = text.Replace(" ", "").Replace("\t", "").Replace("\n", "").Replace("\r", "").Length;

            return $"Text statistics:\n" +
                   $"Lines: {lines.Length}\n" +
                   $"Words: {words.Length}\n" +
                   $"Characters (with spaces): {characters}\n" +
                   $"Characters (without spaces): {charactersNoSpaces}";
        }
        catch (Exception ex)
        {
            return $"Error counting text: {ex.Message}";
        }
    }

    [McpServerTool(Name = "format_text"), Description("Apply formatting to text (uppercase, lowercase, title case).")]
    public static string FormatText(string text, string format)
    {
        try
        {
            string result = format.ToLowerInvariant() switch
            {
                "uppercase" or "upper" => text.ToUpperInvariant(),
                "lowercase" or "lower" => text.ToLowerInvariant(),
                "titlecase" or "title" => ToTitleCase(text),
                "trim" => text.Trim(),
                "remove_extra_spaces" => Regex.Replace(text, @"\s+", " "),
                _ => throw new ArgumentException($"Unsupported format: {format}")
            };

            return $"Applied '{format}' formatting to text.\n\nResult:\n{result}";
        }
        catch (Exception ex)
        {
            return $"Error formatting text: {ex.Message}";
        }
    }

    [McpServerTool(Name = "split_text"), Description("Split text by a delimiter and return the parts.")]
    public static string SplitText(string text, string delimiter, int maxParts = 0)
    {
        try
        {
            string[] parts;
            if (maxParts > 0)
            {
                parts = text.Split(new[] { delimiter }, maxParts, StringSplitOptions.None);
            }
            else
            {
                parts = text.Split(new[] { delimiter }, StringSplitOptions.None);
            }

            var result = new List<string>();
            for (int i = 0; i < parts.Length; i++)
            {
                result.Add($"Part {i + 1}: {parts[i]}");
            }

            return $"Split text into {parts.Length} parts using delimiter '{delimiter}':\n{string.Join("\n", result)}";
        }
        catch (Exception ex)
        {
            return $"Error splitting text: {ex.Message}";
        }
    }

    private static string ToTitleCase(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        var words = text.Split(' ');
        for (int i = 0; i < words.Length; i++)
        {
            if (words[i].Length > 0)
            {
                words[i] = char.ToUpperInvariant(words[i][0]) + 
                          (words[i].Length > 1 ? words[i][1..].ToLowerInvariant() : "");
            }
        }
        return string.Join(" ", words);
    }
}