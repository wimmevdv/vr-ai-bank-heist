using UnityEngine;

/// <summary>
/// Trigger-zone die bijhoudt of de speler binnen de extractie-cirkel staat.
/// <see cref="ExtractionButton"/> leest deze vlag bij elke druk om te valideren.
/// </summary>
[RequireComponent(typeof(Collider))]
public class SafeZone : MonoBehaviour
{
    /// <summary>True als de speler op dit moment in de zone staat.</summary>
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