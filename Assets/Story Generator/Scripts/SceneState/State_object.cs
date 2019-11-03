using StoryGenerator.DoorProperties;
using StoryGenerator.CharInteraction;
using StoryGenerator.Helpers;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace StoryGenerator.SceneState
{
	using Toggle           = StoryGenerator.CharInteraction.HandInteraction.Toggle;
	using ChangeVector     = StoryGenerator.CharInteraction.HandInteraction.ChangeVector;
	using ApplyTorque      = StoryGenerator.CharInteraction.HandInteraction.ApplyTorque;
	using TransitionBase   = StoryGenerator.CharInteraction.HandInteraction.TransitionBase;
	using ActivationSwitch = StoryGenerator.CharInteraction.HandInteraction.ActivationSwitch;
	using TargetProperty   = StoryGenerator.CharInteraction.HandInteraction.ChangeVector.TargetProperty;

	public class State_object : MonoBehaviour
	{
		public class MonitorInfo
		{
			public readonly int iteration;
			public readonly StateType stateType;
			public readonly MonitorBase monitor;

			public MonitorInfo(int _iteration, StateType _st, MonitorBase _mb)
			{
				iteration = _iteration;
				stateType = _st;
				monitor = _mb;
			}
		}

		private class MonitorVector3 : MonitorBase
		{
			public enum Coordinate2Monitor
			{
				World,
				Local
			}

			readonly Vector3 origin;
			readonly Vector3 unitDir;
			readonly float magnitude;
			readonly TargetProperty property2Monitor;
			readonly Coordinate2Monitor coordinate2Monitor;

			public MonitorVector3(GameObject _go, Vector3 min, Vector3 max, TargetProperty p2m,
			  Coordinate2Monitor c2m) : base(_go)
			{
				origin = min;
				Vector3 dirVect = (max - min);
				unitDir = (max - min).normalized;
				magnitude = dirVect.magnitude;
				property2Monitor = p2m;
				coordinate2Monitor = c2m;
			}

			public MonitorVector3(ChangeVector cv) : base( cv.GetFirstTarget() )
			{
				origin = cv.GetInitialValue();

				Vector3 adjustedDelta = cv.delta * cv.roi_delta;
				unitDir = adjustedDelta.normalized;
				magnitude = adjustedDelta.magnitude;

				property2Monitor = cv.targetProperty;
				// CHangeVector always changes local coordinates, not the world.
				coordinate2Monitor = Coordinate2Monitor.Local;
			}
			
			protected override bool GetCurrentState()
			{
				Vector3 val = GetProperty();
				const float dirTolerance = 0.98f;

				Vector3 diff = val - origin;
				// 1. check if direction is aligned
				if ( Vector3.Dot(diff.normalized, unitDir) > dirTolerance)
				{
					// 2. Check manitude
					if ( Vector3.Dot(diff, unitDir) > magnitude )
					{
						return true;
					}
				}
				return false;
			}

			private Vector3 GetProperty()
			{
				Transform tsfm = go.transform;
				Vector3 retVal = Vector3.zero;
				switch (property2Monitor)
				{
					case TargetProperty.Position:
						if (coordinate2Monitor == Coordinate2Monitor.World)
						{
							retVal = tsfm.position;
						}
						else
						{
							retVal = tsfm.localPosition;
						}
						break;
					case TargetProperty.Rotation:
						if (coordinate2Monitor == Coordinate2Monitor.World)
						{
							retVal = tsfm.rotation.eulerAngles;
						}
						else
						{
							retVal = tsfm.localRotation.eulerAngles;
						}
						// Our annotation assumes that angles are between -180 and 180 degree.
						Helper.AdjustRotationVector(ref retVal);
						break;
					case TargetProperty.Scale:
						if (coordinate2Monitor == Coordinate2Monitor.World)
						{
							retVal = tsfm.lossyScale;
						}
						else
						{
							retVal = tsfm.localScale;
						}
						break;
					default:
						Debug.LogError("Unknown Property");
						Debug.Break();
						break;
				}
				return retVal;
			}
		}

		public class MonitorActive : MonitorBase
		{
			public MonitorActive(Toggle tgl) : base( tgl.GetFirstTarget() ) {}

			protected override bool GetCurrentState()
			{
				return go.activeSelf;
			}

		}

		public class MonitorBase
		{
			// Used to prevent spawning another coroutine for interrupt
			// if it's already being monitored.
			public bool isMonitoring { get; private set; } = false;

			readonly protected GameObject go;
			
			// Saves initial state. True can also be used to monitor Vector3 value:
			// True: inside the region of interest (roi), False: outside the roi.
			bool initialState;

			public MonitorBase(GameObject _go)
			{
				go = _go;
			}

			public IEnumerator Start()
			{
				isMonitoring = true;
				initialState = GetCurrentState();
				do
				{
					yield return null;
				}
				while ( initialState == GetCurrentState() );
				isMonitoring = false;
			}

			protected virtual bool GetCurrentState() {return true;}
		}

		public enum StateType
		{
			Open,
			Power
		}

		public ObjectState objState; // Set to public so that we can the status in the inspector
		SceneStateSequence sceneStateSequence = null;

		Dictionary<object, MonitorInfo> monitorDict;

		public void Initialize(bool isOpen = false, bool isOn = false)
		{
			objState = new ObjectState(gameObject, isOpen, isOn);
			monitorDict = new Dictionary<object, MonitorInfo> ();
		}

		public void Prepare(SceneStateSequence sss)
		{
			sceneStateSequence = sss;
			UpdateParentInfo(null);
		}
		
		public string GetObjType()
		{
			return objState.type;
		}

		public MonitorInfo LinkObj2Monitor(ActivationSwitch actSwch)
		{
			// Don't need to overwrite the entry if already exists
			if (! monitorDict.ContainsKey(actSwch) )
			{
				int itrn = 1;
				StateType st = StateType.Open;
				if (actSwch.action == HandInteraction.ActivationAction.SwitchOn)
				{
					st = StateType.Power;
				}

				MonitorBase mb = null;
				// TransitionSequences can be null for switches that has immediate transition
				// that doesn't need monitoring (e.g. phantom switch on a microwave)
				if (actSwch.transitionSequences != null)
				{
					itrn += actSwch.transitionSequences[0].NumAutoLinks();

					// First TransitionSequence is the main
					TransitionBase tb = actSwch.transitionSequences[0].transitions[0];
					bool isTypeFound = false;
					Toggle tgl = tb as Toggle;
					if (tgl != null)
					{
						mb = new MonitorActive(tgl);
						isTypeFound = true;
					}
					if (!isTypeFound)
					{
						ChangeVector cv = tb as ChangeVector;
						// roi_delta value of zero means immediate transition (e.g. using coffee machine)
						if (cv != null && cv.roi_delta != 0.0f)
						{
							mb = new MonitorVector3(cv);
							isTypeFound = true;
						}
					}
					if (!isTypeFound)
					{
						ApplyTorque at = tb as ApplyTorque;
						if (at != null)
						{
							mb = new MonitorVector3(at.GetFirstTarget(), at.roi_min, at.roi_max, TargetProperty.Rotation,
							MonitorVector3.Coordinate2Monitor.World);
						}
					}
					// Other types are considered as immediate transition.
				}
				MonitorInfo mi = new MonitorInfo(itrn, st, mb);
				monitorDict[actSwch] = mi;

				return mi;
			}

			return null;
		}

		public MonitorInfo LinkObj2Monitor(Properties_door pd, Vector3 roi_min, Vector3 roi_max)
		{
			StateType st = StateType.Open;
			MonitorVector3 mv3 = new MonitorVector3(pd.gameObject, roi_min, roi_max,
			  TargetProperty.Rotation, MonitorVector3.Coordinate2Monitor.World);
			
			MonitorInfo mi = new MonitorInfo(1, st, mv3);
			monitorDict[pd] = mi;
			return mi;
		}

		public void LinkObj2Monitor(object obj, MonitorInfo mi)
		{
			Debug.Assert(! monitorDict.ContainsKey(obj), "Monitor dictionary already has this key.");
			monitorDict[obj] = mi;
		}

		public void ToggleState(object obj)
		{
			try
			{
				ToggleState(monitorDict[obj].stateType);
			}
			catch (System.Exception e)
			{
				Debug.LogError("Error occured during lookup: " + e);
			}
		}		

		public IEnumerator MonitorState(object obj)
		{
			Debug.Assert(monitorDict.ContainsKey(obj), "Monitor dictionary doesn't have given key" );
			
			// SceneStateSequence can be null if Prepare method is not called,
			// which means we are not generating SceneState.
			if (sceneStateSequence != null)
			{
				MonitorInfo mi = monitorDict[obj];
				StateType st = mi.stateType;
				MonitorBase mb = mi.monitor;

				// Monitorbase value of null means it's immediate transition.
				// Proceed only if it's not already being monitored. This prevents
				// interrupt from HandInteraction spawning another coroutine.
				if (mb != null && ! mb.isMonitoring)
				{
					// Need to iterate more than once if necessary (e.g. toaster - auto transition from switch on to off)
					for (int i = 0; i < mi.iteration; i++)
					{
						yield return StartCoroutine( mb.Start() );
						ToggleState(st);
						sceneStateSequence.AddState(objState);
					}
				}
			}
		}

		public void UpdateParentInfo(Transform charTsfm)
		{
			Debug.Assert(sceneStateSequence != null, "SceneStateSequence is null on " + name);
			
			// If char tranform is given, set this as parent (no need to extract parent info).
			if (charTsfm != null)
			{
				objState.SetParent_inside(charTsfm);
			}
			else
			{
				objState.ExtractParentInfo(gameObject);
			}
			sceneStateSequence.AddState(objState);
		}

		void ToggleState(StateType st)
		{
			switch (st)
			{
				case StateType.Open:
					objState.open = ! objState.open;
					break;
				
				case StateType.Power:
					objState.power = ! objState.power;
					break;
				default:
					Debug.LogError("Unknown StateType on " + name);
					break;
			}
		}
	}

}