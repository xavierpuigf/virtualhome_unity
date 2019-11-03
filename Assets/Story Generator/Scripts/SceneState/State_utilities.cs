using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using StoryGenerator.Helpers;
using StoryGenerator.ChairProperties;
using StoryGenerator.RoomProperties;
using StoryGenerator.Utilities;
using UnityEngine;
using UnityEngine.AI;

namespace StoryGenerator.SceneState
{
	
	[Serializable]
	public class SceneStateSequence
	{
		public List<SceneState> states = new List<SceneState> ();

		// Cache current scene state since we will keep using this
		SceneState curSceneState = null;

		public void AddState(IStateBase sb)
		{
			AddNewSceneStateIfShould();

			// Create actual copy so that states in SceneState class won't be affected by
			// the original, which would be keep changing 
			if (sb is ObjectState)
			{
				curSceneState.objects.Add( (ObjectState) sb.Copy() );
			}
			else
			{
				// Make sure character state is added only once.
				Debug.Assert(curSceneState.characters.Count == 0);
				curSceneState.characters.Add( (CharState) sb.Copy() );
			}
		}

		public void SetFrameNum(int frameNum)
		{
			// Check if the current SceneState's frame number is undef (-1),
			// which means that this is newly added SceneState
			if (curSceneState.frame == -1)
			{
				curSceneState.frame = frameNum;
			}
		}

		void AddNewSceneStateIfShould()
		{
			// Determine whether we should add new SceneState or
			// just append to the current SceneState (last element in the list)
			if (curSceneState == null || curSceneState.frame != -1)
			{
				curSceneState = new SceneState();
				states.Add( curSceneState );
			}
		}
	}

	[Serializable]
	public class SceneState
	{
		public int frame = -1;
		public List<ObjectState> objects = new List<ObjectState> ();
		public List<CharState> characters = new List<CharState> ();
	}

	[Serializable]
	public class ObjectState : IStateBase<ObjectState>
	{
		public string type;
		public bool open;
		public bool power;
		public List<ParentInfo> parents = new List<ParentInfo> ();

		const string STR_PLACEMENT_INSIDE = "inside";
		const string STR_PLACEMENT_TOP = "top";

		public ObjectState(GameObject go, bool _open, bool _power)
		{
			type = Helper.GetClassGroups()[go.name].className;
			open = _open;
			power = _power;
		}


		public ObjectState(string _type, bool _open, bool _power)
		{
			type = _type;
			open = _open;
			power = _power;
		}

		public ObjectState Copy()
		{
			ObjectState osCpy = new ObjectState(type, open, power);
			osCpy.parents = parents.ConvertAll( p => p.Copy() );
			return osCpy;
		}

		object IStateBase.Copy()
		{
			return this.Copy();
		}

		public void ExtractParentInfo(GameObject go)
		{
			const float POS_OFFSET = 0.01f;
			// GameObjects that has more than this distance away from the object of interest
			// won't be cosidered as being under. Empirically determined on our scenes.
			const float THRESHOLD_UNDER = 0.05f;
			// Rooms in our scene have height of 2.5 so this is enough.
			const float CAPSULE_HEIGHT = 3.0f;
			// Make it narrow so that it won't include colliders beside this GameObject.
			const float CAPSULE_RAD = 0.01f;
			const int LAYER_MASK_DEFAULT = 1;
			const int LAYER_MASK_GROUND = 0x1 << 8;
			const int IDX_WALL_LAYER = 10;

			// Clear the current list of parents since they are no longer valid.
			parents.Clear();

			// Get the bounds of this GameObjects to avoid hitting colliders of its own.
			Bounds bnd = GameObjectUtils.GetBounds(go);

			// 1. Get all colliders that are above this GameObject.
			// We want to avoid hitting colliders of its own so add some offset
			Vector3 topCapsule_start = bnd.center + new Vector3(0.0f, bnd.extents.y + POS_OFFSET + CAPSULE_RAD);
			Vector3 topCapsule_end = topCapsule_start + new Vector3(0.0f, CAPSULE_HEIGHT - CAPSULE_RAD);

			HashSet<Transform> topGOs = new HashSet<Transform> ();
			Collider[] cs_top = Physics.OverlapCapsule(topCapsule_start, topCapsule_end, CAPSULE_RAD, LAYER_MASK_DEFAULT);

			foreach (Collider c in cs_top)
			{
				Transform tsfm = c.transform;
				while (tsfm != null)
				{
					if ( Helper.GetClassGroups().ContainsKey(tsfm.name) && ! topGOs.Contains(tsfm) &&
					  tsfm.gameObject.layer != IDX_WALL_LAYER )
					// Corner case for walls. Even though wall layer does not collide with default layer, walls have 
					// children box collider on default layer that are used for camera selection which uses ray cast.
					{
						topGOs.Add(tsfm);
						break;
					}
					tsfm = tsfm.parent;
				}
			}

// Debug.Log("Objects on top of " + go.name);
// foreach (Transform tsfm in topGOs)
// {
// 	Debug.Log(tsfm.name);
// }

			// 2. Get all colliders that are below this GameObject.
			Vector3 botCapsule_start = bnd.center - new Vector3(0.0f, bnd.extents.y + POS_OFFSET + CAPSULE_RAD);
			Vector3 botCapsule_end = botCapsule_start - new Vector3(0.0f, CAPSULE_HEIGHT - CAPSULE_RAD);

			// Transform --> distance from this GameObject
			Dictionary<Transform, float> dict_below = new Dictionary<Transform, float> ();
			Collider[] cs_bot = Physics.OverlapCapsule(botCapsule_start, botCapsule_end, CAPSULE_RAD, LAYER_MASK_DEFAULT);

			foreach (Collider c in cs_bot)
			{
				Transform tsfm = c.transform;
				while (tsfm != null)
				{
					if ( Helper.GetClassGroups().ContainsKey(tsfm.name) && tsfm.gameObject.layer != IDX_WALL_LAYER )
					{
						float distance = go.transform.position.y - c.bounds.center.y;
						bool doesKeyExists = dict_below.ContainsKey(tsfm);

						// Multiple collider from a single GameObject can overlap with the capsule (e.g. kitchen counter - top part and a drawer's collider)
						// If that's the case, find the minimum distance
						if ( (doesKeyExists && distance < dict_below[tsfm]) || (! doesKeyExists) )
						{
							dict_below[tsfm] = distance;
						}
						break;
					}
					tsfm = tsfm.parent;
				}
			}

// Debug.Log("Objects under " + go.name);
// foreach (Transform tsfm in dict_below.Keys)
// {
// 	Debug.Log(tsfm.name);
// }

			IEnumerable<Transform> tsfm_below = dict_below.OrderBy(x => x.Value).Select(x => x.Key);
			// This variable stores the y coordinate of the bottom face of an object of interest.
			// It is used to determine whether two objects are on top of each other.
			float bottomFaceYcoord = bnd.min.y;
			
			foreach (Transform tsfm in tsfm_below)
			{
				string type = Helper.GetClassGroups()[tsfm.name].className;
				Bounds b = GameObjectUtils.GetBounds(tsfm.gameObject);
				float diff = bottomFaceYcoord - b.max.y;

				if ( topGOs.Contains(tsfm) )
				{
					parents.Add( new ParentInfo(type, STR_PLACEMENT_INSIDE) );
					bottomFaceYcoord = b.min.y;
				}
				// Check whether distance between this and the previous bottom most GameObject is within the threshold
				// Must check if the distance is less than zero (e.g. desktop on a floor and desk)
				else if (0 < diff && diff <= THRESHOLD_UNDER)
				{
					parents.Add( new ParentInfo(type, STR_PLACEMENT_TOP) );
					bottomFaceYcoord = b.min.y;
				}
			}

			// 3. Finally, find out which room this object is located, using ground layer mask
			// In our scene, colliders in ground layer won't interact with those on default layer.
			RaycastHit h;
            bool raycast = Physics.Raycast(bnd.center, Vector3.down, out h, CAPSULE_HEIGHT, LAYER_MASK_GROUND);
                
            Debug.Assert(raycast, "Raycast to the ground on " + go.name + " doesn't hit anything.");

			// Get parent of parent. All floor GameObject are grouped under "Floor"
			// so it looks like: Home >> Room >> Floor >> Floor GameObject.
			string roomName = h.transform.parent.parent.GetComponent<Properties_room> ().roomName;
			parents.Add( new ParentInfo(roomName, STR_PLACEMENT_INSIDE) );
		}

		public void SetParent_inside(Transform tsfm_parent)
		{
			// Clear the current list of parents since they are no longer valid.
			parents.Clear();
			parents.Add( new ParentInfo(tsfm_parent.name, STR_PLACEMENT_INSIDE) );
		}

		public void SetParent_top(Transform tsfm_parent)
		{
			// Clear the current list of parents since they are no longer valid.
			parents.Clear();
			parents.Add( new ParentInfo(tsfm_parent.name, STR_PLACEMENT_TOP) );
		}

	}

	[Serializable]
	public class CharState : IStateBase<CharState>
	{
		public string room;
		public string sitting_on;
		public string hand_left;
		public string hand_right;

		public CharState Copy()
		{
			return (CharState) this.MemberwiseClone();
		}

		object IStateBase.Copy()
		{
			return this.Copy();
		}
	}

	[Serializable]
	public class ParentInfo : IStateBase<ParentInfo>
	{
		public string type;
		public string placement;

		public ParentInfo(string _type, string _placement)
		{
			type = _type;
			placement = _placement;
		}

		public ParentInfo Copy()
		{
			return (ParentInfo) this.MemberwiseClone();
		}

		object IStateBase.Copy()
		{
			return this.Copy();
		}
	}

	public interface IStateBase<T> : IStateBase
	{
		new T Copy();
	}

	public interface IStateBase
	{
		object Copy();
	}

}