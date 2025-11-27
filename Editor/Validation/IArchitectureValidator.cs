using System;
using System.Collections.Generic;

namespace Strada.Core.Editor.Validation
{
    /// <summary>
    /// Severity level for validation issues.
    /// </summary>
    public enum ValidationSeverity
    {
        Info,
        Warning,
        Error
    }

    /// <summary>
    /// Represents a validation issue found during architecture validation.
    /// </summary>
    public struct ValidationIssue
    {
        public ValidationSeverity Severity;
        public string Message;
        public string FilePath;
        public int LineNumber;
        public string SuggestedFix;
        public Type RelatedType;

        public ValidationIssue(ValidationSeverity severity, string message, string suggestedFix = null)
        {
            Severity = severity;
            Message = message;
            FilePath = null;
            LineNumber = 0;
            SuggestedFix = suggestedFix;
            RelatedType = null;
        }

        public ValidationIssue WithFile(string filePath, int lineNumber = 0)
        {
            FilePath = filePath;
            LineNumber = lineNumber;
            return this;
        }

        public ValidationIssue WithType(Type type)
        {
            RelatedType = type;
            return this;
        }
    }

    /// <summary>
    /// Interface for architecture validation rules.
    /// </summary>
    public interface IArchitectureRule
    {
        /// <summary>
        /// Unique identifier for this rule.
        /// </summary>
        string RuleId { get; }

        /// <summary>
        /// Human-readable name of the rule.
        /// </summary>
        string RuleName { get; }

        /// <summary>
        /// Description of what this rule validates.
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Validates a type against this rule.
        /// </summary>
        IEnumerable<ValidationIssue> Validate(Type type);

        /// <summary>
        /// Checks if this rule applies to the given type.
        /// </summary>
        bool AppliesTo(Type type);
    }


    /// <summary>
    /// Interface for the architecture validator service.
    /// </summary>
    public interface IArchitectureValidator
    {
        /// <summary>
        /// Validates a type against all registered rules.
        /// </summary>
        IEnumerable<ValidationIssue> ValidateType(Type type);

        /// <summary>
        /// Validates all types in an assembly.
        /// </summary>
        IEnumerable<ValidationIssue> ValidateAssembly(System.Reflection.Assembly assembly);

        /// <summary>
        /// Gets all registered validation rules.
        /// </summary>
        IReadOnlyList<IArchitectureRule> Rules { get; }

        /// <summary>
        /// Registers a new validation rule.
        /// </summary>
        void RegisterRule(IArchitectureRule rule);

        /// <summary>
        /// Unregisters a validation rule by its ID.
        /// </summary>
        bool UnregisterRule(string ruleId);
    }
}
