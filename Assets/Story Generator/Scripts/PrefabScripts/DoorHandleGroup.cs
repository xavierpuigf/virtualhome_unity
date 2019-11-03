using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace StoryGenerator.DoorProperties
{

	public class DoorHandleGroup : MonoBehaviour
	{
		const int IDX_ITRN_POS = 0;
		const int IDX_LOOK_AT_BODY = 1;
		const int IDX_IK_TARGET_DOOR_HANDLE = 2;
		const int IDX_IK_TARGET_BEND_GOAL = 3;

		void Awake ()
		{
			string[] NAMES = {"Position_interaction", "LookAt_body", "IkTarget_handle", "IkBendGoal"};

			for (int i = 0; i < NAMES.Length; ++i)
			{
				Transform tsfm = new GameObject(NAMES[i]).transform;
				tsfm.SetParent(transform, false);
			}
		}

		public Vector3 GetInteractionPos()
		{
			return transform.GetChild(IDX_ITRN_POS).position;
		}
		
		public Vector3 GetLookAt_body()
		{
			return transform.GetChild(IDX_LOOK_AT_BODY).position;
		}		

		public Transform GetIkTarget_doorHandle()
		{
			return transform.GetChild(IDX_IK_TARGET_DOOR_HANDLE);
		}
		
		public Transform GetIkBendGoal()
		{
			return transform.GetChild(IDX_IK_TARGET_BEND_GOAL);
		}

		public void AdjustForPush()
		{
			Vector3[] LOCAL_POSITIONS = {
				new Vector3(-0.52f, -1.1521f, -0.3031f),  // position_interaction
				new Vector3(2.0f, -1.1521f, -0.3031f),  // LookAt_body
				new Vector3(-0.093f, 0.0279f, -0.0721f),  // IkTarget_handle
				new Vector3(-0.368f, -0.2821f, 0.0639f)  // IkBendGoal
			};

			SetChildPos(LOCAL_POSITIONS);
		}

		public void AdjustForPull()
		{
			Vector3[] LOCAL_POSITIONS = {
				new Vector3(0.802f, -1.1521f, -0.3031f), // position_interaction
				new Vector3(-2.0f, -1.1521f, -0.3031f), // LookAt_body
				new Vector3(0.093f, 0.0279f, -0.0721f), // IkTarget_handle
				new Vector3(0.275f, -0.229f, -0.272f) // IkBendGoal
			};

			SetChildPos(LOCAL_POSITIONS);
		}

		void SetChildPos(Vector3[] localPosition)
		{
			for (int i = 0; i < localPosition.Length; ++i)
			{
				Transform tsfm = transform.GetChild(i);
				tsfm.localPosition = localPosition[i];
			}
		}
	}

}