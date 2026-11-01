using UnityEngine;
namespace PinchOfTheft.Movement
{
    [DefaultExecutionOrder(-900)]
    public sealed class DoorPassageCollisions : MonoBehaviour
    {
        private void Awake()
        {
            int player = LayerMask.NameToLayer("Player");
            int doors = LayerMask.NameToLayer("DoorPassage");
            if (player >= 0 && doors >= 0) Physics.IgnoreLayerCollision(player, doors, true);
        }
    }
}
