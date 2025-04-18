namespace TgStatsApp.Models;

public record ApiResponse<TResult>
{
    public ApiResponse(
        TResult result,
        string errorMessage)
    {
        Result = result;
        ErrorMessage = errorMessage;
    }

    public TResult Result { get; }

    public string ErrorMessage { get; }

    public bool IsSuccess => string.IsNullOrEmpty(ErrorMessage);
}