using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using StoryGenerator.HomeAnnotation;
using StoryGenerator.Recording;
using StoryGenerator.Rendering;
using StoryGenerator.RoomProperties;
using StoryGenerator.SceneState;
using StoryGenerator.Scripts;
using StoryGenerator.Utilities;
using StoryGenerator.Communication;
using Newtonsoft.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using StoryGenerator.CharInteraction;
using Unity.Profiling;
using UnityEngine.InputSystem;
using Unity.RenderStreaming;
using Unity.RenderStreaming.Signaling;
using StoryGenerator.Helpers;
using TMPro;

namespace StoryGenerator
{
    public class ButtonClicks
    {
        public string button_str;
        public string button_action;
        public float [] button_pos;

        public ButtonClicks(string button_str, string button_action, Vector2 pos)
        {
            
            
            this.button_str = button_str;
            this.button_action = button_action;
            button_pos = new float[2]{pos.x, pos.y};

        }


    }

    public class ServerMessage
    {
        public string task_name;
        public string task_content;

        public ServerMessage(string task_name, string task_content)
        {
            this.task_name = task_name;
            this.task_content = task_content;
        }
    }

    [RequireComponent(typeof(Recorder))]
    public class TestDriver : MonoBehaviour
    {

        static ProfilerMarker s_GetGraphMarker = new ProfilerMarker("MySystem.GetGraph");
        static ProfilerMarker s_GetMessageMarker = new ProfilerMarker("MySystem.GetMessage");
        static ProfilerMarker s_UpdateGraph = new ProfilerMarker("MySystem.UpdateGraph");
        static ProfilerMarker s_SimulatePerfMarker = new ProfilerMarker("MySystem.Simulate");

        public static ManualResetEvent mre = new ManualResetEvent(false);
        public static bool t_lock = false;

        private const int DefaultPort = 8080;
        private const int DefaultChars = 2;
        private const int DefaultTimeout = 500000;
        private const string DefaultFileName = "default";


        //static ProcessingController processingController;
        static HttpCommunicationServer commServer;
        static NetworkRequest networkRequest = null;

        public static DataProviders dataProviders;

        public List<State> CurrentStateList = new List<State>();
        public int num_renderings = 0;

        private int numSceneCameras = 0;
        private int numCharacters = 0;
        public int finishedChars = 0; // Used to count the number of characters finishing their actions.


        Recorder recorder;
        // TODO: should we delete this ^
        private List<Recorder> recorders = new List<Recorder>();
        List<Camera> sceneCameras;
        List<Camera> cameras;
        private List<CharacterControl> characters = new List<CharacterControl>();
        private List<ScriptExecutor> sExecutors = new List<ScriptExecutor>();
        private List<GameObject> rooms = new List<GameObject>();

        WaitForSeconds WAIT_AFTER_END_OF_SCENE = new WaitForSeconds(3.0f);

        private Color startcolor;
        Renderer rend;

        private bool episodeDone;
        private int episodeNum = -1;

        private List<GameObject> highlightedObj = new List<GameObject> ();



        static int port_websocket = 80;
        static int num_chars = 1;
        static string file_name = "default";

        bool click = false;

        public List<bool> keyPressed;
        List<bool> first_press;
        List<bool> first_click;
        List<bool> mouse_clicked;
        public List<bool> executing_action;
        private List<Keyboard> m_keyboard_l;
        private List<Mouse> m_mouse_l;


        public List<string> scriptLines;
        EnvironmentGraphCreator currentGraphCreator = null;
        EnvironmentGraph currentGraph = null;
        List<WebBrowserInputData> licr;
        Episode currentEpisode;
        float currTime;
        public List<List<string>> action_button;
        List<GameObject> pointer;

        ICollection<string> openableContainers = new HashSet<string>{"bathroomcabinet", "kitchencabinet", "cabinet", "fridge", "stove", "dishwasher", "microwave"}; 

        [Serializable]
        class SceneData
        {
            //public string characterName;
            public string sceneName;
            public int frameRate;
        }

        IEnumerator Start()
        {
            recorder = GetComponent<Recorder>();

            // Initialize data from files to static variable to speed-up the execution process
            if (dataProviders == null)
            {
                dataProviders = new DataProviders();
            }

            List<string> list_assets = dataProviders.AssetsProvider.GetAssetsPaths();



            // Check all the assets exist
            //foreach (string asset_name in list_assets)
            //{
            //    if (asset_name != null)
            //    {
            //        GameObject loadedObj = Resources.Load(ScriptUtils.TransformResourceFileName(asset_name)) as GameObject;
            //        if (loadedObj == null)
            //        {
            //            Debug.Log(asset_name);
            //            Debug.Log(loadedObj);
            //        }
            //    }
            //}

            if (commServer == null)
            {
                InitServer();
            }
            commServer.Driver = this;

            ProcessHome(false);
            DeleteChar();
            Screen.sleepTimeout = SleepTimeout.NeverSleep;

            yield return null;
            if (networkRequest == null)
            {
                commServer.UnlockProcessing(); // Allow to proceed with requests
            }
            //StartCoroutine(ProcessNetworkRequest());
            if (EpisodeNumber.episodeNum == -1)
            {
                EpisodeNumber.episodeNum = 1;
                episodeNum = EpisodeNumber.episodeNum;
                Debug.Log($"init episode number {episodeNum}");
                // Load correct scene (episodeNum is just for testing rn)
                string episodePath = $"Episodes/pilot_task_id_{episodeNum}_bounds.json";

                string episodeFileContent = File.ReadAllText(episodePath);
                //TextAsset episodeFile = Resources.Load<TextAsset>(episodePath);
                Episode currentEpisode = JsonUtility.FromJson<Episode>(episodeFileContent);
                currentEpisode.file_name = file_name;
                SceneManager.LoadScene(currentEpisode.env_id);
                yield return null;
            }
            else
            {
                Debug.Log($"next episode number {EpisodeNumber.episodeNum}");
                episodeNum = EpisodeNumber.episodeNum;
                StartCoroutine(ProcessInputRequest(episodeNum));
            }
        }

        private void InitServer()
        {
            string[] args = Environment.GetCommandLineArgs();
            var argDict = DriverUtils.GetParameterValues(args);
            //Debug.Log(argDict);
            string portString = null;
            string chString = null;
            int port;
            int nchars;
            
            if (!argDict.TryGetValue("http-port", out portString) || !Int32.TryParse(portString, out port))
                port = DefaultPort;

            if (!argDict.TryGetValue("numchars", out chString) || !Int32.TryParse(chString, out nchars))
                nchars = DefaultChars;

            if(!argDict.TryGetValue("filename", out file_name))
                file_name = DefaultFileName;

            num_chars = nchars;
            port_websocket = port;
            Debug.Log(this.GetInstanceID());

            Debug.Log("Setting port " + port_websocket.ToString());

            commServer = new HttpCommunicationServer(port+1) { Timeout = DefaultTimeout };
        }

        private void OnApplicationQuit()
        {
            commServer?.Stop();
        }

        void OnMouseEnter()
        {
            if (GetComponent<Renderer>() != null)
            {
                Debug.Log("HIGHLIGHT");
                rend = GetComponent<Renderer>();
                startcolor = rend.material.color;
                rend.material.color = Color.yellow;
            }
            
        }

        void OnMouseExit()
        {
            if (rend != null)
            {
                Debug.Log("UNHIGHLIGHT");
                rend.material.color = startcolor;
            }
        }

        void DeleteChar()
        {

            foreach (Transform tf_child in transform)
            {
                foreach (Transform tf_obj in tf_child)
                {
                    if (tf_obj.gameObject.name.ToLower().Contains("male"))
                    {
                        Destroy(tf_obj.gameObject);
                    }
                }
            }


        }

        void ProcessHome(bool randomizeExecution)
        {
            UtilsAnnotator.ProcessHome(transform, randomizeExecution);
            ColorEncoding.EncodeCurrentScene(transform);
            // Disable must come after color encoding. Otherwise, GetComponent<Renderer> failes to get
            // Renderer for the disabled objects.
            UtilsAnnotator.PostColorEncoding_DisableGameObjects();
        }

        public void ProcessRequest(string request)
        {
            Debug.Log(string.Format("Processing request {0}", request));

            NetworkRequest newRequest = null;
            try
            {
                newRequest = JsonConvert.DeserializeObject<NetworkRequest>(request);
            }
            catch (JsonException)
            {
                return;
            }

            if (networkRequest != null)
                return;

            networkRequest = newRequest;
        }

        public void SetRequest(NetworkRequest request)
        {
            networkRequest = request;
        }

        IEnumerator TimeOutEnumerator(IEnumerator enumerator)
        {
            while (enumerator.MoveNext() && !recorder.BreakExecution())
                yield return enumerator.Current;
        }
        void OnDeviceChange(InputDevice device, InputDeviceChange change)
        {
            switch (change)
            {
                case InputDeviceChange.Added:
                    SetDevice(device);
                    return;
                case InputDeviceChange.Removed:
                    SetDevice(device, false);
                    return;
            }
        }

        void SetDevice(InputDevice device, bool add = true)
        {


            switch (device)
            {
                case Mouse mouse:
                    if (add)
                    {
                        for (int i = 0; i < m_mouse_l.Count; i++)
                        {
                            if (m_mouse_l[i] == null)
                            {
                                m_mouse_l[i] = mouse;
                                break;
                            }

                        }
                    }
                    else
                    {
                        for (int i = 0; i < m_mouse_l.Count; i++)
                        {
                            if (m_mouse_l[i] == mouse)
                            {
                                m_mouse_l[i] = null;
                            }
                        }
                        
                    }
                    //m_mouse = add ? mouse : null;
                    return;
                case Keyboard keyboard:
                    if (add)
                    {
                        for (int i = 0; i < m_keyboard_l.Count; i++)
                        {
                            if (m_keyboard_l[i] == null)
                            {
                                m_keyboard_l[i] = keyboard;

                                string task_info = JsonConvert.SerializeObject(new ServerMessage("PlayerInfo", "Player "+i.ToString()));
                                string task = JsonConvert.SerializeObject(currentEpisode.UpdateTasksString());
                                string task_info2 = JsonConvert.SerializeObject(new ServerMessage("UpdateTask", task));

                                if (licr[i] != null)
                                {
                                    licr[i].SendData(task_info2);
                                    licr[i].SendData(task_info);
                                }
                                break;
                            }
                                
                        }
                        //m_keyboard = add ? keyboard : null;
                    }
                    else
                    {
                        for (int i = 0; i < m_keyboard_l.Count; i++)
                        {
                            if (m_keyboard_l[i] == keyboard)
                            {
                                m_keyboard_l[i] = null;
                            }
                        }
                    }
                    return;


            }
        }

        IEnumerator ProcessInputRequest(int episode)
        {
            yield return null;
            string episodePath = $"Episodes/pilot_task_id_{episode}_bounds.json";
            string episodeFile = File.ReadAllText(episodePath);
            currentEpisode = JsonUtility.FromJson<Episode>(episodeFile);
            currentEpisode.ClearDataFile();
            currentEpisode.file_name = file_name;

            sceneCameras = ScriptUtils.FindAllCameras(transform);
            numSceneCameras = sceneCameras.Count;
            cameras = sceneCameras.ToList();
            cameras.AddRange(CameraExpander.AddRoomCameras(transform));
            CameraUtils.DeactivateCameras(cameras);
            OneTimeInitializer cameraInitializer = new OneTimeInitializer();
            OneTimeInitializer homeInitializer = new OneTimeInitializer();

            keyPressed = new List<bool>();
            first_press = new List<bool>();
            first_click = new List<bool>();
            mouse_clicked = new List<bool>();
            executing_action = new List<bool>();
            m_keyboard_l = new List<Keyboard>();
            m_mouse_l = new List<Mouse>();
            pointer = new List<GameObject>();


            InitRooms();
            action_button = new List<List<string>>();
            cameraInitializer.initialized = false;
            if (currentGraph == null)
            {
                currentGraphCreator = new EnvironmentGraphCreator(dataProviders);
                currentGraph = currentGraphCreator.CreateGraph(transform);
            }

            //EXPAND SCENE
            cameraInitializer.initialized = false;
            List<IEnumerator> animationEnumerators = new List<IEnumerator>();
            int expandSceneCount = 0;
            UtilsAnnotator.SetSkipAnimation(transform);

            try
            {
                if (currentGraph == null)
                {
                    currentGraphCreator = new EnvironmentGraphCreator(dataProviders);
                    currentGraph = currentGraphCreator.CreateGraph(transform);
                }

                ExpanderConfig graph_config = new ExpanderConfig();
                Debug.Log("Successfully de-serialized object");
                EnvironmentGraph graph = currentEpisode.init_graph;
                Debug.Log("got graph");


                if (graph_config.randomize_execution)
                {
                    InitRandom(graph_config.random_seed);
                }

                // Maybe we do not need this
                if (graph_config.animate_character)
                {
                    foreach (CharacterControl c in characters)
                    {
                        c.GetComponent<Animator>().speed = 1;
                    }
                }

                SceneExpander graphExpander = new SceneExpander(dataProviders)
                {
                    Randomize = graph_config.randomize_execution,
                    IgnoreObstacles = graph_config.ignore_obstacles,
                    AnimateCharacter = graph_config.animate_character,
                    TransferTransform = graph_config.transfer_transform
                };

                // TODO: set this with a flag
                bool exact_expand = false;
                graphExpander.ExpandScene(transform, graph, currentGraph, expandSceneCount, exact_expand);

                SceneExpanderResult result = graphExpander.GetResult();

                // TODO: Do we need this?
                //currentGraphCreator.SetGraph(graph);
                if (!result.Success)
                {
                    Tuple<EnvironmentGraph, string> currentGraph_list = new Tuple<EnvironmentGraph, string> (currentGraph, JsonConvert.SerializeObject(result.Messages));
                    string cgraph_str = JsonConvert.SerializeObject(currentGraph_list);
                    File.WriteAllText(String.Format("ErrorGraph_{0}.json", currentEpisode.task_id), cgraph_str);
                }

                currentGraph = currentGraphCreator.UpdateGraph(transform);
                animationEnumerators.AddRange(result.enumerators);
                expandSceneCount++;
            }
            catch (Exception e)
            {
                Debug.Log("Graph expansion did not work");
                Debug.Log(e);
            }

            foreach (IEnumerator e in animationEnumerators)
            {
                yield return e;
            }
            foreach (CharacterControl c in characters)
            {
                c.SetSpeed(20.0f);
                c.GetComponent<Animator>().speed = 0;
                
            }  
            

            //add one character by default
            CharacterConfig configchar = new CharacterConfig();//JsonConvert.DeserializeObject<CharacterConfig>(networkRequest.stringParams[0]);
            CharacterControl newchar;

            GameObject newRenderStream = new GameObject("RenderStreaming");
            //RenderStreaming.Create
            RenderStreaming rs = newRenderStream.AddComponent<RenderStreaming>();
            rs.runOnAwake = false;
            MultiPlayerBroadcast bc = newRenderStream.AddComponent<MultiPlayerBroadcast>();
            //Broadcast bc = newRenderStream.AddComponent<Broadcast>();

            List<Camera> currentCameras = new List<Camera>();
            licr = new List<WebBrowserInputData>();
            //List<CharacterControl>  characters = new List<CharacterControl>();
            //List <ScriptExecutor> sExecutors = new List<ScriptExecutor>();


            scriptLines = new List<string>();
            for (int itt = 0; itt < num_chars; itt++)
            {
                
                scriptLines.Add("");
                m_keyboard_l.Add(null);
                m_mouse_l.Add(null);
                highlightedObj.Add(null);
                action_button.Add(new List<string> ());
                newchar = AddCharacter(configchar.character_resource, false, "fix_room", configchar.character_position, currentEpisode.init_rooms[0]);
                newchar.SetSpeed(20.0f);
                StopCharacterAnimation(newchar.gameObject);

                characters.Add(newchar);
                CurrentStateList.Add(null);
                numCharacters++;
                List<Camera> charCameras = CameraExpander.AddCharacterCameras(newchar.gameObject, transform, "");
                CameraUtils.DeactivateCameras(charCameras);
                cameras.AddRange(charCameras);

                

                keyPressed.Add(false);
                mouse_clicked.Add(false);
                first_press.Add(false);
                first_click.Add(false);
                executing_action.Add(false);
                

                Camera currentCameraChar = charCameras.Find(c => c.name.Equals("Character_Camera_Fwd"));
                CameraStreamer cs = currentCameraChar.gameObject.AddComponent<CameraStreamer>();

                WebBrowserInputData icr = currentCameraChar.gameObject.AddComponent<WebBrowserInputData>();
                icr.SetDriver(this, itt);
                licr.Add(icr);
                icr.onDeviceChange += OnDeviceChange;
                currentCameraChar.gameObject.SetActive(true);
                //recorders[0].CamCtrls[cameras.IndexOf(currentCamera)].Activate(true);
                currentCameraChar.transform.localPosition = currentCameraChar.transform.localPosition + new Vector3(0, -0.15f, 0.1f);


                
                bc.AddComponent(cs);
                bc.AddComponent(icr);
                currentCameras.Add(currentCameraChar);
            }
            yield return null;
            currentGraph = currentGraphCreator.UpdateGraph(transform);
            ExecutionConfig config = new ExecutionConfig();
            config.walk_before_interaction = false;
            IObjectSelectorProvider objectSelectorProvider = new InstanceSelectorProvider(currentGraph);
            IList<GameObject> objectList = ScriptUtils.FindAllObjects(transform);
            createRecorders(config);
            sExecutors = InitScriptExecutors(config, objectSelectorProvider, sceneCameras, objectList);

            
            string connection_type = typeof(WebSocketSignaling).FullName;
            float interval = 5.0f;
            Debug.Log("GETTING " + port_websocket.ToString());
            string url = "ws://127.0.0.1:"+port_websocket.ToString(); //"ws://10.0.0.243:83";
            Debug.Log(this.GetInstanceID());
            SignalingHandlerBase[] handlers = { bc };
            //object[] args = { url, interval, SynchronizationContext.Current };
            ISignaling signal = (ISignaling) new WebSocketSignaling(url, interval, SynchronizationContext.Current);
            //Activator.CreateInstance(Type.GetType(connection_type), args);
            rs.Stop();
            rs.Run(null, null, signal, handlers);
            //rs.Run(null, null, signal, null);

            // Buttons: grab, open, putleft, putright, close

            // Create canvas and event system
            GameObject newCanvas = new GameObject("Canvas");
            Canvas canv = newCanvas.AddComponent<Canvas>();
            canv.renderMode = RenderMode.ScreenSpaceOverlay;
            newCanvas.AddComponent<CanvasScaler>();
            newCanvas.AddComponent<GraphicRaycaster>();
            GameObject eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

            Rect canvasrect = canv.GetComponent<RectTransform>().rect;
            
            GameObject panel = new GameObject("Panel");
            panel.AddComponent<CanvasRenderer>();

            float sizew = canvasrect.width * 0.25f;
            float sizeh = sizew * 0.5f;
            float margin = sizew * 0.05f;
            float posx = -canvasrect.width / 2.0f + sizew * 0.5f + margin;
            panel.transform.position = new Vector3(posx, +canvasrect.height * 0.5f - sizeh/2.0f - margin);
            Image i = panel.AddComponent<Image>();
            i.rectTransform.sizeDelta = new Vector2(sizew, sizeh);
            i.color = Color.white;
            var tempColor = i.color;
            tempColor.a = .6f;
            i.color = tempColor;

            panel.transform.SetParent(newCanvas.transform, false);
           
            GameObject tasksText = new GameObject("tasksText");
            tasksText.AddComponent<TextMeshProUGUI>();
            TextMeshProUGUI tasksUI = tasksText.GetComponent<TextMeshProUGUI>();
            tasksUI.raycastTarget = false;
            List<string> goals = new List<string>();
            tasksUI.fontSize = 12 * sizew / 250;
            //tasksUI.font
            tasksUI.rectTransform.position = new Vector3(posx, +canvasrect.height * 0.5f - sizeh/2.0f - margin);
            tasksUI.rectTransform.sizeDelta = new Vector2(sizew, sizeh);
            currentEpisode.GenerateTasksAndGoals();


            List<Goal> text_task = currentEpisode.UpdateTasksString();

            string task = JsonConvert.SerializeObject(currentEpisode.UpdateTasksString());
            string task_info = JsonConvert.SerializeObject(new ServerMessage("UpdateTask", task));
            //for (int itt = 0; itt < num_chars; itt++)
            //{

            //    licr[itt].SendData(task_info);
            //}



            List<bool> button_created = new List<bool>();
            for (int itt = 0; itt < num_chars; itt++)
            {
                GameObject cpointer = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                cpointer.transform.localScale = new Vector3(0.05f, 0.05f, 0.05f);
                cpointer.GetComponent<MeshRenderer>().material.color = Color.magenta;
                pointer.Add(cpointer);
                button_created.Add(false);
            }

            currentEpisode.StoreGraph(currentGraph, 0, "", -1);

            while (!episodeDone)
            {
                bool saveEpisode = false;
                
                currTime = Time.time;
                string move = "";
                for (int kboard_id = 0; kboard_id < m_keyboard_l.Count(); kboard_id++)
                {
                    if (keyPressed[kboard_id])
                        continue;
                    keyPressed[kboard_id] = false;
                    if (executing_action[kboard_id])
                        continue;
                    int char_id = kboard_id;
                    Keyboard m_keyboard = m_keyboard_l[kboard_id];

                    if (m_keyboard != null)
                    {
                        
                        if (m_keyboard.upArrowKey.isPressed)
                        {
                            if (!first_press[kboard_id])
                            {

                                move = String.Format("<char{0}> [walkforward]", char_id);
                                Debug.Log("move forward");
                                keyPressed[kboard_id] = true;
                                first_press[kboard_id] = true;
                            }
                        }
                        else if (m_keyboard.downArrowKey.isPressed)
                        {
                            if (!first_press[kboard_id])
                            {
                                move = String.Format("<char{0}> [walkbackward]", char_id);
                                Debug.Log("move backward");
                                keyPressed[kboard_id] = true;
                                first_press[kboard_id] = true;
                            }
                        }
                        else if (m_keyboard.rightArrowKey.isPressed)
                        {
                            if (!first_press[kboard_id])
                            {
                                move = String.Format("<char{0}> [turnright]", char_id);
                                Debug.Log("move right");
                                keyPressed[kboard_id] = true;
                                first_press[kboard_id] = true;
                            }

                        }
                        else if (m_keyboard.leftArrowKey.isPressed)
                        {
                            if (!first_press[kboard_id])
                            {
                                move = String.Format("<char{0}> [turnleft]", char_id);
                                Debug.Log("move left");
                                keyPressed[kboard_id] = true;
                                first_press[kboard_id] = true;
                            }

                        }
                        else if (m_keyboard.vKey.isPressed)
                        {
                            if (!first_press[kboard_id])
                            {
                                saveEpisode = true;
                                first_press[kboard_id] = true;
                            }
                        }
                        else if (m_keyboard.gKey.isPressed)
                        {
                            if (!first_press[kboard_id])
                            {
                                episodeDone = true;

                                saveEpisode = true;
                                first_press[kboard_id] = true;
                            }
                        }
                        //TODO: add going backwards and clean up Input code


                        //TODO: add camera movement
                        else if (m_keyboard.oKey.isPressed)
                        {
                            if (!first_press[kboard_id])
                            {
                                Debug.Log("move cam up");
                                currentEpisode.AddAction("move cam up", currTime);
                                currentCameras[kboard_id].transform.Rotate(-3, 0, 0);
                                first_press[kboard_id] = true;
                            }
                        }
                        else if (m_keyboard.lKey.isPressed)
                        {
                            if (!first_press[kboard_id])
                            {
                                Debug.Log("move cam down");
                                currentEpisode.AddAction("move cam down", currTime);
                                currentCameras[kboard_id].transform.Rotate(3, 0, 0);
                                first_press[kboard_id] = true;
                            }

                        }
                        else if (m_keyboard.eKey.isPressed)
                        {
                            if (highlightedObj[kboard_id] != null & !first_press[kboard_id])
                            {
                                highlightedObj[kboard_id].transform.Rotate(new Vector3(0, 1, 0), 15);
                                first_press[kboard_id] = true;
                            }
                        }
                        else if (m_keyboard.qKey.isPressed)
                        {
                            if (highlightedObj[kboard_id] != null & !first_press[kboard_id])
                            {
                                highlightedObj[kboard_id].transform.Rotate(new Vector3(0, 1, 0), -15);
                                first_press[kboard_id] = true;
                            }
                        }
                        else
                        {
                            //Debug.Log("no key pressed");
                            keyPressed[kboard_id] = false;
                            first_press[kboard_id] = false;
                        }

                        if (keyPressed[kboard_id])
                        {
                            if (move != "")
                            {
                                scriptLines[kboard_id] = move;
                                currentEpisode.AddAction(move, currTime);
                            }


                        }

                        //if (m_keyboard.GetKe)
                    }
                    else
                    {
                        first_press[kboard_id] = false;
                        keyPressed[kboard_id] = false;
                    }




                    mouse_clicked[kboard_id] = false;
                    if (m_mouse_l[kboard_id] != null)
                    {
                        if (m_mouse_l[kboard_id].leftButton.isPressed)
                        {
                            //click = true;
                            if (!first_click[kboard_id])
                            {
                                first_click[kboard_id] = true;
                                mouse_clicked[kboard_id] = true;
                            }



                        }
                        else
                        {
                            first_click[kboard_id] = false;
                        }
                    }
                    // rotate highlighted object
                    if (keyPressed[kboard_id])
                    {
                        highlightedObj[kboard_id] = null;
                    }
                


                    //if (Input.GetMouseButtonDown(0))
                    if (mouse_clicked[kboard_id] && first_click[kboard_id])
                    {
                        highlightedObj[kboard_id] = null;
                        
                        if (button_created[kboard_id])
                        {

                            button_created[kboard_id] = false;
                            pointer[kboard_id].SetActive(false);
                            licr[kboard_id].SendData("DeleteButtons");
                            //break;
                            //continue;
                        }
                        if (!click)
                        {
                            RaycastHit rayHit;
                            Vector2 mouseClickPosition = new Vector2(m_mouse_l[kboard_id].position.x.ReadValue(), m_mouse_l[kboard_id].position.y.ReadValue());
                            Ray ray = currentCameras[kboard_id].ScreenPointToRay(mouseClickPosition);
                            bool hit = Physics.Raycast(ray, out rayHit);
                            //Debug.DrawRay(ray.origin, ray.direction, Color.green, 20, true);
                            List<ButtonClicks> buttons_show = new List<ButtonClicks>();
                            if (hit)
                            {



                                //click = true;
                                Transform t = rayHit.transform;
                                pointer[kboard_id].SetActive(true);

                                pointer[kboard_id].transform.position = rayHit.point;
                                licr[kboard_id].SendData("DeleteButtons");

                                InstanceSelectorProvider objectInstanceSelectorProvider = (InstanceSelectorProvider)objectSelectorProvider;

                                while (t != null && !objectInstanceSelectorProvider.objectIdMap.ContainsKey(t.gameObject))
                                {
                                    t = t.parent;
                                }

                                EnvironmentObject obj;
                                try
                                {
                                    currentGraphCreator.objectNodeMap.TryGetValue(t.gameObject, out obj);
                                }
                                catch
                                {
                                    obj = null;
                                    Debug.Log("ERROR GETTING OBJECT");
                                    Debug.Log(t);
                                }

                                if (obj == null)
                                {
                                    continue;
                                }
                                string objectName = obj.class_name;
                                int objectId = obj.id;
                                Debug.Log("object name " + objectName);

                                rend = t.GetComponent<Renderer>();

                                //TODO: add Halo around objects, activating and disactivating
                                /*GameObject haloPrefab = Resources.Load("Halo") as GameObject;
                                GameObject halo = (GameObject)Instantiate(haloPrefab);
                                halo.transform.SetParent(t, false);
                                */

                                ICollection<string> objProperties = obj.properties;
                                ISet<Utilities.ObjectState> objStates = obj.states_set;

                                // coordinate of click
                                Vector2 mousePos = new Vector2();
                                mousePos.x = Input.mousePosition.x;
                                mousePos.y = currentCameras[kboard_id].pixelHeight - Input.mousePosition.y;
                                Vector3 point = currentCameras[kboard_id].ScreenToWorldPoint(new Vector3(mousePos.x, mousePos.y, currentCameras[kboard_id].nearClipPlane));
                                Debug.Log("point " + point);

                                //TODO: grabbing/putting with right and left hands
                                State currentState = this.CurrentStateList[kboard_id];
                                if (currentState == null)
                                {
                                    Debug.Log("HEre");
                                }
                                GameObject rh = currentState.GetGameObject("RIGHT_HAND_OBJECT");
                                GameObject lh = currentState.GetGameObject("LEFT_HAND_OBJECT");
                                EnvironmentObject obj1, obj2, obj3;

                                currentGraphCreator.objectNodeMap.TryGetValue(characters[kboard_id].gameObject, out obj1);
                                Character character_graph;
                                currentGraphCreator.characters.TryGetValue(obj1, out character_graph);

                                float distance = Vector3.Distance(characters[kboard_id].transform.position, obj.transform.position);
                                //distance = 0.0f;
                                Debug.Log("MY POSITION " + characters[0].transform.position);
                                Debug.Log("OBJECT POSITION " + obj.transform.position);
                                Debug.Log("DISTANCE " + distance);

                                if (objProperties.Contains("GRABBABLE") && (lh == null || rh == null) && distance < 2.8)
                                {
                                    highlightedObj[kboard_id] = t.gameObject;
                                    Debug.Log("grab");

 
                                    buttons_show.Add(new ButtonClicks("Grab " + objectName, String.Format("<char{2}> [grab] <{0}> ({1})",
                                        objectName, objectId, kboard_id),
                                        new Vector3(mouseClickPosition.x * 100.0f / currentCameras[kboard_id].pixelWidth, 100.0f - mouseClickPosition.y * 100.0f / currentCameras[kboard_id].pixelHeight)));

                                    button_created[kboard_id] = true;

                                }

                                //put on/in surfaces
                                else if ((objProperties.Contains("SURFACES") || (openableContainers.Contains(objectName) && objStates.Contains(Utilities.ObjectState.OPEN)) ||
                                    (objProperties.Contains("CONTAINERS") && !openableContainers.Contains(objectName))) && (rh != null || lh != null)
                                    // && (!goPutLeft.activeSelf || !goPutRight.activeSelf)
                                    && distance < 2)
                                {
                                    Debug.Log("put");

                                    if (lh != null)
                                    {
                                        currentGraphCreator.objectNodeMap.TryGetValue(lh, out obj2);
                                        Debug.Log("Put " + obj2.class_name + " on " + objectName);

                                        string putPos = String.Format("{0},{1},{2}", rayHit.point.x.ToString(), rayHit.point.y.ToString(), rayHit.point.z.ToString());

                                        string action = String.Format("<char{5}> [put] <{2}> ({3}) <{0}> ({1}) {4}",
                                            objectName, objectId, obj2.class_name, obj2.id, putPos, kboard_id);

                                        buttons_show.Add(new ButtonClicks("Put " + obj2.class_name + " on " + objectName, String.Format(action, objectName, objectId),
                                        new Vector3(mouseClickPosition.x * 100.0f / currentCameras[kboard_id].pixelWidth, 100.0f - mouseClickPosition.y * 100.0f / currentCameras[kboard_id].pixelHeight)));



                                        button_created[kboard_id] = true;


                                    }
                                    if (rh != null)
                                    {
                                        currentGraphCreator.objectNodeMap.TryGetValue(rh, out obj3);
                                        Debug.Log("Put " + obj3.class_name + " on " + objectName);
                                        //goPutRight.GetComponentInChildren<TextMeshProUGUI>().text = "Put " + obj3.class_name + "\n on " + objectName;
                                        //goPutRight.SetActive(true);
                                        //Button buttonPutRight = goPutRight.GetComponent<Button>();


                                        string putPos = String.Format("{0},{1},{2}", rayHit.point.x.ToString(), rayHit.point.y.ToString(), rayHit.point.z.ToString());

                                        string action = String.Format("<char{5}> [put] <{2}> ({3}) <{0}> ({1}) {4}",
                                            objectName, objectId, obj3.class_name, obj3.id, putPos, kboard_id);

                                        buttons_show.Add(new ButtonClicks("Put " + obj3.class_name + " on " + objectName, String.Format(action, objectName, objectId),
                                        new Vector3(mouseClickPosition.x * 100.0f / currentCameras[kboard_id].pixelWidth, 100.0f - mouseClickPosition.y * 100.0f / currentCameras[kboard_id].pixelHeight)));

                                        button_created[kboard_id] = true;



                                    }


                                }
                                //open/close
                                if (openableContainers.Contains(objectName) && distance < 2.8)
                                {
                                    //TODO: fix highlight
                                    /*if (rend != null)
                                    {
                                        startcolor = rend.material.color;
                                        Debug.Log("rend not null! " + rend.material.color);
                                        rend.material.color = Color.yellow;
                                    }
                                    */
                                    if (objStates.Contains(Utilities.ObjectState.CLOSED))
                                    {
                                        Debug.Log("open");
                                        buttons_show.Add(new ButtonClicks("Open " + objectName, String.Format("<char{2}> [open] <{0}> ({1})", objectName, objectId, kboard_id),
                                        new Vector3(mouseClickPosition.x * 100.0f / currentCameras[kboard_id].pixelWidth, 100.0f - mouseClickPosition.y * 100.0f / currentCameras[kboard_id].pixelHeight)));

                                        button_created[kboard_id] = true;

                                    }
                                    else if (objStates.Contains(Utilities.ObjectState.OPEN))
                                    {
                                        Debug.Log("close");
                                        Debug.Log("open");
                                        buttons_show.Add(new ButtonClicks("Close " + objectName, String.Format("<char{2}> [close] <{0}> ({1})", objectName, objectId, kboard_id),
                                        new Vector3(mouseClickPosition.x * 100.0f / currentCameras[kboard_id].pixelWidth, 100.0f - mouseClickPosition.y * 100.0f / currentCameras[kboard_id].pixelHeight)));
                                        button_created[kboard_id] = true;




                                        button_created[kboard_id] = true;

                                    }
                                }

                                string button_click_info = JsonConvert.SerializeObject(buttons_show);
                                button_click_info = JsonConvert.SerializeObject(new ServerMessage("ButtonInfo", button_click_info));
                                action_button[kboard_id].Clear();
                                for (int it = 0; it < buttons_show.Count; it++)
                                {
                                    action_button[kboard_id].Add(buttons_show[it].button_action);
                                }

                                licr[kboard_id].SendData(button_click_info);
                            }
                        }
                    }
                }
                if (saveEpisode)
                {
                    Debug.Log("Saving data...");
                    currentEpisode.RecordData();
                    for (int itt = 0; itt < licr.Count(); itt++)
                    {
                        licr[itt].SendData("SaveTime");
                    }
                }
                for (int char_id = 0; char_id < keyPressed.Count(); char_id++) {
                    if (keyPressed[char_id])
                    {
                        //un-highlight TODO 
                        /*if (rend != null)
                        {
                            rend.material.color = startcolor;
                            rend = null;
                        }*/
                        //Debug.Log(first_press);



                        Debug.Log(first_press);
                        pointer[char_id].SetActive(false);
                        keyPressed[char_id] = false;
                        yield return ExecuteScript(char_id);

                    }
                }

                yield return null;

            }
            Debug.Log("done");
            EpisodeNumber.episodeNum++;
            string nextEpisodePath = $"Episodes/pilot_task_id_{EpisodeNumber.episodeNum}_bounds.json";
            string nextEpisodeFile =  File.ReadAllText(nextEpisodePath);
            Episode nextEpisode = JsonUtility.FromJson<Episode>(nextEpisodeFile);
            int nextSceneIndex = nextEpisode.env_id;
            if (nextSceneIndex >= 0 && nextSceneIndex < SceneManager.sceneCountInBuildSettings)
            {
                Debug.Log($"loading episode: {nextSceneIndex}");
                SceneManager.LoadScene(nextSceneIndex);
                yield break;
            }
            Debug.Log($"Scene Loaded {SceneManager.GetActiveScene().buildIndex}");
            yield return null;

        }

        private string GetObj(string instr)
        {
            string obj_name = "";
            string pattParams = @"<([\w\s]+)>\s*\((\d+)\)\s*(:\d+:)?";
            Regex r = new Regex(pattParams);
            Match m = r.Match(instr);
            while (m.Success)
            {
                obj_name = m.Groups[1].Value + m.Groups[2].Value;

                m = m.NextMatch();
            }
            return obj_name;


        }
        public IEnumerator ExecuteScript(int char_id)
        {
            executing_action[char_id] = true;
            if (t_lock)
            {
                mre.WaitOne();

            }

            t_lock = true;
            if (action_button[char_id].Count > 0)
                for (int it = 0; it < action_button.Count; it++)
                {
                    // Delete action if an agent just did it
                    if (it != char_id && action_button[it].Count() > 0) {
                        string obj1 = GetObj(action_button[it][0]);
                        string obj2 = GetObj(action_button[char_id][0]);
                        if (obj1 == obj2 && obj1 != "")
                            action_button[it].Clear();

                    }
                }
            sExecutors[char_id].ClearScript();
            sExecutors[char_id].smooth_walk = false;
            if (scriptLines[char_id] == "")
            {
                yield return null;
            }
            string action_str = scriptLines[char_id];
            Debug.Log("deleting");
            Debug.Log("Scriptlines " + scriptLines[char_id]);
            Debug.Log("Scriptlines count " + scriptLines.Count);
            List<string> nscriptLines = new List<string>();
            nscriptLines.Add(scriptLines[char_id]);
            ScriptReader.ParseScript(sExecutors, nscriptLines, dataProviders.ActionEquivalenceProvider);
            finishedChars = 0;

            StartCoroutine(sExecutors[char_id].ProcessAndExecute(false, this));
            //while (finishedChars == 0)
            //    yield return new WaitForSeconds(0.01f);
            yield return new WaitUntil(() => finishedChars > 0);
            if (!sExecutors[char_id].Success)
            {
                currentEpisode.FailAction();
                Debug.Log("Failure");
            }
            else
            {
                // Update the graph
                List<ActionObjectData> last_action = new List<ActionObjectData>();
                ScriptPair script = sExecutors[char_id].script[0];
                State currentState = this.CurrentStateList[char_id];
                EnvironmentObject obj1;
                currentGraphCreator.objectNodeMap.TryGetValue(characters[char_id].gameObject, out obj1);
                Character character_graph;
                currentGraphCreator.characters.TryGetValue(obj1, out character_graph);

                ActionObjectData object_script = new ActionObjectData(character_graph, script, currentState.scriptObjects);
                last_action.Add(object_script);

                GameObject rh = currentState.GetGameObject("RIGHT_HAND_OBJECT");
                GameObject lh = currentState.GetGameObject("LEFT_HAND_OBJECT");
                EnvironmentObject obj2;
                if (lh != null)
                {
                    currentGraphCreator.objectNodeMap.TryGetValue(lh, out obj2);
                    character_graph.grabbed_left = obj2;

                }
                else
                {
                    character_graph.grabbed_left = null;
                }
                if (rh != null)
                {
                    currentGraphCreator.objectNodeMap.TryGetValue(rh, out obj2);
                    character_graph.grabbed_right = obj2;
                }
                else
                {
                    character_graph.grabbed_right = null;
                }

                foreach (KeyValuePair<Tuple<string, int>, ScriptObjectData> entry in currentState.scriptObjects)
                {
                    if (entry.Value.OpenStatus == OpenStatus.OPEN)
                    {
                        currentGraphCreator.objectNodeMap[entry.Value.GameObject].states_set.Remove(Utilities.ObjectState.CLOSED);
                        currentGraphCreator.objectNodeMap[entry.Value.GameObject].states_set.Add(Utilities.ObjectState.OPEN);
                    }
                    else if (entry.Value.OpenStatus == OpenStatus.CLOSED)
                    {
                        currentGraphCreator.objectNodeMap[entry.Value.GameObject].states_set.Remove(Utilities.ObjectState.OPEN);
                        currentGraphCreator.objectNodeMap[entry.Value.GameObject].states_set.Add(Utilities.ObjectState.CLOSED);
                    }
                }

                currentGraph = currentGraphCreator.UpdateGraph(transform, null, last_action);
                string fadeText = currentEpisode.checkTasks(currentGraph, currentGraphCreator);


                string task = JsonConvert.SerializeObject(currentEpisode.UpdateTasksString());
                string task_info = JsonConvert.SerializeObject(new ServerMessage("UpdateTask", task));
                for (int itt = 0; itt < licr.Count; itt++)
                {
                    if (licr[itt] != null)
                        licr[itt].SendData(task_info);
                }

                currentEpisode.StoreGraph(currentGraph, currTime, action_str, currentEpisode.posAndRotation.Count);
            }

            Debug.Log("Finished");





            scriptLines[char_id] = "";
            licr[char_id].SendData("DeleteButtons");

            click = false;
            Debug.Log("action executed");
            pointer[char_id].SetActive(false);

            //if (characters[char_id].transform.position != currentEpisode.previousPos || characters[char_id].transform.eulerAngles != currentEpisode.previousRotation)
            //{
            currentEpisode.AddCharInfo(char_id, characters[char_id].transform.position, characters[char_id].transform.eulerAngles, Time.time);
            //}
            currentEpisode.previousPos = characters[char_id].transform.position;
            currentEpisode.previousRotation = characters[char_id].transform.eulerAngles;

            //if (currentEpisode.IsCompleted || episodeDone)
            if (episodeDone)

            {
                currentEpisode.RecordData();
                for (int itt = 0; itt < licr.Count(); itt++)
                {
                    licr[itt].SendData("SaveTime");
                }
                string completed = "Tasks: Completed!\nLoading New Episode";
                string tasksCompletedText = completed.AddColor(Color.green);
                //tasksUI.text = tasksCompletedText;
                //FadeCompletionText(tasksCompletedText, newCanvas);
                //currentEpisode.RecordData(episode);
                episodeDone = true;
                episodeNum++;
            }

            t_lock = false;
            mre.Set();
            mre.Reset();
            executing_action[char_id] = false;
            yield return null;

        }
        // TODO: move this to utils
        void AddButton(string text)
        {
            //TODO: create buttons 
            GameObject buttonPrefab = Resources.Load("UI/UIButton") as GameObject;
            GameObject curr_button = (GameObject)Instantiate(buttonPrefab);
            curr_button.name = text + "Button";
            //curr_button.tag = "Button";
            curr_button.GetComponentInChildren<TextMeshProUGUI>().text = text;
            curr_button.GetComponentInChildren<TextMeshProUGUI>().color = Color.red; //does this actually work?
            curr_button.GetComponentInChildren<TextMeshProUGUI>().enableWordWrapping = false;
            var panel = GameObject.Find("Canvas");
            curr_button.transform.position = panel.transform.position;
            curr_button.GetComponent<RectTransform>().SetParent(panel.transform);
            curr_button.GetComponent<RectTransform>().SetInsetAndSizeFromParentEdge(RectTransform.Edge.Left, 0, 100);
            curr_button.layer = 5;

        }

        public void FadeCompletionText(string fadeText, GameObject canv)
        {
            GameObject compText = new GameObject("CompletedText");
            compText.AddComponent<TextMeshProUGUI>();
            TextMeshProUGUI fadingText = compText.GetComponent<TextMeshProUGUI>();
            fadingText.raycastTarget = false;
            fadingText.fontSize = 18;
            float fadingW = fadingText.maxWidth / 2;
            float fadingH = fadingText.maxHeight / 2;
            fadingText.rectTransform.localPosition = new Vector3(-fadingW, -fadingH, 0);
            fadingText.text = fadeText;
            fadingText.alignment = TextAlignmentOptions.Center;
            compText.transform.SetParent(canv.transform, false);
            StartCoroutine(FadeTextToZeroAlpha(10, fadingText));
        }

        public IEnumerator FadeTextToZeroAlpha(float t, TextMeshProUGUI i)
        {
            i.color = new Color(i.color.r, i.color.g, i.color.b, 1);
            while (i.color.a > 0.0f)
            {
                i.color = new Color(i.color.r, i.color.g, i.color.b, i.color.a - (8 * Time.deltaTime / t));
                yield return null;
            }
            Destroy(i.gameObject);
        }

        

        private bool SeenByCamera(Camera camera, Transform transform)
        {
            Vector3 origin = camera.transform.position;
            Bounds bounds = GameObjectUtils.GetBounds(transform.gameObject);
            List<Vector3> checkPoints = new List<Vector3>();
            Vector3 bmin = bounds.min;
            Vector3 bmax = bounds.max;
            checkPoints.Add(bmin);
            checkPoints.Add(bmax);
            checkPoints.Add(new Vector3(bmin.x, bmin.y, bmax.z));
            checkPoints.Add(new Vector3(bmin.x, bmax.y, bmin.z));
            checkPoints.Add(new Vector3(bmax.x, bmin.y, bmin.z));
            checkPoints.Add(new Vector3(bmin.x, bmax.y, bmax.z));
            checkPoints.Add(new Vector3(bmax.x, bmin.y, bmax.z));
            checkPoints.Add(new Vector3(bmax.x, bmax.y, bmin.z));
            checkPoints.Add(bounds.center);

            return checkPoints.Any(p => checkHit(origin, p, bounds));
        }

        private bool checkHit(Vector3 origin, Vector3 dest, Bounds bounds, double yTolerance = 0.001)
        {


            RaycastHit hit;

            if (!Physics.Raycast(origin, (dest - origin).normalized, out hit)) return false;

            if (!bounds.Contains(hit.point))
            {
                return false;
            }
            // if (Mathf.Abs(hit.point.y - dest.y) > yTolerance) return false;

            return true;
        }

        private FrontCameraControl CreateFrontCameraControls(GameObject character)
        {
            List<Camera> charCameras = CameraExpander.AddCharacterCameras(character, transform, CameraExpander.FORWARD_VIEW_CAMERA_NAME);
            CameraUtils.DeactivateCameras(charCameras);
            Camera camera = charCameras.First(c => c.name == CameraExpander.FORWARD_VIEW_CAMERA_NAME);
            return new FrontCameraControl(camera);
        }

        private FixedCameraControl CreateFixedCameraControl(GameObject character, string cameraName, bool focusToObject)
        {
            List<Camera> charCameras = CameraExpander.AddCharacterCameras(character, transform, cameraName);
            CameraUtils.DeactivateCameras(charCameras);
            Camera camera = charCameras.First(c => c.name == cameraName);
            return new FixedCameraControl(camera) { DoFocusObject = focusToObject };
        }

        private void updateRecorder(ExecutionConfig config, string outDir, Recorder rec)
        {
            ICameraControl cameraControl = null;
            if (config.recording)
            {
                // Add extra cams
                if (rec.CamCtrls == null)
                    rec.CamCtrls = new List<ICameraControl>();
                if (config.camera_mode.Count > rec.CamCtrls.Count)
                {
                    for (int extra_cam = 0; extra_cam < (config.camera_mode.Count - rec.CamCtrls.Count);)
                    {
                        rec.CamCtrls.Add(null);
                    }
                }
                else
                {
                    for (int extra_cam = config.camera_mode.Count; extra_cam < rec.CamCtrls.Count; extra_cam++)
                    {
                        rec.CamCtrls[extra_cam] = null;
                    }
                }

                for (int cam_id = 0; cam_id < config.camera_mode.Count; cam_id++)
                {

                    if (rec.CamCtrls[cam_id] != null && cam_id < rec.currentCameraMode.Count && rec.currentCameraMode[cam_id].Equals(config.camera_mode[cam_id]))
                    {
                        rec.CamCtrls[cam_id].Activate(true);
                    }
                    else
                    {
                        if (rec.CamCtrls[cam_id] != null)
                        {
                            rec.CamCtrls[cam_id].Activate(false);
                        }

                        CharacterControl chc = characters[rec.charIdx];
                        int index_cam = 0;
                        if (int.TryParse(config.camera_mode[cam_id], out index_cam))
                        {
                            //Camera cam = new Camera();
                            //cam.CopyFrom(sceneCameras[index_cam]);
                            Camera cam = cameras[index_cam];
                            cameraControl = new FixedCameraControl(cam);
                        }
                        else
                        {
                            switch (config.camera_mode[cam_id])
                            {
                                case "FIRST_PERSON":
                                    cameraControl = CreateFixedCameraControl(chc.gameObject, CameraExpander.FORWARD_VIEW_CAMERA_NAME, false);
                                    break;
                                case "PERSON_TOP":
                                    cameraControl = CreateFixedCameraControl(chc.gameObject, CameraExpander.TOP_CHARACTER_CAMERA_NAME, false);
                                    break;
                                case "PERSON_FROM_BACK":
                                    cameraControl = CreateFixedCameraControl(chc.gameObject, CameraExpander.FROM_BACK_CAMERA_NAME, false);
                                    break;
                                case "PERSON_FROM_LEFT":
                                    cameraControl = CreateFixedCameraControl(chc.gameObject, CameraExpander.FROM_LEFT_CAMERA_NAME, false);
                                    break;
                                case "PERSON_FRONT":
                                    cameraControl = CreateFrontCameraControls(chc.gameObject);
                                    break;
                                // case "TOP_VIEW":
                                //     // every character has 6 cameras
                                //     Camera top_cam = new Camera();
                                //     top_cam.CopyFrom(cameras[cameras.Count - 6 * numCharacters - 1]);
                                //     cameraControl = new FixedCameraControl(top_cam);
                                //     break;
                                default:
                                    AutoCameraControl autoCameraControl = new AutoCameraControl(sceneCameras, chc.transform, new Vector3(0, 1.0f, 0));
                                    autoCameraControl.RandomizeCameras = config.randomize_execution;
                                    autoCameraControl.CameraChangeEvent += rec.UpdateCameraData;
                                    cameraControl = autoCameraControl;
                                    break;
                            }

                        }

                        cameraControl.Activate(true);
                        rec.CamCtrls[cam_id] = cameraControl;
                    }
                }
            }
            //Debug.Log($"config.recording : {config.recording}");
            //Debug.Log($"cameraCtrl is not null2? : {cameraControl != null}");
            rec.Recording = config.recording;

            rec.currentCameraMode = config.camera_mode;
            rec.FrameRate = config.frame_rate;
            rec.imageSynthesis = config.image_synthesis;
            rec.savePoseData = config.save_pose_data;
            rec.saveSceneStates = config.save_scene_states;
            rec.FileName = config.file_name_prefix;
            rec.ImageWidth = config.image_width;
            rec.ImageHeight = config.image_height;
            rec.OutputDirectory = outDir;
        }

        private void createRecorder(ExecutionConfig config, string outDir, int index)
        {
            Recorder rec = recorders[index];
            updateRecorder(config, outDir, rec);
        }

        private void createRecorders(ExecutionConfig config)
        {
            // For the 1st Recorder.
            recorders.Clear();
            recorders.Add(GetComponent<Recorder>());
            recorders[0].charIdx = 0;
            if (numCharacters > 1)
            {
                for (int i = 1; i < numCharacters; i++)
                {
                    recorders.Add(gameObject.AddComponent<Recorder>() as Recorder);
                    recorders[i].charIdx = i;
                }
            }

            for (int i = 0; i < numCharacters; i++)
            {
                string outDir = Path.Combine(config.output_folder, config.file_name_prefix, i.ToString());
                Directory.CreateDirectory(outDir);
                createRecorder(config, outDir, i);
            }
        }

        private void updateRecorders(ExecutionConfig config)
        {
            for (int i = 0; i < numCharacters; i++)
            {
                string outDir = Path.Combine(config.output_folder, config.file_name_prefix, i.ToString());
                if (!Directory.Exists(outDir))
                {
                    Directory.CreateDirectory(outDir);
                }
                updateRecorder(config, outDir, recorders[i]);

            }
        }

        private List<ScriptExecutor> InitScriptExecutors(ExecutionConfig config, IObjectSelectorProvider objectSel, List<Camera> sceneCameras, IList<GameObject> objectList)
        {
            List<ScriptExecutor> sExecutors = new List<ScriptExecutor>();

            InteractionCache interaction_cache = new InteractionCache();
            for (int i = 0; i < numCharacters; i++)
            {
                CharacterControl chc = characters[i];
                chc.DoorControl.Update(objectList);


                // Initialize the scriptExecutor for the character
                ScriptExecutor sExecutor = new ScriptExecutor(objectList, dataProviders.RoomSelector, objectSel, recorders[i], i, interaction_cache, !config.skip_animation, config.walk_before_interaction);
                sExecutor.RandomizeExecution = config.randomize_execution;
                sExecutor.ProcessingTimeLimit = config.processing_time_limit;
                sExecutor.SkipExecution = config.skip_execution;
                sExecutor.AutoDoorOpening = false;

                sExecutor.Initialize(chc, recorders[i].CamCtrls);
                sExecutors.Add(sExecutor);
            }
            return sExecutors;
        }


        private void InitCharacterForCamera(GameObject character)
        {
            // This helps with rendering issues (disappearing suit on some cameras)
            SkinnedMeshRenderer[] mrCompoments = character.GetComponentsInChildren<SkinnedMeshRenderer>();

            foreach (SkinnedMeshRenderer mr in mrCompoments)
            {
                mr.updateWhenOffscreen = true;
                mr.enabled = false;
                mr.enabled = true;
            }
        }

        private void InitRooms()
        {
            List<GameObject> rooms = ScriptUtils.FindAllRooms(transform);

            foreach (GameObject r in rooms)
            {
                r.AddComponent<Properties_room>();
            }
        }

        private void InitRandom(int seed)
        {
            if (seed >= 0)
            {
                UnityEngine.Random.InitState(seed);
            }
            else
            {
                UnityEngine.Random.InitState((int)DateTimeOffset.Now.ToUnixTimeMilliseconds());
            }
        }

        private Dictionary<int, float[]> GetInstanceColoring(IList<EnvironmentObject> graphNodes)
        {
            Dictionary<int, float[]> result = new Dictionary<int, float[]>();

            foreach (EnvironmentObject eo in graphNodes)
            {
                if (eo.transform == null)
                {
                    result[eo.id] = new float[] { 1.0f, 1.0f, 1.0f };
                }
                else
                {
                    int instId = eo.transform.gameObject.GetInstanceID();
                    Color instColor;

                    if (ColorEncoding.instanceColor.TryGetValue(instId, out instColor))
                    {
                        result[eo.id] = new float[] { instColor.r, instColor.g, instColor.b };
                    }
                    else
                    {
                        result[eo.id] = new float[] { 1.0f, 1.0f, 1.0f };
                    }
                }
            }
            return result;
        }

        private void StopCharacterAnimation(GameObject character)
        {
            Animator animator = character.GetComponent<Animator>();
            if (animator != null)
            {
                animator.speed = 0;
            }
        }

        private bool CheckCameraIndexes(IList<int> indexes, int count)
        {
            if (indexes == null) return false;
            else if (indexes.Count == 0) return true;
            else return indexes.Min() >= 0 && indexes.Max() < count;
        }

        private int ParseCameraPass(string string_mode)
        {
            if (string_mode == null) return 0;
            else return Array.IndexOf(ImageSynthesis.PASSNAMES, string_mode.ToLower());
        }

        private void CreateSceneInfoFile(string outDir, SceneData sd)
        {
            using (StreamWriter sw = new StreamWriter(Path.Combine(outDir, "sceneInfo.json")))
            {
                sw.WriteLine(JsonUtility.ToJson(sd, true));
            }
        }

        // Add character (prefab) at path to the scene and returns its CharacterControl
        // component
        // - If name == null or prefab does not exist, we keep the character currently in the scene
        // - Any other character in the scene is deactivated
        // - Character is "randomized" if randomizeExecution = true 
        private CharacterControl AddCharacter(string path, bool randomizeExecution, string mode, Vector3 position, string initial_room)
        {
            GameObject loadedObj = Resources.Load(ScriptUtils.TransformResourceFileName(path)) as GameObject;
            List<GameObject> sceneCharacters = ScriptUtils.FindAllCharacters(transform);

            if (loadedObj == null)
            {
                if (sceneCharacters.Count > 0) return sceneCharacters[0].GetComponent<CharacterControl>();
                else return null;
            }
            else
            {
                Transform destRoom;


                List<GameObject> rooms = ScriptUtils.FindAllRooms(transform);
                foreach (GameObject r in rooms)
                {
                    if (r.GetComponent<Properties_room>() == null)
                        r.AddComponent<Properties_room>();
                }


                if (sceneCharacters.Count > 0 && mode == "random") destRoom = sceneCharacters[0].transform.parent.transform;
                else
                {
                    string room_name = "livingroom";
                    if (mode == "fix_room")
                    {
                        room_name = initial_room;
                    }
                    int index = 0;
                    for (int i = 0; i < rooms.Count; i++)
                    {
                        if (rooms[i].name.Contains(room_name.Substring(1)))
                        {
                            index = i;
                        }
                    }
                    destRoom = rooms[index].transform;
                }

                if (mode == "fix_position")
                {
                    bool contained_somewhere = false;
                    for (int i = 0; i < rooms.Count; i++)
                    {
                        if (GameObjectUtils.GetRoomBounds(rooms[i]).Contains(position))
                        {
                            contained_somewhere = true;
                        }
                    }
                    if (!contained_somewhere)
                        return null;
                }

                GameObject newCharacter = Instantiate(loadedObj, destRoom) as GameObject;

                newCharacter.name = loadedObj.name;
                ColorEncoding.EncodeGameObject(newCharacter);
                CharacterControl cc = newCharacter.GetComponent<CharacterControl>();

                // sceneCharacters.ForEach(go => go.SetActive(false));
                newCharacter.SetActive(true);

                if (mode == "random")
                {
                    if (sceneCharacters.Count == 0 || randomizeExecution)
                    {
                        //newCharacter.transform.position = ScriptUtils.FindRandomCCPosition(rooms, cc);
                        var nma = newCharacter.GetComponent<NavMeshAgent>();
                        nma.Warp(ScriptUtils.FindRandomCCPosition(rooms, cc));
                        newCharacter.transform.rotation *= Quaternion.Euler(0, UnityEngine.Random.Range(-180.0f, 180.0f), 0);
                    }
                    else
                    {
                        //newCharacter.transform.position = sceneCharacters[0].transform.position;
                        var nma = newCharacter.GetComponent<NavMeshAgent>();
                        nma.Warp(sceneCharacters[0].transform.position);
                        newCharacter.transform.rotation = Quaternion.Euler(0, 0, 0);
                    }
                }
                else if (mode == "fix_position")
                {
                    var nma = newCharacter.GetComponent<NavMeshAgent>();
                    nma.Warp(position);
                    newCharacter.transform.rotation = Quaternion.Euler(0, 0, 0);
                }
                else if (mode == "fix_room")
                {
                    List<GameObject> rooms_selected = new List<GameObject>();
                    foreach (GameObject room in rooms)
                    {
                        if (room.name == destRoom.name)
                        {
                            rooms_selected.Add(room);
                        }
                    }
                    var nma = newCharacter.GetComponent<NavMeshAgent>();
                    nma.Warp(ScriptUtils.FindCCPosition(rooms_selected[0], cc));
                    newCharacter.transform.rotation = Quaternion.Euler(0, 0, 0);
                }
                // Must be called after correct char placement so that char's location doesn't change
                // after instantiation.
                if (recorder.saveSceneStates)
                {
                    State_char sc = newCharacter.AddComponent<State_char>();
                    sc.Initialize(rooms, recorder.sceneStateSequence);
                    cc.stateChar = sc;
                }

                return cc;
            }
        }

        public void PlaceCharacter(GameObject character, List<GameObject> rooms)
        {
            CharacterControl cc = character.GetComponent<CharacterControl>();
            var nma = character.GetComponent<NavMeshAgent>();
            nma.Warp(ScriptUtils.FindRandomCCPosition(rooms, cc));
            character.transform.rotation *= Quaternion.Euler(0, UnityEngine.Random.Range(-180.0f, 180.0f), 0);
        }
    }

    internal class InstanceSelectorProvider : IObjectSelectorProvider
    {
        private Dictionary<int, GameObject> idObjectMap;
        public Dictionary<GameObject, int> objectIdMap;

        public InstanceSelectorProvider(EnvironmentGraph currentGraph)
        {
            idObjectMap = new Dictionary<int, GameObject>();
            objectIdMap = new Dictionary<GameObject, int>();
            foreach (EnvironmentObject eo in currentGraph.nodes)
            {
                if (eo.transform != null)
                {
                    idObjectMap[eo.id] = eo.transform.gameObject;
                    objectIdMap[eo.transform.gameObject] = eo.id;
                }
            }
        }

        public IObjectSelector GetSelector(string name, int instance)
        {
            GameObject go;

            if (idObjectMap.TryGetValue(instance, out go)) return new FixedObjectSelector(go);
            else return EmptySelector.Instance;
        }
    }

    class OneTimeInitializer
    {
        public bool initialized = false;
        private Action defaultAction;

        public OneTimeInitializer()
        {
            defaultAction = () => { };
        }

        public OneTimeInitializer(Action defaultAction)
        {
            this.defaultAction = defaultAction;
        }

        public void Initialize(Action action)
        {
            if (!initialized)
            {
                action();
                initialized = true;
            }
        }

        public void Initialize()
        {
            Initialize(defaultAction);
        }
    }

    public class RecorderConfig
    {
        public string output_folder = "Output/";
        public string file_name_prefix = "script";
        public int frame_rate = 5;
        public List<string> image_synthesis = new List<string>();
        public bool save_pose_data = false;
        public bool save_scene_states = false;
        public bool randomize_recording = false;
        public int image_width = 640;
        public int image_height = 480;
        public List<string> camera_mode = new List<string>();
    }

    public class ImageConfig : RecorderConfig
    {
        public string mode = "normal";
    }

    public class CameraConfig
    {
        public Vector3 rotation = new Vector3(0.0f, 0.0f, 0.0f);
        public Vector3 position = new Vector3(0.0f, 0.0f, 0.0f);
        public float focal_length = 0.0f;

    }

    public class ExpanderConfig
    {
        public bool randomize_execution = false;
        public int random_seed = -1;
        public bool ignore_obstacles = false;
        public bool animate_character = false;
        public bool transfer_transform = true;
    }

    public class CharacterConfig
    {
        public int char_index = 0;
        public string character_resource = "Chars/Male1";
        public Vector3 character_position = new Vector3(0.0f, 0.0f, 0.0f);
        public string initial_room = "livingroom";
        public string mode = "random";
    }

    public class ExecutionConfig : RecorderConfig
    {
        public bool find_solution = true;
        public bool randomize_execution = false;
        public int random_seed = -1;
        public int processing_time_limit = 10;
        public bool recording = false;
        public bool skip_execution = false;
        public bool skip_animation = false;
        public bool walk_before_interaction = true;
    }

    public class DataProviders
    {
        public NameEquivalenceProvider NameEquivalenceProvider { get; private set; }
        public ActionEquivalenceProvider ActionEquivalenceProvider { get; private set; }
        public AssetsProvider AssetsProvider { get; private set; }
        public ObjectSelectionProvider ObjectSelectorProvider { get; private set; }
        public RoomSelector RoomSelector { get; private set; }
        public ObjectPropertiesProvider ObjectPropertiesProvider { get; private set; }

        public DataProviders()
        {
            Initialize();
        }

        private void Initialize()
        {
            NameEquivalenceProvider = new NameEquivalenceProvider("Data/class_name_equivalence");
            ActionEquivalenceProvider = new ActionEquivalenceProvider("Data/action_mapping");
            AssetsProvider = new AssetsProvider("Data/object_prefabs");
            ObjectSelectorProvider = new ObjectSelectionProvider(NameEquivalenceProvider);
            RoomSelector = new RoomSelector(NameEquivalenceProvider);
            ObjectPropertiesProvider = new ObjectPropertiesProvider(NameEquivalenceProvider);
        }
    }

    public static class DriverUtils
    {
        // For each argument in args of form '--name=value' or '-name=value' returns item ("name", "value") in 
        // the result dicitionary.
        public static IDictionary<string, string> GetParameterValues(string[] args)
        {
            Regex regex = new Regex(@"-{1,2}([^=]+)=([^=]+)");
            var result = new Dictionary<string, string>();

            foreach (string s in args)
            {
                Match match = regex.Match(s);
                if (match.Success)
                {
                    result[match.Groups[1].Value.Trim()] = match.Groups[2].Value.Trim();
                }
            }
            return result;
        }
    }

    public static class StringExtensions
    {
        public static string AddColor(this string text, Color col) => $"<color={ColorHexFromUnityColor(col)}>{text}</color>";
        public static string ColorHexFromUnityColor(this Color unityColor) => $"#{ColorUtility.ToHtmlStringRGBA(unityColor)}";
    }

    [System.Serializable]
    public class Episode
    {
        public string file_name = "default";
        public int env_id;
        public int task_id;
        public int episode;
        public string task_name;
        public int level;
        public string[] init_rooms;
        public Task[] task_goal;
        public EnvironmentGraph init_graph;
        public List<Goal> goals = new List<Goal>();
        public bool IsCompleted = false;

        private List<(string, float, string, int)> allGraphs = new List<(string, float, string, int)>();

        public List<(int, Vector3, Vector3, float)> posAndRotation = new List<(int, Vector3, Vector3, float)>();

        private List<(string, float)> scriptActions = new List<(string, float)>();
        private Dictionary<(string,string), List<string>> goalRelations = new Dictionary<(string,string), List<string>>();
        private HashSet<string> allPlacedObjects = new HashSet<string>();

        public Vector3 previousPos = new Vector3(0, 0, 0);
        public Vector3 previousRotation = new Vector3(0, 0, 0);

        public void ClearDataFile()
        {
            string outputPath = $"Episodes/{file_name}_Episode{episode}Data.txt";
            using (FileStream fs = File.Create(outputPath)) { }
            foreach (EnvironmentRelation r in init_graph.edges)
            {
                r.relation = (ObjectRelation)Enum.Parse(typeof(ObjectRelation), r.relation_type);
            }
            foreach (EnvironmentObject o in init_graph.nodes)
            {
                if (o.obj_transform.position == null)
                {
                    o.obj_transform = null;
                }
                if (o.bounding_box.center == null)
                {
                    o.bounding_box = null;
                }
                foreach (string s in o.states)
                {
                    o.states_set.Add((Utilities.ObjectState)Enum.Parse(typeof(Utilities.ObjectState), s));
                }
            }
        }

        public void RecordData()
        {
            string outputPath = $"Episodes/{file_name}_Episode{episode}Data.txt";
            File.WriteAllText(outputPath, "");
            StreamWriter outputFile = new StreamWriter(outputPath, true);
            outputFile.WriteLine("Position and Orientation Data:");
            foreach ((int, Vector3, Vector3, float) pos in posAndRotation)
            {
                outputFile.WriteLine(pos.ToString());
            }
            outputFile.WriteLine("Script Action Data:");
            foreach ((string, float) action in scriptActions)
            {
                outputFile.WriteLine(action);
            }
            outputFile.WriteLine("Graph Data:");
            foreach ((string, float, string, int) g in allGraphs)
            {
                List<string> graph_res = new List<string>();
                graph_res.Add(g.Item1);
                graph_res.Add(g.Item2.ToString());
                graph_res.Add(g.Item3);
                graph_res.Add(g.Item4.ToString());
                outputFile.WriteLine(JsonConvert.SerializeObject(graph_res));
            }
            outputFile.Close();
        }

        public void AddCharInfo(int char_id, Vector3 coord, Vector3 rot, float t)
        {
            posAndRotation.Add((char_id, coord, rot, t));
        }

        public void FailAction()
        {
            // Make the last action fail
            if (scriptActions.Count > 0)
            {
                var (action, t) = scriptActions[scriptActions.Count - 1];
                action = "failure " + action;
                scriptActions[scriptActions.Count - 1] = (action, t);
            }
            
        }
        public void AddAction(string a, float t)
        {
            scriptActions.Add((a, t));
        }

        public string GenerateTasksAndGoals()
        {
            string response = "Tasks: \n";
            foreach (Task t in task_goal)
            {
                string action = t.task;
                string obj1 = action.Split('{', '}')[1];
                string obj2 = action.Split('{', '}')[3];
                string verb = action.Split('_')[0];
                string rel = action.Split('_')[2];
                //string[] a = action.Split('_');
                response += $"{verb} {obj1} {rel} {obj2} x{t.repetitions}\n";
                Goal newG = new Goal(verb, rel, obj1, obj2, t.repetitions);
                goals.Add(newG);
                allPlacedObjects.Add(obj1);
                if (!goalRelations.ContainsKey((obj2, rel)))
                {
                    goalRelations.Add((obj2, rel), new List<string>());
                }
                for (int i = 0; i < t.repetitions; i++)
                {
                    goalRelations[(obj2, rel)].Add(obj1);
                }
            }
            return response;
        }

        public bool IsOn(EnvironmentObject o1, EnvironmentObject o2, EnvironmentGraphCreator graphCreator)
        {
            return graphCreator.HasEdge(o1.id, o2.id, ObjectRelation.ON);
        }

        public bool IsInside(EnvironmentObject o1, EnvironmentObject o2, EnvironmentGraphCreator graphCreator)
        {
            return graphCreator.HasEdge(o1.id, o2.id, ObjectRelation.INSIDE);
        }

        public List<string> RelationsCompleted(List<string> objs, (string, string) dest, EnvironmentGraph g, EnvironmentGraphCreator gc)
        {
            List<string> allObjsNeeded = new List<string>(objs);
            List<EnvironmentObject> platforms = new List<EnvironmentObject>();
            foreach (EnvironmentObject o in g.nodes) { if (o.class_name.Equals(dest.Item1)) { platforms.Add(o); } }
            foreach (EnvironmentObject env_obj in g.nodes)
            {
                if (objs.Contains(env_obj.class_name))
                {
                    foreach (EnvironmentObject platform in platforms)
                    {
                        if (dest.Item2.Equals("on") && IsOn(env_obj, platform, gc))
                        {
                            allObjsNeeded.Remove(env_obj.class_name);
                        }
                        if (dest.Item2.Equals("in") && IsInside(env_obj, platform, gc))
                        {
                            allObjsNeeded.Remove(env_obj.class_name);
                        }
                    } 
                }
            }
            return allObjsNeeded;
        }

        public Dictionary<(string, string), List<string>> makeRelGoalsCopy()
        {
            Dictionary<(string, string), List<string>> cp = new Dictionary<(string, string), List<string>>();
            foreach ((string, string) dest in goalRelations.Keys.ToList())
            {
                cp[dest] = new List<string>();
                foreach (string plat in goalRelations[dest])
                {
                    cp[dest].Add(string.Copy(plat));
                }
            }
            return cp;
        }

        public string checkTasks(EnvironmentGraph g, EnvironmentGraphCreator gc)
        {
            string completedTask = "None";
            Dictionary<(string, string), List<string>> goalsCopy = makeRelGoalsCopy();
            foreach ((string, string) dest in goalsCopy.Keys.ToList())
            {
                goalsCopy[dest] = RelationsCompleted(goalsCopy[dest], dest, g, gc);
            }
            foreach (Goal goal in goals)
            {
                int newReps = goalsCopy[(goal.obj2, goal.relation)].Where(x => x.Equals(goal.obj1)).Count();
                if (newReps != 0)
                {
                    completedTask = $"Task Completed!:\n {goal.verb} {goal.obj1} {goal.relation} {goal.obj2} x{goal.repetitions - newReps}";
                    completedTask = $"{completedTask.AddColor(Color.green)}";
                }
                goal.count = goal.repetitions - newReps;
            }
            return completedTask;
        }

        //public string UpdateTasksString()
        //{
        //    string response = "Tasks: \n".AddColor(Color.black);
        //    bool moreTasks = false;
        //    foreach (Goal g in goals)
        //    {
        //        if (g.repetitions == 0)
        //        {
        //            string s = $"{g.verb} {g.obj1} {g.relation} {g.obj2} x{g.repetitions}\n";
        //            response += $"{s.AddColor(Color.green)}";
        //        }
        //        else
        //        {
        //            moreTasks = true;
        //            string s = $"{g.verb} {g.obj1} {g.relation} {g.obj2} x{g.repetitions}\n";
        //            response += $"{s.AddColor(Color.black)}"; 
        //        }
        //    }
        //    if (!moreTasks)
        //    {
        //        IsCompleted = true;
        //    }
        //    return response;
        //}

        public List<Goal> UpdateTasksString()
        {
            bool moreTasks = false;
            foreach (Goal g in goals)
            {

                if (g.repetitions != g.count)
                {

                    moreTasks = true;
                }
            }
            if (!moreTasks)
            {
                IsCompleted = true;
            }
            return goals;
        }

        public void StoreGraph(EnvironmentGraph g, float t, string action_str, int ct)
        {
            string graphString = JsonUtility.ToJson(g);
            allGraphs.Add((graphString, t, action_str, ct));
        }

    }
    [System.Serializable]
    public class Task
    {
        public string task;
        public int repetitions;
    }

    public class Goal
    {
        public string verb { get; set; }
        public string relation { get; set; }
        public string obj1 { get; set; }
        public string obj2 { get; set; }
        public int repetitions { get; set; }
        public int count { get; set; }

        public Goal(string v, string rel, string o1, string o2, int reps)
        {
            verb = v;
            relation = rel;
            obj1 = o1;
            obj2 = o2;
            repetitions = reps;
            count = 0;
        }
    }

}
