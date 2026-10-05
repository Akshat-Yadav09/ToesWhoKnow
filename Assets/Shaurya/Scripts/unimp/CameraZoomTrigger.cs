using UnityEngine;

public class CameraZoomTrigger : MonoBehaviour
{
    [Header("Trigger Settings")]
    [SerializeField] private float targetCameraSize = 8f;
    [SerializeField] private Vector3 targetPosition;
    [SerializeField] private float transitionSpeed = 2f;
    
    [Header("Debug")]
    [SerializeField] private bool showGizmos = true;
    
    private CameraController cameraController;
    private BoxCollider2D triggerCollider;
    
    private void Start()
    {
        cameraController = Camera.main.GetComponent<CameraController>();
        triggerCollider = GetComponent<BoxCollider2D>();
        
        if (cameraController == null)
        {
            Debug.LogError("CameraController not found on Main Camera!");
        }
        
        if (triggerCollider == null)
        {
            Debug.LogError("BoxCollider2D not found on " + gameObject.name);
        }
    }
    
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player") && cameraController != null)
        {
            // Use THIS object's world position + targetPosition offset
            Vector3 worldTargetPos = transform.position + targetPosition;
            cameraController.EnterTriggerZone(worldTargetPos, targetCameraSize, transitionSpeed);
            
            Debug.Log("Entered camera trigger - Target: " + worldTargetPos);
        }
    }
    
    private void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.CompareTag("Player") && cameraController != null)
        {
            cameraController.ExitTriggerZone(transitionSpeed);
            Debug.Log("Exited camera trigger");
        }
    }
    
    private void OnDrawGizmos()
    {
        if (!showGizmos) return;
        
        // Draw trigger area
        Gizmos.color = new Color(0, 1, 1, 0.3f);
        if (triggerCollider != null)
        {
            Gizmos.DrawCube(transform.position, triggerCollider.size);
        }
        
        // Draw camera target position
        Vector3 worldTargetPos = transform.position + targetPosition;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(worldTargetPos, 1f);
        
        // Draw camera view at target
        Gizmos.color = Color.red;
        float aspect = Camera.main ? Camera.main.aspect : 1.78f;
        Vector3 size = new Vector3(targetCameraSize * 2 * aspect, targetCameraSize * 2, 0);
        Gizmos.DrawWireCube(worldTargetPos, size);
        
        // Draw line from trigger to target
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(transform.position, worldTargetPos);
    }
}