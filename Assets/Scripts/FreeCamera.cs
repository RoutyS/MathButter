using UnityEngine;

public class FreeCamera : MonoBehaviour
{
    public float speed = 5f;
    public float sensitivity = 2f;
    float rotationY = 0f;

    void Update()
    {
        // Mouvement (ZQSD ou WASD)
        float moveX = Input.GetAxis("Horizontal") * speed * Time.deltaTime;
        float moveZ = Input.GetAxis("Vertical") * speed * Time.deltaTime;

        // Monter/Descendre
        float moveY = 0f;
        if (Input.GetKey(KeyCode.E)) moveY += speed * Time.deltaTime;
        if (Input.GetKey(KeyCode.Q)) moveY -= speed * Time.deltaTime;

        transform.Translate(new Vector3(moveX, moveY, moveZ));

        // Rotation souris
        float mouseX = Input.GetAxis("Mouse X") * sensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * sensitivity;

        rotationY -= mouseY;
        rotationY = Mathf.Clamp(rotationY, -90f, 90f);

        transform.localRotation = Quaternion.Euler(rotationY, 0, 0);
        transform.parent.Rotate(Vector3.up * mouseX);
    }
}