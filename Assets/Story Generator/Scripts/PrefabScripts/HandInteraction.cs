using RootMotion.FinalIK;
using System;
using System.Collections;
using System.Collections.Generic;
using StoryGenerator.Helpers;
using StoryGenerator.SceneState;
using UnityEngine;
using UnityEngine.Video;

namespace StoryGenerator.CharInteraction
{
    public class HandInteraction : MonoBehaviour
    {
        [Tooltip("Can this object be picked up by a character?")]
        public bool allowPickUp = false;

        public HandPose grabHandPose;

        [Tooltip("Which GameObject should be picked up? If not set, this GameObject is used")]
        public Transform objectToBePickedUp = null;

        [Tooltip("Where should the hand be during grabbing animation? This is the position respect to ObjToBePickedUp (if null, respect to this GameObject)")]
        public Vector3 handPosition;

        [Tooltip("List of properties of switch that will initiate the visual change after character touches the switch")]
        public List<ActivationSwitch> switches;

        // Used to put back grabbed object. It is basically an interaction with invisible object.
        public GameObject invisibleCpy {get; private set;}

        // Used to determine if we should skip Awake function when we create copy of this GameObject
        static bool m_skipAwake = false;

        // Boolean used to differentiate original object and its invisible copy.
        bool isThisOriginal = false;


        public bool isPickedUp = false;

        // Whether the hand interaction is added when running the script
        public bool added_runtime = false;


        // Interaction Object component of the GameObject that will be picked up;
        InteractionObject m_io_grab;

        // The original parent transform. Used for resetting parent transform after putting back this GameObject
        public Transform m_tsfm_parent;

        // The transform of a character that will pick up this GameObject. It's used to genenerate textual GTs regarding which will new parent of this GameObject upon picked up.
        Transform m_tsfm_char;

        // Save which hand is used when m_io_grab is requested so that we know which field of State_char to update.
        FullBodyBipedEffector grabHand;

        // Used to identify which switch is activated after InteractionObject is returned to caller
        int m_activatedSwitchIdx;

        // List of switch position, in local coordinates. We save local because the throughout several interactions, their world
        // coordinate might change.
        List<Vector3> m_list_switchPos = null;

        // List of colliders on this GameObject. Will be disabled upon picked up and enabled when put back.
        Collider[] arry_cs = null;

        const string PATH_TO_PICKUP_IO_HOLDER = "Chars/HandPoses/Pickup_IO_holder";

        // Mapping from character name to HandPose Group ID
        static readonly Dictionary<string, int> CHAR_NAME_2_GID = new Dictionary<string, int> ()
        {
            {"Female1", 0},
            {"Male1", 0},
            {"Male1_invisible", 0},
            {"Male1_red", 0},
            {"Male1_blue", 0},
            {"Female2", 1},
            {"Female4", 1},
            {"Female4_red", 1},
            {"Female4_blue", 1},
            {"Male2", 1},
            {"Male6", 1},
            {"Male10", 1},
        };

        static readonly string[] HAND_POSE_PATHS = {"Chars/HandPoses/Male1/", "Chars/HandPoses/Mixamo/"};

        public enum HandPose
        {
            Button,
            GrabHorizontal,
            GrabHorizontalSmall,
            GrabVertical,
            GrabVerticalSmall,
            GrabVerticalFlat,
            GrabVerticalFlatSmall
        };

        public enum ActivationAction
        {
            SwitchOn,
            Open,
        };

        #region ClassDefinitions
        [System.Serializable]
        // Fields are public so that we can see what their values are on the inspector.
        public class ActivationSwitch
        {
            [Tooltip("Hand pose prefab that will be used to interact with this switch. Refer to Resources/Chars/HandPoses to visualize them")]
            public readonly HandPose handPose;

            [Tooltip("Type of action")]
            public readonly ActivationAction action;

            [Tooltip("Transform of GameObject that corresponds to the switch. The center of this GameObject would be used. If such thing does not exists, use SwitchPosition below")]
            public readonly Transform switchTransform;

            [Tooltip("Original position of that switch, instead of the current position. This is uses so that when we close a fridge, we do it from the same place wheer we opened it")]
            public readonly Vector3 originalPosition;


            [Tooltip("If SwitchTransform = null, this will work as the position of switch. Else, this will work as a offset from the center of the SwitchTransform.")]
            public readonly Vector3 switchPosition;

            [Tooltip("Array of TransitionSequence that will activated")]
            public readonly List<TransitionSequence> transitionSequences; // Can be null for phantom switches - switch that does not have any behavior.

            [Tooltip("List of shared transitions that are associated with another ActivationSwitch. The idea is to avoid duplicates")]
            public readonly SharedVisualChange[] sharedVisualChanges;

            [HideInInspector]
            public List<Dictionary<FullBodyBipedEffector, InteractionObject>> GID_2_effector_2_IO;

            HandInteraction hi;

            public State_object so;

            public ActivationSwitch(HandPose hp, ActivationAction aa, Vector3 swchPos, List<TransitionSequence> ts,
              Transform swchTsfm = null, SharedVisualChange[] svc = null)
            {
                handPose = hp;
                action = aa;
                if (swchTsfm != null)
                {
                    originalPosition = swchTsfm.TransformPoint(swchPos);
                }
                else
                {
                    originalPosition = Vector3.zero;
                }
                switchTransform = swchTsfm;
                switchPosition = swchPos;
                transitionSequences = ts;
                sharedVisualChanges = svc;
            }

            public ActivationSwitch(HandPose hp, ActivationAction aa, Vector3 swchPos,
              List<TransitionBase> tb, List<TransitionSequence.TransitionType> tt,
              Transform swchTsfm = null, SharedVisualChange[] svc = null)
            {
                handPose = hp;
                action = aa;
                if (swchTsfm != null)
                {
                    originalPosition = swchTsfm.TransformPoint(swchPos);
                }
                else
                {
                    originalPosition = Vector3.zero;
                }
                switchTransform = swchTsfm;
                switchPosition = swchPos;
                transitionSequences = new List<TransitionSequence>() { new TransitionSequence(tb, tt) };
                sharedVisualChanges = svc;
            }

            public void Initialize(HandInteraction _hi)
            {
                GID_2_effector_2_IO = new List<Dictionary<FullBodyBipedEffector, InteractionObject>> ();
                hi = _hi;
                so = _hi.GetComponent<State_object> ();
                if ( StateObjectCheck() )
                {
                    so.LinkObj2Monitor(this);
                }
            }

            public void Activate()
            {
                StartMonitoringState();
                if (transitionSequences != null)
                {
                    foreach (TransitionSequence ts in transitionSequences)
                    {
                        hi.StartCoroutine( ts.Start(hi) );
                    }
                }

                if (sharedVisualChanges != null)
                {
                    foreach (SharedVisualChange svc in sharedVisualChanges)
                    {
                        svc.Activate(hi);
                    }
                }
            }

            // Used for Flipping state of this switch
            // e.g.
            // 1. Turning off a light that should be turned on (set updateStateObject to false)
            // 2. Opening a drawer that should be closed (set updateStateObject to true).
            public void FlipInitialState()
            {
                UpdateStateObject();
                if (transitionSequences != null)
                {
                    foreach (TransitionSequence ts in transitionSequences)
                    {
                        ts.Flip();
                    }
                }
            }

            public void UpdateStateObject()
            {
                StateObjectCheck();
                so.ToggleState(this);
            }

            public void SetInstantTransition()
            {
                if (transitionSequences != null)
                {
                    foreach (TransitionSequence ts in transitionSequences)
                    {
                        ts.SetInstantTransition();
                    }
                }
            }

            void StartMonitoringState()
            {
                if ( StateObjectCheck() )
                {
                    hi.StartCoroutine( so.MonitorState(this) );
                }
            }

            // returns whether it's OK to call methods on state_object or not.
            bool StateObjectCheck()
            {
                if (so != null)
                {
                    return true;
                }
                else if (transitionSequences == null) // State_object can be null only if transitionSequences is null
                {
                    return false;
                }
                else
                {
                    Debug.LogError("Cannot find required State_object component.");
                    Debug.Break();
                }
                return false;
            }
        }

        public class TransitionSequence
        {
            public enum TransitionType
            {
                Auto,
                Manual
            }

            public List<TransitionBase> transitions {get; private set;}
            List<TransitionType> links;

            int idx = 0;

            public TransitionSequence(List<TransitionBase> _transitions, List<TransitionType> _links = null)
            {
                if (transitions != null)
                {
                    Debug.Assert(transitions.Count <= 2,
                    "Currently, TransitionSequence can handle 2 Transitions max. Using more Transitions might have undefined behavior");
                }
                transitions = _transitions;
                links = _links;
            }

            public IEnumerator Start(HandInteraction hi)
            {
                // Reset the index if we reached the end of the sequence.
                if (idx >= transitions.Count)
                {
                    idx = 0;
                }
                for (; idx < transitions.Count; idx++)
                {
                    TransitionBase tb = transitions[idx];
                    yield return tb.Transition(hi);
                    if (!tb.proceed)
                    {
                        yield break;
                    }

                    // If this is not the last transition (which has no link) and type is
                    // manual, break the loop so that it can continue later.
                    if (idx != transitions.Count - 1 && links[idx] == TransitionType.Manual)
                    {
                        idx++;
                        yield break;
                    }
                }
            }

            public void Flip()
            {
                // Since this method is only called before actual execution,
                // it'safe to assume idx is zero
                transitions[idx].Flip();
                idx++;
            }

            public void SetInstantTransition()
            {
                foreach (TransitionBase tb in transitions)
                {
                    tb.SetInstantTransition();
                }
            }

            // For auto transition, Monitoring has to be executed more than once to keep track
            // of the state without waiting for next "Start" coroutine to be executed
            public int NumAutoLinks()
            {
                int cntr = 0;
                if (links != null)
                {
                    foreach (TransitionType tt in links)
                    {
                        if (tt == TransitionType.Auto)
                        {
                            cntr++;
                        }
                    }
                }

                return cntr;
            }

            // Example usage: picking up toasted bread on a toaster
            public void RemoveTargets()
            {
                transitions.Clear();
                // Links can be null for single Transition
                if (links != null)
                {
                    links.Clear();
                }
            }
        }

        public class Toggle : TransitionBase
        {
            public Toggle(List<GameObject> _targets, float _delay = 0.0f, InterruptBehavior _ib_delay = InterruptBehavior.Ignore) :
              base(_targets, _delay, DURATION_INSTANT, _ib_delay, InterruptBehavior.Ignore) {}

            protected override void UpdatePropertyValue(float direction, float weight)
            {
                foreach (GameObject go in targets)
                {
                    bool curActiveVal = go.activeSelf;
                    go.SetActive(! curActiveVal);
                    VideoPlayer vp = go.GetComponent<VideoPlayer>();
                    if (vp != null)
                    {
                        if (curActiveVal)
                        {
                            vp.Stop();
                        }
                        else
                        {
                            vp.Play();
                        }
                    }
                }
            }
        }

        public class ToggleEmission : TransitionBase
        {
            static readonly string[] EMISSIVE_MATERIALS = {"_Light_bulb_", "_lamp_"};
            const string KEYWORD_EMISSION = "_EMISSION";

            // Cache materials
            List<Material> list_emissiveMats = null;

            public ToggleEmission(List<GameObject> _targets, float _delay = 0.0f, InterruptBehavior _ib_delay = InterruptBehavior.Ignore) :
              base(null, _delay, DURATION_INSTANT, _ib_delay, InterruptBehavior.Ignore)
            {
                list_emissiveMats = Helper.FindMatNameMatch(_targets, EMISSIVE_MATERIALS);
            }

            protected override void UpdatePropertyValue(float direction, float weight)
            {
                foreach (Material m in list_emissiveMats)
                {
                    // Alternative:
                    // https://forum.unity.com/threads/how-to-use-emissive-materials-with-global-illumination-parameter-set-to-realtime.473481/#post-3163087
                    // In a nutshell, instead of toggling Emission property, update emission color
                    bool curVal = m.IsKeywordEnabled(KEYWORD_EMISSION);
                    if (curVal)
                    {
                        m.DisableKeyword(KEYWORD_EMISSION);
                    }
                    else
                    {
                        m.EnableKeyword(KEYWORD_EMISSION);
                    }
                }
            }
        }

        public class ChangeVector : TransitionBase
        {
            public enum TargetProperty
            {
                Position,
                Scale,
                Rotation,
            };

            public readonly TargetProperty targetProperty;

            [Tooltip("This value would be added to starting value during transition.")]
            public readonly Vector3 delta;

            [Tooltip("Initial value and delta * roi_delta defines the min and the max value of RoI. This RoI is used to track the state.")]
            public readonly float roi_delta;


            [Tooltip("If useInitialValue is toggled, transition will start from this value.")]
            readonly Vector3 from;

            public ChangeVector(List<GameObject> _targets, float _delay, float _duration,
              TargetProperty tp, bool uiv, Vector3 _from, Vector3 _delta, float _roi_delta,
              InterruptBehavior _ib_delay = InterruptBehavior.Ignore,
              InterruptBehavior _ib_transition = InterruptBehavior.Ignore) :
              base(_targets, _delay, _duration, _ib_delay, _ib_transition, uiv)
            {
                targetProperty = tp;
                from = _from;
                delta = _delta;
                roi_delta = _roi_delta;
            }

            // This is used for MonitorVector3 Initialization (State_object.cs)
            // Although we have a list of GameObject to track, just return the first
            // one's property.
            public Vector3 GetInitialValue()
            {
                if (useInitialValue)
                {
                    return from;
                }
                if (targetProperty == TargetProperty.Position)
                {
                    return targets[0].transform.localPosition;
                }
                else if (targetProperty == TargetProperty.Scale)
                {
                    return targets[0].transform.localScale;
                }
                return targets[0].transform.localRotation.eulerAngles;
            }

            protected override void UseInitValue()
            {
                foreach (GameObject go in targets)
                {
                    Transform tsfm = go.transform;
                    if (targetProperty == TargetProperty.Position)
                    {
                        tsfm.localPosition = from;
                    }
                    else if (targetProperty == TargetProperty.Scale)
                    {
                        tsfm.localScale = from;
                    }
                    else
                    {
                        tsfm.localRotation = Quaternion.Euler(from);
                    }
                }
            }

            protected override void UpdatePropertyValue(float direction, float weight)
            {
                Vector3 value = Vector3.Lerp(Vector3.zero, delta, weight) * direction;
                foreach (GameObject go in targets)
                {
                    Transform tsfm = go.transform;
                    if (targetProperty == TargetProperty.Position)
                    {
                        tsfm.localPosition += value;
                    }
                    else if (targetProperty == TargetProperty.Scale)
                    {
                        tsfm.localScale += value;
                    }
                    else
                    {
                        tsfm.localRotation = Quaternion.Euler(tsfm.localRotation.eulerAngles + value);
                    }
                }
            }
        }

        public class ChangeColor : TransitionBase
        {
            // Cache materials
            List<Material> list_mats = new List<Material> ();

            // We are using Vector4 instead of color to support negative values
            // E.g. transition from red to green requires one field of delta to be negative.

            [Tooltip("If useInitialValue is toggled, transition will start from this value.")]
            readonly Vector4 from;

            [Tooltip("This value would be added to starting value during transition.")]
            readonly Vector4 delta;

            public ChangeColor(List<GameObject> _targets, float _delay, float _duration,
              bool uiv, Vector4 _from, Vector4 _delta,
              InterruptBehavior _ib_delay = InterruptBehavior.Ignore,
              InterruptBehavior _ib_transition = InterruptBehavior.Ignore) :
              base(null, _delay, _duration, _ib_delay, _ib_transition, uiv)
            {
                from = _from;
                delta = _delta;

                foreach (GameObject go in _targets)
                {
                    foreach ( Renderer rdr in go.GetComponentsInChildren<Renderer> () )
                    {
                        list_mats.AddRange(rdr.materials);
                    }
                }
            }

            protected override void UseInitValue()
            {
                foreach (Material m in list_mats)
                {
                    m.color = from;
                }
            }

            protected override void UpdatePropertyValue(float direction, float weight)
            {
                Vector4 value = Vector4.Lerp(Vector4.zero, delta, weight) * direction;
                foreach (Material m in list_mats)
                {
                    m.color = (Vector4) m.color + value;
                }
            }
        }

        public class ApplyTorque : TransitionBase
        {
            public readonly Vector3 roi_min;
            public readonly Vector3 roi_max;

            // Cache Rigidbodies
            List<Rigidbody> list_rb = new List<Rigidbody> ();
            readonly Vector3 dirVect;
            readonly AnimationCurve aniCurve;
            readonly float multiplier;
            // This is the duration of the aniCurve.
            readonly float curveDuration;

            // Ignore interrupts on ApplyTorque for now
            public ApplyTorque(List<GameObject> _targets, Vector3 _dirVect, float delay,
              float _physicsDuration, Keyframe[] kfs, float _multiplier, Vector3 _roi_min, Vector3 _roi_max) :
              base(_targets, delay, _physicsDuration, InterruptBehavior.Ignore, InterruptBehavior.Ignore)
            {
                if (duration < curveDuration)
                {
                    Debug.LogError("Duration must be greater or equal to the curveDuration (" + curveDuration + ")" );
                    Debug.Break();
                }

                foreach (GameObject go in _targets)
                {
                    Rigidbody rb = go.GetComponentInChildren<Rigidbody> ();
                    if (rb == null)
                    {
                        Debug.LogError("Cannot found RigidBody component on " + go.name);
                        Debug.Break();
                    }
                    list_rb.Add(rb);
                }
                dirVect = _dirVect;
                aniCurve = new AnimationCurve(kfs);
                aniCurve.postWrapMode = WrapMode.Once;
                multiplier = _multiplier;
                roi_min = _roi_min;
                roi_max = _roi_max;

                // Find out how long the animation curve lasts - the time value of the last keypoint
                curveDuration = aniCurve.keys[aniCurve.keys.Length - 1].time;
            }

            protected override void PreTransition()
            {
                foreach (Rigidbody rb in list_rb)
                {
                    rb.isKinematic = false;
                }
            }

            protected override void UpdatePropertyValue(float direction, float weight)
            {
                float actualTime = normalizedTime_elapsed * duration;
                if (actualTime <= curveDuration)
                {
                    float curTorque = aniCurve.Evaluate(actualTime) * multiplier;
                    foreach (Rigidbody rb in list_rb)
                    {
        			    rb.AddTorque(dirVect * curTorque);
                    }
                }
            }
        }

        public class TransitionBase
        {
            public enum InterruptBehavior
            {
                Stop,
                Ignore,
                Immediate, // Only applies to delay period
                Revert // Only applies to transition period
            }

            public bool proceed {get; private set;} = true;
            // For toggles, use very small duration so that the UpdatePropertyValue can be called at least once
            protected const float DURATION_INSTANT = 0.00001f;

            protected List<GameObject> targets;
            protected readonly InterruptBehavior ib_delay;
            protected readonly InterruptBehavior ib_transition;
            protected readonly bool useInitialValue; // Used for graudual changes (e.g. Vector3, Color)

            protected float normalizedTime_elapsed = 0.0f;
            protected float delay;
            protected float duration;

            bool interrupt = false;
            bool inTransition = false;
            bool isResume = false;

            public TransitionBase(List<GameObject> _targets, float _delay, float _duration,
              InterruptBehavior _ib_delay, InterruptBehavior _ib_transition, bool _useInitialValue = false)
            {
                // Impose Interrupt behavior limitations.
                Debug.Assert(_ib_delay != InterruptBehavior.Revert, "InterruptBehavior Revert is not supported during delay");
                Debug.Assert(_ib_transition != InterruptBehavior.Immediate, "InterruptBehavior Immediate is not supported during transiton");

                targets = _targets;
                delay = _delay;
                duration = _duration;
                ib_delay = _ib_delay;
                ib_transition = _ib_transition;
                useInitialValue = _useInitialValue;
            }

            public GameObject GetFirstTarget()
            {
                return targets[0];
            }

            public void Flip()
            {
                if (useInitialValue)
                {
                    UseInitValue();
                }
                UpdatePropertyValue(1.0f, 1.0f);
            }

            public void SetInstantTransition()
            {
                delay = 0.0f;
                duration = DURATION_INSTANT;
            }

            public IEnumerator Transition(HandInteraction hi)
            {
                // If there's another coroutine in execution, just raise interrupt flag from this
                // coroutine and exit
                if (inTransition)
                {
                    interrupt = true;
                    yield break;
                }

                inTransition = true;
                // if resuming from stopped execution, skip overwriting to initial value
                // and delay.
                if (!isResume)
                {
                    if (useInitialValue)
                    {
                        UseInitValue();
                    }
                    if (delay > 0.0f)
                    {
                        // Use loop instead of WaitForSeconds to allow interrupts
                        while ( ShouldContinue(ib_delay) )
                        {
                            // Use fixed update for more precise timing.
                            yield return new WaitForFixedUpdate();
                            normalizedTime_elapsed += Time.deltaTime / delay;
                        }
                        normalizedTime_elapsed = 0.0f;
                        // Handle interrupt if the loop is terminated due to interrupt.
                        if (interrupt)
                        {
                            Debug.Assert(ib_delay != InterruptBehavior.Ignore, "Unexpected interrupt behaviour during delay");
                            interrupt = false;
                            proceed = false;
                            if (ib_delay == InterruptBehavior.Stop)
                            {
                                inTransition = false;
                                yield break;
                            }
                        }
                    }
                }
                PreTransition();
                while ( ShouldContinue(ib_transition) )
                {
                    // Use fixed update for more precise timing. It also allows correct behaviour
                    // when force/torque is applied.
                    yield return new WaitForFixedUpdate();
                    OnUpdate(1.0f);
                }
                // Handle interrupt if the loop above is terminated due to interrupt
                if (interrupt)
                {
                    interrupt = false;
                    Debug.Assert(ib_transition != InterruptBehavior.Ignore, "Unexpected interrupt behaviour during transition");
                    if (ib_transition == InterruptBehavior.Stop)
                    {
                        inTransition = false;
                        proceed = false;
                        isResume = true;
                        yield break;
                    }
                    else if (ib_transition == InterruptBehavior.Revert)
                    {
                        proceed = false;
                        yield return Revert(hi, -1.0f);
                    }
                }
                PostTransition();
                normalizedTime_elapsed = 0.0f;
                inTransition = false;
                proceed = true;
                isResume = false;
            }

            protected virtual void UseInitValue() {}
            protected virtual void PreTransition() {}
            protected virtual void PostTransition() {}
            protected virtual void UpdatePropertyValue(float direction, float weight) {}

            bool ShouldContinue(InterruptBehavior ib)
            {
                return 0.0f <= normalizedTime_elapsed && normalizedTime_elapsed <= 1.0f &&
                  ( !interrupt || (interrupt && ib == InterruptBehavior.Ignore) );
            }

            void OnUpdate(float direction)
            {
                float normalizedTime_delta = Time.deltaTime / duration;
                normalizedTime_elapsed += normalizedTime_delta * direction;

                // Handle cases where normalizedTime_elapsed is out of boundary
                //
                // We shouldn't add more than maximum value to the inital property value. Adjusting "normalizedTime_delta"
                // will lose the true meaning of its variable name but just reusing the variable is fine.
                // Example sequence (in normalized time):
                // 1. delta time = 0.6, elapsed time = 0.6 --> add 0.6 x target value
                // 2. delta time = 0.6, elapsed time = 1.2 --> add 0.4 x target value (0.6 - (1.2 - 1) = 0.4)
                if (normalizedTime_elapsed > 1.0f)
                {
                    normalizedTime_delta -= (normalizedTime_elapsed - 1.0f);
                    normalizedTime_elapsed = 1.1f;
                }
                else if (normalizedTime_elapsed < 0.0f)
                {
                    normalizedTime_delta -= Mathf.Abs(normalizedTime_elapsed);
                    normalizedTime_elapsed = -0.1f;
                }
                UpdatePropertyValue(direction, normalizedTime_delta);
            }

            IEnumerator Revert(HandInteraction hi, float direction)
            {
                while ( ShouldContinue(ib_transition) )
                {
                    yield return new WaitForFixedUpdate();
                    OnUpdate(direction);
                }
                // Allow interrupt during reverting phase.
                if (interrupt)
                {
                    interrupt = false;
                    yield return Revert(hi, -direction);
                }
            }
        }

        [System.Serializable]
        public class SharedVisualChange
        {
            public enum VisualChangeTypes
            {
                Toggle,
                ToggleEmission,
                Vector,
                Color
            };

            [Tooltip("Index of ActivationSwitch that contains visual change in interest")]
            public int switchIdx;

            public VisualChangeTypes visualChangeTypes;

            [Tooltip("Index of the visual change")]
            public int index;

            public void Activate(HandInteraction hi)
            {
                ActivationSwitch s = hi.switches[switchIdx];
                hi.StartCoroutine( s.transitionSequences[index].Start(hi) );
            }

            public void FlipInitialState(HandInteraction hi)
            {
                ActivationSwitch s = hi.switches[switchIdx];
                s.transitionSequences[index].Flip();
            }
        }
        #endregion

        /// <summary>
        /// Returns List<Vector3> that contains the location of the switch in world coordinate. Can be used to decide which switch to
        /// use. E.g. choosing which drawer to open among this shelf.
        /// Note: the index of Vector3 can be used later to get InteractionObject corresponding to this swtich.
        /// </summary>
        public Vector3[] GetSwitchPosList()
        {
            if (m_list_switchPos == null)
            {
                return null;
            }
            int numOfSwitch = m_list_switchPos.Count;
            Vector3[] arry_worldPos = new Vector3[numOfSwitch];
            for (int i = 0; i < numOfSwitch; i++)
            {
                arry_worldPos[i] = transform.TransformPoint(m_list_switchPos[i]);
            }
            return arry_worldPos;
        }
        public void CreateInvisibleCpy(){
          invisibleCpy = Instantiate(gameObject) as GameObject;

          const int IDX_IO_WEIGHTCURVE_POSER_WEIGHT = 1;
          // Delete Mesh Renderer so that clone is not visible
          MeshRenderer[] arry_mr = invisibleCpy.GetComponentsInChildren<MeshRenderer> ();
          foreach (MeshRenderer mr in arry_mr)
          {
              Destroy(mr);
          }

          Collider[] cs = invisibleCpy.GetComponentsInChildren<Collider> ();
          foreach (Collider c in cs)
          {
              Destroy(c);
          }
          InteractionObject io = invisibleCpy.GetComponent<InteractionObject> ();
          
          // io.positionOffsetSpace = invisibleCpy.transform;
          
          io.events[0].pickUp = false; // We don't need pickup for clone
          // Adjust Poser weight curve so that hand poses as grabbing pose
          // and finishes as free hand.
          Keyframe kf_1 = new Keyframe(0.0f, 1.0f);
          Keyframe kf_2 = new Keyframe(0.5f, 1.0f);
          Keyframe kf_3 = new Keyframe(1.0f, 0.0f);
          io.weightCurves[IDX_IO_WEIGHTCURVE_POSER_WEIGHT].curve.MoveKey(0, kf_1);
          io.weightCurves[IDX_IO_WEIGHTCURVE_POSER_WEIGHT].curve.MoveKey(1, kf_2);
          io.weightCurves[IDX_IO_WEIGHTCURVE_POSER_WEIGHT].curve.AddKey(kf_3);

          // Interaction with the copy will trigger OnPickup of original
          io.events[0].messages[0].recipient = gameObject;
          //io.other_copy = gameObject;
          io.Initiate();

        }
        public InteractionObject Get_IO_grab(Transform charTsfm, FullBodyBipedEffector fbbe)
        {
            // Save character transform and hand to be used so that we can update SceneState
            // accordingly later.
            m_tsfm_char = charTsfm;
            grabHand = fbbe;

            Quaternion charRot = charTsfm.localRotation;
            float yComp;
            // This if statement is reached if original
            if (isThisOriginal)
            {
                // // On the first call, create the invisible copy. This will be reused through out the scene
                if (invisibleCpy == null)
                {

                    CreateInvisibleCpy();


                }

                //yComp = transform.localRotation.eulerAngles.y - charRot.eulerAngles.y;
                //invisibleCpy.transform.localRotation = Quaternion.Euler( new Vector3(0.0f, yComp, 0.0f) );
                return m_io_grab;
            }
            // Statements below are reached if invisible copy.
            //yComp = transform.localRotation.eulerAngles.y + charRot.eulerAngles.y;
            //transform.localRotation = Quaternion.Euler( new Vector3(0.0f, yComp, 0.0f) );
            return GetComponent<InteractionObject> ();
        }

        public InteractionObject Get_IO_interaction(int switchIdx, string charName, FullBodyBipedEffector effectorType)
        {
            if (m_list_switchPos == null)
            {
                return null;
            }
            m_activatedSwitchIdx = switchIdx;
            int gid = CHAR_NAME_2_GID[charName];
            return switches[switchIdx].GID_2_effector_2_IO[gid][effectorType];
        }

        public void SetInstantTransition()
        {
            if (switches != null)
            {
                foreach (ActivationSwitch sw in switches)
                {
                    sw.SetInstantTransition();
                }
            }
        }

        void Awake()
        {
            if (!m_skipAwake) {
                Initialize();
            }
        }

        public void Initialize()
        {
            const string FUNC_NAME_ON_PICKUP = "OnPickup";
            const string PREFIX_SWITCH_GAMEOBJECT = "Switch_";

            Vector3 adjustedScale = new Vector3(1 / transform.localScale.x, 1 / transform.localScale.y, 1 / transform.localScale.z);
            isThisOriginal = true;

            if (allowPickUp)
            {
                arry_cs = gameObject.GetComponentsInChildren<Collider> ();

                m_tsfm_parent = transform.parent;

                GameObject go_to_add_io_comp = gameObject;
                if (objectToBePickedUp != null)
                {
                    go_to_add_io_comp = objectToBePickedUp.gameObject;
                }

                Vector3 handPoseParentWorldPos;

                // Most of the cases, it's better to use the center of the GameObject
                if (handPosition == Vector3.zero)
                {
                    Renderer rdr = go_to_add_io_comp.GetComponent<Renderer> ();
                    if (rdr == null)
                    {
                        handPoseParentWorldPos = go_to_add_io_comp.transform.position;
                    }
                    else
                    {
                        handPoseParentWorldPos = rdr.bounds.center;
                    }
                }
                else
                {
                    handPoseParentWorldPos = go_to_add_io_comp.transform.TransformPoint(handPosition);
                }

                GameObject ioHolder = Resources.Load(PATH_TO_PICKUP_IO_HOLDER) as GameObject;
                InteractionObject io_orig = ioHolder.GetComponent<InteractionObject> ();
                m_io_grab = go_to_add_io_comp.AddComponent<InteractionObject> ();
                CopyIO(io_orig, m_io_grab);

                m_io_grab.events[0].messages[0].function = FUNC_NAME_ON_PICKUP;
                m_io_grab.events[0].messages[0].recipient = gameObject;

                for (int i = 0; i < HAND_POSE_PATHS.Length; i++)
                {
                    GameObject prefab_handPose_left = Resources.Load( HAND_POSE_PATHS[i] +
                        grabHandPose.ToString() + "Left") as GameObject;
                    GameObject prefab_handPose_right = Resources.Load( HAND_POSE_PATHS[i] +
                        grabHandPose.ToString() + "Right" ) as GameObject;
                    GameObject go_handPose_left = Instantiate(prefab_handPose_left, handPoseParentWorldPos,
                        Quaternion.identity, go_to_add_io_comp.transform) as GameObject;
                    GameObject go_handPose_right = Instantiate(prefab_handPose_right, handPoseParentWorldPos,
                        Quaternion.identity, go_to_add_io_comp.transform) as GameObject;
                    // Adjust local scale so that hand pose is correct regardless of this GO's scale.
                    go_handPose_left.transform.localScale = adjustedScale;
                    go_handPose_right.transform.localScale = adjustedScale;
                }
                m_io_grab.Initiate();
            }

            // Proceed only if this object is interact-able (has ActivationSwitch)
            if (switches != null && switches.Count != 0 && switches[0] != null)
            {
                m_list_switchPos = new List<Vector3> ();

                // Add hand prefab for each switch.
                // Note that InteractionObject is already attached to the prefab
                for (int i = 0; i < switches.Count; i++)
                {
                    ActivationSwitch s = switches[i];
                    s.Initialize(this);

                    Transform handPoseParent;
                    Vector3 handPoseParentWorldPos;
                    if (s.switchTransform != null)
                    {
                        handPoseParent = s.switchTransform;
                        handPoseParentWorldPos = s.switchTransform.TransformPoint(s.switchPosition);
                    }
                    else
                    {
                        handPoseParent = new GameObject(PREFIX_SWITCH_GAMEOBJECT + i).transform;
                        handPoseParent.SetParent(transform, false);
                        handPoseParent.localPosition = s.switchPosition;
                        handPoseParentWorldPos = handPoseParent.position;
                    }

                    m_list_switchPos.Add(handPoseParentWorldPos);

                    for (int j = 0; j < HAND_POSE_PATHS.Length; j++)
                    {
                        Dictionary<FullBodyBipedEffector, InteractionObject> effector2IO = new Dictionary<FullBodyBipedEffector, InteractionObject>();
                        s.GID_2_effector_2_IO.Add(effector2IO);
                        GameObject prefab_handPose_left = Resources.Load(HAND_POSE_PATHS[j] +
                            s.handPose.ToString() + "Left") as GameObject;
                        GameObject prefab_handPose_right = Resources.Load(HAND_POSE_PATHS[j] +
                            s.handPose.ToString() + "Right") as GameObject;
                        GameObject go_handPose_left = Instantiate(prefab_handPose_left, handPoseParentWorldPos,
                                                        Quaternion.identity, handPoseParent) as GameObject;
                        GameObject go_handPose_right = Instantiate(prefab_handPose_right, handPoseParentWorldPos,
                                                         Quaternion.identity, handPoseParent) as GameObject;
                        go_handPose_left.transform.localScale = adjustedScale;
                        go_handPose_right.transform.localScale = adjustedScale;
                        InteractionObject io_left = go_handPose_left.GetComponent<InteractionObject>();
                        InteractionObject io_right = go_handPose_right.GetComponent<InteractionObject>();
                        InteractionObject.Message msg_left = io_left.events[0].messages[0];
                        InteractionObject.Message msg_right = io_right.events[0].messages[0];
                        Debug.Assert(msg_left != null && msg_right != null, "Prefab Hand Poses must have at leat one event and at least one messags!");
                        msg_left.recipient = gameObject;
                        msg_right.recipient = gameObject;

                        effector2IO.Add(FullBodyBipedEffector.LeftHand, io_left);
                        effector2IO.Add(FullBodyBipedEffector.RightHand, io_right);
                    }
                }
            }
        }

        void CopyIO(InteractionObject io_orig, InteractionObject io_copy)
        {
            int len = io_orig.weightCurves.Length;
            io_copy.weightCurves = new InteractionObject.WeightCurve[len];
            for (int i = 0; i < len; i++)
            {
                InteractionObject.WeightCurve wc_orig = io_orig.weightCurves[i];
                InteractionObject.WeightCurve wc_new = new InteractionObject.WeightCurve();

                wc_new.type = wc_orig.type;
                wc_new.curve = new AnimationCurve();

                Keyframe[] wc_orig_kfs = wc_orig.curve.keys;
                for (int j = 0; j < wc_orig_kfs.Length; j++)
                {
                    wc_new.curve.AddKey(wc_orig_kfs[j]);
                }
                io_copy.weightCurves[i] = wc_new;
            }

            len = io_orig.multipliers.Length;
            io_copy.multipliers = new InteractionObject.Multiplier[len];
            for (int i = 0; i < len; i++)
            {
                InteractionObject.Multiplier mp_orig = io_orig.multipliers[i];
                InteractionObject.Multiplier mp_new = new InteractionObject.Multiplier();

                mp_new.curve = mp_orig.curve;
                mp_new.multiplier = mp_orig.multiplier;
                mp_new.result = mp_orig.result;

                io_copy.multipliers[i] = mp_new;
            }

            InteractionObject.InteractionEvent ie_orig = io_orig.events[0];
            io_copy.events = new InteractionObject.InteractionEvent[1];

            InteractionObject.InteractionEvent ie_new = new InteractionObject.InteractionEvent();
            ie_new.time = ie_orig.time;
            ie_new.pickUp = ie_orig.pickUp;
            ie_new.messages = new InteractionObject.Message[1];

            // No need to copy message content.
            ie_new.messages[0] = new InteractionObject.Message();

            io_copy.events[0] = ie_new;
            io_copy.events[0].animations = new InteractionObject.AnimatorEvent[0];
        }

        void Start()
        {
            // Start call comes after Awake and by this time, call necessary GameObjects are created so
            // we can skip Awake function for clones.
            m_skipAwake = true;
        }

        void ToggleCollider()
        {
            foreach(Collider c in arry_cs)
            {
                c.enabled = ! c.enabled;
            }
        }

        // This method is called by InteractionObject which handles pickup
        // It is also called during putback since InteractionObject of invisible copy calls
        // this method of the original (not its own OnPickup).
        void OnPickup()
        {
            ToggleCollider();
            Transform charTsfm = m_tsfm_char;
            string objType = "";
            // If this GameObject was previously picked up,
            // reaching here means the character is putting it back.
            if (isPickedUp)
            {
                // Set to null so that State_object will extract parent info instead.
                charTsfm = null;

                transform.parent = m_tsfm_parent;
                Transform tsfm = invisibleCpy.transform;
                transform.position = tsfm.position;
                transform.localRotation = tsfm.localRotation;
            }

            // Update SceneStates if necessary
            State_object so = GetComponent<State_object> ();
            if (so != null)
            {
                so.UpdateParentInfo(charTsfm);
                // If charater is putting it back, we should put empty string instead.
                if (! isPickedUp)
                {
                    objType = so.GetObjType();
                }
            }

            State_char sc = m_tsfm_char.GetComponent<State_char> ();
            if (sc != null)
            {
                if (grabHand == FullBodyBipedEffector.LeftHand)
                {
                    sc.UpdateHandLeft(objType);
                }
                else if (grabHand == FullBodyBipedEffector.RightHand)
                {
                    sc.UpdateHandRight(objType);
                }
            }
            isPickedUp = ! isPickedUp;
        }

        // This method is called by InteractionObject which handles interaction (Resources/Chars/HandPoses/Male1/)
        void ActivateObject()
        {
            switches[m_activatedSwitchIdx].Activate();
        }

        internal int SwitchIndex(ActivationAction action)
        {
            if (switches != null) {
                for (int i = 0; i < switches.Count; i++)
                    if (switches[i].action == action)
                        return i;
            }
            return -1;
        }

        // Get all the swi of corresponding action
        internal List<int> SwitchIndices(ActivationAction action)
        {
            List<int> switchIndices = new List<int>();

            if (switches != null)
            {
                for (int i = 0; i < switches.Count; i++)
                    if (switches[i].action == action)
                        switchIndices.Add(i);
            }

            return switchIndices;
        }
        // Draws switch Gizmo (purple sphere) that activates the visual effect
        void OnDrawGizmosSelected()
        {
            if (this.enabled)
            {
                if (allowPickUp && handPosition != Vector3.zero)
                {
                    Transform tsfm = transform;
                    if (objectToBePickedUp != null)
                    {
                        tsfm = objectToBePickedUp;
                    }
                    Vector3 worldPos = tsfm.TransformPoint(handPosition);
                    DrawSphere( worldPos, tsfm, new Color(0.0f, 1.0f, 0.0f, 0.85f) );
                }

                if (switches != null && switches.Count > 0 && switches[0] != null)
                {
                    foreach (ActivationSwitch curAs in switches)
                    {
                        Vector3 worldPos;
                        if (curAs.switchTransform == null)
                        {
                            worldPos = transform.TransformPoint(curAs.switchPosition);
                        }
                        else
                        {
                            worldPos = curAs.switchTransform.TransformPoint(curAs.switchPosition);
                        }
                        DrawSphere( worldPos, transform, new Color(0.6f, 0.0f, 0.9f, 0.85f) );
                    }
                }
            }
        }

        void DrawSphere(Vector3 worldPos, Transform tsfm, Color c)
        {
            const float GIZMO_SPHERE_RAD = 0.025f;

            Matrix4x4 cubeTransform = Matrix4x4.TRS(worldPos, tsfm.rotation, tsfm.localScale);
            Matrix4x4 origGizmosMatrix = Gizmos.matrix;
            Gizmos.matrix *= cubeTransform;
            Gizmos.color = c;
            Gizmos.DrawSphere(Vector3.zero, GIZMO_SPHERE_RAD);
            Gizmos.matrix = origGizmosMatrix;
        }

        // Reset static variable
        void OnDestroy()
        {
            if (invisibleCpy != null)
            {
                Destroy(invisibleCpy);
            }
            Destroy(m_io_grab);
            m_skipAwake = false;
        }
    }
}
