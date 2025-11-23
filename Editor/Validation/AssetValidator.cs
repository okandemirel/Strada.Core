using UnityEngine;

namespace Strada.Core.Editor.Validation
{
    /// <summary>
    /// Base class for all asset validators in the Strada framework.
    /// Provides common validation infrastructure and patterns.
    /// </summary>
    public abstract class AssetValidator
    {
        /// <summary>
        /// Validates a specific asset and returns the result.
        /// </summary>
        public abstract ValidationResult Validate(Object asset);

        /// <summary>
        /// Returns true if this validator can validate the given asset type.
        /// </summary>
        public abstract bool CanValidate(Object asset);

        /// <summary>
        /// Gets the display name for this validator.
        /// </summary>
        public abstract string ValidatorName { get; }

        /// <summary>
        /// Gets the category this validator belongs to.
        /// </summary>
        public virtual string Category => "General";

        /// <summary>
        /// Helper method to validate that a field is not null.
        /// </summary>
        protected void ValidateNotNull(ValidationResult result, Object obj, string fieldName, string assetPath)
        {
            if (obj == null)
            {
                result.AddError(
                    $"{fieldName} is null or missing",
                    assetPath,
                    Category,
                    $"Assign a valid reference to {fieldName}"
                );
            }
        }

        /// <summary>
        /// Helper method to validate that a string field is not empty.
        /// </summary>
        protected void ValidateNotEmpty(ValidationResult result, string value, string fieldName, string assetPath)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                result.AddError(
                    $"{fieldName} is empty",
                    assetPath,
                    Category,
                    $"Provide a value for {fieldName}"
                );
            }
        }

        /// <summary>
        /// Helper method to validate that a numeric value is within a range.
        /// </summary>
        protected void ValidateRange(ValidationResult result, float value, float min, float max, string fieldName, string assetPath)
        {
            if (value < min || value > max)
            {
                result.AddError(
                    $"{fieldName} ({value}) is outside valid range [{min}, {max}]",
                    assetPath,
                    Category,
                    $"Set {fieldName} to a value between {min} and {max}"
                );
            }
        }

        /// <summary>
        /// Helper method to add a warning if a value is outside recommended range.
        /// </summary>
        protected void ValidateRecommendedRange(ValidationResult result, float value, float min, float max, string fieldName, string assetPath)
        {
            if (value < min || value > max)
            {
                result.AddWarning(
                    $"{fieldName} ({value}) is outside recommended range [{min}, {max}]",
                    assetPath,
                    Category,
                    $"Consider setting {fieldName} to a value between {min} and {max}"
                );
            }
        }
    }
}
