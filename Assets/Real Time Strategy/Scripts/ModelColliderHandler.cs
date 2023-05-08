using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem.Utilities;

public class ModelColliderHandler : MonoBehaviour
{
    /*
        This script is done for creating Mesh Collider onto parrent gameobject, 
        so that GetComponent<> can on collider's raycast can work easily rather than 
        again search for the required component in parent also.
     */
    [SerializeField] private bool removeExistingParentColliders = false;
    [SerializeField] private bool generateColliders = false;
    [Tooltip("the main gameobject in which to generate the colliders")]
    [SerializeField] private Transform colliderParent; public Transform CcolliderParent => colliderParent;


    [SerializeField] private MeshFilter[] meshFilters;
    [SerializeField] private MeshRenderer[] meshRenderers;
    [SerializeField] private List<MeshCollider> colliders;
    public ReadOnlyArray<MeshFilter> MeshFilters => meshFilters;
    public ReadOnlyArray<MeshRenderer> MeshRenderers => meshRenderers;
    public ReadOnlyArray<MeshCollider> Colliders => colliders.ToArray();

#if UNITY_EDITOR
    public (string, MessageType) Run()
    {
        // make sure any child gameobject, should have both (MeshFilter and MeshRenderer)
        // or both should not be present to main the order
        meshFilters = transform.GetComponentsInChildren<MeshFilter>();
        meshRenderers = transform.GetComponentsInChildren<MeshRenderer>();
        colliders.Clear();

        if (generateColliders)
        {
            if (colliderParent == null)
                return ("Parent GameObject not set, failed to generate colliders.", MessageType.Error);

            colliderParent.localPosition = transform.localPosition;
            colliderParent.localRotation = transform.localRotation;
            colliderParent.localScale = transform.localScale;

            if (removeExistingParentColliders)
            {
                foreach (Collider collider in colliderParent.GetComponentsInChildren<Collider>())
                    DestroyImmediate(collider.gameObject);
            }

            // remove any colliders if present in child's models
            var cols = transform.GetComponentsInChildren<Collider>().ToList();
            cols.ForEach(x => DestroyImmediate(x)); // on editor, destroy cant be called
            cols.Clear();

            foreach (MeshFilter filter in meshFilters)
            {
                // create new game obj for the resp obj
                GameObject temp = new GameObject(filter.name);
                temp.transform.parent = colliderParent; temp.layer = colliderParent.gameObject.layer;
                temp.transform.localPosition = filter.transform.localPosition;
                temp.transform.localRotation = filter.transform.localRotation;
                temp.transform.localScale = filter.transform.localScale;

                // add mesh collider
                var mc = temp.AddComponent<MeshCollider>();
                mc.sharedMesh = filter.sharedMesh;
                mc.convex = true;
                colliders.Add(mc);
            }
            return ("Colliders created successfully, Save the Prefab to save these changes", MessageType.Info);
        }
        colliders = transform.GetComponentsInChildren<MeshCollider>().ToList();
        return ("Mesh Filters and Renderer references from the child has been pulled form childs", MessageType.None);
    }
#endif
}

#if UNITY_EDITOR
[CustomEditor(typeof(ModelColliderHandler))]
public class MCHEditor : Editor
{
    float timer = 0;
    (string, MessageType) temp;
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector(); // same as  => base.OnInspectorGUI(); this actually calls DrawDefaultInspector(),
                                // see in Editor Core Module for ur reference

        ModelColliderHandler mch = target as ModelColliderHandler;


        if (GUILayout.Button("Setup!", EditorStyles.miniButtonMid))
        {
            temp = mch.Run();
            timer = 10; // shows for few secs
        }
        if (timer > 0)
        {
            timer -= Time.deltaTime;
            EditorGUILayout.HelpBox(temp.Item1, temp.Item2);
        }
    }
}
#endif