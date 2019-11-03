using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using RootMotion.FinalIK;
using StoryGenerator;
using StoryGenerator.DoorProperties;

public class DoorScene : MonoBehaviour
{
	public bool randomExecution;
	public GameObject door;
	public GameObject chrPush;
	public GameObject chrPull;

	Rigidbody rb_door;
	Vector3 chrPush_origPos;
	Vector3 chrPull_origPos;
	Quaternion chrPush_origQtrn;
	Quaternion chrPull_origQtrn;

	CharacterControl cc;

	#region UnityEventFunctions
	void Start()
	{
		Properties_door pd = door.AddComponent<Properties_door> ();
		pd.SetDoorToClose();

		chrPush_origPos = chrPush.transform.position;
		chrPull_origPos = chrPull.transform.position;
		chrPush_origQtrn = chrPush.transform.rotation;
		chrPull_origQtrn = chrPull.transform.rotation;
	}

	void OnGUI()
	{
		if ( GUILayout.Button("Reset") )
        {
			door.transform.rotation = Quaternion.Euler(0.0f, 90.0f, 0.0f);
			chrPush.transform.position = chrPush_origPos;
			chrPull.transform.position = chrPull_origPos;
			chrPush.transform.rotation = chrPush_origQtrn;
			chrPull.transform.rotation = chrPull_origQtrn;
		}
		if ( GUILayout.Button("Push Door") )
		{
			SetActionChar(chrPush, chrPull);
			StartCoroutine( DoorInteraction(true) );
		}
		if ( GUILayout.Button("Push Door and close behind") )
		{
			SetActionChar(chrPush, chrPull);
			StartCoroutine( OpenDoorAndCloseImmediately(true) );
		}
		if ( GUILayout.Button("Pull Door") )
		{
			// Setting a different action char is good enough to make this
			// action "pull door". Hence, same method is called.
			SetActionChar(chrPull, chrPush);
			StartCoroutine( DoorInteraction(false) );
		}
		if ( GUILayout.Button("Pull Door and close front") )
		{
			SetActionChar(chrPull, chrPush);
			StartCoroutine( OpenDoorAndCloseImmediately(false) );
		}
		if ( GUILayout.Button("Close Facing Door - Left Hand") )
		{
			SetActionChar(chrPull, chrPush, false);
			StartCoroutine( cc.StartInteraction(door, FullBodyBipedEffector.LeftHand) );
		}
		if ( GUILayout.Button("Close Facing Door - Right Hand") )
		{
			SetActionChar(chrPull, chrPush, false);
			StartCoroutine( cc.StartInteraction(door, FullBodyBipedEffector.RightHand) );
		}
	}
	#endregion
	
	#region Interfaces
	IEnumerator DoorInteraction(bool isPush)
	{
		Properties_door pd = door.GetComponent<Properties_door> ();
		if (randomExecution)
		{
			Vector3 posItrn;
			Vector3 lookAt;

			if (isPush)
			{
				posItrn = pd.posItrnPush;
				lookAt = pd.lookAtPush;
			}
			else
			{
				posItrn = pd.posItrnPull;
				lookAt = pd.lookAtPull;
			}

			yield return cc.walkOrRunTo(true, posItrn, lookAt);
		}
		yield return cc.DoorOpenLeft(door);
	}

	IEnumerator OpenDoorAndCloseImmediately(bool isPush)
	{
		yield return DoorInteraction(isPush);
		yield return cc.DoorCloseRightAfterOpening(door);
	}
	#endregion

	#region Helpers
	void SetActionChar(GameObject chr_main, GameObject chr_other, bool allowRandomPos = true)
	{
		chr_main.SetActive(true);
		chr_other.SetActive(false);

		cc = chr_main.GetComponent<CharacterControl> ();

		if (randomExecution && allowRandomPos)
		{
			const float RAND_CIRCLE_RAD = 3.0f;
			Vector2 v2RandPos = Random.insideUnitCircle * RAND_CIRCLE_RAD;
			chr_main.transform.position = new Vector3(v2RandPos.x, 0.0f, v2RandPos.y);
			chr_main.transform.rotation = Quaternion.Euler(0.0f, Random.value * 360.0f, 0.0f);
		}
	}
	#endregion




}
