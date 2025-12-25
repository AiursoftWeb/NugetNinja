namespace Aiursoft.NugetNinja.GeminiBot.Models;

/// <summary>
/// Represents the result of processing an issue or repository.
/// Provides a type-safe way to communicate success/failure/skip states with messages and errors.
/// </summary>
public class ProcessResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public Exception? Error { get; init; }

    public static ProcessResult Succeeded(string message) => new()
    {
        Success = true,
        Message = message
    };

    public static ProcessResult Failed(string message, Exception? error = null) => new()
    {
        Success = false,
        Message = message,
        Error = error
    };

    public static ProcessResult Skipped(string reason) => new()
    {
        Success = true,
        Message = $"Skipped: {reason}"
    };

    public override string ToString() => Success ? $"Success: {Message}" : $"Failed: {Message}";
}
