namespace WealthLedger.WebApp.Models;

public class Error
{
    public string Message { get; set; }
    public string? Code { get; set; }
    public string? Field { get; set; }

    public Error(string message)
    {
        Message = message;
    }

    public Error(string message, string code)
    {
        Message = message;
        Code = code;
    }

    public Error(string message, string code, string field)
    {
        Message = message;
        Code = code;
        Field = field;
    }

    public static Error ForField(string field, string message) => new(message, "ValidationError", field);

    public static implicit operator Error(string message) => new(message);
}
