using UnityEngine;
using System.Collections;

/// <summary>
/// Wacht tot de speler de VR-headset daadwerkelijk opzet voordat de
/// <see cref="CharacterController"/>-fysica wordt geactiveerd. Voorkomt dat de
/// speler door de vloer valt wanneer de scène start vóór de headset-tracking
/// een geldige hoogte rapporteert.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class VRSpawnStabilizer : MonoBehaviour
{
    [Header("Tracking Reference")]
    [Tooltip("Main Camera onder XR Origin — wordt gebruikt om te detecteren of de headset is opgezet.")]
    public Transform mainCamera;

    private CharacterController characterController;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();

        // Schakel fysica uit op frame 1; pas her-activeren als de headset
        // een realistische hoogte rapporteert.
        characterController.enabled = false;
    }

    private IEnumerator Start()
    {
        if (mainCamera == null)
        {
            Debug.LogError("[VR STABILIZER] Main Camera mist! Koppel deze in de Inspector.");
            yield break;
        }

        // Zolang de headset op het bureau ligt of in slaapstand staat, blijft
        // de lokale Y rond 0. Wachten tot hij echt wordt opgetild.
        while (mainCamera.localPosition.y < 0.5f)
        {
            yield return null;
        }

        // Korte buffer zodat de capsule kan uitrekken naar de spelergrootte
        // voordat de fysica botsingen gaat detecteren.
        yield return new WaitForSeconds(0.25f);

        characterController.enabled = true;
        Debug.Log("[VR STABILIZER] Speler heeft de headset opgezet. Physics veilig geactiveerd.");
    }
}