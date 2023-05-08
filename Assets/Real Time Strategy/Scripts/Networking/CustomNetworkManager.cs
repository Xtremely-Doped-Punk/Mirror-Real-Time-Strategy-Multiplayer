using System.Collections.Generic;
using UnityEngine;
using Mirror;
using UnityEngine.SceneManagement;
using UnityEditor;
using System.Linq;
using System;
# if UNITY_EDITOR
using UnityEditorInternal;
#endif
namespace RTS
{
    public class CustomNetworkManager : NetworkManager
    {
        [Header("Server Spawnable Objects")] // we are not using list here, as do not change in run time, rather array is prefered
        [SerializeField] private PlayerBase basePrefab = null;
        [SerializeField] private Building[] buildingPrefabs = new Building[0];
        [SerializeField] private UnitBehaviour[] unitPrefabs = new UnitBehaviour[0];
        [SerializeField] private ProjectileBehaviour[] projectilePrefabs = new ProjectileBehaviour[0];
        [SerializeField] private GameSession GameSessionPrefab = null;

        //private Dictionary<NetworkConnection, GameObject> Organizer; // if in a editor, re-organize the game-objects of all player grouped
        /* Mirror does not support using Networked scripts on child objects:
            Note that Unet doesn't actually support GameObjects with a NetworkIdentity component as children of another object. 
            Objects with a NetworkIdentity component are supposed to be at the root of the scene, or you will get unexpected behavior.
            thus, dont try to instanciate a networked gameobject with parrent transform attribute set...
            Thus we are trying to create another GameObject without NetworkIdentity component, as parent for all the parrent belonging entities.
         */
        public override void Awake()
        {
            base.Awake();

            /* <bug-fix> => error caused by below line: NetworkServer.Spawn(Instance_Created_newly)
                prob: "Registered Spawnable Prefabs" field doesn't update in different network managers of their resp builds.
                Generally, each of the build has its own network manager, even through only of it acts a server.
                Even if Multiple NetworkManagers detected in the scene. Only one NetworkManager can exist at a time. 
                The duplicate NetworkManager will not be used. Also note that these network manager in their resp build have their own resp
                "Registered Spawnable Prefabs" field, which should contain all the spawnable units regardless of its mechanics so that it could
                be spawned through "NetworkServer.Spawn", that takes gives the spawned unit authority the client who has spawned it resp below.
                if the spawned unit not present in that "Registered Spawnable Prefabs" (field) ==> spawnPrefabs (variable name), 
                the updates are only seen in the server and it throws error at client side. Thus make sure you register your prefabs on 
                both client-"NetworkManager" and both server-"NetworkManager".
             */

            // </bug-fix> done as a easy setup through unity inspector (editor scripting)
        }

        public void SetupRegisteredSpawnablePrefabs()
        {
            spawnPrefabs.Clear();
            buyablesIDMap.Clear();

            if (basePrefab != null)
            {
                spawnPrefabs.Add(basePrefab.gameObject);
                AddBuyable(basePrefab);
            }
            else Debug.LogWarning("Base Prefab is not assigned!");

            if (GameSessionPrefab != null) spawnPrefabs.Add(GameSessionPrefab.gameObject);
            else Debug.LogWarning("Game Session Prefab is not assigned!");

            if (buildingPrefabs.Length > 0)
            {
                spawnPrefabs.AddRange(buildingPrefabs.Select(x => x.gameObject));
                foreach (Building building in buildingPrefabs)
                {
                    AddBuyable(building);
                }
                BuyablesMapKeys = buyablesIDMap.Keys.ToArray();
            }
            else Debug.LogWarning("List of Building Prefabs is empty!");

            if (unitPrefabs.Length > 0) spawnPrefabs.AddRange(unitPrefabs.Select(x => x.gameObject));
            else Debug.LogWarning("List of Unit Prefabs is empty!");

            if (projectilePrefabs.Length > 0) spawnPrefabs.AddRange(projectilePrefabs.Select(x => x.gameObject));
            else Debug.LogWarning("List of Projectile Prefabs is empty!");
        }

        // initially had a private var and public get => property, but it seems it doesnt work this '=>'
        // only works on auto properties set onto declaration object (rather than defining get {return private_obj} -> not working)
        //[field: SerializeField] // field keyword only works on auto-properties made public
        //public Dictionary<int, Buyable> BuyablesIDMap { get; private set; } = new Dictionary<int, Buyable>();

        [SerializeField, HideInInspector]
        private SerializedDictionary<int, Buyable> buyablesIDMap = new(); 
        // note, if the object is not serialized, then its saved values / references are lost as they are saved onto the game object
        
        [SerializeField, HideInInspector, Obsolete("made just for display purposes, dont use this reference else where")] 
        internal int[] BuyablesMapKeys = new int[0]; 
        public IReadOnlyDictionary<int, Buyable> BuyablesIDMap => buyablesIDMap;

        private void AddBuyable(Buyable attrib)
        {
            try
            {
                if (attrib.ID < 0)
                    throw new ArgumentNullException("Buyable ID not set! (cannot be negative)");
                
                buyablesIDMap.Add(attrib.ID, attrib);
            }
            catch (ArgumentNullException msg)
            {
                // key is null or set to negative by default
                Debug.LogWarning(msg.Message);
            }
            catch (ArgumentException)
            {
                // key already exists
                Debug.LogWarning(attrib.name + " and " + buyablesIDMap[attrib.ID].name + " have same buyable ID:"+ attrib.ID);
            }
        }

        public override void OnStartHost() // called after OnServerStart only but its safe
        {
            // fail safe for game session
            if (GameSession.Instance == null)
                OnServerSceneChanged(SceneManager.GetActiveScene().name);
        }

        public override void OnStartClient() // client end refresh UI
        {
            if (UnitSelectionHandler.Instance != null) UnitSelectionHandler.Instance.enabled = true;
        }

        public override void OnServerAddPlayer(NetworkConnectionToClient conn)
        {
            base.OnServerAddPlayer(conn);

            GameObject initialBaseInstance = Instantiate(basePrefab.gameObject, 
                conn.identity.transform.position, conn.identity.transform.rotation);

            SpawnOnServer(initialBaseInstance, conn, basePrefab.name);
            // Error: Failed to spawn server object, did you forget to add it to the NetworkManager? (fixed explained above)
        }

        // note that game session must be spawned before player connect to game, thats y we are spawning in as soon it is changed to playable game scene
        // note: OnServerChangeScene() get called before the next scene gets loaded whereas OnServerSceneChanged() get called after next scene is loaded
        public override void OnServerSceneChanged(string sceneName) 
        {
            // note this newSceneName string is "path/newSceneName" (path where the scene exists), thus we wont be using
            // if the current scene is Map scene, then we need to spawn in game session

            //if (SceneManager.GetActiveScene().name.Contains("Map"))
            if(sceneName.Contains("Map")) // just avoiding using sceneManager as i m using contains() of substring functionallity
            {
                GameSession sessionInstance = Instantiate(GameSessionPrefab);
                SpawnOnServer(sessionInstance.gameObject);
            }
        }
        
        /*
        private void AddPlayerEntityGroup(NetworkConnection conn)
        {
            var PlayerEntities = new GameObject("Player[" + conn.connectionId + "]-Entites");
            Organizer.Add(conn, PlayerEntities);
            transform.parent = PlayerEntities.transform; // on player
        }
        */

        public static bool TryGetComponentThoroughly<T>(GameObject obj, out T component)
        {
            if (obj.TryGetComponent<T>(out component))
            {
                return true;
            }
            else
            {
                // check component in its gameObject as well as in parents
                component = obj.GetComponentInParent<T>();
                if (component == null) return false;
                else return true;
            }
        }

        [Server] public static void SpawnOnServer(GameObject prefabInstance, NetworkConnection conn = null, string name = null)
        {
            if (name == null) name = prefabInstance.name;
            int id = (conn != null) ? conn.connectionId : -1; 
            prefabInstance.name = name + $" [ID={id}]"; // if id =-1, mean server only accessable (not belonging to any client)
            // this function just to add name to be specifc on server end for debugging purposes
            NetworkServer.Spawn(prefabInstance, conn);
        }
    }

#if UNITY_EDITOR
    [CustomEditor(typeof(CustomNetworkManager))]
    [CanEditMultipleObjects]
    public class CustomNetworkManagerEditor : NetworkManagerEditor
    {
        bool ShowRegisteredPrefabs = false;
        bool ShowBuyables = false;
        ReorderableList buyablesList;
        CustomNetworkManager TargetClassRef;
        SerializedProperty MapKeyProperty;

        //[SerializeField] List<Buyable> BuyablesMapValues = new();

        private void CustomInit()
        {
            if (buyablesList != null) return;

            TargetClassRef = target as CustomNetworkManager;
            //var dicprop = serializedObject.FindProperty("buyablesIDMap");
            MapKeyProperty = serializedObject.FindProperty("BuyablesMapKeys");

            buyablesList = new ReorderableList(serializedObject, MapKeyProperty, true, true, false, false)
            {
                drawHeaderCallback = DrawBuyableHeader,
                drawElementCallback = DrawBuyableChild
            };

        }

        private void DrawBuyableChild(Rect r, int index, bool isActive, bool isFocused)
        {
            //var key = BuyablesMapKeys[index];
            SerializedProperty _key = MapKeyProperty.GetArrayElementAtIndex(index);
            var key = _key.intValue;
            var value = TargetClassRef.BuyablesIDMap.GetValueOrDefault(key, null);


            GUIContent label = new(key.ToString(), "\"" + value + "\" buyable ID");
            EditorGUI.ObjectField(r, label, value, typeof(Buyable), false);
        }

        private void DrawBuyableHeader(Rect headerRect)
        {
            GUI.Label(headerRect, "Buyables Reference's ID Map: (Key => Value)");
        }

        public override void OnInspectorGUI()
        {
            Init();
            CustomInit();

            DrawDefaultInspector();
            EditorGUILayout.Space(10);

            if (GUILayout.Button("Setup!", EditorStyles.miniButtonMid))
            {
                ShowRegisteredPrefabs = true; ShowBuyables = true;
                TargetClassRef.SetupRegisteredSpawnablePrefabs();
            }

            EditorGUILayout.BeginHorizontal();
            ShowBuyables = EditorGUILayout.Toggle("Show Buyables", ShowBuyables);
            ShowRegisteredPrefabs = EditorGUILayout.Toggle("Show Prefabs", ShowRegisteredPrefabs);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(10);


            EditorGUI.BeginChangeCheck();
            if (ShowBuyables)
            {
                buyablesList.DoLayoutList();
            }

            if (ShowRegisteredPrefabs)
            {
                spawnList.DoLayoutList();
            }
            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
            }
        }
    }
#endif

    #region UnityEngine.Rendering.SerializedDictionary produces error on deserializing (i.e. custom made to log it as warnings)
    // -------------------------------------------------------------------------------------------------------
    /// <summary>
    /// Unity can't serialize Dictionary so here's a custom wrapper that does. Note that you have to
    /// extend it before it can be serialized as Unity won't serialized generic-based types either.
    /// </summary>
    /// <typeparam name="K">The key type</typeparam>
    /// <typeparam name="V">The value</typeparam>
    /// <example>
    /// public sealed class MyDictionary : SerializedDictionary&lt;KeyType, ValueType&gt; {}
    /// </example>
    [Serializable]
    public class SerializedDictionary<K, V> : SerializedDictionary<K, V, K, V>
    {
        /// <summary>
        /// Conversion to serialize a key
        /// </summary>
        /// <param name="key">The key to serialize</param>
        /// <returns>The Key that has been serialized</returns>
        public override K SerializeKey(K key) => key;

        /// <summary>
        /// Conversion to serialize a value
        /// </summary>
        /// <param name="val">The value</param>
        /// <returns>The value</returns>
        public override V SerializeValue(V val) => val;

        /// <summary>
        /// Conversion to serialize a key
        /// </summary>
        /// <param name="key">The key to serialize</param>
        /// <returns>The Key that has been serialized</returns>
        public override K DeserializeKey(K key) => key;

        /// <summary>
        /// Conversion to serialize a value
        /// </summary>
        /// <param name="val">The value</param>
        /// <returns>The value</returns>
        public override V DeserializeValue(V val) => val;
    }

    /// <summary>
    /// Dictionary that can serialize keys and values as other types
    /// </summary>
    /// <typeparam name="K">The key type</typeparam>
    /// <typeparam name="V">The value type</typeparam>
    /// <typeparam name="SK">The type which the key will be serialized for</typeparam>
    /// <typeparam name="SV">The type which the value will be serialized for</typeparam>
    [Serializable]
    public abstract class SerializedDictionary<K, V, SK, SV> : Dictionary<K, V>, ISerializationCallbackReceiver
    {
        [SerializeField]
        List<SK> m_Keys = new List<SK>();

        [SerializeField]
        List<SV> m_Values = new List<SV>();

        /// <summary>
        /// From <see cref="K"/> to <see cref="SK"/>
        /// </summary>
        /// <param name="key">They key in <see cref="K"/></param>
        /// <returns>The key in <see cref="SK"/></returns>
        public abstract SK SerializeKey(K key);

        /// <summary>
        /// From <see cref="V"/> to <see cref="SV"/>
        /// </summary>
        /// <param name="value">The value in <see cref="V"/></param>
        /// <returns>The value in <see cref="SV"/></returns>
        public abstract SV SerializeValue(V value);


        /// <summary>
        /// From <see cref="SK"/> to <see cref="K"/>
        /// </summary>
        /// <param name="serializedKey">They key in <see cref="SK"/></param>
        /// <returns>The key in <see cref="K"/></returns>
        public abstract K DeserializeKey(SK serializedKey);

        /// <summary>
        /// From <see cref="SV"/> to <see cref="V"/>
        /// </summary>
        /// <param name="serializedValue">The value in <see cref="SV"/></param>
        /// <returns>The value in <see cref="V"/></returns>
        public abstract V DeserializeValue(SV serializedValue);

        /// <summary>
        /// OnBeforeSerialize implementation.
        /// </summary>
        public void OnBeforeSerialize()
        {
            m_Keys.Clear();
            m_Values.Clear();

            foreach (var kvp in this)
            {
                try
                {
                    m_Keys.Add(SerializeKey(kvp.Key));
                    m_Values.Add(SerializeValue(kvp.Value));
                }
                catch (Exception e)
                {
                    Debug.LogWarning(e);
                }
            }
        }

        /// <summary>
        /// OnAfterDeserialize implementation.
        /// </summary>
        public void OnAfterDeserialize()
        {
            for (int i = 0; i < m_Keys.Count; i++)
            {
                try
                {
                    Add(DeserializeKey(m_Keys[i]), DeserializeValue(m_Values[i]));
                }
                catch (Exception e)
                {
                    Debug.LogWarning(e);
                }
            }

            m_Keys.Clear();
            m_Values.Clear();
        }
    }
    // -------------------------------------------------------------------------------------------------------
    #endregion
}