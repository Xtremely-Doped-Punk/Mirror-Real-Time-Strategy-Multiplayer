using UnityEngine;

namespace RTS
{
    [CreateAssetMenu(menuName = "RTS/CameraControllerConfiguration")]
    public class CameraControllerConfigurationSO : ScriptableObject
    {
        [field: SerializeField] public float MoveSpeed { get; set; } = 20f;
        [field: SerializeField] public Vector2 ScreenBorderThickness { get; set; } = new Vector2(10f, 5f);

        [field: SerializeField, Tooltip("X-Rotation value in degrees, make sure all the spawn points have identity rotation")] 
        public float AngleOfInclination { get; set; } = 45f;

        [field: SerializeField] public int CameraFOV { get; set; } = 40 ;

        [field: SerializeField, Tooltip("max (+/-) given deviation will be applied to FOV while using mouse scroll zoom (out/in) resp")] 
        public int ZoomDeviation { get; set; } = 10 ;
        
        [field: SerializeField] public float WorldYLimit { get; set; } = 10f;
        [field: SerializeField, Tooltip("put min.val in x, max.val in y")] public Vector2 WorldXLimits { get; set; } = new Vector2 (-100f, 100f);
        [field: SerializeField, Tooltip("put min.val in x, max.val in y")] public Vector2 WorldZLimits { get; set; } = new Vector2(-100f, 100f);

        [field: SerializeField, Tooltip("As the camera is tilted at an angle from above, " +
            "small offset required to adjust the view projected by to the point clicked resp")] public float OffsetZ { get; set; } = 10;
    }
}