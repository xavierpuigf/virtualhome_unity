/*
License:
--------
Copyright 2017 Naureen Mahmood and the Max Planck Gesellschaft.  
All rights reserved. This software is provided for research purposes only.
By using this software you agree to the terms of the SMPL Model license here http://smpl.is.tue.mpg.de/license

To get more information about SMPL and other downloads visit: http://smpl.is.tue.mpg.
For comments or questions, please email us at: smpl@tuebingen.mpg.de

Special thanks to Joachim Tesch and Max Planck Institute for Biological Cybernetics 
in helping to create and test these scripts for Unity.

This is a demo version of the scripts & sample project for using the SMPL model's shape-blendshapes 
& corrective pose-blendshapes inside Unity. We would be happy to receive comments, help and suggestions 
on improving the model and in making it available on more platforms. 


	About this Script:
	==================
	This script file defines the SMPLModifyBones class which updates the joints of the model after a new 
	shape has been defined using the 'shapeParmsJSON' field and SMPLJointCalculator class has computed 
	the new joints. 
*/
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class SMPLModifyBones {

	private SkinnedMeshRenderer targetRenderer;

	private Transform[] _bones = null;
	private Transform[] _bonesBackup = null;

    private string _boneNamePrefix;
    private Dictionary<string, int> _boneNameToJointIndex;

    private bool _initialized;
    private bool _bonesAreModified;

    private Transform _pelvis;
    private Vector3[] _bonePositions;
    private Mesh _bakedMesh = null;

	public SMPLModifyBones(SkinnedMeshRenderer tr)
    {
		targetRenderer = tr;

        _initialized = false;
        _bonesAreModified = false;

        _boneNamePrefix = "";

        _boneNameToJointIndex = new Dictionary<string, int>();

        _boneNameToJointIndex.Add("Pelvis", 0);
        _boneNameToJointIndex.Add("L_Hip", 1);
        _boneNameToJointIndex.Add("R_Hip", 2);
        _boneNameToJointIndex.Add("Spine1", 3);
        _boneNameToJointIndex.Add("L_Knee", 4);
        _boneNameToJointIndex.Add("R_Knee", 5);
        _boneNameToJointIndex.Add("Spine2", 6);
        _boneNameToJointIndex.Add("L_Ankle", 7);
        _boneNameToJointIndex.Add("R_Ankle", 8);
        _boneNameToJointIndex.Add("Spine3", 9);
        _boneNameToJointIndex.Add("L_Foot", 10);
        _boneNameToJointIndex.Add("R_Foot", 11);
        _boneNameToJointIndex.Add("Neck", 12);
        _boneNameToJointIndex.Add("L_Collar", 13);
        _boneNameToJointIndex.Add("R_Collar", 14);
        _boneNameToJointIndex.Add("Head", 15);
        _boneNameToJointIndex.Add("L_Shoulder", 16);
        _boneNameToJointIndex.Add("R_Shoulder", 17);
        _boneNameToJointIndex.Add("L_Elbow", 18);
        _boneNameToJointIndex.Add("R_Elbow", 19);
        _boneNameToJointIndex.Add("L_Wrist", 20);
        _boneNameToJointIndex.Add("R_Wrist", 21);
        _boneNameToJointIndex.Add("L_Hand", 22);
        _boneNameToJointIndex.Add("R_Hand", 23);

        _bakedMesh = new Mesh();
    }

	// Use this for initialization
	public bool initialize() {
		if (targetRenderer == null)
		{
			throw new System.ArgumentNullException("ERROR: The script should be added to the 'SkinnedMeshRenderer Object");
			return false;
		}

		_bones = targetRenderer.bones;
        _bonePositions = new Vector3[_bones.Length];
        _bonesBackup = new Transform[_bones.Length];
        _cloneBones(_bones, _bonesBackup);

        // Determine bone name prefix
        foreach (Transform bone in _bones)
        {
            if (bone.name.EndsWith("root"))
            {
                int index = bone.name.IndexOf("root");
                _boneNamePrefix = bone.name.Substring(0, index);
                break;
            }
        }

        // Determine pelvis node
        foreach (Transform bone in _bones)
        {
            if (bone.name.EndsWith("Pelvis"))
            {
                _pelvis = bone;
                break;
            }
        }

        Debug.Log("INFO: Bone name prefix: '" + _boneNamePrefix + "'");
        _initialized = true;
		return true;
    }

	public Transform[] getBones()
	{
		return _bones;
	}
		
	public Dictionary<string, int> getB2J_indices()
	{
		return _boneNameToJointIndex;
	}

    public Transform getPelvis()
    {
        return _pelvis;
    }

    public Vector3[] getBonePositions()
    {
        return _bonePositions;
    }

	public string getBoneNamePrefix()
	{
		return _boneNamePrefix;
	}

    public bool updateBonePositions(Vector3[] newPositions, bool feetOnGround = true)
    {
        if (! _initialized)
            return false;

        float heightOffset = 0.0f;

        int pelvisIndex = -1;
		for (int i=0; i<_bones.Length; i++)
		{
            int index;
            string boneName = _bones[i].name;

            // Remove f_avg/m_avg prefix
            boneName = boneName.Replace(_boneNamePrefix, "");

            if (boneName == "root")
                continue;

            if (boneName == "Pelvis")
                pelvisIndex = i;


            Transform avatarTransform = targetRenderer.transform.parent;
            if (_boneNameToJointIndex.TryGetValue(boneName, out index))
            {
                // Incoming new positions from joint calculation are centered at origin in world space
                // Transform to avatar position+orientation for correct world space position
                _bones[i].position = avatarTransform.TransformPoint(newPositions[index]);
                _bonePositions[i] = _bones[i].position;
            }
            else
            {
                Debug.LogError("ERROR: No joint index for given bone name: " + boneName);
            }
		}

        _setBindPose(_bones);

        if (feetOnGround)
        {
            Vector3 min = new Vector3();
            Vector3 max = new Vector3();
            _localBounds(ref min, ref max);
            heightOffset = -min.y;

            _bones[pelvisIndex].Translate(0.0f, heightOffset, 0.0f);

            // Update bone positions to reflect new pelvis position
            for (int i=0; i<_bones.Length; i++)
            {
                _bonePositions[i] = _bones[i].position;
            }
        }

        return true;
		
	}

	public bool updateBoneAngles(float[][] pose, float[] trans)
	{	
		Quaternion quat;
		int pelvisIndex = -1;

		for (int i=0; i<_bones.Length; i++)
		{
			int index;
			string boneName = _bones[i].name;

			// Remove f_avg/m_avg prefix
			boneName = boneName.Replace(_boneNamePrefix, "");

			if (boneName == "root") {
				continue;
			}

			if (boneName == "Pelvis")
				pelvisIndex = i;
			
			if (_boneNameToJointIndex.TryGetValue(boneName, out index))
			{
				quat.x = pose [index][0];
				quat.y = pose [index][1];
				quat.z = pose [index][2];
				quat.w = pose [index][3];

				/*	Quaternions */
				_bones[i].localRotation = quat;
			}
			else
			{
				Debug.LogError("ERROR: No joint index for given bone name: " + boneName);
			}
		}
			
		_bones[pelvisIndex].localPosition = new Vector3(trans[0], trans[1], trans[2]);
		_bonesAreModified = true;
		return true;
	}



    private void _cloneBones(Transform[] bonesOriginal, Transform[] bonesModified)
	{
		// Clone transforms (name, position, rotation)
		for (int i=0; i<bonesModified.Length; i++)
		{
			bonesModified[i] = new GameObject().transform;
			bonesModified[i].name = bonesOriginal[i].name + "_clone";
			bonesModified[i].position = bonesOriginal[i].position;
			bonesModified[i].rotation = bonesOriginal[i].rotation;
		}

		// Clone hierarchy
		for (int i=0; i<bonesModified.Length; i++)
		{
			string parentName = bonesOriginal[i].parent.name;

			// Find transform with same name in copy
			GameObject go = GameObject.Find(parentName + "_clone");
			if (go == null)
			{
				// Cannot find parent so must be armature
				bonesModified[i].parent = bonesOriginal[i].parent;
			}
			else
			{
				bonesModified[i].parent = go.transform;
			}

		}

		return;

	}	

	private void _restoreBones()
	{
		// Restore transforms (name, position, rotation)
		for (int i=0; i<_bones.Length; i++)
		{
			_bones[i].position = _bonesBackup[i].position;
			_bones[i].rotation = _bonesBackup[i].rotation;
		}
	}	

	private void _setBindPose(Transform[] bones)
	{
		Matrix4x4[] bindPoses = targetRenderer.sharedMesh.bindposes;
//		Debug.Log("Bind poses: " + bindPoses.Length);

        Transform avatarRootTransform = targetRenderer.transform.parent;

		for (int i=0; i<bones.Length; i++)
		{
	        // The bind pose is bone's inverse transformation matrix.
	        // Make this matrix relative to the avatar root so that we can move the root game object around freely.            
            bindPoses[i] = bones[i].worldToLocalMatrix * avatarRootTransform.localToWorldMatrix;
		}

		targetRenderer.bones = bones;
		Mesh sharedMesh = targetRenderer.sharedMesh;
		sharedMesh.bindposes = bindPoses;
		targetRenderer.sharedMesh = sharedMesh;

        _bonesAreModified = true;
	}

    private void _localBounds(ref Vector3 min, ref Vector3 max)
    {
        targetRenderer.BakeMesh(_bakedMesh);
        Vector3[] vertices = _bakedMesh.vertices;
        int numVertices = vertices.Length;

        float xMin = Mathf.Infinity;
        float xMax = Mathf.NegativeInfinity;
        float yMin = Mathf.Infinity;
        float yMax = Mathf.NegativeInfinity;
        float zMin = Mathf.Infinity;
        float zMax = Mathf.NegativeInfinity;

        for (int i=0; i<numVertices; i++)
        {
            Vector3 v = vertices[i];

            if (v.x < xMin)
            {
                xMin = v.x;
            }
            else if (v.x > xMax)
            {
                xMax = v.x;
            }

            if (v.y < yMin)
            {
                yMin = v.y;
            }
            else if (v.y > yMax)
            {
                yMax = v.y;
            }

            if (v.z < zMin)
            {
                zMin = v.z;
            }
            else if (v.z > zMax)
            {
                zMax = v.z;
            }
        }

        min.x = xMin;
        min.y = yMin;
        min.z = zMin;
        max.x = xMax;
        max.y = yMax;
        max.z = zMax;
//      Debug.Log("MinMax: x[" + xMin + "," + xMax + "], y["  + yMin + "," + yMax + "], z["  + zMin + "," + zMax + "]");
    }

    // Note: Cannot use OnDestroy() because in OnDestroy the bone Transform objects are already destroyed
    //       See also https://docs.unity3d.com/Manual/ExecutionOrder.html
	public void OnApplicationQuit()
	{
		Debug.Log("OnApplicationQuit: Restoring original bind pose");

        if (! _initialized)
            return;

        if (! _bonesAreModified)
            return;

		if ((_bones != null) && (_bonesBackup != null))
		{
			_restoreBones();
			_setBindPose(_bones);
		}
	}
}
