using System;
using System.Collections.Generic;
using System.Reflection;
using Strada.Core.Patterns;
using UnityEngine;

namespace Strada.Core.Editor.Validation
{
    /// <summary>
    /// Validates that Services do not have Unity-specific dependencies that affect testability.
    /// Requirements: 14.4
    /// </summary>
    public class ServiceUnityDependencyRule : IArchitectureRule
    {
        public string RuleId => "STRADA004";
        public string RuleName => "Service Unity Dependencies";
        public string Description => "Services should avoid Unity-specific dependencies (MonoBehaviour, Transform, GameObject) for better testability";

        private static readonly Type[] UnityTypes = new[]
        {
            typeof(MonoBehaviour),
            typeof(Component),
            typeof(Transform),
            typeof(GameObject),
            typeof(ScriptableObject)
        };

        private static readonly string[] UnityTypeNames = new[]
        {
            "MonoBehaviour",
            "Component",
            "Transform",
            "GameObject",
            "ScriptableObject",
            "Rigidbody",
            "Rigidbody2D",
            "Collider",
            "Collider2D",
            "Camera",
            "AudioSource",
            "Animator",
            "Canvas",
            "RectTransform"
        };

        public bool AppliesTo(Type type)
        {
            if (type == null || type.IsInterface || type.IsAbstract)
                return false;

            return typeof(Service).IsAssignableFrom(type);
        }

        public IEnumerable<ValidationIssue> Validate(Type type)
        {
            var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Static | 
                                        BindingFlags.Public | BindingFlags.NonPublic);
            foreach (var field in fields)
            {
                if (IsUnityType(field.FieldType))
                {
                    yield return new ValidationIssue(
                        ValidationSeverity.Warning,
                        $"Service '{type.Name}' has Unity dependency field '{field.Name}' of type '{field.FieldType.Name}'",
                        "Consider using interfaces or abstractions to improve testability. " +
                        "Inject Unity dependencies through constructor or use a wrapper service.");
                }
            }

            var properties = type.GetProperties(BindingFlags.Instance | BindingFlags.Static | 
                                                BindingFlags.Public | BindingFlags.NonPublic);
            foreach (var prop in properties)
            {
                if (IsUnityType(prop.PropertyType))
                {
                    yield return new ValidationIssue(
                        ValidationSeverity.Warning,
                        $"Service '{type.Name}' has Unity dependency property '{prop.Name}' of type '{prop.PropertyType.Name}'",
                        "Consider using interfaces or abstractions to improve testability.");
                }
            }

            var constructors = type.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            foreach (var ctor in constructors)
            {
                foreach (var param in ctor.GetParameters())
                {
                    if (IsUnityType(param.ParameterType))
                    {
                        yield return new ValidationIssue(
                            ValidationSeverity.Warning,
                            $"Service '{type.Name}' constructor takes Unity dependency '{param.Name}' of type '{param.ParameterType.Name}'",
                            "Consider injecting an interface wrapper instead of the concrete Unity type.");
                    }
                }
            }

            var methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Static | 
                                          BindingFlags.Public | BindingFlags.NonPublic | 
                                          BindingFlags.DeclaredOnly);
            foreach (var method in methods)
            {
                if (method.IsSpecialName)
                    continue;

                foreach (var param in method.GetParameters())
                {
                    if (IsUnityType(param.ParameterType))
                    {
                        yield return new ValidationIssue(
                            ValidationSeverity.Warning,
                            $"Service '{type.Name}' method '{method.Name}' takes Unity dependency '{param.Name}' of type '{param.ParameterType.Name}'",
                            "Consider using interfaces or data transfer objects instead of Unity types.");
                    }
                }
            }
        }

        /// <summary>
        /// Checks if a type is a Unity-specific type that affects testability.
        /// </summary>
        public static bool IsUnityType(Type type)
        {
            if (type == null)
                return false;

            foreach (var unityType in UnityTypes)
            {
                if (type == unityType || type.IsSubclassOf(unityType))
                    return true;
            }

            var typeName = type.Name;
            foreach (var unityTypeName in UnityTypeNames)
            {
                if (typeName == unityTypeName)
                    return true;
            }

            if (type.Namespace != null && type.Namespace.StartsWith("UnityEngine"))
            {
                if (type.IsValueType || type.IsEnum)
                    return false;

                if (type == typeof(Vector2) || type == typeof(Vector3) || type == typeof(Vector4) ||
                    type == typeof(Quaternion) || type == typeof(Color) || type == typeof(Color32) ||
                    type == typeof(Matrix4x4) || type == typeof(Rect) || type == typeof(Bounds))
                    return false;

                return true;
            }

            if (type.IsGenericType)
            {
                foreach (var arg in type.GetGenericArguments())
                {
                    if (IsUnityType(arg))
                        return true;
                }
            }

            return false;
        }
    }
}
