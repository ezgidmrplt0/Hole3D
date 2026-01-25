using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class JoystickPlayerExample : MonoBehaviour
{
    public float speed;
    public VariableJoystick variableJoystick;
    public Rigidbody rb;

    public void FixedUpdate()
    {
        Vector3 direction = Vector3.forward * variableJoystick.Vertical + Vector3.right * variableJoystick.Horizontal;
        
        // Speed Skill uygula
        float finalSpeed = speed;
        if (SkillManager.Instance != null && SkillManager.Instance.IsSpeedActive)
        {
            finalSpeed *= SkillManager.Instance.GetSpeedMultiplier();
        }
        
        rb.AddForce(direction * finalSpeed * Time.fixedDeltaTime, ForceMode.VelocityChange);
    }
}