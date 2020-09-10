using UnityEngine;

using System.Collections.Generic;
using System.Linq;
using StoryGenerator.CharInteraction;
using UnityEngine.AI;
using StoryGenerator.Helpers;
using StoryGenerator.SceneState;
using StoryGenerator.HomeAnnotation;

namespace StoryGenerator.SpecialBehavior
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
    using TransitionBase = StoryGenerator.CharInteraction.HandInteraction.TransitionBase;

    using InterruptBehavior = StoryGenerator.CharInteraction.HandInteraction.TransitionBase.InterruptBehavior;

    using List_TB = List<StoryGenerator.CharInteraction.HandInteraction.TransitionBase>;
    using List_TS = List<StoryGenerator.CharInteraction.HandInteraction.TransitionSequence>;
    using List_Link = List<StoryGenerator.CharInteraction.HandInteraction.TransitionSequence.TransitionType>;

    public interface PrefabBehavior
    {
        void Process(Transform tsfm);
    }

    public class FridgeBehavior : PrefabBehavior
    {
        public void Process(Transform tsfm)
        {
            const string FRIDGE_DOOR_1 = "MOD_APP_Fridge_door_01";
            const string FRIDGE_DOOR_2 = "MOD_APP_Fridge_door_02";
            const int FRIDGE_NUM_DRAWERS = 3;
            const string FRIDGE_DRAWER = "MOD_APP_Fridge_drawer_0";

            const float DELAY_LIGHT_ON = 0.2f;
            const float DELAY_LIGHT_OFF = 0.8f;
            List_Link links = new List_Link() { Link.Manual };

            State_object so = tsfm.gameObject.AddComponent<State_object>();
            so.Initialize();

            // Need to turn off the light of the fridge in the beginnig to minimize
            // its effect on scene lighting. It will turn back on when the door is opened
            GameObject go_pl = tsfm.Find(UtilsAnnotator.LAMPS.LIGHT_SOURCE).gameObject;
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
            List<GameObject> list_fridge = new List<GameObject>() { tsfm.gameObject };
            ToggleEmission tglEmsn_open = new ToggleEmission(list_fridge, DELAY_LIGHT_ON, InterruptBehavior.Stop);
            tglEmsn_open.Flip();
            ToggleEmission tglEmsn_close = new ToggleEmission(list_fridge, DELAY_LIGHT_OFF, InterruptBehavior.Stop);
            List_TB tb_tglEmsn = new List_TB() { tglEmsn_open, tglEmsn_close };
            TransitionSequence ts_tglEmsn = new TransitionSequence(tb_tglEmsn, links);

            // Make white interior less bright since it's reflecting too much light on some scenes.
            string[] WHITE_INTERIOR = new string[] { "Plastic_02_02" };
            foreach (Material m in Helper.FindMatNameMatch(list_fridge, WHITE_INTERIOR))
            {
                const float REDUCE_AMOUNT = 0.58f;
                m.color = new Color(m.color.r * REDUCE_AMOUNT, m.color.g * REDUCE_AMOUNT,
                  m.color.b * REDUCE_AMOUNT, m.color.a);
            }

            Transform tsfm_door1 = tsfm.Find(FRIDGE_DOOR_1);
            TransitionSequence ts_cv_door1 = SwitchUtilities.Create2ChangeVector_sequence(new List<GameObject> { tsfm_door1.gameObject },
              0.0f, InteractionConst.OPEN_ENTER_PRD_MED, 0.0f, InteractionConst.OPEN_EXIT_PRD_MED, TargetProperty.Rotation,
              new Vector3(0.0f, InteractionConst.DOOR_OPEN_DEGREES), InteractionConst.OPEN_ROI_DELTA,
              InterruptBehavior.Ignore, InterruptBehavior.Revert,
              InterruptBehavior.Ignore, InterruptBehavior.Revert, Link.Manual);
            List_TS ts_door1 = new List_TS() { ts_cv_door1, ts_tgl, ts_tglEmsn };
            ActivationSwitch door1 = new ActivationSwitch(HandPose.GrabHorizontalSmall, ActivationAction.Open,
              new Vector3(0.12f, -0.48f, -0.68f), ts_door1, tsfm_door1);

            Transform tsfm_door2 = tsfm.Find(FRIDGE_DOOR_2);
            List_TS ts_door2 = SwitchUtilities.Create2ChangeVector_list(new List<GameObject>() { tsfm_door2.gameObject },
              0.0f, 0.0f, InteractionConst.OPEN_ENTER_PRD_MED, InteractionConst.OPEN_EXIT_PRD_MED, TargetProperty.Rotation,
              new Vector3(0.0f, InteractionConst.DOOR_OPEN_DEGREES), InteractionConst.OPEN_ROI_DELTA,
              InterruptBehavior.Ignore, InterruptBehavior.Revert,
              InterruptBehavior.Ignore, InterruptBehavior.Revert, Link.Manual);
            HandInteraction.ActivationSwitch door2 = new HandInteraction.ActivationSwitch(HandPose.GrabHorizontalSmall,
              ActivationAction.Open, new Vector3(0.12f, 0.37f, -0.68f), ts_door2, tsfm_door2);

            HandInteraction hi = tsfm.gameObject.AddComponent<HandInteraction>();
            hi.switches = new List<HandInteraction.ActivationSwitch>();
            hi.switches.Add(door1);
            hi.switches.Add(door2);


            for (int j = 0; j < FRIDGE_NUM_DRAWERS; j++)
            {
                Transform drawer = tsfm.Find(FRIDGE_DRAWER + (j + 1));
                List_TS ts_drawer = SwitchUtilities.Create2ChangeVector_list(new List<GameObject>() { drawer.gameObject },
                  0.0f, 0.0f, InteractionConst.OPEN_ENTER_PRD_MED, InteractionConst.OPEN_EXIT_PRD_MED,
                  TargetProperty.Position, InteractionConst.DRAWER_OPEN_AMOUNT, InteractionConst.OPEN_ROI_DELTA,
                  InterruptBehavior.Ignore, InterruptBehavior.Revert,
                  InterruptBehavior.Ignore, InterruptBehavior.Revert, Link.Manual);

                HandInteraction.ActivationSwitch swtch = new HandInteraction.ActivationSwitch(HandPose.GrabHorizontalSmall,
                  ActivationAction.Open, new Vector3(0.32f, 0.11f, 0.0f), ts_drawer, drawer);

                hi.switches.Add(swtch);
            }

            hi.Initialize();

        }
    }
    public class StoveBehavior : PrefabBehavior
    {
        public void Process(Transform tsfm)
        {
            const int STOVE_NUM_COILS = 4;
            const string OVEN_DOOR = "MOD_APP_Oven_door_01";
            const string OVEN_TRAY = "MOD_APP_Oven_tray_01";


            UtilsAnnotator.AddNavMeshObstacle(tsfm.gameObject, new Vector3(-0.075f, 0.51f, 0.0f), new Vector3(0.832f, 1.08f, 1.002f));

            if (tsfm.gameObject.GetComponent<HandInteraction>() == null)
            {
                State_object so = tsfm.gameObject.AddComponent<State_object>();
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

                HandInteraction hi = tsfm.gameObject.AddComponent<HandInteraction>();
                hi.switches = new List<HandInteraction.ActivationSwitch>();
                //new HandInteraction.ActivationSwitch[STOVE_NUM_COILS + 1];

                for (int j = 0; j < STOVE_NUM_COILS; j++)
                {
                    const float COIL_HEAT_UP = 3.0f;
                    const float COIL_COOL_DOWN = 4.0f;
                    Vector4 COLOR_DELTA = new Vector4(0.0f, 0.0f, 0.0f, 0.7f);

                    Transform stoveCoil = UtilsAnnotator.SpawnPrefab(STR_PATH.STOVE_COIL, tsfm, coilLocations[j], Vector3.zero);
                    List<GameObject> list_coil = new List<GameObject>() { stoveCoil.gameObject };

                    ChangeColor cc1 = new ChangeColor(list_coil, 0.0f, COIL_HEAT_UP, false, Vector4.zero,
                      COLOR_DELTA, InterruptBehavior.Ignore, InterruptBehavior.Revert);

                    ChangeColor cc2 = new ChangeColor(list_coil, 0.0f, COIL_COOL_DOWN, false, Vector4.zero,
                      -COLOR_DELTA, InterruptBehavior.Ignore, InterruptBehavior.Revert);

                    List_TB tb = new List_TB() { cc1, cc2 };
                    List_Link links = new List_Link() { Link.Manual };

                    ActivationSwitch swtch = new ActivationSwitch(HandPose.GrabVerticalSmall,
                      ActivationAction.SwitchOn, switchPositions[j], tb, links);

                    hi.switches.Add(swtch);
                }

                Transform ovenDoor = tsfm.Find(OVEN_DOOR);

                List_TS ts = SwitchUtilities.Create2ChangeVector_list(new List<GameObject>() { ovenDoor.gameObject },
                  0.0f, 0.0f, InteractionConst.OPEN_ENTER_PRD_MED, InteractionConst.OPEN_EXIT_PRD_MED, TargetProperty.Rotation,
                  new Vector3(0.0f, 0.0f, InteractionConst.DOOR_OPEN_DEGREES), InteractionConst.OPEN_ROI_DELTA,
                  InterruptBehavior.Ignore, InterruptBehavior.Revert,
                  InterruptBehavior.Ignore, InterruptBehavior.Revert, Link.Manual);

                ActivationSwitch ovenDoorSwitch = new ActivationSwitch(HandPose.GrabHorizontalSmall,
                  ActivationAction.Open, new Vector3(0.03f, 0.48f, 0.0f), ts, ovenDoor);

                hi.switches.Add(ovenDoorSwitch);
                hi.Initialize();

                GameObject tray = tsfm.Find(OVEN_TRAY).gameObject;
                if (tray != null)
                {
                    so = tsfm.gameObject.AddComponent<State_object>();
                    so.Initialize();

                    hi = tray.AddComponent<HandInteraction>();
                    hi.allowPickUp = true;
                    hi.grabHandPose = HandInteraction.HandPose.GrabHorizontalSmall;
                    hi.handPosition = new Vector3(0.28f, 0.0f, 0.0f);

                    hi.Initialize();
                }
            }
        }
    }

    public class CoffeeMakerBehavior : PrefabBehavior
    {
        const string COFFEEMAKER_BUTTON = "MOD_PRO_Coffeemaker_01_Button_01";
        const string COFFEE_POT = "MOD_PRO_Coffeemaker_01_Pot_01";
        const string COFFEE_POT_LID = "MOD_PRO_Coffeemaker_01_Lid_01";
        const string COFFEE_POT_COFFEE = "MOD_PRO_Coffeemaker_01_Coffee_01";
        public void Process(Transform tsfm)
        {
            if (tsfm.gameObject.GetComponent<HandInteraction>() == null)
            {
                Transform button = tsfm.Find(COFFEEMAKER_BUTTON);
                Transform pot = tsfm.Find(COFFEE_POT);

                // Annotate only if there's a pot as its child
                if (pot != null)
                {
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
                    hi_lid.switches = new List<HandInteraction.ActivationSwitch>();
                    hi_lid.switches.Add(swch_lid);
                    hi_lid.Initialize();

                    // Annotate coffee maker
                    so = tsfm.gameObject.AddComponent<State_object>();
                    so.Initialize();

                    ChangeVector cv_coffee = new ChangeVector(new List<GameObject>() { coffee.gameObject },
                    0.0f, 5.0f, TargetProperty.Scale, true, new Vector3(1.0f, 0.0f, 1.0f), new Vector3(0.0f, 1.0f, 0.0f), 0.0f,
                    InterruptBehavior.Ignore, InterruptBehavior.Stop);

                    List_TB tb = new List_TB() { cv_coffee };

                    ActivationSwitch swch_button = new ActivationSwitch(HandPose.Button, ActivationAction.SwitchOn,
                    Vector3.zero, tb, null, button);

                    HandInteraction hi_coffeeMaker = tsfm.gameObject.AddComponent<HandInteraction>();
                    hi_coffeeMaker.switches = new List<HandInteraction.ActivationSwitch>();
                    hi_coffeeMaker.switches.Add(swch_button);
                    hi_coffeeMaker.Initialize();


                }
            }
        }
    }

    public class ToasterBehavior : PrefabBehavior
    {
        const string TOASTER = "Toaster_";
        const string TOASTER_LEVER = "MOD_APP_Toaster_01_Lever_01";
        public void Process(Transform tsfm)
        {
            if (tsfm.gameObject.GetComponent<HandInteraction>() == null)
            {
                const float duration = 5.5f;
                const float enterPrd = 0.35f;
                const float extPrd = 0.1f;
                Vector4 burntBreadDelta = new Vector4(-0.39f, -0.235f, -0.078f);

                State_object so = tsfm.gameObject.AddComponent<State_object>();
                so.Initialize();

                Transform bread_1 = UtilsAnnotator.SpawnPrefab(STR_PATH.BREAD, tsfm,
                  new Vector3(-0.0096f, 0.1967f, 0.0417f), new Vector3(-148.414f, -90.0f, 90.0f));
                Transform bread_2 = UtilsAnnotator.SpawnPrefab(STR_PATH.BREAD, tsfm,
                  new Vector3(-0.0096f, 0.1967f, -0.0278f), new Vector3(-148.414f, -90.0f, 90.0f));

                List<GameObject> list_breads = new List<GameObject>() { bread_1.gameObject, bread_2.gameObject };

                Transform lever = tsfm.Find(TOASTER_LEVER);

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

                HandInteraction hi = tsfm.gameObject.AddComponent<HandInteraction>();
                hi.switches = new List<HandInteraction.ActivationSwitch>();
                hi.switches.Add(swch);
                hi.Initialize();
            }
        }
    }

    public class RugBehavior : PrefabBehavior
    {
        public void Process(Transform tsfm)
        {
            // Disable all colliders on a rug since it might interfere with door interaction.
            // For example, a rug on floor can prevent door from opening/closing.
            foreach (Collider c in tsfm.GetComponentsInChildren<Collider>())
            {
                c.enabled = false;
            }
            NavMeshObstacle nmo = tsfm.gameObject.GetComponent<NavMeshObstacle>();
            if (nmo != null)
                nmo.enabled = false;
        }
    }

    public class DesktopBehavior : PrefabBehavior
    {
        const string MONITOR = "CPU_screen";
        const string DESKTOP = "CPU_case";
        public void Process(Transform tsfm)
        {
            if (tsfm.gameObject.GetComponent<HandInteraction>() == null)
            {
                if (tsfm.name.Contains(DESKTOP))
                {
                    State_object so = tsfm.gameObject.AddComponent<State_object>();
                    so.Initialize();

                    // Find matching monitor(s)
                    List<Transform> list_monitors = new List<Transform>();
                    for (int i = 0; i < tsfm.parent.childCount; i++)
                    {
                        if (tsfm.parent.GetChild(i).name.Contains(MONITOR))
                        {
                            list_monitors.Add(tsfm.parent.GetChild(i));
                        }
                    }

                    Debug.Assert(list_monitors.Count != 0, "Cannot find matching monitor for desktop");

                    // Sprite Renderer's bounds change after instantiation. So get the correct bound
                    // from the prefab.
                    GameObject prefab_monitorScreen = Resources.Load(STR_PATH.MONITOR_SCREEN) as GameObject;
                    Vector3 screenSize = prefab_monitorScreen.GetComponent<SpriteRenderer>().bounds.size;
                    List<GameObject> list_screens = new List<GameObject>(list_monitors.Count);

                    foreach (Transform t_monitor in list_monitors)
                    {
                        // Adds screen to the monitor that can be toggled
                        Transform tsfm_screen = UtilsAnnotator.SpawnPrefab(STR_PATH.MONITOR_SCREEN, t_monitor,
                          new Vector3(0.06f, 0.406f, -0.002f), new Vector3(23.848f, -90.0f, 0.0f), true);

                        // Assign random sprite (image/texture) to the screen
                        ElectronicUtils.AssignRandomSprite(tsfm_screen, screenSize, ElectronicUtils.m_sprites_compScreen);
                        list_screens.Add(tsfm_screen.gameObject);
                    }

                    Transform desktopLight = UtilsAnnotator.SpawnPrefab(STR_PATH.DESKTOP_LIGHT, tsfm,
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

                    HandInteraction hi = tsfm.gameObject.AddComponent<HandInteraction>();
                    hi.switches = new List<HandInteraction.ActivationSwitch>();
                    hi.switches.Add(swch);

                    hi.Initialize();
                }
            }
        }
    }

    public class TVBehavior : PrefabBehavior
    {
        public void Process(Transform tsfm)
        {
            if (tsfm.gameObject.GetComponent<HandInteraction>() == null)
            {

                State_object so = tsfm.gameObject.AddComponent<State_object>();
                so.Initialize();

                Transform TVScreen = UtilsAnnotator.SpawnPrefab(STR_PATH.TV_SCREEN, tsfm,
                    new Vector3(0.1091432f, 0.7357803f, 0.0f), new Vector3(90.0f, 90.0f, 0.0f), true);

                if (ElectronicUtils.m_videoClips_tv != null)
                {
                    ElectronicUtils.AssignRandomVideo(TVScreen, Vector3.zero, ElectronicUtils.m_videoClips_tv);
                }
                else
                {
                    // Sprite Renderer's bounds change after instantiation. So get the correct bound
                    // from the prefab.
                    GameObject prefab_TVScreen = Resources.Load(STR_PATH.TV_SCREEN) as GameObject;
                    Vector3 screenSize = prefab_TVScreen.GetComponent<SpriteRenderer>().bounds.size;
                    ElectronicUtils.AssignRandomSprite(TVScreen, screenSize, ElectronicUtils.m_sprites_tv);
                }

                List_TS ts = SwitchUtilities.CreateToggle(new List<GameObject>() { TVScreen.gameObject });

                ActivationSwitch swch = new ActivationSwitch(HandPose.Button, ActivationAction.SwitchOn,
                    new Vector3(0.12f, 0.2f, 0.8f), ts);

                HandInteraction hi = tsfm.gameObject.AddComponent<HandInteraction>();
                hi.switches = new List<HandInteraction.ActivationSwitch>();
                hi.switches.Add(swch);

                hi.Initialize();

            }
        }
    }

    public class LightBehavior : PrefabBehavior
    {

        const string WALL_LAMP = "Wall_lamp";
        const string CEILING_LAMP = "Ceiling_lamp";
        public void Process(Transform tsfm)
        {
            State_object so = tsfm.gameObject.AddComponent<State_object>();
            so.Initialize();

            Transform lamps = tsfm.parent.parent.Find(UtilsAnnotator.LAMPS.GROUP_NAME);

            Debug.Assert(lamps != null, "Cannot find the lamp objects");

            List<GameObject> list_lightSources = new List<GameObject>();
            for (int i = 0; i < lamps.childCount; i++)
            {
                Transform lamp = lamps.GetChild(i);

                if (lamp.name.Contains(CEILING_LAMP) || lamp.name.Contains(WALL_LAMP))
                {
                    for (int j = 0; j < lamp.childCount; j++)
                    {
                        Transform t = lamp.GetChild(j);
                        if (t.name.Equals(UtilsAnnotator.LAMPS.LIGHT_SOURCE))
                        {
                            list_lightSources.Add(t.gameObject);
                        }
                    }
                }
            }

            // Find reflection probe, if exists. It tends to make things brighter.
            Transform rp = tsfm.parent.parent.Find(UtilsAnnotator.LAMPS.REFLECTION_PROBE);
            if (rp != null)
            {
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

            HandInteraction hi = tsfm.gameObject.AddComponent<HandInteraction>();
            hi.switches = new List<HandInteraction.ActivationSwitch>();
            hi.switches.Add(swch);
            hi.Initialize();
        }
    }
    public class FloorCabinetBehavior : PrefabBehavior
    {
        public void Process(Transform tsfm)
        {
            for (int child_id = 0; child_id < tsfm.childCount; child_id++)
            {
                Transform tsf = tsfm.GetChild(child_id);
                if (tsf.gameObject.name.Equals(tsfm.name))
                {
                    tsf.gameObject.name = tsfm.name + "Child";
                }
            }
        }
    }

    public class KitchenCabinetBehavior : PrefabBehavior
    {
        public void Process(Transform tsfm)
        {
            // Create a cabinet for every door
            List<GameObject> toAdd = new List<GameObject>();
            List<GameObject> toDestroy = new List<GameObject>();

            for (int child_id = 0; child_id < tsfm.childCount; child_id++)
            {
                Transform tsf = tsfm.GetChild(child_id);
                if (tsf.gameObject.name.Contains("door"))
                {
                    toAdd.Add(tsf.gameObject);
                }
                else
                {
                    toDestroy.Add(tsf.gameObject);
                }
            }

            foreach (GameObject tsfgo in toDestroy)
            {
                Object.Destroy(tsfgo);
            }

            foreach (GameObject tsfgo in toAdd)
            {
                
                GameObject cabinet_piece = new GameObject("kitchen_cabinet");
                
                cabinet_piece.transform.SetParent(tsfm, false);

                //cabinet_piece = Object.Instantiate(cabinet_piece);
                //cabinet_piece.transform.localRotation = tsfgo.transform.localRotation;
                cabinet_piece.transform.localPosition = tsfgo.transform.localPosition;
                //cabinet_piece.transform.localRotation = Quaternion.Euler(0, 90, 0);

                //cabinet_piece.transform.rotation = tsfgo.transform.rotation;
                //cabinet_piece.transform.parent = tsfm;


                tsfgo.transform.parent = cabinet_piece.transform;

                // Add collider


                GameObject collider_1 = new GameObject("collider");
                //collider_1 = Object.Instantiate(collider_1);
                //collider_1.transform.parent = cabinet_piece.transform;
                collider_1.transform.SetParent(cabinet_piece.transform, false);
                collider_1.AddComponent<BoxCollider>(new BoxCollider());
                collider_1.transform.localPosition = new Vector3(-0.18f, -0.335f, 0.36f);
                collider_1.transform.localScale = new Vector3(0.35f, 0.015f, 0.707f);
                //collider_1.transform.localRotation = Quaternion.Euler(0, 90, 0);

                GameObject collider_2 = new GameObject("collider");
                //collider_2 = Object.Instantiate(collider_2);
                //collider_2.transform.parent = cabinet_piece.transform;
                collider_2.transform.SetParent(cabinet_piece.transform, false);
                collider_2.AddComponent<BoxCollider>(new BoxCollider());
                collider_2.transform.localPosition = new Vector3(-0.18f, -0.116f, 0.36f);
                collider_2.transform.localScale = new Vector3(0.35f, 0.015f, 0.707f);
                //collider_2.transform.localRotation = Quaternion.Euler(0, 90, 0);

                GameObject collider_3 = new GameObject("collider");
                //collider_3 = Object.Instantiate(collider_3);
                //collider_3.transform.parent = cabinet_piece.transform;
                //collider_3.transform.SetParent(cabinet_piece.transform, false);
                collider_3.transform.SetParent(cabinet_piece.transform, false);
                collider_3.AddComponent<BoxCollider>(new BoxCollider());
                collider_3.transform.localPosition = new Vector3(-0.18f, 0.116f, 0.36f);
                collider_3.transform.localScale = new Vector3(0.35f, 0.015f, 0.707f);
                //collider_3.transform.localRotation = Quaternion.Euler(0, 90, 0); ;

                GameObject collider_4 = new GameObject("collider");
                //collider_4 = Object.Instantiate(collider_4);

                collider_4.transform.SetParent(cabinet_piece.transform, false);
                collider_4.AddComponent<BoxCollider>(new BoxCollider());
                collider_4.transform.localPosition = new Vector3(-0.18f, 0.336f, 0.36f);
                collider_4.transform.localScale = new Vector3(0.35f, 0.015f, 0.707f);
                //collider_4.transform.localRotation = Quaternion.Euler(0, 90, 0);

                GameObject collider_5 = new GameObject("collider");
                //collider_5 = Object.Instantiate(collider_5);
                //collider_5.transform.parent = cabinet_piece.transform;
                collider_5.transform.SetParent(cabinet_piece.transform, false);
                collider_5.AddComponent<BoxCollider>(new BoxCollider());
                collider_5.transform.localPosition = new Vector3(-0.18f, 0.0f, 0.737f);
                collider_5.transform.localScale = new Vector3(0.35f, 0.67f, 0.015f);
                //collider_5.transform.localRotation = Quaternion.Euler(0, 90, 0);

                GameObject collider_6 = new GameObject("collider");
                //collider_6 = Object.Instantiate(collider_6);
                collider_6.transform.SetParent(cabinet_piece.transform, false);
                //collider_6.transform.parent = cabinet_piece.transform;
                collider_6.AddComponent<BoxCollider>(new BoxCollider());
                collider_6.transform.localPosition = new Vector3(-0.18f, 0.0f, -0.006f);
                collider_6.transform.localScale = new Vector3(0.35f, 0.67f, 0.015f);
                //collider_6.transform.localRotation = Quaternion.Euler(0, 90, 0);

                GameObject collider_7 = new GameObject("collider");
                //collider_7 = Object.Instantiate(collider_7);
                //collider_7.transform.parent = cabinet_piece.transform;
                collider_7.transform.SetParent(cabinet_piece.transform, false);
                collider_7.AddComponent<BoxCollider>(new BoxCollider());
                collider_7.transform.localPosition = new Vector3(-0.36f, 0.0f, 0.36f);
                collider_7.transform.localScale = new Vector3(0.015f, 0.67f, 0.707f);
                //collider_7.transform.localRotation = Quaternion.Euler(0, 90, 0); ;


                ContainerSwitch cts = new ContainerSwitch();
                cts.objName = "door";
                cts.hand_pose = "GrabVerticalSmall";
                cts.target_property = "Rotation";
                cts.open_degrees = 70;
                cts.sp = new float[] { 0.03f, -0.235f, 0.68f };
                cts.AddSwitches(cabinet_piece.transform);

                cabinet_piece.AddComponent<MeshRenderer>();


            }
        }
    }
}