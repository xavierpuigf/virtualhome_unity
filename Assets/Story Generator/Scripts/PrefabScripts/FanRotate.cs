using UnityEngine;
using System.Collections;

public class FanRotate : MonoBehaviour
{
    public Vector3 rotationVector;

    Transform fanTransform;

    void Start()
    {
        fanTransform = transform.GetChild(0);
    }

    void FixedUpdate()
    {
        fanTransform.Rotate(rotationVector);
    }
}
