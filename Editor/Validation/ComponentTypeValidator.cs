using System;
using System.Collections.Generic;
using System.Reflection;
using Strada.Core.ECS;

namespace Strada.Core.Editor.Validation
{
    /// <summary>
    /// Validates that IComponent implementations are unmanaged structs without reference type fields.
    /// Requirements: 13.4, 14.5
    /// </summary>
    public class ComponentTypeValidator : IArchitectureRule
    {
        public string RuleId => "STRADA001";
        public string RuleName => "Component Type Validation";
        public string Description => "IComponent implementations must be unmanaged structs without reference type fields";

        public bool AppliesTo(Type type)
        {
            if (type == null || type.IsInterface || type.IsAbstract)
                return false;

            return typeof(IComponent).IsAssignableFrom(type);
        }

        public IEnumerable<ValidationIssue> Validate(Type type)
        {
            if (!type.IsValueType)
            {
                yield return new ValidationIssue(
                    ValidationSeverity.Error,
                    $"IComponent '{type.Name}' must be a struct, not a class",
                    $"Change 'class {type.Name}' to 'struct {type.Name}'");
                yield break;
            }

            if (!IsUnmanagedType(type))
            {
                yield return new ValidationIssue(
                    ValidationSeverity.Error,
                    $"IComponent '{type.Name}' must be an unmanaged type",
                    "Remove all reference type fields and ensure all nested types are unmanaged");
            }

            foreach (var field in GetReferenceTypeFields(type))
            {
                yield return new ValidationIssue(
                    ValidationSeverity.Error,
                    $"IComponent '{type.Name}' contains reference type field '{field.Name}' of type '{field.FieldType.Name}'",
                    $"Remove or replace field '{field.Name}' with a value type");
            }
        }

        /// <summary>
        /// Checks if a type is unmanaged (contains no reference types).
        /// </summary>
        public static bool IsUnmanagedType(Type type)
        {
            if (type == null)
                return false;

            if (type.IsPrimitive || type.IsEnum || type.IsPointer)
                return true;

            if (!type.IsValueType)
                return false;

            if (type.IsGenericType && !type.IsConstructedGenericType)
                return false;

            var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            foreach (var field in fields)
            {
                if (!IsUnmanagedType(field.FieldType))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Gets all reference type fields in a type (including nested structs).
        /// </summary>
        public static IEnumerable<FieldInfo> GetReferenceTypeFields(Type type)
        {
            if (type == null)
                yield break;

            var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            foreach (var field in fields)
            {
                if (!field.FieldType.IsValueType)
                {
                    yield return field;
                }
                else if (!field.FieldType.IsPrimitive && !field.FieldType.IsEnum)
                {
                    foreach (var nestedField in GetReferenceTypeFields(field.FieldType))
                    {
                        yield return nestedField;
                    }
                }
            }
        }

        /// <summary>
        /// Validates a specific type and returns whether it's a valid component.
        /// </summary>
        public static bool IsValidComponent(Type type, out string error)
        {
            error = null;

            if (type == null)
            {
                error = "Type is null";
                return false;
            }

            if (!typeof(IComponent).IsAssignableFrom(type))
            {
                error = $"Type '{type.Name}' does not implement IComponent";
                return false;
            }

            if (!type.IsValueType)
            {
                error = $"IComponent '{type.Name}' must be a struct";
                return false;
            }

            if (!IsUnmanagedType(type))
            {
                error = $"IComponent '{type.Name}' must be an unmanaged type";
                return false;
            }

            return true;
        }
    }
}
