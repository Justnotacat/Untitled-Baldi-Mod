using UnityEngine;
using UnityEngine.UI;

public class ReticleScript : MonoBehaviour
{
    [SerializeField] private GameObject hoverSprite;
    [SerializeField] private float interactDistance = 3f;
    [SerializeField] private LayerMask interactableLayer;

    void Awake()
    {
        hoverSprite.SetActive(false);
    }

    void Update()
    {
        Ray ray = Camera.main.ScreenPointToRay(new Vector3((float)(Screen.width / 2), (float)(Screen.height / 2), 0f));
        RaycastHit hit;

        bool isLooking = Physics.Raycast(ray, out hit, interactDistance, interactableLayer);
        hoverSprite.SetActive(isLooking);
    }
}