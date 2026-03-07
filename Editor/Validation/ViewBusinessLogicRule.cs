using System;
using System.Collections.Generic;
using System.Reflection;
using Strada.Core.Patterns;

namespace Strada.Core.Editor.Validation
{
    /// <summary>
    /// Validates that Views do not contain business logic that should be in Controllers.
    /// Requirements: 14.3
    /// </summary>
    public class ViewBusinessLogicRule : IArchitectureRule
    {
        public string RuleId => "STRADA003";
        public string RuleName => "View Business Logic";
        public string Description => "Views should not contain business logic; move complex logic to Controllers";

        private static readonly string[] BusinessLogicMethodPatterns = new[]
        {
            "Calculate",
            "Compute",
            "Process",
            "Validate",
            "Execute",
            "Handle",
            "Perform",
            "Apply",
            "Transform",
            "Convert",
            "Parse",
            "Serialize",
            "Deserialize",
            "Save",
            "Load",
            "Fetch",
            "Send",
            "Receive"
        };

        private static readonly string[] AllowedMethodPrefixes = new[]
        {
            "On",      // Event handlers
            "Update",  // UI updates
            "Refresh", // UI refresh
            "Show",    // Display
            "Hide",    // Display
            "Set",     // Property setters
            "Get",     // Property getters
            "Init",    // Initialization
            "Awake",   // Unity lifecycle
            "Start",   // Unity lifecycle
            "Enable",  // Unity lifecycle
            "Disable", // Unity lifecycle
            "Destroy", // Unity lifecycle
            "Draw",    // Rendering
            "Render",  // Rendering
            "Animate", // Animation
            "Play",    // Animation/Audio
            "Stop",    // Animation/Audio
            "Bind",    // Data binding
            "Unbind"   // Data binding
        };

        public bool AppliesTo(Type type)
        {
            if (type == null || type.IsInterface || type.IsAbstract)
                return false;

            return typeof(View).IsAssignableFrom(type);
        }

        public IEnumerable<ValidationIssue> Validate(Type type)
        {
            var methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Static | 
                                          BindingFlags.Public | BindingFlags.NonPublic | 
                                          BindingFlags.DeclaredOnly);

            foreach (var method in methods)
            {
                if (method.IsSpecialName)
                    continue;

                if (HasBusinessLogicMethodName(method.Name))
                {
                    yield return new ValidationIssue(
                        ValidationSeverity.Warning,
                        $"View '{type.Name}' has method '{method.Name}' which suggests business logic",
                        $"Consider moving '{method.Name}' to a Controller and having the View call it through events");
                }

                if (method.GetParameters().Length > 5)
                {
                    yield return new ValidationIssue(
                        ValidationSeverity.Warning,
                        $"View '{type.Name}' method '{method.Name}' has {method.GetParameters().Length} parameters, suggesting complex logic",
                        "Consider simplifying or moving this logic to a Controller");
                }

                foreach (var param in method.GetParameters())
                {
                    if (IsServiceType(param.ParameterType))
                    {
                        yield return new ValidationIssue(
                            ValidationSeverity.Warning,
                            $"View '{type.Name}' method '{method.Name}' takes service parameter '{param.Name}'",
                            "Views should not directly interact with services; use Controllers as intermediaries");
                    }
                }
            }

            var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Static | 
                                        BindingFlags.Public | BindingFlags.NonPublic);
            foreach (var field in fields)
            {
                if (IsServiceType(field.FieldType))
                {
                    yield return new ValidationIssue(
                        ValidationSeverity.Warning,
                        $"View '{type.Name}' has service field '{field.Name}' of type '{field.FieldType.Name}'",
                        "Views should not hold references to services; use Controllers to coordinate with services");
                }
            }
        }

        /// <summary>
        /// Checks if a method name suggests business logic.
        /// </summary>
        public static bool HasBusinessLogicMethodName(string methodName)
        {
            if (string.IsNullOrEmpty(methodName))
                return false;

            foreach (var prefix in AllowedMethodPrefixes)
            {
                if (methodName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            foreach (var pattern in BusinessLogicMethodPatterns)
            {
                if (methodName.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Checks if a type is a service type.
        /// </summary>
        private static bool IsServiceType(Type type)
        {
            if (type == null)
                return false;

            if (typeof(Service).IsAssignableFrom(type))
                return true;

            var typeName = type.Name;
            if (typeName.EndsWith("Service") || 
                typeName.EndsWith("Repository") || 
                typeName.EndsWith("Manager") ||
                typeName.EndsWith("Handler"))
            {
                return true;
            }

            return false;
        }
    }
}
