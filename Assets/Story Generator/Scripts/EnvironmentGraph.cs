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
        HOLDS_LH
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
            }
        }

        public String category { get; set; }  // Category of this object (Character, Room, ...)

        public string class_name { get; set; }  // Object class name (className from PrefabClass.json, "unknown" in not present)

        public string prefab_name { get; set; }  // GameObject name

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
        public List<EnvironmentObject> nodes = new List<EnvironmentObject>();
        public List<EnvironmentRelation> edges  = new List<EnvironmentRelation>();

        public void AddNode(EnvironmentObject node)
        {
            if (node != null) {
                node.id = nodes.Count + 1;
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
    }


    public class EnvironmentGraphCreator
    {
        public const string CharacterClassName = "character";
        public const string CharactersCategory = "Characters";
        public const string RoomsCategory = "Rooms";
        public static readonly string[] RoomBoundaryCategories = { "Floor", "Walls", "Ceiling", "Windows" };
        public static readonly string[] DEFAULT_ON_OBJECTS = { "lighting", "lightswitch", "light", "tablelamp", "lamp" };
        public const string DoorClassName = "door";
        public const string DoorjambClassName = "doorjamb";
        public const string DoorsCategory = "Doors";

        private DataProviders dataProviders;

        // Initialized by CreateGraph, used in graph creation methods invoked by CreateGraph
        private EnvironmentGraph graph;  // Result fo CreateGraph
        private IDictionary<GameObject, EnvironmentObject> objectNodeMap;  // Game object to its node
        private ISet<Tuple<int, int, ObjectRelation>> edgeSet;  // Contains triples (node id, node id, object relation)
        private IList<EnvironmentObject> rooms;
        private IList<EnvironmentObject> doors;


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
            
            UpdateGraphNodes(homeTransform, null, "Home", null);
            UpdateGraphEdges();
            return graph;
        }

        // Adds nodes and edges to the environment graph, recursively from transform
        // - parentTransform is immediate parent of this transform
        // - category is name of transform which is immediately "below" the room
        // - roomObject is room this object belongs to
        // Should only be called by CreateGraph
        private void UpdateGraphNodes(Transform transform, Transform parentTransform, String category, EnvironmentObject roomObject)
        {
            GameObject gameObject = transform.gameObject;
            string prefabName = gameObject.name;

            if (!gameObject.activeSelf) {
                // Skip inactive objects and their children
                return;
            } else if (transform.CompareTag(Tags.TYPE_ROOM)) {
                // Object is a room
                roomObject = AddRoomObject(transform);
                rooms.Add(roomObject);
            } else if (transform.CompareTag(Tags.TYPE_CHARACTER)) {
                // Object is a character
                EnvironmentObject o = AddCharacterObject(transform);
                if (roomObject != null) {
                    AddRoomRelation(o, roomObject); // Add IN relation to room
                }
                return;
            } else {
                // Ordinary or category object
                if (parentTransform != null && parentTransform.CompareTag(Tags.TYPE_ROOM))
                    category = transform.name;

                EnvironmentObject o = AddObject(transform, category);
                if (o != null && roomObject != null) {
                    AddRoomRelation(o, roomObject);  // Add IN relation to room
                    if (roomObject.bounding_box == null) roomObject.bounding_box = new ObjectBounds(o.bounding_box);  // Set initial room bounds
                    else roomObject.bounding_box.UnionWith(o.bounding_box);  // Grow room bounds
                }
                if (o?.class_name == DoorClassName) {
                    doors.Add(o);
                }
            }

            // Recursive call
            for (int i = 0; i < transform.childCount; i++) {
                Transform childTransform = transform.GetChild(i);
                UpdateGraphNodes(childTransform, transform, category, roomObject);
            }
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
            
            graph.AddNode(node);
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
            graph.AddNode(characterNode);
            objectNodeMap[gameObject] = characterNode;
            return characterNode;
        }

        private EnvironmentObject AddRoomObject(Transform transform)
        {
            GameObject gameObject = transform.gameObject;
            string prefabName = gameObject.name;
            string roomName = dataProviders.RoomSelector.ExtractRoomName(prefabName);
            EnvironmentObject roomNode = new EnvironmentObject() {
                category = RoomsCategory,
                class_name = roomName,
                prefab_name = prefabName,
                transform = transform
            };
            graph.AddNode(roomNode);
            objectNodeMap[gameObject] = roomNode;
            return roomNode;
        }

        // Add relations between nodes
        // Should only be called by CreateGraph
        private void UpdateGraphEdges()
        {
            // Create KD tree to speed-up neighbor search (skip rooms, edges were added in UpdateGraphNodes)
            IEnumerable<EnvironmentObject> treeObjects = graph.nodes.Where(o => o.category != RoomsCategory);
            IEnumerable<float[]> centers = treeObjects.Select(o => o.bounding_box.center);
            var tree = new KDTree<float, EnvironmentObject>(3, centers.ToArray(), treeObjects.ToArray(), L2NormSquaredFloat);
            
            // Methods using only one object
            foreach (EnvironmentObject o in treeObjects) {
                AddRelation(o);
            }

            // Methods using pair of (close) objects
            foreach (EnvironmentObject o1 in treeObjects) {
                float[] center = o1.bounding_box.center;
                Tuple<float[], EnvironmentObject>[] searchResult = tree.RadialSearch(center, EdgeRadius2);

                foreach (var t in searchResult) {
                    if (t.Item2 != o1) {                        
                        AddRelation(o1, t.Item2);
                    }
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

        // Methods using pair od objects
        private void AddRelation(EnvironmentObject o1, EnvironmentObject o2)
        {
            On(o1, o2);
            Inside(o1, o2);
            Close(o1, o2);
            Facing(o1, o2);
        }

        private void AddGraphEdge(EnvironmentObject n1, EnvironmentObject n2, ObjectRelation or)
        {
            var edgeTriple = Tuple.Create(n1.id, n2.id, or);

            // Prevent adding multiple edges with the same relation (TODO: maybe move to graph class)
            if (!edgeSet.Contains(edgeTriple)) {
                edgeSet.Add(edgeTriple);
                graph.AddEdge(n1, n2, or);
            }
        }

        // Adds INSIDE relation if o1 is inside o2
        // Conditions:
        //   - bounding box of o2 contains bounding box of o1
        public bool Inside(EnvironmentObject o1, EnvironmentObject o2)
        {
            if (RoomBoundaryCategories.Contains(o2.category) || o2.category == DoorsCategory)
                return false;

            if (o2.bounding_box.bounds.Contains(o1.bounding_box.bounds.min) &&
                    o2.bounding_box.bounds.Contains(o1.bounding_box.bounds.max)) {
                AddGraphEdge(o1, o2, ObjectRelation.INSIDE);
                return true;
            }
            return false;
        }

        // Adds CLOSE relation if distance from center of o1 to bounds of o2 (or vice-versa) is <= 1.5
        public bool Close(EnvironmentObject o1, EnvironmentObject o2)
        {
            const float MAX_CLOSE_DISTANCE = 1.5f;

            bool close = false;

            Vector3 o2Too1 = o1.bounding_box.bounds.ClosestPoint(o2.bounding_box.bounds.center);
            close = Vector3.Distance(o2.bounding_box.bounds.center, o2Too1) <= MAX_CLOSE_DISTANCE;
            if (!close) {
                Vector3 o1Too2 = o2.bounding_box.bounds.ClosestPoint(o1.bounding_box.bounds.center);
                close = Vector3.Distance(o1.bounding_box.bounds.center, o1Too2) <= MAX_CLOSE_DISTANCE;
            }
            if (close) {
                AddGraphEdge(o1, o2, ObjectRelation.CLOSE);
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
            const float Delta = 0.01f; 

            float o2MaxY = o2.bounding_box.bounds.max.y;
            Interval<float> yInt = new Interval<float>(o2MaxY - Delta, o2MaxY + Delta);

            if (yInt.Contains(o1.bounding_box.bounds.min.y) && CheckOnCondition(o1, o2)) {
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

        private static bool CheckOnCondition(EnvironmentObject o1, EnvironmentObject o2)
        {
            if (o2.bounding_box.bounds.center.y > o1.bounding_box.bounds.min.y)
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
