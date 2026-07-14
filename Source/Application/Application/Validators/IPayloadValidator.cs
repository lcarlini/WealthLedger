namespace WealthLedger.Application.Validators;

public interface IPayloadValidator<in T>
{
    Task<ValidationResult> ValidateAsync(T payload);
}

public class ValidationResult
{
    public bool IsValid => Errors.Count == 0;
    public List<ValidationError> Errors { get; } = [];

    public void AddError(string propertyName, string errorMessage)
    {
        Errors.Add(new ValidationError(propertyName, errorMessage));
    }
}

public class ValidationError
{
    public string PropertyName { get; }
    public string ErrorMessage { get; }

    public ValidationError(string propertyName, string errorMessage)
    {
        PropertyName = propertyName;
        ErrorMessage = errorMessage;
    }
}
