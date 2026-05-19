using UnityEngine;

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