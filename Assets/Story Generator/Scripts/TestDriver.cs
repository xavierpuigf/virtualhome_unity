using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
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

namespace StoryGenerator
{
    [RequireComponent(typeof(Recorder))]
    public class TestDriver : MonoBehaviour
    {
        private const int DefaultPort = 8080;
        private const int DefaultTimeout = 30000;

        //static ProcessingController processingController;
        static HttpCommunicationServer commServer;
        static NetworkRequest networkRequest = null;

        static DataProviders dataProviders;

        Recorder recorder;

        WaitForSeconds WAIT_AFTER_END_OF_SCENE = new WaitForSeconds(3.0f);

        [Serializable]
        class SceneData
        {
            public string characterName;
            public string sceneName;
            public int frameRate;
        }

        void Start()
        {
            recorder = GetComponent<Recorder>();

            // Initialize data from files to static variable to speed-up the execution process
            if (dataProviders == null) {
                dataProviders = new DataProviders();
            }

            if (commServer == null) {
                InitServer();
            }
            commServer.Driver = this;

            ProcessHome(false);

            Screen.sleepTimeout = SleepTimeout.NeverSleep;

            if (networkRequest == null) {
                commServer.UnlockProcessing(); // Allow to proceed with requests
            }
            StartCoroutine(ProcessNetworkRequest());
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

        void ProcessHome(bool randomizeExecution)
        {
            HomeAnnotator.ProcessHome(transform, randomizeExecution);
            ColorEncoding.EncodeCurrentScene(transform);
            // Disable must come after color encoding. Otherwise, GetComponent<Renderer> failes to get
            // Renderer for the disabled objects.
            HomeAnnotator.PostColorEncoding_DisableGameObjects();
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
            GameObject character = ScriptUtils.FindAllCharacters(transform)[0];
            List<Camera> sceneCameras = ScriptUtils.FindAllCameras(transform);
            List<Camera> cameras = sceneCameras.ToList();
            cameras.AddRange(CameraExpander.AddRoomCameras(transform));
            cameras.AddRange(CameraExpander.AddCharacterCameras(character, transform));
            CameraUtils.DeactivateCameras(cameras);
            OneTimeInitializer cameraInitializer = new OneTimeInitializer();
            OneTimeInitializer homeInitializer = new OneTimeInitializer();
            EnvironmentGraph currentGraph = null;
            int expandSceneCount = 0;
            StopCharacterAnimation(character);
            InitRooms();

            while (true) {
                Debug.Log("Waiting for request");

                yield return new WaitUntil(() => networkRequest != null);

                Debug.Log("Processing");

                NetworkResponse response = new NetworkResponse() { id = networkRequest.id };

                if (networkRequest.action == "camera_count") {
                    response.success = true;
                    response.value = cameras.Count;
                } else if (networkRequest.action == "camera_data") {
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
                } else if (networkRequest.action == "camera_image") {
                    cameraInitializer.Initialize(() => CameraUtils.InitCameras(cameras));

                    IList<int> indexes = networkRequest.intParams;

                    if (!CheckCameraIndexes(indexes, cameras.Count)) {
                        response.success = false;
                        response.message = "Invalid parameters";
                    } else {
                        InitCharacterForCamera(character);
                        ImageConfig config = JsonConvert.DeserializeObject<ImageConfig>(networkRequest.stringParams[0]);
                        int cameraPass = ParseCameraPass(config.mode);

                        foreach (int i in indexes) {
                            CameraExpander.AdjustCamera(cameras[i]);
                            cameras[i].gameObject.SetActive(true);
                        }
                        yield return new WaitForEndOfFrame();

                        List<string> imgs = new List<string>();

                        foreach (int i in indexes) {
                            byte[] bytes = CameraUtils.RenderImage(cameras[i], config.image_width, config.image_height, cameraPass);
                            cameras[i].gameObject.SetActive(false);
                            imgs.Add(Convert.ToBase64String(bytes));
                        }
                        response.success = true;
                        response.message_list = imgs;
                    }
                } else if (networkRequest.action == "environment_graph") {
                    var graphCreator = new EnvironmentGraphCreator(dataProviders);
                    var graph = graphCreator.CreateGraph(transform);
                    response.success = true;
                    response.message = JsonConvert.SerializeObject(graph);
                    currentGraph = graph;
                } else if (networkRequest.action == "expand_scene") {
                    List<IEnumerator> animationEnumerators = new List<IEnumerator>();
                    Animator animator = character.GetComponent<Animator>();

                    try {
                        if (currentGraph == null) {
                            EnvironmentGraphCreator graphCreator = new EnvironmentGraphCreator(dataProviders);
                            currentGraph = graphCreator.CreateGraph(transform);
                        }

                        ExpanderConfig config = JsonConvert.DeserializeObject<ExpanderConfig>(networkRequest.stringParams[0]);
                        EnvironmentGraph graph = EnvironmentGraphCreator.FromJson(networkRequest.stringParams[1]);
                        Debug.Log("Successfully de-serialized object");

                        if (config.randomize_execution) {
                            InitRandom(config.random_seed);
                        }
                        if (config.animate_character) {
                            animator.speed = 1;
                        }

                        SceneExpander graphExpander = new SceneExpander(dataProviders) {
                            Randomize = config.randomize_execution,
                            IgnoreObstacles = config.ignore_obstacles,
                            AnimateCharacter = config.animate_character
                        };

                        if (networkRequest.stringParams.Count > 2 && !string.IsNullOrEmpty(networkRequest.stringParams[2])) {
                            graphExpander.AssetsMap = JsonConvert.DeserializeObject<IDictionary<string, List<string>>>(networkRequest.stringParams[2]);
                        }

                        graphExpander.ExpandScene(transform, graph, currentGraph, expandSceneCount);

                        SceneExpanderResult result = graphExpander.GetResult();

                        response.success = result.Success;
                        response.message = JsonConvert.SerializeObject(result.Messages);
                        currentGraph = graph;
                        animationEnumerators.AddRange(result.enumerators);
                        expandSceneCount++;
                    } catch (JsonException e) {
                        response.success = false;
                        response.message = "Error deserializing params: " + e.Message;
                    } catch (Exception e) {
                        response.success = false;
                        response.message = "Error processing input graph: " + e.Message;
                        Debug.Log(e);
                    }

                    foreach (IEnumerator e in animationEnumerators) {
                        yield return e;
                    }
                    animator.speed = 0;
                } else if (networkRequest.action == "point_cloud") {
                    if (currentGraph == null) {
                        EnvironmentGraphCreator graphCreator = new EnvironmentGraphCreator(dataProviders);
                        currentGraph = graphCreator.CreateGraph(transform);
                    }
                    PointCloudExporter exporter = new PointCloudExporter(0.01f);
                    List<ObjectPointCloud> result = exporter.ExportObjects(currentGraph.nodes);
                    response.success = true;
                    response.message = JsonConvert.SerializeObject(result);
                } else if (networkRequest.action == "instance_colors") {
                    if (currentGraph == null) {
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
                } else if (networkRequest.action == "render_script") {
                    ExecutionConfig config = JsonConvert.DeserializeObject<ExecutionConfig>(networkRequest.stringParams[0]);

                    if (config.randomize_execution) {
                        InitRandom(config.random_seed);
                    }

                    string outDir = Path.Combine(config.output_folder, config.file_name_prefix);
                    if (!config.skip_execution) {
                        Directory.CreateDirectory(outDir);
                    }

                    InitRecorder(config, outDir);

                    CharacterControl cc = AddCharacter(config.character_resource, config.randomize_execution);
                    cc.rcdr = recorder;
                    cc.Randomize = config.randomize_execution;

                    recorder.Animator = cc.GetComponent<Animator>();
                    recorder.Animator.speed = 1;

                    ICameraControl cameraControl;

                    switch (config.camera_mode) {
                        case "FIRST_PERSON":
                            cameraControl = CreateFixedCameraControl(cc.gameObject, CameraExpander.FORWARD_VIEW_CAMERA_NAME, true);
                            break;
                        case "PERSON_TOP":
                            cameraControl = CreateFixedCameraControl(cc.gameObject, CameraExpander.TOP_CHARACTER_CAMERA_NAME, false);
                            break;
                        case "PERSON_FRONT":
                            cameraControl = CreateFrontCameraControls(cc.gameObject);
                            break;
                        default:
                            AutoCameraControl autoCameraControl = new AutoCameraControl(sceneCameras, cc.transform, new Vector3(0, 1.0f, 0));
                            autoCameraControl.RandomizeCameras = config.randomize_execution;
                            autoCameraControl.CameraChangeEvent += recorder.UpdateCameraData;
                            cameraControl = autoCameraControl;
                            break;
                    }


                    recorder.CamCtrl = cameraControl;

                    IObjectSelectorProvider objectSelectorProvider;

                    if (config.find_solution) {
                        objectSelectorProvider = dataProviders.ObjectSelectorProvider;
                    } else {
                        if (currentGraph == null) {
                            EnvironmentGraphCreator graphCreator = new EnvironmentGraphCreator(dataProviders);
                            currentGraph = graphCreator.CreateGraph(transform);
                        }
                        objectSelectorProvider = new InstanceSelectorProvider(currentGraph); 
                    }

                    IList<GameObject> objectList = ScriptUtils.FindAllObjects(transform);
                    ScriptExecutor sExecutor = new ScriptExecutor(objectList, dataProviders.RoomSelector, objectSelectorProvider, recorder);
                    sExecutor.RandomizeExecution = config.randomize_execution;
                    sExecutor.ProcessingTimeLimit = config.processing_time_limit;
                    sExecutor.SkipExecution = config.skip_execution;
                    sExecutor.AutoDoorOpening = false;
                    sExecutor.Initialize(cc, cameraControl);

                    cc.DoorControl.Update(objectList);

                    recorder.Recording = false;

                    bool parseSuccess;

                    try {
                        List<string> scriptLines = networkRequest.stringParams.ToList();
                        scriptLines.RemoveAt(0);
                        ScriptReader.ParseScript(sExecutor, scriptLines, dataProviders.ActionEquivalenceProvider);
                        parseSuccess = true;
                    } catch (Exception e) {
                        parseSuccess = false;
                        response.success = false;
                        response.message = $"Error parsing script: {e.Message}";
                    }

                    if (parseSuccess) {
                        yield return TimeOutEnumerator(sExecutor.ProcessAndExecute(true));

                        if (config.skip_execution) {
                            if (sExecutor.Success) {
                                response.success = true;
                            } else {
                                response.success = false;
                                response.message = sExecutor.CreateReportString();
                            }
                        } else {
                            if (!recorder.Recording) {
                                response.success = false;
                                response.message = sExecutor.CreateReportString();
                            } else if (recorder.Error != null) {
                                Directory.Delete(outDir, true);
                                response.success = false;
                                response.message = recorder.Error.Message;
                                recorder.Recording = false;
                            } else {
                                // Marking termination adds NULL action corresponds to the intentional delay (WAIT_AFTER_END_OF_SCENE)
                                recorder.MarkTermination();
                                yield return WAIT_AFTER_END_OF_SCENE;
                                recorder.Recording = false;
                                recorder.Animator.speed = 0;
                                CreateSceneInfoFile(outDir, new SceneData() {
                                    characterName = config.character_resource,
                                    frameRate = config.frame_rate,
                                    sceneName = SceneManager.GetActiveScene().name
                                });
                                recorder.CreateTextualGTs();
                                response.success = true;
                            }
                        }
                    }
                } else if (networkRequest.action == "reset") {
                    networkRequest.action = "idle"; // return result after scene reload
                    if (networkRequest.intParams?.Count > 0) {
                        int sceneIndex = networkRequest.intParams[0];

                        if (sceneIndex >= 0 && sceneIndex < SceneManager.sceneCountInBuildSettings) {
                            SceneManager.LoadScene(sceneIndex);
                            yield break;
                        } else {
                            response.success = false;
                            response.message = "Invalid scene index";
                        }
                    } else {
                        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
                        yield break;
                    }
                } else if (networkRequest.action == "idle") {
                    response.success = true;
                    response.message = "";
                } else {
                    response.success = false;
                    response.message = "Unknown action " + networkRequest.action;
                }
                
                // Ready for next request
                networkRequest = null;

                commServer.UnlockProcessing(response);
            }
        }

        private FrontCameraControl CreateFrontCameraControls(GameObject character)
        {
            List<Camera> charCameras = CameraExpander.AddCharacterCameras(character, transform);
            CameraUtils.DeactivateCameras(charCameras);
            Camera camera = charCameras.First(c => c.name == CameraExpander.FORWARD_VIEW_CAMERA_NAME);
            return new FrontCameraControl(camera);
        }

        private FixedCameraControl CreateFixedCameraControl(GameObject character, string cameraName, bool focusToObject)
        {
            List<Camera> charCameras = CameraExpander.AddCharacterCameras(character, transform);
            CameraUtils.DeactivateCameras(charCameras);
            Camera camera = charCameras.First(c => c.name == cameraName);
            return new FixedCameraControl(camera) { DoFocusObject = focusToObject };
        }

        private void InitRecorder(RecorderConfig config, string outDir)
        {
            recorder.FrameRate = config.frame_rate;
            recorder.imageSynthesis = config.image_synthesis;
            recorder.captureScreenshot = config.capture_screenshot;
            recorder.savePoseData = config.save_pose_data;
            recorder.saveSceneStates = config.save_scene_states;
            recorder.FileName = config.file_name_prefix;
            recorder.ImageWidth = config.image_width;
            recorder.ImageHeight = config.image_height;
            recorder.OutputDirectory = outDir;
            recorder.Initialize();
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
            List<GameObject> rooms = ScriptUtils.FindAllRooms(transform);

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
        private CharacterControl AddCharacter(string path, bool randomizeExecution)
        {
            GameObject loadedObj = Resources.Load(ScriptUtils.TransformResourceFileName(path)) as GameObject;
            List<GameObject> sceneCharacters = ScriptUtils.FindAllCharacters(transform);

            if (loadedObj == null) {
                if (sceneCharacters.Count > 0) return sceneCharacters[0].GetComponent<CharacterControl>();
                else return null;
            } else {
                List<GameObject> rooms = ScriptUtils.FindAllRooms(transform);
                foreach (GameObject r in rooms)
                {
                    r.AddComponent<Properties_room> ();
                }
                Transform destRoom;

                if (sceneCharacters.Count > 0) destRoom = sceneCharacters[0].transform.parent.transform;
                else destRoom = rooms[0].transform;

                GameObject newCharacter = Instantiate(loadedObj, destRoom) as GameObject;
                newCharacter.name = loadedObj.name;
                ColorEncoding.EncodeGameObject(newCharacter);
                CharacterControl cc = newCharacter.GetComponent<CharacterControl>();

                sceneCharacters.ForEach(go => go.SetActive(false));
                newCharacter.SetActive(true);

                if (sceneCharacters.Count == 0 || randomizeExecution) {
                    //newCharacter.transform.position = ScriptUtils.FindRandomCCPosition(rooms, cc);
                    var nma = newCharacter.GetComponent<NavMeshAgent>();
                    nma.Warp(ScriptUtils.FindRandomCCPosition(rooms, cc));
                    newCharacter.transform.rotation *= Quaternion.Euler(0, UnityEngine.Random.Range(-180.0f, 180.0f), 0);
                } else {
                    //newCharacter.transform.position = sceneCharacters[0].transform.position;
                    var nma = newCharacter.GetComponent<NavMeshAgent>();
                    nma.Warp(sceneCharacters[0].transform.position);
                }

                // Must be called after correct char placement so that char's location doesn't change
                // after instantiation.
                if (recorder.saveSceneStates)
                {
                    State_char sc = newCharacter.AddComponent<State_char> ();
                    sc.Initialize(rooms, recorder.sceneStateSequence);
                    cc.stateChar = sc;
                }

                return cc;
            }
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
        private bool initialized = false;
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
        public bool image_synthesis = false;
        public bool capture_screenshot = false;
        public bool save_pose_data = false;
        public bool save_scene_states = false;
        public string character_resource = "Chars/Male1";
        public bool randomize_recording = false;
        public int image_width = 640;
        public int image_height = 480;
    }

    public class ImageConfig : RecorderConfig
    {
        public int image_width = 640;
        public int image_height = 480;
        public string mode = "normal";
    }

    public class ExpanderConfig
    {
        public bool randomize_execution = false;
        public int random_seed = -1;
        public bool ignore_obstacles = false;
        public bool animate_character = false;
    }

    public class ExecutionConfig : RecorderConfig
    {
        public bool find_solution = true;
        public bool randomize_execution = false;
        public int random_seed = -1;
        public int processing_time_limit = 10;
        public bool skip_execution = false;
        public string camera_mode = "AUTO";
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
            NameEquivalenceProvider = new NameEquivalenceProvider("Assets/Resources/Data/class_name_equivalence.json");
            ActionEquivalenceProvider = new ActionEquivalenceProvider("Assets/Resources/Data/action_mapping.txt");
            AssetsProvider = new AssetsProvider("Assets/Resources/Data/object_prefabs.json");
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
