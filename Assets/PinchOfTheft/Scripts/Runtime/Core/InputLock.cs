using System.Collections.Generic;
using UnityEngine;
namespace PinchOfTheft.Core
{
    public sealed class InputLock : MonoBehaviour
    {
        private readonly HashSet<object> owners = new();
        public bool IsLocked => owners.Count > 0;
        public void Acquire(object owner) { if (owner != null) owners.Add(owner); }
        public void Release(object owner) { if (owner != null) owners.Remove(owner); }
        public bool HasOtherOwner(object owner) => owners.Count > (owners.Contains(owner) ? 1 : 0);
    }
}
