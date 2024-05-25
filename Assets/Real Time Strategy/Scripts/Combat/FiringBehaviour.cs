using Mirror;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RTS
{
    [RequireComponent(typeof(TargetBehaviour))]
    public class FiringBehaviour : NetworkBehaviour
    {
        [Header("References Needed")]
        [SerializeField] private TargetBehaviour targetingConfig;
        [SerializeField] private GameObject projectilePrefab;
        [SerializeField] private Transform projectileSpawnPoint;
        //[Tooltip("the force at which the projectiles is fired from the cannon")]
        public float fireRange => targetingConfig.attackRange; // simply extended attribute

        private void Reset()
        {
            targetingConfig = GetComponent<TargetBehaviour>();
        }

        [Header("Firing Properties")]
        [Tooltip("no.of projectiles shot per second")]
        [SerializeField] private float fireRate = 1f;
        [SerializeField] private float rotationSpeed = 90f;
        // once the nav mesh path is clear, when the target is moving the attacking radius of the unit,
        // then the unit doesn't rotate tracking the target's direction, just here we will rotate with a given speed towards facing target

        //[SerializeField,SyncVar] // sync var for bug check
        private bool isInRange = false; 
        public bool inRange { get { return isInRange; } set { isInRange = value; } }
        
        [Tooltip("difference in angles threshold before it can shoot")]
        [SerializeField,Range(1,5)] private float FireAngleRangeThreshold = 2.5f;
        private const float angle_precision = 0.5f;
        private float lastFiredTime;

        private void Start()
        {
            if (targetingConfig == null) targetingConfig = GetComponent<TargetBehaviour>();
        }

        #region Server

        [ServerCallback] void Update()
        {
            if (!isInRange) return;
            
            var targetDir = targetingConfig.GetTargetDirection();
            var turretDir = transform.TransformDirection(Vector3.forward);
            var angleBetTargetAndTurret = Vector3.SignedAngle(turretDir, targetDir, Vector3.up);

            if (Mathf.Abs(angleBetTargetAndTurret) > FireAngleRangeThreshold)
            {
                // rotate facing towards the target
                Quaternion targetRotation = Quaternion.LookRotation(targetDir);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
                // note: unit rotation occurs along the xz plane
            }
            else
            {
                if (Mathf.Abs(angleBetTargetAndTurret) > angle_precision) // trying to be precise
                {
                    //Debug.Log("In rotational range: Precisive rotation made...");
                    transform.Rotate(angleBetTargetAndTurret * Vector3.up);
                }

                // start firing
                if (Time.time > lastFiredTime + (1 / fireRate))  // offset (last-fired-time) + time_period (time taken for 1 proj to be fired)
                {
                    lastFiredTime = Time.time;

                    // we can fire now

                    /* Expirimenting plane based shooting (not working)
                    // diffining 3 points to form a plane
                    Vector3 localPoint1, localPoint2, localPoint3; localPoint1 = projectileSpawnPoint.position;
                    localPoint2 = projectileSpawnPoint.localPosition; localPoint2.z = 0f;
                    localPoint3 = localPoint2; localPoint3.y = 0f;
                    localPoint2 = transform.TransformPoint(localPoint2); localPoint3 = transform.TransformPoint(localPoint3);
                    Debug.Log($"Init Points: {localPoint1} , {localPoint2} , {localPoint3}");

                    // normal of plane formed by three points
                    var planeNormal = Vector3.Cross(localPoint1 - localPoint3, localPoint2 - localPoint3);
                    Debug.Log("Plane Normal: " + planeNormal);
                    var aimProjection = Vector3.ProjectOnPlane(unit.targetingConfig.activeTarget.AimPoint.position, planeNormal);
                    var diff = aimProjection - projectileSpawnPoint.position;
                    */

                    // default shoot at target technique
                    var aimPoints = targetingConfig.activeTarget.AimPoints;
                    Transform targetAim = aimPoints[Random.Range(0, aimPoints.Length)];
                    var diff = targetAim.position - projectileSpawnPoint.position;
                    // note: projectile rotation occurs along the y axis plane corresponding the x,z position of unit in the platform
                    var projectileRotation = Quaternion.LookRotation(diff);

                    GameObject projectileInstance = Instantiate(projectilePrefab, projectileSpawnPoint.position, projectileRotation);
                    // organizing (not working)
                    //projectileInstance.GetComponent<ProjectileMovement>().parent = projectileSpawnPoint;
                    //projectileInstance.transform.parent = projectileSpawnPoint;

                    // spawn through network (then only sync is maintained in all clients)
                    CustomNetworkManager.SpawnOnServer(projectileInstance, connectionToClient, projectilePrefab.name);
                }
            }
        }

        #endregion
    }
}
