using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Strada.Core.ECS;
using Strada.Core.ECS.Core;
using Strada.Core.Patterns;

namespace Strada.Core.Editor.Validation
{
    /// <summary>
    /// Validates that Controllers do not directly access ECS components without using the Bridge layer.
    /// Requirements: 14.2
    /// </summary>
    public class ControllerEcsAccessRule : IArchitectureRule
    {
        public string RuleId => "STRADA002";
        public string RuleName => "Controller ECS Access";
        public string Description => "Controllers should not directly access EntityManager or World; use the Sync layer instead";

        private static readonly Type[] ForbiddenTypes = new[]
        {
            typeof(EntityManager),
            typeof(Entity),
        };

        private static readonly string[] ForbiddenTypeNames = new[]
        {
            "EntityManager",
            "World",
            "ComponentStore",
            "SparseSet",
            "EntityQuery"
        };

        public bool AppliesTo(Type type)
        {
            if (type == null || type.IsInterface || type.IsAbstract)
                return false;

            return typeof(Controller).IsAssignableFrom(type);
        }

        public IEnumerable<ValidationIssue> Validate(Type type)
        {
            var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Static | 
                                        BindingFlags.Public | BindingFlags.NonPublic);
            foreach (var field in fields)
            {
                if (IsEcsType(field.FieldType))
                {
                    yield return new ValidationIssue(
                        ValidationSeverity.Warning,
                        $"Controller '{type.Name}' has field '{field.Name}' of ECS type '{field.FieldType.Name}'",
                        "Use EntityMediator or EventBus to interact with ECS instead of direct access");
                }
            }

            var properties = type.GetProperties(BindingFlags.Instance | BindingFlags.Static | 
                                                BindingFlags.Public | BindingFlags.NonPublic);
            foreach (var prop in properties)
            {
                if (IsEcsType(prop.PropertyType))
                {
                    yield return new ValidationIssue(
                        ValidationSeverity.Warning,
                        $"Controller '{type.Name}' has property '{prop.Name}' of ECS type '{prop.PropertyType.Name}'",
                        "Use EntityMediator or EventBus to interact with ECS instead of direct access");
                }
            }

            var methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Static | 
                                          BindingFlags.Public | BindingFlags.NonPublic | 
                                          BindingFlags.DeclaredOnly);
            foreach (var method in methods)
            {
                if (IsEcsType(method.ReturnType))
                {
                    yield return new ValidationIssue(
                        ValidationSeverity.Warning,
                        $"Controller '{type.Name}' method '{method.Name}' returns ECS type '{method.ReturnType.Name}'",
                        "Use EntityMediator or EventBus to interact with ECS instead of direct access");
                }

                foreach (var param in method.GetParameters())
                {
                    if (IsEcsType(param.ParameterType))
                    {
                        yield return new ValidationIssue(
                            ValidationSeverity.Warning,
                            $"Controller '{type.Name}' method '{method.Name}' has parameter '{param.Name}' of ECS type '{param.ParameterType.Name}'",
                            "Use EntityMediator or EventBus to interact with ECS instead of direct access");
                    }
                }
            }
        }

        /// <summary>
        /// Checks if a type is an ECS-related type that Controllers shouldn't access directly.
        /// </summary>
        public static bool IsEcsType(Type type)
        {
            if (type == null)
                return false;

            foreach (var forbidden in ForbiddenTypes)
            {
                if (type == forbidden || type.IsSubclassOf(forbidden))
                    return true;
            }

            var typeName = type.Name;
            if (ForbiddenTypeNames.Contains(typeName))
                return true;

            if (type.Namespace != null &&
                type.Namespace.Contains("Strada.Core.ECS") &&
                !typeof(IComponent).IsAssignableFrom(type) &&
                type != typeof(IComponent))
            {
                return true;
            }

            if (type.IsGenericType)
            {
                foreach (var arg in type.GetGenericArguments())
                {
                    if (IsEcsType(arg))
                        return true;
                }
            }

            return false;
        }
    }
}
