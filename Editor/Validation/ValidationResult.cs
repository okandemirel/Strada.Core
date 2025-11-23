using System.Collections.Generic;

namespace Strada.Core.Editor.Validation
{
    /// <summary>
    /// Represents the result of a validation operation.
    /// Contains errors, warnings, and informational messages.
    /// </summary>
    public class ValidationResult
    {
        public enum Severity
        {
            Info,
            Warning,
            Error
        }

        public class ValidationMessage
        {
            public Severity Severity { get; set; }
            public string Message { get; set; }
            public string AssetPath { get; set; }
            public string Category { get; set; }
            public string FixSuggestion { get; set; }
        }

        public List<ValidationMessage> Messages { get; private set; }

        public int ErrorCount => Messages.FindAll(m => m.Severity == Severity.Error).Count;
        public int WarningCount => Messages.FindAll(m => m.Severity == Severity.Warning).Count;
        public int InfoCount => Messages.FindAll(m => m.Severity == Severity.Info).Count;

        public bool IsValid => ErrorCount == 0;
        public bool HasWarnings => WarningCount > 0;

        public ValidationResult()
        {
            Messages = new List<ValidationMessage>();
        }

        public void AddError(string message, string assetPath = "", string category = "", string fixSuggestion = "")
        {
            Messages.Add(new ValidationMessage
            {
                Severity = Severity.Error,
                Message = message,
                AssetPath = assetPath,
                Category = category,
                FixSuggestion = fixSuggestion
            });
        }

        public void AddWarning(string message, string assetPath = "", string category = "", string fixSuggestion = "")
        {
            Messages.Add(new ValidationMessage
            {
                Severity = Severity.Warning,
                Message = message,
                AssetPath = assetPath,
                Category = category,
                FixSuggestion = fixSuggestion
            });
        }

        public void AddInfo(string message, string assetPath = "", string category = "")
        {
            Messages.Add(new ValidationMessage
            {
                Severity = Severity.Info,
                Message = message,
                AssetPath = assetPath,
                Category = category
            });
        }

        public void Merge(ValidationResult other)
        {
            Messages.AddRange(other.Messages);
        }

        public void Clear()
        {
            Messages.Clear();
        }
    }
}
