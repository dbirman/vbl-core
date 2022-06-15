using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class BrainCameraController : MonoBehaviour
{
    [SerializeField] private Camera brainCamera;
    [SerializeField] private GameObject brainCameraRotator;
    [SerializeField] private GameObject brain;

    private Vector3 initialCameraRotatorPosition;

    public float minFoV = 15.0f;
    public float maxFoV = 90.0f;
    public float fovDelta = 15.0f;
    public float orthoDelta = 5.0f;
    public float moveSpeed = 50.0f;
    public float rotSpeed = 1000.0f;
    public float minXRotation = -90;
    public float maxXRotation = 90;
    public float minZRotation = -90;
    public float maxZRotation = 90;

    private bool mouseDownOverBrain;
    private int mouseButtonDown;
    private bool brainTransformChanged;
    private float lastLeftClick;
    private float lastRightClick;

    private float totalYaw;
    private float totalPitch;
    private float totalSpin;

    private bool blockBrainControl;

    // auto-rotation
    private bool autoRotate;
    private float autoRotateSpeed = 10.0f;

    public static float doubleClickTime = 0.2f;
    // Targeting
    private Vector3 cameraTarget;

    private void Awake()
    {
        // Artifically limit the framerate
#if !UNITY_WEBGL
        Application.targetFrameRate = 144;
#endif

        cameraTarget = brain.transform.position;
        initialCameraRotatorPosition = brainCameraRotator.transform.position;
        autoRotate = false;
    }
    // Start is called before the first frame update
    void Start()
    {
        lastLeftClick = Time.realtimeSinceStartup;
        lastRightClick = Time.realtimeSinceStartup;
    }

    // Update is called once per frame
    void Update()
    {
        // Check the scroll wheel and deal with the field of view
        float fov = brainCamera.orthographic ? brainCamera.orthographicSize : brainCamera.fieldOfView;

        float scroll = -Input.GetAxis("Mouse ScrollWheel");
        fov += (brainCamera.orthographic ? orthoDelta : fovDelta) * scroll;
        fov = Mathf.Clamp(fov, minFoV, maxFoV);

        if (brainCamera.orthographic)
            brainCamera.orthographicSize = fov;
        else
            brainCamera.fieldOfView = fov;

        // Now check if the mouse wheel is being held down
        if (Input.GetMouseButton(1) && !blockBrainControl && !EventSystem.current.IsPointerOverGameObject())
        {
            mouseDownOverBrain = true;
            mouseButtonDown = 1;
        }

        // Now deal with dragging
        if (Input.GetMouseButtonDown(0) && !blockBrainControl && !EventSystem.current.IsPointerOverGameObject())
        {
            //BrainCameraDetectTargets();
            mouseDownOverBrain = true;
            mouseButtonDown = 0;
            autoRotate = false;
        }

        if (autoRotate)
        {
            totalSpin += autoRotateSpeed * Time.deltaTime;
            ApplyBrainCameraRotatorRotation();
        }
        else
            BrainCameraControl_noTarget();
    }

    public void SetControlBlock(bool state)
    {
        blockBrainControl = state;
    }


    void BrainCameraControl_noTarget()
    {
        if (Input.GetMouseButtonUp(0))
            SetControlBlock(false);

        if (mouseDownOverBrain)
        {
            // Deal with releasing the mouse (anywhere)
            if (mouseButtonDown == 0 && Input.GetMouseButtonUp(0))
            {
                lastLeftClick = Time.realtimeSinceStartup;
                ClearMouseDown(); return;
            }
            if (mouseButtonDown == 1 && Input.GetMouseButtonUp(1))
            {
                if (!brainTransformChanged)
                {
                    // Check for double click
                    if ((Time.realtimeSinceStartup - lastRightClick) < doubleClickTime)
                    {
                        // Reset the brainCamera transform position
                        brainCamera.transform.localPosition = Vector3.zero;
                    }
                }

                lastRightClick = Time.realtimeSinceStartup;
                ClearMouseDown(); return;
            }

            if (mouseButtonDown == 1)
            {
                // While right-click is held down 
                float xMove = -Input.GetAxis("Mouse X") * moveSpeed * Time.deltaTime;
                float yMove = -Input.GetAxis("Mouse Y") * moveSpeed * Time.deltaTime;

                if (xMove != 0 || yMove != 0)
                {
                    brainTransformChanged = true;
                    brainCamera.transform.Translate(xMove, yMove, 0, Space.Self);
                }
            }

            // If the mouse is down, even if we are far way now we should drag the brain
            if (mouseButtonDown == 0)
            {
                float xRot = -Input.GetAxis("Mouse X") * rotSpeed * Time.deltaTime;
                float yRot = Input.GetAxis("Mouse Y") * rotSpeed * Time.deltaTime;

                if (xRot != 0 || yRot != 0)
                {
                    brainTransformChanged = true;

                    // Pitch Locally, Yaw Globally. See: https://gamedev.stackexchange.com/questions/136174/im-rotating-an-object-on-two-axes-so-why-does-it-keep-twisting-around-the-thir

                    // if shift is down, we can apply spin instead of yaw
                    if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                    {
                        totalSpin = Mathf.Clamp(totalSpin + xRot, minXRotation, maxXRotation);
                    }
                    else
                    {
                        totalYaw = Mathf.Clamp(totalYaw + yRot, minXRotation, maxXRotation);
                        totalPitch = Mathf.Clamp(totalPitch + xRot, minZRotation, maxZRotation);
                    }
                    ApplyBrainCameraRotatorRotation();
                }
            }
        }
    }

    void ApplyBrainCameraRotatorRotation()
    {
        Quaternion curRotation = Quaternion.Euler(totalYaw, totalSpin, totalPitch);
        // Move the camera back to zero, perform rotation, then offset back
        brainCameraRotator.transform.position = initialCameraRotatorPosition;
        brainCameraRotator.transform.LookAt(cameraTarget, Vector3.back);
        brainCameraRotator.transform.position = curRotation * (brainCameraRotator.transform.position - cameraTarget) + cameraTarget;
        brainCameraRotator.transform.rotation = curRotation * brainCameraRotator.transform.rotation;
    }

    void ClearMouseDown()
    {
        mouseDownOverBrain = false;
        //brainCameraClickthroughTarget = null;
        brainTransformChanged = false;
    }

    public Vector2 GetPitchYaw()
    {
        return new Vector2(totalPitch, totalYaw);
    }

    public void SetBrainAxisAngles(Vector2 pitchYaw)
    {
        totalPitch = pitchYaw.x;
        totalYaw = pitchYaw.y;
        totalSpin = 0f;
        ApplyBrainCameraRotatorRotation();
    }
    public void SetBrainAxisAngles(Vector3 pitchYawSpin)
    {
        totalPitch = pitchYawSpin.x;
        totalYaw = pitchYawSpin.y;
        totalSpin = pitchYawSpin.z;
        ApplyBrainCameraRotatorRotation();
    }

    public void SetYaw(float newYaw)
    {
        totalYaw = newYaw;
    }

    public void SetPitch(float newPitch)
    {
        totalPitch = newPitch;
    }

    public void SetSpin(float newSpin)
    {
        totalSpin = newSpin;
    }

    public void SetCameraTarget(Vector3 newTarget)
    {
        Debug.Log("Setting camera target to: " + newTarget);

        // Reset any panning 
        brainCamera.transform.localPosition = Vector3.zero;

        cameraTarget = new Vector3(5.7f - newTarget.z, 4f - newTarget.y, -6.6f + newTarget.x);

        ApplyBrainCameraRotatorRotation();
    }

    public void SetCameraContinuousRotation(bool state)
    {
        autoRotate = state;
    }

    public void SetCameraRotationSpeed(float speed)
    {
        autoRotateSpeed = speed;
    }

    public void SetCamera(Camera newCamera)
    {
        brainCamera = newCamera;
        ApplyBrainCameraRotatorRotation();
    }

    public void SetCameraBackgroundToggle(bool white)
    {
        if (white)
            SetCameraBackgroundColor(Color.white);
        else
            SetCameraBackgroundColor(Color.black);
    }

    public void SetCameraBackgroundColor(Color newColor)
    {
        brainCamera.backgroundColor = newColor;
    }
}
