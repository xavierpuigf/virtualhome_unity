using RootMotion.FinalIK;
using StoryGenerator.CharInteraction;
using StoryGenerator.ChairProperties;
using StoryGenerator.DoorProperties;
using StoryGenerator.Helpers;
using StoryGenerator.Recording;
using StoryGenerator.SceneState;
using StoryGenerator.Utilities;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using StoryGenerator.Scripts;

namespace StoryGenerator
{

    // Some logics are from ThirdPersonCharacter.cs script
    [RequireComponent(typeof(Animator))]
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof (NavMeshAgent))]
    [RequireComponent(typeof (InteractionSystem))]
    [RequireComponent(typeof (FullBodyBipedIK))]
    public class CharacterControl : MonoBehaviour
    {
        public class IkTargets
        {
            // Class that contains coefficients to calculate local position of
            // IK tareget parent transform. Used for sitting and door interaction.
            class CharCoeffs
            {
                public Quaternion qtrn_ikTarget_doorHandle_push;
                public Quaternion qtrn_ikTarget_doorHandle_pull;

                float y_a;
                float y_c;
                float z_min_a;
                float z_min_xOffset;
                float z_min_c;
                
                float z_max_a;
                float z_max_c;
                
                public CharCoeffs(float _y_a, float _y_b,
                  float _z_min_a, float _z_min_xOffset, float _z_min_c,
                  float _z_max_a, float _z_max_c,
                  Vector3 rotPush, Vector3 rotPull)
                {
                    y_a = _y_a;
                    y_c = _y_b;
                    z_min_a = _z_min_a;
                    z_min_xOffset = _z_min_xOffset;
                    z_min_c = _z_min_c;
                    z_max_a = _z_max_a;
                    z_max_c = _z_max_c;

                    qtrn_ikTarget_doorHandle_push = Quaternion.Euler(rotPush);
                    qtrn_ikTarget_doorHandle_pull = Quaternion.Euler(rotPull);
                }

                public Vector3 CalcuateIkParentPos(float sitHeight, bool isRandomZPos)
                {
                    float y = y_a * sitHeight + y_c;

                    float x_z_translated = (sitHeight + z_min_xOffset);
                    float zMin = z_min_a * x_z_translated * x_z_translated + z_min_c;
                    float zMax = z_max_a * sitHeight + z_max_c;

                    float z;
                    if (isRandomZPos)
                    {
                        z = Random.Range(zMin, zMax);
                    }
                    else
                    {
                        z = zMin + (zMax - zMin) / 2.0f;
                    }

                    return new Vector3(0.0f, y, z);
                }
            }

            static readonly Dictionary<string, CharCoeffs> charName2Coeffs =
              new Dictionary<string, CharCoeffs> ()
              {
                  {"Female1", new CharCoeffs(1.1088f, -0.94116f, 0.75f, 0.01f, -0.5f, 0.4512f, -0.26784f,
                    new Vector3(0.0f, 0.0f, 180.0f), new Vector3(0.0f, 180.0f, 180.0f) )},
                  {"Female2", new CharCoeffs(1.1872f, -0.83104f, 1.45f, -0.125f, -0.38f, 0.3488f, -0.09316f,
                    new Vector3(90.0f, 0.0f, -90.0f), new Vector3(90.0f, 0.0f, 90.0f) )},
                  {"Female4", new CharCoeffs(1.2512f, -0.89384f, 1.3f, -0.1f, -0.46f, 0.4304f, -0.21228f,
                    new Vector3(90.0f, 0.0f, -90.0f), new Vector3(90.0f, 0.0f, 90.0f) )},
                  {"Female4_red", new CharCoeffs(1.2512f, -0.89384f, 1.3f, -0.1f, -0.46f, 0.4304f, -0.21228f,
                    new Vector3(90.0f, 0.0f, -90.0f), new Vector3(90.0f, 0.0f, 90.0f) )},
                  {"Female4_blue", new CharCoeffs(1.2512f, -0.89384f, 1.3f, -0.1f, -0.46f, 0.4304f, -0.21228f,
                    new Vector3(90.0f, 0.0f, -90.0f), new Vector3(90.0f, 0.0f, 90.0f) )},
                  {"Male1", new CharCoeffs(1.168f, -0.8736f, 0.82f, 0.1f, -0.48f, 0.1152f, 0.00336f,
                    new Vector3(90.0f, 0.0f, 0.0f), new Vector3(90.0f, 0.0f, 180.0f) )},
                  {"Male1_invisible", new CharCoeffs(1.168f, -0.8736f, 0.82f, 0.1f, -0.48f, 0.1152f, 0.00336f,
                    new Vector3(90.0f, 0.0f, 0.0f), new Vector3(90.0f, 0.0f, 180.0f) )},
                  {"Male1_red", new CharCoeffs(1.168f, -0.8736f, 0.82f, 0.1f, -0.48f, 0.1152f, 0.00336f,
                    new Vector3(90.0f, 0.0f, 0.0f), new Vector3(90.0f, 0.0f, 180.0f) )},
                  {"Male1_blue", new CharCoeffs(1.168f, -0.8736f, 0.82f, 0.1f, -0.48f, 0.1152f, 0.00336f,
                    new Vector3(90.0f, 0.0f, 0.0f), new Vector3(90.0f, 0.0f, 180.0f) )},
                  {"Male2", new CharCoeffs(1.1664f, -0.95248f, -1.13f, -0.8f, 0.0f, 0.712f, -0.36f,
                    new Vector3(90.0f, 0.0f, -90.0f), new Vector3(90.0f, 0.0f, 90.0f) )},
                  {"Male6", new CharCoeffs(1.264f, -1.0038f, 1.13f, -0.05f, -0.53f, 0.2752f, -0.18f,
                    new Vector3(90.0f, 0.0f, -90.0f), new Vector3(90.0f, 0.0f, 90.0f) )},
                  {"Male10", new CharCoeffs(1.2192f, -0.93244f, 0.8f, 0.1f, -0.52f, 0.536f, -0.3182f,
                    new Vector3(90.0f, 0.0f, -90.0f), new Vector3(90.0f, 0.0f, 90.0f) )}
              };

            CharCoeffs curCC;
            bool shouldRandomize = false;

            // All IK targets are children of this transform
            Transform m_tsfm_ikTarget_parent;

            IKEffector m_ike_thighLeft;
            IKEffector m_ike_thighRight;
            IKEffector m_ike_body;
            IKEffector m_ike_shoulderLeft;
            IKEffector m_ike_shoulderRight;
            // Hands IKEffectors are needed to set "maintain relative position"
            // which enables better hand positioning while sitting
            // This is a quick workaround. The proper why of setting it would be using
            // Effector Offset from FinalIK and set its offset position based on ikTargetParent position.
            //
            // Set as public so that other methods in CharacterControl can use it.
            public IKEffector m_ike_handLeft;
            public IKEffector m_ike_handRight;

            // This class provides adjustments to IK targets that is tailored to each character
            public IkTargets(GameObject charGo, bool _shouldRandomize)
            {
                const string IK_TARGET_TSFM_PARENT = "ikTargets";
                const string IK_TARGET_TSFM_THIGH_LEFT = "thighLeft";
                const string IK_TARGET_TSFM_THIGH_RIGHT = "thighRight";
                const string IK_TARGET_TSFM_BODY = "body";
                const string IK_TARGET_TSFM_SHOULDER_LEFT = "shoulderLeft";
                const string IK_TARGET_TSFM_SHOULDER_RIGHT = "shoulderRight";

                // Check if the character name contains brackets e.g. "(Clone)". If so, remove it.
                int bracketIdx = charGo.name.IndexOf("(");
                if ( bracketIdx != -1)
                {
                    curCC = charName2Coeffs[charGo.name.Substring(0, bracketIdx)];
                }
                else
                {
                    curCC = charName2Coeffs[charGo.name];
                }
                
                shouldRandomize = _shouldRandomize;

                FullBodyBipedIK fbbik = charGo.GetComponent<FullBodyBipedIK> ();

                m_tsfm_ikTarget_parent = new GameObject(IK_TARGET_TSFM_PARENT).transform;
                m_tsfm_ikTarget_parent.SetParent(charGo.transform, false);

                Transform ikTarget_thighLeft = new GameObject(IK_TARGET_TSFM_THIGH_LEFT).transform;
                Transform ikTarget_thighRight = new GameObject(IK_TARGET_TSFM_THIGH_RIGHT).transform;
                Transform ikTarget_body = new GameObject(IK_TARGET_TSFM_BODY).transform;
                Transform ikTarget_shoulderLeft = new GameObject(IK_TARGET_TSFM_SHOULDER_LEFT).transform;
                Transform ikTarget_shoulderRight = new GameObject(IK_TARGET_TSFM_SHOULDER_RIGHT).transform;

                ikTarget_thighLeft.position = fbbik.references.leftThigh.position;
                ikTarget_thighRight.position = fbbik.references.rightThigh.position;
                ikTarget_body.position = fbbik.references.spine[0].position;
                ikTarget_shoulderLeft.position = fbbik.references.leftUpperArm.position;
                ikTarget_shoulderRight.position = fbbik.references.rightUpperArm.position;

                ikTarget_thighLeft.SetParent(m_tsfm_ikTarget_parent.transform, true);
                ikTarget_thighRight.SetParent(m_tsfm_ikTarget_parent.transform, true);
                ikTarget_body.SetParent(m_tsfm_ikTarget_parent.transform, true);
                ikTarget_shoulderLeft.SetParent(m_tsfm_ikTarget_parent.transform, true);
                ikTarget_shoulderRight.SetParent(m_tsfm_ikTarget_parent.transform, true);

                IKSolverFullBodyBiped slvr = charGo.GetComponent<FullBodyBipedIK> ().solver;
                m_ike_thighLeft = slvr.GetEffector(FullBodyBipedEffector.LeftThigh);
                m_ike_thighRight = slvr.GetEffector(FullBodyBipedEffector.RightThigh);
                m_ike_body = slvr.GetEffector(FullBodyBipedEffector.Body);
                m_ike_shoulderLeft = slvr.GetEffector(FullBodyBipedEffector.LeftShoulder);
                m_ike_shoulderRight = slvr.GetEffector(FullBodyBipedEffector.RightShoulder);
                m_ike_handLeft = slvr.GetEffector(FullBodyBipedEffector.LeftHand);
                m_ike_handRight = slvr.GetEffector(FullBodyBipedEffector.RightHand);

                m_ike_thighLeft.target = ikTarget_thighLeft;
                m_ike_thighRight.target = ikTarget_thighRight;
                m_ike_body.target = ikTarget_body;
                m_ike_shoulderRight.target = ikTarget_shoulderRight;
                m_ike_shoulderLeft.target = ikTarget_shoulderLeft;
            }

            public void ActionSit(Transform tsfm_su)
            {
                const float IK_HAND_MAINTAIN_RELATIVE_POS = 0.4f;
                m_tsfm_ikTarget_parent.localPosition = curCC.CalcuateIkParentPos(tsfm_su.position.y,
                  shouldRandomize);
                m_ike_handLeft.maintainRelativePositionWeight = IK_HAND_MAINTAIN_RELATIVE_POS;
                m_ike_handRight.maintainRelativePositionWeight = IK_HAND_MAINTAIN_RELATIVE_POS;
            }

            public void ActionDoorOpen(Transform ikTarget, bool isPush)
            {
                if (isPush)
                {
                    ikTarget.localRotation = curCC.qtrn_ikTarget_doorHandle_push;
                }
                else
                {
                    ikTarget.localRotation = curCC.qtrn_ikTarget_doorHandle_pull;
                }
            }

            public void SetWeightsSit(float masterWeight)
            {
                const float IK_TARGET_POS_WEIGHT_BODY = 0.5f;
                const float IK_TARGET_POS_WEIGHT_SHOULDER = 0.2f;

                m_ike_thighLeft.positionWeight = masterWeight * IK_TARGET_POS_WEIGHT_BODY;
                m_ike_thighRight.positionWeight = masterWeight * IK_TARGET_POS_WEIGHT_BODY;
                m_ike_body.positionWeight = masterWeight * IK_TARGET_POS_WEIGHT_BODY;
                m_ike_shoulderLeft.positionWeight = masterWeight * IK_TARGET_POS_WEIGHT_SHOULDER;
                m_ike_shoulderRight.positionWeight = masterWeight * IK_TARGET_POS_WEIGHT_SHOULDER;
            }

            public void RevertActionSit()
            {
                m_ike_handLeft.maintainRelativePositionWeight = 0.0f;
                m_ike_handRight.maintainRelativePositionWeight = 0.0f;
            }
        }

        public float animSpeedMultiplier = 1.0f;
        public bool extraTurn;
        public Recorder rcdr { get; internal set; }
        public ProcessingReport report { get; internal set; }
        public bool Randomize { get; internal set; }
        public DoorControl DoorControl { get; } = new DoorControl();
        public State_char stateChar { get; internal set; } = null;

        Animator m_animator;
        Rigidbody m_rb;
        NavMeshAgent m_nma;
        InteractionSystem m_is;
        Vector3? m_pos_lookAt;
        bool m_anm_isCharSittingDown;
        bool m_anm_isLastDoorOpenPush;
        IkTargets m_ikTargets;
        
        delegate void LateUpdateDelegate();
        LateUpdateDelegate onLateUpdate;

        const float TIMEDAMP_MOVE = 0.1f;
        const float TIMEDAMP_STOP_RUN = 0.23f; // More time damping for stopping so that stopping looks more natural and smooth
        const float SPEED_WALK = 0.5f;
        const float SPEED_RUN = 1.0f;
        const float VELOCITY_STOP_TOLERANCE_WALK_N_RUN = 0.05f;
        const float BLENDING_TURN_MULTIPLIER = 0.6f;
        const float TURN_AMOUNT_MULTIPLIER = 1.25f;
        const float DISTANCE_DIFF_BLEND_ACTION_RANGE = 0.25f;
        const float MIN_TURN_AMOUNT = 0.025f;
        const float ALMOST_TOUCHING = 0.95f;

        const float MIN_DISPLACEMENT = 3.0f;
        const float MAX_FRAMES_STOP = 50.0f;
        const float MAX_TIME_STOP = 5.0f;

        const string ANIM_STR_FORWARD = "Forward";
        const string ANIM_STR_TURN = "Turn";
        const string ANIM_STR_SIT = "Sit";
        const string ANIM_STR_SIT_WEIGHT = "SitWeight";
        const string ANIM_STR_HAND_WEIGHT = "HandWeight";

        #region UnityEventFunctions
        void Awake()
        {
            // Get the components on the object we need ( should not be null due to require component so no need to check )
            m_animator = GetComponent<Animator>();
            m_rb = GetComponent<Rigidbody> ();
            m_nma = GetComponent<NavMeshAgent>();
            m_is = GetComponent<InteractionSystem> ();

            m_is.speed = animSpeedMultiplier;
            m_animator.speed = animSpeedMultiplier;
            m_animator.applyRootMotion = true;

            m_nma.updateRotation = true;

            Rigidbody rigidbody = GetComponent<Rigidbody>();
            rigidbody.constraints = RigidbodyConstraints.FreezeRotationX |
                                    RigidbodyConstraints.FreezeRotationY |
                                    RigidbodyConstraints.FreezeRotationZ;
            m_anm_isCharSittingDown = false;
            m_ikTargets = new IkTargets(gameObject, Randomize);
        }

        public void SetSpeed(float speed_value=1.0f)
        {
            m_is.speed = speed_value;
            m_animator.speed = speed_value;

        }

        void LateUpdate()
        {
            if (onLateUpdate != null)
            {
                onLateUpdate();
            }
        }
        #endregion
        

        #region PublicMethods
        public IEnumerator GrabObject(GameObject go, FullBodyBipedEffector fbbe)
        {
            InteractionObject io = go.GetComponent<HandInteraction>().Get_IO_grab(transform, fbbe);
            m_is.StartInteraction(fbbe, io, false);
            while ( canContinue(m_is.inInteraction) )
            {
                yield return null;
            }
        }

        public IEnumerator StartInteraction(GameObject go, FullBodyBipedEffector fbbe, int switchIdx = 0)
        {
            // NavMeshAgent has to be disabled on some occasions when target GameObject has NavMeshObstacle
            // component (e.g. closing a door using IK). 
            m_nma.enabled = false;
            InteractionObject io = go.GetComponent<HandInteraction>().Get_IO_interaction(switchIdx, name, fbbe);
            m_is.StartInteraction(fbbe, io, false);
            while ( canContinue(m_is.inInteraction) )
            {
                yield return null;
            }
            m_nma.enabled = true;
        }

        // go: GameObject that is getting sit on
        // objToInteract: The target that this character must look at
        // targetObject: IK target of body
        public IEnumerator Sit(GameObject go, GameObject objToInteract, GameObject targetObject, bool perform_animation = false)
        {
            if (objToInteract == null || targetObject == null) {
                Debug.LogError("Null objectToInteract or targetObject in Sit" + go.name);
                yield break;
            }

            yield return Turn(objToInteract.transform.position);
            
            m_animator.SetBool(ANIM_STR_SIT, true);
            m_ikTargets.ActionSit(targetObject.transform);
            onLateUpdate += AdjustSittingAnim;
            // NavMeshAgent has to be disabled during sitting because it will interfere
            // with sitting position of character.
            m_nma.enabled = false;

            while ( canContinue(!m_anm_isCharSittingDown) )
            {
                yield return null;
            }

            if (stateChar != null)
            {
                string objType = Helper.GetClassGroups()[go.name].className;
                stateChar.UpdateSittingOn(objType);
            }
        }

        public IEnumerator Stand()
        {
            m_animator.SetBool(ANIM_STR_SIT, false);
            while ( canContinue(onLateUpdate != null) )
            {
                yield return null;
            }

            // Reset values
            m_ikTargets.RevertActionSit();
            m_nma.enabled = true;
            // Comment this for now. It might be needed laster on
            // when we have multiple characters.
            // m_su.isSittable = true;

            if (stateChar != null)
            {
                stateChar.UpdateSittingOn("");
            }
        }

        public IEnumerator walkOrRunToWithDoorOpening(Recorder recorder, int scriptLine, bool isWalk,
          IEnumerable<Vector3> positions, IEnumerable<Vector3> lookAts)
        {
            Vector3 pos;
            Vector3? lookAt;
            NavMeshPath path;

            if ( SelectPosAndLookAt(positions, lookAts, out pos, out lookAt, out path) )
            {
                if (DoorControl.ClosedDoorsCount() == 0)
                {
                    yield return walkOrRunTo(isWalk, pos, lookAt);
                }
                else
                {
                    Debug.Log($"Path from {transform.position} to {pos}" + string.Join("->", path.corners));

                    IList<DoorAction> doors = DoorControl.SelectDoorsOnPath(path.corners, true);

                    if (doors.Count > 0)
                    {
                        foreach (DoorAction action in doors)
                        {
                            Properties_door pd = action.properties;
                            GameObject go = pd.gameObject;

                            Debug.Log("Need to go through closed door " + go.name);

                            // Debug.Log($"walkOrRunTo door open position {action.posOne}");
                            yield return walkOrRunTo(isWalk, action.posOne, action.posTwo); //go.transform.position);
                            // Debug.Log("DoorOpenLeft");
                            recorder.MarkActionStart(InteractionType.OPEN, scriptLine);
                            yield return DoorOpenLeft(go);
                            // Debug.Log("DoorCloseRightAfterOpening");
                            recorder.MarkActionStart(InteractionType.CLOSE, scriptLine);
                            yield return DoorCloseRightAfterOpening(go);
                            // Debug.Log("Finished");
                        }
                    }
                    // Debug.Log($"Direct walk to {pos}");
                    recorder.MarkActionStart(isWalk ? InteractionType.WALK : InteractionType.RUN, scriptLine);
                    yield return walkOrRunTo(isWalk, pos, lookAt);
                }
            }
        }

        public IEnumerator walkOrRunTo(bool isWalk, IEnumerable<Vector3> positions, IEnumerable<Vector3> lookAts)
        {
            Vector3 pos;
            Vector3? lookAt;
            NavMeshPath path;

            if ( SelectPosAndLookAt(positions, lookAts, out pos, out lookAt, out path) )
            {
                yield return walkOrRunTo(isWalk, pos, lookAt, false);
            }
        }

        public IEnumerator walkOrRunTo(bool isWalk, Vector3 pos, Vector3? lookAt = null, bool shouldCheckPath = true)
        {
            if (shouldCheckPath)
            {
                NavMeshPath path = new NavMeshPath();
                m_nma.CalculatePath(pos, path);
                if (path.status != NavMeshPathStatus.PathComplete)
                {
                    Debug.LogError($"Character cannot reach the destination {pos}");
                    if (rcdr != null)
                    {
                        rcdr.Error = new ExecutionError("Path not complete");
                    }
                    if (report != null)
                    {
                        report.AddItem("Character cannot reach the desitnation");
                    }
                    yield break;
                }
            }

            m_pos_lookAt = lookAt;

            if (isWalk)
            {
                m_nma.speed = SPEED_WALK;
            }
            else
            {
                m_nma.speed = SPEED_RUN;
            }

            if (Randomize)
            {
                m_nma.speed += Random.Range(-0.15f, 0.1f);
            }

            m_nma.isStopped = false;
            m_nma.SetDestination(pos);

            while ( canContinue(m_nma.pathPending) )
            {
                yield return null;
            }

            Debug.Assert(m_nma.pathStatus == NavMeshPathStatus.PathComplete, "Path is not complete");
            float init_dist = m_nma.remainingDistance;
            float total_displacement = init_dist - m_nma.remainingDistance;
            //int cont = 0;

            var init_time = Time.time;

            for (float distanceDiff = float.PositiveInfinity;
              distanceDiff > 0 && ((rcdr == null) || ( (rcdr != null) && !rcdr.BreakExecution() ));
              distanceDiff = m_nma.remainingDistance - m_nma.stoppingDistance)
            {
                total_displacement = init_dist - m_nma.remainingDistance;
                var current_time = Time.time - init_time;
                if (total_displacement < MIN_DISPLACEMENT && current_time > MAX_TIME_STOP)
                {
                    m_nma.isStopped = true;
                    if (rcdr != null)
                    {
                        rcdr.Error = new ExecutionError("Path not complete");
                    }
                    if (report != null)
                        report.AddItem("Character cannot reach the desitnation due to collision");
                    yield break;
                    //cont = 0;

                }
                

                Vector3 velo = m_nma.desiredVelocity;
                // Convert the world relative moveInput vector into a local-relative
                // turn amount and forward amount required to head in the desired
                // direction.
                if (velo.magnitude > 1f)
                {
                    velo.Normalize ();
                }
                velo = transform.InverseTransformDirection (velo);
                float turnAmount = Mathf.Atan2 (velo.x, velo.z) * TURN_AMOUNT_MULTIPLIER;
                float forwardAmount = velo.z;

                // Help the character turn faster (this is in addition to root rotation in the animation)
                // if (extraTurn)
                // {
                //     const float movingTurnSpeed = 360.0f;
                //     const float stationaryTurnSpeed = 180.0f;
                //     float turnSpeed = Mathf.Lerp (stationaryTurnSpeed, movingTurnSpeed, forwardAmount);
                //     transform.Rotate (0, turnAmount * turnSpeed * Time.deltaTime, 0);
                // }

                // Update the animator parameters
                m_animator.SetFloat (ANIM_STR_FORWARD, forwardAmount, TIMEDAMP_MOVE, Time.deltaTime);
                SetAnimatorTurnAmount(distanceDiff, turnAmount);
                yield return null;
            }            

            if (m_pos_lookAt == null)
            {
                while ( canContinue(m_rb.velocity.magnitude > VELOCITY_STOP_TOLERANCE_WALK_N_RUN) )
                {
                    float timeDampValue = TIMEDAMP_MOVE;
                    if (! isWalk)
                    {
                        timeDampValue = TIMEDAMP_STOP_RUN;
                    }

                    m_animator.SetFloat(ANIM_STR_FORWARD, 0.0f, timeDampValue, Time.deltaTime);
                    m_animator.SetFloat(ANIM_STR_TURN, 0.0f, timeDampValue, Time.deltaTime);
                    yield return null;
                }
            }
            else
            {
                m_nma.isStopped = true;

                for ( float turnAmount = CalculateTurnAmount(false);
                  Mathf.Abs(turnAmount) > MIN_TURN_AMOUNT && ((rcdr == null) || ( (rcdr != null) && !rcdr.BreakExecution() ));
                  turnAmount = CalculateTurnAmount(false) ) 
                {
                    m_animator.SetFloat(ANIM_STR_FORWARD, 0.0f, TIMEDAMP_MOVE, Time.deltaTime);
                    m_animator.SetFloat(ANIM_STR_TURN, turnAmount, TIMEDAMP_MOVE, Time.deltaTime);
                    yield return null;
                }

                m_pos_lookAt = null;
            }

            m_animator.SetFloat(ANIM_STR_FORWARD, 0.0f);
            m_animator.SetFloat(ANIM_STR_TURN, 0.0f);
            m_nma.isStopped = true;
        }

        public IEnumerator walkOrRunTeleport(bool isWalk, Vector3 pos, Vector3 lookAt)
        {


            yield return walkOrRunTeleport(isWalk, pos, lookAt, false);

        }

        public IEnumerator walkOrRunTeleport(bool isWalk, Vector3 pos, Vector3? lookAt = null, bool shouldCheckPath = true)
        {
            if (shouldCheckPath)
            {
                NavMeshPath path = new NavMeshPath();
                m_nma.CalculatePath(pos, path);
                if (path.status != NavMeshPathStatus.PathComplete)
                {
                    Debug.LogError($"Character cannot reach the destination {pos}");
                    if (rcdr != null)
                    {
                        rcdr.Error = new ExecutionError("Path not complete");
                    }
                    if (report != null)
                    {
                        report.AddItem("Character cannot reach the desitnation");
                    }
                    yield break;
                }
                else
                {
                    Debug.Log($"Character wants to reach the position {pos}");
                }
            }

            m_pos_lookAt = lookAt;


            m_nma.isStopped = false;
            m_nma.Warp(pos);
            if (m_pos_lookAt.HasValue)
            {
                Vector3 mpos = (Vector3)m_pos_lookAt;
                mpos.y = m_nma.transform.position.y;
                m_nma.transform.LookAt(mpos);
            }


            m_animator.SetFloat(ANIM_STR_FORWARD, 0.0f);
            m_animator.SetFloat(ANIM_STR_TURN, 0.0f);
            m_nma.isStopped = true;
        }

        public IEnumerator TurnDegrees(float degrees)
        {
            float radians = degrees * Mathf.PI / 180.0f;
            m_nma.gameObject.transform.Rotate(new Vector3(0.0f, degrees, 0.0f));
            yield return null;
        }

        // Simple turn, just for test...
        public IEnumerator Turn(Vector3 posToInteract)
        {
            m_pos_lookAt = posToInteract;
            for (float turnAmount = CalculateTurnAmount(false);
                        Mathf.Abs(turnAmount) > MIN_TURN_AMOUNT;
                        turnAmount = CalculateTurnAmount(false)) {
                m_animator.SetFloat(ANIM_STR_FORWARD, 0.0f, TIMEDAMP_MOVE, Time.deltaTime);
                m_animator.SetFloat(ANIM_STR_TURN, turnAmount, TIMEDAMP_MOVE, Time.deltaTime);
                yield return null;
            }
        }

        #region Public SimpleActions
        public IEnumerator DrinkLeft()
        {
            yield return SimpleAction("DrinkLeft");
        }

        public IEnumerator DrinkRight()
        {
            yield return SimpleAction("DrinkRight");
        }

        public IEnumerator TextLeft()
        {
            yield return SimpleAction("TextLeft");
        }

        public IEnumerator TextRight()
        {
            yield return SimpleAction("TextRight");
        }

        public IEnumerator TalkLeft()
        {
            yield return SimpleAction("TalkLeft");
        }

        public IEnumerator TalkRight()
        {
            yield return SimpleAction("TalkRight");
        }
        #endregion

        #region Public DoorActions
            
        public IEnumerator DoorOpenLeft(GameObject door)
        {
            const float BEND_GOAL_MAX = 0.7f;
            
            Properties_door pd = door.GetComponent<Properties_door> ();
            Rigidbody rb_door = door.GetComponent<Rigidbody> ();
            IKEffector ike_handLeft = m_ikTargets.m_ike_handLeft;
            FBIKChain fc_armLeft = GetComponent<FullBodyBipedIK> ().solver.leftArmChain;

            // Disable this to allow torque to take effect.
            rb_door.isKinematic = false;
            // NavMeshAgent MUST be disabled since door animations will move the character's position
            // (ApplyRootMotion = true). And it can cause race condition.
            // For more information: https://docs.unity3d.com/Manual/nav-MixingComponents.html
            m_nma.enabled = false;
            
            // Currently, based on door prefabs in our scenes,
            // if a door handle is on the right, it's pull
            // and if the handle on the left, it's push
            // However, it's more simple to just measure distance to 
            // both handles to decide the action. This will avoid
            // getting local coordinate of both points

            DoorHandleGroup dhg;
            string animStr;
            Properties_door.ForceCurve fc;
            
            if ( pd.ShouldPush(transform.position) )
            {
                m_anm_isLastDoorOpenPush = true;
                dhg = pd.transformPush.GetComponent<DoorHandleGroup> ();
                animStr = "DoorPushLeft";
                fc = pd.push;
            }
            else
            {
                m_anm_isLastDoorOpenPush = false;
                dhg = pd.transformPull.GetComponent<DoorHandleGroup> ();
                animStr = "DoorPullLeft";
                fc = pd.pull;
            }

            Transform ikTarget = dhg.GetIkTarget_doorHandle();
            m_ikTargets.ActionDoorOpen(ikTarget, m_anm_isLastDoorOpenPush);
            ike_handLeft.target = ikTarget;
            fc_armLeft.bendConstraint.bendGoal = dhg.GetIkBendGoal();            

            pd.StartMonitoringState();

            m_animator.SetBool(animStr, true);
            float timer = 0.0f;
            while ( canContinue( m_animator.GetBool(animStr) ) )
            {
                float ikWeight = m_animator.GetFloat(ANIM_STR_HAND_WEIGHT);
                ike_handLeft.positionWeight = ikWeight;
                ike_handLeft.rotationWeight = ikWeight;
                fc_armLeft.bendConstraint.weight = ikWeight * BEND_GOAL_MAX;
                
                if (ikWeight > ALMOST_TOUCHING)
                {
                    float curTorque = fc.GetTorque(timer);
                    rb_door.AddTorque( new Vector3(0.0f, curTorque) );
                    timer += Time.deltaTime;
                }

                yield return new WaitForFixedUpdate();
            }

            // Reset IK targets
            // No need to revert IK target adjustment for door handle
            // since it will change depending on interacting char anyway.
            ike_handLeft.target = null;
            fc_armLeft.bendConstraint.bendGoal = null;
            rb_door.isKinematic = true;
            m_nma.enabled = true;
        }

        // If a door should be close immediately after opening, this method
        // is preferred for natural looking animation.
        public IEnumerator DoorCloseRightAfterOpening(GameObject door)
        {
            Properties_door pd = door.GetComponent<Properties_door> ();
            Rigidbody rb_door = door.GetComponent<Rigidbody> ();
            DoorHandleGroup dhg_push = pd.transformPush.GetComponent<DoorHandleGroup> ();

            pd.StartMonitoringState();
            rb_door.isKinematic = false;

            // Decide animation based on last opening action, push vs. pull
            if (m_anm_isLastDoorOpenPush)
            {
                yield return CloseDoorBehind(pd, rb_door);
            }
            else
            {
                // Need handle for push since it will pull the handle of the push action
                // to pull the door to close it.
                yield return CloseDoorFront(pd, rb_door, dhg_push);
            }

            rb_door.isKinematic = true;
            m_nma.enabled = true;
        }
        #endregion
        
        #endregion
        

        #region PrivateMethods

        bool SelectPosAndLookAt(IEnumerable<Vector3> positions, IEnumerable<Vector3> lookAts,
          out Vector3 pos, out Vector3? lookAt, out NavMeshPath out_path)
        {
            NavMeshPath path = new NavMeshPath();
            Vector3 p = Vector3.positiveInfinity;

            //NavMeshPath path = new NavMeshPath();
            //m_nma.CalculatePath(pos, path);
            //Debug.Log($"Path from {transform.position} to {pos}: " + string.Join("->", path.corners));
            //for (int i = 1; i < path.corners.Length; i++) {
                //foreach (var pd in DoorControl.doors) {
                //    Debug.Log(Vector3.Distance(pd.offMeshLink.startTransform.position, path.corners[i - 1]) + " " +
                //     Vector3.Distance(pd.offMeshLink.endTransform.position, path.corners[i]) + " " +
                //     Vector3.Distance(pd.offMeshLink.endTransform.position, path.corners[i - 1]) + " " +
                //     Vector3.Distance(pd.offMeshLink.startTransform.position, path.corners[i]));
                //}
                //Properties_door link = DoorControl.doors.FirstOrDefault(pd =>
                //    (Vector3.Distance(pd.offMeshLink.startTransform.position, path.corners[i - 1]) < 0.01f &&
                //     Vector3.Distance(pd.offMeshLink.endTransform.position, path.corners[i]) < 0.01f ||
                //     Vector3.Distance(pd.offMeshLink.endTransform.position, path.corners[i - 1]) < 0.01f &&
                //     Vector3.Distance(pd.offMeshLink.startTransform.position, path.corners[i]) < 0.01f));
                //if (link != null) {
                //    Debug.Log("Off mesh link traversed!");
                //}
            //}

            int idx = 0;
            using ( var enumerator = positions.GetEnumerator() )
            {
                while ( enumerator.MoveNext() )
                {
                    p = enumerator.Current;
                    m_nma.CalculatePath(p, path);
                    if (path.status == NavMeshPathStatus.PathComplete)
                    {
                        break;
                    }
                    idx++;
                }
            }

            out_path = path;

            bool retval = true;
            if (p == Vector3.positiveInfinity || path.status == NavMeshPathStatus.PathInvalid)
            {
                retval = false;
                Debug.LogError( "Character cannot reach the destination" + string.Join(", ", positions) );
                if (rcdr != null)
                {
                    rcdr.Error = new ExecutionError("Path not complete");
                }
                pos = Vector3.zero;
                lookAt = null;
            }
            else
            {
                pos = p;
                int numElements = lookAts.Count();
                if (numElements == 0)
                {
                    lookAt = null;
                }
                // Out of bound check
                else if (idx > numElements - 1)
                {
                    lookAt = lookAts.ElementAt(0);
                }
                else
                {
                    lookAt = lookAts.ElementAt(idx);
                }
            }

            return retval;
        }

        float CalculateTurnAmount(bool isExtraTurn)
        {
            Vector3 pos_objToInteract_local = transform.InverseTransformPoint (m_pos_lookAt.Value);
            pos_objToInteract_local.y = 0;
            pos_objToInteract_local.Normalize();

            // Make turnAmount greater so that we can see more turns in per frame
            float turnMultiplier = TURN_AMOUNT_MULTIPLIER;
            if (isExtraTurn)
            {
                turnMultiplier = BLENDING_TURN_MULTIPLIER;
            }

            return Mathf.Atan2 (pos_objToInteract_local.x, pos_objToInteract_local.z) * turnMultiplier;
        }

        void SetAnimatorTurnAmount(float distanceDiff, float turnAmount)
        {
            // If the character has an object to pickup and    is within range of
            // starting other action (so that it blends with walking, not discontinuous)
            // change turnAmount.
            if (m_pos_lookAt != null && distanceDiff <= DISTANCE_DIFF_BLEND_ACTION_RANGE)
            {
                turnAmount = CalculateTurnAmount(true);
            }
            m_animator.SetFloat(ANIM_STR_TURN, turnAmount, TIMEDAMP_MOVE, Time.deltaTime);
        }

        // Delegate that will be called if some changes should be made during LateUpdate()
        void AdjustSittingAnim()
        {
            float sitWeight = m_animator.GetFloat(ANIM_STR_SIT_WEIGHT);
            // If sitWeight has been previously increased, and now it has
            // reached zero, that means this late update delegate is no
            // longer needed.
            if (sitWeight == 0.0f && m_anm_isCharSittingDown)
            {
                m_anm_isCharSittingDown = false;
                onLateUpdate -= AdjustSittingAnim;
            }
            else if (sitWeight > 0.99f)
            {
                m_anm_isCharSittingDown = true;
            }
            m_ikTargets.SetWeightsSit(sitWeight);
            m_animator.SetFloat(ANIM_STR_FORWARD, 0.0f, TIMEDAMP_MOVE, Time.deltaTime);
            m_animator.SetFloat(ANIM_STR_TURN, 0.0f, TIMEDAMP_MOVE, Time.deltaTime);
        }

        // Used for simple animation where setting bool value on animator and
        // just polling it is good enough. Mostly Used for Upper body animations (drink, text etc)
        IEnumerator SimpleAction(string anim_param)
        {
		    m_animator.SetBool(anim_param, true);
            // Since this bool will reset after the animation finishes
            // (it's controlled by DisableBoolean.cs), we can just poll
            // this animation parameter.
            while( canContinue( m_animator.GetBool(anim_param) ) )
            {
                yield return null;
            }
        }

        bool canContinue(bool condition)
        {
            if (condition && ((rcdr == null) || ( (rcdr != null) && !rcdr.BreakExecution() )))
            {
                return true;
            }
            return false;
        }

        #region Private DoorActions
        IEnumerator CloseDoorBehind(Properties_door pd, Rigidbody rb_door)
        {
            const string ANIM_STR_DOOR_CLOSE_BEHIND_RIGHT = "CloseBehindRight";
            Vector3 MOVE_DEST = new Vector3(-0.2f, 0.0f, 0.95f);

            // First, character need to walk forward left a bit.
            yield return walkOrRunTo( true, transform.TransformPoint(MOVE_DEST) );

            m_nma.enabled = false;

        	m_animator.SetBool(ANIM_STR_DOOR_CLOSE_BEHIND_RIGHT, true);
            IKEffector ike_handRight = m_ikTargets.m_ike_handRight;

        	ike_handRight.target = pd.transformIKClose;
        	float timer = 0.0f;

        	while ( canContinue( m_animator.GetBool(ANIM_STR_DOOR_CLOSE_BEHIND_RIGHT) ) )
        	{
        		float ikWeight = m_animator.GetFloat(ANIM_STR_HAND_WEIGHT);
        		ike_handRight.positionWeight = ikWeight;
        		if (ikWeight > ALMOST_TOUCHING)
        		{
        			float curTorque = pd.closeBehind.GetTorque(timer);
        			rb_door.AddTorque( new Vector3(0.0f, curTorque) );
        			timer += Time.deltaTime;
        		}

                yield return new WaitForFixedUpdate();
        	}

        	ike_handRight.target = null;
        }

        IEnumerator CloseDoorFront(Properties_door pd, Rigidbody rb_door, DoorHandleGroup dhg)
        {
            const string ANIM_STR_DOOR_CLOSE_FRONT_LEFT = "CloseFrontLeft";
            const string ANIM_STR_TURN_WEIGHT = "TurnWeight";
            const float ANIM_TURN_MULTIPLIER = -2.0f;

            m_nma.enabled = false;

        	m_animator.SetBool(ANIM_STR_DOOR_CLOSE_FRONT_LEFT, true);
            IKEffector ike_handLeft = m_ikTargets.m_ike_handLeft;

        	ike_handLeft.target = dhg.GetIkTarget_doorHandle();
            // Slightly Adjust ikTarget position. It's due to the fact that it's different
            // animation from door opening animations.
            Vector3 ikTarget_origPos = ike_handLeft.target.localPosition;
            ike_handLeft.target.localPosition = new Vector3(-0.093f, 0.002f, -0.092f);

        	float timer = 0.0f;

        	while ( canContinue( m_animator.GetBool(ANIM_STR_DOOR_CLOSE_FRONT_LEFT) ) )
        	{
        		float ikWeight = m_animator.GetFloat(ANIM_STR_HAND_WEIGHT);
        		ike_handLeft.positionWeight = ikWeight;
        		if (ikWeight > ALMOST_TOUCHING)
        		{
        			float curTorque = pd.closeFront.GetTorque(timer);
        			rb_door.AddTorque( new Vector3(0.0f, curTorque) );
        			timer += Time.deltaTime;
        		}

                // Make character turn left a bit to avoid character colliding into doorjamb
                m_animator.SetFloat(ANIM_STR_TURN, m_animator.GetFloat(ANIM_STR_TURN_WEIGHT) * ANIM_TURN_MULTIPLIER);
                yield return new WaitForFixedUpdate();        		
        	}

            ike_handLeft.target.localPosition = ikTarget_origPos;
        	ike_handLeft.target = null;
        }
        #endregion

        #endregion

        #region PublicUtils

        public Bounds UpperPartArea()
        {
            Collider coll = GameObjectUtils.GetCollider(gameObject);
            Bounds bounds = coll.bounds;
            Vector3 boundsMin = new Vector3(bounds.min.x, bounds.center.y, bounds.min.z);

            bounds.min = boundsMin;
            return bounds;
        }
        
        #endregion
    }

}
