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
	This script file defines the SMPLBlendshapes class which creates the module to be attached to the 
	SMPL mesh inside the Unity scene. This script controls both the 'Shape blendshapes' and 'Pose blendshapes'
	for the SMPL model. 

	To use this module:
	1. Please attach this class as a Component to the Mesh object of the SMPL model in the scene (make 
	sure it is attached to the 'Mesh' object, and not the main 'Transform' object. 

	2. Link the SMPL_jReg_*.json file corresponding to the gender of the model to the 'Joint Regressor JSON'
	field of the component (i.e. SMPL_jReg_f_*.json for female, SMPL_jReg_m_*.json for male)

	(optional)
	3. If you want to try using a new shape, link one of the SMPL_samplShape*.json file to the 'Shape Parms JSON'
	field of the component (you can create your own shape parameters similar to the SMPL_sampleShape*.json 
	files)

	(optional)
	4. If the model is running slow on your machine, you can speed up the runtime by turning on the 'Optimize 
	Pose Blends' switch. This makes it so that the script only sets the first 40 most important 
	Pose Blendshapes (out of a total of 207), and ignores the rest to speed up the runtime. 

	This script will create the new body shape provided by the shape parameters in the 'Shape Parms JSON' field. 
	The script updates the Shape blendshapes to create the new mesh to match the shape parameters from the provided 
	JSON file, and updates the skeleton inside the model to match the new body shape. 

	This script will also apply the pose-dependent deformations. These are parameters that are updated with each new 
	pose to create more realistic joint creases and muscle-bulging effects based on the pose. This scripts applies 
	these deformations by updating the Pose blendshapes in the model at each new pose. 
	
*/

using UnityEngine;
using LightweightMatrixCSharp;
using System.Collections.Generic;

public class SMPLBlendshapes : MonoBehaviour {

	public TextAsset jointRegressorJSON;
	public TextAsset shapeParmsJSON;
	public bool optimizePoseBlends = false;

	private SkinnedMeshRenderer targetMeshRenderer;
	private SMPLJointCalculator _jointCalculator;
	private SMPLModifyBones     _modifyBones;
	private SMPLOptimalPoseBlends _optimalPoseBlends;

	private string _gender;
	private List<int> _poseBlendsToSet;
	private int _numPoseBlendsToSet = 40;
	private float _shapeBlendsScale = 5.0f;
	private int _numJoints = 24;
	private int _numShapeParms = 10;
	private float[] _shapeParms;

	void Awake()
	{
		targetMeshRenderer = GetComponent<SkinnedMeshRenderer> ();
	}


	/*	Perform cleanup before quiting	
	 */
	void OnApplicationQuit()
	{
		resetShapeParms ();
		setShapeBlendValues ();
		setPoseBlendValues ();
		calculateJoints ();
		
		// Restore original bones & bindpose
		_modifyBones.OnApplicationQuit ();

	}
		
	/*  Initialize modules and variables
	 */ 
	void Start () 
	{
		// 0. Initialize array of shape parameters & set them all to 0
		_shapeParms = new float[_numShapeParms];
		resetShapeParms ();

		// 1. Initialize joints-modifier. Quit app if there's an error 
		_modifyBones = new SMPLModifyBones(targetMeshRenderer);
		if (!_modifyBones.initialize ()) 
		{
			Debug.Log ("ERROR: Failed to initialize SMPLModifyBones object");
			Application.Quit ();
		}

		// 2. Initialize joints-calculator. Quit app if there's an error 
		_jointCalculator = new SMPLJointCalculator (jointRegressorJSON, _numJoints, _shapeParms.Length);
		if (!_jointCalculator.initialize ()) 
		{
			Debug.Log ("ERROR: Failed to initialize SMPLJointCalculator object");
			Application.Quit ();
		}

		_gender = _jointCalculator.getGender ();
		if (_gender != "male" && _gender != "female") 
		{
			Debug.LogError ("WARNING: Invalid gender.. Setting gender to female");
			_gender = "female";
		}

		// 3. If Optimize Pose Blends is on, use only the best 50 poseblendshapes
		_optimalPoseBlends = new SMPLOptimalPoseBlends(_gender, _numPoseBlendsToSet);
		if (optimizePoseBlends)
			_poseBlendsToSet = new List<int>(_optimalPoseBlends.getSortedPoseBlends ());
		else
			_poseBlendsToSet = null;

		// 4. If JSON for betas is provided, read new betas from JSON file
		if (shapeParmsJSON != null)	 readShapeParms();		

	}

	/* 	Actions to perform at each time step
	 */ 
	void Update()
	{
		// Update the corrective pose blendshapes at each new time step 
		setPoseBlendValues ();

		// Quit application if ESC key is pressed
		if (Input.GetKey("escape"))
			Application.Quit();
	}


	/*	Set the corrective pose blendshape values from current pose-parameters (joint angles)
	 */ 
	void setPoseBlendValues()
	{
		Transform[] _bones = _modifyBones.getBones();
		Dictionary<string, int> _boneNameToJointIndex = _modifyBones.getB2J_indices();
		string _boneNamePrefix = _modifyBones.getBoneNamePrefix();
		int doubledShapeParms = _numShapeParms * 2;

		for (int i = 0; i < _bones.Length; i++) 
		{
			int index;
			string boneName = _bones[i].name;

			// Remove f_avg/m_avg prefix
			boneName = boneName.Replace (_boneNamePrefix, "");

			if (boneName == "root" || boneName == "Pelvis")
				continue;

			if (_boneNameToJointIndex.TryGetValue (boneName, out index)) 
			{
				float[] rot3x3 = Quat_to_3x3Mat(_bones[i].localRotation);

				// Can't use the 'index' value as-is from the _boneNameToJointIndex dict; 
				// The pose blendshapes have no values corresponding to Pelvis joint. 
				// Poseblendshapes start from hip-joint instead of Pelvis.
				// So we have to begin pose_blend indices from 'index-1'
				int idx = (index-1) * 9 * 2; 

				for (int mat_elem = 0; mat_elem < 9; mat_elem++) 
				{
					float pos, neg;
					float theta = rot3x3 [mat_elem];

					if (theta >= 0)
					{
						pos = theta;
						neg = 0.0f;
					}
					else
					{
						pos = 0.0f;
						neg = -theta;
					}

					int non_doubled_idx = ((index - 1) * 9)+mat_elem;

					if (optimizePoseBlends)
					{
						if (!_poseBlendsToSet.Contains (non_doubled_idx))
							continue;
					}
					int bl_idx_0 = doubledShapeParms + idx + (mat_elem * 2) + 0;
					int bl_idx_1 = doubledShapeParms + idx + (mat_elem * 2) + 1;
					targetMeshRenderer.SetBlendShapeWeight (bl_idx_0, pos * 100.0f);
					targetMeshRenderer.SetBlendShapeWeight (bl_idx_1, neg * 100.0f);

				}
			}
		}
	}


	/*  Convert Quaternions to rotation matrices
	 * 
	 * 	parms:
	 * 	- quat: 	the quaternion value to be converted to 3x3 rotation matrix
	 */ 
	private float[] Quat_to_3x3Mat (Quaternion quat)
	{
		// Converting quaternions from Unity's LHS to coordinate system 
		// RHS so that pose blendshapes get the correct values (because SMPL model's
		// pose-blendshapes were trained using a RHS coordinate system)
		float qx = quat.x * -1.0f;
		float qy = quat.y * 1.0f;
		float qz = quat.z * 1.0f;
		float qw = quat.w * -1.0f;

		float[] rot3x3 = new float[9];

		// Note: the -1 in indices 0, 4 & 8 are the rotation-np.eye(3) for pose-mapping of SMPL model
		rot3x3[0] = 1 - (2 * qy*qy) - (2 * qz*qz) - 1;
		rot3x3[1] = (2 * qx * qy) - (2 * qz * qw);
		rot3x3[2] = (2 * qx * qz) + (2 * qy * qw);

		rot3x3[3] = (2 * qx * qy) + (2 * qz * qw);
		rot3x3[4] = 1 - (2 * qx*qx) - (2 * qz*qz) - 1;
		rot3x3[5] = (2 * qy * qz) - (2 * qx * qw);

		rot3x3[6] = (2 * qx * qz) - (2 * qy * qw);
		rot3x3[7] = (2 * qy * qz) + (2 * qx * qw);
		rot3x3[8] = 1 - (2 * qx*qx) - (2 * qy*qy) - 1;

		return rot3x3;
	}


	/*  Set all shape-parameters (betas) to 0
	 */
	public void resetShapeParms()
	{
		for (int bi = 0; bi < _numShapeParms; bi++) 
			_shapeParms[bi] = 0.0f;
	}
		

	/*	Set the new shape-parameters (betas) to create a new body shape
	 */ 
	public void setShapeBlendValues()
	{
		for (int i=0; i<10; i++)
		{
			float pos, neg;
			float beta = _shapeParms[i] / _shapeBlendsScale;

			if (beta >= 0)
			{
				pos = beta;
				neg = 0.0f;
			}
			else
			{
				pos = 0.0f;
				neg = -beta;
			}

			targetMeshRenderer.SetBlendShapeWeight(i * 2 + 0, pos * 100.0f); // map [0, 1] space to [0, 100]
			targetMeshRenderer.SetBlendShapeWeight(i * 2 + 1, neg * 100.0f); // map [0, 1] space to [0, 100]
		}

	}


	/*	Calculate the updated joint positions for new body shape
	 */ 
	public void calculateJoints()
	{
		_jointCalculator.calculateJoints(_shapeParms);
		Vector3[] joints = _jointCalculator.getJoints();
		_modifyBones.updateBonePositions(joints);
	}


	/* Load shape parameters, aka 'betas', from the JSON file provided. 
	 * These parameters change the body shape of the model according to 
	 * the shape-parametrization defined in the SMPL model paper
	 */
	private void readShapeParms()
	{	
		// 1. Read the JSON file with shape parameters 
		SimpleJSON.JSONNode node = SimpleJSON.JSON.Parse (shapeParmsJSON.text);
		for (int bi = 0; bi < node ["betas"].Count; bi++) 
			_shapeParms [bi] = node ["betas"] [bi].AsFloat;

		// 2. Set shape parameters (betas) of avg mesh in FBX model 
		// 	  to shape-parameters (betas) from user's JSON file
		setShapeBlendValues();

		// 3. Calculate joint-locations from betas & update joints of the FBX model
		calculateJoints();
	}
}