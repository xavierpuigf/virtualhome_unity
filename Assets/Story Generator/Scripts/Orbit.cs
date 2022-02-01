// using System.Collections;
// using System.Collections.Generic;
// using UnityEngine;

// public class Orbit : MonoBehaviour
// {
//     public string time;
//     public float timeSpeed = 1.0f;

//     private float currentTime;
//     private float maxTime = 86400.0f;
    

//     void Start()
//     {
//         time = System.DateTime.Now.ToString("hh:mm:ss");

//         currentTime = 0f;
//         currentTime += System.DateTime.Now.Hour*3600f;
//         currentTime += System.DateTime.Now.Minute*60f;
//         currentTime += System.DateTime.Now.Second;

//         float rot = 360 * (currentTime/maxTime) - 90;
//         transform.rotation = Quaternion.Euler(0,0,rot);
//     }

//     void Update()
//     {
//         float rot = 360 * (1/maxTime) * Time.deltaTime * timeSpeed;
//         transform.Rotate(0,0,rot);
//     }

//     public void SetTime(int hour, int minute, int second)
//     {
//         time = hour + ":" + minute + ":" + second;
//         currentTime = 0f;
//         currentTime += hour*3600f;
//         currentTime += minute*60f;
//         currentTime += second;
//         float rot = 360 * (currentTime/maxTime) - 90;
//         transform.rotation = Quaternion.Euler(0,0,rot);
//     }
// }
