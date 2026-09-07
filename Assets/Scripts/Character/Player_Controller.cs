using UnityEngine;
using UnityEngine.InputSystem;

public class Player_Controller : MonoBehaviour
{

    public Rigidbody rb;

    [SerializeField]
    public Player_Class player_Class;

    public Animator playerAnimation;

    private Vector2 _moveDirection;

    public bool isAttacking = false;

    public GameObject weapon;

    public InputActionReference move;
    public InputActionReference attack;
    void Start()
    {
       rb = GetComponent<Rigidbody>();
       player_Class = GetComponent<Player_Class>();
       playerAnimation = GetComponent<Animator>();

       move.action.Enable();
       attack.action.Enable();
       
    }

    // Update is called once per frame
    void Update()
    {
        _moveDirection = move.action.ReadValue<Vector2>();

        
        rb.linearVelocity = new Vector3(_moveDirection.x * player_Class.speed, rb.linearVelocity.y ,_moveDirection.y * player_Class.speed);
        if (attack.action.IsPressed() && isAttacking == false)
        {
            playerAnimation.SetTrigger("isAttacking");
        }

        

    }
    
}
