// #define DYNAMIC_COLOR_SPLIT

using StoryGenerator.Helpers;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace StoryGenerator.Recording
{

    public class ColorEncoding
    {
        class GameObjectSgmtInfo
        {
            public GameObject go;
            public int m_sgmt_inst_id;
            public Color m_sgmt_class_clr;
            public List<Renderer> list_rdr = new List<Renderer>();

            public GameObjectSgmtInfo(GameObject _go, int id_inst, Color c)
            {
                go = _go;
                m_sgmt_inst_id = id_inst;
                m_sgmt_class_clr = c;
            }

            public void AddRenderer(Renderer[] rdrs)
            {
                list_rdr.AddRange(rdrs);
            }
        }

        // GameObject.GetInstanceID() --> RGB color
        public static Dictionary<int, Color> instanceColor = new Dictionary<int, Color>();
        static int numOfInstances = 1;
        static int[] idToColorMapping;

#if (DYNAMIC_COLOR_SPLIT)
    static float split_red;
    static float split_green;
    static float split_blue;
    static float stepSize_red;
    static float stepSize_green;
    static float stepSize_blue;
#else
        const int SPLIT_RGB = 8;
        const int MAX_INSTANCE = SPLIT_RGB * SPLIT_RGB * SPLIT_RGB;
        const float STEP_SIZE_RGB = 1 / (float)(SPLIT_RGB - 1);
#endif
        const int ID_WALL = 0;
        const int ID_FLOOR = 2;
        const int ID_FLOOR_INIT = 10000000;

        // This is used to determine is all children of prefab must share the
        // same color
        static Dictionary<string, bool> m_instanceGroup_prefab;
        // This is used to lookup ID for GameObjects that must share the same color
        // throuhout the scene (e.g. wall, floor ceiling etc)
        static Dictionary<string, int> m_instanceGroup_go;


        // =========================== Static Methods =========================== //
        public static void EncodeCurrentScene(Transform rootTsfm)
        {
            // Need to check only one 
            if (m_instanceGroup_prefab == null) {
                Helper.GetInstanceGroups(out m_instanceGroup_prefab, out m_instanceGroup_go);
            }

            // Reset value
            numOfInstances = 1;
            instanceColor.Clear();

            ColorEncoding.Segmentation(rootTsfm);
        }

        // Used for encoding GameObjects that are spawned during prepare steps in 
        // ScriptExecution.cs. These are spawned because current scene lacks them but the 
        // script makes a use of it.
        public static void EncodeGameObject(GameObject go)
        {
            Debug.Assert(Helper.GetClassGroups().ContainsKey(go.name), "No color class for " + go.name);

            int mappedId = idToColorMapping[ColorEncoding.GetInstID() - 1] + 1;
            Color clr_inst = ColorEncoding.EncodeIDAsColor(mappedId);
            Color clr_class = Helper.GetClassGroups()[go.name].encodedColor;

            Debug.Assert(clr_inst != Color.white, "Incorrect instance segmentation for " + go.name + " during post segmentation");
            Debug.Assert(clr_class != Color.white, "Incorrect class segmentation for " + go.name + " during post segmentation");

            Renderer[] arry_rdr = go.GetComponentsInChildren<Renderer>();
            MaterialPropertyBlock mpb = new MaterialPropertyBlock();

            instanceColor.Add(go.GetInstanceID(), clr_inst);

            mpb.SetColor("_ObjectColor", clr_inst);
            mpb.SetColor("_ClusterColor", clr_class);
            foreach (Renderer r in arry_rdr) {
                r.SetPropertyBlock(mpb);
            }
        }

        static int GetInstID()
        {
            int id = numOfInstances;
            numOfInstances++;
            return id;
        }

        static Color EncodeIDAsColor(int clr_id)
        {
#if (DYNAMIC_COLOR_SPLIT)
      float numSteps_r = Mathf.Floor( clr_id / (split_green * split_blue) );
      clr_id -= (int) ( (split_green * split_blue) * numSteps_r );

      float numSteps_g = Mathf.Floor(clr_id / split_blue);
      clr_id -= (int) ( split_blue * numSteps_g );
      float numSteps_b = Mathf.Floor(clr_id % split_blue);

      return new Color(numSteps_r * stepSize_red, numSteps_g * stepSize_green, numSteps_b * stepSize_blue);
#else
            float numSteps_r = (clr_id & 0x000001C0) >> 6;
            float numSteps_g = (clr_id & 0x00000038) >> 3;
            float numSteps_b = clr_id & 0x00000007;
            return new Color(numSteps_r * STEP_SIZE_RGB, numSteps_g * STEP_SIZE_RGB, numSteps_b * STEP_SIZE_RGB);
#endif
        }

        static void Segmentation(Transform home)
        {
            // Maximum number of objects that will be spawn on runtime since
            // current scene lacks them while the script to be executed needs them.
            const int NUM_TO_BE_SPAWNED_RUNTIME = 40;
#if (DYNAMIC_COLOR_SPLIT)
      List<GameObjectSgmtInfo> list_gsi = new List<GameObjectSgmtInfo>();
      Dictionary<string, int> goGroupID_Map = new Dictionary<string, int> ();

      // Set to Colors to white. It would be good for visual debugging
      // This is recursive call of depth first search
      ColorEncoding.SegmentationHelper(home, -1, Color.white, list_gsi, goGroupID_Map);

      // Need to include wall (0 - black), floor (max - white) and prefabs that will be
      // spawned during runtime.
      int numObjToBeEncoded = numOfInstances + (2 + NUM_TO_BE_SPAWNED_RUNTIME);

      // Split RGB space into closest number of total instances in the scene.
      float oneThird = 1.0f / 3.0f;
      float s = Mathf.RoundToInt( Mathf.Pow(numObjToBeEncoded, oneThird) );

      split_red = s;
      split_green = s;
      split_blue = s;

      if (split_red * split_green * split_blue < numObjToBeEncoded)
      {
        split_blue += 1;
        if (split_red * split_green * split_blue < numObjToBeEncoded)
        {
          split_green += 1;
        }
      }

      float totalSplit = split_red * split_green * split_blue;
      Debug.Assert(totalSplit >= numObjToBeEncoded, "Number of instances is greater than RGB color split.");

      stepSize_red = 1 / (split_red - 1);
      stepSize_green = 1 / (split_green - 1);
      stepSize_blue = 1 / (split_blue - 1);

      // Shuffle the colors so that nearby objects (which tend to have similar id) have
      // different colors
      // Note that we are hard coding walls and floors --> numObjToBeEncoded -2
      idToColorMapping = Helper.SampleRandomOrder(numObjToBeEncoded - 2);
#else
            List<GameObjectSgmtInfo> list_gsi = new List<GameObjectSgmtInfo>();
            Dictionary<string, int> goGroupID_Map = new Dictionary<string, int>();

            // Set to Colors to white. It would be good for visual debugging
            // This is recursive call of depth first search
            ColorEncoding.SegmentationHelper(home, -1, Color.white, list_gsi, goGroupID_Map);

            float totalSplit = MAX_INSTANCE;
            Debug.Assert(MAX_INSTANCE >= numOfInstances + NUM_TO_BE_SPAWNED_RUNTIME, "Number of instances is greater than RGB color split.");

            // Shuffle the colors so that nearby objects (which tend to have similar id) have
            // different colors
            // Note that we are hard coding walls and floors --> numObjToBeEncoded -2
            idToColorMapping = Helper.SampleRandomOrder(MAX_INSTANCE - 2);
#endif
            MaterialPropertyBlock mpb = new MaterialPropertyBlock();
            int gsi_id = 0;
            Color instClr, classClr;
            foreach (GameObjectSgmtInfo gsi in list_gsi) {
                bool isWall = false;
                bool isFloor = false;
                int instID;
                // Wall color would be black
                if (gsi_id == 0)
                {
                    instID = 0;

                    instClr = Color.white;
                    classClr = gsi.m_sgmt_class_clr;
                }
                else
                {
                    if (gsi.m_sgmt_inst_id == ID_WALL)
                    {
                        isWall = true;
                        instID = 0;
                    }
                    // Floor color would be white
                    else if (gsi.m_sgmt_inst_id == ID_FLOOR_INIT)
                    {
                        isFloor = true;
                        instID = (int)totalSplit - 1;
                    }
                    else
                    {
                        // Need -1 index due to the fact that id to color map starts at index 0 and
                        // id starts at 1 due to 0 is reserved for wall
                        // Need + 1 at the result because output must start at 1
                        instID = idToColorMapping[gsi.m_sgmt_inst_id - 1] + 1;
                    }
                    instClr = EncodeIDAsColor(instID);
                    classClr = gsi.m_sgmt_class_clr;
                    // This Object is neither floor nor wall but has white segmentaion color
                    // I used white color to represent null in this case so this is clearly an error.
                    if (!isFloor && !isWall && (instClr == Color.white || classClr == Color.white))
                    {
                        Debug.LogError("Object with " + instID + " color instance ID has white segmentaion color");
                        Debug.Break();
                    }

                }




                instanceColor.Add(gsi.go.GetInstanceID(), instClr);
                mpb.SetColor("_ObjectColor", instClr);
                mpb.SetColor("_ClusterColor", classClr);

                foreach (Renderer rdr in gsi.list_rdr) {
                    rdr.SetPropertyBlock(mpb);
                }

                gsi_id += 1;
            }
        }

        static void SegmentationHelper(Transform tsfm, int id_inst_parent, Color clr_class_parent,
          List<GameObjectSgmtInfo> list_gsi, Dictionary<string, int> ggim, int index_gsi_parent = 0)
        {
            int id_inst_self = -1;
            Color clr_class_self = Color.white;

            Renderer[] arry_rdr = tsfm.GetComponents<Renderer>();
            if (list_gsi.Count == 0)
            {
                GameObject dummy_go = new GameObject();
                list_gsi.Add(new GameObjectSgmtInfo(dummy_go, -1, clr_class_self));
                index_gsi_parent = list_gsi.Count - 1;
            }
            if (tsfm.name.Contains("StoveCoil"))
            {
                Debug.Log("Here");
            }
            if (m_instanceGroup_prefab.ContainsKey(tsfm.name)) {
                id_inst_self = ColorEncoding.GetInstID();
                id_inst_parent = id_inst_self;
            }
            // Encode colors for the all GameObjects that belong to the JSON group name
            // Examples are: Walls, Floors, Ceiliing etc
            // These GameObjects don't have Renderer Component but their children do
            else if (m_instanceGroup_go.ContainsKey(tsfm.name)) {
                // If we already visited this type, share the same ID
                if (ggim.ContainsKey(tsfm.name)) {
                    id_inst_self = ggim[tsfm.name];
                } else {
                    // Hard code the wall to be black
                    if (m_instanceGroup_go[tsfm.name] == ID_WALL) {
                        id_inst_self = 0;
                    } else if (m_instanceGroup_go[tsfm.name] == ID_FLOOR) {
                        id_inst_self = ID_FLOOR_INIT;
                    } else {
                        id_inst_self = ColorEncoding.GetInstID();
                    }
                    ggim[tsfm.name] = id_inst_self;
                }
                id_inst_parent = id_inst_self;
            } else if (arry_rdr != null && arry_rdr.Length > 0 && id_inst_parent == -1) {
                id_inst_self = ColorEncoding.GetInstID();
            }

            if (Helper.GetClassGroups().ContainsKey(tsfm.name)) {
                if (id_inst_self == -1) {
                    if (id_inst_parent != -1) {
                        id_inst_self = id_inst_parent;
                    } else {
                        id_inst_self = ColorEncoding.GetInstID();
                        id_inst_parent = id_inst_self;
                    }
                }

                Color c = Helper.GetClassGroups()[tsfm.name].encodedColor;
                clr_class_self = c;
                clr_class_parent = c;

                list_gsi.Add(new GameObjectSgmtInfo(tsfm.gameObject, id_inst_self, c));
                index_gsi_parent = list_gsi.Count - 1;

            }

            if (arry_rdr != null && arry_rdr.Length > 0 && list_gsi.Count > 1 && tsfm.IsChildOf(list_gsi[index_gsi_parent].go.transform)) {
                list_gsi[index_gsi_parent].AddRenderer(arry_rdr);
            }
            else
            {
                list_gsi[0].AddRenderer(arry_rdr);
            }

            // Recursive call
            for (int i = 0; i < tsfm.childCount; i++) {
                ColorEncoding.SegmentationHelper(tsfm.GetChild(i), id_inst_parent, clr_class_parent, list_gsi, ggim, index_gsi_parent);
            }
        }
    }

}
