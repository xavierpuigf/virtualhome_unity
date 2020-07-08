using System;
using System.Collections;
using System.Collections.Generic;
using StoryGenerator.CharInteraction;
using StoryGenerator.Utilities;
using UnityEngine.AI;
using StoryGenerator.Helpers;
using StoryGenerator.SceneState;
using UnityEngine;

namespace StoryGenerator.DoorProperties
{
	public class Properties_door : MonoBehaviour
	{
		[System.Serializable]
		public class ForceCurve
		{
			public AnimationCurve forceCurve;
			public float multiplier;

			public ForceCurve(Keyframe[] kfs, float mult)
			{
				forceCurve = new AnimationCurve(kfs);
				forceCurve.postWrapMode = WrapMode.Once;
				multiplier = mult;
			}

			public float GetTorque(float t)
			{
				return forceCurve.Evaluate(t) * multiplier;
			}
		}

		public ForceCurve push;
		public ForceCurve pull;
		public ForceCurve closeBehind;
		public ForceCurve closeFront;

		public Transform transformPush;
		public Transform transformPull;
		public Transform transformIKClose;

		public float yRotClose;

        public Vector3 posItrnPush;
        public Vector3 posItrnPull;
        public Vector3 lookAtPush;
        public Vector3 lookAtPull;
		public Vector2 doorwayA;
        public Vector2 doorwayB;

        public OffMeshLink offMeshLink;

		Quaternion qtrn_close = Quaternion.identity;
        Quaternion qtrn_orig;
        State_object stateObject;
		
		// Must not be used after scene is started! 
		public void SetDoorToClose()
		{
			transform.rotation = qtrn_close;
			stateObject.ToggleState(this);
		}

        public void SetDoorToOpen()
        {
            transform.rotation = qtrn_orig;
            stateObject.ToggleState(this);
        }

        public bool ShouldPush(Vector3 pos)
		{
			if ( (posItrnPush - pos).sqrMagnitude <
              (posItrnPull - pos).sqrMagnitude )
			{
				return true;
			}
			return false;
		}

		public void StartMonitoringState()
		{
			Debug.Assert(stateObject != null, "State_object is null on " + name);
			StartCoroutine( stateObject.MonitorState(this) );
		}

		void Awake()
		{
			stateObject = gameObject.AddComponent<State_object> ();
            stateObject.Initialize(true);
			
			qtrn_orig = transform.rotation;

			FindDoorjamb();
			AdjustNavMeshObstacle();
			AddRigidBody();
			SetIktargets();
			SetForceCurves();
			AddHingeJoint();

			transform.rotation = qtrn_orig;
		}

		void FindDoorjamb()
		{
			const float SPHERE_RAD = 1.0f;
			const string NAME_DOORJAMB = "_Doorjamb_";
			const int IDX_WALL_LAYER = 10;
			const float EDGE_OF_DOOR_Z = 0.75f;
			const float DOOR_SIZE_Y = 1.02f;
			const float DOOR_SIZE_Z = 0.8804308f;
			const float DOOR_STOPPER_SIZE_X = 0.1f;
			const float DOOR_STOPPER_SIZE_Y = 0.05f;
			const float DOOR_STOPPER_SIZE_Z = 0.05f;

			Collider[] arry_c = Physics.OverlapSphere(transform.position, SPHERE_RAD);
			Transform tsfm_doorjamb = null;
			foreach (Collider c in arry_c)
			{
				Transform tsfm = c.transform.parent.parent;
				if ( tsfm.name.Contains(NAME_DOORJAMB) )
				{
					tsfm_doorjamb = tsfm;
					break;
				}
			}

			Debug.Assert(tsfm_doorjamb != null, "Can't find matching doorjamb.");

			// Move collider layer to "wall" to enable rotation of door by using torque.
			// Doorjamb only has one child that contains all colliders.
			Transform colliderParent =  tsfm_doorjamb.GetChild(0);
			for (int i = 0; i < colliderParent.childCount; i++)
			{
				Transform tsfm = colliderParent.GetChild(i);
				tsfm.gameObject.layer = IDX_WALL_LAYER;
			}

			float yRot1 = tsfm_doorjamb.rotation.eulerAngles.y;
			float yRot2 = yRot1 - 180.0f;

			transform.rotation = Quaternion.Euler(0.0f, yRot1, 0.0f);
			float dist1 = (transform.TransformPoint(0.0f, 0.0f, EDGE_OF_DOOR_Z) - tsfm_doorjamb.position).magnitude;

			transform.rotation = Quaternion.Euler(0.0f, yRot2, 0.0f);
			float dist2 = (transform.TransformPoint(0.0f, 0.0f, EDGE_OF_DOOR_Z) - tsfm_doorjamb.position).magnitude;

			yRotClose = dist1 < dist2 ? yRot1 : yRot2;
			qtrn_close = Quaternion.Euler(0.0f, yRotClose, 0.0f);
            SetDoorwayExtents(tsfm_doorjamb.gameObject);
            AddOffMeshLinks(tsfm_doorjamb.gameObject);

			// Instead of using HingeJoint's Limit use box collider to stop door's movement.
			// We can use OnCollisionEnter method to trigger actions when door is closed.
			// Close the door temporarily so that we can calculate door stopper's position.
			transform.rotation = qtrn_close;
			Transform tsfm_stopper = new GameObject( "DoorStopper", typeof(BoxCollider), typeof(DoorStopper) ).transform;
			tsfm_stopper.SetParent(colliderParent);
			tsfm_stopper.localRotation = Quaternion.identity;

			tsfm_stopper.position = transform.TransformPoint(-0.09343f - DOOR_STOPPER_SIZE_X / 2.0f,
			  DOOR_SIZE_Y - DOOR_STOPPER_SIZE_Y / 2.0f, DOOR_SIZE_Z - DOOR_STOPPER_SIZE_Z);
			BoxCollider bc = tsfm_stopper.GetComponent<BoxCollider> ();
			bc.size = new Vector3(DOOR_STOPPER_SIZE_X, DOOR_STOPPER_SIZE_Y, DOOR_STOPPER_SIZE_Z);
		}

        void SetDoorwayExtents(GameObject doorJamb)
        {
            Bounds bounds = GameObjectUtils.GetBounds(doorJamb);
            Vector3 min = bounds.min;
            Vector3 max = bounds.max;

            if (max.x - min.x > max.z - min.z) {
                doorwayA = new Vector2(min.x, bounds.center.z);
                doorwayB = new Vector2(max.x, bounds.center.z);
            } else {
                doorwayA = new Vector2(bounds.center.x, min.z);
                doorwayB = new Vector2(bounds.center.x, max.z);
            }
        }

        void AddOffMeshLinks(GameObject doorJamb)
        {
            Vector2 doorWay = doorwayB - doorwayA;
            Vector3 doorWayCenter = DoorControl.ToXZ((doorwayA + doorwayB)/2.0f);
            Vector3 dir = new Vector3(doorWay.y, 0, -doorWay.x).normalized;

            GameObject cylA = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            GameObject cylB = GameObject.CreatePrimitive(PrimitiveType.Cylinder);

            cylA.transform.localScale = new Vector3(0.2f, 0.1f, 0.2f);
            cylB.transform.localScale = new Vector3(0.2f, 0.1f, 0.2f);
            cylA.transform.parent = doorJamb.transform;
            cylB.transform.parent = doorJamb.transform;
            cylA.transform.position = doorWayCenter + dir * 0.5f;
            cylB.transform.position = doorWayCenter - dir * 0.5f;
            cylA.SetActive(false);
            cylB.SetActive(false);

            offMeshLink = doorJamb.AddComponent<OffMeshLink>();

            offMeshLink.startTransform = cylA.transform;
            offMeshLink.endTransform = cylB.transform;
            offMeshLink.costOverride = 2.0f;
            offMeshLink.activated = false;
        }

		void AdjustNavMeshObstacle()
		{
			// Lower the threshold so that the carving can take place sooner
			// Without this adjustment, the character can't navigate through the
			// opened door.
			NavMeshObstacle[] nmos = GetComponentsInChildren<NavMeshObstacle> ();
			foreach (NavMeshObstacle nmo in nmos)
			{
				nmo.carvingMoveThreshold = 0.08f;
			}
		}

        void SetIktargets()
		{
			const string NAME_PREFIX_GROUP = "HandleGroup_";
			const string NAME_IK_TARGET_HAND_CLOSE = "IkTarget_hand_close";

			transformPush = new GameObject(NAME_PREFIX_GROUP + "push",
			  typeof(DoorHandleGroup)).transform;
			transformPull = new GameObject(NAME_PREFIX_GROUP + "pull",
			  typeof(DoorHandleGroup)).transform;

			transformPush.SetParent(transform, false);
			transformPull.SetParent(transform, false);

			transformPush.localPosition = new Vector3(-0.192f, 0.1521f, 0.7491f);
			transformPull.localPosition = new Vector3(0.124f, 0.1521f, 0.7491f);

			DoorHandleGroup dhgPush = transformPush.GetComponent<DoorHandleGroup> ();
			DoorHandleGroup dhgPull = transformPull.GetComponent<DoorHandleGroup> ();
			
			// IK targets, bend goal and interaction positions are slightly differeent
			// for each action, due to different animation.
			dhgPush.AdjustForPush();
			dhgPull.AdjustForPull();

			posItrnPush = dhgPush.GetInteractionPos();
			posItrnPull = dhgPull.GetInteractionPos();
			lookAtPush = dhgPush.GetLookAt_body();
			lookAtPull = dhgPull.GetLookAt_body();

			// IK target for close is special. All we need is IK target for the hand.
			transformIKClose = new GameObject(NAME_IK_TARGET_HAND_CLOSE).transform;
			transformIKClose.SetParent(transform, false);
			transformIKClose.localPosition = new Vector3(-0.035f, 0.077f, 0.893f);

			Vector3 closeRot = qtrn_close.eulerAngles;
			Helper.AdjustRotationVector(ref closeRot);
			Vector3 openRot = closeRot + new Vector3(0.0f, 5.0f, 0.0f);

			HandInteraction.ApplyTorque appTq = new HandInteraction.ApplyTorque(
			  new List<GameObject> () {gameObject}, Vector3.up, 0.0f, 3.0f, CurvePreset(0), -1.8f,
			  closeRot, openRot);

			List<HandInteraction.TransitionSequence> list_ts = new List<HandInteraction.TransitionSequence> ()
			{
				new HandInteraction.TransitionSequence( new List<HandInteraction.TransitionBase> () {appTq} )
			};

			HandInteraction.ActivationSwitch doorSwitch = new HandInteraction.ActivationSwitch(
			  HandInteraction.HandPose.GrabVertical, HandInteraction.ActivationAction.Open,
			  Vector3.zero, list_ts, transformIKClose);

			// Get the Monitor info from this door switch so that it can be re-used.
			State_object.MonitorInfo monitorInfo = stateObject.LinkObj2Monitor(doorSwitch);
			stateObject.LinkObj2Monitor(this, monitorInfo);
			
			HandInteraction hi = gameObject.AddComponent<HandInteraction>();
			hi.switches = new List<HandInteraction.ActivationSwitch>();
			hi.switches.Add(doorSwitch);
			hi.Initialize();
		}

		void SetForceCurves()
		{
			Keyframe[] kfsType0 = CurvePreset(0);
			push = new ForceCurve(kfsType0, 1.4f);
			closeFront = new ForceCurve(kfsType0, -1.3f);
			pull = new ForceCurve(CurvePreset(1), 1.0f);
			closeBehind = new ForceCurve(CurvePreset(2), -5.0f);
		}

		Keyframe[] CurvePreset(int id)
		{
			Keyframe[] kfs = new Keyframe[5];
			if (id == 0)
			{
				kfs[0] = new Keyframe(0.0f, 0.0f);
				kfs[1] = new Keyframe(0.105f, 0.894f);
				kfs[1].inTangent = kfs[1].outTangent = 6.827997f;
				kfs[2] = new Keyframe(0.274f, 1.443f);
				kfs[3] = new Keyframe(0.392f, 0.983f);
				kfs[3].inTangent = kfs[3].outTangent = -4.844041f;
				kfs[4] = new Keyframe(1.0f, 0.0f);
			}
			else if (id == 1)
			{
				kfs = new Keyframe[5];
				kfs[0] = new Keyframe(0.0f, 0.0f);
				kfs[1] = new Keyframe(0.2f, 0.6f);
				kfs[1].inTangent = kfs[1].outTangent = 2.166667f;
				kfs[2] = new Keyframe(0.5f, 1.0f);
				kfs[3] = new Keyframe(0.8f, 0.6f);
				kfs[3].inTangent = kfs[3].outTangent = -2.166667f;
				kfs[4] = new Keyframe(1.0f, 0.0f);
			}
			else if (id == 2)
			{
				kfs = new Keyframe[5];
				kfs[0] = new Keyframe(0.0f, 0.0f);
				kfs[1] = new Keyframe(0.0425f, 1.642f);
				kfs[2] = new Keyframe(0.0736f, 0.0f);
				kfs[3] = new Keyframe(0.1099f, 1.0f);
				kfs[4] = new Keyframe(0.3f, 0.0f);
			}
			else
			{
				Debug.LogError("Unknown curve preset id");
				Debug.Break();
			}

			return kfs;
		}

		void AddRigidBody()
		{
			Rigidbody rb = gameObject.AddComponent<Rigidbody> ();
			rb.useGravity = false;
			// This must be set to true to prevent interference with NavMeshAgent behaviour.
			// It will be enabled back before actual door interaction.
			rb.isKinematic = true;
			rb.drag = 1.5f;
			rb.angularDrag = 1.0f;
		}

		void AddHingeJoint()
		{
			HingeJoint hj = gameObject.AddComponent<HingeJoint> ();
			hj.anchor = Vector3.zero;
			hj.axis = Vector3.up;
		}
	}
}