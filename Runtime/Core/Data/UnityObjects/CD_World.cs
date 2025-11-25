using UnityEngine;

namespace Strada.Core.Core.Data.UnityObjects
{
    /// <summary>
    /// A ScriptableObject that acts as a reference to a WorldDefinition.
    /// This allows different scenes or systems to easily share the same world setup.
    /// </summary>
    [CreateAssetMenu(fileName = "CD_World", menuName = "Strada/Core/World Container")]
    public class CD_World : ScriptableObject
    {
        [field: SerializeField] public WorldDefinition Definition { get; private set; }
    }
}