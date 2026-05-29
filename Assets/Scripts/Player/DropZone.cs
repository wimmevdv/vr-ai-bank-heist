using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Trigger-zone waar afgeleverde loot in de score telt. Annuleert eerst een
/// actieve XR-grab voordat het object wordt vernietigd, zodat de hand niet
/// een vernietigd item blijft vasthouden.
/// </summary>
[RequireComponent(typeof(Collider))]
public class DropZone : MonoBehaviour
{
    [Header("Audio Feedback")]
    [Tooltip("The AudioSource that plays the cash register sound effect.")]
    [SerializeField] private AudioSource cashRegisterAudio;

    private void OnTriggerEnter(Collider other)
    {
        LootItem loot = other.GetComponent<LootItem>() ?? other.GetComponentInParent<LootItem>();

        if (loot == null) return;

        SecureLoot(loot);
    }


    private void SecureLoot(LootItem loot)
    {
        if (HeistManager.Instance != null)
        {
            HeistManager.Instance.SecureLootItem(loot.monetaryValue);
        }
        else
        {
            Debug.LogWarning("[DROP ZONE] HeistManager instance missing in scene! Value could not be registered.");
        }

        if (cashRegisterAudio != null)
        {
            cashRegisterAudio.Play();
        }

        if (loot.TryGetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>(out var grabInteractable))
        {
            if (grabInteractable.isSelected)
            {
                grabInteractable.interactionManager.CancelInteractableSelection((IXRSelectInteractable)grabInteractable);
            }
        }

        Destroy(loot.gameObject);
    }
}