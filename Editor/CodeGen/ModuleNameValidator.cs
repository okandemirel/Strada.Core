using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;

namespace Strada.Core.Editor.CodeGen
{
    /// <summary>
    /// Validates module names according to Strada conventions.
    /// Checks PascalCase convention, reserved names, and existing module conflicts.
    /// </summary>
    public static class ModuleNameValidator
    {
        /// <summary>
        /// Reserved names that cannot be used as module names.
        /// </summary>
        public static readonly HashSet<string> ReservedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "System", "Object", "String", "Int", "Float", "Double", "Bool", "Void",
            "Class", "Struct", "Interface", "Enum", "Delegate", "Event",
            "Namespace", "Using", "Public", "Private", "Protected", "Internal",
            "Static", "Abstract", "Virtual", "Override", "Sealed", "Readonly",
            "Const", "New", "This", "Base", "Null", "True", "False",

            "Unity", "UnityEngine", "UnityEditor", "MonoBehaviour", "ScriptableObject",
            "GameObject", "Transform", "Component", "Behaviour", "Renderer",
            "Collider", "Rigidbody", "Camera", "Light", "Audio", "Animation",
            "Animator", "Canvas", "Image", "Text", "Button", "Input",

            "Strada", "Core", "Module", "Modules", "Game", "App", "Application",
            "Controller", "Service", "View", "Model", "Manager", "Handler",
            "Factory", "Builder", "Provider", "Repository", "Entity", "World",
            "System", "Bridge", "Bus", "Config", "Data", "State",

            "Base", "Abstract", "Default", "Generic", "Common", "Shared",
            "Utility", "Utilities", "Helper", "Helpers", "Extension", "Extensions",
            "Test", "Tests", "Mock", "Mocks", "Fake", "Fakes"
        };

        /// <summary>
        /// Validates a module name according to PascalCase convention and reserved names.
        /// </summary>
        /// <param name="name">The module name to validate.</param>
        /// <returns>Validation result with success status and error message if invalid.</returns>
        public static ModuleNameValidationResult Validate(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return new ModuleNameValidationResult(false, "Module name cannot be empty.");
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                return new ModuleNameValidationResult(false, "Module name cannot be whitespace only.");
            }

            if (!char.IsUpper(name[0]))
            {
                return new ModuleNameValidationResult(false, 
                    "Module name must start with an uppercase letter (PascalCase convention).");
            }

            if (!Regex.IsMatch(name, @"^[A-Z][a-zA-Z0-9]*$"))
            {
                return new ModuleNameValidationResult(false, 
                    "Module name must contain only letters and numbers, starting with an uppercase letter.");
            }

            if (name.Length < 2)
            {
                return new ModuleNameValidationResult(false, 
                    "Module name must be at least 2 characters long.");
            }

            if (name.Length > 64)
            {
                return new ModuleNameValidationResult(false, 
                    "Module name must be 64 characters or less.");
            }

            if (IsReservedName(name))
            {
                return new ModuleNameValidationResult(false, 
                    $"'{name}' is a reserved name and cannot be used as a module name.");
            }

            if (ModuleExists(name))
            {
                return new ModuleNameValidationResult(false, 
                    $"A module named '{name}Module' already exists in the project.");
            }

            return new ModuleNameValidationResult(true, null);
        }

        /// <summary>
        /// Checks if a name is in the reserved names list.
        /// </summary>
        /// <param name="name">The name to check.</param>
        /// <returns>True if the name is reserved.</returns>
        public static bool IsReservedName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return false;

            return ReservedNames.Contains(name);
        }

        /// <summary>
        /// Checks if a module with the given name already exists.
        /// </summary>
        /// <param name="name">The module name (without 'Module' suffix).</param>
        /// <returns>True if a module with this name exists.</returns>
        public static bool ModuleExists(string name)
        {
            if (string.IsNullOrEmpty(name))
                return false;

            var moduleName = name.EndsWith("Module") ? name : name + "Module";
            var existingModules = FindExistingModuleNames();
            
            return existingModules.Contains(moduleName);
        }

        /// <summary>
        /// Checks if a string follows PascalCase convention.
        /// </summary>
        /// <param name="name">The string to check.</param>
        /// <returns>True if the string is valid PascalCase.</returns>
        public static bool IsPascalCase(string name)
        {
            if (string.IsNullOrEmpty(name))
                return false;

            if (!char.IsUpper(name[0]))
                return false;

            if (!Regex.IsMatch(name, @"^[A-Z][a-zA-Z0-9]*$"))
                return false;

            return true;
        }

        /// <summary>
        /// Sanitizes a string to be a valid module name.
        /// Removes invalid characters and ensures PascalCase.
        /// </summary>
        /// <param name="name">The string to sanitize.</param>
        /// <returns>A sanitized module name.</returns>
        public static string Sanitize(string name)
        {
            if (string.IsNullOrEmpty(name))
                return name;

            var sanitized = Regex.Replace(name, @"[^a-zA-Z0-9]", "");

            if (string.IsNullOrEmpty(sanitized))
                return sanitized;

            return char.ToUpper(sanitized[0]) + sanitized.Substring(1);
        }

        /// <summary>
        /// Finds all existing module names in the project.
        /// </summary>
        /// <returns>Set of existing module names.</returns>
        public static HashSet<string> FindExistingModuleNames()
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var guids = AssetDatabase.FindAssets("t:Script");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var fileName = Path.GetFileNameWithoutExtension(path);
                
                if (fileName.EndsWith("Module", StringComparison.OrdinalIgnoreCase))
                {
                    names.Add(fileName);
                }
            }

            var moduleConfigType = typeof(Modules.ModuleConfig);
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    foreach (var type in assembly.GetTypes())
                    {
                        if (moduleConfigType.IsAssignableFrom(type) &&
                            type.IsClass &&
                            !type.IsAbstract)
                        {
                            names.Add(type.Name);
                        }
                    }
                }
                catch
                {
                }
            }

            return names;
        }
    }

    /// <summary>
    /// Result of module name validation.
    /// </summary>
    public class ModuleNameValidationResult
    {
        /// <summary>
        /// Whether the module name is valid.
        /// </summary>
        public bool IsValid { get; }

        /// <summary>
        /// Error message if validation failed, null otherwise.
        /// </summary>
        public string ErrorMessage { get; }

        /// <summary>
        /// Creates a new validation result.
        /// </summary>
        /// <param name="isValid">Whether validation passed.</param>
        /// <param name="errorMessage">Error message if validation failed.</param>
        public ModuleNameValidationResult(bool isValid, string errorMessage)
        {
            IsValid = isValid;
            ErrorMessage = errorMessage;
        }
    }
}
