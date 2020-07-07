using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Video;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Linq;
using StoryGenerator.ChairProperties;
using StoryGenerator.CharInteraction;
using StoryGenerator.DoorProperties;
using StoryGenerator.Helpers;
using StoryGenerator.SceneState;
using StoryGenerator.Utilities;

namespace StoryGenerator.HomeAnnotation
{
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

    public class HomeAnnotator
    {

        class ObjGroupBase
        {
            protected Transform tsfm_group;

            public ObjGroupBase(string groupName, Transform room, bool isRequired = true)
            {
                tsfm_group = room.Find(groupName);
                if (tsfm_group == null && isRequired) {
                    Debug.LogError(groupName + " group is not found in " + room.name);
                    Debug.Break();
                }
            }

            public void Process()
            {
                if (tsfm_group != null) {
                    for (int i = 0; i < tsfm_group.childCount; i++) {
                        ProcessItem(tsfm_group.GetChild(i));
                    }
                }
            }

            public virtual void ProcessItem(Transform tsfm) { }
        }

        class Walls : ObjGroupBase
        {
            const string GROUP_NAME = "Walls";

            const string CORNER = "Corner";
            const string DOORWAY_CORNER = "Doorway_corner";
            const string DOORWAY = "Doorway";


            public Walls(Transform room) : base(GROUP_NAME, room) { }

            public override void ProcessItem(Transform wall)
            {
                const int IDX_WALL_LAYER = 10;

                // Wall should be on different layer to allow doors to be opened.
                // This works because default layer collider 
                // won't collide with wall layer colliders.
                // Box colliders on default layer are created so that camera selection
                // algorithm works correctly.
                wall.gameObject.layer = IDX_WALL_LAYER;

                Vector3[] centers;
                Vector3[] sizes;

                if (wall.name.Contains(CORNER)) {
                    centers = new Vector3[] { new Vector3(0.03f, 1.25f, 0.0f), new Vector3(1.25f, 1.25f, -1.22f) };
                    sizes = new Vector3[] { new Vector3(0.06f, 2.5f, 2.5f), new Vector3(2.5f, 2.5f, 0.06f) };
                } else if (wall.name.Contains(DOORWAY_CORNER)) {
                    centers = new Vector3[] { new Vector3(0.86f, 1.25f, 0.03f), new Vector3(-0.83f, 1.25f, 0.03f), new Vector3(-1.25f, 1.25f, 1.25f) };
                    sizes = new Vector3[] { new Vector3(0.7f, 2.5f, 0.04f), new Vector3(0.72f, 2.5f, 0.04f), new Vector3(0.05f, 2.5f, 2.5f) };
                } else if (wall.name.Contains(DOORWAY)) {
                    centers = new Vector3[] { new Vector3(0.03f, 1.25f, 0.85f), new Vector3(0.03f, 1.25f, -0.85f) };
                    sizes = new Vector3[] { new Vector3(0.04f, 2.5f, 0.77f), new Vector3(0.04f, 2.5f, 0.77f) };
                } else {
                    centers = new Vector3[] { new Vector3(0.03f, 1.25f) };
                    sizes = new Vector3[] { new Vector3(0.06f, 2.5f, 2.5f) };
                }

                AddNavMeshObstacles(wall, centers, sizes);
                AddBoxColliders(wall, centers, sizes);
            }

            void AddBoxColliders(Transform parent, Vector3[] centers, Vector3[] sizes)
            {
                const string STR_BC_HOLDER = "BoxColliders";
                const string STR_BC = "BoxCollider";

                Transform bc_holder = new GameObject(STR_BC_HOLDER).transform;
                bc_holder.SetParent(parent, false);

                for (int i = 0; i < centers.Length; i++) {
                    GameObject go = new GameObject(STR_BC);
                    go.transform.SetParent(bc_holder, false);
                    AddBoxCollider(go, centers[i], sizes[i]);
                }
            }

            void AddBoxCollider(GameObject go, Vector3 center, Vector3 size)
            {
                BoxCollider bc = go.AddComponent<BoxCollider>();
                if (center != Vector3.zero) {
                    bc.center = center;
                }
                if (size != Vector3.zero) {
                    bc.size = size;
                }
            }
        }

        class Furnitures : ObjGroupBase
        {
            const string GROUP_NAME = "Furniture";

            const string STALLS = "_stall_";
            const string SHOWER = "Shower_";
            const string SHOWER_CURTAIN_GROUP = "MOD_FUR_Shower_stall_01_Curtain_01";

            const string BED_TYPE_6 = "_Bed_01";
            const string GENERIC_DRAWER = "drawer";
            const string CLOSET_TYPE_1 = "_Closet_01";
            const string CLOSET_TYPE_2 = "_Closet_02";
            const string GROUP_NAME_CLOSET_HANGER = "Hangers";
            const string GROUP_NAME_CLOSET_DRAWER = "Drawers";
            const string CLOSET_TYPE_2_DRAWER = "PRE_FUR_Closet_02_Drawer_0";

            const string SOFA_TYPE_1 = "Sofa_01_";
            const string SOFA_TYPE_2 = "Sofa_02_";

            const string TOILET_TYPE_1 = "Toilet_1";
            const string TOILET_TYPE_2 = "Toilet_";
            const string TOILET_LID = "Toilet_lid";

            const string BATHROOM_CABINET = "Bathroom_cabinet_";
            const string BATHROOM_CABINET_DOORNAME = "MOD_FUR_Bathroom_cabinet_door_0";

            const string KITCHEN_CABINET = "Kitchen_cabinets_";
            const string KITCHEN_CABINET_DOORNAME = "MOD_FUR_Kitchen_cabinet_door_0";

            const string BATHROOM_COUNTER = "Bathroom_counter_";
            const string BATHROOM_COUNTER_DRAWERNAME = "MOD_COL_Bathroom_counter_door_0";

            const string KITCHEN_COUNTER_TYPE_1 = "Kitchen_counter_01";
            const string KITCHEN_COUNTER_TYPE_2 = "Kitchen_counter_02";
            const string KITCHEN_COUNTER_TYPE_3 = "Kitchen_counter_03";
            const string KITCHEN_COUNTER_TYPE_5 = "Kitchen_counter_corner_";

            const string RECLINER = "Recliner";
            const string CPU_CHAIR = "CPU_chair";
            const string KITCHEN_CHAIR = "Kitchen_chair";
            const string BENCH = "Bench_";

            const string CABINET_TYPE_1 = "Cabinet_1";
            const string CABINET_TYPE_2 = "Cabinet_2";
            const string CABINET_DOOR = "door";


            public Furnitures(Transform room) : base(GROUP_NAME, room) { }

            public override void ProcessItem(Transform f)
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

                if (f.name.Contains(STALLS)) {
                    Vector3[] centers = new Vector3[] { new Vector3(0.167f, 1.25f, -0.728f), new Vector3(-0.542f, 1.25f, 0.0f), new Vector3(0.167f, 1.25f, 0.728f) };
                    Vector3[] sizes = new Vector3[] { new Vector3(1.564f, 2.5f, 0.01f), new Vector3(0.148f, 2.5f, 1.36f), new Vector3(1.564f, 2.5f, 0.01f) };

                    AddNavMeshObstacles(f, centers, sizes);

                    // Special handling for shower stall
                    if (f.name.Contains(SHOWER)) {
                        Transform curtainGroup = null;
                        // Do breath first search for shower curtain
                        Queue<Transform> children = new Queue<Transform>();
                        children.Enqueue(f);
                        do {
                            Transform tsfm = children.Dequeue();
                            if (tsfm.name.Contains(SHOWER_CURTAIN_GROUP) && tsfm.gameObject.GetComponent<HandInteraction>() == null) {
                                curtainGroup = tsfm;
                                break;
                            }

                            for (int j = 0; j < tsfm.childCount; j++) {
                                children.Enqueue(tsfm.GetChild(j));
                            }

                        } while (children.Count > 0);

                        if (curtainGroup != null) {
                            Vector3 CURTAIN_OPEN_SCALE = new Vector3(1.0f, 1.0f, 0.23f);
                            Vector3 CURTAIN_DELTA = Vector3.one - CURTAIN_OPEN_SCALE;

                            Transform actualCurtain = curtainGroup.GetChild(0);
                            // Fold curtain
                            actualCurtain.localScale = CURTAIN_OPEN_SCALE;

                            AddNavMeshObstacle(actualCurtain.gameObject, Vector3.zero, Vector3.zero);

                            State_object so = curtainGroup.gameObject.AddComponent<State_object>();
                            so.Initialize(true);

                            List<GameObject> targets = new List<GameObject>() { actualCurtain.gameObject };
                            List_TS ts = SwitchUtilities.Create2ChangeVector_list(targets, 0.0f, 0.0f,
                              InteractionConst.OPEN_ENTER_PRD_MED, InteractionConst.OPEN_EXIT_PRD_MED,
                              TargetProperty.Scale, CURTAIN_DELTA, 0.9f,
                              InterruptBehavior.Ignore, InterruptBehavior.Revert,
                              InterruptBehavior.Ignore, InterruptBehavior.Revert, Link.Manual);

                            ActivationSwitch swch = new ActivationSwitch(HandPose.GrabVerticalSmall,
                              ActivationAction.Open, new Vector3(0.0f, 0.24f, -1.3f), ts, actualCurtain);

                            HandInteraction hi = curtainGroup.gameObject.AddComponent<HandInteraction>();
                            hi.switches = new HandInteraction.ActivationSwitch[1] { swch };
                            hi.Initialize();
                        }
                    }
                } else if (f.name.Contains(BED_TYPE_6)) {
                    // Shrink the size of the box collider corresponding to a blanket
                    // It disturbs correct placement of sittable units on the bed.
                    BoxCollider[] cdrs = f.GetComponentsInChildren<BoxCollider>();
                    float maxZ = 0.0f;
                    BoxCollider blanketCdr = null;

                    foreach (BoxCollider cdr in cdrs) {
                        Vector3 scaledSize = Vector3.Scale(cdr.transform.localScale, cdr.size);
                        if (scaledSize.z > maxZ) {
                            blanketCdr = cdr;
                            maxZ = scaledSize.z;
                        }
                    }

                    blanketCdr.size = Vector3.Scale(blanketCdr.size, new Vector3(1.0f, 1.0f, 0.85f));

                    AddNavMeshObstacle(f.GetChild(0).gameObject, Vector3.zero, Vector3.zero);
                    AddProperties_chair(f.gameObject, new Vector3[] { new Vector3(0.35f, 0.48f, 0.0f) }, new Vector3[] { new Vector3(1.55f, 0.0f, 1.85f) }, true);

                    // Not used for now.
                    // AddSwitches(f, GENERIC_DRAWER, HandPose.GrabHorizontalSmall, ActivationAction.Open,
                    //   new Vector3(0.0f, 0.115f, -0.51f), TargetProperty.Position, new Vector3(0.0f, 0.0f, 0.408f),
                    //   0.484f, 0.56f, InteractionConst.OPEN_ENTER_PRD_MED, InteractionConst.OPEN_EXIT_PRD_MED);
                } else if (f.name.Contains(CLOSET_TYPE_1)) {
                    AddNavMeshObstacle(f.GetChild(0).gameObject, Vector3.zero, Vector3.zero);
                    if (f.GetComponent<HandInteraction>() == null) {
                        const int DOOR_IDX = 2;
                        State_object so = f.gameObject.AddComponent<State_object>();
                        so.Initialize();

                        Transform door = f.GetChild(DOOR_IDX);


                        List_TS ts = SwitchUtilities.Create2ChangeVector_list(new List<GameObject>() { door.gameObject },
                          0.0f, 0.0f, InteractionConst.OPEN_ENTER_PRD_MED, InteractionConst.OPEN_EXIT_PRD_MED, TargetProperty.Position,
                          new Vector3(0.0f, 0.0f, -0.8f), InteractionConst.OPEN_ROI_DELTA,
                          InterruptBehavior.Ignore, InterruptBehavior.Revert,
                          InterruptBehavior.Ignore, InterruptBehavior.Revert, Link.Manual);

                        ActivationSwitch doorSwitch = new ActivationSwitch(HandPose.GrabVerticalSmall, ActivationAction.Open,
                          new Vector3(0.0f, 1.05f, 0.47f), ts, door);

                        HandInteraction hi = f.gameObject.AddComponent<HandInteraction>();
                        hi.switches = new HandInteraction.ActivationSwitch[1] { doorSwitch };

                        hi.Initialize();
                    }
                } else if (f.name.Contains(CLOSET_TYPE_2)) {
                    AddNavMeshObstacle(f.GetChild(0).gameObject, Vector3.zero, Vector3.zero);
                    if (f.GetComponent<HandInteraction>() == null) {
                        for (int i = 0; i < f.childCount; i++) {
                            Transform t = f.GetChild(i);
                            if (t.name.Contains(GROUP_NAME_CLOSET_HANGER)) {
                                for (int k = 0; k < t.childCount; k++) {
                                    GameObject go = t.GetChild(k).gameObject;

                                    if (go.GetComponent<HandInteraction>() == null) {
                                        HandInteraction hi = go.AddComponent<HandInteraction>();
                                        hi.allowPickUp = true;
                                        hi.grabHandPose = HandInteraction.HandPose.GrabVerticalSmall;
                                        hi.handPosition = new Vector3(0.0f, 0.0f, 0.21f);
                                    }
                                }
                            } else if (t.name.Contains(GROUP_NAME_CLOSET_DRAWER)) {
                                HandInteraction hi = f.gameObject.AddComponent<HandInteraction>();
                                AddSwitches(t, CLOSET_TYPE_2_DRAWER, HandPose.GrabHorizontalSmall, ActivationAction.Open,
                                    new Vector3(0.28f, 0.2f, 0.0f), TargetProperty.Position, InteractionConst.DRAWER_OPEN_AMOUNT,
                                    InteractionConst.OPEN_ROI_DELTA, InteractionConst.OPEN_ENTER_PRD_MED,
                                    InteractionConst.OPEN_EXIT_PRD_MED, hi);
                                hi.Initialize();
                            }
                        }
                    }
                } else if (f.name.Contains(SOFA_TYPE_1)) {
                    Vector3[] centers = new Vector3[] { new Vector3(-1.26f, 0.443f, -0.66f), new Vector3(-0.278f, 0.443f, 1.25f) };
                    Vector3[] sizes = new Vector3[] { new Vector3(1.25f, 0.886f, 2.6f), new Vector3(3.2f, 0.886f, 1.26f) };
                    AddNavMeshObstacles(f, centers, sizes);

                    Vector3[] positions = new Vector3[] { new Vector3(-1.04f, 0.39f, -0.293f), new Vector3(0.09f, 0.39f, 1.01f) };
                    Vector3[] areas = new Vector3[] { new Vector3(0.8f, 0.0f, 1.68f), new Vector3(1.4f, 0.0f, 0.8f) };
                    AddProperties_chair(f.gameObject, positions, areas);
                } else if (f.name.Contains(SOFA_TYPE_2)) {
                    AddNavMeshObstacle(f.gameObject, Vector3.zero, Vector3.zero);
                    AddProperties_chair(f.gameObject, new Vector3[] { new Vector3(0.17f, 0.55f, 0.0f) }, new Vector3[] { new Vector3(0.8f, 0.0f, 2.15f) });
                } else if (f.name.Contains(TOILET_TYPE_1)) {
                    AddNavMeshObstacle(f.GetChild(0).gameObject, Vector3.zero, Vector3.zero);

                    // Work around for this specific toilet - add box collider at the back
                    GameObject go = new GameObject();
                    go.transform.SetParent(f);
                    go.transform.localPosition = new Vector3(-0.1810002f, 0.446f, 0.0f);
                    BoxCollider bc = go.AddComponent<BoxCollider>();
                    bc.size = new Vector3(0.5f, 0.5f, 0.5f);
                    AddProperties_chair(f.gameObject, new Vector3[] { new Vector3(0.27f, 0.5f, 0.0f) }, new Vector3[] { new Vector3(0.35f, 0.0f, 0.49f) });

                    if (f.gameObject.GetComponent<HandInteraction>() == null) {
                        State_object so = f.gameObject.AddComponent<State_object>();
                        so.Initialize(true);

                        ActivationSwitch flushSwitch = new ActivationSwitch(HandPose.Button, ActivationAction.SwitchOn,
                          new Vector3(-0.5f, 0.85f, -0.2f), null);

                        HandInteraction hi = f.gameObject.AddComponent<HandInteraction>();
                        hi.switches = new HandInteraction.ActivationSwitch[1] { flushSwitch };
                        hi.Initialize();
                    }
                } else if (f.name.Contains(TOILET_TYPE_2)) {
                    AddNavMeshObstacle(f.gameObject, Vector3.zero, Vector3.zero);
                    AddProperties_chair(f.gameObject, new Vector3[] { new Vector3(0.55f, 0.6f, 0.0f) }, new Vector3[] { new Vector3(0.35f, 0.0f, 0.4f) });

                    Transform lid = null;
                    for (int j = 0; j < f.childCount; j++) {
                        lid = f.GetChild(j);
                        if (lid.name.Contains(TOILET_LID)) {
                            // Open the toilet lid so that it's accessible
                            lid.localRotation = Quaternion.Euler(new Vector3(0.0f, 0.0f, 90.0f));
                            if (lid.GetComponent<BoxCollider>() == null) {
                                lid.gameObject.AddComponent<BoxCollider>();
                            }
                            break;
                        }
                    }

                    Debug.Assert(lid != null, "Cannot find the GameObject of Toilet Lid.");

                    if (f.gameObject.GetComponent<HandInteraction>() == null) {
                        State_object so = f.gameObject.AddComponent<State_object>();
                        so.Initialize(true);

                        ActivationSwitch switch_flush = new ActivationSwitch(HandPose.Button, ActivationAction.SwitchOn,
                          new Vector3(0.0f, 0.8f, 0.33f), null);

                        List_TS ts = SwitchUtilities.Create2ChangeVector_list(new List<GameObject>() { lid.gameObject },
                          0.0f, 0.0f, 0.6f, InteractionConst.OPEN_ENTER_PRD_MED, TargetProperty.Rotation,
                          new Vector3(0.0f, 0.0f, -90.0f), 0.8f,
                          InterruptBehavior.Ignore, InterruptBehavior.Revert,
                          InterruptBehavior.Ignore, InterruptBehavior.Revert,
                          Link.Manual);

                        ActivationSwitch switch_lid = new ActivationSwitch(HandPose.GrabHorizontalSmall, ActivationAction.Open,
                          new Vector3(0.65f, -0.04f, 0.0f), ts, lid);

                        HandInteraction hi = f.gameObject.AddComponent<HandInteraction>();
                        hi.switches = new HandInteraction.ActivationSwitch[2]
                        {
                            switch_flush,
                            switch_lid
                        };
                        hi.Initialize();
                    }
                } else if (f.name.Contains(BATHROOM_CABINET)) {
                    AddSwitches(f, BATHROOM_CABINET_DOORNAME, HandPose.GrabVerticalSmall, ActivationAction.Open,
                      new Vector3(0.04f, -0.265f, -0.865f), TargetProperty.Rotation,
                      new Vector3(0.0f, InteractionConst.DOOR_OPEN_DEGREES, 0.0f), InteractionConst.OPEN_ROI_DELTA,
                      InteractionConst.OPEN_ENTER_PRD_LONG, InteractionConst.OPEN_EXIT_PRD_LONG);
                } else if (f.name.Contains(KITCHEN_CABINET)) {
                    AddSwitches(f, KITCHEN_CABINET_DOORNAME,
                      HandPose.GrabVerticalSmall,
                      ActivationAction.Open,
                      new Vector3(0.03f, -0.235f, 0.68f), TargetProperty.Rotation,
                      new Vector3(0.0f, -InteractionConst.DOOR_OPEN_DEGREES, 0.0f), InteractionConst.OPEN_ROI_DELTA,
                      InteractionConst.OPEN_ENTER_PRD_LONG, InteractionConst.OPEN_EXIT_PRD_LONG);
                } else if (f.name.Contains(BATHROOM_COUNTER)) {
                    AddNavMeshObstacle(f.gameObject, new Vector3(-0.005f, 0.77f), new Vector3(0.652f, 0.39f, 2.862f));

                    if (f.gameObject.GetComponent<HandInteraction>() == null) {
                        AddSwitches(f, BATHROOM_COUNTER_DRAWERNAME, HandPose.GrabHorizontalSmall, ActivationAction.Open,
                          new Vector3(0.33f, 0.0f, 0.0f), TargetProperty.Position, InteractionConst.DRAWER_OPEN_AMOUNT,
                          InteractionConst.OPEN_ROI_DELTA, InteractionConst.OPEN_ENTER_PRD_MED,
                          InteractionConst.OPEN_EXIT_PRD_MED);

                        AddFaucet(f, new Vector3(-0.26f, 1.01f, 0.03f),
                          new Vector3(0.1275f, 0.0966f, -0.03f),
                          new Vector3(70.258f, 90.0f, 90.0f));
                    }
                } else if (f.name.Contains(KITCHEN_COUNTER_TYPE_1)) {
                    AddNavMeshObstacle(f.gameObject, new Vector3(-0.021f, 0.52f, 0.0f), new Vector3(0.77f, 1.05f, 3.014f));
                    if (f.gameObject.GetComponent<HandInteraction>() == null) {
                        AddSwitches(f, GENERIC_DRAWER, HandPose.GrabHorizontalSmall, ActivationAction.Open,
                          new Vector3(0.38f, 0.13f, 0.0f), TargetProperty.Position, InteractionConst.DRAWER_OPEN_AMOUNT,
                          InteractionConst.OPEN_ROI_DELTA, InteractionConst.OPEN_ENTER_PRD_MED,
                          InteractionConst.OPEN_EXIT_PRD_MED);

                        AddFaucet(f, new Vector3(-0.3f, 1.1f, 0.03f),
                          new Vector3(0.138f, 0.09819996f, -0.03f),
                          new Vector3(70.258f, 90.0f, 90.0f));
                    }
                } else if (f.name.Contains(KITCHEN_COUNTER_TYPE_2)) {
                    AddNavMeshObstacle(f.gameObject, new Vector3(-0.021f, 0.52f, 0.0f), new Vector3(0.77f, 1.05f, 3.014f));
                    if (f.gameObject.GetComponent<HandInteraction>() == null) {
                        AddSwitches(f, GENERIC_DRAWER,
                          HandPose.GrabHorizontalSmall, ActivationAction.Open, new Vector3(0.38f, 0.13f, 0.0f),
                          TargetProperty.Position, InteractionConst.DRAWER_OPEN_AMOUNT,
                          InteractionConst.OPEN_ROI_DELTA,
                          InteractionConst.OPEN_ENTER_PRD_MED, InteractionConst.OPEN_EXIT_PRD_MED);
                    }
                } else if (f.name.Contains(KITCHEN_COUNTER_TYPE_3)) {
                    AddNavMeshObstacle(f.gameObject, Vector3.zero, Vector3.zero);
                    if (f.gameObject.GetComponent<HandInteraction>() == null) {
                        AddSwitches(f, GENERIC_DRAWER,
                          HandPose.GrabHorizontalSmall, ActivationAction.Open, new Vector3(0.412f, 0.675f, 0.0f),
                          TargetProperty.Position, InteractionConst.DRAWER_OPEN_AMOUNT,
                          InteractionConst.OPEN_ROI_DELTA,
                          InteractionConst.OPEN_ENTER_PRD_MED, InteractionConst.OPEN_EXIT_PRD_MED);
                    }
                } else if (f.name.Contains(KITCHEN_COUNTER_TYPE_5)) {
                    Vector3[] centers = new Vector3[] { new Vector3(0.0f, 0.52f, -1.536f), new Vector3(1.89f, 0.52f, 0.0f) };
                    Vector3[] sizes = new Vector3[] { new Vector3(0.68f, 1.05f, 3.786f), new Vector3(3.02f, 1.05f, 0.7f) };
                    AddNavMeshObstacles(f, centers, sizes);
                    if (f.gameObject.GetComponent<HandInteraction>() == null) {
                        AddSwitches(f, GENERIC_DRAWER, HandPose.GrabHorizontalSmall, ActivationAction.Open,
                          new Vector3(0.38f, 0.13f, 0.0f), TargetProperty.Position,
                          InteractionConst.DRAWER_OPEN_AMOUNT, InteractionConst.OPEN_ROI_DELTA,
                          InteractionConst.OPEN_ENTER_PRD_MED, InteractionConst.OPEN_EXIT_PRD_MED);

                        AddFaucet(f, new Vector3(1.96f, 1.103f, 0.244f),
                          new Vector3(-0.039f, 0.0901f, -0.1304f),
                          new Vector3(109.742f, 0.0f, 90.0f));
                    }
                } else if (f.name.Contains(RECLINER)) {
                    AddNavMeshObstacle(f.gameObject, Vector3.zero, Vector3.zero);
                    AddProperties_chair(f.gameObject, new Vector3[] { new Vector3(0.03f, 0.51f, 0.0f) }, new Vector3[] { new Vector3(0.47f, 0.0f, 0.6f) });
                } else if (f.name.Contains(CPU_CHAIR)) {
                    AddNavMeshObstacle(f.gameObject, Vector3.zero, Vector3.zero);
                    AddProperties_chair(f.gameObject, new Vector3[] { new Vector3(0.01f, 0.58f, 0.0f) }, new Vector3[] { new Vector3(0.52f, 0.0f, 0.56f) });
                } else if (f.name.Contains(KITCHEN_CHAIR)) {
                    AddNavMeshObstacle(f.gameObject, Vector3.zero, Vector3.zero);
                    AddProperties_chair(f.gameObject, new Vector3[] { new Vector3(0.0f, 0.52f, 0.0f) }, new Vector3[] { new Vector3(0.42f, 0.0f, 0.42f) });
                } else if (f.name.Contains(BENCH)) {
                    AddNavMeshObstacle(f.gameObject, Vector3.zero, Vector3.zero);
                    AddProperties_chair(f.gameObject, new Vector3[] { new Vector3(0.0f, 0.6f, 0.0f) }, new Vector3[] { new Vector3(0.42f, 0.0f, 1.85f) });
                } else if (f.name.Contains(CABINET_TYPE_1)) {
                    AddNavMeshObstacle(f.gameObject, new Vector3(0.0f, 0.0f, 0.525f), new Vector3(1.18f, 0.38f, 1.05f));
                    if (f.gameObject.GetComponent<HandInteraction>() == null) {
                        AddSwitches(f, CABINET_DOOR, HandPose.GrabVerticalSmall, ActivationAction.Open,
                          new Vector3(-0.553f, 0.0f, 0.456f), TargetProperty.Rotation,
                          new Vector3(0.0f, 0.0f, -InteractionConst.DOOR_OPEN_DEGREES),
                          InteractionConst.OPEN_ROI_DELTA,
                          InteractionConst.OPEN_ENTER_PRD_MED, InteractionConst.OPEN_EXIT_PRD_MED);
                    }
                } else if (f.name.Contains(CABINET_TYPE_2)) {
                    AddNavMeshObstacle(f.gameObject, new Vector3(0.0f, 0.562f, 0.007f), new Vector3(0.75f, 1.124f, 0.514f));
                    if (f.gameObject.GetComponent<HandInteraction>() == null) {
                        State_object so = f.gameObject.AddComponent<State_object>();
                        so.Initialize();

                        const float DOOR_OPEN_AMOUNT_ADJUSTMENT = 15.0f;

                        // Find door transforms
                        List<Transform> list_doors = new List<Transform>();
                        for (int i = 0; i < f.childCount; i++) {
                            Transform t = f.GetChild(i);
                            // if (t.name.Contains(CABINET_DOOR)) {
                            // The new MHIP uses different door name
                            if (t.name.Contains("Door")) {
                                list_doors.Add(t);
                            }
                        }

                        Debug.Assert(list_doors.Count == 2, "Number of doors on " + CABINET_TYPE_2 + " doesn't match");

                        List_TS ts_door_1 = SwitchUtilities.Create2ChangeVector_list(new List<GameObject>() { list_doors[0].gameObject },
                          0.0f, 0.0f, InteractionConst.OPEN_ENTER_PRD_MED, InteractionConst.OPEN_EXIT_PRD_MED, TargetProperty.Rotation,
                          new Vector3(0.0f, -InteractionConst.DOOR_OPEN_DEGREES + DOOR_OPEN_AMOUNT_ADJUSTMENT), InteractionConst.OPEN_ROI_DELTA,
                          InterruptBehavior.Ignore, InterruptBehavior.Revert,
                          InterruptBehavior.Ignore, InterruptBehavior.Revert, Link.Manual);

                        ActivationSwitch door_1 = new ActivationSwitch(HandPose.GrabVerticalSmall, ActivationAction.Open,
                          new Vector3(-0.248f, -0.037f, 0.03f), ts_door_1, list_doors[0]);

                        List_TS ts_door_2 = SwitchUtilities.Create2ChangeVector_list(new List<GameObject>() { list_doors[1].gameObject },
                          0.0f, 0.0f, InteractionConst.OPEN_ENTER_PRD_MED, InteractionConst.OPEN_EXIT_PRD_MED, TargetProperty.Rotation,
                          new Vector3(0.0f, InteractionConst.DOOR_OPEN_DEGREES - DOOR_OPEN_AMOUNT_ADJUSTMENT), InteractionConst.OPEN_ROI_DELTA,
                          InterruptBehavior.Ignore, InterruptBehavior.Revert,
                          InterruptBehavior.Ignore, InterruptBehavior.Revert, Link.Manual);

                        ActivationSwitch door_2 = new ActivationSwitch(HandPose.GrabVerticalSmall, ActivationAction.Open,
                          new Vector3(0.248f, -0.037f, 0.03f), ts_door_2, list_doors[1]);

                        HandInteraction hi = f.gameObject.AddComponent<HandInteraction>();
                        hi.switches = new HandInteraction.ActivationSwitch[2]
                        {
                            door_1,
                            door_2
                        };
                        hi.Initialize();
                    }
                } else {
                    foreach (string str in DEFAULT_NMOS) {
                        if (f.name.Contains(str)) {
                            AddNavMeshObstacle(f.gameObject, Vector3.zero, Vector3.zero);
                            break;
                        }
                    }
                }
            }

            void AddProperties_chair(GameObject go, Vector3[] positions, Vector3[] areas, bool isBed = false)
            {
                if (go.GetComponent<Properties_chair>() != null) {
                    return;
                }
                int numOfSA = positions.Length;

                Properties_chair pc = go.AddComponent<Properties_chair>();
                pc.isBed = isBed;
                pc.viewOptions.sittableUnit = true;
                pc.SittableAreas = new Properties_chair.SittableArea[numOfSA];

                for (int i = 0; i < numOfSA; i++) {
                    Properties_chair.SittableArea sa = new Properties_chair.SittableArea();
                    sa.position = positions[i];
                    sa.area = areas[i];

                    pc.SittableAreas[i] = sa;
                }

                pc.Initialize();
            }

            void AddFaucet(Transform tsfm_parent, Vector3 localPos_faucet,
              Vector3 localPos_stream, Vector3 localRot_stream)
            {
                const string STR_FAUCET = "Faucet";

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
                Transform waterStream = SpawnPrefab(STR_PATH.WATER_STREAM, go_faucet.transform, localPos_stream, localRot_stream, true);

                List_TS ts = SwitchUtilities.CreateToggle(new List<GameObject>() { waterStream.gameObject }, 0.0f);

                ActivationSwitch swch = new ActivationSwitch(HandPose.GrabVerticalSmall, ActivationAction.SwitchOn,
                  Vector3.zero, ts, go_faucet.transform);

                HandInteraction hi = go_faucet.AddComponent<HandInteraction>();
                hi.switches = new HandInteraction.ActivationSwitch[1] { swch };
                hi.Initialize();
            }

            void AddSwitches(Transform tsfm, string objName, HandPose hp,
              ActivationAction action, Vector3 sp, TargetProperty tp,
              Vector3 delta, float roi_delta, float enterPrd, float exitPrd,
              HandInteraction hi = null)
            {
                if (tsfm.gameObject.GetComponent<HandInteraction>() != null && hi == null) {
                    return;
                }
                State_object so;
                if (hi == null) {
                    so = tsfm.gameObject.AddComponent<State_object>();
                } else {
                    so = hi.gameObject.AddComponent<State_object>();
                }
                so.Initialize();

                List<Transform> list_objs = new List<Transform>();
                for (int i = 0; i < tsfm.childCount; i++) {
                    Transform t = tsfm.GetChild(i);
                    if (t.name.Contains(objName)) {
                        list_objs.Add(t);
                    }
                }

                int numOfObjs = list_objs.Count;
                if (numOfObjs > 0) {
                    bool shouldInitialize = false;
                    if (hi == null) {
                        hi = tsfm.gameObject.AddComponent<HandInteraction>();
                        shouldInitialize = true;
                    }
                    hi.switches = new ActivationSwitch[numOfObjs];

                    for (int i = 0; i < numOfObjs; i++) {
                        Transform o = list_objs[i];
                        List<GameObject> targets = new List<GameObject>() { o.gameObject };

                        List_TS ts = SwitchUtilities.Create2ChangeVector_list(targets, 0.0f, 0.0f,
                          enterPrd, exitPrd, tp, delta, roi_delta,
                          InterruptBehavior.Ignore, InterruptBehavior.Revert,
                          InterruptBehavior.Ignore, InterruptBehavior.Revert,
                          Link.Manual);

                        ActivationSwitch swch = new ActivationSwitch(hp, action, sp, ts, o);
                        hi.switches[i] = swch;
                    }

                    if (shouldInitialize) {
                        hi.Initialize();
                    }
                }
            }
        }

        class Appliances : ObjGroupBase
        {
            const string GROUP_NAME = "Appliances";

            const string FRIDGE = "Fridge_";
            const string FRIDGE_DOOR_1 = "MOD_APP_Fridge_door_01";
            const string FRIDGE_DOOR_2 = "MOD_APP_Fridge_door_02";
            const int FRIDGE_NUM_DRAWERS = 3;
            const string FRIDGE_DRAWER = "MOD_APP_Fridge_drawer_0";

            const string STOVE = "Stove_";
            const int STOVE_NUM_COILS = 4;
            const string STOVE_FAN = "Stove_fan_";
            const string OVEN_DOOR = "MOD_APP_Oven_door_01";
            const string OVEN_TRAY = "MOD_APP_Oven_tray_01";

            const string DISH_WASHER = "Dishwasher_";
            const string DISH_WASHER_DOOR = "MOD_APP_Dishwasher_door_01";

            const string MICROWAVE_TYPE_1 = "Microwave_1";
            const string MICROWAVE_TYPE_2 = "Microwave_2";
            const string MICROWAVE_TYPE_3 = "Microwave_3";
            const string MICROWAVE_DOOR = "Door";

            const string COFFEEMAKER = "Coffeemaker_";
            const string COFFEEMAKER_BUTTON = "MOD_PRO_Coffeemaker_01_Button_01";
            const string COFFEE_POT = "MOD_PRO_Coffeemaker_01_Pot_01";
            const string COFFEE_POT_LID = "MOD_PRO_Coffeemaker_01_Lid_01";
            const string COFFEE_POT_COFFEE = "MOD_PRO_Coffeemaker_01_Coffee_01";

            const string TOASTER = "Toaster_";
            const string TOASTER_LEVER = "MOD_APP_Toaster_01_Lever_01";

            const string WASHING_MACHINE = "Washing_Machine";


            public Appliances(Transform room) : base(GROUP_NAME, room, false) { }

            public override void ProcessItem(Transform a)
            {

                if (a.name.Contains(FRIDGE)) {
                    AddNavMeshObstacle(a.gameObject, Vector3.zero, Vector3.zero);

                    if (a.gameObject.GetComponent<HandInteraction>() == null) {
                        const float DELAY_LIGHT_ON = 0.2f;
                        const float DELAY_LIGHT_OFF = 0.8f;
                        List_Link links = new List_Link() { Link.Manual };

                        State_object so = a.gameObject.AddComponent<State_object>();
                        so.Initialize();

                        // Need to turn off the light of the fridge in the beginnig to minimize
                        // its effect on scene lighting. It will turn back on when the door is opened
                        GameObject go_pl = a.Find(LAMPS.LIGHT_SOURCE).gameObject;
                        // Default light intensity is too weak.
                        Light l = go_pl.GetComponent<Light>();
                        l.intensity = 1.0f;

                        List<GameObject> list_pl = new List<GameObject>() { go_pl };
                        Toggle tgl_open = new Toggle(list_pl, DELAY_LIGHT_ON, InterruptBehavior.Stop);
                        tgl_open.Flip();
                        Toggle tgl_close = new Toggle(list_pl, DELAY_LIGHT_OFF, InterruptBehavior.Stop);
                        List_TB tb_tgl = new List_TB() { tgl_open, tgl_close };
                        TransitionSequence ts_tgl = new TransitionSequence(tb_tgl, links);

                        // Turn emissive material off - one light bulb at the top of the fridge.
                        List<GameObject> list_fridge = new List<GameObject>() { a.gameObject };
                        ToggleEmission tglEmsn_open = new ToggleEmission(list_fridge, DELAY_LIGHT_ON, InterruptBehavior.Stop);
                        tglEmsn_open.Flip();
                        ToggleEmission tglEmsn_close = new ToggleEmission(list_fridge, DELAY_LIGHT_OFF, InterruptBehavior.Stop);
                        List_TB tb_tglEmsn = new List_TB() { tglEmsn_open, tglEmsn_close };
                        TransitionSequence ts_tglEmsn = new TransitionSequence(tb_tglEmsn, links);

                        // Make white interior less bright since it's reflecting too much light on some scenes.
                        string[] WHITE_INTERIOR = new string[] { "Plastic_02_02" };
                        foreach (Material m in Helper.FindMatNameMatch(list_fridge, WHITE_INTERIOR)) {
                            const float REDUCE_AMOUNT = 0.58f;
                            m.color = new Color(m.color.r * REDUCE_AMOUNT, m.color.g * REDUCE_AMOUNT,
                              m.color.b * REDUCE_AMOUNT, m.color.a);
                        }

                        Transform tsfm_door1 = a.Find(FRIDGE_DOOR_1);
                        TransitionSequence ts_cv_door1 = SwitchUtilities.Create2ChangeVector_sequence(new List<GameObject> { tsfm_door1.gameObject },
                          0.0f, InteractionConst.OPEN_ENTER_PRD_MED, 0.0f, InteractionConst.OPEN_EXIT_PRD_MED, TargetProperty.Rotation,
                          new Vector3(0.0f, InteractionConst.DOOR_OPEN_DEGREES), InteractionConst.OPEN_ROI_DELTA,
                          InterruptBehavior.Ignore, InterruptBehavior.Revert,
                          InterruptBehavior.Ignore, InterruptBehavior.Revert, Link.Manual);
                        List_TS ts_door1 = new List_TS() { ts_cv_door1, ts_tgl, ts_tglEmsn };
                        ActivationSwitch door1 = new ActivationSwitch(HandPose.GrabHorizontalSmall, ActivationAction.Open,
                          new Vector3(0.12f, -0.48f, -0.68f), ts_door1, tsfm_door1);

                        Transform tsfm_door2 = a.Find(FRIDGE_DOOR_2);
                        List_TS ts_door2 = SwitchUtilities.Create2ChangeVector_list(new List<GameObject>() { tsfm_door2.gameObject },
                          0.0f, 0.0f, InteractionConst.OPEN_ENTER_PRD_MED, InteractionConst.OPEN_EXIT_PRD_MED, TargetProperty.Rotation,
                          new Vector3(0.0f, InteractionConst.DOOR_OPEN_DEGREES), InteractionConst.OPEN_ROI_DELTA,
                          InterruptBehavior.Ignore, InterruptBehavior.Revert,
                          InterruptBehavior.Ignore, InterruptBehavior.Revert, Link.Manual);
                        HandInteraction.ActivationSwitch door2 = new HandInteraction.ActivationSwitch(HandPose.GrabHorizontalSmall,
                          ActivationAction.Open, new Vector3(0.12f, 0.37f, -0.68f), ts_door2, tsfm_door2);

                        HandInteraction hi = a.gameObject.AddComponent<HandInteraction>();
                        hi.switches = new HandInteraction.ActivationSwitch[FRIDGE_NUM_DRAWERS + 2];
                        hi.switches[0] = door1;
                        hi.switches[1] = door2;

                        for (int j = 0; j < FRIDGE_NUM_DRAWERS; j++) {
                            Transform drawer = a.Find(FRIDGE_DRAWER + (j + 1));
                            List_TS ts_drawer = SwitchUtilities.Create2ChangeVector_list(new List<GameObject>() { drawer.gameObject },
                              0.0f, 0.0f, InteractionConst.OPEN_ENTER_PRD_MED, InteractionConst.OPEN_EXIT_PRD_MED,
                              TargetProperty.Position, InteractionConst.DRAWER_OPEN_AMOUNT, InteractionConst.OPEN_ROI_DELTA,
                              InterruptBehavior.Ignore, InterruptBehavior.Revert,
                              InterruptBehavior.Ignore, InterruptBehavior.Revert, Link.Manual);

                            HandInteraction.ActivationSwitch swtch = new HandInteraction.ActivationSwitch(HandPose.GrabHorizontalSmall,
                              ActivationAction.Open, new Vector3(0.32f, 0.11f, 0.0f), ts_drawer, drawer);

                            hi.switches[j + 2] = swtch;
                        }

                        hi.Initialize();
                    }
                } else if (a.name.Contains(STOVE) && !a.name.Contains(STOVE_FAN)) {
                    AddNavMeshObstacle(a.gameObject, new Vector3(-0.075f, 0.51f, 0.0f), new Vector3(0.832f, 1.08f, 1.002f));

                    if (a.gameObject.GetComponent<HandInteraction>() == null) {
                        State_object so = a.gameObject.AddComponent<State_object>();
                        so.Initialize();

                        Vector3[] coilLocations = new Vector3[STOVE_NUM_COILS] {
                            new Vector3(0.134f, 1.0688f, 0.203f),
                            new Vector3(-0.265f, 1.0688f, 0.203f),
                            new Vector3(0.134f, 1.0688f, -0.2f),
                            new Vector3(-0.265f, 1.0688f, -0.2f)
                        };
                        Vector3[] switchPositions = new Vector3[STOVE_NUM_COILS] {
                            new Vector3(0.33f, 0.925f, 0.215f),
                            new Vector3(0.33f, 0.925f, 0.37f),
                            new Vector3(0.33f, 0.925f, -0.196f),
                            new Vector3(0.33f, 0.925f, -0.35f)
                        };

                        HandInteraction hi = a.gameObject.AddComponent<HandInteraction>();
                        hi.switches = new HandInteraction.ActivationSwitch[STOVE_NUM_COILS + 1];

                        for (int j = 0; j < STOVE_NUM_COILS; j++) {
                            const float COIL_HEAT_UP = 3.0f;
                            const float COIL_COOL_DOWN = 4.0f;
                            Vector4 COLOR_DELTA = new Vector4(0.0f, 0.0f, 0.0f, 0.7f);

                            Transform stoveCoil = SpawnPrefab(STR_PATH.STOVE_COIL, a, coilLocations[j], Vector3.zero);
                            List<GameObject> list_coil = new List<GameObject>() { stoveCoil.gameObject };

                            ChangeColor cc1 = new ChangeColor(list_coil, 0.0f, COIL_HEAT_UP, false, Vector4.zero,
                              COLOR_DELTA, InterruptBehavior.Ignore, InterruptBehavior.Revert);

                            ChangeColor cc2 = new ChangeColor(list_coil, 0.0f, COIL_COOL_DOWN, false, Vector4.zero,
                              -COLOR_DELTA, InterruptBehavior.Ignore, InterruptBehavior.Revert);

                            List_TB tb = new List_TB() { cc1, cc2 };
                            List_Link links = new List_Link() { Link.Manual };

                            ActivationSwitch swtch = new ActivationSwitch(HandPose.GrabVerticalSmall,
                              ActivationAction.SwitchOn, switchPositions[j], tb, links);

                            hi.switches[j] = swtch;
                        }

                        Transform ovenDoor = a.Find(OVEN_DOOR);

                        List_TS ts = SwitchUtilities.Create2ChangeVector_list(new List<GameObject>() { ovenDoor.gameObject },
                          0.0f, 0.0f, InteractionConst.OPEN_ENTER_PRD_MED, InteractionConst.OPEN_EXIT_PRD_MED, TargetProperty.Rotation,
                          new Vector3(0.0f, 0.0f, InteractionConst.DOOR_OPEN_DEGREES), InteractionConst.OPEN_ROI_DELTA,
                          InterruptBehavior.Ignore, InterruptBehavior.Revert,
                          InterruptBehavior.Ignore, InterruptBehavior.Revert, Link.Manual);

                        ActivationSwitch ovenDoorSwitch = new ActivationSwitch(HandPose.GrabHorizontalSmall,
                          ActivationAction.Open, new Vector3(0.03f, 0.48f, 0.0f), ts, ovenDoor);

                        hi.switches[STOVE_NUM_COILS] = ovenDoorSwitch;
                        hi.Initialize();

                        GameObject tray = a.Find(OVEN_TRAY).gameObject;
                        if (tray != null) {
                            so = a.gameObject.AddComponent<State_object>();
                            so.Initialize();

                            hi = tray.AddComponent<HandInteraction>();
                            hi.allowPickUp = true;
                            hi.grabHandPose = HandInteraction.HandPose.GrabHorizontalSmall;
                            hi.handPosition = new Vector3(0.28f, 0.0f, 0.0f);

                            hi.Initialize();
                        }
                    }
                } else if (a.name.Contains(DISH_WASHER)) {
                    AddNavMeshObstacle(a.gameObject, Vector3.zero, Vector3.zero);

                    if (a.gameObject.GetComponent<HandInteraction>() == null) {
                        State_object so = a.gameObject.AddComponent<State_object>();
                        so.Initialize();

                        Transform door = a.Find(DISH_WASHER_DOOR);

                        List_TS ts = SwitchUtilities.Create2ChangeVector_list(new List<GameObject>() { door.gameObject },
                          0.0f, 0.0f, InteractionConst.OPEN_ENTER_PRD_MED, InteractionConst.OPEN_EXIT_PRD_MED, TargetProperty.Rotation,
                          new Vector3(0.0f, 0.0f, InteractionConst.DOOR_OPEN_DEGREES), InteractionConst.OPEN_ROI_DELTA,
                          InterruptBehavior.Ignore, InterruptBehavior.Revert,
                          InterruptBehavior.Ignore, InterruptBehavior.Revert, Link.Manual);

                        ActivationSwitch doorSwitch = new ActivationSwitch(HandPose.GrabHorizontalSmall, ActivationAction.Open,
                          new Vector3(0.07f, 0.8f, 0.0f), ts, door);

                        ActivationSwitch phantomSwitch = new ActivationSwitch(HandPose.Button, ActivationAction.SwitchOn,
                          new Vector3(0.03f, 0.87f, 0.0f), null, door);

                        HandInteraction hi = a.gameObject.AddComponent<HandInteraction>();
                        hi.switches = new HandInteraction.ActivationSwitch[2]
                        {
                            doorSwitch,
                            phantomSwitch
                        };

                        hi.Initialize();
                    }
                } else if (a.name.Equals(MICROWAVE_TYPE_1)) {
                    if (a.gameObject.GetComponent<HandInteraction>() == null) {
                        State_object so = a.gameObject.AddComponent<State_object>();
                        so.Initialize();

                        Transform door = a.Find(MICROWAVE_DOOR);

                        List_TS ts = SwitchUtilities.Create2ChangeVector_list(new List<GameObject>() { door.gameObject },
                          0.0f, 0.0f, InteractionConst.OPEN_ENTER_PRD_MED, InteractionConst.OPEN_EXIT_PRD_MED, TargetProperty.Rotation,
                          new Vector3(0.0f, 0.0f, -InteractionConst.DOOR_OPEN_DEGREES), InteractionConst.OPEN_ROI_DELTA,
                          InterruptBehavior.Ignore, InterruptBehavior.Revert,
                          InterruptBehavior.Ignore, InterruptBehavior.Revert, Link.Manual);

                        ActivationSwitch doorSwitch = new ActivationSwitch(HandPose.GrabVerticalSmall, ActivationAction.Open,
                          new Vector3(-0.39f, -0.03f, -0.05f), ts, door);

                        ActivationSwitch phantomSwitch = new ActivationSwitch(HandPose.Button, ActivationAction.SwitchOn,
                          new Vector3(-0.2f, -0.18f, 0.25f), null);

                        HandInteraction hi = a.gameObject.AddComponent<HandInteraction>();
                        hi.switches = new HandInteraction.ActivationSwitch[2]
                        {
                            doorSwitch,
                            phantomSwitch
                        };

                        hi.Initialize();
                    }
                } else if (a.name.Equals(MICROWAVE_TYPE_2)) {
                    if (a.gameObject.GetComponent<HandInteraction>() == null) {
                        State_object so = a.gameObject.AddComponent<State_object>();
                        so.Initialize();

                        Transform door = a.Find(MICROWAVE_DOOR);

                        List_TS ts = SwitchUtilities.Create2ChangeVector_list(new List<GameObject>() { door.gameObject },
                          0.0f, 0.0f, InteractionConst.OPEN_ENTER_PRD_MED, InteractionConst.OPEN_EXIT_PRD_MED, TargetProperty.Rotation,
                          new Vector3(-InteractionConst.DOOR_OPEN_DEGREES, 0.0f), InteractionConst.OPEN_ROI_DELTA,
                          InterruptBehavior.Ignore, InterruptBehavior.Revert,
                          InterruptBehavior.Ignore, InterruptBehavior.Revert, Link.Manual);

                        ActivationSwitch doorSwitch = new ActivationSwitch(HandPose.GrabVerticalSmall, ActivationAction.Open,
                          new Vector3(0.0f, 0.31f, 0.03f), ts, door);

                        ActivationSwitch actSwitch = new ActivationSwitch(HandPose.Button, ActivationAction.SwitchOn,
                          Vector3.zero, null, a.Find("Button"));

                        HandInteraction hi = a.gameObject.AddComponent<HandInteraction>();
                        hi.switches = new HandInteraction.ActivationSwitch[2]
                        {
                            doorSwitch,
                            actSwitch
                        };

                        hi.Initialize();
                    }
                } else if (a.name.Equals(MICROWAVE_TYPE_3)) {
                    if (a.gameObject.GetComponent<HandInteraction>() == null) {
                        State_object so = a.gameObject.AddComponent<State_object>();
                        so.Initialize();

                        Transform door = a.Find(MICROWAVE_DOOR);

                        List_TS ts = SwitchUtilities.Create2ChangeVector_list(new List<GameObject>() { door.gameObject },
                          0.0f, 0.0f, InteractionConst.OPEN_ENTER_PRD_MED, InteractionConst.OPEN_EXIT_PRD_MED, TargetProperty.Rotation,
                          new Vector3(-InteractionConst.DOOR_OPEN_DEGREES, 0.0f), InteractionConst.OPEN_ROI_DELTA,
                          InterruptBehavior.Ignore, InterruptBehavior.Revert,
                          InterruptBehavior.Ignore, InterruptBehavior.Revert, Link.Manual);

                        ActivationSwitch doorSwitch = new ActivationSwitch(HandPose.GrabVerticalSmall, ActivationAction.Open,
                          new Vector3(0.0f, 0.24f, 0.02f), ts, door);

                        ActivationSwitch actSwitch = new ActivationSwitch(HandPose.Button, ActivationAction.SwitchOn,
                          Vector3.zero, null, a.Find("control3"));

                        HandInteraction hi = a.gameObject.AddComponent<HandInteraction>();
                        hi.switches = new HandInteraction.ActivationSwitch[2];
                        hi.switches[0] = doorSwitch;
                        hi.switches[1] = actSwitch;

                        hi.Initialize();
                    }
                } else if (a.name.Contains(COFFEEMAKER) && a.gameObject.GetComponent<HandInteraction>() == null) {
                    Transform button = a.Find(COFFEEMAKER_BUTTON);
                    Transform pot = a.Find(COFFEE_POT);

                    // Annotate only if there's a pot as its child
                    if (pot != null) {
                        Transform lid = pot.Find(COFFEE_POT_LID);
                        Transform coffee = pot.Find(COFFEE_POT_COFFEE);

                        // Annotate Coffee pot first.
                        State_object so = pot.gameObject.AddComponent<State_object>();
                        so.Initialize();

                        List_TS ts_lid = SwitchUtilities.Create2ChangeVector_list(new List<GameObject>() { lid.gameObject },
                        0.0f, 0.0f, 0.3f, 0.3f, TargetProperty.Rotation,
                        new Vector3(0.0f, 0.0f, -90.0f), InteractionConst.OPEN_ROI_DELTA,
                        InterruptBehavior.Ignore, InterruptBehavior.Revert,
                        InterruptBehavior.Ignore, InterruptBehavior.Revert, Link.Manual);

                        ActivationSwitch swch_lid = new ActivationSwitch(HandPose.GrabHorizontalSmall, ActivationAction.Open,
                        new Vector3(-0.16f, 0.0f, 0.0f), ts_lid, lid);

                        HandInteraction hi_lid = pot.gameObject.AddComponent<HandInteraction>();
                        hi_lid.allowPickUp = true;
                        hi_lid.grabHandPose = HandInteraction.HandPose.GrabVerticalSmall;
                        hi_lid.handPosition = new Vector3(0.13f, 0.065f, 0.0f);
                        hi_lid.switches = new HandInteraction.ActivationSwitch[1] { swch_lid };
                        hi_lid.Initialize();

                        // Annotate coffee maker
                        so = a.gameObject.AddComponent<State_object>();
                        so.Initialize();

                        ChangeVector cv_coffee = new ChangeVector(new List<GameObject>() { coffee.gameObject },
                        0.0f, 5.0f, TargetProperty.Scale, true, new Vector3(1.0f, 0.0f, 1.0f), new Vector3(0.0f, 1.0f, 0.0f), 0.0f,
                        InterruptBehavior.Ignore, InterruptBehavior.Stop);

                        List_TB tb = new List_TB() { cv_coffee };

                        ActivationSwitch swch_button = new ActivationSwitch(HandPose.Button, ActivationAction.SwitchOn,
                        Vector3.zero, tb, null, button);

                        HandInteraction hi_coffeeMaker = a.gameObject.AddComponent<HandInteraction>();
                        hi_coffeeMaker.switches = new HandInteraction.ActivationSwitch[1] { swch_button };
                        hi_coffeeMaker.Initialize();
                    }
                } else if (a.name.Contains(TOASTER) && a.gameObject.GetComponent<HandInteraction>() == null) {
                    const float duration = 5.5f;
                    const float enterPrd = 0.35f;
                    const float extPrd = 0.1f;
                    Vector4 burntBreadDelta = new Vector4(-0.39f, -0.235f, -0.078f);

                    State_object so = a.gameObject.AddComponent<State_object>();
                    so.Initialize();

                    Transform bread_1 = SpawnPrefab(STR_PATH.BREAD, a,
                      new Vector3(-0.0096f, 0.1967f, 0.0417f), new Vector3(-148.414f, -90.0f, 90.0f));
                    Transform bread_2 = SpawnPrefab(STR_PATH.BREAD, a,
                      new Vector3(-0.0096f, 0.1967f, -0.0278f), new Vector3(-148.414f, -90.0f, 90.0f));

                    List<GameObject> list_breads = new List<GameObject>() { bread_1.gameObject, bread_2.gameObject };

                    Transform lever = a.Find(TOASTER_LEVER);

                    TransitionSequence ts_lever = SwitchUtilities.Create2ChangeVector_sequence(
                      new List<GameObject>() { lever.gameObject }, 0.0f, enterPrd, duration, extPrd,
                      TargetProperty.Position, new Vector3(0.0f, -0.0874f, 0.0f), 0.95f,
                      InterruptBehavior.Ignore, InterruptBehavior.Ignore,
                      InterruptBehavior.Immediate, InterruptBehavior.Ignore, Link.Auto);
                    TransitionSequence ts_bread_pos_1 = SwitchUtilities.Create2ChangeVector_sequence(
                      new List<GameObject>() { bread_1.gameObject }, 0.0f, enterPrd, duration, extPrd,
                      TargetProperty.Position, new Vector3(-0.0003000025f, 0.145f, 0.03789f) - bread_1.localPosition, 0.0f,
                      InterruptBehavior.Ignore, InterruptBehavior.Ignore,
                      InterruptBehavior.Immediate, InterruptBehavior.Ignore, Link.Auto);
                    TransitionSequence ts_bread_pos_2 = SwitchUtilities.Create2ChangeVector_sequence(
                      new List<GameObject>() { bread_2.gameObject }, 0.0f, enterPrd, duration, extPrd,
                      TargetProperty.Position, new Vector3(-0.0f, 0.145f, -0.038f) - bread_2.localPosition, 0.0f,
                      InterruptBehavior.Ignore, InterruptBehavior.Ignore,
                      InterruptBehavior.Immediate, InterruptBehavior.Ignore, Link.Auto);

                    ChangeColor cc_bread = new ChangeColor(list_breads, enterPrd, duration, false,
                      Vector4.zero, burntBreadDelta, InterruptBehavior.Ignore, InterruptBehavior.Stop);
                    // Add place holder at the end because The other TransitionSequence has 2 TransitonBases.
                    TransitionBase tb_doNothing = new TransitionBase(null, 0.0f, 0.0f,
                      InterruptBehavior.Ignore, InterruptBehavior.Ignore);
                    List_TB tb_bread_clr = new List_TB() { cc_bread, tb_doNothing };
                    TransitionSequence ts_bread_clr = new TransitionSequence(tb_bread_clr, new List_Link() { Link.Auto });

                    List_TS ts = new List_TS() { ts_lever, ts_bread_pos_1, ts_bread_pos_2, ts_bread_clr };

                    ActivationSwitch swch = new ActivationSwitch(HandPose.Button, ActivationAction.SwitchOn,
                      new Vector3(-0.02f, 0.0f, 0.0f), ts, lever);

                    HandInteraction hi = a.gameObject.AddComponent<HandInteraction>();
                    hi.switches = new HandInteraction.ActivationSwitch[1] { swch };

                    hi.Initialize();
                } else if (a.name.Contains(WASHING_MACHINE)) {
                    AddNavMeshObstacle(a.gameObject, Vector3.zero, Vector3.zero);
                }
            }
        }

        class Lamps : ObjGroupBase
        {
            const string GROUP_NAME = "Lamps";

            const string TABLE_LAMP = "Table_lamp";

            public Lamps(Transform room) : base(GROUP_NAME, room, false) { }

            public override void ProcessItem(Transform e)
            {
                if (e.gameObject.GetComponent<HandInteraction>() == null) {
                    if (e.name.Contains(TABLE_LAMP)) {
                        State_object so = e.gameObject.AddComponent<State_object>();
                        so.Initialize();

                        List<GameObject> list_lightSources = new List<GameObject>();

                        for (int j = 0; j < e.childCount; j++) {
                            Transform t = e.GetChild(j);
                            if (t.name.Equals(LAMPS.LIGHT_SOURCE)) {
                                list_lightSources.Add(t.gameObject);
                            }
                        }
                        AddLampSwitch(e.gameObject, list_lightSources);
                    }
                }
            }
        }

        class Electronics : ObjGroupBase
        {
            const string GROUP_NAME = "Electronics";

            const string MONITOR = "CPU_screen";
            const string DESKTOP = "CPU_case";
            const string LIGHTSWITCH = "Light_switch";
            const string WALL_LAMP = "Wall_lamp";
            const string CEILING_LAMP = "Ceiling_lamp";
            const string TABLE_LAMP = "Table_lamp";
            const string WALL_PHONE = "Wall_phone";
            const string TV_1 = "ELE_TV_01";
            const string TV_2 = "ELE_TV_02";
            const string RADIO_1 = "Radio_1";
            const string RADIO_2 = "Radio_2";
            const string RADIO_4 = "Radio_4";
            const string RADIO_5 = "Radio_5";
            const string RADIO_6 = "Radio_6";


            public Electronics(Transform room) : base(GROUP_NAME, room, false) { }

            public override void ProcessItem(Transform e)
            {
                if (e.gameObject.GetComponent<HandInteraction>() == null) {
                    if (e.name.Contains(DESKTOP)) {
                        State_object so = e.gameObject.AddComponent<State_object>();
                        so.Initialize();

                        // Find matching monitor(s)
                        List<Transform> list_monitors = new List<Transform>();
                        //for (int i = 0; i < tsfm_group.childCount; i++) {
                        //    if (tsfm_group.GetChild(i).name.Contains(MONITOR)) {
                        //        list_monitors.Add(tsfm_group.GetChild(i));
                        //    }
                        //}
                        for (int i = 0; i < e.parent.childCount; i++) {
                            if (e.parent.GetChild(i).name.Contains(MONITOR)) {
                                list_monitors.Add(e.parent.GetChild(i));
                            }
                        }

                        Debug.Assert(list_monitors.Count != 0, "Cannot find matching monitor for desktop");

                        // Sprite Renderer's bounds change after instantiation. So get the correct bound
                        // from the prefab.
                        GameObject prefab_monitorScreen = Resources.Load(STR_PATH.MONITOR_SCREEN) as GameObject;
                        Vector3 screenSize = prefab_monitorScreen.GetComponent<SpriteRenderer>().bounds.size;
                        List<GameObject> list_screens = new List<GameObject>(list_monitors.Count);

                        foreach (Transform tsfm in list_monitors) {
                            // Adds screen to the monitor that can be toggled
                            Transform tsfm_screen = SpawnPrefab(STR_PATH.MONITOR_SCREEN, tsfm,
                              new Vector3(0.06f, 0.406f, -0.002f), new Vector3(23.848f, -90.0f, 0.0f), true);

                            // Assign random sprite (image/texture) to the screen
                            AssignRandomSprite(tsfm_screen, screenSize, m_sprites_compScreen);
                            list_screens.Add(tsfm_screen.gameObject);
                        }

                        Transform desktopLight = SpawnPrefab(STR_PATH.DESKTOP_LIGHT, e,
                          new Vector3(0.3742f, 0.1854f, -0.0896f), Vector3.zero, true);

                        Toggle tgl_desktopLight = new Toggle(new List<GameObject>() { desktopLight.gameObject });
                        TransitionSequence ts_desktopLight = new TransitionSequence(new List_TB() { tgl_desktopLight });

                        Toggle tgl_on = new Toggle(list_screens, InteractionConst.OPEN_ENTER_PRD_MED);
                        Toggle tgl_off = new Toggle(list_screens, InteractionConst.OPEN_EXIT_PRD_MED);
                        List_TB tb = new List_TB() { tgl_on, tgl_off };
                        List_Link links = new List_Link() { Link.Manual };
                        TransitionSequence ts_screen = new TransitionSequence(tb, links);

                        List_TS ts = new List_TS() { ts_desktopLight, ts_screen };

                        ActivationSwitch swch = new ActivationSwitch(HandPose.Button, ActivationAction.SwitchOn,
                          new Vector3(0.37f, 0.7f, 0.0f), ts);

                        HandInteraction hi = e.gameObject.AddComponent<HandInteraction>();
                        hi.switches = new HandInteraction.ActivationSwitch[1] { swch };

                        hi.Initialize();
                    } else if (e.name.Contains(LIGHTSWITCH)) {
                        State_object so = e.gameObject.AddComponent<State_object>();
                        so.Initialize();

                        Transform lamps = e.parent.parent.Find(LAMPS.GROUP_NAME);

                        Debug.Assert(lamps != null, "Cannot find the lamp objects");

                        List<GameObject> list_lightSources = new List<GameObject>();
                        for (int i = 0; i < lamps.childCount; i++) {
                            Transform lamp = lamps.GetChild(i);

                            if (lamp.name.Contains(CEILING_LAMP) || lamp.name.Contains(WALL_LAMP)) {
                                for (int j = 0; j < lamp.childCount; j++) {
                                    Transform t = lamp.GetChild(j);
                                    if (t.name.Equals(LAMPS.LIGHT_SOURCE)) {
                                        list_lightSources.Add(t.gameObject);
                                    }
                                }
                            }
                        }

                        // Find reflection probe, if exists. It tends to make things brighter.
                        Transform rp = e.parent.parent.Find(LAMPS.REFLECTION_PROBE);
                        if (rp != null) {
                            list_lightSources.Add(rp.gameObject);
                        }

                        Toggle tgl = new Toggle(list_lightSources);
                        TransitionSequence ts_light = new TransitionSequence(new List_TB() { tgl });

                        // When lights are toggled, the emission property of emissive materials
                        // must be toggled as well.
                        ToggleEmission tgl_emsn = new ToggleEmission(new List<GameObject>() { lamps.gameObject });
                        TransitionSequence ts_emsn = new TransitionSequence(new List_TB() { tgl_emsn });

                        ActivationSwitch swch = new ActivationSwitch(HandPose.Button, ActivationAction.SwitchOn,
                          new Vector3(0.0f, 0.02f, 0.0f), new List_TS() { ts_light, ts_emsn });

                        HandInteraction hi = e.gameObject.AddComponent<HandInteraction>();
                        hi.switches = new HandInteraction.ActivationSwitch[1] { swch };
                        hi.Initialize();
                    } else if (e.name.Contains(WALL_PHONE)) {
                        HandInteraction hi = e.gameObject.AddComponent<HandInteraction>();
                        hi.allowPickUp = true;
                        hi.grabHandPose = HandInteraction.HandPose.GrabVertical;
                        hi.objectToBePickedUp = e.GetChild(0);
                    } else if (e.name.Contains(TV_1) || e.name.Contains(TV_2)) {
                        State_object so = e.gameObject.AddComponent<State_object>();
                        so.Initialize();

                        Transform TVScreen = SpawnPrefab(STR_PATH.TV_SCREEN, e,
                          new Vector3(0.1091432f, 0.7357803f, 0.0f), new Vector3(90.0f, 90.0f, 0.0f), true);

                        if (m_videoClips_tv != null) {
                            AssignRandomVideo(TVScreen, Vector3.zero, m_videoClips_tv);
                        } else {
                            // Sprite Renderer's bounds change after instantiation. So get the correct bound
                            // from the prefab.
                            GameObject prefab_TVScreen = Resources.Load(STR_PATH.TV_SCREEN) as GameObject;
                            Vector3 screenSize = prefab_TVScreen.GetComponent<SpriteRenderer>().bounds.size;
                            AssignRandomSprite(TVScreen, screenSize, m_sprites_tv);
                        }

                        List_TS ts = SwitchUtilities.CreateToggle(new List<GameObject>() { TVScreen.gameObject });

                        ActivationSwitch swch = new ActivationSwitch(HandPose.Button, ActivationAction.SwitchOn,
                          new Vector3(0.12f, 0.2f, 0.8f), ts);

                        HandInteraction hi = e.gameObject.AddComponent<HandInteraction>();
                        hi.switches = new HandInteraction.ActivationSwitch[1] { swch };

                        hi.Initialize();
                    } else if (e.name.Contains(RADIO_1)) {
                        AddPhantomSwitch(e.gameObject, new Vector3(0.0f, 0.0656f, 0.0829f));
                    } else if (e.name.Contains(RADIO_2)) {
                        AddPhantomSwitch(e.gameObject, new Vector3(0.1553f, 0.084f, 0.0961f));
                    } else if (e.name.Contains(RADIO_4)) {
                        AddPhantomSwitch(e.gameObject, new Vector3(0.0f, 0.2536f, 0.1651f));
                    } else if (e.name.Contains(RADIO_5)) {
                        AddPhantomSwitch(e.gameObject, new Vector3(0.2038f, 0.0507f, 0.1607f));
                    } else if (e.name.Contains(RADIO_6)) {
                        AddPhantomSwitch(e.gameObject, new Vector3(-0.0813f, 0.1751f, 0.15058f));
                    }
                }
            }

            static void AssignRandomSprite(Transform tsfm, Vector3 screenSize, Sprite[] arry_sprite)
            {
                Sprite newSprite = arry_sprite[Random.Range(0, arry_sprite.Length)];
                SpriteRenderer sr = tsfm.GetComponent<SpriteRenderer>();
                sr.sprite = newSprite;
                Vector3 spriteSize = newSprite.bounds.size;
                tsfm.localScale = new Vector3(screenSize.x / spriteSize.x, screenSize.y / spriteSize.y, 1);
            }

            static void AssignRandomVideo(Transform tsfm, Vector3 screenSize, VideoClip[] arry_video)
            {
                VideoClip newClip = arry_video[Random.Range(0, arry_video.Length)];
                VideoPlayer vp = tsfm.GetComponent<VideoPlayer>();
                vp.clip = newClip;
            }

            static void AddPhantomSwitch(GameObject go, Vector3 pos)
            {
                State_object so = go.AddComponent<State_object>();
                so.Initialize();

                ActivationSwitch phantomSwitch = new ActivationSwitch(HandPose.Button, ActivationAction.SwitchOn, pos, null);
                HandInteraction hi = go.AddComponent<HandInteraction>();
                hi.switches = new HandInteraction.ActivationSwitch[1]
                {
                    phantomSwitch
                };

                hi.Initialize();
            }
        }

        class Doors : ObjGroupBase
        {
            const string GROUP_NAME = "Doors";

            const string DOORJAMB = "Doorjamb";
            const string DOOR_NAME = "Door";
            const string DOOR_MAIN = "Main";


            public Doors(Transform room) : base(GROUP_NAME, room) { }

            public override void ProcessItem(Transform d)
            {
                // Don't annotate the main door since opening/closing will expose 
                // skybox, which is not what we want currently.
                if (d.name.Contains(DOOR_NAME) && !d.name.Contains(DOORJAMB) && !d.name.Contains(DOOR_MAIN)) {
                    Vector3[] centers = new Vector3[] { new Vector3(-0.034f, 0.03f, 0.434f), new Vector3(-0.034f, 0.03f, 0.7f) };
                    Vector3[] sizes = new Vector3[] { new Vector3(0.115f, 2.028f, 0.878f), new Vector3(0.36f, 2.028f, 0.17f) };
                    //AddNavMeshObstacles(d, centers, sizes);
                    d.gameObject.AddComponent<Properties_door>();
                }
            }
        }

        class Decors : ObjGroupBase
        {
            const string GROUP_NAME = "Decor";

            public Decors(Transform room) : base(GROUP_NAME, room, false) { }

            public override void ProcessItem(Transform d)
            {
                const string RUG = "Rug";

                // Disable all colliders on a rug since it might interfere with door interaction.
                // For example, a rug on floor can prevent door from opening/closing.
                if (d.name.Contains(RUG)) {
                    foreach (Collider c in d.GetComponentsInChildren<Collider>()) {
                        c.enabled = false;
                    }
                }
            }
        }

        static class SwitchUtilities
        {
            public static List_TS Create2ChangeVector_list(List<GameObject> targets,
              float delay1, float delay2, float duration1, float duration2,
              TargetProperty tp, Vector3 delta, float roi_delta,
              InterruptBehavior ib1_delay, InterruptBehavior ib1_transition,
              InterruptBehavior ib2_delay, InterruptBehavior ib2_transition, Link link)
            {
                return new List_TS() { Create2ChangeVector_sequence(targets, delay1, duration1, delay2, duration2,
                  tp, delta, roi_delta, ib1_delay, ib1_transition, ib2_delay, ib2_transition, link) };
            }

            public static TransitionSequence Create2ChangeVector_sequence(List<GameObject> targets,
              float delay1, float duration1, float delay2, float duration2,
              TargetProperty tp, Vector3 delta, float roi_delta,
              InterruptBehavior ib1_delay, InterruptBehavior ib1_transition,
              InterruptBehavior ib2_delay, InterruptBehavior ib2_transition, Link link)
            {
                ChangeVector cv1 = new ChangeVector(targets, delay1, duration1, tp, false,
                    Vector3.zero, delta, roi_delta, ib1_delay, ib1_transition);
                ChangeVector cv2 = new ChangeVector(targets, delay2, duration2, tp, false,
                    Vector3.zero, -delta, roi_delta, ib2_delay, ib2_transition);

                List_TB tb = new List_TB() { cv1, cv2 };
                List_Link links = new List_Link() { link };

                return new TransitionSequence(tb, links);
            }

            public static List_TS CreateToggle(List<GameObject> targets, float delay = 0.0f,
              InterruptBehavior ib_delay = InterruptBehavior.Ignore)
            {
                Toggle tgl = new Toggle(targets, delay, ib_delay);
                List_TB tb = new List_TB() { tgl };
                return new List_TS() { new TransitionSequence(tb, null) };
            }
        }

        static class LAMPS
        {
            public const string GROUP_NAME = "Lamps";
            public const string LIGHT_SOURCE = "Point light";
            public const string REFLECTION_PROBE = "Reflection Probe";
        }

        static class STR_PATH
        {
            public const string MONITOR_SCREEN = "PrefabsForInteractables/Prefabs/WindowsLockScreen";
            public const string DESKTOP_LIGHT = "PrefabsForInteractables/Prefabs/DesktopLight";
            public const string TV_SCREEN = "PrefabsForInteractables/Prefabs/TVScreen";
            public const string STOVE_COIL = "PrefabsForInteractables/Prefabs/StoveCoil";
            public const string WATER_STREAM = "PrefabsForInteractables/Prefabs/WaterStream";
            public const string BREAD = "ExtraObjects/Bread_slice_1/Bread_slice_1";
            public const string SPRITE_COMP_SCREEN = "PrefabsForInteractables/Textures/laptop";
            public const string VIDEO_TV = "PrefabsForInteractables/Videos/tv";
            public const string SPRITE_COMP_TV = "PrefabsForInteractables/Textures/tv";
        }

        // constants (will have randomnoise if randomize option is enabled)
        static class InteractionConst
        {
            public static readonly float OPEN_ROI_DELTA = 0.1f;
            public static Vector3 DRAWER_OPEN_AMOUNT = new Vector3(0.44f, 0.0f, 0.0f);

            public static float DOOR_OPEN_DEGREES = -50.0f;
            public static float OPEN_ENTER_PRD_MED = 1.0f;
            public static float OPEN_EXIT_PRD_MED = 1.0f;
            public static float OPEN_ENTER_PRD_LONG = 1.7f;
            public static float OPEN_EXIT_PRD_LONG = 1.7f;

            public static void Reset()
            {
                DRAWER_OPEN_AMOUNT = new Vector3(0.44f, 0.0f, 0.0f);
                DOOR_OPEN_DEGREES = -50.0f;
                OPEN_ENTER_PRD_MED = 1.0f;
                OPEN_EXIT_PRD_MED = 1.0f;
                OPEN_ENTER_PRD_LONG = 1.7f;
                OPEN_EXIT_PRD_LONG = 1.7f;
            }
        }

        static bool shouldRandomize;

        // Cache Sprites for Computer Screen and TV screen
        static Sprite[] m_sprites_compScreen;
        static Sprite[] m_sprites_tv;
        static VideoClip[] m_videoClips_tv;

        // List of GameObjects to disable
        static List<GameObject> m_GO_toDisable = new List<GameObject>();

        #region PublicMethods
        public static void ProcessHome(Transform h, bool _shouldRandomize)
        {
#if UNITY_EDITOR
            UnityEditor.AI.NavMeshBuilder.ClearAllNavMeshes();
            UnityEditor.AI.NavMeshBuilder.BuildNavMesh();
#endif
            shouldRandomize = _shouldRandomize;
            CacheSpritesAndVids();
            GlobalRandomization(h);

            m_GO_toDisable.Clear();

            for (int i = 0; i < h.childCount; i++) {
                Transform room = h.GetChild(i);
                Walls walls = new Walls(room);
                Furnitures frntrs = new Furnitures(room);
                Appliances applns = new Appliances(room);
                Electronics elecns = new Electronics(room);
                Lamps lmps = new Lamps(room);
                Doors drs = new Doors(room);
                Decors dcr = new Decors(room);

                walls.Process();
                frntrs.Process();
                applns.Process();
                elecns.Process();
                lmps.Process();
                drs.Process();
                dcr.Process();
            }
        }

        // Used after placing new object in scene preparation
        public static void ProcessItem(Transform t)
        {
            m_GO_toDisable.Clear();
            Transform roomTr = GameObjectUtils.GetRoomTransform(t);

            if (roomTr != null) {
                Furnitures frntrs = new Furnitures(roomTr);
                Appliances applns = new Appliances(roomTr);
                Electronics elecns = new Electronics(roomTr);
                Lamps lmps = new Lamps(roomTr);
                Doors drs = new Doors(roomTr);
                Decors dcr = new Decors(roomTr);
                frntrs.ProcessItem(t);
                applns.ProcessItem(t);
                elecns.ProcessItem(t);
                lmps.ProcessItem(t);
                drs.ProcessItem(t);
                dcr.ProcessItem(t);
            }
            HomeAnnotator.PostColorEncoding_DisableGameObjects();
        }

        // Some GameObjects must be disabled to that every GameObjects have default
        // state of "power off" state.
        public static void PostColorEncoding_DisableGameObjects()
        {
            if (m_GO_toDisable != null && m_GO_toDisable.Count > 0) {
                foreach (GameObject go in m_GO_toDisable) {
                    go.SetActive(false);
                }
            }
        }

        #endregion

        #region HelperMethods
        static void CacheSpritesAndVids()
        {
            if (m_sprites_compScreen == null) {
                m_sprites_compScreen = Resources.LoadAll<Sprite>(STR_PATH.SPRITE_COMP_SCREEN);
                Debug.Assert(m_sprites_compScreen != null && m_sprites_compScreen.Length != 0,
                  "Computer screen sprite load error");
            }
            if (m_videoClips_tv == null) {
                m_videoClips_tv = Resources.LoadAll<VideoClip>(STR_PATH.VIDEO_TV);
            }
            if (m_sprites_tv == null) {
                m_sprites_tv = Resources.LoadAll<Sprite>(STR_PATH.SPRITE_COMP_TV);
                Debug.Assert(m_sprites_tv != null && m_sprites_tv.Length != 0,
                  "TV screen sprite load error");
            }
        }

        static void GlobalRandomization(Transform h)
        {
            if (shouldRandomize) {
                // Randomize all light values
                Light[] allLights = h.GetComponentsInChildren<Light>();
                foreach (Light l in allLights) {
                    l.intensity *= Random.Range(0.9f, 1.2f);
                }

                InteractionConst.Reset();

                // Add noise to interaction values
                InteractionConst.DRAWER_OPEN_AMOUNT.x += Random.Range(-1.0f, 0.4f);
                InteractionConst.DOOR_OPEN_DEGREES *= Random.Range(0.9f, 1.15f);
                InteractionConst.OPEN_ENTER_PRD_MED *= Random.Range(0.8f, 1.2f);
                InteractionConst.OPEN_EXIT_PRD_MED *= Random.Range(0.8f, 1.2f);
                InteractionConst.OPEN_ENTER_PRD_LONG *= Random.Range(0.85f, 1.15f);
                InteractionConst.OPEN_EXIT_PRD_LONG *= Random.Range(0.85f, 1.15f);
            }
        }

        static void AddNavMeshObstacles(Transform parent, Vector3[] centers, Vector3[] sizes)
        {
            const string STR_NMO_HOLDER = "NavMeshObstacles";
            const string STR_NMO = "NavMeshObstacle";

            NavMeshObstacle oldNmo = parent.GetComponentInChildren<NavMeshObstacle>();

            if (oldNmo != null)
                return;

            Transform nmo_holder = new GameObject(STR_NMO_HOLDER).transform;
            nmo_holder.SetParent(parent, false);

            for (int i = 0; i < centers.Length; i++) {
                GameObject go = new GameObject(STR_NMO);
                go.transform.SetParent(nmo_holder, false);
                AddNavMeshObstacle(go, centers[i], sizes[i]);
            }
        }

        static void AddNavMeshObstacle(GameObject go, Vector3 center, Vector3 size)
        {
            NavMeshObstacle oldNmo = go.GetComponentInChildren<NavMeshObstacle>();

            if (oldNmo != null)
                return;

            NavMeshObstacle nmo = go.AddComponent<NavMeshObstacle>();
            if (center != Vector3.zero) {
                nmo.center = center;
            }
            if (size != Vector3.zero) {
                nmo.size = size;
            }

            nmo.carving = true;
            nmo.carvingTimeToStationary = 0.0f;
            nmo.carveOnlyStationary = false;
        }

        static void AddLampSwitch(GameObject lamp, List<GameObject> list_lightSources)
        {
            Toggle tgl = new Toggle(list_lightSources);
            TransitionSequence ts_light = new TransitionSequence(new List_TB() { tgl });

            ActivationSwitch swch = new ActivationSwitch(HandPose.Button, ActivationAction.SwitchOn,
              new Vector3(0.0f, 0.02f, 0.0f), new List_TS() { ts_light });

            HandInteraction hi = lamp.AddComponent<HandInteraction>();
            hi.switches = new HandInteraction.ActivationSwitch[1] { swch };
            hi.Initialize();
        }

        static Transform SpawnPrefab(string prefabPath, Transform tsfm_parent,
            Vector3 localPos, Vector3 localRot, bool shouldDisabled = false)
        {
            GameObject prefab = Resources.Load(prefabPath) as GameObject;
            Transform tsfm = Object.Instantiate(prefab).transform;
            // Rename the instantiated object to remove suffix (like (1))
            // for proper segmentation
            tsfm.name = prefab.name;
            tsfm.SetParent(tsfm_parent, false);
            tsfm.localPosition = localPos;
            if (localRot != Vector3.zero) {
                tsfm.localRotation = Quaternion.Euler(localRot);
            }
            if (shouldDisabled) {
                m_GO_toDisable.Add(tsfm.gameObject);
            }

            return tsfm;
        }

        #endregion

    }
}

