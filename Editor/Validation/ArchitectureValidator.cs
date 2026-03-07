using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Strada.Core.Editor.Validation
{
    /// <summary>
    /// Main architecture validator that manages and executes validation rules.
    /// </summary>
    public class ArchitectureValidator : IArchitectureValidator
    {
        private readonly List<IArchitectureRule> _rules = new List<IArchitectureRule>();
        private static ArchitectureValidator _instance;

        /// <summary>
        /// Gets the singleton instance of the architecture validator.
        /// </summary>
        public static ArchitectureValidator Instance => _instance ??= new ArchitectureValidator();

        public IReadOnlyList<IArchitectureRule> Rules => _rules;

        public ArchitectureValidator()
        {
            RegisterDefaultRules();
        }

        /// <summary>
        /// Registers the default set of Strada architecture rules.
        /// </summary>
        private void RegisterDefaultRules()
        {
            RegisterRule(new ComponentTypeValidator());
            RegisterRule(new ControllerEcsAccessRule());
            RegisterRule(new ViewBusinessLogicRule());
            RegisterRule(new ServiceUnityDependencyRule());
        }

        public void RegisterRule(IArchitectureRule rule)
        {
            if (rule == null)
                throw new ArgumentNullException(nameof(rule));

            if (_rules.Any(r => r.RuleId == rule.RuleId))
                return;

            _rules.Add(rule);
        }

        public bool UnregisterRule(string ruleId)
        {
            return _rules.RemoveAll(r => r.RuleId == ruleId) > 0;
        }

        public IEnumerable<ValidationIssue> ValidateType(Type type)
        {
            if (type == null)
                yield break;

            foreach (var rule in _rules)
            {
                if (!rule.AppliesTo(type))
                    continue;

                foreach (var issue in rule.Validate(type))
                {
                    yield return issue.WithType(type);
                }
            }
        }

        public IEnumerable<ValidationIssue> ValidateAssembly(Assembly assembly)
        {
            if (assembly == null)
                yield break;

            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                types = ex.Types.Where(t => t != null).ToArray();
            }

            foreach (var type in types)
            {
                foreach (var issue in ValidateType(type))
                {
                    yield return issue;
                }
            }
        }

        /// <summary>
        /// Validates all Strada-related types in the current domain.
        /// </summary>
        public IEnumerable<ValidationIssue> ValidateAll()
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !a.IsDynamic && 
                           (a.FullName.Contains("Strada") || 
                            a.FullName.Contains("Assembly-CSharp")));

            foreach (var assembly in assemblies)
            {
                foreach (var issue in ValidateAssembly(assembly))
                {
                    yield return issue;
                }
            }
        }

        /// <summary>
        /// Clears all registered rules.
        /// </summary>
        public void ClearRules()
        {
            _rules.Clear();
        }

        /// <summary>
        /// Resets to default rules.
        /// </summary>
        public void ResetToDefaults()
        {
            ClearRules();
            RegisterDefaultRules();
        }
    }
}
