using Mirror;
using UnityEngine;

namespace RTS
{
    public class ProjectileBehaviour : NetworkBehaviour
    {
        [Header("References Needed")]
        [SerializeField] private Rigidbody body;

        [Header("Projectile Properties")]
        [SerializeField] private float damageAmount = 10;
        [SerializeField] private float lauchVelocity = 10f;
        [SerializeField] private float destroyAfterSecs = 3f;

        //[SyncVar] public Transform parent;

        public override void OnStartServer()
        {
            //if (transform!=null) transform.SetParent(parent); // organizing (not working)

            if (body == null) { body = GetComponent<Rigidbody>(); }
            if (body != null)
            {
                /* AddForce vs Velocity (in RigidBody):
                    The difference between Rigidbody.AddForce and Rigidbody.velocity is how Unity performs the calculations for applying 
                    the physics.AddForce takes into account the GameObjects mass when simulating the physics of adding the velocity.
                    For example, imagine on a game where shooting objects need the objects to respond to the impact of the 
                    projectiles or explosions. If you add velocity to everything impacted by the projectile or shockwave, 
                    then a pebble will respond the same way a bus will and that would appear strange to the player.
                    Now, if you use AddForce instead, then when the velocity is applied to the pebble it will move much further 
                    than something the size / weight of the bus.
                */
                body.velocity = transform.forward * lauchVelocity;
                //body.AddForce(transform.forward*lauchForce);
            }

            Invoke(nameof(DestroySelf), destroyAfterSecs);
        }
        [Server] private void DestroySelf()
        {
            NetworkServer.Destroy(gameObject);
            // destroy through network server so that the object is destroyed in all the clients not only in server
        }

        [ServerCallback] private void OnTriggerEnter(Collider other)
        {
            if (CustomNetworkManager.TryGetComponentThoroughly<NetworkIdentity>(other.gameObject, out var networkID))
            {
                if (networkID.connectionToClient == connectionToClient) // make sure that collision onto friendly object doesn'nt affect it
                    return;
            }
            if (networkID!=null && networkID.TryGetComponent<Health>(out var health))
                health.DealDamage(damageAmount); // if the obj has health comp, deal damage to it
            
            DestroySelf(); // projectile is destroyed on triggering any surface other than play's own object
        }
    }
}
