using UnityEngine;

public class FreeCamera : MonoBehaviour
{
    public float speed = 5f;
    public float fastSpeed = 15f;
    public float sensitivity = 2f;
    public Transform cameraTransform; // assigner Main Camera ici

    private float rotationX = 0f;
    private float rotationY = 0f;

    void Update()
    {
        float currentSpeed = Input.GetKey(KeyCode.LeftShift) ? fastSpeed : speed;

        // === Rotation avec clic droit ===
        if (Input.GetMouseButton(1)) // clic droit maintenu
        {
            float mouseX = Input.GetAxis("Mouse X") * sensitivity;
            float mouseY = Input.GetAxis("Mouse Y") * sensitivity;

            rotationX += mouseX;
            rotationY -= mouseY;
            rotationY = Mathf.Clamp(rotationY, -90f, 90f);

            cameraTransform.localRotation = Quaternion.Euler(rotationY, rotationX, 0f);
        }


        // === Zoom avec clic molette ===
        if (Input.GetMouseButton(2)) // clic molette
        {
            float scrollMove = Input.GetAxis("Mouse Y") * currentSpeed * Time.deltaTime;
            transform.position += transform.forward * scrollMove;
        }

        // === Déplacement clavier (ZQSD + flèches + E/Q) ===
        float arrowX = ArrowAxisHorizontal();
        float arrowZ = ArrowAxisVertical();

        float moveX = (Input.GetAxis("Horizontal") + arrowX);
        float moveZ = (Input.GetAxis("Vertical") + arrowZ);
        float moveY = 0f;
        if (Input.GetKey(KeyCode.E)) moveY += 1f;
        if (Input.GetKey(KeyCode.Q)) moveY -= 1f;

        Vector3 moveDir = (transform.right * moveX + transform.forward * moveZ).normalized;
        Vector3 move = moveDir * currentSpeed * Time.deltaTime + Vector3.up * moveY * currentSpeed * Time.deltaTime;
        transform.position += move;

        if (arrowX != 0 || arrowZ != 0)
            Debug.Log($"\u2192 Flèches détectées : X={arrowX}, Z={arrowZ}");
    }

    float ArrowAxisHorizontal()
    {
        float val = 0f;
        if (Input.GetKey(KeyCode.LeftArrow)) val -= 1f;
        if (Input.GetKey(KeyCode.RightArrow)) val += 1f;
        return val;
    }

    float ArrowAxisVertical()
    {
        float val = 0f;
        if (Input.GetKey(KeyCode.DownArrow)) val -= 1f;
        if (Input.GetKey(KeyCode.UpArrow)) val += 1f;
        return val;
    }
}
