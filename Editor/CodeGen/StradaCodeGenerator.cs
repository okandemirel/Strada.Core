using UnityEditor;
using UnityEngine;

namespace Strada.Core.Editor.CodeGen
{
    public static class StradaCodeGenerator
    {
        [MenuItem("Strada/Generate All Code", priority = 0)]
        public static void GenerateAll()
        {
            Debug.Log("[Strada] Starting code generation...");

            SystemRegistryGenerator.GenerateSystemRegistry();
            ModuleInitializerGenerator.GenerateModuleInitializer();

            Debug.Log("[Strada] Code generation complete!");
        }

        [MenuItem("Strada/Clean Generated Code")]
        public static void CleanGeneratedCode()
        {
            const string folder = "Assets/Strada.Generated";

            if (System.IO.Directory.Exists(folder))
            {
                System.IO.Directory.Delete(folder, true);
                var metaFile = folder + ".meta";
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
    }
}
