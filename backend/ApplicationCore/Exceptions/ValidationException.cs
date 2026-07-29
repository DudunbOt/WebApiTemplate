namespace ApplicationCore.Exceptions;

public class ValidationException : Exception
{
    public Dictionary<string, List<string>> Errors { get; }

    public ValidationException() : base("One or more validation errors occurred.")
    {
        Errors = new Dictionary<string, List<string>>();
    }

    public ValidationException(string message) : base(message)
    {
        Errors = new Dictionary<string, List<string>>();
    }

    public ValidationException(Dictionary<string, List<string>> errors) : base("One or more validation errors occurred.")
    {
        Errors = errors;
    }

    public ValidationException(string propertyName, string errorMessage) : base("One or more validation errors occurred.")
    {
        Errors = new Dictionary<string, List<string>>
        {
            { propertyName, new List<string> { errorMessage } }
        };
    }
}