using UnityEngine;

/// <summary>
/// Monitors physical tracking bounds to verify if the player's capsule 
/// is currently stationed within the designated safe extraction area.
/// </summary>
[RequireComponent(typeof(Collider))]
public class SafeZone : MonoBehaviour
{
    /// <summary>
    /// Returns true if the player is physically standing inside this trigger zone.
    /// </summary>
    public bool IsPlayerInZone { get; private set; }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            IsPlayerInZone = true;
            Debug.Log("[SAFE ZONE] Player has entered the extraction area. Ready for button input.");
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            IsPlayerInZone = false;
            Debug.Log("[SAFE ZONE] Player left the extraction area. Extraction disabled.");
        }
    }
}