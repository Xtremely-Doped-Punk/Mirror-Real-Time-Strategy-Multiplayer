using Mirror;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
# if UNITY_EDITOR
using static UnityEditor.BaseShaderGUI;
#endif

namespace RTS
{
    public class BuildingPlacer : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler // client end only UI
    {
        [Header("References Needed")]
        [SerializeField] private Buyable buyableRef = null;
        [SerializeField] private Image icon = null;
        [SerializeField] private TMP_Text priceText = null;
        [SerializeField] private LayerMask platformLayers = new();
        [SerializeField] private LayerMask buildingLayers = new();

        private Camera mainCam;
        private RTSPlayer player;
        private GameObject buildingPreviewInstance; // client end only created preview of the building gameobject that needs to be placed
        private ModelColliderHandler ModelInstance; // renderer to display if it is a valid location to place building (show in red/green colors)
        private bool _canPlace = false, canPlace = true;
        private Vector3 avg_center, max_extends;
        private Material[] previewMaterials;
        private Material[] originalMaterials;

        private void Start()
        {
            mainCam = Camera.main;
            priceText.text = buyableRef.Price.ToString();
            icon.sprite = buyableRef.Icon;

            var modelHandler = buyableRef.GetComponentInChildren<ModelColliderHandler>();
            if (modelHandler != null)
            {
                var buildingColliders = modelHandler.Colliders;
                var centers = buildingColliders.Select(x => x.bounds.center);
                avg_center = new Vector3(centers.Average(v => v.x), centers.Average(v => v.y), centers.Average(v => v.z));
                max_extends = buildingColliders.Max(x => 
                Vector3.Scale(x.sharedMesh.bounds.extents, x.transform.localScale) + Vector3.one * Building.BUILDING_GAP);
                originalMaterials = modelHandler.MeshRenderers.Select(x => x.sharedMaterial).ToArray();
            }
            else
                Debug.LogError("Model Collider Handler component not found in reference");
        }

        private void Update()
        {
            // -------------------------------------- [ Fail Safes ] -----------------------------------------
            if (player == null) // just for safty, incase to work without offline and online scenes
            {
                if (!(NetworkClient.connection == null || NetworkClient.connection.identity == null))
                    player = NetworkClient.connection.identity.GetComponent<RTSPlayer>();
                else return;
            }

            // -------------------------------------- [ OnDragging ] -----------------------------------------
            if (buildingPreviewInstance != null)
            {
                OnPointerDrag(); // updating the building preview here...
                ToggleMaterialTransparency();
            }
        }

        public void OnPointerDown(PointerEventData eventData) // before starting dragging
        {
            // we are giving controls to left mouse btn only for now
            if (player == null || eventData.button != PointerEventData.InputButton.Left) return;

            // disable unit selection handler while placing buildings
            if (UnitSelectionHandler.Instance) UnitSelectionHandler.Instance.enabled = false;

            buildingPreviewInstance = Instantiate(buyableRef.Preview);
            ModelInstance = buildingPreviewInstance.GetComponentInChildren<ModelColliderHandler>();
            previewMaterials = ModelInstance.MeshRenderers.Select(x => x.material).ToArray();

            // initially deactivate the intance (later we can adjust the renderer component)
            buildingPreviewInstance.SetActive(false);
            _canPlace = false;
        }

        public void OnPointerUp(PointerEventData eventData) // after stopping dragging
        {
            // enable back unit selection handler after placing buildings
            if (UnitSelectionHandler.Instance)
                UnitSelectionHandler.Instance.enabled = true;


            if (buildingPreviewInstance == null) return;

            if (_canPlace)
            {
                Debug.Log("Server req made for spawning new building :", buyableRef);
                // place building if other conditions are satisfied
                // like player has enough money and it is right place to place it in environment
                player.cmdPlaceBuildingonBase(buyableRef.ID,
                    buildingPreviewInstance.transform.position,
                    buildingPreviewInstance.transform.rotation);
            }
            else
            {
                _canPlace = true;
                ToggleMaterialTransparency();
            }
            Destroy(buildingPreviewInstance);
        }

        private void OnPointerDrag() // not interface, kinda self made case
        {
            _canPlace = false;

            Ray ray = mainCam.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (!Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, platformLayers)) return;

            //Debug.Log("Hit");
            if (!buildingPreviewInstance.activeSelf)
                buildingPreviewInstance.SetActive(true);

            if (Physics.CheckBox(hit.point + avg_center, max_extends, 
                buildingPreviewInstance.transform.rotation, buildingLayers))
            { // building is overlapping other, thus return as we cant place the building
                // note: building preview is not suposed to contain collider in order for this to work
                Debug.Log("Overlapping over buildings/units, cant place here.");
                return;
            }
            buildingPreviewInstance.transform.position = hit.point;

            if (!player.MyBases.Any(x => x.withinBaseRange(hit.point))) return; // if not in any base's range
            
            _canPlace = true;
        }

        private void ToggleMaterialTransparency()
        {                
            if (canPlace == _canPlace) return;

            canPlace = _canPlace;

            Debug.Log("Toggle Material Transparency : "+canPlace);
            /*
            You can't change in runtime the surface type to transparent in urp/lwrp, 
            when you do it from the inspector during play mode, 
            the editor is cheating so you can play with it and after do it properly.
            https://answers.unity.com/questions/1608815/change-surface-type-with-lwrp.html
             */

            for (int i = 0; i < previewMaterials.Length; i++)
            {
                var mat = previewMaterials[i]; var org = originalMaterials[i];
                if (!canPlace)
                {
                    //org.SetFloat("_Surface", (float)SurfaceType.Transparent);
                    //org.SetFloat("_Blend", (float)BlendMode.Alpha);
                    //org.SetColor("_EmissionColor", Color.red);
                    mat.SetColor("_BaseColor", MutiplyColors(org.GetColor("_BaseColor"), Color.red));
                }
                else
                {
                    //org.SetFloat("_Surface", (float)SurfaceType.Opaque);
                    //org.SetColor("_EmissionColor", Color.green);
                    mat.SetColor("_BaseColor", MutiplyColors(org.GetColor("_BaseColor"), Color.green));
                }
            }
        }
        private Color MutiplyColors(Color c1, Color c2, float decay = 0.125f)
        {
            float[] vals = { c1.r, c2.r, c1.g, c2.g, c1.b, c2.b, c1.a, c2.a };
            for (int i = 0; i < vals.Length; i++)
            {
                if (vals[i]<=decay)
                    vals[i] = decay;
            }
            return new Color(vals[0] * vals[1], vals[2] * vals[3], vals[4] * vals[5], vals[6] * vals[7]);
        }

        public void OnPointerClick(PointerEventData eventData) // rotate by 90 deg on right click
        {
            if (buildingPreviewInstance == null || eventData.button != PointerEventData.InputButton.Right) return;
            buildingPreviewInstance.transform.Rotate(new Vector3(0, 90, 0));
        }
    }
}