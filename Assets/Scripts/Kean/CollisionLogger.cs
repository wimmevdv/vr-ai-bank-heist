using UnityEngine;

/// <summary>
/// Debug-helper die elke collision en trigger naar de console schrijft. Plaats op
/// een object om botsingen of trigger-flow te volgen tijdens testing.
/// </summary>
public class CollisionLogger : MonoBehaviour
{
    private void OnCollisionEnter(Collision collision)
    {
        Debug.Log($"[Collision] Geraakt: {collision.gameObject.name} | Tag: {collision.gameObject.tag}");
    }

    private void OnTriggerEnter(Collider other)
    {
        Debug.Log($"[Trigger] Geraakt: {other.gameObject.name} | Tag: {other.gameObject.tag}");
    }
}