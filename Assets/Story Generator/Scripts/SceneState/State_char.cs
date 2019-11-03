using System.Collections;
using System.Collections.Generic;
using StoryGenerator.RoomProperties;
using UnityEngine;

namespace StoryGenerator.SceneState
{

	public class State_char : MonoBehaviour
	{
		// Reference to Recorder's SceneStateSequence
		SceneStateSequence sceneStateSequence = null;
		CharState charState = new CharState();
		int curRoomID = -1; // Instance ID of the GameObject seems to be all positive numbers
		List<Properties_room> list_rooms = new List<Properties_room> (4); // Most homes in our data has 4 rooms.

		public void Initialize(List<GameObject> list_go_rooms, SceneStateSequence sss)
		{
			foreach (GameObject go_room in list_go_rooms)
			{
				list_rooms.Add( go_room.GetComponent<Properties_room> () );
			}

			sceneStateSequence = sss;
			UpdateCharRoom();
		}

		public void UpdateSittingOn(string sitObjName)
		{
			charState.sitting_on = sitObjName;
			sceneStateSequence.AddState(charState);
		}

		public void UpdateHandLeft(string goName)
		{
			charState.hand_left = goName;
			sceneStateSequence.AddState(charState);
		}

		public void UpdateHandRight(string goName)
		{
			charState.hand_right = goName;
			sceneStateSequence.AddState(charState);
		}

		// On each frame, check which room the character is located
		// Simply checks all room without any logic
		// Need to be optimized if this addes too much computation overhead
		void LateUpdate()
		{
			UpdateCharRoom();
		}

		void UpdateCharRoom()
		{
			// For visualization, Look at the how "Occlusion Area" components overlap in the Scene view
			Properties_room pr = null;
			bool isSameRoom = false;

			foreach (Properties_room r in list_rooms)
			{
				if ( r.bounds.Contains(transform.position) )
				{
					// Initial condition: just select this room.
					if (curRoomID == -1)
					{
						pr = r;
						break;
					}
					else if (r.GetInstanceID() == curRoomID)
					{
						isSameRoom = true;
					}
					else
					{
						pr = r;
					}
				}
			}

			if (! isSameRoom)
			{
				Debug.Assert(pr != null, "Cannot find a room where character is at.");
				curRoomID = pr.GetInstanceID();
				charState.room = pr.roomName;
				sceneStateSequence.AddState(charState);
			}
		}
	}

}