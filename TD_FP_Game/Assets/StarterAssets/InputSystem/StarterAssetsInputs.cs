using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace StarterAssets
{
    public class StarterAssetsInputs : MonoBehaviour
    {
       [Header("Character Input Values")]
       public Vector2 move;
       public Vector2 look;
       public bool jump;
       public bool sprint;

       // --- WEAPON INPUTS (Publicly Polled by WeaponController) ---
       [Header("Weapon Input Values")]
       [Tooltip("True when the Fire button is pressed, False when released.")]
       public bool fire; 
       [Tooltip("True when the Aim button is pressed, False when released.")]
       public bool aim; 
       [Tooltip("True when the Reload button is pressed.")]
       public bool reload; 

       [Header("Movement Settings")]
       public bool analogMovement;

       [Header("Mouse Cursor Settings")]
       public bool cursorLocked = true;
       public bool cursorInputForLook = true;

#if ENABLE_INPUT_SYSTEM
       // --- MOVEMENT/LOOK CALLBACKS (Invoked by PlayerInput) ---

       public void OnMove(InputValue value)
       {
          MoveInput(value.Get<Vector2>());
       }

       public void OnLook(InputValue value)
       {
          if(cursorInputForLook)
          {
             LookInput(value.Get<Vector2>());
          }
       }

       public void OnJump(InputValue value)
       {
          JumpInput(value.isPressed);
       }

       public void OnSprint(InputValue value)
       {
          SprintInput(value.isPressed);
       }
       
       // --- WEAPON CALLBACKS (Invoked by PlayerInput) ---

       public void OnFire(InputValue value)
       {
          // We read the state of the button (true for pressed, false for released)
          FireInput(value.isPressed);
       }
       
       public void OnAim(InputValue value)
       {
          AimInput(value.isPressed);
       }

       public void OnReload(InputValue value)
       {
          ReloadInput(value.isPressed);
       }

#endif

       // --- INPUT SETTERS ---

       public void MoveInput(Vector2 newMoveDirection)
       {
          move = newMoveDirection;
       } 

       public void LookInput(Vector2 newLookDirection)
       {
          look = newLookDirection;
       }

       public void JumpInput(bool newJumpState)
       {
          jump = newJumpState;
       }

       public void SprintInput(bool newSprintState)
       {
          sprint = newSprintState;
       }
       
       public void FireInput(bool newFireState)
       {
          fire = newFireState;
       } 

       public void AimInput(bool newAimState)
       {
          aim = newAimState;
       } 

       public void ReloadInput(bool newReloadState)
       {
          reload = newReloadState;
       } 

       
       private void OnApplicationFocus(bool hasFocus)
       {
          SetCursorState(cursorLocked);
       }

       private void SetCursorState(bool newState)
       {
          Cursor.lockState = newState ? CursorLockMode.Locked : CursorLockMode.None;
       }
    }
    
}
