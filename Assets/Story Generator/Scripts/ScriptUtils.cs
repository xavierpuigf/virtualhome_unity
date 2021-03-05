using StoryGenerator.Helpers;
using StoryGenerator.Utilities;
using StoryGenerator.RoomProperties;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using Newtonsoft.Json;

namespace StoryGenerator.Scripts
{

    //public class NameEquivalenceData
    //{
    //    public List<string> equivalent;
    //    public List<string> forbidden;

    //    public NameEquivalenceData(List<string> equivalent, List<string> forbidden)
    //    {
    //        this.equivalent = equivalent;
    //        this.forbidden = forbidden;
    //    }

    //    public NameEquivalenceData(string name)
    //    {
    //        equivalent = new List<string> { name };
    //        forbidden = new List<string>();
    //    }

    //    internal void AddRanges(List<string> equiv, List<string> forb)
    //    {
    //        equivalent.AddRange(equiv);
    //        forbidden.AddRange(forb);
    //    }
    //}

    public class NameEquivalenceProvider : IEnumerable<KeyValuePair<string, ISet<string>>>
    {
        private IDictionary<string, ISet<string>> nameEquiv;

        public NameEquivalenceProvider(string resourceName)
        {
            InitNameEquivMap(resourceName);
        }

        public IEnumerator<KeyValuePair<string, ISet<string>>> GetEnumerator()
        {
            return nameEquiv.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return nameEquiv.GetEnumerator();
        }

        public ISet<string> GetSynonyms(string name)
        {
            ISet<string> result;

            if (nameEquiv.TryGetValue(name.ToLower(), out result)) return result;
            else return new HashSet<string>() { name };
        }

        public List<string> GetEquivalentNames(string name)
        {
            ISet<string> ned;

            name = ScriptUtils.TransformClassName(name);
            if (!nameEquiv.TryGetValue(name, out ned))
            {
                return new List<string>() { name };
            }
            else
            {
                List<string> result = ned.ToList();

                if (!ned.Contains(name))
                {
                    result.Insert(0, name);
                }
                return result;
            }
        }

        // return true if name1 is in "equivalence list" of name2
        public bool IsEquivalent(string name1, string name2)
        {
            //name1 = ScriptUtils.TransformClassName(name1);
            //name2 = ScriptUtils.TransformClassName(name2);

            if (name1 == name2)
                return true;

            ISet<string> result;

            if (nameEquiv.TryGetValue(name2.ToLower(), out result))
            {
                return result.Contains(name1);
            }
            else
            {
                return false;
            }
        }

        private void InitNameEquivMap(string resourceName)
        {
            TextAsset txtAsset = Resources.Load<TextAsset>(resourceName);
            var fileEquivMap = JsonConvert.DeserializeObject<Dictionary<string, List<string>>>(txtAsset.text);

            nameEquiv = new Dictionary<string, ISet<string>>();

            foreach (var e in fileEquivMap)
            {
                string key = ScriptUtils.TransformClassName(e.Key);
                ISet<string> values = new HashSet<string>(e.Value.Select(s => ScriptUtils.TransformClassName(s)));
                nameEquiv[key] = values;
            }
        }

        private static bool ParseLine(string line, out string key, out List<string> equivs, out List<string> forbiddens)
        {
            // Line example: 
            // DRINKING GLASSES -> Glass, _glass
            key = "";
            equivs = new List<string>();
            forbiddens = new List<string>();

            if (string.IsNullOrEmpty(line) || line[0] == '#')
                return false;

            string patt = @"([\w_\s]+)\s*->(.*)";

            Regex r = new Regex(patt);
            Match m = r.Match(line);

            if (!m.Success)
                return false;

            key = m.Groups[1].Value.Trim();
            string[] values = m.Groups[2].Value.Split(',');

            foreach (string v in values)
            {
                string vt = v.Trim();

                if (vt.Length > 0)
                {
                    if (vt[0] != '!')
                    {
                        equivs.Add(vt);
                    }
                    else
                    {
                        if (vt.Length > 1)
                            forbiddens.Add(vt.Substring(1));
                    }
                }
            }
            return true;
        }


    }

    public class ActionEquivalenceProvider
    {
        private IDictionary<Tuple<string, string, string>, string> actionEquivMap;

        public ActionEquivalenceProvider(string resourceName)
        {
            actionEquivMap = LoadActionMap(resourceName);
        }

        public bool TryGetEquivalentAction(string actionStr, string objectName, out string newActionStr)
        {
            actionStr = actionStr.ToUpper();
            objectName = objectName.ToLower();

            if (actionEquivMap.TryGetValue(Tuple.Create(actionStr, objectName, ""), out newActionStr))
                return true;
            else if (actionEquivMap.TryGetValue(Tuple.Create(actionStr, "*", ""), out newActionStr))
                return true;
            else
                return false;
        }

        public bool TryGetEquivalentAction(string actionStr, string object1Name, string object2Name, out string newActionStr)
        {
            actionStr = actionStr.ToUpper();
            object1Name = object1Name.ToLower();
            object2Name = object2Name.ToLower();

            if (actionEquivMap.TryGetValue(Tuple.Create(actionStr, object1Name, object2Name), out newActionStr))
                return true;
            else if (actionEquivMap.TryGetValue(Tuple.Create(actionStr, object1Name, "*"), out newActionStr))
                return true;
            else if (actionEquivMap.TryGetValue(Tuple.Create(actionStr, "*", object2Name), out newActionStr))
                return true;
            else if (actionEquivMap.TryGetValue(Tuple.Create(actionStr, "*", "*"), out newActionStr))
                return true;
            else
                return false;
        }

        private static IDictionary<Tuple<string, string, string>, string> LoadActionMap(string resourceName)
        {
            var result = new Dictionary<Tuple<string, string, string>, string>();
            TextAsset txtFile = Resources.Load<TextAsset>(resourceName);

            foreach (string line in txtFile.text.Split('\n'))
            {
                string action;
                string srcObj;
                string destObj;
                string eqAction;

                if (ParseActionLine(line, out action, out srcObj, out destObj, out eqAction))
                {
                    result[Tuple.Create(action.ToUpper(), srcObj.ToLower(), destObj.ToLower())] = eqAction.ToUpper();
                }
            }
            return result;
        }

        private static bool ParseActionLine(string line, out string action, out string srcObj, out string destObj, out string eqAction)
        {
            // Line example: 
            // FLUSH, toilet -> SWITCHON
            // PUTBACK, *, dishwasher -> PUTIN
            action = "";
            srcObj = "";
            destObj = "";
            eqAction = "";

            if (string.IsNullOrEmpty(line) || line[0] == '#')
                return false;

            string patt = @"(.*)->(.*)";

            Regex r = new Regex(patt);
            Match m = r.Match(line);

            if (!m.Success)
                return false;

            eqAction = m.Groups[2].Value.Trim();
            string[] values = m.Groups[1].Value.Split(',');

            if (values.Length == 2)
            {
                action = values[0].Trim();
                srcObj = values[1].Trim();
                return true;
            }
            else if (values.Length == 3)
            {
                action = values[0].Trim();
                srcObj = values[1].Trim();
                destObj = values[2].Trim();
                return true;
            }
            else
            {
                return false;
            }

        }
    }

    public class AssetsProvider
    {
        IDictionary<string, List<string>> assetsMap;

        public AssetsProvider(string resourceName)
        {
            InitAssetsMap(resourceName);
        }

        private void InitAssetsMap(string resourceName)
        {
            TextAsset txtAsset = Resources.Load<TextAsset>(resourceName);
            var tmpAssetsMap = JsonConvert.DeserializeObject<Dictionary<string, List<string>>>(txtAsset.text);

            assetsMap = new Dictionary<string, List<string>>();
            foreach (var e in tmpAssetsMap)
            {
                assetsMap[ScriptUtils.TransformClassName(e.Key)] = e.Value;
            }
        }

        public bool TryGetAssets(string mo, out List<string> paths)
        {
            return assetsMap.TryGetValue(mo.ToLower(), out paths);
        }
        public List<string> GetAssetsNames()
        {
            return assetsMap.Keys.ToList();
        }
        public List<string> GetAssetsPaths()
        {
            List<string> res = new List<string>();
            for (int index_val = 0; index_val < assetsMap.Count; index_val += 1)
            {
                var item = assetsMap.ElementAt(index_val);
                List<string> curr_val = item.Value.ToList();
                res = res.Union(curr_val).ToList();
            }
            return res;
        }
        public List<string> GetMissingPrefabs()
        {
            List<string> list_assets = this.GetAssetsPaths();
            List<string> results = new List<string>();
            foreach (string asset_name in list_assets)
            {
                if (asset_name != "Null")
                {
                    GameObject loadedObj = Resources.Load(ScriptUtils.TransformResourceFileName(asset_name)) as GameObject;
                    if (loadedObj == null)
                    {
                        results.Add(asset_name);

                    }

                }
            }
            return results;
        }
    }

    public class PlacingDataProvider
    {
        IDictionary<string, List<Tuple<string, string>>> placingMap;

        public PlacingDataProvider(string fileName)
        {
            placingMap = ScriptUtils.ParseBeginEndFileWithRoomName(fileName);
        }

        public bool TryGetPlaces(string mo, out List<Tuple<string, string>> places)
        {
            return placingMap.TryGetValue(mo.ToLower(), out places);
        }
    }

    public static class ScriptUtils
    {
        public static IDictionary<string, GameObject> FindAllObjectsMap(Transform tsfm)
        {
            var result = new Dictionary<string, GameObject>();
            FindAllObjects(tsfm, result, "");
            return result;
        }

        public static void FindAllObjects(Transform tsfm, IDictionary<string, GameObject> dict, string prefix)
        {
            if (!tsfm.gameObject.activeSelf)
                return;
            string key = prefix + "/" + tsfm.name;
            if (!dict.ContainsKey(key))
            {
                dict.Add(key, tsfm.gameObject);
                for (int i = 0; i < tsfm.childCount; i++)
                    FindAllObjects(tsfm.GetChild(i), dict, key);
            }
        }

        public static IList<GameObject> FindAllObjects(Transform tsfm)
        {
            var result = new List<GameObject>();
            FindAllObjects(tsfm, result);
            return result;
        }

        public static void FindAllObjects(Transform tsfm, IList<GameObject> list)
        {
            if (!tsfm.gameObject.activeSelf)
                return;
            list.Add(tsfm.gameObject);
            for (int i = 0; i < tsfm.childCount; i++)
                FindAllObjects(tsfm.GetChild(i), list);
        }

        public static IEnumerable<GameObject> FindAllObjects(Transform tsfm, Func<Transform, bool> selector)
        {
            if (selector(tsfm))
                yield return tsfm.gameObject;
            for (int i = 0; i < tsfm.childCount; i++)
            {
                foreach (var o in FindAllObjects(tsfm.GetChild(i), selector))
                    yield return o;
            }
        }

        public static IEnumerable<GameObject> FindAllObjects(Transform tsfm, string containsName)
        {
            return FindAllObjects(tsfm, t => t.name.Contains(containsName));
        }

        public static Vector3 FindRandomCCPosition(List<GameObject> rooms, CharacterControl characterControl)
        {
            const float ObstructionHeight = 0.1f;   // Allow for some obstuction around target object (e.g., carpet)
            const int maxIterations = 10;

            NavMeshAgent nma = characterControl.GetComponent<NavMeshAgent>();

            for (int iter = 0; iter < maxIterations; iter++)
            {
                GameObject room = rooms[UnityEngine.Random.Range(0, rooms.Count)];
                // Position of the room transform is not necessarily the center of the room
                // Bounds field of Properties_room is created to have the correct center.
                Bounds bnd = room.GetComponent<Properties_room>().bounds;
                Vector3 center = bnd.center;
                // What is the radius of the largest circle that can fit in a room?
                float maxRad = Mathf.Min(bnd.extents.x, bnd.extents.z) - 0.5f;
                Vector2 randomc = maxRad * UnityEngine.Random.insideUnitCircle;

                center.x += randomc.x;
                center.z += randomc.y;

                Vector3 cStart = new Vector3(center.x, nma.radius + ObstructionHeight, center.z);
                Vector3 cEnd = new Vector3(center.x, nma.height - nma.radius + ObstructionHeight, center.z);

                // Check for space
                if (!Physics.CheckCapsule(cStart, cEnd, nma.radius))
                {
                    return center;
                }
            }
            return nma.gameObject.transform.position;
        }

        public static List<Camera> FindAllCameras(Transform tsfm)
        {
            List<Camera> result = new List<Camera>();

            foreach (GameObject go in ScriptUtils.FindAllObjects(tsfm, t => t.GetComponent<Camera>() != null && t.GetComponent<Camera>().enabled))
            {
                result.Add(go.GetComponent<Camera>());
            }
            return result;
        }

        public static List<GameObject> FindAllCharacters(Transform tsfm)
        {
            List<GameObject> result = new List<GameObject>();

            foreach (GameObject go in ScriptUtils.FindAllObjects(tsfm, t => t.CompareTag(Tags.TYPE_CHARACTER) && t.gameObject.activeSelf))
            {
                result.Add(go);
            }
            return result;
        }

        public static List<GameObject> FindAllRooms(Transform tsfm)
        {
            List<GameObject> result = new List<GameObject>();

            foreach (GameObject go in ScriptUtils.FindAllObjects(tsfm, t => t.CompareTag(Tags.TYPE_ROOM) && t.gameObject.activeSelf))
            {
                result.Add(go);
            }
            return result;
        }

        public static string TransformResourceFileName(string fName)
        {
            string result = fName;
            if (fName.StartsWith("Resources/"))
                result = result.Remove(0, 10);
            int dotPos = result.LastIndexOf('.');
            if (dotPos >= 0)
                result = result.Remove(dotPos, result.Length - dotPos);
            return result;
        }

        internal static IDictionary<string, List<string>> ParseBeginEndFile(string fileName)
        {
            Dictionary<string, List<string>> result = new Dictionary<string, List<string>>();

            using (System.IO.StreamReader file = new System.IO.StreamReader(fileName))
            {
                string line;
                string key = null;

                while ((line = file.ReadLine()) != null)
                {
                    string value = null;

                    ParseBeginEndLine(line, ref key, ref value);

                    if (value != null && key != null)
                    {
                        key = key.ToLower();

                        List<string> list;

                        if (!result.TryGetValue(key, out list))
                            result[key] = list = new List<string>();
                        list.Add(value);
                    }
                }
            }
            ScriptUtils.TransformClassNameMap(result);
            return result;
        }

        private static void ParseBeginEndLine(string line, ref string key, ref string value)
        {
            string pattBegin = @"BEGIN<([\w_\s]+)>";
            string pattEnd = @"END<([\w_\s]+)>";
            Regex r;
            Match m;

            r = new Regex(pattBegin);
            m = r.Match(line);

            if (m.Success)
            {
                key = m.Groups[1].Value.Trim();
                value = null;
                return;
            }

            r = new Regex(pattEnd);
            m = r.Match(line);

            if (m.Success)
            {
                key = null;
                value = null;
                return;
            }

            // key = unchanged
            value = line.Trim();
        }

        internal static IDictionary<string, List<Tuple<string, string>>> ParseBeginEndFileWithRoomName(string fileName)
        {
            var generalParse = ParseBeginEndFile(fileName);
            var result = new Dictionary<string, List<Tuple<string, string>>>();

            foreach (var klPair in generalParse)
            {
                var newList = new List<Tuple<string, string>>();

                foreach (string val in klPair.Value)
                {
                    string name, roomName;

                    Utils.ParseNamePlaceName(val, out name, out roomName);
                    newList.Add(Tuple.Create(name, roomName));
                }
                result.Add(klPair.Key, newList);
            }
            return result;
        }

        // All items in list to lower case, if contains space or underscore delete them and add to the end of the list
        public static List<string> TransformClassNameList(List<string> list)
        {
            List<string> result = list.Select(s => s.ToLower()).ToList();
            HashSet<string> items = new HashSet<string>(result);

            // "Complicate" to maintain original order
            foreach (string s in result)
            {
                string ts = TransformClassName(s);
                if (!items.Contains(ts))
                {
                    items.Add(ts);
                }
            }
            foreach (string s in result)
            {
                items.Remove(s);
            }
            result.AddRange(items);
            return result;
        }

        public static string TransformClassName(string s)
        {
            return s.Replace(" ", "").Replace("_", "").ToLower();
        }

        public static void TransformClassNameMap<T>(Dictionary<string, T> map)
        {
            List<string> keys = new List<string>(map.Keys);

            foreach (string key in keys)
            {
                string tkey = TransformClassName(key);
                if (!map.ContainsKey(tkey))
                {
                    map[tkey] = map[key];
                }
            }
        }
    }

    public static class RandomUtils
    {

        public static int RandomNonUniform(float[] elements)
        {
            float[] bins = new float[elements.Length + 1];

            bins[0] = 0.0f;
            for (int i = 0; i < elements.Length; i++)
            {
                bins[i + 1] = elements[i] + bins[i];
            }
            float r = UnityEngine.Random.Range(0, bins[bins.Length - 1]);
            for (int i = 1; i < bins.Length; i++)
            {
                if (r < bins[i]) return i - 1;
            }
            return elements.Length - 1;
        }

        public static T Choose<T>(IList<T> l)
        {
            return l[UnityEngine.Random.Range(0, l.Count)];
        }

        // Randomly permute first n entries
        public static void PermuteHead<T>(List<T> ipl, int n)
        {
            int max = Mathf.Min(ipl.Count, n);

            for (int i = 0; i < max - 1; i++)
            {
                int j = UnityEngine.Random.Range(i, max);
                T tmp = ipl[i];

                ipl[i] = ipl[j];
                ipl[j] = tmp;
            }
        }

        public static void Permute<T>(List<T> ipl)
        {
            PermuteHead(ipl, ipl.Count);
        }

        public static List<T> RanomPermutation<T>(List<T> ipl)
        {
            var iplCopy = new List<T>(ipl);
            PermuteHead(iplCopy, iplCopy.Count);
            return iplCopy;
        }


    }


    public class ObjectPropertiesProvider
    {
        Dictionary<string, List<string>> namePropertiesMap;

        public ObjectPropertiesProvider(NameEquivalenceProvider nameEquivalenceProvider)
        {
            namePropertiesMap = Helper.getObjectProperties(nameEquivalenceProvider);
        }

        public List<string> PropertiesForClass(string className)
        {
            List<string> properties;

            if (namePropertiesMap.TryGetValue(className.ToLower(), out properties))
            {
                return properties;
            }
            else
            {
                return new List<string>();
            }
        }
    }

    public interface IObjectSelectorProvider
    {
        IObjectSelector GetSelector(string name, int instance);
    }

    public class InstanceSelectorProvider : IObjectSelectorProvider
    {
        private Dictionary<int, GameObject> idObjectMap;

        public InstanceSelectorProvider(EnvironmentGraph currentGraph)
        {
            idObjectMap = new Dictionary<int, GameObject>();

            foreach (EnvironmentObject eo in currentGraph.nodes)
            {
                if (eo.transform != null)
                {
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

    public class ObjectSelectionProvider : IObjectSelectorProvider
    {
        /*
         * 
         *   We want:
         *    f: name -> { game_object_name1, game_object_name2, ...,  game_obejct_name2 }; string -> set(lowercase strings)
         *    examples: food -> { aaa_banana_1212, banana1, minced_meat, ... } 
         *              banana -> { aaa_banana_1212, banana1 }
         *    
         *   We have: 
         *   
         *   PrefabClass.json
         *      dictionary: game_object_name -> class_name
         *      examples: fmgp_pre_bananas_512 -> bananas
         *                bananas_1 -> bananas
         *                
         *   objects2Unity.txt
         *      dicitionary: top_class_name -> [class_name1, class_name2, ...] (and forbidden class names [fclass_name1, fclass_name2, ...])
         *      example:     food -> bananas, meat, ...
         *                   fork -> fork
         *                   dog
         * 
         */

        private IDictionary<string, ISet<string>> classObjectNamesMap;
        IDictionary<string, string> objectNameClassMap;

        public ObjectSelectionProvider(NameEquivalenceProvider nameEquivProvider)
        {
            Initialize(nameEquivProvider);
        }

        public IObjectSelector GetSelector(string name, int instance)
        {
            ISet<string> names;

            if (classObjectNamesMap.TryGetValue(name.ToLower(), out names))
                return new ObjectNameSetSelector(names);
            else
                throw new Exception("Unknown object " + name);
            // return new ObjectNameSetSelector(new string[] { }); 
        }

        public string GetClassName(string prefabName)
        {
            string result;

            objectNameClassMap.TryGetValue(prefabName, out result);
            return result;
        }

        private void Initialize(NameEquivalenceProvider nameEquivProvider)
        {
            objectNameClassMap = new Dictionary<string, string>();

            IDictionary<string, PrefabClassEncoding> tmpObjectNameClassMap = Helper.GetClassGroups();

            foreach (var item in tmpObjectNameClassMap)
            {
                objectNameClassMap[item.Key] = item.Value.className.ToLower();
            }

            // reverse objectNameClassMap
            var classObjectNamesMapTmp = new Dictionary<string, ISet<string>>();

            foreach (var item in objectNameClassMap)
            {
                ISet<string> values;

                if (!classObjectNamesMapTmp.TryGetValue(item.Value, out values))
                {
                    values = new HashSet<string>();
                    classObjectNamesMapTmp[item.Value] = values;
                }
                values.Add(item.Key.ToLower());
            }

            classObjectNamesMap = new Dictionary<string, ISet<string>>();

            // create equivalence classes from classObjectNamesMap and equivNameMap
            foreach (var item in nameEquivProvider)
            {
                string catName = item.Key;
                ISet<string> objNames = new HashSet<string>();

                ISet<string> names;
                if (classObjectNamesMapTmp.TryGetValue(catName, out names))
                {
                    objNames.UnionWith(names);
                }
                foreach (string s in item.Value)
                {

                    if (classObjectNamesMapTmp.TryGetValue(s, out names))
                    {
                        objNames.UnionWith(names);
                    }
                }
                if (objNames.Count > 0)
                    classObjectNamesMap[catName] = objNames;
            }

            // join classObjectNamesMap and classObjectNamesMapTmp
            foreach (var item in classObjectNamesMapTmp)
            {
                if (!classObjectNamesMap.ContainsKey(item.Key))
                    classObjectNamesMap.Add(item);
            }
        }

    }

    public static class TimeMeasurement
    {
        // Total time for each key (measured piece of code)
        private static IDictionary<string, long> totalTime = new ConcurrentDictionary<string, long>();

        // Timestamp for each started measurement, key is measurement uid, value is pair of measured code name and start time
        private static IDictionary<string, Tuple<string, long>> measurementTime = new ConcurrentDictionary<string, Tuple<string, long>>();

        public static void ResetAll()
        {
            totalTime.Clear();
            measurementTime.Clear();
        }

        public static string Start(string name)
        {
            string mKey = Guid.NewGuid().ToString();
            measurementTime[mKey] = Tuple.Create(name, Stopwatch.GetTimestamp());
            return mKey;
        }

        public static void Stop(string mKey)
        {
            Tuple<string, long> mValue;
            long now = Stopwatch.GetTimestamp();

            if (measurementTime.TryGetValue(mKey, out mValue))
            {
                if (totalTime.ContainsKey(mValue.Item1))
                    totalTime[mValue.Item1] += now - mValue.Item2;
                else
                    totalTime[mValue.Item1] = now - mValue.Item2;
            }
        }

        public static string PrintResults()
        {
            StringBuilder sb = new StringBuilder();

            foreach (var item in totalTime)
            {
                sb.Append(string.Format("{0}: {1}\n", item.Key, item.Value / TimeSpan.TicksPerMillisecond));
            }
            return sb.ToString();
        }

    }

}
