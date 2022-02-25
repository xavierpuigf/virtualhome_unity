using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using StoryGenerator.ChairProperties;
using StoryGenerator.CharInteraction;
using StoryGenerator.DoorProperties;
using StoryGenerator.Scripts;
using Supercluster.KDTree;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;


namespace StoryGenerator.Utilities
{

    public enum ObjectRelation
    {
        ON,
        INSIDE,
        UNDER,
        BETWEEN,
        CLOSE,
        FACING,
        HOLDS_RH,
        HOLDS_LH,
        SITTING
    }

    public enum ObjectState
    {
        CLOSED,
        OPEN,
        ON,
        OFF,
        SITTING,
        LYING,
        CLEAN,
        DIRTY,
        PLUGGED_IN,
        PLUGGED_OUT,
    }

    //public enum ObjectProperty
    //{
    //    OPENABLE,
    //    SITTABLE,
    //}

    public class ObjectBounds
    {
        private Bounds unityBounds;

        protected ObjectBounds()
        {
        }

        public ObjectBounds(ObjectBounds ob)
        {
            bounds = ob.bounds;
        }

        public ObjectBounds(Bounds b)
        {
            bounds = b;
        }

        public float[] center { get; set; }
        public float[] size { get; set; }

        [JsonIgnore]
        public Bounds bounds
        {
            get { return unityBounds; }
            set {
                unityBounds = value;
                center = ToFloatArray(unityBounds.center);
                size = ToFloatArray(unityBounds.size);
            }
        }

        public static ObjectBounds FromGameObject(GameObject gameObject)
        {

            Bounds bounds = GameObjectUtils.GetBounds(gameObject);
            return bounds.size == Vector3.zero ? null : new ObjectBounds(bounds);
        }

        public void UnionWith(ObjectBounds ob)
        {
            unityBounds.Encapsulate(ob.bounds);
            center = ToFloatArray(unityBounds.center);
            size = ToFloatArray(unityBounds.size);
        }

        public static float[] ToFloatArray(Vector3 v)
        {
            return new float[] { v.x, v.y, v.z };
        }

    }
    public class Character
    {
        public EnvironmentObject character;
        public EnvironmentObject grabbed_left;
        public EnvironmentObject grabbed_right;

        public Character(EnvironmentObject character)
        {
            this.character = character;
        }
    }
    public class ObjectTransform
    {
        public float[] position;
        public float[] rotation;
        public float[] scale;

        public ObjectTransform(Transform t)
        {
            if (t != null)
            {

                this.rotation = new float[4] { t.rotation.x, t.rotation.y, t.rotation.z, t.rotation.w };
                this.position = new float[3] { t.position.x, t.position.y, t.position.z };
                this.scale = new float[3] { t.localScale.x, t.localScale.y, t.localScale.z };
            }
        }
        public Quaternion GetRotation()
        {
            return new Quaternion(rotation[0], rotation[1], rotation[2], rotation[3]);
        }

        public Vector3 GetPosition()
        {
            return new Vector3(position[0], position[1], position[2]);
        }
        public Vector3? GetScale()
        {
            if (scale == null)
                return null;
            return new Vector3(scale[0], scale[1], scale[2]);
        }
    }

    // Represents scene objects; nodes of the environment graph
    public class EnvironmentObject : IEquatable<EnvironmentObject>
    {
        public int id { get; set; }  // Unique id

        [JsonIgnore]
        private Transform ts;

        [JsonIgnore]
        public Transform transform
        {
            get { return ts; }
            set {
                ts = value;
                this.obj_transform = new ObjectTransform(ts);
            }
        }

        public String category { get; set; }  // Category of this object (Character, Room, ...)

        public string class_name { get; set; }  // Object class name (className from PrefabClass.json, "unknown" in not present)

        public string prefab_name { get; set; }  // GameObject name

        public ObjectTransform obj_transform { get; set; }

        public ObjectBounds bounding_box { get; set; }  // Axis aligned bounding box

        public ICollection<string> properties { get; set; } = new List<string>();  // List of properties ("SITTABLE", ...), from PropertiesData.json

        [JsonProperty(ItemConverterType = typeof(StringEnumConverter))]
        public ISet<ObjectState> states { get; set; } = new HashSet<ObjectState>();  // List of states (CLOSED, OPEN, ON, OFF, ...) 

        public bool Equals(EnvironmentObject other)
        {
            return id == other.id;
        }

        public override int GetHashCode()
        {
            return id;
        }

        public override bool Equals(object obj)
        {
            if (obj == null) return false;
            EnvironmentObject objAsEO = obj as EnvironmentObject;
            if (objAsEO == null) return false;
            else return Equals(objAsEO);
        }

        public EnvironmentObject Copy()
        {
            return new EnvironmentObject() {
                id = id,
                ts = ts,
                category = category,
                class_name = class_name,
                prefab_name = prefab_name,
                bounding_box = bounding_box,
                properties = new List<string>(properties),
                states = new HashSet<ObjectState>(states)
            };
        }
    }

    // Represents edges of the environment graph
    // Eg, for Milk (from) in (relation) Fridge (to)
    public class EnvironmentRelation
    {
        public int from_id { get; set; }  // First node (object id)

        public int to_id { get; set; }  // Second node (object id)

        [JsonConverter(typeof(StringEnumConverter))]
        public ObjectRelation relation_type { get; set; }  // Relation type

    }

    public class EnvironmentGraph
    {
        // We assign 1-10 as the id range reserved only for characters Object> characters = new List<EnvironmentObject>();
        public List<EnvironmentObject> nodes = new List<EnvironmentObject>();
        public List<EnvironmentRelation> edges  = new List<EnvironmentRelation>();

        public void AddNode(EnvironmentObject node, int id)
        {
            if (node != null) {
                //node.id = nodes.Count + 1;
                node.id = id;
                nodes.Add(node);
            }
        }

        public void AddEdge(EnvironmentObject node1, EnvironmentObject node2, ObjectRelation relation)
        {
            if (node1 != null && node2 != null) {
                edges.Add(new EnvironmentRelation() {
                    from_id = node1.id,
                    to_id = node2.id,
                    relation_type = relation
                });
            }
        }

        public EnvironmentGraph Copy()
        {
            EnvironmentGraph graphCopy = new EnvironmentGraph();
            graphCopy.nodes = new List<EnvironmentObject>(this.nodes);
            graphCopy.edges = new List<EnvironmentRelation>(this.edges);
            return graphCopy;
        }
    }


    public class EnvironmentGraphCreator
    {
        public const string CharacterClassName = "character";
        public const string CharactersCategory = "Characters";
        public const string RoomsCategory = "Rooms";
        public static readonly string[] RoomBoundaryCategories = { "Floor", "Walls", "Ceiling", "Windows" };
        public static readonly string[] DEFAULT_ON_OBJECTS = { "lighting", "lightswitch", "light", "tablelamp", "lamp" };
        public static readonly string[] IGNORE_IN_EDGE_OBJECTS = { "curtains", "floor", "wall", "ceiling", "ceilinglamp", "walllamp", "doorjamb" };
        public const string DoorClassName = "door";
        public const string DoorjambClassName = "doorjamb";
        public const string DoorsCategory = "Doors";
        public static int ids_char = 10;

        private DataProviders dataProviders;

        // Initialized by CreateGraph, used in graph creation methods invoked by CreateGraph
        private EnvironmentGraph graph;  // Result fo CreateGraph
        public IDictionary<GameObject, EnvironmentObject> objectNodeMap;  // Game object to its node
        private HashSet<Tuple<int, int, ObjectRelation>> edgeSet;  // Contains triples (node id, node id, object relation)
        private IList<EnvironmentObject> rooms;
        private IList<EnvironmentObject> doors;

        public IDictionary<EnvironmentObject, Character> characters;
        private int nodeCounter = ids_char + 1;
        public double EdgeRadius2 { get; set; } = 25.0;

        public EnvironmentGraphCreator(DataProviders dataProviders)
        {
            this.dataProviders = dataProviders;
        }

        public EnvironmentGraph CreateGraph(Transform homeTransform)
        {
            graph = new EnvironmentGraph();
            objectNodeMap = new Dictionary<GameObject, EnvironmentObject>();
            edgeSet = new HashSet<Tuple<int, int, ObjectRelation>>();
            rooms = new List<EnvironmentObject>();
            doors = new List<EnvironmentObject>();
            characters = new Dictionary<EnvironmentObject, Character>();

            UpdateGraphNodes(homeTransform, null, "Home", null);
            UpdateGraphEdges();
            return graph;
        }

        public EnvironmentGraph SetGraph(EnvironmentGraph newGraph)
        {
            graph = newGraph.Copy();
            edgeSet.Clear();
            objectNodeMap.Clear();
            foreach (EnvironmentObject eo in graph.nodes)
            {
                if (eo.transform != null)
                {
                    objectNodeMap[eo.transform.gameObject] = eo;
                }
            }
            return graph;
        }

        public EnvironmentGraph GetGraph()
        {
            graph.nodes.Sort((eo1, eo2) => eo1.id.CompareTo(eo2.id));
            return graph;
        }

        public EnvironmentGraph UpdateGraph(Transform homeTransform, ISet<GameObject> changedObjs = null, List<ActionObjectData> last_actions = null)
        {
            // Updates current graph according to some actions or the full graph

            if (changedObjs == null && last_actions == null)
            {
                graph.edges.Clear();
                edgeSet.Clear();
                UpdateGraphNodes(homeTransform, null, "Home", null);
                UpdateGraphEdges();
                //foreach (EnvironmentObject eo in graph.nodes){
                //    if (eo.properties.Contains("SURFACES")){
                //    if (!eo.transform.gameObject.GetComponent<Rigidbody>()){
                //        Rigidbody rb = eo.transform.gameObject.AddComponent<Rigidbody>();
                //        rb.useGravity = true;
                //        rb.isKinematic = true;
                //    }
                //}
                //}
            }
            else if (last_actions == null)
            {
                List<EnvironmentObject> chars = new List<EnvironmentObject>(characters.Keys);

                foreach (EnvironmentObject c in chars)
                {
                    changedObjs.Add(c.transform.gameObject);
                }

                List<EnvironmentObject> changedEnvObjs = (from go in changedObjs select objectNodeMap[go]).ToList();

                foreach (EnvironmentObject eo in changedEnvObjs)
                {
                    RemoveGraphEdgesWithObject(eo);
                }

                UpdateGraphNodes(homeTransform, null, "Home", null);

                UpdateGraphEdges(changedEnvObjs);
            }
            else
            {
                UpdateGraphNodes(last_actions);
                UpdateGraphEdges(last_actions);


            }

            graph.nodes.Sort((eo1, eo2) => eo1.id.CompareTo(eo2.id));

            return graph;
        }

        public EnvironmentObject AddChar(Transform transform)
        {
            EnvironmentObject o = AddCharacterObject(transform);
            characters[o] = new Character(o);
            AddRoomRelation(o, FindRoomLocation(o)); // Add IN relation to room
            return o;
        }
        // Adds nodes and edges to the environment graph, recursively from transform
        // - parentTransform is immediate parent of this transform
        // - category is name of transform which is immediately "below" the room
        // - roomObject is room this object belongs to
        // Should only be called by CreateGraph
        public void UpdateGraphNodes(Transform transform, Transform parentTransform, String category, EnvironmentObject roomObject)
        {
            GameObject gameObject = transform.gameObject;
            string prefabName = gameObject.name;

            if (!gameObject.activeInHierarchy && !IsInGraph(gameObject))
            {
                // Skip inactive objects and their children
                return;
            }
            else if (!gameObject.activeInHierarchy && IsInGraph(gameObject))
            {
                // Remove inactive objects and their children
                graph.nodes.Remove(objectNodeMap[gameObject]);
                for (int i = 0; i < transform.childCount; i++)
                {
                    Transform childTransform = transform.GetChild(i);
                    childTransform.gameObject.SetActive(false);
                }
            }
            else if (gameObject.activeInHierarchy && !IsInGraph(gameObject))
            {
                if (transform.CompareTag(Tags.TYPE_ROOM))
                {
                    // Object is a room
                    roomObject = AddRoomObject(transform);
                    rooms.Add(roomObject);
                }
                else if (transform.CompareTag(Tags.TYPE_CHARACTER))
                {
                    // Object is a character
                    AddChar(transform);
                    return;
                }
                else
                {
                    // Ordinary or category object
                    if (parentTransform != null && parentTransform.CompareTag(Tags.TYPE_ROOM))
                        category = transform.name;

                    EnvironmentObject o = AddObject(transform, category);
                    
                    if (o != null && roomObject != null)
                    {
                        AddRoomRelation(o, roomObject);  // Add IN relation to room

                        // TODO: Include a checker here, this should never happen
                        //if (roomObject.bounding_box == null) roomObject.bounding_box = new ObjectBounds(o.bounding_box);  // Set initial room bounds
                        //else roomObject.bounding_box.UnionWith(o.bounding_box);  // Grow room bounds
                    }
                    if (o?.class_name == DoorClassName)
                    {
                        doors.Add(o);
                    }
                }
            }
            else
            {
                // this block will only be reached when updating the graph
                // due to an action but not when creating the graph

                EnvironmentObject currentObject = objectNodeMap[gameObject];
                // update the bounding boxes of objects that are already in the graph and are active
                string className = dataProviders.ObjectSelectorProvider.GetClassName(prefabName);

                if (className != null)
                {
                    className = ScriptUtils.TransformClassName(className);
                    ObjectBounds bounds;
                    if (className == DoorClassName)
                    {
                        bounds = DoorBounds(gameObject);
                    }
                    else
                    {
                        bounds = ObjectBounds.FromGameObject(gameObject);
                    }
                    currentObject.bounding_box = bounds;
                }

                // update the inside room edges if the object it self is not a room
                if (transform.CompareTag(Tags.TYPE_ROOM))
                {
                    // Object is a room
                    roomObject = currentObject;
                }
                else if (transform.CompareTag(Tags.TYPE_CHARACTER))
                {
                    AddRoomRelation(currentObject, FindRoomLocation(currentObject));
                }
                else if (currentObject.properties.Contains("GRABBABLE"))
                {
                    AddRoomRelation(currentObject, FindRoomLocation(currentObject));
                }
                else if (currentObject != null && roomObject != null)
                {
                    AddRoomRelation(currentObject, roomObject);
                }
            }

            // Recursive call
            for (int i = 0; i < transform.childCount; i++) {
                Transform childTransform = transform.GetChild(i);
                UpdateGraphNodes(childTransform, transform, category, roomObject);
            }
        }

        private void UpdateGraphNodes(List<ActionObjectData> last_actions)
        {
            for (int i = 0; i < last_actions.Count; i++)
            {
                ScriptPair action_script = last_actions[i].script;

                ScriptObjectData first_obj;

                if (last_actions[i].GetFirstObject(out first_obj))
                {
                    GameObject gameObject = first_obj.GameObject;


                    // Update state
                    if (action_script.Action is OpenAction)
                    {
                        if (((OpenAction)action_script.Action).Close)
                        {
                            objectNodeMap[gameObject].states.Remove(Utilities.ObjectState.OPEN);
                            objectNodeMap[gameObject].states.Add(Utilities.ObjectState.CLOSED);

                        }
                        else
                        {
                            objectNodeMap[gameObject].states.Remove(Utilities.ObjectState.CLOSED);
                            objectNodeMap[gameObject].states.Add(Utilities.ObjectState.OPEN);
                        }

                    }
                    else if (action_script.Action is SwitchOnAction)
                    {
                        if (((SwitchOnAction)action_script.Action).Off)
                        {
                            objectNodeMap[gameObject].states.Remove(Utilities.ObjectState.ON);
                            objectNodeMap[gameObject].states.Add(Utilities.ObjectState.OFF);

                        }
                        else
                        {
                            objectNodeMap[gameObject].states.Remove(Utilities.ObjectState.OFF);
                            objectNodeMap[gameObject].states.Add(Utilities.ObjectState.ON);
                        }
                    }

                    ObjectBounds bounds = ObjectBounds.FromGameObject(gameObject);
                    objectNodeMap[gameObject].bounding_box = bounds;
                    objectNodeMap[gameObject].obj_transform = new ObjectTransform(gameObject.transform); 
                }
            }
            // Update bounds of character
            foreach (Character character in characters.Values)
            {
                // Update bounds of object held by character
                ObjectBounds bounds = ObjectBounds.FromGameObject(character.character.transform.gameObject);
                character.character.bounding_box = bounds;
                character.character.obj_transform = new ObjectTransform(character.character.transform);


                if (character.grabbed_left != null)
                {
                    GameObject gameObjectGrabbed = character.grabbed_left.transform.gameObject;
                    bounds = ObjectBounds.FromGameObject(gameObjectGrabbed);
                    objectNodeMap[gameObjectGrabbed].bounding_box = bounds;

                    objectNodeMap[gameObjectGrabbed].obj_transform = new ObjectTransform(gameObjectGrabbed.transform); 

                }
                if (character.grabbed_right != null)
                {
                    GameObject gameObjectGrabbed = character.grabbed_right.transform.gameObject;
                    bounds = ObjectBounds.FromGameObject(gameObjectGrabbed);
                    objectNodeMap[gameObjectGrabbed].bounding_box = bounds;

                    objectNodeMap[gameObjectGrabbed].obj_transform = new ObjectTransform(gameObjectGrabbed.transform); 

                }
            }
        }

        private bool isCloseSymmetric()
        {
            foreach (EnvironmentObject c in characters.Keys)
            {
                foreach (EnvironmentRelation rel in graph.edges)
                {
                    if (rel.relation_type.Equals(ObjectRelation.CLOSE) && rel.from_id == c.id)
                    {
                        if (!edgeSet.Contains(Tuple.Create(rel.to_id, rel.from_id, rel.relation_type)))
                        {
                            return false;
                        }
                    }
                }
            }
            return true;
        }

        public EnvironmentObject FindRoomLocation(EnvironmentObject currentObject)
        {
            List<EnvironmentObject> candidates = new List<EnvironmentObject>();
            Vector3 objectCenter = currentObject.bounding_box.bounds.center;

            foreach (EnvironmentObject room in rooms)
            {
                if (room.transform.gameObject.activeInHierarchy &&
                    room.bounding_box.bounds.Contains(objectCenter))
                {
                    candidates.Add(room);
                }
            }
            if (candidates.Count == 0)
            {
                Debug.LogError(String.Format("Object {0} with id {1} is not in any room", currentObject.class_name, currentObject.id));
                return null;
            }
            else
            {
                
                EnvironmentObject roomCandidate = candidates[0];
                float maxDistanceToBound = 0;

                foreach (EnvironmentObject room in candidates)
                {
                    
                    float distX = room.bounding_box.bounds.extents.x - Math.Abs(room.bounding_box.bounds.center.x - objectCenter.x);
                    float distZ = room.bounding_box.bounds.extents.z - Math.Abs(room.bounding_box.bounds.center.z - objectCenter.z);

                    float distance = Math.Min(distX, distZ);
                    if (distance > maxDistanceToBound)
                    {
                        maxDistanceToBound = distance;
                        roomCandidate = room;
                    }
                }
                return roomCandidate;
            }

        }

        public Boolean IsInGraph(GameObject go)
        {
            return objectNodeMap.Keys.Contains(go) && graph.nodes.Contains(objectNodeMap[go]);
        }
        
        private EnvironmentObject AddObject(Transform transform, String category)
        {
            GameObject gameObject = transform.gameObject;
            string prefabName = gameObject.name;
            string className = dataProviders.ObjectSelectorProvider.GetClassName(prefabName);

            if (className == null)
                return null;

            className = ScriptUtils.TransformClassName(className);

            ObjectBounds bounds;
            
            if (className == DoorClassName) {
                bounds = DoorBounds(gameObject);
            } else {
                bounds = ObjectBounds.FromGameObject(gameObject);
            }

            if (bounds == null)
                return null;

            ICollection<string> properties = ObjectProperties(className);

            EnvironmentObject node = new EnvironmentObject() {
                transform = transform,
                category = category,
                class_name = className,
                prefab_name = prefabName,
                bounding_box = bounds,
                states = GetDefaultObjectStates(gameObject, className, properties),
                properties = properties
            };

            if (node.transform.CompareTag(Tags.TYPE_CHARACTER))
            {
                graph.AddNode(node, characters.Count + 1);
                Debug.Assert(characters.Count + 1 <= 10);
            }
            else
            {
                graph.AddNode(node, nodeCounter);
                nodeCounter++;
            }

            //graph.AddNode(node);
            objectNodeMap[gameObject] = node;
            return node;
        }

        private ICollection<string> ObjectProperties(string className)
        {
            return dataProviders.ObjectPropertiesProvider.PropertiesForClass(className);
        }

        private EnvironmentObject AddCharacterObject(Transform transform)
        {
            GameObject gameObject = transform.gameObject;
            string prefabName = gameObject.name;
            EnvironmentObject characterNode = new EnvironmentObject() {
                category = CharactersCategory,
                class_name = CharacterClassName,
                prefab_name = prefabName,
                bounding_box = ObjectBounds.FromGameObject(gameObject),
                transform = transform
            };
            graph.AddNode(characterNode, characters.Count + 1);
            objectNodeMap[gameObject] = characterNode;
            return characterNode;
        }

        private EnvironmentObject AddRoomObject(Transform transform)
        {
            GameObject gameObject = transform.gameObject;
            string prefabName = gameObject.name;
            string roomName = dataProviders.RoomSelector.ExtractRoomName(prefabName);
            ObjectBounds bounds = new ObjectBounds(gameObject.GetComponent<RoomProperties.Properties_room>().bounds);

            //GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            //sphere.transform.position = bounds.bounds.center - bounds.bounds.extents;
            //sphere.transform.localScale = new Vector3(0.05f, 0.05f, 0.05f);


            //GameObject sphere2 = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            //sphere2.transform.position = bounds.bounds.center + bounds.bounds.extents;
            //sphere2.transform.localScale = new Vector3(0.05f, 0.05f, 0.05f);

            //GameObject sphere3 = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            //sphere3.transform.position = bounds.bounds.center;
            //sphere3.transform.localScale = new Vector3(0.05f, 0.05f, 0.05f);

            EnvironmentObject roomNode = new EnvironmentObject() {
                category = RoomsCategory,
                class_name = roomName,
                prefab_name = prefabName,
                transform = transform,
                bounding_box = bounds
            };
            graph.AddNode(roomNode, nodeCounter);
            nodeCounter++;
            objectNodeMap[gameObject] = roomNode;
            return roomNode;
        }

        private KDTree<float, EnvironmentObject> BuildKDTree(IEnumerable<EnvironmentObject> treeObjects)
        {
            foreach (EnvironmentObject eo in treeObjects)
            {
                if (eo.bounding_box == null)
                {
                    eo.bounding_box = ObjectBounds.FromGameObject(eo.transform.gameObject);
                }
            }

            IEnumerable<float[]> centers = treeObjects.Select(o => o.bounding_box.center);
            var centerArray = centers.ToArray();

            var tree = new KDTree<float, EnvironmentObject>(3, centers.ToArray(), treeObjects.ToArray(), L2NormSquaredFloat);
            return tree;
        }

        public void UpdateGraphEdges(List<ActionObjectData> last_actions)
        {
            // Updates graph edges based on actions, only the close edges will be updated as normal
            // Update close relationship
            IEnumerable<EnvironmentObject> treeObjects = graph.nodes.Where(o => o.category != RoomsCategory &&
                                                                               o.category != DoorsCategory &&
                                                                               !IGNORE_IN_EDGE_OBJECTS.Contains(o.class_name) &&
                                                                               o.transform != null);
            var tree = BuildKDTree(treeObjects);
            for (int i = 0; i < last_actions.Count; i++)
            {
                ActionObjectData action = last_actions[i];
                Character char_action = action.character;

                ScriptObjectData first_obj;
                action.GetFirstObject(out first_obj);

                if (action.script.Action is GrabAction)
                {
                    if (char_action.grabbed_left != null && char_action.grabbed_left.id == objectNodeMap[first_obj.GameObject].id)
                    {
                        RemoveGraphEdgesWithObject(char_action.grabbed_left);
                        AddGraphEdge(char_action.character, char_action.grabbed_left, ObjectRelation.HOLDS_LH);
                    }
                    if (char_action.grabbed_right != null && char_action.grabbed_right.id == objectNodeMap[first_obj.GameObject].id)
                    {
                        RemoveGraphEdgesWithObject(char_action.grabbed_right);
                        AddGraphEdge(char_action.character, char_action.grabbed_right, ObjectRelation.HOLDS_RH);
                    }
                }
                else if (action.script.Action is PutAction)
                {
                    EnvironmentObject envobj1 = objectNodeMap[first_obj.GameObject];
                    ScriptObjectData second_obj;

                    if (action.GetSecondObject(out second_obj))
                    {
                        EnvironmentObject envobj2 = objectNodeMap[second_obj.GameObject];

                        RemoveGraphEdgesWithObject(envobj1);
                        if (((PutAction)(action.script.Action)).PutInside)
                        {
                            AddGraphEdge(envobj1, envobj2, ObjectRelation.INSIDE);
                        }
                        else
                        {
                            EnvironmentObject roomobj2 = FindRoomLocation(envobj2);
                            AddGraphEdge(envobj1, envobj2, ObjectRelation.ON);
                            AddGraphEdge(envobj1, roomobj2, ObjectRelation.INSIDE);
                        }


                    }

                }


                else if (action.script.Action is PutBackAction)
                {
                    Debug.LogError("PUTOBJBACK NOT IMPLEMENTED ON THIS SETUP");
                }
                else if (action.script.Action is StandupAction)
                {
                    RemoveGraphEdgesWithObject(char_action.character, ObjectRelation.SITTING);
                }
                else if (action.script.Action is SitAction)
                {
                    EnvironmentObject envobj1 = objectNodeMap[first_obj.GameObject];
                    AddGraphEdge(char_action.character, envobj1, ObjectRelation.SITTING);
                }

                // Update character location and that of their objects
                EnvironmentObject char_o1 = char_action.character;

                RemoveGraphEdgesWithObject(char_o1, ObjectRelation.CLOSE);
                RemoveGraphEdgesWithObject(char_o1, ObjectRelation.ON);
                float[] center = char_o1.bounding_box.center;
                Tuple<float[], EnvironmentObject>[] searchResult = tree.RadialSearch(center, EdgeRadius2);

                foreach (var t in searchResult)
                {
                    if (t.Item2 != char_o1)
                    {
                        AddRelation(char_o1, t.Item2);
                    }
                }


                RemoveGraphEdgesWithObject(char_o1, ObjectRelation.INSIDE);
                EnvironmentObject room_char = FindRoomLocation(char_o1);

                AddRoomRelation(char_o1, room_char);
                if (char_action.grabbed_left != null)
                {

                    RemoveGraphEdgesWithObject(char_action.grabbed_left, ObjectRelation.INSIDE);
                    AddRoomRelation(char_action.grabbed_left, room_char);

                }
                if (char_action.grabbed_right != null)
                {

                    RemoveGraphEdgesWithObject(char_action.grabbed_right, ObjectRelation.INSIDE);
                    AddRoomRelation(char_action.grabbed_right, room_char);

                }


            }

        }

        // Add relations between nodes
        // Should only be called by CreateGraph
        private void UpdateGraphEdges(IEnumerable<EnvironmentObject> changedEnvObjs = null)
        {
            // Updates graph edges based on actions, only the close edges will be updated as normal
            // Update close relationship

            IEnumerable<EnvironmentObject> treeObjects = graph.nodes.Where(o => o.category != RoomsCategory &&
                                                                               o.category != DoorsCategory &&
                                                                               !IGNORE_IN_EDGE_OBJECTS.Contains(o.class_name) &&
                                                                               o.transform != null);
            if (changedEnvObjs == null)
            {
                changedEnvObjs = treeObjects;
            }

            var tree = BuildKDTree(treeObjects);
            // Methods using only one object
            foreach (EnvironmentObject o in treeObjects) {
                AddRelation(o);
            }

            // Methods using pair of (close) objects
            foreach (EnvironmentObject o1 in changedEnvObjs)
            {

                float[] center = o1.bounding_box.center;
                Tuple<float[], EnvironmentObject>[] searchResult = tree.RadialSearch(center, EdgeRadius2);
                
                foreach (var t in searchResult)
                {
                    if (t.Item2 != o1)
                    {
                        AddRelation(o1, t.Item2);
                    }
                }

            }
            // Add grabbed objects
            foreach (KeyValuePair<EnvironmentObject, Character> o in characters)
            {
                if (o.Value.grabbed_left != null)
                {
                    EnvironmentObject grabbed_obj = o.Value.grabbed_left;
                    RemoveGraphEdgesWithObject(grabbed_obj);
                    AddGraphEdge(o.Value.character, grabbed_obj, ObjectRelation.HOLDS_LH);
                }
                if (o.Value.grabbed_right != null)
                {
                    EnvironmentObject grabbed_obj = o.Value.grabbed_right;
                    RemoveGraphEdgesWithObject(grabbed_obj);
                    AddGraphEdge(o.Value.character, grabbed_obj, ObjectRelation.HOLDS_RH);
                }
            }

        }

        private void AddRoomRelation(EnvironmentObject o, EnvironmentObject roomObject)
        {
            AddGraphEdge(o, roomObject, ObjectRelation.INSIDE);
        }

        // Method using single object
        private void AddRelation(EnvironmentObject o)
        {
            On(o);
            Between(o);
        }

        // Methods using pair of objects
        private void AddRelation(EnvironmentObject o1, EnvironmentObject o2, bool only_close_char=true)
        {
            if (o1.id == o2.id)
                return;
            if (o1.id  == 193 && o2.id == 139)
            {
                Debug.Log("Here");
            }
            if (!Inside(o1, o2))
                if (!Inside(o2, o1))
                    if (!On(o1, o2))
                        On(o2, o1);

            if (!only_close_char || o1.class_name == "character" || o2.class_name == "character")
                Close(o1, o2);
            Facing(o1, o2);
        }

        public void AddGraphEdge(EnvironmentObject n1, EnvironmentObject n2, ObjectRelation or)
        {
            if (n1 == n2)
                return;
            var edgeTriple = Tuple.Create(n1.id, n2.id, or);

            // Prevent adding multiple edges with the same relation (TODO: maybe move to graph class)
            if (!edgeSet.Contains(edgeTriple)) {
                edgeSet.Add(edgeTriple);
                graph.AddEdge(n1, n2, or);
            }
        }

        public void RemoveObject(EnvironmentObject o)
        {
            RemoveGraphEdgesWithObject(o);
            graph.nodes.Remove(o);
        }

        public void RemoveGraphEdgesWithObject(EnvironmentObject o, ObjectRelation? or = null)
        {
            if (or.HasValue)
            {
                edgeSet.RemoveWhere(e => (o.id == e.Item1 || o.id == e.Item2) && e.Item3 == or);
                graph.edges.RemoveAll(e => (o.id == e.from_id || o.id == e.to_id) && e.relation_type == or);
            }
            else
            {

                edgeSet.RemoveWhere(e => o.id == e.Item1 || o.id == e.Item2);
                graph.edges.RemoveAll(e => o.id == e.from_id || o.id == e.to_id);
            }
        }


        // Adds INSIDE relation if o1 is inside o2
        // Conditions:
        //   - bounding box of o2 contains bounding box of o1
        public bool Inside(EnvironmentObject o1, EnvironmentObject o2)
        {
            if (RoomBoundaryCategories.Contains(o2.category) || o2.category == DoorsCategory)
                return false;

            if (!o2.class_name.Contains("sink") && o2.properties.Contains("CONTAINERS") && o2.bounding_box.bounds.Contains(o1.bounding_box.bounds.center))
            {
                if (o1.properties.Contains("CONTAINERS") && o2.bounding_box.bounds.Contains(o1.bounding_box.bounds.center))
                {

                    // If one inside each other, only add if o2 is larger
                    float size_o2 = o2.bounding_box.size[0] * o2.bounding_box.size[2] * o2.bounding_box.size[1];
                    float size_o1 = o1.bounding_box.size[0] * o1.bounding_box.size[2] * o1.bounding_box.size[1];
                    if (size_o2 > size_o1)
                    {
                        AddGraphEdge(o1, o2, ObjectRelation.INSIDE);
                    }

                }
                else
                {
                    AddGraphEdge(o1, o2, ObjectRelation.INSIDE);
                }
                return true;
            }
            return false;
        }

        // Adds CLOSE relation if distance from center of o1 to bounds of o2 (or vice-versa) is <= MAX_CLOSE_DISTANCE
        public bool Close(EnvironmentObject o1, EnvironmentObject o2)
        {
            
            if (o1.id == o2.id)
            {
                return false;
            }
            const float MAX_CLOSE_DISTANCE = 1.3f;

            bool close = false;

            Vector3 o2Too1 = o1.bounding_box.bounds.ClosestPoint(o2.bounding_box.bounds.center);
            Vector3 o2center = o2.bounding_box.bounds.center;

            if (o1.class_name == "character" || o2.class_name == "character")
            {

                o2center.y = 0.0f;
                o2Too1.y = 0.0f;

            }

            close = Vector3.Distance(o2center, o2Too1) <= MAX_CLOSE_DISTANCE;
            if (!close) {
                Vector3 o1Too2 = o2.bounding_box.bounds.ClosestPoint(o1.bounding_box.bounds.center);
                Vector3 o1center = o1.bounding_box.bounds.center;
                if (o1.class_name == "characer" || o2.class_name == "character")
                {

                    o1center.y = 0.0f;
                    o1Too2.y = 0.0f;

                }
                close = Vector3.Distance(o1center, o1Too2) <= MAX_CLOSE_DISTANCE;
            }

            if (close) {
                AddGraphEdge(o1, o2, ObjectRelation.CLOSE);
                AddGraphEdge(o2, o1, ObjectRelation.CLOSE);
            }
            return close;
        }

        // Adds ON relation if o is on some object using "boxcast" method
        public void On(EnvironmentObject o)
        {
            const float Delta = 0.05f;

            RaycastHit[] raycastHits = Physics.BoxCastAll(o.bounding_box.bounds.center + new Vector3(0, Delta, 0), o.bounding_box.bounds.extents, Vector3.down,
                    Quaternion.identity, 2 * Delta);
            foreach (RaycastHit hit in raycastHits) {
                Transform t = hit.transform;

                while (t != null) {
                    EnvironmentObject o2;
                    if (objectNodeMap.TryGetValue(t.gameObject, out o2)) {
                        if (o.id != o2.id && o2.category != RoomsCategory /* && ... */ && CheckOnCondition(o, o2) && !CheckInsideCondition(o2, o)
                                && !CheckInsideCondition(o, o2))
                            AddGraphEdge(o, o2, ObjectRelation.ON);
                        break;
                    }
                    t = t.parent;
                }
            }
        }

        // Adds BETWEEN relation on doors (two edges door-room, each with BETWEEN relation)
        public void Between(EnvironmentObject o)
        {
            List<EnvironmentObject> ir = new List<EnvironmentObject>();

            if (o.class_name == DoorClassName) {
                foreach (EnvironmentObject r in rooms) {
                    if (o.bounding_box.bounds.Intersects(r.bounding_box.bounds)) {
                        ir.Add(r);
                    }
                }
            } else if (o.class_name == DoorjambClassName) {
                Bounds box = o.bounding_box.bounds;

                if (box.max.x - box.min.x > box.max.z - box.min.z) {
                    box.Expand(new Vector3(0, 0, 0.5f));
                } else {
                    box.Expand(new Vector3(0.5f, 0, 0));
                }
                foreach (EnvironmentObject d in doors) {
                    if (box.Intersects(d.bounding_box.bounds))
                        return;
                }
                foreach (EnvironmentObject r in rooms) {
                    if (box.Intersects(r.bounding_box.bounds)) {
                        ir.Add(r);
                    }
                }
            }
            if (ir.Count == 2) {
                AddGraphEdge(o, ir[0], ObjectRelation.BETWEEN);
                AddGraphEdge(o, ir[1], ObjectRelation.BETWEEN);
            }
        }

        // Adds ON relation if o1 is on o2 using bounding box-on-bounding box method
        public bool On(EnvironmentObject o1, EnvironmentObject o2)
        {
            float Delta = 0.01f; 

            float o2MaxY = o2.bounding_box.bounds.max.y;
            var pc = o2.transform.GetComponent<Properties_chair>();
            bool is_chair = false;

            if (pc != null)
            {
                List<Properties_chair.SittableUnit> suList = pc.GetSittableUnits();
                if (suList.Count() > 0)
                    o2MaxY = suList[0].tsfm_group.position.y;
                is_chair = true;
            }
            Delta = Math.Min(Math.Max(Delta, o1.bounding_box.size[1]*0.3f), 0.2f);
            Interval<float> yInt = new Interval<float>(o2MaxY - Delta, o2MaxY + Delta);
            if (yInt.Contains(o1.bounding_box.bounds.min.y) && CheckOnCondition(o1, o2, !is_chair)) {
                AddGraphEdge(o1, o2, ObjectRelation.ON);
                return true;
            }
            return false;
        }

        // o2 is facing o1 if o1 is lookable, differenct conditions apply if o2 is sofa or chair
        public void Facing(EnvironmentObject o1, EnvironmentObject o2)
        {
            if (o1.transform == null || o2.transform == null)
                return;

            if (o1.properties.Contains("LOOKABLE")) {
                Bounds o1bounds = o1.bounding_box.bounds;
                Bounds o2bounds = o2.bounding_box.bounds;

                // Check visibility
                if (!ScriptExecutor.IsVisibleFromCorners(o2.transform.gameObject, o1bounds) &&
                        !ScriptExecutor.IsVisibleFromCorners(o1.transform.gameObject, o2bounds)) {
                    return;
                }

                var pc = o2.transform.GetComponent<Properties_chair>();

                if (pc != null) {
                    List<Properties_chair.SittableUnit> suList = pc.GetSittableUnits();
                    foreach (var su in suList) {
                        Transform lookAt = su.GetTsfm_lookAtBody();
                        if (AddFacingEdge(o1, o1.transform.right, o2, lookAt.right)) {
                            break;
                        }
                    }
                } else if (o2.class_name == "chair" || o2.class_name == "sofa") {
                    // Just is in case if there is no properties_chair attached to a sittable object "chair" or "sofa".
                    // Check if angle between x axis of o1 and vector o1 -> o2 is less than 75 deg
                    AddFacingEdge(o1, o1.transform.right, o2, o2.transform.right);
                } else {
                    Vector3 axis1 = o1.transform.right;
                    Vector3 axis2 = o2.bounding_box.bounds.center - o1.bounding_box.bounds.center;
                    axis1.y = 0;
                    axis2.y = 0;

                    float angle = Vector3.Angle(axis1, axis2);

                    if (angle < 75) {
                        AddGraphEdge(o2, o1, ObjectRelation.FACING);
                    }
                }

            }
        }

        private bool AddFacingEdge(EnvironmentObject o1, Vector3 o1Dir, EnvironmentObject o2, Vector3 o2Dir)
        {
            Vector3 axis1 = o1Dir;
            Vector3 axis2 = o2.bounding_box.bounds.center - o1.bounding_box.bounds.center;
            axis1.y = 0;
            axis2.y = 0;

            float angle = Vector3.Angle(axis1, axis2);

            if (angle > 75) {
                return false;
            }
            axis1 = o2Dir;
            axis2 = o1.bounding_box.bounds.center - o2.bounding_box.bounds.center;
            axis1.y = 0;
            axis2.y = 0;
            angle = Vector3.Angle(axis1, axis2);

            if (angle > 75) {
                return false;
            }
            AddGraphEdge(o2, o1, ObjectRelation.FACING);
            return true;
        }

        private ObjectBounds DoorBounds(GameObject gameObject)
        {
            Properties_door pd = gameObject.GetComponent<Properties_door>();

            if (pd == null)
                return null;

            Vector2 dwCenter = (pd.doorwayA + pd.doorwayB) / 2.0f;
            float dwSize = (pd.doorwayA - pd.doorwayB).magnitude;
            return new ObjectBounds(new Bounds(new Vector3(dwCenter.x, 0.5f, dwCenter.y), 
                new Vector3(dwSize, 1.0f, dwSize)));
        }

        private static bool CheckOnCondition(EnvironmentObject o1, EnvironmentObject o2, bool y_check=true)
        {
            if (y_check && o2.bounding_box.bounds.center.y > o1.bounding_box.bounds.min.y)
                return false;

            Rect o1XZRect = BoundsUtils.XZRect(o1.bounding_box.bounds);
            Rect o2XZRect = BoundsUtils.XZRect(o2.bounding_box.bounds);

            // Sufficient area intersection
            return RectUtils.IntersectionArea(o1XZRect, o2XZRect) > o1XZRect.Area() * 0.5f;
        }

        private static bool CheckInsideCondition(EnvironmentObject o1, EnvironmentObject o2)
        {
            return o2.bounding_box.bounds.Contains(o1.bounding_box.bounds.min) &&
                o2.bounding_box.bounds.Contains(o1.bounding_box.bounds.max);
        }

        public static ISet<ObjectState> GetDefaultObjectStates(GameObject go, string className, ICollection<string> properties)
        {
            ISet<ObjectState> states = new HashSet<ObjectState>();

            Properties_door pd = go.GetComponent<Properties_door>();
            if (pd != null) {
                states.Add(ObjectState.OPEN);  // Assume that all doors are open by default
                return states;
            }

            HandInteraction hi = go.GetComponent<HandInteraction>();
            if (hi != null && hi.switches != null) {
                foreach (HandInteraction.ActivationSwitch s in hi.switches) {
                    if (s.action == HandInteraction.ActivationAction.Open) states.Add(ObjectState.CLOSED);  // Assume that all objects are closed by default
                    else if (s.action == HandInteraction.ActivationAction.SwitchOn) {
                        if (DEFAULT_ON_OBJECTS.Contains(className)) states.Add(ObjectState.ON);
                        else states.Add(ObjectState.OFF);
                    }
                }
                return states;
            }

            if (properties.Contains("CAN_OPEN")) {
                states.Add(ObjectState.CLOSED);
            }
            if (properties.Contains("HAS_SWITCH")) {
                states.Add(ObjectState.OFF);
            }
            return states;
        }

        public static Func<float[], float[], double> L2NormSquaredFloat = (u, v) => {
            float dist2 = 0f;

            for (int i = 0; i < u.Length; i++) {
                dist2 += (u[i] - v[i]) * (u[i] - v[i]);
            }
            return dist2;
        };

        public static EnvironmentGraph FromJson(string jsonString)
        {
            EnvironmentGraph result = JsonConvert.DeserializeObject<EnvironmentGraph>(jsonString);

            result.nodes.Sort((eo1, eo2) => eo1.id.CompareTo(eo2.id));
            foreach (EnvironmentObject eo in result.nodes) {
                eo.class_name = ScriptUtils.TransformClassName(eo.class_name);
            }
            return result;
        }
    }

}
