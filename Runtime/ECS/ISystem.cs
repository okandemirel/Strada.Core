using System;

namespace Strada.Core.ECS
{
    /// <summary>
    /// Defines the contract for a system that processes entities and their components.
    /// </summary>
    public interface ISystem : IDisposable
    {
        /// <summary>
        /// Called once when the system is first created and added to the executor.
        /// </summary>
        void Initialize();

        /// <summary>
        /// Called every frame.
        /// </summary>
        /// <param name="deltaTime">The time elapsed since the last frame.</param>
        void Update(float deltaTime);
    }
}