using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Video;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Linq;
using System;
using StoryGenerator.ChairProperties;
using StoryGenerator.CharInteraction;
using StoryGenerator.DoorProperties;
using StoryGenerator.Helpers;
using StoryGenerator.SceneState;
using StoryGenerator.Utilities;
using StoryGenerator.SpecialBehavior;
using StoryGenerator.HomeAnnotation;

/*

Apartment
    Room
        Group
            Stuff


Target search:
    1. child of this transform
    2. Nested child. (e.g. shower stall)
    3. Some other object in a room (e.g. computer & monitor, light switch & light sources)


1.
    name
    child_no

2. stov
    name
    child_no: X, X, 

3.
    group=X
    name=Y
    instances (number of GOs that can be associated with this: computer is 1. Light switch if INF)


*/


namespace StoryGenerator.HomeAnnotation
{
    //using UtilsAnnotator = StoryGenerator.HomeAnnotation.UtilsAnnotator;

    using HandPose = StoryGenerator.CharInteraction.HandInteraction.HandPose;
    using ActivationAction = StoryGenerator.CharInteraction.HandInteraction.ActivationAction;
    using ActivationSwitch = StoryGenerator.CharInteraction.HandInteraction.ActivationSwitch;

    using TransitionSequence = StoryGenerator.CharInteraction.HandInteraction.TransitionSequence;
    using Link = StoryGenerator.CharInteraction.HandInteraction.TransitionSequence.TransitionType;

    using Toggle = StoryGenerator.CharInteraction.HandInteraction.Toggle;
    using ToggleEmission = StoryGenerator.CharInteraction.HandInteraction.ToggleEmission;
    using ChangeVector = StoryGenerator.CharInteraction.HandInteraction.ChangeVector;
    using TargetProperty = StoryGenerator.CharInteraction.HandInteraction.ChangeVector.TargetProperty;
    using ChangeColor = StoryGenerator.CharInteraction.HandInteraction.ChangeColor;
    using ApplyTorque = StoryGenerator.CharInteraction.HandInteraction.ApplyTorque;
    using TransitionBase = StoryGenerator.CharInteraction.HandInteraction.TransitionBase;

    using InterruptBehavior = StoryGenerator.CharInteraction.HandInteraction.TransitionBase.InterruptBehavior;

    using List_TB = List<StoryGenerator.CharInteraction.HandInteraction.TransitionBase>;
    using List_TS = List<StoryGenerator.CharInteraction.HandInteraction.TransitionSequence>;
    using List_Link = List<StoryGenerator.CharInteraction.HandInteraction.TransitionSequence.TransitionType>;


    static class InteractionConst
    {
        public static readonly float OPEN_ROI_DELTA = 0.1f;
        public static Vector3 DRAWER_OPEN_AMOUNT = new Vector3(0.44f, 0.0f, 0.0f);

        public static float DOOR_OPEN_DEGREES = -85.0f;
        public static float OPEN_ENTER_PRD_MED = 1.0f;
        public static float OPEN_EXIT_PRD_MED = 1.0f;
        public static float OPEN_ENTER_PRD_LONG = 1.7f;
        public static float OPEN_EXIT_PRD_LONG = 1.7f;

        public static void Reset()
        {
            DRAWER_OPEN_AMOUNT = new Vector3(0.44f, 0.0f, 0.0f);
            DOOR_OPEN_DEGREES = -85.0f;
            OPEN_ENTER_PRD_MED = 1.0f;
            OPEN_EXIT_PRD_MED = 1.0f;
            OPEN_ENTER_PRD_LONG = 1.7f;
            OPEN_EXIT_PRD_LONG = 1.7f;
        }
    }




    public class AnnotationRoot
    {
        public List<AnnotationContainer> models;
    }

    [System.Serializable]
    public class AnnotationContainer
    {
        public List<string> model_names;
        public Annotation annotation;
    }

    [System.Serializable]
    public class OtherProperties
    {
        public string[] property_names;

        public void Process(Transform tsfm)
        {
            for (int i = 0; i < property_names.Length; i++)
            {
                if (property_names[i] == "door")
                {
                    tsfm.gameObject.AddComponent<Properties_door>();
                }
                if (property_names[i] == "shower_stall")
                {
                    UtilsAnnotator.HandleShowerStall(tsfm);
                }
                if (property_names[i] == "lamp")
                {
                    UtilsAnnotator.HandleLamp(tsfm);
                }
            }
        }
    }

    [System.Serializable]
    public class Annotation
    {

        public BoxShape nmos { get; set; }
        public BoxShape colliders { get; set; }
        public SingleNMO nmo { get; set; }
        public Interaction hand_interaction;
        public Chair chair;
        public ContainerSwitch[] container_doors;
        public Faucet faucet;
        public Switch electronic_switch;
        public OtherProperties other_properties;
        public PickupObject pickup_obj;
        public string special_behavior;

        public void Annotate(Transform tsfm)
        {
            if (special_behavior != null)
            {
                string special_behavior_full_name = "StoryGenerator.SpecialBehavior." + special_behavior;
                PrefabBehavior pb = (StoryGenerator.SpecialBehavior.PrefabBehavior)System.Activator.CreateInstance(Type.GetType(special_behavior_full_name));
                if (pb != null)
                {
                    pb.Process(tsfm);
                }
            }
            if (other_properties != null)
            {
                other_properties.Process(tsfm);
            }
            if (nmo != null)
            {
                nmo.AddNavMeshObstacle(tsfm);
            }
            else if (nmos != null)
            {
                nmos.AddNavMeshObstacles(tsfm);
            }


            if (colliders != null)
            {
                nmos.AddBoxColliders(tsfm);
            }
            if (chair != null)
            {
                chair.AddPropChair(tsfm);
            }
            if (hand_interaction != null)
            {
                hand_interaction.AddInteraction(tsfm);
            }
            if (container_doors != null)
            {
                for (int i = 0; i < container_doors.Length; i++)
                {
                    container_doors[i].AddSwitches(tsfm);
                }

            }
            if (faucet != null)
            {
                faucet.AddFaucet(tsfm);
            }
            if (electronic_switch != null)
            {
                electronic_switch.AddSwitch(tsfm);
            }
            if (pickup_obj != null)
            {
                pickup_obj.Process(tsfm);
            }

        }
    }

    [System.Serializable]
    public class SingleNMO
    {
        public float[] center;
        public float[] size;
        public bool use_child;
        public void AddNavMeshObstacle(Transform parent)
        {
            Vector3 vcenter = new Vector3(center[0], center[1], center[2]);
            Vector3 vsize = new Vector3(size[0], size[1], size[2]);
            GameObject go = parent.gameObject;
            if (use_child)
            {
                go = parent.GetChild(0).gameObject;
            }
            UtilsAnnotator.AddNavMeshObstacle(go, vcenter, vsize);
        }

    }

    [System.Serializable]
    public class ContainerSwitch
    {
        public string objName;
        public string hand_pose;
        public string target_property;
        public float[] sp;

        public float[] delta;
        public float open_degrees = InteractionConst.DOOR_OPEN_DEGREES;


        public float roi_delta = InteractionConst.OPEN_ROI_DELTA;
        public float? enter_prd { get; set; }
        public float? exit_prd { get; set; }


        public void AddSwitches(Transform tsfm, HandInteraction hi = null)
        {

            if (hi == null && tsfm.gameObject.GetComponent<HandInteraction>() != null)
            {
                hi = tsfm.gameObject.GetComponent<HandInteraction>();
            }
            Vector3 vh_delta;
            if (delta == null)
            {
                vh_delta = new Vector3(0.0f, open_degrees, 0.0f);
            }
            else
            {
                vh_delta = new Vector3(delta[0], delta[1], delta[2]);

            }
            if (tsfm.gameObject.name == "mH_FloorCabinet01" && tsfm.parent.gameObject.name != "mH_FloorCabinet01")
            {
                Debug.Log("HERE");
            }

            Vector3 vh_sp = new Vector3(sp[0], sp[1], sp[2]);
            HandPose vh_hp = (HandPose)System.Enum.Parse(typeof(HandPose), hand_pose);
            ActivationAction action = ActivationAction.Open;
            TargetProperty vh_tp = (TargetProperty)System.Enum.Parse(typeof(TargetProperty), target_property);

            if (enter_prd == null)
            {
                enter_prd = TargetProperty.Rotation == vh_tp ? InteractionConst.OPEN_ENTER_PRD_LONG : InteractionConst.OPEN_ENTER_PRD_MED;
            }
            if (exit_prd == null)
            {
                exit_prd = TargetProperty.Rotation == vh_tp ? InteractionConst.OPEN_EXIT_PRD_LONG : InteractionConst.OPEN_EXIT_PRD_MED;
            }

            
            State_object so;
            //if (hi == null)
            //{
            if (!tsfm.gameObject.GetComponent<State_object>())
            {
                so = tsfm.gameObject.AddComponent<State_object>();
                so.Initialize();
            }

            //}
            //else
            //{
            //    so = tsfm.gameObject.GetComponent<State_object>();
            //}
            //else
            //{
            //    so = hi.gameObject.AddComponent<State_object>();
            //}


            List<Transform> list_objs = new List<Transform>();
            for (int i = 0; i < tsfm.childCount; i++)
            {
                Transform t = tsfm.GetChild(i);
                if (t.name.Contains(objName))
                {
                    list_objs.Add(t);
                }
            }

            int numOfObjs = list_objs.Count;
            if (numOfObjs > 0)
            {
                bool shouldInitialize = false;
                shouldInitialize = true;
                if (hi == null)
                {
                    hi = tsfm.gameObject.AddComponent<HandInteraction>();
                    
                }

                for (int i = 0; i < numOfObjs; i++)
                {
                    Transform o = list_objs[i];
                    List<GameObject> targets = new List<GameObject>() { o.gameObject };

                    List_TS ts = SwitchUtilities.Create2ChangeVector_list(targets, 0.0f, 0.0f,
                      (float)enter_prd, (float)exit_prd, vh_tp, vh_delta, roi_delta,
                      InterruptBehavior.Ignore, InterruptBehavior.Revert,
                      InterruptBehavior.Ignore, InterruptBehavior.Revert,
                      Link.Manual);

                    ActivationSwitch swch = new ActivationSwitch(vh_hp, action, vh_sp, ts, o);
                    if (hi.switches == null)
                    {
                        hi.switches = new List<ActivationSwitch>();
                    }
                    hi.switches.Add(swch);
                }

                if (shouldInitialize)
                {
                    hi.Initialize();
                }
            }
        }
    }


    [System.Serializable]
    public class BoxShape
    {
        public float[] centers;
        public float[] sizes;

        public void AddNavMeshObstacles(Transform parent)
        {
            if (parent.GetComponentInChildren<NavMeshObstacle>() == null)
            {
                const string STR_NMO_HOLDER = "NavMeshObstacles";
                const string STR_NMO = "NavMeshObstacle";

                Transform nmo_holder = new GameObject(STR_NMO_HOLDER).transform;
                nmo_holder.SetParent(parent, false);

                Vector3[] centersV3;
                Vector3[] sizesV3;


                UtilsAnnotator.CreateVector3Array(sizes, out sizesV3);
                UtilsAnnotator.CreateVector3Array(centers, out centersV3);

                for (int i = 0; i < centersV3.Length; i++)
                {
                    GameObject go = new GameObject(STR_NMO);
                    go.transform.SetParent(nmo_holder, false);
                    AddNavMeshObstacle(go, centersV3[i], sizesV3[i]);
                }
            }
        }

        public void AddBoxColliders(Transform parent)
        {
            const string STR_BC_HOLDER = "BoxColliders";
            const string STR_BC = "BoxCollider";

            Transform bc_holder = new GameObject(STR_BC_HOLDER).transform;
            bc_holder.SetParent(parent, false);

            Vector3[] centersV3;
            Vector3[] sizesV3;

            UtilsAnnotator.CreateVector3Array(sizes, out sizesV3);
            UtilsAnnotator.CreateVector3Array(centers, out centersV3);

            for (int i = 0; i < centersV3.Length; i++)
            {
                GameObject go = new GameObject(STR_BC);
                go.transform.SetParent(bc_holder, false);
                AddBoxCollider(go, centersV3[i], sizesV3[i]);
            }
        }

        private static void AddBoxCollider(GameObject go, Vector3 center, Vector3 size)
        {
            BoxCollider bc = go.AddComponent<BoxCollider>();
            if (center != Vector3.zero)
            {
                bc.center = center;
            }
            if (size != Vector3.zero)
            {
                bc.size = size;
            }
        }

        private static void AddNavMeshObstacle(GameObject go, Vector3 center, Vector3 size)
        {
            if (go.GetComponentInChildren<NavMeshObstacle>() == null)
            {
                NavMeshObstacle nmo = go.AddComponent<NavMeshObstacle>();
                if (center != Vector3.zero)
                {
                    nmo.center = center;
                }
                if (size != Vector3.zero)
                {
                    nmo.size = size;
                }

                nmo.carving = true;
                nmo.carvingTimeToStationary = 0.0f;
                nmo.carveOnlyStationary = false;
            }
        }


    }

    [System.Serializable]
    public class Switch
    {
        public float[] switch_pos;
        public string objName;
        public void AddSwitch(Transform tsfm)
        {
            GameObject go = tsfm.gameObject;
            Vector3 pos = new Vector3(switch_pos[0], switch_pos[1], switch_pos[2]);
            State_object so = go.AddComponent<State_object>();
            so.Initialize();
            ActivationSwitch phantomSwitch;
            if (objName != null)
            {
                Transform child_obj = tsfm.Find(objName);
                phantomSwitch = new ActivationSwitch(HandPose.Button, ActivationAction.SwitchOn, pos, null, child_obj);

            }
            else
            {
                phantomSwitch = new ActivationSwitch(HandPose.Button, ActivationAction.SwitchOn, pos, null);

            }
            HandInteraction hi;
            if (go.GetComponent<HandInteraction>())
            {
                hi = go.GetComponent<HandInteraction>();
            }
            else
                hi = go.AddComponent<HandInteraction>();

            if (hi.switches == null)
            {
                hi.switches = new List<ActivationSwitch>();
            }
            hi.switches.Add(phantomSwitch);

            hi.Initialize();
        }
    }

    [System.Serializable]
    public class Faucet
    {
        public float[] local_pos_faucet;
        public float[] local_pos_stream;
        public float[] local_rot_stream;

        public void AddFaucet(Transform tsfm_parent)
        {
            const string STR_FAUCET = "Faucet";
            Vector3 localPos_faucet = new Vector3(local_pos_faucet[0], local_pos_faucet[1], local_pos_faucet[2]);
            Vector3 localPos_stream = new Vector3(local_pos_stream[0], local_pos_stream[1], local_pos_stream[2]);
            Vector3 localRot_stream = new Vector3(local_rot_stream[0], local_rot_stream[1], local_rot_stream[2]);


            // For setting up interaction position and GameObject "Faucet"
            // to find the GameObject to interact during parsing
            GameObject go_faucet = new GameObject(STR_FAUCET);
            go_faucet.transform.SetParent(tsfm_parent, false);
            go_faucet.transform.localPosition = localPos_faucet;

            // Add collider for bounds calculation
            BoxCollider collider = go_faucet.AddComponent<BoxCollider>();
            collider.center = Vector3.zero;
            collider.size = 0.1f * Vector3.one;


            State_object so = go_faucet.AddComponent<State_object>();
            so.Initialize();

            // For water stream
            Transform waterStream = UtilsAnnotator.SpawnPrefab(STR_PATH.WATER_STREAM, go_faucet.transform, localPos_stream, localRot_stream, true);

            List_TS ts = SwitchUtilities.CreateToggle(new List<GameObject>() { waterStream.gameObject }, 0.0f);

            ActivationSwitch swch = new ActivationSwitch(HandPose.GrabVerticalSmall, ActivationAction.SwitchOn,
              Vector3.zero, ts, go_faucet.transform);

            HandInteraction hi = go_faucet.AddComponent<HandInteraction>();
            if (hi.switches == null)
            {
                hi.switches = new List<ActivationSwitch>();
            }
            hi.switches.Add(swch);
            hi.Initialize();

        }
    }

    [System.Serializable]
    public class PickupObject
    {
        public string hand_pose;
        public float?[] hand_position;
        public int? child_pickup;

        public void Process(Transform tsfm)
        {
            GameObject go = tsfm.gameObject;
            if (go.GetComponent<HandInteraction>() == null)
            {
                HandInteraction hi = go.AddComponent<HandInteraction>();
                hi.allowPickUp = true;
                HandPose vh_hp = (HandPose)System.Enum.Parse(typeof(HandPose), hand_pose);
                hi.grabHandPose = vh_hp;
                if (hand_position != null)
                {
                    hi.handPosition = new Vector3((float) hand_position[0], (float) hand_position[1], (float) hand_position[2]);

                }

                if (child_pickup != null)
                {
                    hi.objectToBePickedUp = tsfm.GetChild((int)child_pickup);
                }

                hi.Initialize();
            }
        }
    }


    [System.Serializable]
    public class Chair
    {
        public bool is_bed;
        public float[] positions;
        public float[] areas;

        public void AddPropChair(Transform tsfm)
        {
            GameObject go = tsfm.gameObject;
            if (go.GetComponent<Properties_chair>() != null)
            {
                return;
            }
            Vector3[] vpositions, vareas;
            UtilsAnnotator.CreateVector3Array(positions, out vpositions);
            UtilsAnnotator.CreateVector3Array(areas, out vareas);
            int numOfSA = vpositions.Length;

            Properties_chair pc = go.AddComponent<Properties_chair>();
            pc.isBed = is_bed;
            pc.viewOptions.sittableUnit = true;
            pc.SittableAreas = new Properties_chair.SittableArea[numOfSA];

            for (int i = 0; i < numOfSA; i++)
            {
                Properties_chair.SittableArea sa = new Properties_chair.SittableArea();
                sa.position = vpositions[i];
                sa.area = vareas[i];

                pc.SittableAreas[i] = sa;
            }

            pc.Initialize();
        }
    }

    [System.Serializable]
    public class Interaction
    {
        public bool optional;
        public TransformPointer component_of;
        public TransformPointer target;

        public void AddInteraction(Transform tsfm)
        {

        }

        private Transform Find;
    }

    [System.Serializable]
    public class TransformPointer
    {
        const int NOT_INIT = -1;
        public string name = null;
        public int child_no = NOT_INIT;

    }

    public class ObjectAnnotator
    {
        const string ANNOTATION_JSON = "object_annotation.json";

        public static void AnnotateObj(Transform tsfm)
        {
            // TODO: remove this
            if (tsfm.name.Contains("PRE_FUR_Bed_01"))
            {
                BoxCollider[] cdrs = tsfm.GetComponentsInChildren<BoxCollider>();
                float maxZ = 0.0f;
                BoxCollider blanketCdr = null;

                foreach (BoxCollider cdr in cdrs)
                {
                    Vector3 scaledSize = Vector3.Scale(cdr.transform.localScale, cdr.size);
                    if (scaledSize.z > maxZ)
                    {
                        blanketCdr = cdr;
                        maxZ = scaledSize.z;
                    }
                }

                blanketCdr.size = Vector3.Scale(blanketCdr.size, new Vector3(1.0f, 1.0f, 0.85f));


            }




            Dictionary<string, Annotation> annotationDict = Helper.GetAnnotations();
            //Debug.Assert(annotationDict.ContainsKey(tsfm.name), tsfm.name);


            Annotation attn;

            if (Helper.GetAnnotations().TryGetValue(tsfm.name, out attn))
            {
                attn.Annotate(tsfm);
            }
            else
            {
                // Unity calculates correct size of NMO for GOs below
                string[] DEFAULT_NMOS = new string[] {
                    "Bathtub_",
                    "Nightstand_",
                    "Bookshelf_",
                    "table_",
                    "stand_",
                    "chair_",
                    "Recliner_",
                    "Bench",
                    "Sofa",
                    "counter_",
                    "TV_Stand_",
                    "Kitchen_table"
                };
                foreach (string str in DEFAULT_NMOS)
                {
                    if (tsfm.name.Contains(str))
                    {

                        SingleNMO nmo_new = new SingleNMO();
                        nmo_new.center = new float[3] { 0.0f, 0.0f, 0.0f };
                        nmo_new.size = new float[3] { 0.0f, 0.0f, 0.0f };
                        nmo_new.AddNavMeshObstacle(tsfm);
                        break;
                    }
                }

            }

            if (tsfm.gameObject.name == "Wall")
            {
                tsfm.gameObject.layer = 10;
            }
        }
    }
}