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
	This script defines the SMPLOptimalPoseBlends class which is used when the 'Optimize Pose Blends' 
	option is turned on. 

	The class has a predefined list of pose blendshapes sorted in order of importance (i.e. going from  
	pose blendshapes that have the greatest effect on the SMPL model to those that have the least effect).
	This is helpful if you need to optimize your executable, especially in the case of VR. Turning on 
	pose blendshape optimization can help provide a much higher framerate.

	Note: the number of pose blendshapes being used in the optimized version of the code can be changed in 
	this file. Currently it is set to 40 (look at the 'size' variable in the SMPLOptimalPoseBlends() constructor.
*/
using System;

public class SMPLOptimalPoseBlends
{
	private int[] _malePoseBlends;
	private int[] _femalePoseBlends;
	private int[] _bestNblends;

	/* Initializes the class. 
	 * 
	 * params:
	 * - gender:	gender of the SMPL model that this class is being used in
	 * - size:		size of 'best N' optimized array of pose blendshapes to use
	 */
	public SMPLOptimalPoseBlends(string gender, int size=40)
	{
		_malePoseBlends = new int[]{40, 44, 162, 31, 35, 170, 161, 153, 120, 4, 14, 5, 7, 16, 13, 93, 150, 111, 8, 110, 17, 141, 147, 133, 118, 114, 91, 109, 138, 92, 131, 84, 119, 123, 23, 169, 145, 25, 165, 1, 136, 82, 151, 166, 65, 160, 96, 69, 149, 104, 158, 90, 137, 56, 10, 146, 88, 106, 94, 60, 142, 86, 140, 167, 97, 156, 83, 148, 87, 34, 32, 101, 41, 12, 43, 135, 42, 33, 168, 105, 152, 95, 29, 26, 22, 144, 3, 6, 164, 102, 159, 132, 155, 185, 174, 66, 194, 100, 139, 196, 52, 50, 184, 64, 187, 157, 115, 128, 143, 183, 203, 172, 117, 163, 178, 173, 176, 15, 177, 199, 38, 182, 37, 181, 108, 124, 59, 70, 190, 204, 186, 85, 121, 2, 61, 192, 63, 55, 57, 195, 127, 116, 81, 188, 107, 191, 122, 68, 201, 134, 175, 179, 71, 11, 154, 113, 205, 200, 129, 130, 125, 0, 28, 112, 77, 79, 89, 62, 98, 47, 103, 180, 51, 48, 24, 171, 46, 54, 58, 99, 19, 20, 27, 126, 9, 30, 39, 36, 21, 193, 67, 202, 74, 197, 78, 189, 73, 75, 53, 198, 206, 49, 76, 80, 18, 45, 72
		};
		_femalePoseBlends = new int[]{40, 44, 35, 31, 161, 170, 153, 162, 4, 13, 138, 17, 147, 120, 14, 8, 151, 16, 141, 150, 5, 145, 119, 136, 110, 7, 118, 133, 111, 149, 142, 131, 114, 123, 94, 109, 93, 169, 146, 137, 140, 104, 160, 91, 165, 88, 106, 86, 167, 90, 82, 166, 23, 84, 25, 152, 1, 43, 34, 41, 32, 156, 33, 95, 97, 158, 10, 132, 105, 135, 184, 101, 56, 155, 60, 143, 164, 42, 102, 168, 6, 187, 196, 100, 144, 159, 185, 29, 188, 128, 172, 174, 69, 83, 177, 12, 139, 92, 50, 52, 178, 64, 65, 194, 163, 176, 148, 127, 203, 87, 117, 59, 108, 183, 124, 61, 191, 204, 157, 115, 66, 175, 3, 195, 181, 70, 15, 68, 173, 121, 38, 129, 96, 192, 190, 116, 200, 26, 22, 199, 85, 205, 122, 186, 179, 182, 134, 125, 113, 107, 55, 201, 130, 37, 2, 11, 79, 77, 154, 112, 171, 81, 57, 180, 48, 0, 28, 103, 63, 24, 19, 202, 89, 46, 62, 193, 71, 30, 21, 20, 197, 189, 27, 54, 99, 67, 51, 47, 58, 39, 198, 206, 36, 98, 75, 9, 73, 78, 74, 126, 53, 49, 76, 80, 18, 45, 72
		};

		_bestNblends = new int[size];
		for (int i = 0; i < size; i++) 
		{
			if (gender == "male")
				_bestNblends [i] = _malePoseBlends [i];
			else
				_bestNblends [i] = _femalePoseBlends [i];
		}
	}

	/* Getter for bestNblends
	 */
	public int[] getSortedPoseBlends()
	{
		return _bestNblends;
	}
}


