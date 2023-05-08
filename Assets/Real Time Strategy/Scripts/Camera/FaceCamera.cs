using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RTS
{
    public class FaceCamera : MonoBehaviour
    {
        private Transform mainCamTransform;

        // Start is called before the first frame update
        void Start()
        {
            mainCamTransform = Camera.main.transform;
            //Debug.Log("Current Pos: " + transform.position);
            //Debug.Log(name + "=> Forward:" + Vector3.forward + ", Up:" + Vector3.up);
            //Debug.Log("Relative LookAt CamPos: " + mainCamTransform.rotation * Vector3.forward);
            //Debug.Log("World up: " + mainCamTransform.rotation * Vector3.up);
        }

        // its just called every frame after update()
        void LateUpdate()
        {
            /*
                the reason for late update is that, we need to update this every frame after the camera momement has been done
                so if the camera movement has been done in update(), then when this LateUpdate() is called on that frame, its called
                only after the updation of camera's transform, this ensures the proper flow of updating happens as per our needs, i.e.
                update camera in update() in any script, which all execute at the same time and once all script's update() per resp frame
                has been called, then LateUpdate() get called here and by then camera updation would have already happened...
                
                rather than putting this updation also in a update() block, might not make much difference to do, but in the actual process
                this update() can only execute after camera's update has been made and thus might execute after one frame only, which can
                be clearly seen in a low end system which run this game, also its not good in practice...
            */

            transform.LookAt(
                transform.position + 
                mainCamTransform.rotation * Vector3.forward, // forward component of the rotation
                mainCamTransform.rotation * Vector3.up // up component of the rotation
                );

            // mirror game example logic // LateUpdate so that all camera updates are finished.
            //transform.forward = Camera.main.transform.forward;
            // need to find how these functionalities work and how they are similar or different...
        }
    }
}