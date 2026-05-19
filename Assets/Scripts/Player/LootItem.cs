using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;


[RequireComponent(typeof(XRGrabInteractable))]
[RequireComponent(typeof(Renderer))]
public class LootItem : MonoBehaviour
{
    [Header("Loot Data")]
    [Tooltip("The display name of the item (e.g., 'Gold Bar', 'Diamond').")]
    public string itemName = "Valuable Item";

    [Tooltip("The monetary value added to the score upon extraction.")]
    public int monetaryValue = 1000;

    [Header("UX Feedback")]
    [Tooltip("The emissive material applied when the player hovers over this item.")]
    [SerializeField] private Material highlightMaterial;

    private Material originalMaterial;
    private Renderer itemRenderer;
    private XRGrabInteractable grabInteractable;

    private void Awake()
    {
        itemRenderer = GetComponent<Renderer>();
        grabInteractable = GetComponent<XRGrabInteractable>();

        originalMaterial = itemRenderer.material;
    }

    private void OnEnable()
    {
        grabInteractable.firstHoverEntered.AddListener(OnHoverEnter);
        grabInteractable.lastHoverExited.AddListener(OnHoverExit);
    }

    private void OnDisable()
    {
        grabInteractable.firstHoverEntered.RemoveListener(OnHoverEnter);
        grabInteractable.lastHoverExited.RemoveListener(OnHoverExit);
    }


    private void OnHoverEnter(HoverEnterEventArgs args)
    {
        if (highlightMaterial != null)
        {
            itemRenderer.material = highlightMaterial;
        }
    }

    private void OnHoverExit(HoverExitEventArgs args)
    {
        itemRenderer.material = originalMaterial;
    }
}