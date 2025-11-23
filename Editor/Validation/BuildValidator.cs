using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Strada.Core.Editor.Validation
{
    /// <summary>
    /// Validates the project before building.
    /// Can block builds if critical validation errors are found.
    /// </summary>
    public class BuildValidator : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        private const string PREF_KEY_VALIDATE_ON_BUILD = "Strada.ValidateOnBuild";
        private const string PREF_KEY_BLOCK_ON_ERRORS = "Strada.BlockBuildOnErrors";

        [MenuItem("Tools/Strada/Build Validation/Enable Validation on Build")]
        public static void EnableValidationOnBuild()
        {
            EditorPrefs.SetBool(PREF_KEY_VALIDATE_ON_BUILD, true);
            Debug.Log("[Strada] Build validation enabled");
        }

        [MenuItem("Tools/Strada/Build Validation/Disable Validation on Build")]
        public static void DisableValidationOnBuild()
        {
            EditorPrefs.SetBool(PREF_KEY_VALIDATE_ON_BUILD, false);
            Debug.Log("[Strada] Build validation disabled");
        }

        [MenuItem("Tools/Strada/Build Validation/Enable Build Blocking on Errors")]
        public static void EnableBuildBlocking()
        {
            EditorPrefs.SetBool(PREF_KEY_BLOCK_ON_ERRORS, true);
            Debug.Log("[Strada] Build blocking on errors enabled");
        }

        [MenuItem("Tools/Strada/Build Validation/Disable Build Blocking on Errors")]
        public static void DisableBuildBlocking()
        {
            EditorPrefs.SetBool(PREF_KEY_BLOCK_ON_ERRORS, false);
            Debug.Log("[Strada] Build blocking on errors disabled");
        }

        [MenuItem("Tools/Strada/Build Validation/Enable Validation on Build", true)]
        public static bool EnableValidationOnBuild_Validate()
        {
            return !EditorPrefs.GetBool(PREF_KEY_VALIDATE_ON_BUILD, true);
        }

        [MenuItem("Tools/Strada/Build Validation/Disable Validation on Build", true)]
        public static bool DisableValidationOnBuild_Validate()
        {
            return EditorPrefs.GetBool(PREF_KEY_VALIDATE_ON_BUILD, true);
        }

        [MenuItem("Tools/Strada/Build Validation/Enable Build Blocking on Errors", true)]
        public static bool EnableBuildBlocking_Validate()
        {
            return !EditorPrefs.GetBool(PREF_KEY_BLOCK_ON_ERRORS, false);
        }

        [MenuItem("Tools/Strada/Build Validation/Disable Build Blocking on Errors", true)]
        public static bool DisableBuildBlocking_Validate()
        {
            return EditorPrefs.GetBool(PREF_KEY_BLOCK_ON_ERRORS, false);
        }

        public void OnPreprocessBuild(BuildReport report)
        {
            var validateOnBuild = EditorPrefs.GetBool(PREF_KEY_VALIDATE_ON_BUILD, true);

            if (!validateOnBuild)
            {
                Debug.Log("[Strada] Build validation is disabled. Skipping validation.");
                return;
            }

            Debug.Log("[Strada] Running pre-build validation...");

            var result = RunValidation();

            DisplayResults(result);

            var blockOnErrors = EditorPrefs.GetBool(PREF_KEY_BLOCK_ON_ERRORS, false);

            if (!result.IsValid && blockOnErrors)
            {
                var message = $"Build failed: {result.ErrorCount} validation error(s) found.\n\n" +
                             "Fix the errors or disable build blocking via:\n" +
                             "Tools > Strada > Build Validation > Disable Build Blocking on Errors";

                throw new BuildFailedException(message);
            }

            if (result.HasWarnings)
            {
                Debug.LogWarning($"[Strada] Build proceeding with {result.WarningCount} warning(s)");
            }

            if (result.IsValid)
            {
                Debug.Log("[Strada] All validation checks passed!");
            }
        }

        private ValidationResult RunValidation()
        {
            var result = new ValidationResult();

            var moduleResult = ModuleValidator.ValidateAllModules();
            result.Merge(moduleResult);

            var validator = new ScriptableObjectValidator();
            var guids = AssetDatabase.FindAssets("t:ScriptableObject");

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);

                if (asset != null && validator.CanValidate(asset))
                {
                    var assetResult = validator.Validate(asset);
                    result.Merge(assetResult);
                }
            }

            return result;
        }

        private void DisplayResults(ValidationResult result)
        {
            Debug.Log($"[Strada] Validation complete: {result.ErrorCount} errors, {result.WarningCount} warnings, {result.InfoCount} info");

            foreach (var message in result.Messages)
            {
                var logMessage = $"[{message.Category}] {message.Message}";

                if (!string.IsNullOrEmpty(message.AssetPath))
                {
                    logMessage += $"\nAsset: {message.AssetPath}";
                }

                if (!string.IsNullOrEmpty(message.FixSuggestion))
                {
                    logMessage += $"\nSuggestion: {message.FixSuggestion}";
                }

                switch (message.Severity)
                {
                    case ValidationResult.Severity.Error:
                        Debug.LogError(logMessage);
                        break;
                    case ValidationResult.Severity.Warning:
                        Debug.LogWarning(logMessage);
                        break;
                    case ValidationResult.Severity.Info:
                        Debug.Log(logMessage);
                        break;
                }
            }
        }
    }
}
