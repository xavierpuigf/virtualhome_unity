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
	=================
	This script file defines the SMPLJointCalculator class which computes the joints of the SMPL model 
	whenever the base mesh is changed using 'shapeParmsJSON' field in the SMPLBlendshapes module.

	The class uses the joint-regressor from one for the 'SMPL_jReg_*.json' file to regress to the 
	updated joint locations for the new mesh shape.
*/
using UnityEngine;
using System.Collections;
using LightweightMatrixCSharp;

public class SMPLJointCalculator {

	private TextAsset jntsRegr_JSON;
	private int _numberOfJoints;
	private int _numberOfBetas;
    private Matrix[] _template;
	private Matrix[] _jntsRegr;
    private Vector3[] _joints;
	private bool _initialized;
	private string _gender;

	public SMPLJointCalculator(TextAsset jreg, int numJ, int numB)
	{
		_numberOfBetas = numB;
		_numberOfJoints = numJ;
		jntsRegr_JSON = jreg;

		_initialized = false;
		_joints = new Vector3[_numberOfJoints];

        _template = new Matrix[3];
		_jntsRegr = new Matrix[3];

        for (int i=0; i<=2; i++)
        {
            _template[i] = new Matrix(_numberOfJoints, 1);
            _jntsRegr[i] = new Matrix(_numberOfJoints, _numberOfBetas);
        }
	}

	public bool initialize()
    {
		if (jntsRegr_JSON == null)
        {
			throw new System.ArgumentNullException("ERROR: no joint regressor JSON file provided");
            return false;
        }   

		if (! _initRegressorMatrix(ref _template, ref _jntsRegr, ref jntsRegr_JSON))
        {
			throw new System.ArgumentNullException("ERROR: Could not create joint regressor matrix");
            return false;
        }
        Debug.Log("Joint regressor matrix initialized.");
        _initialized = true;
        return true;
    }

    private bool _initRegressorMatrix(ref Matrix[] jointTemplate, ref Matrix[] regressor, ref TextAsset ta)
    {
        string jsonText = ta.text;
        SimpleJSON.JSONNode node = SimpleJSON.JSON.Parse(jsonText);

		// Get gender of the joint-regressor being used 
	 	_gender = node ["gender"];

		// Init matrices
        for (int i=0; i < _numberOfJoints; i++)
        {
            // Init joint template matrix
            double x = node["template_J"][i][0].AsDouble;
			double y = node["template_J"][i][1].AsDouble; 
			double z = node["template_J"][i][2].AsDouble; 

            (jointTemplate[0])[i, 0] = x;
            (jointTemplate[1])[i, 0] = y;
            (jointTemplate[2])[i, 0] = z;

            // Init beta regressor matrix    
            for (int j=0; j< _numberOfBetas; j++)
            {
				(regressor[0])[i, j] = node["betasJ_regr"][i][0][j].AsDouble;
				(regressor[1])[i, j] = node["betasJ_regr"][i][1][j].AsDouble;
				(regressor[2])[i, j] = node["betasJ_regr"][i][2][j].AsDouble;
            }

        }
        return true;
    }

	public string getGender()
	{
		return _gender;
	}

    public bool calculateJoints(float[] betas)
    {
        if (! _initialized)
            return false;

        // Check dimensions of beta values
        int numCurrentBetas = betas.Length;
        if (numCurrentBetas != _numberOfBetas)
        {
            Debug.LogError("ERROR: Invalid beta input value count in baked mesh: need " + _numberOfBetas + " but have " + numCurrentBetas);
            return false;
        }

        // Create beta value matrix
        Matrix betaMatrix = new Matrix(_numberOfBetas, 1);
        for (int row = 0; row < _numberOfBetas; row++)
        {
            betaMatrix[row, 0] = betas[row];
            //Debug.Log("beta " + row + ": " + betas[row]);
        }           

        // Apply joint regressor to beta matrix to calculate new joint positions
        Matrix[] regressor;
        Matrix[] jointTemplate;
        regressor = _jntsRegr;
        jointTemplate = _template;

        Matrix newJointsX = regressor[0] * betaMatrix + jointTemplate[0];
        Matrix newJointsY = regressor[1] * betaMatrix + jointTemplate[1];
        Matrix newJointsZ = regressor[2] * betaMatrix + jointTemplate[2];

        // Update joints vector
        for (int row = 0; row < _numberOfJoints; row++)
        {
            // Convert regressor to Unity's Left-handed coordinate system by negating X value
            _joints[row] = new Vector3(-(float)newJointsX[row, 0], (float)newJointsY[row, 0], (float)newJointsZ[row, 0]);
        }

        return true;
    }

	public Vector3[] getJoints()
	{
		return _joints;
	}
	
}
