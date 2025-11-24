using UnityEngine;

namespace Strada.Core.ECS.Communication
{
    public static class StradaCommands
    {
        private static StradaCommandBuffer _globalBuffer;

        public static StradaCommandBuffer Global
        {
            get
            {
                if (_globalBuffer == null)
                {
                    _globalBuffer = new StradaCommandBuffer();
                    Debug.LogWarning("Global command buffer was not initialized. Creating default instance.");
                }
                return _globalBuffer;
            }
            set => _globalBuffer = value;
        }

        public static void Send<T>(T command) where T : struct, IStradaCommand
        {
            Global.Send(command);
        }

        public static void SendDelayed<T>(T command, float delaySeconds) where T : struct, IStradaCommand
        {
            Global.SendDelayed(command, delaySeconds);
        }

        public static T[] GetCommands<T>() where T : struct, IStradaCommand
        {
            return Global.GetCommands<T>();
        }

        public static bool HasCommands<T>() where T : struct, IStradaCommand
        {
            return Global.HasCommands<T>();
        }

        public static int GetCommandCount<T>() where T : struct, IStradaCommand
        {
            return Global.GetCommandCount<T>();
        }

        public static void Update(float deltaTime)
        {
            Global.Update(deltaTime);
        }

        public static void Clear()
        {
            Global.Clear();
        }
    }
}
