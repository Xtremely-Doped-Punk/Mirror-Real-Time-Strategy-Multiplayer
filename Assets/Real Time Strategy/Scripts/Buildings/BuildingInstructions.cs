using Mirror;
using System;
using UnityEditor;
using UnityEngine;

namespace RTS  // C# allows public inheritance only. C++ allowed all three kinds. (thus using all private objects as "protected")
{
    public abstract class Buyable : NetworkBehaviour
    {
        [Header("UI References Needed")]
        [SerializeField] protected Sprite icon = null; public Sprite Icon => icon;

        [Header("3D Model References Needed")]
        [SerializeField] protected GameObject preview = null; public GameObject Preview => preview;
        // requires prefab's instance that can be used in building placer

        [Header("Buyable Properties")]
        [SerializeField] protected int id = -1; public int ID => id;
        [SerializeField] protected int price = 100; public int Price => price;
    }

    [RequireComponent(typeof(Health))]
    public abstract class Building : Buyable // this building class should not be used for base, as it is differently done
    {
        public const float BUILDING_GAP = .125f;
        private void Awake()
        {
            if (this.GetType() == typeof(Building))
                Destroy(this.gameObject);
        }

        [Header("References Needed")]
        [SerializeField] protected Health healthConfig = null; public Health HealthConfig => healthConfig;

        protected RTSPlayer player;

        private void Reset()
        {
            healthConfig = GetComponent<Health>();
        }

        // to keep track of each player's units spawned by them resp (kept track in RTSPlayer.cs)
        public static event Action<Building> onBuildingSpawned;
        public static event Action<Building> onBuildingDespawned;

        /*void Start()
        {
            if (healthConfig == null) healthConfig = GetComponent<Health>(); // returns null only, see notes for why?
        }*/

        #region Server
        public override void OnStartServer()
        {
            onBuildingSpawned?.Invoke(this);

            player = connectionToClient.identity.GetComponent<RTSPlayer>();
        }
        public override void OnStopServer()
        {
            onBuildingDespawned?.Invoke(this);
        }
        #endregion

        #region Client
        public override void OnStartAuthority()
        {
            if (!NetworkServer.active)  //i.e. isClientOnly refer RTSPlayer for why?
                onBuildingSpawned?.Invoke(this);
        }
        public override void OnStopClient()
        {
            if (isClientOnly && isOwned)
                onBuildingDespawned?.Invoke(this);
        }
        #endregion
    }

    public class BuildingInstructions : MonoBehaviour
    {
        public readonly string Title = "Instructions for SetingUp Building Prefab";

        public readonly string[] Instructions = {
            "\"Building\" class is simply an abstract class, this class " +
                "has been made into class just to illustrate the main prefab of a building.",

            "A proper building gameobject must be of derived class instance whose base " +
                "class is \"Building\" class and implements other resp functionalities.",

            "Remove this component in an actual implementation of a building prefab and...",

            "Add their resp derived class script as component. " +
                "For example: remove this \"Building (Script)\" comp and add \"UnitSpawner (Script)\" comp..." ,

            "Then add the 3D model of building as a child to this gameobject in this prefab.",

            "Finally dont forget to attach the references needed for the newly added script" +
                " and other scripts wherever needed in the prefab."
        };

        public readonly string Error = "Only Derived-class object of buildings in prefab variants should be used in game.\n" +
            "(this script-component will destroy itself on awake, " +
            "to simply avoid overhead just dont add this component an game entity)";

        private void Awake()
        {
            Destroy(this);
        }
    }

#if UNITY_EDITOR
    [CustomEditor(typeof(BuildingInstructions))]
    public class BuildingEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            Init();
            var bi = (BuildingInstructions)target;

            GUILayout.Space(10);
            GUILayout.Label(bi.Title, TitleStyle);
            GUILayout.Space(10);

            for (int i = 0; i < bi.Instructions.Length; i++)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label((i + 1).ToString() + ".", PointStyle);
                GUILayout.Box(bi.Instructions[i], BodyStyle);
                GUILayout.EndHorizontal();
                //GUILayout.Space(5);
            }
            EditorGUILayout.HelpBox(bi.Error, MessageType.Warning);
        }


        bool initialized;

        [SerializeField] GUIStyle TitleStyle;
        [SerializeField] GUIStyle BodyStyle;
        [SerializeField] GUIStyle PointStyle;

        void Init()
        {
            if (initialized)
                return;

            BodyStyle = new GUIStyle(EditorStyles.label);
            BodyStyle.wordWrap = true;
            BodyStyle.fontSize = 12;
            //BodyStyle.margin = GUI.skin.box.margin;

            PointStyle = new GUIStyle(BodyStyle);
            PointStyle.stretchWidth = false;

            TitleStyle = new GUIStyle(BodyStyle);
            TitleStyle.fontSize = 16;
            TitleStyle.alignment = TextAnchor.MiddleCenter;
            TitleStyle.fontStyle = FontStyle.Bold;

            initialized = true;
        }
    }
#endif
}
