using System.Text;
using Strada.Core.Editor.ModuleGenerator.Config;

namespace Strada.Core.Editor.ModuleGenerator
{
    /// <summary>
    /// Processes templates and generates code.
    /// </summary>
    public static class TemplateProcessor
    {
        public static string GeneratePreview(string fileName, string moduleName, string ns, StradaGeneratorSettings settings)
        {
            if (fileName.EndsWith("ModuleConfig.cs"))
                return GenerateModuleConfigPreview(moduleName, ns, settings);

            if (fileName.StartsWith("I") && fileName.Contains("Service"))
                return GenerateServiceInterfacePreview(moduleName, ns, settings);

            if (fileName.EndsWith("Service.cs"))
                return GenerateServicePreview(moduleName, ns, settings);

            if (fileName.EndsWith("Controller.cs"))
                return GenerateControllerPreview(moduleName, ns, settings);

            if (fileName.EndsWith("Model.cs"))
                return GenerateModelPreview(moduleName, ns, settings);

            if (fileName.EndsWith("View.cs"))
                return GenerateViewPreview(moduleName, ns, settings);

            if (fileName.EndsWith("System.cs"))
                return GenerateSystemPreview(moduleName, ns, settings);

            if (fileName.EndsWith("Component.cs"))
                return GenerateComponentPreview(moduleName, ns, settings);

            if (fileName.StartsWith("CD_"))
                return GenerateConfigDataPreview(moduleName, ns, settings);

            if (fileName.EndsWith("Config.cs"))
                return GenerateValueObjectPreview(moduleName, ns, settings);

            if (fileName.EndsWith("Events.cs"))
                return GenerateEventsPreview(moduleName, ns, settings);

            if (fileName.EndsWith("Signals.cs"))
                return GenerateSignalsPreview(moduleName, ns, settings);

            return $"// Preview not available for {fileName}";
        }

        private static string GenerateModuleConfigPreview(string name, string ns, StradaGeneratorSettings settings)
        {
            var sb = new StringBuilder();

            sb.AppendLine("using UnityEngine;");
            sb.AppendLine("using Strada.Core.DI;");
            sb.AppendLine("using Strada.Core.Modules;");
            sb.AppendLine();
            sb.AppendLine($"namespace {ns}");
            sb.AppendLine("{");

            if (settings.GenerateSummaries)
            {
                sb.AppendLine("    /// <summary>");
                sb.AppendLine($"    /// Module configuration for {name}.");
                sb.AppendLine("    /// </summary>");
            }

            sb.AppendLine($"    [CreateAssetMenu(fileName = \"{name}ModuleConfig\", menuName = \"{name}/Module Config\")]");
            sb.AppendLine($"    public class {name}ModuleConfig : ModuleConfig");
            sb.AppendLine("    {");
            sb.AppendLine("        protected override void Configure(IModuleBuilder builder)");
            sb.AppendLine("        {");
            sb.AppendLine($"            builder.RegisterService<I{name}Service, {name}Service>();");
            sb.AppendLine($"            builder.RegisterController<{name}Controller>();");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        public override void Initialize(IServiceLocator services)");
            sb.AppendLine("        {");
            sb.AppendLine("            var container = services.Get<IContainer>();");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        public override void Shutdown()");
            sb.AppendLine("        {");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
        }

        private static string GenerateServiceInterfacePreview(string name, string ns, StradaGeneratorSettings settings)
        {
            var sb = new StringBuilder();

            sb.AppendLine($"namespace {ns}");
            sb.AppendLine("{");

            if (settings.GenerateSummaries)
            {
                sb.AppendLine("    /// <summary>");
                sb.AppendLine($"    /// Service interface for {name} functionality.");
                sb.AppendLine("    /// </summary>");
            }

            sb.AppendLine($"    public interface I{name}Service");
            sb.AppendLine("    {");
            sb.AppendLine("        void Initialize();");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
        }

        private static string GenerateServicePreview(string name, string ns, StradaGeneratorSettings settings)
        {
            var sb = new StringBuilder();

            sb.AppendLine("using Strada.Core.DI.Attributes;");
            sb.AppendLine("using Strada.Core.Communication;");
            sb.AppendLine();
            sb.AppendLine($"namespace {ns}");
            sb.AppendLine("{");

            if (settings.GenerateSummaries)
            {
                sb.AppendLine("    /// <summary>");
                sb.AppendLine($"    /// Service implementation for {name}.");
                sb.AppendLine("    /// </summary>");
            }

            sb.AppendLine($"    public class {name}Service : I{name}Service");
            sb.AppendLine("    {");
            sb.AppendLine("        [Inject] private readonly EventBus _eventBus;");
            sb.AppendLine();
            sb.AppendLine("        public void Initialize()");
            sb.AppendLine("        {");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
        }

        private static string GenerateControllerPreview(string name, string ns, StradaGeneratorSettings settings)
        {
            var sb = new StringBuilder();

            sb.AppendLine("using Strada.Core.DI.Attributes;");
            sb.AppendLine("using Strada.Core.Communication;");
            sb.AppendLine();
            sb.AppendLine($"namespace {ns}");
            sb.AppendLine("{");

            if (settings.GenerateSummaries)
            {
                sb.AppendLine("    /// <summary>");
                sb.AppendLine($"    /// Controller for {name} functionality.");
                sb.AppendLine("    /// </summary>");
            }

            sb.AppendLine($"    public class {name}Controller");
            sb.AppendLine("    {");
            sb.AppendLine($"        [Inject] private readonly I{name}Service _service;");
            sb.AppendLine("        [Inject] private readonly EventBus _eventBus;");
            sb.AppendLine();
            sb.AppendLine("        public void Initialize()");
            sb.AppendLine("        {");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        public void Tick(float deltaTime)");
            sb.AppendLine("        {");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
        }

        private static string GenerateModelPreview(string name, string ns, StradaGeneratorSettings settings)
        {
            var sb = new StringBuilder();

            sb.AppendLine("using Strada.Core.Sync;");
            sb.AppendLine();
            sb.AppendLine($"namespace {ns}");
            sb.AppendLine("{");

            if (settings.GenerateSummaries)
            {
                sb.AppendLine("    /// <summary>");
                sb.AppendLine($"    /// Model for {name} state management.");
                sb.AppendLine("    /// </summary>");
            }

            sb.AppendLine($"    public class {name}Model");
            sb.AppendLine("    {");
            sb.AppendLine($"        public ReactiveProperty<int> Value {{ get; }} = new(0);");
            sb.AppendLine();
            sb.AppendLine("        public void Reset()");
            sb.AppendLine("        {");
            sb.AppendLine("            Value.Value = 0;");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
        }

        private static string GenerateViewPreview(string name, string ns, StradaGeneratorSettings settings)
        {
            var sb = new StringBuilder();

            sb.AppendLine("using UnityEngine;");
            sb.AppendLine("using Strada.Core.Patterns;");
            sb.AppendLine();
            sb.AppendLine($"namespace {ns}");
            sb.AppendLine("{");

            if (settings.GenerateSummaries)
            {
                sb.AppendLine("    /// <summary>");
                sb.AppendLine($"    /// View for {name} visual representation.");
                sb.AppendLine("    /// </summary>");
            }

            sb.AppendLine($"    public class {name}View : View");
            sb.AppendLine("    {");
            sb.AppendLine("        public override void Initialize()");
            sb.AppendLine("        {");
            sb.AppendLine("            base.Initialize();");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        protected override void OnShow()");
            sb.AppendLine("        {");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        protected override void OnHide()");
            sb.AppendLine("        {");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
        }

        private static string GenerateSystemPreview(string name, string ns, StradaGeneratorSettings settings)
        {
            var sb = new StringBuilder();

            sb.AppendLine("using Strada.Core.ECS;");
            sb.AppendLine("using Strada.Core.ECS.Systems;");
            sb.AppendLine();
            sb.AppendLine($"namespace {ns}");
            sb.AppendLine("{");

            if (settings.GenerateSummaries)
            {
                sb.AppendLine("    /// <summary>");
                sb.AppendLine($"    /// ECS System for {name} processing.");
                sb.AppendLine("    /// </summary>");
            }

            sb.AppendLine("    [SystemOrder(0)]");
            sb.AppendLine($"    public class {name}System : SystemBase");
            sb.AppendLine("    {");
            sb.AppendLine("        protected override void OnInitialize()");
            sb.AppendLine("        {");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        protected override void OnUpdate(float deltaTime)");
            sb.AppendLine("        {");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        protected override void OnDispose()");
            sb.AppendLine("        {");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
        }

        private static string GenerateComponentPreview(string name, string ns, StradaGeneratorSettings settings)
        {
            var sb = new StringBuilder();

            sb.AppendLine("using System.Runtime.InteropServices;");
            sb.AppendLine("using Strada.Core.ECS;");
            sb.AppendLine();
            sb.AppendLine($"namespace {ns}");
            sb.AppendLine("{");

            if (settings.GenerateSummaries)
            {
                sb.AppendLine("    /// <summary>");
                sb.AppendLine($"    /// ECS Component for {name} data.");
                sb.AppendLine("    /// </summary>");
            }

            sb.AppendLine("    [StructLayout(LayoutKind.Sequential)]");
            sb.AppendLine($"    public struct {name}Component : IComponent");
            sb.AppendLine("    {");
            sb.AppendLine("        public float Value;");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
        }

        private static string GenerateConfigDataPreview(string name, string ns, StradaGeneratorSettings settings)
        {
            var sb = new StringBuilder();

            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using UnityEngine;");
            sb.AppendLine("using Strada.Core.Data;");
            sb.AppendLine();
            sb.AppendLine($"namespace {ns}");
            sb.AppendLine("{");

            if (settings.GenerateSummaries)
            {
                sb.AppendLine("    /// <summary>");
                sb.AppendLine($"    /// Configuration data for {name}.");
                sb.AppendLine("    /// </summary>");
            }

            sb.AppendLine($"    [CreateAssetMenu(fileName = \"CD_{name}\", menuName = \"{name}/Config/{name}\")]");
            sb.AppendLine($"    public class CD_{name} : ConfigData<{name}Config>");
            sb.AppendLine("    {");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
        }

        private static string GenerateValueObjectPreview(string name, string ns, StradaGeneratorSettings settings)
        {
            var sb = new StringBuilder();

            sb.AppendLine("using System;");
            sb.AppendLine("using UnityEngine;");
            sb.AppendLine();
            sb.AppendLine($"namespace {ns}");
            sb.AppendLine("{");

            if (settings.GenerateSummaries)
            {
                sb.AppendLine("    /// <summary>");
                sb.AppendLine($"    /// Value object for {name} configuration.");
                sb.AppendLine("    /// </summary>");
            }

            sb.AppendLine("    [Serializable]");
            sb.AppendLine($"    public class {name}Config");
            sb.AppendLine("    {");
            sb.AppendLine("        [SerializeField] private string _id;");
            sb.AppendLine("        [SerializeField] private int _value;");
            sb.AppendLine();
            sb.AppendLine("        public string Id => _id;");
            sb.AppendLine("        public int Value => _value;");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
        }

        private static string GenerateEventsPreview(string name, string ns, StradaGeneratorSettings settings)
        {
            var sb = new StringBuilder();

            sb.AppendLine($"namespace {ns}");
            sb.AppendLine("{");

            if (settings.GenerateSummaries)
            {
                sb.AppendLine("    /// <summary>");
                sb.AppendLine($"    /// Event for {name} state changes.");
                sb.AppendLine("    /// </summary>");
            }

            sb.AppendLine($"    public readonly struct {name}ChangedEvent");
            sb.AppendLine("    {");
            sb.AppendLine("        public readonly int Value;");
            sb.AppendLine();
            sb.AppendLine($"        public {name}ChangedEvent(int value)");
            sb.AppendLine("        {");
            sb.AppendLine("            Value = value;");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
        }

        private static string GenerateSignalsPreview(string name, string ns, StradaGeneratorSettings settings)
        {
            var sb = new StringBuilder();

            sb.AppendLine("using Strada.Core.ECS;");
            sb.AppendLine();
            sb.AppendLine($"namespace {ns}");
            sb.AppendLine("{");

            if (settings.GenerateSummaries)
            {
                sb.AppendLine("    /// <summary>");
                sb.AppendLine($"    /// Signal for {name} actions.");
                sb.AppendLine("    /// </summary>");
            }

            sb.AppendLine($"    public struct {name}Signal");
            sb.AppendLine("    {");
            sb.AppendLine("        public Entity Entity;");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
        }
    }
}
