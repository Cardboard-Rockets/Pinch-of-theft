using System.Collections.Generic;
using UnityEngine;
namespace PinchOfTheft.Core
{
    public sealed class CursorController : MonoBehaviour
    {
        private readonly HashSet<object> owners = new();
        private void Start() => Apply();
        public void Unlock(object owner) { if (owner != null) owners.Add(owner); Apply(); }
        public void Lock(object owner) { if (owner != null) owners.Remove(owner); Apply(); }
        private void Apply() { Cursor.lockState = owners.Count == 0 ? CursorLockMode.Locked : CursorLockMode.None; Cursor.visible = owners.Count > 0; }
        private void OnApplicationFocus(bool focus) { if (focus) Apply(); }
    }
}
