using System.Collections;
using System.Collections.Generic;
using System.Linq.Expressions;
using UnityEngine;

public class Movement : MonoBehaviour
{
    public float speed = 100f;
    public Transform circleCenter;
    private float angle;
    public float radius = 1f;
    void Update()
    {
        angle += speed * Time.deltaTime;
        if (angle > 360) angle -= 360;
        float x = Mathf.Cos(angle * Mathf.Deg2Rad) * radius;
        float y = Mathf.Sin(angle * Mathf.Deg2Rad) * radius;
        transform.position = circleCenter.position + new Vector3(x, y, 0);
    }

    public float GetCurrentAngle()
    {
        return angle;
    }

    public void SetRadius(float newRadius)
    {
        radius = Mathf.Max(0, newRadius);
    }
}
