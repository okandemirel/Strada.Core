namespace Strada.Core.Editor.ModuleGenerator.Models
{
    /// <summary>
    /// Validation message with severity level.
    /// </summary>
    public class ValidationMessage
    {
        public ValidationSeverity Severity { get; }
        public string Message { get; }
        public string Field { get; }

        public ValidationMessage(ValidationSeverity severity, string message, string field = null)
        {
            Severity = severity;
            Message = message;
            Field = field;
        }

        public static ValidationMessage Error(string message, string field = null)
            => new ValidationMessage(ValidationSeverity.Error, message, field);

        public static ValidationMessage Warning(string message, string field = null)
            => new ValidationMessage(ValidationSeverity.Warning, message, field);

        public static ValidationMessage Info(string message, string field = null)
            => new ValidationMessage(ValidationSeverity.Info, message, field);
    }

    public enum ValidationSeverity
    {
        Info,
        Warning,
        Error
    }
}
