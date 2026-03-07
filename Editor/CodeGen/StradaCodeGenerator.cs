using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Strada.Core.Editor.CodeGen
{
    public static class StradaCodeGenerator
    {
        internal const string GeneratedFolder = "Assets/Strada.Generated";

        [MenuItem("Strada/Generate All Code", priority = 0)]
        public static void GenerateAll()
        {
            Debug.Log("[Strada] Starting code generation...");

            SystemRegistryGenerator.GenerateSystemRegistry();
            ModuleInitializerGenerator.GenerateModuleRegistry();

            Debug.Log("[Strada] Code generation complete!");
        }

        [MenuItem("Strada/Clean Generated Code")]
        public static void CleanGeneratedCode()
        {
            if (System.IO.Directory.Exists(GeneratedFolder))
            {
                System.IO.Directory.Delete(GeneratedFolder, true);
                var metaFile = GeneratedFolder + ".meta";
                if (System.IO.File.Exists(metaFile))
                    System.IO.File.Delete(metaFile);

                AssetDatabase.Refresh();
                Debug.Log("[Strada] Cleaned generated code.");
            }
            else
            {
                Debug.Log("[Strada] No generated code folder found.");
            }
        }

        /// <summary>
        /// Gets the fully qualified type name suitable for code generation.
        /// Handles nested types and generic types.
        /// </summary>
        internal static string GetFullTypeName(Type type)
        {
            if (!type.IsGenericType)
                return type.FullName?.Replace("+", ".") ?? type.Name;

            var genericDef = type.GetGenericTypeDefinition();
            var baseName = genericDef.FullName;
            if (baseName == null)
                return type.Name;

            var tickIndex = baseName.IndexOf('`');
            if (tickIndex > 0)
                baseName = baseName.Substring(0, tickIndex);

            var args = type.GetGenericArguments();
            var argNames = string.Join(", ", args.Select(GetFullTypeName));

            return $"{baseName.Replace("+", ".")}<{argNames}>";
        }

        /// <summary>
        /// Ensures the generated code folder exists.
        /// </summary>
        internal static void EnsureGeneratedFolder()
        {
            if (!System.IO.Directory.Exists(GeneratedFolder))
                System.IO.Directory.CreateDirectory(GeneratedFolder);
        }
    }
}
