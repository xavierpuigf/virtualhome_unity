using UnityEngine;
using UnityEditor;
using System.Collections;
using System.Collections.Generic;
 
public class UpdateSkinnedMeshWindow : EditorWindow
{
    [MenuItem("Window/UpdateSkinnedMeshWindow")]
    public static void OpenWindow()
    {
        var window = GetWindow<UpdateSkinnedMeshWindow>();
        window.titleContent = new GUIContent("Skin Updater");
    }
 
    private SkinnedMeshRenderer targetSkin;
    private SkinnedMeshRenderer originalSkin;

    private Transform transformRoot;
 
    private Transform GetClosest(Transform curr, Transform[] roots) {
    {
            Transform tMin = null;
            float minDist = Mathf.Infinity;
            Transform closestBone = null;
            Vector3 currentPos = curr.position;
            foreach (Transform t in roots)
            {
                float dist = Vector3.Distance(t.position, currentPos);
                if (dist < minDist)
                {
                    tMin = t;
                    minDist = dist;
                    closestBone = t;
                }
            }
            return closestBone;
        }
    }

    private Transform GetClosestUsedBone(Transform curr, List<string> keys, Transform[] roots) {
    {
            Transform tMin = null;
            float minDist = Mathf.Infinity;
            Transform closestBone = null;
            Vector3 currentPos = curr.position;
            foreach (Transform t in roots)
            {
                if (keys.Contains(t.name) && curr.position != t.position) {
                    float dist = Vector3.Distance(t.position, currentPos);
                    if (dist < minDist)
                    {
                        tMin = t;
                        minDist = dist;
                        closestBone = t;
                    }
                }
            }
            return closestBone;
        }
    }
    private void OnGUI()
    {
        // Dictionary<string, string> nameMap = new Dictionary<string, string>(){
        //     {"Root_M", "Hips"},
        //     {"Hip_L", "LeftHip"},
        //     {"Knee_L", "LeftKnee"},
        //     {"Ankle_L", "LeftAnkle"},
        //     {"Toes_L", "LeftToe"},
        //     {"Hip_R", "RightHip"},
        //     {"Knee_R", "RightKnee"},
        //     {"Ankle_R", "RightAnkle"},
        //     {"Toes_R", "RightToe"},
        //     {"Spine1_M", "Chest"},
        //     {"Spine1Part1_M", "Chest2"},
        //     {"Spine1Part2_M", "Chest3"},
        //     {"Chest_M", "Chest4"},
        //     {"Neck_M", "Neck"},
        //     {"Head_M", "Head"},
        //     {"Scapula_L", "LeftCollar"},
        //     {"Shoulder_L", "LeftShoulder"},
        //     {"Elbow_L", "LeftElbow"},
        //     {"Wrist_L", "LeftWrist"},
        //     {"Scapula_R", "RightCollar"},
        //     {"Shoulder_R", "RightShoulder"},
        //     {"Elbow_R", "RightElbow"},
        //     {"Wrist_R", "RightWrist"}
        // };
        // // {"Cup_L", "Tip"},
        // // {"Cup_R", "Tip"}
        Dictionary<string, string> nameMap = new Dictionary<string, string>(){
            {"m_avg_Pelvis", "Hips"},
            {"m_avg_R_Hip", "LeftHip"},
            {"m_avg_R_Knee", "LeftKnee"},
            {"m_avg_R_Ankle", "LeftAnkle"},
            {"m_avg_R_Foot", "LeftToe"},
            {"m_avg_L_Hip", "RightHip"},
            {"m_avg_L_Knee", "RightKnee"},
            {"m_avg_L_Ankle", "RightAnkle"},
            {"m_avg_L_Foot", "RightToe"},
            {"m_avg_Spine1", "Chest"},
            {"m_avg_Spine2", "Chest2"},
            {"m_avg_Spine3", "Chest3"},
            {"m_avg_Neck", "Neck"},
            {"m_avg_Head", "Head"},
            {"m_avg_R_Collar", "LeftCollar"},
            {"m_avg_R_Shoulder", "LeftShoulder"},
            {"m_avg_R_Elbow", "LeftElbow"},
            {"m_avg_R_Wrist", "LeftWrist"},
            {"m_avg_R_Hand", "LeftTip"},
            {"m_avg_L_Collar", "RightCollar"},
            {"m_avg_L_Shoulder", "RightShoulder"},
            {"m_avg_L_Elbow", "RightElbow"},
            {"m_avg_L_Wrist", "RightWrist"},
            {"m_avg_L_Hand", "RightTip"}
        };

        targetSkin = EditorGUILayout.ObjectField("Target", targetSkin, typeof(SkinnedMeshRenderer), true) as SkinnedMeshRenderer;
        // originalSkin = EditorGUILayout.ObjectField("Original", originalSkin, typeof(SkinnedMeshRenderer), true) as SkinnedMeshRenderer;
        transformRoot = EditorGUILayout.ObjectField("Root", transformRoot, typeof(Transform), true) as Transform;
 
        // GUI.enabled = (targetSkin != null && originalSkin != null && transformRoot != null);
        GUI.enabled = (targetSkin != null && transformRoot != null);

 
        if (GUILayout.Button("Update Skinned Mesh Renderer")) {
            
            Dictionary<string, Transform> skeletonMap = new Dictionary<string, Transform>();
            Transform[] nsmBones = new Transform[nameMap.Count];
            int j = 0;
            foreach (var transf in nameMap.Keys) {
                string objName = nameMap[transf];
                var bone = GameObject.Find(objName).transform;
                skeletonMap.Add(transf, bone);
                nsmBones[j] = bone;
                j++;
            }
 
            Transform[] newBones = new Transform[targetSkin.bones.Length];

            for (int i=0; i < targetSkin.bones.Length; i++) {
                var targetBone = targetSkin.bones[i];
                if (nameMap.ContainsKey(targetBone.name)) {
                    // targetBone.position = new Vector3(0,0,0);
                    targetBone.parent = skeletonMap[targetBone.name];
                    // newBones[i] = skeletonMap[targetBone.name];
                }
            }

            // for (int i=0; i < targetSkin.bones.Length; i++) {
            //     var targetBone = targetSkin.bones[i];
            //     if (nameMap.ContainsKey(targetBone.name)) {
            //         newBones[i] = skeletonMap[targetBone.name];
            //     }
            //     else {
            //         Transform closest_bone = GetClosestUsedBone(targetBone, new List<string>(nameMap.Keys), targetSkin.bones);
            //         Vector3 shift = targetBone.position - closest_bone.position;
            //         Transform t = skeletonMap[closest_bone.name];
            //         Vector3 new_pos = t.position + shift;
            //         Transform newBone = t;
            //         newBone.position = new_pos;
            //         newBones[i] = newBone;
            //         // newBones[i] = targetBone;
            //     }
            // }

            // targetSkin.bones = newBones;
        }
    }
}