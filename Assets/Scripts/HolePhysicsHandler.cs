using UnityEngine;
using System.Collections.Generic;

public class HolePhysicsHandler : MonoBehaviour
{
    public Collider floorCollider;
    private List<Collider> ignoredColliders = new List<Collider>();

    private void OnTriggerEnter(Collider other)
    {
        if (floorCollider == null) return;

        // Ignore collision with floor
        Physics.IgnoreCollision(other, floorCollider, true);
        if (!ignoredColliders.Contains(other))
        {
            ignoredColliders.Add(other);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (floorCollider == null) return;

        // Restore collision with floor
        Physics.IgnoreCollision(other, floorCollider, false);
        ignoredColliders.Remove(other);
    }

    private void OnDisable()
    {
        // Resets collisions when disabled or destroyed
        if (floorCollider == null) return;
        foreach (var col in ignoredColliders)
        {
            if (col != null)
                Physics.IgnoreCollision(col, floorCollider, false);
        }
        ignoredColliders.Clear();
    }
}
