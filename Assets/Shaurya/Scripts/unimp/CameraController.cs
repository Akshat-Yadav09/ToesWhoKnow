/*
using UnityEngine;

public class CameraController : MonoBehaviour
{
    [Header("Follow Player")]
    [SerializeField] private Transform player;
    [SerializeField] private float aheadDistance;
    [SerializeField] private float cameraSpeed;
    private float lookAhead;
    
    [Header("Trigger System")]
    private bool inTriggerZone = false;
    private Vector3 triggerTargetPosition;
    private float triggerTargetSize;
    private float transitionSpeed = 2f;
    private float originalCameraSize;

    private void Start()
    {
        originalCameraSize = Camera.main.orthographicSize;
    }

    private void Update()
    {
        if (inTriggerZone)
        {
            // Smoothly move to trigger position
            Vector3 newPos = Vector3.Lerp(
                transform.position, 
                triggerTargetPosition, 
                Time.deltaTime * transitionSpeed
            );
            // Keep Z the same!
            transform.position = new Vector3(newPos.x, newPos.y, transform.position.z);
            
            // Smoothly zoom
            Camera.main.orthographicSize = Mathf.Lerp(
                Camera.main.orthographicSize, 
                triggerTargetSize, 
                Time.deltaTime * transitionSpeed
            );
        }
        else
        {
            // Normal follow - X with lookahead, Y directly follows player
            float newX = player.position.x + lookAhead;
            float newY = player.position.y;
            
            transform.position = new Vector3(newX, newY, transform.position.z);
            lookAhead = Mathf.Lerp(lookAhead, aheadDistance * player.localScale.x, Time.deltaTime * cameraSpeed);
            
            // Return to original zoom
            Camera.main.orthographicSize = Mathf.Lerp(
                Camera.main.orthographicSize, 
                originalCameraSize, 
                Time.deltaTime * transitionSpeed
            );
        }
    }

    public void EnterTriggerZone(Vector3 targetPos, float targetSize, float speed)
    {
        inTriggerZone = true;
        // Use actual world position, not relative!
        triggerTargetPosition = new Vector3(targetPos.x, targetPos.y, transform.position.z);
        triggerTargetSize = targetSize;
        transitionSpeed = speed;
    }

    public void ExitTriggerZone(float speed)
    {
        inTriggerZone = false;
        transitionSpeed = speed;
    }
}
*/
using UnityEngine;

public class CameraController : MonoBehaviour
{
    [Header("Follow Player")]
    [SerializeField] private Transform player;
    [SerializeField] private float aheadDistance;
    [SerializeField] private float yDistance = 0f; // Y offset from player! ⭐
    [SerializeField] private float cameraSpeed;
    private float lookAhead;
    
    [Header("Trigger System")]
    private bool inTriggerZone = false;
    private Vector3 triggerTargetPosition;
    private float triggerTargetSize;
    private float transitionSpeed = 2f;
    private float originalCameraSize;

    private void Start()
    {
        originalCameraSize = Camera.main.orthographicSize;
    }

    private void Update()
    {
        if (inTriggerZone)
        {
            // Smoothly move to trigger position
            Vector3 newPos = Vector3.Lerp(
                transform.position, 
                triggerTargetPosition, 
                Time.deltaTime * transitionSpeed
            );
            // Keep Z the same!
            transform.position = new Vector3(newPos.x, newPos.y, transform.position.z);
            
            // Smoothly zoom
            Camera.main.orthographicSize = Mathf.Lerp(
                Camera.main.orthographicSize, 
                triggerTargetSize, 
                Time.deltaTime * transitionSpeed
            );
        }
        else
        {
            // Normal follow - X with lookahead, Y with offset! ⭐
            float newX = player.position.x + lookAhead;
            float newY = player.position.y + yDistance; // Added Y offset! ⭐
            
            transform.position = new Vector3(newX, newY, transform.position.z);
            lookAhead = Mathf.Lerp(lookAhead, aheadDistance * player.localScale.x, Time.deltaTime * cameraSpeed);
            
            // Return to original zoom
            Camera.main.orthographicSize = Mathf.Lerp(
                Camera.main.orthographicSize, 
                originalCameraSize, 
                Time.deltaTime * transitionSpeed
            );
        }
    }

    public void EnterTriggerZone(Vector3 targetPos, float targetSize, float speed)
    {
        inTriggerZone = true;
        // Use actual world position, not relative!
        triggerTargetPosition = new Vector3(targetPos.x, targetPos.y, transform.position.z);
        triggerTargetSize = targetSize;
        transitionSpeed = speed;
    }

    public void ExitTriggerZone(float speed)
    {
        inTriggerZone = false;
        transitionSpeed = speed;
    }
}