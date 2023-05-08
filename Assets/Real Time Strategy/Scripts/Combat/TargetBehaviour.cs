using Mirror;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RTS
{
    public class TargetBehaviour : NetworkBehaviour
    {
        [Header("Targetable Properties")]
        [Tooltip("IS (or) IS_NOT vurnerable to attacks by other targeters")]
        [SerializeField] private bool targetable = true; public bool isTargetable { get { return targetable; } }
        [SerializeField] private Transform aimPoint = null; public Transform AimPoint { get { return aimPoint; } }


        [Header("Targeter Properties")]
        [Tooltip("HAS (or) NOT_HAS the abilty to shoot projectiles / attack the targetable objects")]
        [SerializeField] private bool targeter = true;
        //[SerializeField,SyncVar]
        private TargetBehaviour targetingObj; public TargetBehaviour activeTarget { get { return targetingObj; } }
        [SerializeField] private float attackRangeRadius = 5f; public float attackRange { get { return attackRangeRadius; } }
        
        private void Start()
        {
            if (aimPoint == null) // adding fail safe for target-aim transform incase if not set
            {
                // create new empty obj and set its parrent under the unit prefab instance
                GameObject AimObj = new("Aim Point");
                AimObj.transform.parent = transform;

                // reset transform
                AimObj.transform.SetPositionAndRotation(transform.position, transform.rotation);

                var colliders = GetComponentsInChildren<Collider>();
                float height = 0;
                foreach (var collider in colliders)
                {
                    height += collider.bounds.size.y;
                }
                height = height / colliders.Length;
                var aim = AimObj.transform.position;
                aim.y += height * 3 / 5; // just above the middle-height of the object
                AimObj.transform.position = aim;

                aimPoint = AimObj.transform;
            }
        }

        #region Server

        [Command]
        public void cmdSetTarget(TargetBehaviour targetComp)
        {
            // if: obj that calls this,
            // (is not a targeter /
            // target-obj passed doesn't have the appropriate script behaviour /
            // target object is not an targetable entity)
            if (!targeter || !targetComp.isTargetable) return;
            this.targetingObj = targetComp;
        }
        [Server] public void ClearTarget() { this.targetingObj = null; }

        #endregion

        public Vector3 GetTargetDirection()
        {
            return activeTarget.transform.position - transform.position;
        }

        public bool CheckInRange() // this condition is need to be checked every frame, thus it require some optimization:
        {
            //if (Vector3.Distance(transform.position, targetingConfig.activeTarget.transform.position) < targetingConfig.activeTarget.rangeRadius) 
            // "Vector3.Distance()" fn or "magnitude" attrib uses sqrt, which are quite slow and not suggestable to calculate every frame (minor optimization):
            //- UnityEngine.Random.Range(agent.stoppingDistance, Mathf.Pow(agent.stoppingDistance, 2)); // chacing range randomize based on agent's stoping distance
            return !(GetTargetDirection().sqrMagnitude > Mathf.Pow(attackRangeRadius, 2));
        }
    }
}
