using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMoveRM : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    Animator animator;
    void Start()
    {
        animator = GetComponent<Animator>();
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    public void PlayerMove(InputAction.CallbackContext callbackContext)
    {
        Vector2 vector2 = callbackContext.ReadValue<Vector2>();
        if (vector2.y > 0f)
        {
            animator.SetBool("向前走", true);
        }
        else
        {
            animator.SetBool("向前走", false);
        }
        if (vector2.y < 0f)
        {
            animator.SetBool("向后走", true);
        }
        else
        {
            animator.SetBool("向后走", false);
        }
    }
}
