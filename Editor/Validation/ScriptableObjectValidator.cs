using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Strada.Core.Editor.Validation
{
    /// <summary>
    /// Validates Strada ScriptableObject configurations (CD_* assets).
    /// Checks naming conventions, required fields, and calls IsValid() methods.
    /// </summary>
    public class ScriptableObjectValidator : AssetValidator
    {
        public override string ValidatorName => "ScriptableObject Config Validator";
        public override string Category => "Configuration";

        public override bool CanValidate(Object asset)
        {
            if (asset == null)
                return false;

            return asset is ScriptableObject && asset.GetType().Name.StartsWith("CD_");
        }

        public override ValidationResult Validate(Object asset)
        {
            var result = new ValidationResult();
            var so = asset as ScriptableObject;
            var assetPath = AssetDatabase.GetAssetPath(asset);

            if (so == null)
            {
                result.AddError("Asset is not a ScriptableObject", assetPath, Category);
                return result;
            }

            ValidateNamingConvention(result, so, assetPath);
            ValidateConfigProperty(result, so, assetPath);
            ValidateIsValidMethod(result, so, assetPath);
            ValidateFileLocation(result, assetPath);

            return result;
        }

        private void ValidateNamingConvention(ValidationResult result, ScriptableObject so, string assetPath)
        {
            var typeName = so.GetType().Name;

            if (!typeName.StartsWith("CD_"))
            {
                result.AddWarning(
                    $"ScriptableObject type '{typeName}' does not follow CD_* naming convention",
                    assetPath,
                    Category,
                    "Rename the class to start with CD_"
                );
            }

            var assetName = so.name;
            if (!assetName.StartsWith("CD_"))
            {
                result.AddWarning(
                    $"Asset name '{assetName}' does not follow CD_* naming convention",
                    assetPath,
                    Category,
                    "Rename the asset file to start with CD_"
                );
            }
        }

        private void ValidateConfigProperty(ValidationResult result, ScriptableObject so, string assetPath)
        {
            var type = so.GetType();
            var configField = type.GetField("Config", BindingFlags.Public | BindingFlags.Instance);

            if (configField == null)
            {
                result.AddWarning(
                    $"{type.Name} does not have a public 'Config' field",
                    assetPath,
                    Category,
                    "Add a public Config field to store configuration data"
                );
                return;
            }

            var configValue = configField.GetValue(so);
            if (configValue == null)
            {
                result.AddError(
                    $"{type.Name}.Config is null",
                    assetPath,
                    Category,
                    "Initialize the Config field with a valid instance"
                );
                return;
            }

            ValidateConfigObject(result, configValue, assetPath);
        }

        private void ValidateConfigObject(ValidationResult result, object config, string assetPath)
        {
            var configType = config.GetType();
            var isValidMethod = configType.GetMethod("IsValid", BindingFlags.Public | BindingFlags.Instance);

            if (isValidMethod == null)
            {
                result.AddInfo(
                    $"{configType.Name} does not implement IsValid() method",
                    assetPath,
                    Category
                );
                return;
            }

            try
            {
                var isValid = (bool)isValidMethod.Invoke(config, null);
                if (!isValid)
                {
                    result.AddError(
                        $"{configType.Name}.IsValid() returned false",
                        assetPath,
                        Category,
                        "Fix validation errors in the configuration"
                    );
                }
            }
            catch (System.Exception ex)
            {
                result.AddError(
                    $"Error calling {configType.Name}.IsValid(): {ex.Message}",
                    assetPath,
                    Category
                );
            }
        }

        private void ValidateIsValidMethod(ValidationResult result, ScriptableObject so, string assetPath)
        {
            var type = so.GetType();
            var onValidateMethod = type.GetMethod("OnValidate", BindingFlags.NonPublic | BindingFlags.Instance);

            if (onValidateMethod == null)
            {
                result.AddInfo(
                    $"{type.Name} does not implement OnValidate()",
                    assetPath,
                    Category
                );
            }
        }

        private void ValidateFileLocation(ValidationResult result, string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
                return;

            if (!assetPath.Contains("/Data/") && !assetPath.Contains("/Configs/"))
            {
                result.AddWarning(
                    "ScriptableObject is not in a /Data/ or /Configs/ folder",
                    assetPath,
                    Category,
                    "Move the asset to Assets/Modules/{Module}/Data/ or Assets/Configs/"
                );
            }

            if (!assetPath.Contains("UnityObjects") && assetPath.Contains("/Data/"))
            {
                result.AddWarning(
                    "ScriptableObject is not in /Data/UnityObjects/ folder",
                    assetPath,
                    Category,
                    "Move the asset to the UnityObjects subfolder"
                );
            }
        }
    }
}
