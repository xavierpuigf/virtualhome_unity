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

    public static class STR_PATH
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
    public class UtilsAnnotator
    {
        static List<GameObject> m_GO_toDisable = new List<GameObject>();

        public static void ProcessHome(Transform h, bool shouldRandomize)
        {
#if UNITY_EDITOR
            UnityEditor.AI.NavMeshBuilder.ClearAllNavMeshes();
            UnityEditor.AI.NavMeshBuilder.BuildNavMesh();
#endif
            if (shouldRandomize)
            {
                GlobalRandomization(h);
            }
            Reset();

            var allChildren = h.GetComponentsInChildren<Transform>();
            foreach (Transform child in allChildren)
            {
                
                ObjectAnnotator.AnnotateObj(child);
            }

            PostColorEncoding_DisableGameObjects();
        }

        public static void Reset()
        {
            m_GO_toDisable.Clear();
            ElectronicUtils.CacheSpritesAndVids();
        }

        // Some GameObjects must be disabled to that every GameObjects have default
        // state of "power off" state.
        public static void PostColorEncoding_DisableGameObjects()
        {
            if (m_GO_toDisable != null && m_GO_toDisable.Count > 0)
            {
                foreach (GameObject go in m_GO_toDisable)
                {
                    go.SetActive(false);
                }
            }
        }


        public static Transform SpawnPrefab(string prefabPath, Transform tsfm_parent,
            Vector3 localPos, Vector3 localRot, bool shouldDisabled = false)
        {
            GameObject prefab = Resources.Load(prefabPath) as GameObject;
            Transform tsfm = Object.Instantiate(prefab).transform;
            // Rename the instantiated object to remove suffix (like (1))
            // for proper segmentation
            tsfm.name = prefab.name;
            tsfm.SetParent(tsfm_parent, false);
            tsfm.localPosition = localPos;
            if (localRot != Vector3.zero)
            {
                tsfm.localRotation = Quaternion.Euler(localRot);
            }
            if (shouldDisabled)
            {
                m_GO_toDisable.Add(tsfm.gameObject);
            }

            return tsfm;
        }
        public static void CreateVector3Array(float[] input, out Vector3[] output)
        {
            // If creating default one
            if (input == null)
            {
                output = new Vector3[1] { Vector3.zero };

            }
            else
            {
                int v3len = input.Length / 3;
                output = new Vector3[v3len];
                for (int i = 0; i < v3len; i++)
                {
                    int baseIdx = i * 3;
                    output[i] = new Vector3(input[baseIdx], input[baseIdx + 1], input[baseIdx + 2]);
                }
            }
        }

        public static void AddNavMeshObstacle(GameObject go, Vector3 vcenter, Vector3 vsize)
        {

            if (go.GetComponentInChildren<NavMeshObstacle>() == null)
            {
                NavMeshObstacle nmo = go.AddComponent<NavMeshObstacle>();
                if (vcenter != Vector3.zero)
                {
                    nmo.center = vcenter;
                }
                if (vsize != Vector3.zero)
                {
                    nmo.size = vsize;
                }

                nmo.carving = true;
                nmo.carvingTimeToStationary = 0.0f;
                nmo.carveOnlyStationary = false;
            }
        }

        public static void GlobalRandomization(Transform h)
        {
            
            // Randomize all light values
            Light[] allLights = h.GetComponentsInChildren<Light>();
            foreach (Light l in allLights)
            {
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

        public static void SetSkipAnimation(Transform h)
        {
            HandInteraction[] his = h.GetComponentsInChildren<HandInteraction> ();
            foreach (HandInteraction hi in his)
            {
                hi.SetInstantTransition();
            }
        }

        // TODO: brings this to another class
        public static void HandleShowerStall(Transform tfsm)

        {
            Transform curtainGroup = null;
            // Do breath first search for shower curtain
            Queue<Transform> children = new Queue<Transform>();
            children.Enqueue(tfsm);
            do
            {
                Transform tsfm = children.Dequeue();
                const string SHOWER_CURTAIN_GROUP = "MOD_FUR_Shower_stall_01_Curtain_01";
                if (tsfm.name.Contains(SHOWER_CURTAIN_GROUP) && tsfm.gameObject.GetComponent<HandInteraction>() == null)
                {
                    curtainGroup = tsfm;
                    break;
                }

                for (int j = 0; j < tsfm.childCount; j++)
                {
                    children.Enqueue(tsfm.GetChild(j));
                }

            } while (children.Count > 0);

            if (curtainGroup != null)
            {
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
                if (hi.switches == null)
                {
                    hi.switches = new List<ActivationSwitch>();
                }
                hi.switches.Add(swch);
                hi.Initialize();
            }
        }
        public static class LAMPS
        {
            public const string GROUP_NAME = "Lamps";
            public const string LIGHT_SOURCE = "Point light";
            public const string REFLECTION_PROBE = "Reflection Probe";
        }
        public static void HandleLamp(Transform e)

        {
            if (e.gameObject.GetComponent<HandInteraction>() == null)
            {

                State_object so = e.gameObject.AddComponent<State_object>();
                so.Initialize();

                List<GameObject> list_lightSources = new List<GameObject>();

                for (int j = 0; j < e.childCount; j++)
                {
                    Transform t = e.GetChild(j);
                    if (t.name.Equals(LAMPS.LIGHT_SOURCE))
                    {
                        list_lightSources.Add(t.gameObject);
                    }
                }
                AddLampSwitch(e.gameObject, list_lightSources);

            }

        }
        static void AddLampSwitch(GameObject lamp, List<GameObject> list_lightSources)
        {
            Toggle tgl = new Toggle(list_lightSources);
            TransitionSequence ts_light = new TransitionSequence(new List_TB() { tgl });

            ActivationSwitch swch = new ActivationSwitch(HandPose.Button, ActivationAction.SwitchOn,
              new Vector3(0.0f, 0.02f, 0.0f), new List_TS() { ts_light });

            HandInteraction hi = lamp.AddComponent<HandInteraction>();
            if (hi.switches == null)
            {
                hi.switches = new List<ActivationSwitch>();
            }
            hi.switches.Add(swch);
            hi.Initialize();
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


    static class ElectronicUtils
    {
        // Cache Sprites for Computer Screen and TV screen
        public static Sprite[] m_sprites_compScreen;
        public static Sprite[] m_sprites_tv;
        public static VideoClip[] m_videoClips_tv;

        public static void CacheSpritesAndVids()
        {
            if (m_sprites_compScreen == null)
            {
                m_sprites_compScreen = Resources.LoadAll<Sprite>(STR_PATH.SPRITE_COMP_SCREEN);
                Debug.Assert(m_sprites_compScreen != null && m_sprites_compScreen.Length != 0,
                  "Computer screen sprite load error");
            }
            if (m_videoClips_tv == null)
            {
                m_videoClips_tv = Resources.LoadAll<VideoClip>(STR_PATH.VIDEO_TV);
            }
            if (m_sprites_tv == null)
            {
                m_sprites_tv = Resources.LoadAll<Sprite>(STR_PATH.SPRITE_COMP_TV);
                Debug.Assert(m_sprites_tv != null && m_sprites_tv.Length != 0,
                  "TV screen sprite load error");
            }
        }

        public static void AssignRandomSprite(Transform tsfm, Vector3 screenSize, Sprite[] arry_sprite)
        {
            Sprite newSprite = arry_sprite[Random.Range(0, arry_sprite.Length)];
            SpriteRenderer sr = tsfm.GetComponent<SpriteRenderer>();
            sr.sprite = newSprite;
            Vector3 spriteSize = newSprite.bounds.size;
            tsfm.localScale = new Vector3(screenSize.x / spriteSize.x, screenSize.y / spriteSize.y, 1);
        }

        public static void AssignRandomVideo(Transform tsfm, Vector3 screenSize, VideoClip[] arry_video)
        {
            VideoClip newClip = arry_video[Random.Range(0, arry_video.Length)];
            VideoPlayer vp = tsfm.GetComponent<VideoPlayer>();
            vp.clip = newClip;
        }
    }
}
