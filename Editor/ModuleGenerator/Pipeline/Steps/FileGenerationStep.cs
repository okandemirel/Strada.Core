using System;
using System.IO;
using System.Text;
using Strada.Core.Editor.ModuleGenerator.Config;
using Strada.Core.Editor.ModuleGenerator.Models;
using UnityEditor;
using UnityEngine;

namespace Strada.Core.Editor.ModuleGenerator.Pipeline.Steps
{
    /// <summary>
    /// Generates all code files for the module.
    /// </summary>
    public class FileGenerationStep : IGenerationStep
    {
        public string Name => "File Generation";
        public int Order => 30;

        private StradaGeneratorSettings _settings;

        public bool CanExecute(GenerationContext context)
        {
            return true;
        }

        public StepResult Execute(GenerationContext context)
        {
            _settings = StradaGeneratorSettings.GetOrCreateSettings();

            var basePath = context.Definition.FullPath;
            var name = context.Definition.ModuleName;
            var ns = context.Definition.FullNamespace;
            var components = context.Definition.Components;

            int filesCreated = 0;

            if (components.ModuleConfig && context.Definition.ModuleType == ModuleType.Main)
            {
                CreateFile($"{basePath}/Scripts/{name}ModuleConfig.cs", GenerateModuleConfig(name, ns), context);
                filesCreated++;
            }

            if (components.ServiceInterface)
            {
                CreateFile($"{basePath}/Scripts/Interfaces/I{name}Service.cs", GenerateServiceInterface(name, ns), context);
                filesCreated++;
            }

            if (components.Service)
            {
                CreateFile($"{basePath}/Scripts/Services/{name}Service.cs", GenerateService(name, ns, components.ServiceInterface), context);
                filesCreated++;
            }

            if (components.Controller)
            {
                CreateFile($"{basePath}/Scripts/Controllers/{name}Controller.cs", GenerateController(name, ns, components.ServiceInterface), context);
                filesCreated++;
            }

            if (components.Model)
            {
                CreateFile($"{basePath}/Scripts/Models/{name}Model.cs", GenerateModel(name, ns), context);
                filesCreated++;
            }

            if (components.View)
            {
                CreateFile($"{basePath}/Scripts/Views/{name}View.cs", GenerateView(name, ns), context);
                filesCreated++;
            }

            if (components.EcsSystem)
            {
                CreateFile($"{basePath}/Scripts/Systems/{name}System.cs", GenerateSystem(name, ns), context);
                filesCreated++;
            }

            if (components.EcsComponent)
            {
                CreateFile($"{basePath}/Scripts/Components/{name}Component.cs", GenerateComponent(name, ns), context);
                filesCreated++;
            }

            if (components.EntityMediator)
            {
                CreateFile($"{basePath}/Scripts/Views/{name}Mediator.cs", GenerateMediator(name, ns), context);
                filesCreated++;
            }

            if (components.ConfigData)
            {
                CreateFile($"{basePath}/Scripts/Data/UnityObjects/CD_{name}.cs", GenerateConfigData(name, ns), context);
                filesCreated++;
            }

            if (components.ValueObject)
            {
                CreateFile($"{basePath}/Scripts/Data/ValueObjects/{name}Config.cs", GenerateValueObject(name, ns), context);
                filesCreated++;
            }

            if (components.Events)
            {
                CreateFile($"{basePath}/Scripts/Events/{name}Events.cs", GenerateEvents(name, ns), context);
                filesCreated++;
            }

            if (components.Signals)
            {
                CreateFile($"{basePath}/Scripts/Events/{name}Signals.cs", GenerateSignals(name, ns), context);
                filesCreated++;
            }

            if (components.RuntimeTests)
            {
                CreateFile($"{basePath}/Tests/Runtime/{name}Tests.cs", GenerateRuntimeTests(name, ns), context);
                filesCreated++;
            }

            if (components.EditorTests)
            {
                CreateFile($"{basePath}/Tests/Editor/{name}EditorTests.cs", GenerateEditorTests(name, ns), context);
                filesCreated++;
            }

            AssetDatabase.Refresh();

            return StepResult.Ok($"Created {filesCreated} files");
        }

        private void CreateFile(string path, string content, GenerationContext context)
        {
            var fullPath = Path.GetFullPath(path);
            if (!fullPath.StartsWith(Application.dataPath))
                throw new InvalidOperationException($"Path outside project: {fullPath}");

            var dir = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            File.WriteAllText(fullPath, content);
            context.AddCreatedFile(fullPath);
        }

        public void Rollback(GenerationContext context)
        {
            foreach (var file in context.CreatedFiles)
            {
                if (file.EndsWith(".cs") && File.Exists(file))
                {
                    File.Delete(file);
                }
            }

            AssetDatabase.Refresh();
        }

        private string GenerateModuleConfig(string name, string ns)
        {
            var sb = new StringBuilder();
            sb.AppendLine("using UnityEngine;");
            sb.AppendLine("using Strada.Core.DI;");
            sb.AppendLine("using Strada.Core.Modules;");
            sb.AppendLine();
            sb.AppendLine($"namespace {ns}");
            sb.AppendLine("{");
            AddSummary(sb, $"Module configuration for {name}.", 1);
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

        private string GenerateServiceInterface(string name, string ns)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"namespace {ns}");
            sb.AppendLine("{");
            AddSummary(sb, $"Service interface for {name} functionality.", 1);
            sb.AppendLine($"    public interface I{name}Service");
            sb.AppendLine("    {");
            sb.AppendLine("        void Initialize();");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            return sb.ToString();
        }

        private string GenerateService(string name, string ns, bool hasInterface)
        {
            var sb = new StringBuilder();
            sb.AppendLine("using Strada.Core.DI.Attributes;");
            sb.AppendLine("using Strada.Core.Communication;");
            sb.AppendLine();
            sb.AppendLine($"namespace {ns}");
            sb.AppendLine("{");
            AddSummary(sb, $"Service implementation for {name}.", 1);
            var iface = hasInterface ? $" : I{name}Service" : "";
            sb.AppendLine($"    public class {name}Service{iface}");
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

        private string GenerateController(string name, string ns, bool hasServiceInterface)
        {
            var sb = new StringBuilder();
            sb.AppendLine("using Strada.Core.DI.Attributes;");
            sb.AppendLine("using Strada.Core.Communication;");
            sb.AppendLine();
            sb.AppendLine($"namespace {ns}");
            sb.AppendLine("{");
            AddSummary(sb, $"Controller for {name} functionality.", 1);
            sb.AppendLine($"    public class {name}Controller");
            sb.AppendLine("    {");
            if (hasServiceInterface)
            {
                sb.AppendLine($"        [Inject] private readonly I{name}Service _service;");
            }
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

        private string GenerateModel(string name, string ns)
        {
            var sb = new StringBuilder();
            sb.AppendLine("using Strada.Core.Sync;");
            sb.AppendLine();
            sb.AppendLine($"namespace {ns}");
            sb.AppendLine("{");
            AddSummary(sb, $"Model for {name} state management.", 1);
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

        private string GenerateView(string name, string ns)
        {
            var sb = new StringBuilder();
            sb.AppendLine("using UnityEngine;");
            sb.AppendLine("using Strada.Core.Patterns;");
            sb.AppendLine();
            sb.AppendLine($"namespace {ns}");
            sb.AppendLine("{");
            AddSummary(sb, $"View for {name} visual representation.", 1);
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

        private string GenerateSystem(string name, string ns)
        {
            var sb = new StringBuilder();
            sb.AppendLine("using Strada.Core.ECS;");
            sb.AppendLine("using Strada.Core.ECS.Systems;");
            sb.AppendLine();
            sb.AppendLine($"namespace {ns}");
            sb.AppendLine("{");
            AddSummary(sb, $"ECS System for {name} processing.", 1);
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

        private string GenerateComponent(string name, string ns)
        {
            var sb = new StringBuilder();
            sb.AppendLine("using System.Runtime.InteropServices;");
            sb.AppendLine("using Strada.Core.ECS;");
            sb.AppendLine();
            sb.AppendLine($"namespace {ns}");
            sb.AppendLine("{");
            AddSummary(sb, $"ECS Component for {name} data.", 1);
            sb.AppendLine("    [StructLayout(LayoutKind.Sequential)]");
            sb.AppendLine($"    public struct {name}Component : IComponent");
            sb.AppendLine("    {");
            sb.AppendLine("        public float Value;");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            return sb.ToString();
        }

        private string GenerateMediator(string name, string ns)
        {
            var sb = new StringBuilder();
            sb.AppendLine("using Strada.Core.Sync;");
            sb.AppendLine();
            sb.AppendLine($"namespace {ns}");
            sb.AppendLine("{");
            AddSummary(sb, $"Entity mediator for {name} view binding.", 1);
            sb.AppendLine($"    public class {name}Mediator : EntityMediator<{name}View>");
            sb.AppendLine("    {");
            sb.AppendLine("        protected override void OnBind()");
            sb.AppendLine("        {");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        protected override void OnUnbind()");
            sb.AppendLine("        {");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            return sb.ToString();
        }

        private string GenerateConfigData(string name, string ns)
        {
            var sb = new StringBuilder();
            sb.AppendLine("using UnityEngine;");
            sb.AppendLine("using Strada.Core.Data;");
            sb.AppendLine();
            sb.AppendLine($"namespace {ns}");
            sb.AppendLine("{");
            AddSummary(sb, $"Configuration data for {name}.", 1);
            sb.AppendLine($"    [CreateAssetMenu(fileName = \"CD_{name}\", menuName = \"{name}/Config/{name}\")]");
            sb.AppendLine($"    public class CD_{name} : ConfigData<{name}Config>");
            sb.AppendLine("    {");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            return sb.ToString();
        }

        private string GenerateValueObject(string name, string ns)
        {
            var sb = new StringBuilder();
            sb.AppendLine("using System;");
            sb.AppendLine("using UnityEngine;");
            sb.AppendLine();
            sb.AppendLine($"namespace {ns}");
            sb.AppendLine("{");
            AddSummary(sb, $"Value object for {name} configuration.", 1);
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

        private string GenerateEvents(string name, string ns)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"namespace {ns}");
            sb.AppendLine("{");
            AddSummary(sb, $"Events for {name} state changes.", 1);
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

        private string GenerateSignals(string name, string ns)
        {
            var sb = new StringBuilder();
            sb.AppendLine("using Strada.Core.ECS;");
            sb.AppendLine();
            sb.AppendLine($"namespace {ns}");
            sb.AppendLine("{");
            AddSummary(sb, $"Signals for {name} actions.", 1);
            sb.AppendLine($"    public struct {name}Signal");
            sb.AppendLine("    {");
            sb.AppendLine("        public Entity Entity;");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            return sb.ToString();
        }

        private string GenerateRuntimeTests(string name, string ns)
        {
            var sb = new StringBuilder();
            sb.AppendLine("using NUnit.Framework;");
            sb.AppendLine();
            sb.AppendLine($"namespace {ns}.Tests");
            sb.AppendLine("{");
            sb.AppendLine("    [TestFixture]");
            sb.AppendLine($"    public class {name}Tests");
            sb.AppendLine("    {");
            sb.AppendLine("        [SetUp]");
            sb.AppendLine("        public void SetUp()");
            sb.AppendLine("        {");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        [TearDown]");
            sb.AppendLine("        public void TearDown()");
            sb.AppendLine("        {");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        [Test]");
            sb.AppendLine("        public void Example_Test()");
            sb.AppendLine("        {");
            sb.AppendLine("            Assert.Pass();");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            return sb.ToString();
        }

        private string GenerateEditorTests(string name, string ns)
        {
            var sb = new StringBuilder();
            sb.AppendLine("using NUnit.Framework;");
            sb.AppendLine("using UnityEditor;");
            sb.AppendLine();
            sb.AppendLine($"namespace {ns}.Tests");
            sb.AppendLine("{");
            sb.AppendLine("    [TestFixture]");
            sb.AppendLine($"    public class {name}EditorTests");
            sb.AppendLine("    {");
            sb.AppendLine("        [Test]");
            sb.AppendLine("        public void Editor_Test()");
            sb.AppendLine("        {");
            sb.AppendLine("            Assert.Pass();");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            return sb.ToString();
        }

        private void AddSummary(StringBuilder sb, string summary, int indent)
        {
            if (_settings == null || !_settings.GenerateSummaries) return;

            var padding = new string(' ', indent * 4);
            sb.AppendLine($"{padding}/// <summary>");
            sb.AppendLine($"{padding}/// {summary}");
            sb.AppendLine($"{padding}/// </summary>");
        }
    }
}
