using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
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
using RootMotion.FinalIK;
using DunGen;


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


        // Prefab placement
        [SerializeField] GameObject[] prefab;
        public GameObject _instance;
        public Transform houseTransform;

        // Environment memory
        public GameObject object1;
        public GameObject object2;

        // Physics
        public float maxDepenetrationVelocity = 0.1f;

        // Set Time
        public float time = 0;


        WaitForSeconds WAIT_AFTER_END_OF_SCENE = new WaitForSeconds(3.0f);

        [Serializable]
        class SceneData
        {
            //public string characterName;
            public string sceneName;
            public int frameRate;
        }

        void ProcessHomeandCameras()
        {
            ProcessHome(false);
            InitRooms();
            sceneCameras = ScriptUtils.FindAllCameras(houseTransform);
            numSceneCameras = sceneCameras.Count;
            cameras = sceneCameras.ToList();
            cameras.AddRange(CameraExpander.AddRoomCameras(houseTransform));
            CameraUtils.DeactivateCameras(cameras);

        }
        void Start()
        {
            recorder = GetComponent<Recorder>();

            // Initialize data from files to static variable to speed-up the execution process
            if (dataProviders == null) {
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

            if (transform.gameObject.name.Contains("Home_Procedural_Generation") || !transform.gameObject.name.Contains("Home"))
            {
                if (commServer == null)
                {
                    InitServer();
                }
                commServer.Driver = this;


                Screen.sleepTimeout = SleepTimeout.NeverSleep;

                if (networkRequest == null)
                {
                    commServer.UnlockProcessing(); // Allow to proceed with requests
                }

                // LightingSetup();
                StartCoroutine(ProcessNetworkRequest());
            }
            
            

            

        }
        
        private void InitServer()
        {
            string[] args = Environment.GetCommandLineArgs();
            var argDict = DriverUtils.GetParameterValues(args);
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
            UtilsAnnotator.ProcessHome(houseTransform, randomizeExecution);

            ColorEncoding.EncodeCurrentScene(houseTransform);
            // Disable must come after color encoding. Otherwise, GetComponent<Renderer> failes to get
            // Renderer for the disabled objects.
            UtilsAnnotator.PostColorEncoding_DisableGameObjects();
        }

        public void ProcessRequest(string request)
        {
            Debug.Log(string.Format("Processing request {0}", request));

            NetworkRequest newRequest = null;
            try {
                newRequest = JsonConvert.DeserializeObject<NetworkRequest>(request);
            } catch (JsonException) {
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

        IEnumerator ProcessNetworkRequest()
        {
            // There is not always a character
            OneTimeInitializer cameraInitializer = new OneTimeInitializer();
            OneTimeInitializer homeInitializer = new OneTimeInitializer();
            EnvironmentGraphCreator currentGraphCreator = null;
            EnvironmentGraph currentGraph = null;
            int expandSceneCount = 0;

            
            CameraExpander.ResetCharacterCameras();

            while (true) {
                Debug.Log("Waiting for request");

                yield return new WaitUntil(() => networkRequest != null);

                Debug.Log("Processing");

                NetworkResponse response = new NetworkResponse() { id = networkRequest.id };

                if (networkRequest.action == "camera_count"){
                    response.success = true;
                    response.value = cameras.Count;
                } 
                
                else if (networkRequest.action == "character_cameras")
                {
                    response.success = true;
                    response.message = JsonConvert.SerializeObject(CameraExpander.GetCamNames());
                } 
                
                else if (networkRequest.action == "camera_data") 
                {
                    cameraInitializer.Initialize(() => CameraUtils.InitCameras(cameras));

                    IList<int> indexes = networkRequest.intParams;

                    if (!CheckCameraIndexes(indexes, cameras.Count)) {
                        response.success = false;
                        response.message = "Invalid parameters";
                    } else {
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

                    // new_camera.usePhysicalProperties = true;
                    float field_view = camera_config.field_view;
                    new_camera.fieldOfView = field_view;

                    new_camera.renderingPath = RenderingPath.UsePlayerSettings;

                    Vector3 position_vec = camera_config.position;
                    Vector3 rotation_vec = camera_config.rotation;

                    go.transform.localPosition = position_vec;
                    go.transform.localEulerAngles = rotation_vec;

                    cameras.Add(new_camera);
                    response.message = "New camera created. Id:" + camera_name;
                    response.success = true;;
                    cameraInitializer.initialized = false;
                    CameraUtils.DeactivateCameras(cameras);
                }

                else if (networkRequest.action == "update_camera")
                {
                    CameraConfig camera_config = JsonConvert.DeserializeObject<CameraConfig>(networkRequest.stringParams[0]);
                    IList<int> indexes = networkRequest.intParams;
                    int index = indexes[0];
                    if (index >= cameras.Count())
                    {
                        response.message = "The camera index is not valid, there are only "+cameras.Count().ToString()+" cameras";
                    }
                    else
                    {
                        Camera camera = cameras[index];
                        float field_view = camera_config.field_view;
                        camera.fieldOfView = field_view;
                        camera.renderingPath = RenderingPath.UsePlayerSettings;

                        Vector3 position_vec = camera_config.position;
                        Vector3 rotation_vec = camera_config.rotation;
                        GameObject go = camera.gameObject;
                        go.transform.localPosition = position_vec;
                        go.transform.localEulerAngles = rotation_vec;

                        response.message = "Camera updated";
                        response.success = true;

                    }

                }

                else if (networkRequest.action == "add_character_camera")
                {
                    CameraConfig camera_config = JsonConvert.DeserializeObject<CameraConfig>(networkRequest.stringParams[0]);
                    String camera_name = camera_config.camera_name;
                    Quaternion rotation = new Quaternion();
                    rotation.eulerAngles = camera_config.rotation;

                    bool cam_added = CameraExpander.AddNewCamera(camera_name, camera_config.position, rotation);
                    if (characters.Count > 0)
                    {
                        response.success = false;
                        response.message = "Error: you should add these cameras before defining the characters";
                    }
                    else
                    {
                        if (cam_added)
                        {
                            response.success = true;
                            response.message = "New camera defined with name: " + camera_name;
                        }
                        else
                        {
                            response.success = false;
                            response.message = "Error: a camera with this name already exists";
                        }
                    }
                }

                else if (networkRequest.action == "update_character_camera")
                {
                    CameraConfig camera_config = JsonConvert.DeserializeObject<CameraConfig>(networkRequest.stringParams[0]);
                    String camera_name = camera_config.camera_name;
                    Quaternion rotation = new Quaternion();
                    rotation.eulerAngles = camera_config.rotation;

                    CharacterCamera char_cam;
                    bool camera_found = CameraExpander.char_cams.TryGetValue(camera_name, out char_cam);
                    if (camera_found)
                    {
                        char_cam.localPosition = camera_config.position;
                        char_cam.localRotation = rotation;
                        response.success = true;
                        response.message = "Camera successfully updated.";
                              
                    }
                    else
                    {
                        response.success = false;
                        response.message = "Error: camera does not exist.";
                    }
                    
                }

                else if (networkRequest.action == "camera_image") 
                {
                    
                    cameraInitializer.Initialize(() => CameraUtils.InitCameras(cameras));

                    IList<int> indexes = networkRequest.intParams;

                    if (!CheckCameraIndexes(indexes, cameras.Count)) {
                        response.success = false;
                        response.message = "Invalid parameters";
                    } else {
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
                        var graph = currentGraphCreator.CreateGraph(houseTransform);
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

                    Dictionary<GameObject, int> char_ind = new Dictionary<GameObject, int>();
                    Dictionary<GameObject, List<Tuple<GameObject, ObjectRelation>>> grabbed_objs = new Dictionary<GameObject, List<Tuple<GameObject, ObjectRelation>>>();
                    try {
                        if (currentGraph == null) {
                            currentGraphCreator = new EnvironmentGraphCreator(dataProviders);
                            currentGraph = currentGraphCreator.CreateGraph(houseTransform);
                        }

                        ExpanderConfig config = JsonConvert.DeserializeObject<ExpanderConfig>(networkRequest.stringParams[0]);
                        Debug.Log("Successfully de-serialized object");
                        EnvironmentGraph graph = EnvironmentGraphCreator.FromJson(networkRequest.stringParams[1]);


                        if (config.randomize_execution) {
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

                        SceneExpander graphExpander = new SceneExpander(dataProviders) {
                            Randomize = config.randomize_execution,
                            IgnoreObstacles = config.ignore_obstacles,
                            AnimateCharacter = config.animate_character,
                            TransferTransform = config.transfer_transform
                        };

                        if (networkRequest.stringParams.Count > 2 && !string.IsNullOrEmpty(networkRequest.stringParams[2])) {
                            graphExpander.AssetsMap = JsonConvert.DeserializeObject<IDictionary<string, List<string>>>(networkRequest.stringParams[2]);
                        }
                        // TODO: set this with a flag
                        bool exact_expand = config.exact_expand;



                        // This should go somewhere else...
                        List<GameObject> added_chars = new List<GameObject>();

                        graphExpander.ExpandScene(houseTransform, graph, currentGraph, expandSceneCount, added_chars, grabbed_objs, exact_expand);
                        int chid = 0;
                        foreach(GameObject added_char in added_chars)
                        {
                            Debug.Assert(!currentGraphCreator.IsInGraph(added_char));

                            CharacterControl cc = added_char.GetComponent<CharacterControl>();
                            added_char.SetActive(true);
                            var nma = added_char.GetComponent<NavMeshAgent>();
                            nma.Warp(added_char.transform.position);

                            characters.Add(cc);
                            
                            cc.SetSpeed(150.0f);

                            CurrentStateList.Add(null);
                            numCharacters++;
                            List<Camera> charCameras = CameraExpander.AddCharacterCameras(added_char.gameObject, "");
                            CameraUtils.DeactivateCameras(charCameras);
                            cameras.AddRange(charCameras);
                            cameraInitializer.initialized = false;

                            EnvironmentObject char_obj = currentGraphCreator.AddChar(added_char.transform);
                            char_ind[added_char] = chid;
                            chid += 1;

                        }
                        SceneExpanderResult result = graphExpander.GetResult();

                        response.success = result.Success;
                        response.message = JsonConvert.SerializeObject(result.Messages);
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
                    yield return null;
                    currentGraph = currentGraphCreator.UpdateGraph(houseTransform);

                    // Update grabbed stuff. This is a hack because it is difficult to get
                    // from the transforms the grabbing relationships. Could be much improved
                    foreach (GameObject character_grabbing in grabbed_objs.Keys)
                    {
                        EnvironmentObject char_obj = currentGraphCreator.objectNodeMap[character_grabbing];
                        Character char_char = currentGraphCreator.characters[char_obj];
                        State new_state = new State(character_grabbing.transform.position);

                        foreach (Tuple<GameObject, ObjectRelation> grabbed_obj in grabbed_objs[character_grabbing])
                        {
                            EnvironmentObject obj_grabbed = currentGraphCreator.objectNodeMap[grabbed_obj.Item1];
                            GameObject obj_grabbedgo = grabbed_obj.Item1;
                            HandInteraction hi;
                            if (obj_grabbedgo.GetComponent<HandInteraction>() == null)
                            {
                                hi = obj_grabbedgo.AddComponent<HandInteraction>();
                                hi.added_runtime = true;
                                hi.allowPickUp = true;
                                hi.grabHandPose = ScriptExecutor.GetGrabPose(obj_grabbedgo).Value; // HandInteraction.HandPose.GrabVertical;
                                hi.Initialize();
                                    
                            }
                            else
                            {
                                hi = obj_grabbedgo.GetComponent<HandInteraction>();
                            }
                            if (grabbed_obj.Item2 == ObjectRelation.HOLDS_LH)
                            {
                                char_char.grabbed_left = obj_grabbed;
                                new_state.AddScriptGameObject(obj_grabbed.class_name, obj_grabbed.id, obj_grabbedgo, Vector3.one, Vector3.one, true);
                                new_state.AddGameObject("LEFT_HAND_OBJECT", grabbed_obj.Item1);

                                new_state.AddObject("INTERACTION_HAND", FullBodyBipedEffector.LeftHand);
                                // hi.Get_IO_grab(character_grabbing.transform, FullBodyBipedEffector.LeftHand);

                            }
                            if (grabbed_obj.Item2 == ObjectRelation.HOLDS_RH)
                            {

                                char_char.grabbed_right = obj_grabbed;
                                new_state.AddScriptGameObject(obj_grabbed.class_name, obj_grabbed.id, obj_grabbedgo, Vector3.one, Vector3.one, true);
                                new_state.AddGameObject("RIGHT_HAND_OBJECT", grabbed_obj.Item1);
                                new_state.AddObject("INTERACTION_HAND", FullBodyBipedEffector.RightHand);
                                // hi.Get_IO_grab(character_grabbing.transform, FullBodyBipedEffector.RightHand);

                            }

                            yield return characters[char_ind[character_grabbing]].GrabObject(obj_grabbedgo, (FullBodyBipedEffector)new_state.GetObject("INTERACTION_HAND"));
                            EnvironmentObject roomobj2 = currentGraphCreator.FindRoomLocation(obj_grabbed);
                            currentGraphCreator.RemoveGraphEdgesWithObject(obj_grabbed);
                            currentGraphCreator.AddGraphEdge(char_obj, obj_grabbed, grabbed_obj.Item2);
                            currentGraphCreator.AddGraphEdge(obj_grabbed, roomobj2, ObjectRelation.INSIDE);


                        }

                            
                        CurrentStateList[char_ind[character_grabbing]] = new_state;
                    }

                        //animationEnumerators.AddRange(result.enumerators);
                        expandSceneCount++;
                    

                    foreach (IEnumerator e in animationEnumerators) {
                        yield return e;
                    }
                    foreach (CharacterControl c in characters)
                    {
                        c.GetComponent<Animator>().speed = 0;
                    }    
                    
                } 
                
                else if (networkRequest.action == "point_cloud") 
                {
                    if (currentGraph == null) {
                        currentGraphCreator = new EnvironmentGraphCreator(dataProviders);
                        currentGraph = currentGraphCreator.CreateGraph(houseTransform);
                    }
                    PointCloudExporter exporter = new PointCloudExporter(0.01f);
                    List<ObjectPointCloud> result = exporter.ExportObjects(currentGraph.nodes);
                    response.success = true;
                    response.message = JsonConvert.SerializeObject(result);
                } 
                
                else if (networkRequest.action == "instance_colors") 
                {
                    if (currentGraph == null) {
                        EnvironmentGraphCreator graphCreator = new EnvironmentGraphCreator(dataProviders);
                        currentGraph = graphCreator.CreateGraph(houseTransform);
                    }
                    response.success = true;
                    response.message = JsonConvert.SerializeObject(GetInstanceColoring(currentGraph.nodes));
                }

                else if (networkRequest.action == "add_character")
                {
                    CharacterConfig config = JsonConvert.DeserializeObject<CharacterConfig>(networkRequest.stringParams[0]);
                    CharacterControl newchar;
                    newchar = AddCharacter(config.character_resource, false, config.mode, config.character_position, config.initial_room);

                    if (newchar != null)
                    {
                        characters.Add(newchar);
                        CurrentStateList.Add(null);
                        numCharacters++;
                        List<Camera> charCameras = CameraExpander.AddCharacterCameras(newchar.gameObject, "");
                        CameraUtils.DeactivateCameras(charCameras);
                        cameras.AddRange(charCameras);
                        cameraInitializer.initialized = false;

                        if (currentGraph == null)
                        {
                            currentGraphCreator = new EnvironmentGraphCreator(dataProviders);
                            currentGraph = currentGraphCreator.CreateGraph(houseTransform);
                        }
                        else
                        {
                            currentGraph = currentGraphCreator.UpdateGraph(houseTransform);
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


                    List<GameObject> rooms = ScriptUtils.FindAllRooms(houseTransform);
                    foreach (GameObject r in rooms)
                    {
                        if (r.GetComponent<Properties_room>() == null)
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


                    if (config.randomize_execution) {
                        InitRandom(config.random_seed);
                    }

                    if (currentGraph == null)
                    {
                        currentGraphCreator = new EnvironmentGraphCreator(dataProviders);
                        currentGraph = currentGraphCreator.CreateGraph(houseTransform);
                    }

                    string outDir = Path.Combine(config.output_folder, config.file_name_prefix);
                    if (!config.skip_execution) {
                        Directory.CreateDirectory(outDir);
                    }
                    IObjectSelectorProvider objectSelectorProvider;
                    if (config.find_solution)
                        objectSelectorProvider = new ObjectSelectionProvider(dataProviders.NameEquivalenceProvider);
                    else
                        objectSelectorProvider = new InstanceSelectorProvider(currentGraph);
                    IList<GameObject> objectList = ScriptUtils.FindAllObjects(houseTransform);
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
                        UtilsAnnotator.SetSkipAnimation(houseTransform);
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
                        //continue;
                    }

                    //s_SimulatePerfMarker.Begin();


                    if (parseSuccess)
                    {
                        List<Tuple<int, Tuple<String, String>>> error_messages = new List<Tuple<int, Tuple<String, String>>>();
                        if (!config.find_solution)
                            error_messages = ScriptChecker.SolveConflicts(sExecutors);
                        for (int i = 0; i < numCharacters; i++)
                        {
                            StartCoroutine(sExecutors[i].ProcessAndExecute(config.recording, this));
                        }

                        while (finishedChars != numCharacters)
                        {
                            yield return new WaitForSeconds(0.01f);
                        }

                        // Add back errors from concurrent actions

                        for (int error_index = 0; error_index < error_messages.Count; error_index++)
                        {
                            sExecutors[error_messages[error_index].Item1].report.AddItem(error_messages[error_index].Item2.Item1, error_messages[error_index].Item2.Item2);
                        }

                    
                        //s_SimulatePerfMarker.End();

                        finishedChars = 0;
                        ScriptExecutor.actionsPerLine = new Hashtable();
                        ScriptExecutor.currRunlineNo = 0;
                        ScriptExecutor.currActionsFinished = 0;

                        response.message = "";
                        response.success = false;
                        bool[] success_per_agent = new bool[numCharacters];

                        bool agent_failed_action = false;
                        Dictionary<int, Dictionary<String, String>> messages = new Dictionary<int, Dictionary<String, String>>();


                        if (!config.recording)
                        {
                            for (int i = 0; i < numCharacters; i++)
                            {
                                Dictionary<String, String> current_message = new Dictionary<String, String>();

                                if (!sExecutors[i].Success)
                                {
                                    String message = "";
                                    message += $"ScriptExcutor {i}: ";
                                    message += sExecutors[i].CreateReportString();
                                    message += "\n";
                                    current_message["message"] = message;

                                    success_per_agent[i] = false;
                                    agent_failed_action = true;
                                }
                                else
                                {
                                    current_message["message"] = "Success";
                                    response.success = true;
                                    success_per_agent[i] = true;
                                }
                                messages[i] = current_message;
                            }
                        }
                        else
                        {
                            for (int i = 0; i < numCharacters; i++)
                            {
                                Dictionary<String, String> current_message = new Dictionary<String, String>();
                                Recorder rec = recorders[i];
                                if (!sExecutors[i].Success)
                                {
                                    //response.success = false;
                                    String message = "";
                                    message += $"ScriptExcutor {i}: ";
                                    message += sExecutors[i].CreateReportString();
                                    message += "\n";
                                    current_message["message"] = message;
                                }
                                else if (rec.Error != null)
                                {
                                    //Directory.Delete(rec.OutputDirectory);
                                    //response.success = false;
                                    agent_failed_action = true;
                                    String message = "";
                                    message += $"Recorder {i}: ";
                                    message += recorder.Error.Message;
                                    message += "\n";
                                    rec.Recording = false;
                                    current_message["message"] = message;
                                }
                                else
                                {
                                    current_message["message"] = "Success";
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


                                messages[i] = current_message;
                            }
                        }
                        response.message = JsonConvert.SerializeObject(messages);

                        // If any of the agent fails an action, report failure

                        if (agent_failed_action)
                            response.success = false;
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

                                    }

                                }
                                foreach (KeyValuePair<Tuple<string, int>, ScriptObjectData> entry in script_object_changed)
                                {
                                    if (entry.Value.OpenStatus == OpenStatus.OPEN)
                                    {
                                        currentGraphCreator.objectNodeMap[entry.Value.GameObject].states.Remove(Utilities.ObjectState.CLOSED);
                                        currentGraphCreator.objectNodeMap[entry.Value.GameObject].states.Add(Utilities.ObjectState.OPEN);
                                    }
                                    else if (entry.Value.OpenStatus == OpenStatus.CLOSED)
                                    {
                                        currentGraphCreator.objectNodeMap[entry.Value.GameObject].states.Remove(Utilities.ObjectState.OPEN);
                                        currentGraphCreator.objectNodeMap[entry.Value.GameObject].states.Add(Utilities.ObjectState.CLOSED);
                                    }
                                }
                            }

                            using (s_UpdateGraph.Auto())
                            {
                                if (single_action)
                                    currentGraph = currentGraphCreator.UpdateGraph(houseTransform, null, last_action);
                                else
                                    currentGraph = currentGraphCreator.UpdateGraph(houseTransform, changedObjs);
                            }
                        }
                    }
                } 

                else if (networkRequest.action == "observation")
                {
                    if (currentGraph == null)
                    {
                        response.success = false;
                        response.message = "Envrionment graph is not yet initialized";
                    }
                    else
                    {
                        IList<int> indices = networkRequest.intParams;
                        if (!CheckCameraIndexes(indices, cameras.Count))
                        {
                            response.success = false;
                            response.message = "Invalid parameters";
                        }
                        else
                        {
                            int index = indices[0];
                            Camera cam = cameras[index];
                            CameraExpander.AdjustCamera(cam);
                            cameras[index].gameObject.SetActive(true);
                            cam.enabled = true;
                            yield return new WaitForEndOfFrame();

                            Plane[] frustum = GeometryUtility.CalculateFrustumPlanes(cam);

                            Dictionary<int, String> visibleObjs = new Dictionary<int, String>();
                            foreach (EnvironmentObject node in currentGraph.nodes)
                            {
                                if (EnvironmentGraphCreator.IGNORE_IN_EDGE_OBJECTS.Contains(node.class_name))
                                {
                                    continue;
                                }

                                // check if the object bounding box is in the camera frustum
                                if (GeometryUtility.TestPlanesAABB(frustum, node.bounding_box.bounds))
                                {
                                    // check is camera can actually see the object by raycasting
                                    if (SeenByCamera(cam, node.transform))
                                    {
                                        visibleObjs.Add(node.id, node.class_name);
                                    }
                                }
                            }
                            cam.enabled = false;
                            Debug.Log(visibleObjs);

                            response.success = true;
                            response.message = JsonConvert.SerializeObject(visibleObjs);

                        }
                    }
                }

                else if (networkRequest.action == "environment") 
                {   
                    cameraInitializer.initialized = false;
                    currentGraph = null;
                    currentGraphCreator = null;
                    CurrentStateList = new List<State>();
                    numCharacters = 0;
                    characters = new List<CharacterControl>();
                    sExecutors = new List<ScriptExecutor>();
                    CameraExpander.ResetCharacterCameras();

                    if (networkRequest.intParams?.Count > 0)
                    {
                        int environment = networkRequest.intParams[0];

                        PreviousEnvironment.IndexMemory = environment;

                        if (environment >= 0 && environment < 50)
                        {   
                            GameObject _instance = Instantiate(prefab[environment], new Vector3(0, 0, 0), Quaternion.Euler(0f, 0f, 0f)) as GameObject;
                            houseTransform = _instance.transform;
                            response.success = true;
                            response.message = "";
                            ProcessHomeandCameras();
                        }
                    }
                    else if (PreviousEnvironment.IndexMemory == -1)
                    {
                        SceneManager.LoadScene(1);

                        response.success = true;
                        response.message = "";
                    }
                    else
                    {
                        GameObject _instance = Instantiate(prefab[PreviousEnvironment.IndexMemory], new Vector3(0, 0, 0), Quaternion.Euler(0f, 0f, 0f)) as GameObject;
                        houseTransform = _instance.transform;
                        response.success = true;
                        response.message = "";
                        ProcessHomeandCameras();
                    }
     
                    NavMeshSurface nm = GameObject.FindObjectOfType<NavMeshSurface>();
                    nm.BuildNavMesh();

                    // environment_graph
                    currentGraphCreator = new EnvironmentGraphCreator(dataProviders);
                    var graph = currentGraphCreator.CreateGraph(houseTransform);
                    currentGraph = graph;
                    response.message = "";
                    response.success = true;

                }

                else if (networkRequest.action == "clear")
                {
                    networkRequest.action = "";
                    if (networkRequest.intParams?.Count > 0)
                    {
                        SceneManager.LoadScene(0);
                        yield break;
                    }
                    else if (PreviousEnvironment.IndexMemory == -1)
                    {
                        // do nothing
                    }
                    else
                    {
                        SceneManager.LoadScene(0);
                        yield break;
                    }

                    response.success = true;
                   
                }

                else if (networkRequest.action == "procedural_generation") 
                {   
                    GameObject ProceduralGenerationObject = GameObject.FindWithTag("Procedural");
                    var runtimeDungeon = ProceduralGenerationObject.GetComponent<RuntimeDungeon>();
                    var generator = runtimeDungeon.Generator;

                    if (networkRequest.intParams?.Count > 0)
                    {
                        int seed = networkRequest.intParams[0];

                        generator.ShouldRandomizeSeed = false;
                        generator.Seed = seed;
                        generator.Generate();
                        PreviousEnvironment.IndexMemory = -1;
                        response.message = "";
                        response.success = true;
                    }
                    else
                    {
                        generator.ShouldRandomizeSeed = true;
                        generator.Generate();
                        PreviousEnvironment.IndexMemory = -1;
                        response.message = "";
                        response.success = true;
                    }

                    cameraInitializer.initialized = false;
                    currentGraph = null;
                    currentGraphCreator = null;
                    CurrentStateList = new List<State>();
     
                    NavMeshSurface nm = GameObject.FindObjectOfType<NavMeshSurface>();
                    nm.BuildNavMesh();

                    houseTransform = GameObject.Find("Dungeon").transform;

                    yield return null;
                    ProcessHomeandCameras();

                    numCharacters = 0;
                    characters = new List<CharacterControl>();
                    sExecutors = new List<ScriptExecutor>();
                    CameraExpander.ResetCharacterCameras();

                    yield return null;

                    currentGraphCreator = new EnvironmentGraphCreator(dataProviders);
                    var graph = currentGraphCreator.CreateGraph(houseTransform);
                    currentGraph = graph;

                    response.message = "";
                    response.success = true;

                }

                else if (networkRequest.action == "clear_procedural") 
                {   
                    SceneManager.LoadScene(1);
                    response.message = "";
                    response.success = true;
                }

                else if (networkRequest.action == "remove_terrain") 
                {   
                    GameObject Terrain = GameObject.FindWithTag("Terrain");
                    Destroy(Terrain);

                    response.message = "";
                    response.success = true;

                }

                else if (networkRequest.action == "process") 
                {   
                    cameraInitializer.initialized = false;
                    currentGraph = null;
                    currentGraphCreator = null;
                    CurrentStateList = new List<State>();
     
                    NavMeshSurface nm = GameObject.FindObjectOfType<NavMeshSurface>();
                    nm.BuildNavMesh();

                    houseTransform = GameObject.Find("Dungeon").transform;
                    ProcessHomeandCameras();

                    numCharacters = 0;
                    characters = new List<CharacterControl>();
                    sExecutors = new List<ScriptExecutor>();
                    CameraExpander.ResetCharacterCameras();

                    yield return null;

                    currentGraphCreator = new EnvironmentGraphCreator(dataProviders);
                    var graph = currentGraphCreator.CreateGraph(houseTransform);
                    currentGraph = graph;

                    response.message = "";
                    response.success = true;
                }

                else if (networkRequest.action == "activate_physics") 
                {   
                    PhysicsConfig config = JsonConvert.DeserializeObject<PhysicsConfig>(networkRequest.stringParams[0]);

                    float gravity_force = config.gravity;

                    GameObject gravityObject = GameObject.FindWithTag("Time");
                    Gravity gravity = gravityObject.GetComponent<Gravity>();

                    gravity.ActivateGravity();
            
                    Physics.gravity = new Vector3(0, gravity_force, 0);   

                    response.success = true;
                }

                else if (networkRequest.action == "set_time") 
                {   
                    TimeConfig config = JsonConvert.DeserializeObject<TimeConfig>(networkRequest.stringParams[0]);

                    int hours = config.hours;
                    int minutes = config.minutes;
                    int seconds = config.seconds;

                    hours = hours * 3600;
                    minutes = minutes * 60;
                    seconds = hours + minutes + seconds;

                    GameObject timeObject = GameObject.FindWithTag("Time");
                    LightingManager currentLightingManager = timeObject.GetComponent<LightingManager>();

                    currentLightingManager.SetTime(seconds);
        
                    response.success = true;
                }

                else if (networkRequest.action == "fast_reset")
                {
                    cameraInitializer.initialized = false;
                    currentGraph = null;
                    currentGraphCreator = null;
                    CurrentStateList = new List<State>();
                    numCharacters = 0;
                    characters = new List<CharacterControl>();
                    sExecutors = new List<ScriptExecutor>();
                    CameraExpander.ResetCharacterCameras();
        
                    GameObject _preload = GameObject.FindWithTag("Home");
                    Destroy(_preload);

                    if (networkRequest.intParams?.Count > 0)
                    {
                        int environment = networkRequest.intParams[0];

                        PreviousEnvironment.IndexMemory = environment;

                        if (environment >= 0 && environment < 50)
                        {   
                            GameObject _instance = Instantiate(prefab[environment], new Vector3(0, 0, 0), Quaternion.Euler(0f, 0f, 0f)) as GameObject;
                            houseTransform = _instance.transform;
                            response.success = true;
                            response.message = "";
                            ProcessHomeandCameras();
                        }

                        NavMeshSurface nm = GameObject.FindObjectOfType<NavMeshSurface>();
                        nm.BuildNavMesh();

                        // environment_graph
                        currentGraphCreator = new EnvironmentGraphCreator(dataProviders);
                        var graph = currentGraphCreator.CreateGraph(houseTransform);
                        currentGraph = graph;
                        response.message = "";
                        response.success = true;
                    }
                    else
                    {
                        response.success = false;
                        response.message = "";
                    }

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
            List<Camera> charCameras = CameraExpander.AddCharacterCameras(character, CameraExpander.INT_FORWARD_VIEW_CAMERA_NAME);
            CameraUtils.DeactivateCameras(charCameras);
            Camera camera = charCameras.First(c => c.name == CameraExpander.FORWARD_VIEW_CAMERA_NAME);
            return new FrontCameraControl(camera);
        }

        private FixedCameraControl CreateFixedCameraControl(GameObject character, string cameraName, bool focusToObject)
        {
            List<Camera> charCameras = CameraExpander.AddCharacterCameras(character, cameraName);
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
                        bool cam_ctrl = false;
                        if (int.TryParse(config.camera_mode[cam_id], out index_cam))
                        {
                            //Camera cam = new Camera();
                            //cam.CopyFrom(sceneCameras[index_cam]);
                            Camera cam = cameras[index_cam];
                            cameraControl = new FixedCameraControl(cam);
                            cam_ctrl = true;
                        }
                        else
                        {
                            if (config.camera_mode[cam_id] == "PERSON_FRONT")
                            {
                                cameraControl = CreateFrontCameraControls(chc.gameObject);
                                cam_ctrl = true;
                            }
                            else
                            {
                                if (config.camera_mode[cam_id] == "AUTO")
                                {
                                    AutoCameraControl autoCameraControl = new AutoCameraControl(sceneCameras, chc.transform, new Vector3(0, 1.0f, 0));
                                    autoCameraControl.RandomizeCameras = config.randomize_execution;
                                    autoCameraControl.CameraChangeEvent += rec.UpdateCameraData;
                                    cameraControl = autoCameraControl;
                                    cam_ctrl = true;
                                }
                                else
                                {

                                    if (CameraExpander.HasCam(config.camera_mode[cam_id]))
                                    {
                                        cameraControl = CreateFixedCameraControl(chc.gameObject, CameraExpander.char_cams[config.camera_mode[cam_id]].name, false);
                                        cam_ctrl = true;
                                    }
                                }
                            }


                        }
                        if (cam_ctrl)
                        {
                            cameraControl.Activate(true);
                            rec.CamCtrls[cam_id] = cameraControl;
                        }

                    }
                }
            }
            //Debug.Log($"config.recording : {config.recording}");
            //Debug.Log($"cameraCtrl is not null2? : {cameraControl != null}");
            rec.Recording = config.recording;
            rec.currentframeNum = 0;
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
                ScriptExecutor sExecutor = new ScriptExecutor(objectList, dataProviders.RoomSelector, objectSel, recorders[i], i, interaction_cache, !config.skip_animation);
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

            foreach (SkinnedMeshRenderer mr in mrCompoments) {
                mr.updateWhenOffscreen = true;
                mr.enabled = false;
                mr.enabled = true;
            }
        }

        private void InitRooms()
        {
            List<GameObject> rooms = ScriptUtils.FindAllRooms(houseTransform);

            foreach (GameObject r in rooms) {
                r.AddComponent<Properties_room>();
            }
        }

        private void InitRandom(int seed)
        {
            if (seed >= 0) {
                UnityEngine.Random.InitState(seed);
            } else {
                UnityEngine.Random.InitState((int)DateTimeOffset.Now.ToUnixTimeMilliseconds());
            }
        }

        private Dictionary<int, float[]> GetInstanceColoring(IList<EnvironmentObject> graphNodes)
        {
            Dictionary<int, float[]> result = new Dictionary<int, float[]>();

            foreach (EnvironmentObject eo in graphNodes) {
                if (eo.transform == null) {
                    result[eo.id] = new float[] { 1.0f, 1.0f, 1.0f };
                } else {
                    int instId = eo.transform.gameObject.GetInstanceID();
                    Color instColor;

                    if (ColorEncoding.instanceColor.TryGetValue(instId, out instColor)) {
                        result[eo.id] = new float[] { instColor.r, instColor.g, instColor.b };
                    } else {
                        result[eo.id] = new float[] { 1.0f, 1.0f, 1.0f };
                    }
                }
            }
            return result;
        }

        private void StopCharacterAnimation(GameObject character)
        {
            Animator animator = character.GetComponent<Animator>();
            if (animator != null) {
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
            using (StreamWriter sw = new StreamWriter(Path.Combine(outDir, "sceneInfo.json"))) {
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
            List<GameObject> sceneCharacters = ScriptUtils.FindAllCharacters(houseTransform);

            if (loadedObj == null)
            {
                if (sceneCharacters.Count > 0) return sceneCharacters[0].GetComponent<CharacterControl>();
                else return null;
            }
            else
            {
                Transform destRoom;


                List<GameObject> rooms = ScriptUtils.FindAllRooms(houseTransform);
                foreach (GameObject r in rooms)
                {
                    if (r.GetComponent<Properties_room>() == null)
                        r.AddComponent<Properties_room>();
                }


                if (sceneCharacters.Count > 0 && mode == "random") 
                {
                    destRoom = sceneCharacters[0].transform.parent.transform;
                }
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
                        if (rooms[i].name.Contains(room_name.Substring(1))) {
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
                    nma.Warp(ScriptUtils.FindRandomCCPosition(rooms_selected, cc));
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

        public InstanceSelectorProvider(EnvironmentGraph currentGraph)
        {
            idObjectMap = new Dictionary<int, GameObject>(); 

            foreach (EnvironmentObject eo in currentGraph.nodes) {
                if (eo.transform != null) {
                    idObjectMap[eo.id] = eo.transform.gameObject;
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
            if (!initialized) {
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
        public float field_view = 40.0f;
        public string camera_name = "default";

    }

    public class ExpanderConfig
    {
        public bool randomize_execution = false;
        public int random_seed = -1;
        public bool ignore_obstacles = false;
        public bool animate_character = false;
        public bool transfer_transform = true;
        public bool exact_expand = true;
    }

    public class PreviousEnvironment 
    {
        public static int IndexMemory = 0;
    }

    public class PhysicsConfig
    {
        public float gravity = 1.0f;
    }

    public class TimeConfig
    {
        public int hours = 0;
        public int minutes = 0;
        public int seconds = 0;
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
    }

    public class DataProviders
    {
        public NameEquivalenceProvider NameEquivalenceProvider { get; private set; }
        public ActionEquivalenceProvider ActionEquivalenceProvider { get; private set; }
        public AssetsProvider AssetsProvider { get; private set; }
        public ObjectSelectionProvider ObjectSelectorProvider { get; private set; }
        public RoomSelector RoomSelector { get; private set; }
        public ObjectPropertiesProvider ObjectPropertiesProvider { get; private set; }
        public Dictionary<string, string> AssetPathMap;

        public DataProviders()
        {
            Initialize();
        }

        private void Initialize()
        {
            NameEquivalenceProvider = new NameEquivalenceProvider("Data/class_name_equivalence");
            ActionEquivalenceProvider = new ActionEquivalenceProvider("Data/action_mapping");
            AssetsProvider = new AssetsProvider("Data/object_prefabs");
            AssetPathMap = BuildPathMap("Data/object_prefabs");
            ObjectSelectorProvider = new ObjectSelectionProvider(NameEquivalenceProvider);
            RoomSelector = new RoomSelector(NameEquivalenceProvider);
            ObjectPropertiesProvider = new ObjectPropertiesProvider(NameEquivalenceProvider);
        }

        private Dictionary<string, string> BuildPathMap(string resourceName)
        {
            Dictionary<string, string> result = new Dictionary<string, string> ();
            List<string> all_prefabs = new List<string>();
            TextAsset txtAsset = Resources.Load<TextAsset>(resourceName);
            var tmpAssetsMap = JsonConvert.DeserializeObject<Dictionary<string, List<string>>>(txtAsset.text);

            foreach (var e in tmpAssetsMap)
            {
                foreach (var str_prefab in e.Value)
                {
                    string prefab_name = Path.GetFileNameWithoutExtension(str_prefab);
                    result[prefab_name] = str_prefab;
                }

            }
            return result;
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

            foreach (string s in args) {
                Match match = regex.Match(s);
                if (match.Success) {
                    result[match.Groups[1].Value.Trim()] = match.Groups[2].Value.Trim();
                }
            }
            return result;
        }
    }

}
