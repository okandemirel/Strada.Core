using Strada.Core.Editor.ModuleGenerator.Models;

namespace Strada.Core.Editor.ModuleGenerator.Pipeline
{
    /// <summary>
    /// Interface for generation pipeline steps.
    /// </summary>
    public interface IGenerationStep
    {
        string Name { get; }
        int Order { get; }
        bool CanExecute(GenerationContext context);
        StepResult Execute(GenerationContext context);
        void Rollback(GenerationContext context);
    }

    public class StepResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }

        public static StepResult Ok(string message = null) => new StepResult { Success = true, Message = message };
        public static StepResult Fail(string message) => new StepResult { Success = false, Message = message };
    }
}
