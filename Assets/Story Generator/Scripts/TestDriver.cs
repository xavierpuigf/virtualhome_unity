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
using StoryGenerator.Helpers;
using TMPro;

namespace StoryGenerator
{
    [RequireComponent(typeof(Recorder))]
    public class TestDriver : MonoBehaviour
    {

        static ProfilerMarker s_GetGraphMarker = new ProfilerMarker("MySystem.GetGraph");
        static ProfilerMarker s_GetMessageMarker = new ProfilerMarker("MySystem.GetMessage");
        static ProfilerMarker s_UpdateGraph = new ProfilerMarker("MySystem.UpdateGraph");
        static ProfilerMarker s_SimulatePerfMarker = new ProfilerMarker("MySystem.Simulate");


        private const int DefaultPort = 8080;
        private const int DefaultTimeout = 500000;


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
                string episodePath = $"Episodes/pilot_task_id_{episodeNum + 1}";
                TextAsset episodeFile = Resources.Load<TextAsset>(episodePath);
                Episode currentEpisode = JsonUtility.FromJson<Episode>(episodeFile.text);
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
            int port = DefaultPort;

            if (!argDict.TryGetValue("http-port", out portString) || !Int32.TryParse(portString, out port))
                port = DefaultPort;

            commServer = new HttpCommunicationServer(port) { Timeout = DefaultTimeout };
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

        IEnumerator ProcessInputRequest(int episode)
        {
            yield return null;
            string episodePath = $"Episodes/pilot_task_id_{episode}";
            TextAsset episodeFile = Resources.Load<TextAsset>(episodePath);
            Episode currentEpisode = JsonUtility.FromJson<Episode>(episodeFile.text);
            currentEpisode.ClearDataFile(episode);

            sceneCameras = ScriptUtils.FindAllCameras(transform);
            numSceneCameras = sceneCameras.Count;
            cameras = sceneCameras.ToList();
            cameras.AddRange(CameraExpander.AddRoomCameras(transform));
            CameraUtils.DeactivateCameras(cameras);
            OneTimeInitializer cameraInitializer = new OneTimeInitializer();
            OneTimeInitializer homeInitializer = new OneTimeInitializer();
            EnvironmentGraphCreator currentGraphCreator = null;
            EnvironmentGraph currentGraph = null;
            //int expandSceneCount = 0;

            InitRooms();

            cameraInitializer.initialized = false;
            if (currentGraph == null)
            {
                currentGraphCreator = new EnvironmentGraphCreator(dataProviders);
                currentGraph = currentGraphCreator.CreateGraph(transform);
            }

            /*
            //EXPAND SCENE
            cameraInitializer.initialized = false;
            List<IEnumerator> animationEnumerators = new List<IEnumerator>();
            int expandSceneCount = 0;
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
                c.GetComponent<Animator>().speed = 0;
            }
            */
            

            //add one character by default
            CharacterConfig configchar = new CharacterConfig();//JsonConvert.DeserializeObject<CharacterConfig>(networkRequest.stringParams[0]);
            CharacterControl newchar;
            newchar = AddCharacter(configchar.character_resource, false, "fix_room", configchar.character_position, currentEpisode.init_rooms[0]);


            characters.Add(newchar);
            CurrentStateList.Add(null);
            numCharacters++;
            List<Camera> charCameras = CameraExpander.AddCharacterCameras(newchar.gameObject, transform, "");
            CameraUtils.DeactivateCameras(charCameras);
            cameras.AddRange(charCameras);

            currentGraph = currentGraphCreator.UpdateGraph(transform);

            bool keyPressed = false;
            bool leftExecuted = false;
            bool rightExecuted = false;
            bool click = false;

            ExecutionConfig config = new ExecutionConfig();
            config.walk_before_interaction = false;
            IObjectSelectorProvider objectSelectorProvider = new InstanceSelectorProvider(currentGraph);
            IList<GameObject> objectList = ScriptUtils.FindAllObjects(transform);
            createRecorders(config);
            sExecutors = InitScriptExecutors(config, objectSelectorProvider, sceneCameras, objectList);

            Camera currentCamera = cameras.Find(c => c.name.Equals("Character_Camera_Fwd"));
            currentCamera.gameObject.SetActive(true);
            //recorders[0].CamCtrls[cameras.IndexOf(currentCamera)].Activate(true);
            currentCamera.transform.localPosition = currentCamera.transform.localPosition + new Vector3(0, -0.15f, 0.1f);

            
            // Buttons: grab, open, putleft, putright, close

            // Create canvas and event system
            GameObject newCanvas = new GameObject("Canvas");
            Canvas canv = newCanvas.AddComponent<Canvas>();
            canv.renderMode = RenderMode.ScreenSpaceOverlay;
            newCanvas.AddComponent<CanvasScaler>();
            newCanvas.AddComponent<GraphicRaycaster>();
            GameObject eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

            TextMeshProUGUI tasksUI = canv.gameObject.AddComponent<TextMeshProUGUI>();
            tasksUI.raycastTarget = false;
            List<string> goals = new List<string>();
            tasksUI.fontSize = 12;
            tasksUI.text = currentEpisode.GenerateTasksAndGoals();

            AddButton("Open");
            AddButton("Grab");
            AddButton("PutLeft");
            AddButton("PutRight");

            GameObject goOpen = GameObject.Find("OpenButton");
            GameObject goGrab = GameObject.Find("GrabButton");
            GameObject goPutLeft = GameObject.Find("PutLeftButton");
            GameObject goPutRight = GameObject.Find("PutRightButton");
            
            goOpen.gameObject.tag = "HPG_0";
            goGrab.gameObject.tag = "HPG_0";
            goPutLeft.gameObject.tag = "HPG_0";
            goPutRight.gameObject.tag = "HPG_0";
            

            bool button_created = false;
            goOpen.SetActive(false);
            goGrab.SetActive(false);
            goPutLeft.SetActive(false);
            goPutRight.SetActive(false);

            List<string> scriptLines = new List<string>();

            GameObject pointer = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            pointer.transform.localScale = new Vector3(0.05f, 0.05f, 0.05f);
            pointer.GetComponent<MeshRenderer>().material.color = Color.magenta;
            currentEpisode.StoreGraph(currentGraph, 0);

            while (!episodeDone)
            {
                click = false;
                float currTime = Time.time;
                ISet<GameObject> movedObjects = new HashSet<GameObject>();
                if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W))
                {
                    string move = "<char0> [walkforward]";
                    currentEpisode.AddAction(move, currTime);
                    movedObjects.Add(newchar.gameObject);
                    scriptLines.Add(move);
                    Debug.Log("move forward");
                    keyPressed = true;
                }
                else if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A))
                {
                    string move = "<char0> [turnleft]";
                    currentEpisode.AddAction(move, currTime);
                    movedObjects.Add(newchar.gameObject);
                    scriptLines.Add(move);
                    Debug.Log("move left");
                    keyPressed = true;
                }
                else if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
                {
                    string move = "<char0> [turnright]";
                    currentEpisode.AddAction(move, currTime);
                    movedObjects.Add(newchar.gameObject);
                    scriptLines.Add(move);
                    Debug.Log("move right");
                    keyPressed = true;
                }
                //TODO: add going backwards and clean up Input code


                //TODO: add camera movement
                if (Input.GetKeyDown(KeyCode.O))
                {
                    Debug.Log("move cam up");
                    currentEpisode.AddAction("move cam up", currTime);
                    currentCamera.transform.Rotate(-3, 0, 0);
                }
                else if (Input.GetKeyDown(KeyCode.L))
                {
                    Debug.Log("move cam down");
                    currentEpisode.AddAction("move cam down", currTime);
                    currentCamera.transform.Rotate(3, 0, 0);
                }

                if (Input.GetMouseButtonDown(0))
                {
                    Debug.Log("mouse down");
                    if (EventSystem.current.IsPointerOverGameObject() &&
                        EventSystem.current.currentSelectedGameObject != null &&
                        !EventSystem.current.currentSelectedGameObject.CompareTag("HPG_0"))
                    {
                        click = true;
                        Debug.Log("button clicked");
                    }
                    if (button_created)
                    {

                        goOpen.SetActive(false);
                        goOpen.GetComponent<Button>().onClick.RemoveAllListeners();
                        goGrab.SetActive(false);
                        goGrab.GetComponent<Button>().onClick.RemoveAllListeners();
                        goPutLeft.SetActive(false);
                        goPutLeft.GetComponent<Button>().onClick.RemoveAllListeners();
                        goPutRight.SetActive(false);
                        goPutRight.GetComponent<Button>().onClick.RemoveAllListeners();
                        button_created = false;
                        pointer.SetActive(false);
                        //break;
                        //continue;
                    }
                    if (!click)
                    {
                        RaycastHit rayHit;
                        Vector3 mouseClickPosition = Input.mousePosition;
                        Ray ray = currentCamera.ScreenPointToRay(mouseClickPosition);
                        bool hit = Physics.Raycast(ray, out rayHit);
                        //Debug.DrawRay(ray.origin, ray.direction, Color.green, 20, true);

                        if (hit)
                        {
                            click = true;
                            Transform t = rayHit.transform;
                            pointer.SetActive(true);

                            pointer.transform.position = rayHit.point;

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
                                Debug.Log(t.gameObject.name);
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
                            mousePos.y = currentCamera.pixelHeight - Input.mousePosition.y;
                            Vector3 point = currentCamera.ScreenToWorldPoint(new Vector3(mousePos.x, mousePos.y, currentCamera.nearClipPlane));
                            Debug.Log("point " + point);

                            //TODO: grabbing/putting with right and left hands
                            State currentState = this.CurrentStateList[0];
                            GameObject rh = currentState.GetGameObject("RIGHT_HAND_OBJECT");
                            GameObject lh = currentState.GetGameObject("LEFT_HAND_OBJECT");
                            EnvironmentObject obj1;
                            EnvironmentObject obj2;
                            EnvironmentObject obj3;
                            currentGraphCreator.objectNodeMap.TryGetValue(characters[0].gameObject, out obj1);
                            Character character_graph;
                            currentGraphCreator.characters.TryGetValue(obj1, out character_graph);

                            if (objProperties.Contains("CAN_OPEN") && !goOpen.activeSelf)
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
                                    goOpen.GetComponentInChildren<TextMeshProUGUI>().text = "Open " + objectName;
                                    float width = goOpen.GetComponentInChildren<TextMeshProUGUI>().preferredWidth + 50;
                                    float pheight = mousePos.y + 60;
                                    float height = 80;
                                    goOpen.SetActive(true);
                                    Button buttonOpen = goOpen.GetComponent<Button>();
                                    goOpen.GetComponent<RectTransform>().SetInsetAndSizeFromParentEdge(RectTransform.Edge.Left, mousePos.x - width / 2, width);
                                    goOpen.GetComponent<RectTransform>().SetInsetAndSizeFromParentEdge(RectTransform.Edge.Top, pheight, height);
                                    button_created = true;
                                    buttonOpen.onClick.AddListener(() =>
                                    {
                                        Debug.Log("opened");
                                        button_created = false;
                                        objStates.Remove(Utilities.ObjectState.CLOSED);
                                        objStates.Add(Utilities.ObjectState.OPEN);
                                        string action = String.Format("<char0> [open] <{0}> ({1})", objectName, objectId);
                                        currentEpisode.AddAction(action, currTime);
                                        movedObjects.Add(transform.Find(objectName).gameObject);
                                        
                                        currentEpisode.GoalMetSingleObj("open", objectName);
                                        tasksUI.text = currentEpisode.UpdateTasksString();
                                        scriptLines.Add(action);
                                        goOpen.SetActive(false);
                                        keyPressed = true;

                                        buttonOpen.onClick.RemoveAllListeners();
                                    });
                                }
                                else if (objStates.Contains(Utilities.ObjectState.OPEN))
                                {
                                    Debug.Log("close");
                                    goOpen.GetComponentInChildren<TextMeshProUGUI>().text = "Close " + objectName;
                                    float width = goOpen.GetComponentInChildren<TextMeshProUGUI>().preferredWidth + 50;
                                    float height = 80;
                                    float pheight = mousePos.y + 60;
                                    goOpen.SetActive(true);
                                    Button buttonOpen = goOpen.GetComponent<Button>();
                                    goOpen.GetComponent<RectTransform>().SetInsetAndSizeFromParentEdge(RectTransform.Edge.Left, mousePos.x - width / 2, width);
                                    goOpen.GetComponent<RectTransform>().SetInsetAndSizeFromParentEdge(RectTransform.Edge.Top, pheight, height);

                                    button_created = true;

                                    buttonOpen.onClick.AddListener(() =>
                                    {
                                        button_created = false;

                                        Debug.Log("closed");
                                        objStates.Remove(Utilities.ObjectState.OPEN);
                                        objStates.Add(Utilities.ObjectState.CLOSED);
                                        string action = String.Format("<char0> [close] <{0}> ({1})", objectName, objectId);
                                        movedObjects.Add(transform.Find(objectName).gameObject);
                                        
                                        currentEpisode.AddAction(action, currTime);
                                        currentEpisode.GoalMetSingleObj("close", objectName);
                                        tasksUI.text = currentEpisode.UpdateTasksString();
                                        scriptLines.Add(action);
                                        goOpen.SetActive(false);
                                        keyPressed = true;

                                        buttonOpen.onClick.RemoveAllListeners();
                                    });
                                }


                            }
                            else if (objProperties.Contains("GRABBABLE") && !goGrab.activeSelf)
                            {
                                Debug.Log("grab");

                                //TODO: fix highlight
                                /*if (rend != null)
                                {
                                    startcolor = rend.material.color;
                                    Debug.Log("rend not null! " + rend.material.color);
                                    rend.material.color = Color.yellow;
                                }*/

                                goGrab.GetComponentInChildren<TextMeshProUGUI>().text = "Grab " + objectName;
                                float width = goGrab.GetComponentInChildren<TextMeshProUGUI>().preferredWidth + 50;
                                float pheight = mousePos.y + 60;
                                float height = 80;
                                goGrab.SetActive(true);
                                Button buttonGrab = goGrab.GetComponent<Button>();
                                goGrab.GetComponent<RectTransform>().SetInsetAndSizeFromParentEdge(RectTransform.Edge.Left, mousePos.x - width / 2, width);
                                goOpen.GetComponent<RectTransform>().SetInsetAndSizeFromParentEdge(RectTransform.Edge.Top, pheight, height);

                                button_created = true;

                                buttonGrab.onClick.AddListener(() =>
                                {
                                    Debug.Log("grabbed");
                                    button_created = false;

                                    string action = String.Format("<char0> [grab] <{0}> ({1})", objectName, objectId);
                                    movedObjects.Add(transform.Find(objectName).gameObject);
                                    
                                    currentEpisode.AddAction(action, currTime);
                                    currentEpisode.GoalMetSingleObj("grab", objectName);
                                    tasksUI.text = currentEpisode.UpdateTasksString();
                                    Debug.Log(action);
                                    scriptLines.Add(action);
                                    goGrab.SetActive(false);
                                    keyPressed = true;

                                    buttonGrab.onClick.RemoveAllListeners();
                                });
                            }

                            //put on/in surfaces
                            else if ((objProperties.Contains("SURFACES") || objProperties.Contains("CONTAINERS")) && (rh != null || lh != null) && (!goPutLeft.activeSelf || !goPutRight.activeSelf))
                            {
                                Debug.Log("put");

                                if (lh != null)
                                {
                                    currentGraphCreator.objectNodeMap.TryGetValue(lh, out obj2);
                                    Debug.Log("Put " + obj2.class_name + " on " + objectName);
                                    goPutLeft.GetComponentInChildren<TextMeshProUGUI>().text = "Put " + obj2.class_name + " on " + objectName;
                                    float width = goPutLeft.GetComponentInChildren<TextMeshProUGUI>().preferredWidth + 50;
                                    goPutLeft.SetActive(true);

                                    Button buttonPutLeft = goPutLeft.GetComponent<Button>();
                                    goPutLeft.GetComponent<RectTransform>().SetInsetAndSizeFromParentEdge(RectTransform.Edge.Left, mousePos.x - width / 2, width);
                                    goPutLeft.GetComponent<RectTransform>().SetInsetAndSizeFromParentEdge(RectTransform.Edge.Bottom, mousePos.y + 60, 80);
                                    button_created = true;

                                    buttonPutLeft.onClick.AddListener(() =>
                                    {
                                        Debug.Log("put left at " + rayHit.point);
                                        button_created = false;

                                        string putPos = String.Format("{0},{1},{2}", rayHit.point.x.ToString(), rayHit.point.y.ToString(), rayHit.point.z.ToString());

                                        string action = String.Format("<char0> [put] <{2}> ({3}) <{0}> ({1}) {4}", objectName, objectId, obj2.class_name, obj2.id, putPos);
                                        Debug.Log(action);
                                        movedObjects.Add(transform.Find(objectName).gameObject);
                                        
                                        currentEpisode.AddAction(action, currTime);
                                        currentEpisode.GoalMet("put", obj2.class_name, objectName);
                                        tasksUI.text = currentEpisode.UpdateTasksString();
                                        scriptLines.Add(action);

                                        leftExecuted = true;
                                        goPutLeft.SetActive(false);
                                        keyPressed = true;

                                        buttonPutLeft.onClick.RemoveAllListeners();
                                    });
                                }
                                if (rh != null)
                                {
                                    currentGraphCreator.objectNodeMap.TryGetValue(rh, out obj3);
                                    Debug.Log("Put " + obj3.class_name + " on " + objectName);
                                    goPutRight.GetComponentInChildren<TextMeshProUGUI>().text = "Put " + obj3.class_name + " on " + objectName;
                                    float width = goPutRight.GetComponentInChildren<TextMeshProUGUI>().preferredWidth + 50;
                                    goPutRight.SetActive(true);
                                    Button buttonPutRight = goPutRight.GetComponent<Button>();

                                    goPutRight.GetComponent<RectTransform>().SetInsetAndSizeFromParentEdge(RectTransform.Edge.Left, mousePos.x - width / 2, width);
                                    goPutRight.GetComponent<RectTransform>().SetInsetAndSizeFromParentEdge(RectTransform.Edge.Bottom, mousePos.y - 60, 80);
                                    //TODO: are these buttons in the right location?
                                    button_created = true;

                                    buttonPutRight.onClick.AddListener(() =>
                                    {
                                        Debug.Log("put right at " + rayHit.point);
                                        button_created = false;


                                        string putPos = String.Format("{0},{1},{2}", rayHit.point.x.ToString(), rayHit.point.y.ToString(), rayHit.point.z.ToString());

                                        string action = String.Format("<char0> [put] <{2}> ({3}) <{0}> ({1}) {4}", objectName, objectId, obj3.class_name, obj3.id, putPos);
                                        Debug.Log(action);
                                        movedObjects.Add(transform.Find(objectName).gameObject);
                                        
                                        currentEpisode.AddAction(action, currTime);
                                        currentEpisode.GoalMet("put", obj3.class_name, objectName);
                                        tasksUI.text = currentEpisode.UpdateTasksString();
                                        scriptLines.Add(action);

                                        rightExecuted = true;
                                        goPutRight.SetActive(false);
                                        keyPressed = true;

                                        buttonPutRight.onClick.RemoveAllListeners();
                                    });

                                }

                            }


                        }
                    }
                }
                if (keyPressed)
                {
                    //un-highlight TODO 
                    /*if (rend != null)
                    {
                        rend.material.color = startcolor;
                        rend = null;
                    }*/
                    pointer.SetActive(false);
                    sExecutors[0].ClearScript();
                    Debug.Log("Scriptlines " + scriptLines[0]);
                    Debug.Log("Scriptlines count " + scriptLines.Count);
                    ScriptReader.ParseScript(sExecutors, scriptLines, dataProviders.ActionEquivalenceProvider);
                    StartCoroutine(sExecutors[0].ProcessAndExecute(false, this));
                    currentGraph = currentGraphCreator.UpdateGraph(transform, movedObjects);
                    tasksUI.text = currentEpisode.UpdateTasksString();

                    scriptLines.Clear();
                    Debug.Log("key pressed");
                    goOpen.SetActive(false);
                    goGrab.SetActive(false);

                    if (leftExecuted)
                    {
                        goPutLeft.SetActive(false);
                        leftExecuted = false;
                    }
                    if (rightExecuted)
                    {
                        goPutRight.SetActive(false);
                        rightExecuted = false;
                    }
                    
                    

                    keyPressed = false;
                    click = false;
                    Debug.Log("action executed");
                    pointer.SetActive(false);

                    currentEpisode.checkTasks(currentGraph);
                    currentEpisode.StoreGraph(currentGraph, currTime);
                }

                if (newchar.transform.position != currentEpisode.previousPos || newchar.transform.eulerAngles != currentEpisode.previousRotation)
                {
                    currentEpisode.AddCharInfo(newchar.transform.position, newchar.transform.eulerAngles, Time.time);
                }
                currentEpisode.previousPos = newchar.transform.position;
                currentEpisode.previousRotation = newchar.transform.eulerAngles;

                if (currentEpisode.IsCompleted)
                {
                    tasksUI.text = "Taks: Completed!";
                    currentEpisode.RecordData(episode);
                    episodeDone = true;
                    episodeNum++;
                }

                yield return null;

            }
            Debug.Log("done");
            EpisodeNumber.episodeNum++;
            string nextEpisodePath = $"Episodes/pilot_task_id_{EpisodeNumber.episodeNum}";
            TextAsset nextEpisodeFile = Resources.Load<TextAsset>(nextEpisodePath);
            Episode nextEpisode = JsonUtility.FromJson<Episode>(nextEpisodeFile.text);
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
        IEnumerator ProcessNetworkRequest()
        {
            // There is not always a character
            // StopCharacterAnimation(character);

            sceneCameras = ScriptUtils.FindAllCameras(transform);
            numSceneCameras = sceneCameras.Count;
            cameras = sceneCameras.ToList();
            cameras.AddRange(CameraExpander.AddRoomCameras(transform));
            CameraUtils.DeactivateCameras(cameras);
            OneTimeInitializer cameraInitializer = new OneTimeInitializer();
            OneTimeInitializer homeInitializer = new OneTimeInitializer();
            EnvironmentGraphCreator currentGraphCreator = null;
            EnvironmentGraph currentGraph = null;
            int expandSceneCount = 0;

            InitRooms();

            //add one character by default
            CharacterConfig configchar = new CharacterConfig();//JsonConvert.DeserializeObject<CharacterConfig>(networkRequest.stringParams[0]);
            CharacterControl newchar;
            newchar = AddCharacter(configchar.character_resource, false, configchar.mode, configchar.character_position, configchar.initial_room);


            characters.Add(newchar);
            CurrentStateList.Add(null);
            numCharacters++;
            List<Camera> charCameras = CameraExpander.AddCharacterCameras(newchar.gameObject, transform, "");
            CameraUtils.DeactivateCameras(charCameras);
            cameras.AddRange(charCameras);
            cameraInitializer.initialized = false;

            if (currentGraph == null)
            {
                //currentGraphCreator = new EnvironmentGraphCreator(dataProviders);
                currentGraph = currentGraphCreator.CreateGraph(transform);
            }
            else
            {
                currentGraph = currentGraphCreator.UpdateGraph(transform);
            }


            while (true)
            {
                Debug.Log("Waiting for request");

                yield return new WaitUntil(() => networkRequest != null);

                Debug.Log("Processing");

                NetworkResponse response = new NetworkResponse() { id = networkRequest.id };

                if (networkRequest.action == "camera_count")
                {
                    response.success = true;
                    response.value = cameras.Count;
                }
                else if (networkRequest.action == "camera_data")
                {
                    cameraInitializer.Initialize(() => CameraUtils.InitCameras(cameras));

                    IList<int> indexes = networkRequest.intParams;

                    if (!CheckCameraIndexes(indexes, cameras.Count))
                    {
                        response.success = false;
                        response.message = "Invalid parameters";
                    }
                    else
                    {
                        IList<CameraInfo> cameraData = CameraUtils.CreateCameraData(cameras, indexes);

                        response.success = true;
                        response.message = JsonConvert.SerializeObject(cameraData);
                    }
                }
                else if (networkRequest.action == "add_camera")
                {
                    CameraConfig camera_config = JsonConvert.DeserializeObject<CameraConfig>(networkRequest.stringParams[0]);
                    String camera_name = cameras.Count().ToString();
                    GameObject go = new GameObject("new_camera" + camera_name, typeof(Camera));
                    Camera new_camera = go.GetComponent<Camera>();
                    new_camera.renderingPath = RenderingPath.UsePlayerSettings;
                    Vector3 position_vec = camera_config.position;
                    Vector3 rotation_vec = camera_config.rotation;
                    go.transform.localPosition = position_vec;
                    go.transform.localEulerAngles = rotation_vec;

                    cameras.Add(new_camera);
                    response.message = "New camera created. Id:" + camera_name;
                    response.success = true; ;
                    cameraInitializer.initialized = false;
                    CameraUtils.DeactivateCameras(cameras);

                }
                else if (networkRequest.action == "camera_image")
                {

                    cameraInitializer.Initialize(() => CameraUtils.InitCameras(cameras));

                    IList<int> indexes = networkRequest.intParams;

                    if (!CheckCameraIndexes(indexes, cameras.Count))
                    {
                        response.success = false;
                        response.message = "Invalid parameters";
                    }
                    else
                    {
                        ImageConfig config = JsonConvert.DeserializeObject<ImageConfig>(networkRequest.stringParams[0]);
                        int cameraPass = ParseCameraPass(config.mode);
                        if (cameraPass == -1)
                        {
                            response.success = false;
                            response.message = "The current camera mode does not exist";
                        }
                        else
                        {

                            foreach (int i in indexes)
                            {
                                CameraExpander.AdjustCamera(cameras[i]);
                                cameras[i].gameObject.SetActive(true);
                            }
                            yield return new WaitForEndOfFrame();

                            List<string> imgs = new List<string>();

                            foreach (int i in indexes)
                            {
                                byte[] bytes = CameraUtils.RenderImage(cameras[i], config.image_width, config.image_height, cameraPass);
                                cameras[i].gameObject.SetActive(false);
                                imgs.Add(Convert.ToBase64String(bytes));
                            }
                            response.success = true;
                            response.message_list = imgs;
                        }

                    }
                }
                else if (networkRequest.action == "environment_graph")
                {
                    if (currentGraph == null)
                    {
                        currentGraphCreator = new EnvironmentGraphCreator(dataProviders);
                        var graph = currentGraphCreator.CreateGraph(transform);
                        response.success = true;
                        response.message = JsonConvert.SerializeObject(graph);
                        currentGraph = graph;
                    }
                    else
                    {
                        //s_GetGraphMarker.Begin();
                        using (s_GetGraphMarker.Auto())
                            currentGraph = currentGraphCreator.GetGraph();
                        //s_GetGraphMarker.End();
                        response.success = true;
                    }


                    using (s_GetMessageMarker.Auto())
                    {
                        response.message = JsonConvert.SerializeObject(currentGraph);
                    }
                }
                else if (networkRequest.action == "expand_scene")
                {
                    cameraInitializer.initialized = false;
                    List<IEnumerator> animationEnumerators = new List<IEnumerator>();

                    try
                    {
                        if (currentGraph == null)
                        {
                            currentGraphCreator = new EnvironmentGraphCreator(dataProviders);
                            currentGraph = currentGraphCreator.CreateGraph(transform);
                        }

                        ExpanderConfig config = JsonConvert.DeserializeObject<ExpanderConfig>(networkRequest.stringParams[0]);
                        Debug.Log("Successfully de-serialized object");
                        EnvironmentGraph graph = EnvironmentGraphCreator.FromJson(networkRequest.stringParams[1]);


                        if (config.randomize_execution)
                        {
                            InitRandom(config.random_seed);
                        }

                        // Maybe we do not need this
                        if (config.animate_character)
                        {
                            foreach (CharacterControl c in characters)
                            {
                                c.GetComponent<Animator>().speed = 1;
                            }
                        }

                        SceneExpander graphExpander = new SceneExpander(dataProviders)
                        {
                            Randomize = config.randomize_execution,
                            IgnoreObstacles = config.ignore_obstacles,
                            AnimateCharacter = config.animate_character,
                            TransferTransform = config.transfer_transform
                        };

                        if (networkRequest.stringParams.Count > 2 && !string.IsNullOrEmpty(networkRequest.stringParams[2]))
                        {
                            graphExpander.AssetsMap = JsonConvert.DeserializeObject<IDictionary<string, List<string>>>(networkRequest.stringParams[2]);
                        }
                        // TODO: set this with a flag
                        bool exact_expand = false;
                        graphExpander.ExpandScene(transform, graph, currentGraph, expandSceneCount, exact_expand);

                        SceneExpanderResult result = graphExpander.GetResult();

                        response.success = result.Success;
                        response.message = JsonConvert.SerializeObject(result.Messages);

                        // TODO: Do we need this?
                        //currentGraphCreator.SetGraph(graph);
                        currentGraph = currentGraphCreator.UpdateGraph(transform);
                        animationEnumerators.AddRange(result.enumerators);
                        expandSceneCount++;
                    }
                    catch (JsonException e)
                    {
                        response.success = false;
                        response.message = "Error deserializing params: " + e.Message;
                    }
                    catch (Exception e)
                    {
                        response.success = false;
                        response.message = "Error processing input graph: " + e.Message;
                        Debug.Log(e);
                    }

                    foreach (IEnumerator e in animationEnumerators)
                    {
                        yield return e;
                    }
                    foreach (CharacterControl c in characters)
                    {
                        c.GetComponent<Animator>().speed = 0;
                    }

                }
                else if (networkRequest.action == "point_cloud")
                {
                    if (currentGraph == null)
                    {
                        currentGraphCreator = new EnvironmentGraphCreator(dataProviders);
                        currentGraph = currentGraphCreator.CreateGraph(transform);
                    }
                    PointCloudExporter exporter = new PointCloudExporter(0.01f);
                    List<ObjectPointCloud> result = exporter.ExportObjects(currentGraph.nodes);
                    response.success = true;
                    response.message = JsonConvert.SerializeObject(result);
                }
                else if (networkRequest.action == "instance_colors")
                {
                    if (currentGraph == null)
                    {
                        EnvironmentGraphCreator graphCreator = new EnvironmentGraphCreator(dataProviders);
                        currentGraph = graphCreator.CreateGraph(transform);
                    }
                    response.success = true;
                    response.message = JsonConvert.SerializeObject(GetInstanceColoring(currentGraph.nodes));
                    //} else if (networkRequest.action == "start_recorder") {
                    //    RecorderConfig config = JsonConvert.DeserializeObject<RecorderConfig>(networkRequest.stringParams[0]);

                    //    string outDir = Path.Combine(config.output_folder, config.file_name_prefix);
                    //    Directory.CreateDirectory(outDir);

                    //    InitRecorder(config, outDir);

                    //    CharacterControl cc = character.GetComponent<CharacterControl>();
                    //    cc.rcdr = recorder;

                    //    if (recorder.saveSceneStates) {
                    //        List<GameObject> rooms = ScriptUtils.FindAllRooms(transform);
                    //        State_char sc = character.AddComponent<State_char>();
                    //        sc.Initialize(rooms, recorder.sceneStateSequence);
                    //        cc.stateChar = sc;
                    //    }

                    //    foreach (Camera camera in sceneCameras) {
                    //        camera.gameObject.SetActive(true);
                    //    }

                    //    recorder.Animator = cc.GetComponent<Animator>();
                    //    recorder.Animator.speed = 1;

                    //    CameraControl cameraControl = new CameraControl(sceneCameras, cc.transform, new Vector3(0, 1.0f, 0));
                    //    cameraControl.RandomizeCameras = config.randomize_recording;
                    //    cameraControl.CameraChangeEvent += recorder.UpdateCameraData;

                    //    recorder.CamCtrl = cameraControl;
                    //    recorder.MaxFrameNumber = 1000;
                    //    recorder.Recording = true;
                    //} else if (networkRequest.action == "stop_recorder") {
                    //    recorder.MarkTermination();
                    //    yield return WAIT_AFTER_END_OF_SCENE;
                    //    recorder.Recording = false;
                    //    recorder.Animator.speed = 0;
                    //    foreach (Camera camera in sceneCameras) {
                    //        camera.gameObject.SetActive(false);
                    //    }
                }
                else if (networkRequest.action == "add_character")
                {
                    CharacterConfig config = JsonConvert.DeserializeObject<CharacterConfig>(networkRequest.stringParams[0]);
                    //CharacterControl newchar;
                    newchar = AddCharacter(config.character_resource, false, config.mode, config.character_position, config.initial_room);

                    if (newchar != null)
                    {
                        characters.Add(newchar);
                        CurrentStateList.Add(null);
                        numCharacters++;
                        charCameras = CameraExpander.AddCharacterCameras(newchar.gameObject, transform, "");
                        CameraUtils.DeactivateCameras(charCameras);
                        cameras.AddRange(charCameras);
                        cameraInitializer.initialized = false;

                        if (currentGraph == null)
                        {
                            currentGraphCreator = new EnvironmentGraphCreator(dataProviders);
                            currentGraph = currentGraphCreator.CreateGraph(transform);
                        }
                        else
                        {
                            currentGraph = currentGraphCreator.UpdateGraph(transform);
                        }
                        // add camera
                        // cameras.AddRange(CameraExpander.AddCharacterCameras(newchar.gameObject, transform, ""));


                        response.success = true;
                    }
                    else
                    {
                        response.success = false;
                    }

                }

                else if (networkRequest.action == "move_character")
                {
                    CharacterConfig config = JsonConvert.DeserializeObject<CharacterConfig>(networkRequest.stringParams[0]);
                    Vector3 position = config.character_position;
                    int char_index = config.char_index;
                    Debug.Log($"move_char to : {position}");

                    List<GameObject> rooms = ScriptUtils.FindAllRooms(transform);
                    foreach (GameObject r in rooms)
                    {
                        if (r.GetComponent<Properties_room>() != null)
                            r.AddComponent<Properties_room>();
                    }

                    bool contained_somewhere = false;
                    for (int i = 0; i < rooms.Count; i++)
                    {
                        if (GameObjectUtils.GetRoomBounds(rooms[i]).Contains(position))
                        {
                            contained_somewhere = true;
                        }
                    }
                    if (!contained_somewhere)
                    {
                        response.success = false;
                    }
                    else
                    {
                        response.success = true;
                        var nma = characters[char_index].gameObject.GetComponent<NavMeshAgent>();
                        nma.Warp(position);
                    }

                }
                // TODO: remove character as well

                else if (networkRequest.action == "render_script")
                {
                    if (numCharacters == 0)
                    {
                        networkRequest = null;

                        response.message = "No character added yet!";
                        response.success = false;

                        commServer.UnlockProcessing(response);
                        continue;
                    }

                    ExecutionConfig config = JsonConvert.DeserializeObject<ExecutionConfig>(networkRequest.stringParams[0]);


                    if (config.randomize_execution)
                    {
                        InitRandom(config.random_seed);
                    }

                    if (currentGraph == null)
                    {
                        currentGraphCreator = new EnvironmentGraphCreator(dataProviders);
                        currentGraph = currentGraphCreator.CreateGraph(transform);
                    }

                    string outDir = Path.Combine(config.output_folder, config.file_name_prefix);
                    if (!config.skip_execution)
                    {
                        Directory.CreateDirectory(outDir);
                    }
                    IObjectSelectorProvider objectSelectorProvider;
                    if (config.find_solution)
                        objectSelectorProvider = new ObjectSelectionProvider(dataProviders.NameEquivalenceProvider);
                    else
                        objectSelectorProvider = new InstanceSelectorProvider(currentGraph);
                    IList<GameObject> objectList = ScriptUtils.FindAllObjects(transform);
                    // TODO: check if we need this
                    if (recorders.Count != numCharacters)
                    {
                        createRecorders(config);
                    }
                    else
                    {
                        updateRecorders(config);
                    }

                    if (!config.skip_execution)
                    {
                        for (int i = 0; i < numCharacters; i++)
                        {
                            if (config.skip_animation)
                                characters[i].SetSpeed(150.0f);
                            else
                                characters[i].SetSpeed(1.0f);
                        }
                    }
                    if (config.skip_animation)
                    {
                        UtilsAnnotator.SetSkipAnimation(transform);
                    }
                    // initialize the recorders
                    if (config.recording)
                    {
                        for (int i = 0; i < numCharacters; i++)
                        {
                            // Debug.Log($"cameraCtrl is not null? : {recorders[i].CamCtrl != null}");
                            recorders[i].Initialize();
                            recorders[i].Animator = characters[i].GetComponent<Animator>();

                            //recorders[i].Animator.speed = 1;
                        }

                    }

                    if (sExecutors.Count != numCharacters)
                    {
                        sExecutors = InitScriptExecutors(config, objectSelectorProvider, sceneCameras, objectList);
                    }

                    bool parseSuccess;
                    try
                    {
                        List<string> scriptLines = networkRequest.stringParams.ToList();
                        scriptLines.RemoveAt(0);

                        // not ok, has video
                        for (int i = 0; i < numCharacters; i++)
                        {
                            sExecutors[i].ClearScript();
                            sExecutors[i].smooth_walk = !config.skip_animation;
                        }

                        ScriptReader.ParseScript(sExecutors, scriptLines, dataProviders.ActionEquivalenceProvider);
                        parseSuccess = true;
                    }
                    catch (Exception e)
                    {
                        parseSuccess = false;
                        response.success = false;
                        response.message = $"Error parsing script: {e.Message}";
                    }

                    //s_SimulatePerfMarker.Begin();


                    if (parseSuccess)
                    {
                        for (int i = 0; i < numCharacters; i++)
                        {
                            StartCoroutine(sExecutors[i].ProcessAndExecute(config.recording, this));
                            //yield return sExecutors[i].ProcessAndExecute(true, this);
                        }

                        while (finishedChars != numCharacters)
                        {
                            yield return new WaitForSeconds(0.01f);
                        }
                    }


                    //s_SimulatePerfMarker.End();

                    finishedChars = 0;
                    ScriptExecutor.actionsPerLine = new Hashtable();
                    ScriptExecutor.currRunlineNo = 0;
                    ScriptExecutor.currActionsFinished = 0;

                    response.message = "";
                    response.success = false;
                    bool[] success_per_agent = new bool[numCharacters];

                    if (!config.recording)
                    {
                        for (int i = 0; i < numCharacters; i++)
                        {
                            if (!sExecutors[i].Success)
                            {
                                //response.success = false;
                                response.message += $"ScriptExcutor {i}: ";
                                response.message += sExecutors[i].CreateReportString();
                                response.message += "\n";
                                success_per_agent[i] = false;
                            }
                            else
                            {
                                response.success = true;
                                success_per_agent[i] = true;
                            }
                        }
                    }
                    else
                    {
                        for (int i = 0; i < numCharacters; i++)
                        {
                            Recorder rec = recorders[i];
                            if (!sExecutors[i].Success)
                            {
                                //response.success = false;
                                response.message += $"ScriptExcutor {i}: ";
                                response.message += sExecutors[i].CreateReportString();
                                response.message += "\n";
                            }
                            else if (rec.Error != null)
                            {
                                //Directory.Delete(rec.OutputDirectory);
                                //response.success = false;
                                response.message += $"Recorder {i}: ";
                                response.message += recorder.Error.Message;
                                response.message += "\n";
                                rec.Recording = false;
                            }
                            else
                            {
                                response.success = true;
                                success_per_agent[i] = true;
                                rec.MarkTermination();
                                rec.Recording = false;
                                rec.Animator.speed = 0;
                                CreateSceneInfoFile(rec.OutputDirectory, new SceneData()
                                {
                                    frameRate = config.frame_rate,
                                    sceneName = SceneManager.GetActiveScene().name
                                });
                                rec.CreateTextualGTs();
                            }
                        }
                    }


                    ISet<GameObject> changedObjs = new HashSet<GameObject>();
                    IDictionary<Tuple<string, int>, ScriptObjectData> script_object_changed = new Dictionary<Tuple<string, int>, ScriptObjectData>();
                    List<ActionObjectData> last_action = new List<ActionObjectData>();
                    bool single_action = true;

                    for (int char_index = 0; char_index < numCharacters; char_index++)
                    {
                        if (success_per_agent[char_index])
                        {
                            State currentState = this.CurrentStateList[char_index];
                            GameObject rh = currentState.GetGameObject("RIGHT_HAND_OBJECT");
                            GameObject lh = currentState.GetGameObject("LEFT_HAND_OBJECT");
                            EnvironmentObject obj1;
                            EnvironmentObject obj2;
                            currentGraphCreator.objectNodeMap.TryGetValue(characters[char_index].gameObject, out obj1);
                            Character character_graph;
                            currentGraphCreator.characters.TryGetValue(obj1, out character_graph);

                            if (sExecutors[char_index].script.Count > 1)
                            {
                                single_action = false;
                            }
                            if (sExecutors[char_index].script.Count == 1)
                            {
                                // If only one action was executed, we will use that action to update the environment
                                // Otherwise, we will update using coordinates
                                ScriptPair script = sExecutors[char_index].script[0];
                                ActionObjectData object_script = new ActionObjectData(character_graph, script, currentState.scriptObjects);
                                last_action.Add(object_script);

                            }
                            Debug.Assert(character_graph != null);
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

                            IDictionary<Tuple<string, int>, ScriptObjectData> script_objects_state = currentState.scriptObjects;
                            foreach (KeyValuePair<Tuple<string, int>, ScriptObjectData> entry in script_objects_state)
                            {
                                if (!entry.Value.GameObject.IsRoom())
                                {
                                    //if (entry.Key.Item1 == "cutleryknife")
                                    //{

                                    //    //int instance_id = entry.Value.GameObject.GetInstanceID();
                                    //}
                                    changedObjs.Add(entry.Value.GameObject);
                                }

                                if (entry.Value.OpenStatus != OpenStatus.UNKNOWN)
                                {
                                    //if (script_object_changed.ContainsKey(entry.Key))
                                    //{
                                    //    Debug.Log("Error, 2 agents trying to interact at the same time");
                                    if (sExecutors[char_index].script.Count > 0 && sExecutors[char_index].script[0].Action.Name.Instance == entry.Key.Item2)
                                    {
                                        script_object_changed[entry.Key] = entry.Value;
                                    }

                                    //}
                                    //else
                                    //{

                                    //}
                                }

                            }
                            foreach (KeyValuePair<Tuple<string, int>, ScriptObjectData> entry in script_object_changed)
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
                        }

                        using (s_UpdateGraph.Auto())
                        {
                            if (single_action)
                                currentGraph = currentGraphCreator.UpdateGraph(transform, null, last_action);
                            else
                                currentGraph = currentGraphCreator.UpdateGraph(transform, changedObjs);
                        }
                    }

                }
                else if (networkRequest.action == "reset")
                {
                    cameraInitializer.initialized = false;
                    networkRequest.action = "environment_graph"; // return result after scene reload
                    currentGraph = null;
                    currentGraphCreator = null;
                    CurrentStateList = new List<State>();
                    //cc = null;
                    numCharacters = 0;
                    characters = new List<CharacterControl>();
                    sExecutors = new List<ScriptExecutor>();
                    cameras = cameras.GetRange(0, numSceneCameras);

                    if (networkRequest.intParams?.Count > 0)
                    {
                        int sceneIndex = networkRequest.intParams[0];

                        if (sceneIndex >= 0 && sceneIndex < SceneManager.sceneCountInBuildSettings)
                        {

                            SceneManager.LoadScene(sceneIndex);

                            yield break;
                        }
                        else
                        {
                            response.success = false;
                            response.message = "Invalid scene index";
                        }
                    }
                    else
                    {

                        Debug.Log("Reloading");
                        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
                        DeleteChar();
                        Debug.Log("Reloaded");
                        yield break;
                    }
                }
                else if (networkRequest.action == "fast_reset")
                {
                    System.Diagnostics.Stopwatch resetStopwatch = System.Diagnostics.Stopwatch.StartNew();

                    // Reset without reloading scene
                    cameraInitializer.initialized = false;
                    networkRequest.action = "environment_graph"; // return result after scene reload
                    //currentGraph = null;
                    //currentGraphCreator = null;
                    var graph = currentGraph;
                    CurrentStateList = new List<State>();

                    foreach (GameObject go in currentGraphCreator.objectNodeMap.Keys)
                    {
                        if (go == null)
                        {
                            continue;
                        }
                        HandInteraction higo = go.GetComponent<HandInteraction>();
                        if (higo != null && higo.invisibleCpy != null)
                        {
                            bool added_rt = higo.added_runtime;
                            Destroy(higo);
                            if (!added_rt)
                            {
                                go.AddComponent<HandInteraction>();
                            }
                            // Do we need to specify that the invisible copy is not null?
                        }
                    }
                    // Remove characters and objects they may have
                    for (int i = 0; i < characters.Count; i++)
                    {
                        EnvironmentObject character_env_obj = currentGraphCreator.objectNodeMap[characters[i].gameObject];
                        if (currentGraphCreator.characters[character_env_obj].grabbed_left != null)
                        {
                            currentGraphCreator.RemoveObject(currentGraphCreator.characters[character_env_obj].grabbed_left);
                        }
                        if (currentGraphCreator.characters[character_env_obj].grabbed_right != null)
                        {
                            currentGraphCreator.RemoveObject(currentGraphCreator.characters[character_env_obj].grabbed_right);
                        }
                        currentGraphCreator.RemoveObject(character_env_obj);
                        Destroy(characters[i].gameObject);
                    }
                    currentGraphCreator.characters.Clear();

                    characters = new List<CharacterControl>();
                    numCharacters = 0;
                    sExecutors = new List<ScriptExecutor>();
                    cameras = cameras.GetRange(0, numSceneCameras);


                    //Start();
                    response.success = true;
                    response.message = "";

                    resetStopwatch.Stop();
                    Debug.Log(String.Format("fast reset time: {0}", resetStopwatch.ElapsedMilliseconds));

                }
                else if (networkRequest.action == "idle")
                {
                    response.success = true;
                    response.message = "";
                }
                else
                {
                    response.success = false;
                    response.message = "Unknown action " + networkRequest.action;
                }

                // Ready for next request
                networkRequest = null;

                commServer.UnlockProcessing(response);
            }
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
                    }
                }
                else if (mode == "fix_position")
                {
                    var nma = newCharacter.GetComponent<NavMeshAgent>();
                    nma.Warp(position);
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
                    newCharacter.transform.rotation *= Quaternion.Euler(0, UnityEngine.Random.Range(-180.0f, 180.0f), 0);
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

        private List<(string, float)> allGraphs = new List<(string, float)>();

        private List<(Vector3, Vector3, float)> posAndRotation = new List<(Vector3, Vector3, float)>();

        private List<(string, float)> scriptActions = new List<(string, float)>();
        private Dictionary<string, List<string>> goalRelations = new Dictionary<string, List<string>>();
        private HashSet<string> allPlacedObjects = new HashSet<string>();

        public Vector3 previousPos = new Vector3(0, 0, 0);
        public Vector3 previousRotation = new Vector3(0, 0, 0);

        public void ClearDataFile(int episode)
        {
            string outputPath = $"Assets/Resources/Episodes/Episode{episode}Data.txt";
            using (FileStream fs = File.Create(outputPath)) { }
            foreach (EnvironmentRelation r in init_graph.edges)
            {
                r.relation = (ObjectRelation)Enum.Parse(typeof(ObjectRelation), r.relation_type);
            }
            foreach (EnvironmentObject o in init_graph.nodes)
            {
                foreach (string s in o.states)
                {
                    o.states_set.Add((Utilities.ObjectState)Enum.Parse(typeof(Utilities.ObjectState), s));
                }
            }
            Debug.Log("yo");
        }

        public void RecordData(int episode)
        {
            string outputPath = $"Assets/Resources/Episodes/Episode{episode}Data.txt";
            StreamWriter outputFile = new StreamWriter(outputPath, true);
            outputFile.WriteLine("Position and Orientation Data:");
            foreach ((Vector3, Vector3, float) pos in posAndRotation)
            {
                outputFile.WriteLine(pos.ToString());
            }
            outputFile.WriteLine("Script Action Data:");
            foreach ((string, float) action in scriptActions)
            {
                outputFile.WriteLine(action);
            }
            outputFile.WriteLine("Graph Data:");
            foreach ((string, float) g in allGraphs)
            {
                outputFile.WriteLine(g.ToString());
            }
            outputFile.Close();
        }

        public void AddCharInfo(Vector3 coord, Vector3 rot, float t)
        {
            posAndRotation.Add((coord, rot, t));
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
                response += $"{verb} {obj1} on {obj2} x{t.repetitions}\n";
                Goal newG = new Goal(verb, obj1, obj2, t.repetitions);
                goals.Add(newG);
                allPlacedObjects.Add(obj1);
                if (!goalRelations.ContainsKey(obj2))
                {
                    goalRelations.Add(obj2, new List<string>());
                }
                for (int i = 0; i < t.repetitions; i++)
                {
                    goalRelations[obj2].Add(obj1);
                }
            }
            return response;
        }

        public void GoalMet(string v, string o1, string o2)
        {
            int reps = 0;
            foreach (Goal g in goals)
            {
                if (g.repetitions >= 1)
                {
                    if (g.verb == v && g.obj1 == o1 && g.obj2 == o2)
                    {
                        g.repetitions--;
                    }
                    reps += g.repetitions;
                }
            }
            if (reps == 0)
            {
                IsCompleted = true;
            }
        }

        public void GoalMetSingleObj(string v, string o)
        {
            int reps = 0;
            foreach (Goal g in goals)
            {
                if (g.repetitions >= 1)
                {
                    if (g.verb == v && g.obj1 == o)
                    {
                        g.repetitions--;
                    }
                    reps += g.repetitions;
                }
            }
            if (reps == 0)
            {
                IsCompleted = true;
            }
        }

        public bool IsOn(EnvironmentObject o1, EnvironmentObject o2)
        {
            if (o2.bounding_box.bounds.center.y > o1.bounding_box.bounds.min.y)
                return false;

            Rect o1XZRect = BoundsUtils.XZRect(o1.bounding_box.bounds);
            Rect o2XZRect = BoundsUtils.XZRect(o2.bounding_box.bounds);

            // Sufficient area intersection
            return RectUtils.IntersectionArea(o1XZRect, o2XZRect) > o1XZRect.Area() * 0.5f;
        }

        public List<string> OnTop(List<string> objs, string dest, EnvironmentGraph g)
        {
            List<string> allObjsNeeded = new List<string>(objs);
            EnvironmentObject platform = null;
            foreach (EnvironmentObject o in g.nodes) { if (o.class_name.Equals(dest)) { platform = o; break; } }
            foreach (EnvironmentObject env_obj in g.nodes)
            {
                if (objs.Contains(env_obj.class_name))
                {
                    if (IsOn(env_obj, platform))
                    {
                        allObjsNeeded.Remove(env_obj.class_name);
                    }
                }
            }
            return allObjsNeeded;
        }

        public void checkTasks(EnvironmentGraph g)
        {
            Dictionary<string, List<string>> goalsCopy = new Dictionary<string, List<string>>(goalRelations);
            foreach (string dest in goalsCopy.Keys.ToList())
            {
                goalsCopy[dest] = OnTop(goalsCopy[dest], dest, g);
            }
            foreach (Goal goal in goals)
            {
                goal.repetitions = goalsCopy[goal.obj2].Where(x => x.Equals(goal.obj1)).Count();
            }
        }

        public string UpdateTasksString()
        {
            string response = "Tasks: \n";
            foreach (Goal g in goals)
            {
                if (g.repetitions == 0)
                {
                    string s = $"{g.verb} {g.obj1} on {g.obj2} x{g.repetitions}\n";
                    response += $"{s.AddColor(Color.green)}";
                }
                else
                {
                    response += $"{g.verb} {g.obj1} on {g.obj2} x{g.repetitions}\n";
                }
            }
            return response;
        }

        public void StoreGraph(EnvironmentGraph g, float t)
        {
            string graphString = JsonUtility.ToJson(g);
            allGraphs.Add((graphString, t));
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
        public string obj1 { get; set; }
        public string obj2 { get; set; }
        public int repetitions { get; set; }

        public Goal(string v, string o1, string o2, int reps)
        {
            verb = v;
            obj1 = o1;
            obj2 = o2;
            repetitions = reps;
        }
    }

}
