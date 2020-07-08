using UnityEngine;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using StoryGenerator.Scripts;
using StoryGenerator.HomeAnnotation;
using Newtonsoft.Json;

namespace StoryGenerator.Helpers
{
    public class Helper
    {
        public const string DATA_RESOURCES_FOLDER = "Data/";
        const string PATH_PREFIX = "Assets/Resources/";
        static Dictionary<string, PrefabClassEncoding> classDictionary = null;
        static Dictionary<string, List<string>> propertiesDictionary = null;
        static Dictionary<string, Annotation> annotationDictionary = null;



        public static T FindComponentInChildWithTag<T>(GameObject parent, string tag) where T:Component
        {
            Transform t = parent.transform;
            foreach(Transform tr in t)
            {
                if(tr.tag == tag)
                {
                    return tr.GetComponent<T>();
                }
            }
            return null;
        }

        // Algorithm 3.4.2S of Knuth's book Seminumeric Algorithms.
        public static int[] SampleRemove(int populationSize, int sampleSize)
        {
            int[] samples = new int[sampleSize];
            int t = 0; // total input records dealt with
            int m = 0; // number of items selected so far
            while (m < sampleSize)
            {
                float u = Random.value; // call a uniform(0,1) random number generator
                if ( (populationSize - t) * u >= sampleSize - m )
                {
                    t++;
                }
                else
                {
                    samples[m] = t;
                    t++; m++;
                }
            }
            return samples;
        }

        public static int[] SampleRandomOrder(int sampleSize)
        {
            int[] samples = new int[sampleSize];
            for (int i = 0; i < sampleSize; i++)
            {
                samples[i] = i;
            }
            Shuffle(samples);
            return samples;
        }

        public static void Shuffle (int[] deck)
        {
            for (int i = 0; i < deck.Length; i++)
            {
                int temp = deck[i];
                int randomIndex = Random.Range(0, deck.Length);
                deck[i] = deck[randomIndex];
                deck[randomIndex] = temp;
            }
        }

        // Obtained from:
        // https://forum.unity3d.com/threads/random-numbers-with-a-weighted-chance.442190/
        // Slightly modified
        public static int GetRandomWeightedIndex(List<float> weights)
        {
            if (weights == null || weights.Count == 0)
            {
                return -1;
            }

            float weightTotal = 0.0f;
            for (int i = 0; i < weights.Count; i++)
            {
                float w = weights[i];
                if (float.IsPositiveInfinity(w))
                {
                    return i;
                }
                else if (w >= 0.0f && !float.IsNaN(w))
                {
                    weightTotal += w;
                }
            }

            float r = Random.value;
            float s = 0.0f;

            for (int i = 0; i < weights.Count; i++)
            {
                float w = weights[i];
                if (float.IsNaN(w) || w <= 0f)
                {
                    continue;
                }

                s += w / weightTotal;
                if (s >= r)
                {
                    return i;
                }
            }

            return -1;
        }
            
        public static string RemoveFileExt(string fName)
        {
            return fName.Substring (0, fName.IndexOf ('.'));
        }

        public static GameObject GetRandomPrefabIn(string path)
        {
            DirectoryInfo di = new DirectoryInfo(PATH_PREFIX + path);
            FileInfo[] arry_fi_prefab = di.GetFiles("*.prefab");
            DirectoryInfo[] arry_di_prefab = di.GetDirectories();

            int prefab_idx = Random.Range(0, arry_fi_prefab.Length + arry_di_prefab.Length);
            string prefab_path;
            if (prefab_idx >= 0 && prefab_idx < arry_fi_prefab.Length)
            {
                prefab_path = path + RemoveFileExt(arry_fi_prefab[prefab_idx].Name);
            }
            else
            {
                int prefab_idx_adjusted = prefab_idx - arry_fi_prefab.Length;
                string path2 = PATH_PREFIX + path + arry_di_prefab[prefab_idx_adjusted].Name;
                FileInfo[] arry_fi_prefab2 = new DirectoryInfo(path2).GetFiles("*.prefab");
                int prefab_idx2 = Random.Range(0, arry_fi_prefab2.Length);
                prefab_path = path + arry_di_prefab[prefab_idx_adjusted].Name + "/" + RemoveFileExt(arry_fi_prefab2[prefab_idx2].Name);
            }
            return Resources.Load(prefab_path) as GameObject;
        }

        public static GameObject GetPrefabWithIdx(string path, int idx)
        {
            FileInfo[] arry_fi_prefab = new DirectoryInfo(PATH_PREFIX + path).GetFiles("*.prefab");
            string prefab_path = path + RemoveFileExt(arry_fi_prefab[idx].Name);
            return Resources.Load(prefab_path) as GameObject;
        }

        public static bool IsFirstV3GtrThanSecondV3(Vector3 v1, Vector3 v2)
        {
            if (v1.x > v2.x && v1.y > v2.y && v1.z > v2.z)
            {
                return true;
            }
            return false;
        }

        public static void AdjustRotationVector(ref Vector3 val)
        {
            if (val.x > 180.0f)
            {
                val.x -= 360.0f;
            }
            if (val.y > 180.0f)
            {
                val.y -= 360.0f;
            }
            if (val.z > 180.0f)
            {
                val.z -= 360.0f;
            }
        }

        public static List<Material> FindMatNameMatch(List<GameObject> list_go, string[] matNames)
        {
            List<Material> list_mats = new List<Material> ();
            foreach (GameObject go in list_go)
            {
                Renderer[] rdrs = go.GetComponentsInChildren<Renderer> ();
                foreach (Renderer r in rdrs)
                {
                    foreach (Material m in r.materials)
                    {
                        foreach (string mName in matNames)
                        {
                            if ( m.name.Contains(mName) )
                            {
                                list_mats.Add(m);
                            }
                        }
                    }
                }
            }

            return list_mats;
        }

        public static void GetInstanceGroups(out Dictionary<string, bool> dic_prefab,
            out Dictionary<string, int> dic_go)
        {
            dic_prefab = new Dictionary<string, bool> ();
            dic_go = new Dictionary<string, int>();
            TextAsset txtAsset = Resources.Load<TextAsset>(DATA_RESOURCES_FOLDER + "InstanceGroupList");
            InstanceGroupHolder igh = JsonUtility.FromJson<InstanceGroupHolder>(txtAsset.text);

            foreach (PrefabGroup pg in igh.prefabGroups)
            {
                dic_prefab[pg.prefab_name] = true;
            }

            for (int i = 0; i < igh.gameObjectGroups.Length; i++)
            {
                GameObjectGroup gog = igh.gameObjectGroups[i];
                dic_go[gog.group_name] = i;

                if (gog.alias != null)
                {
                    for (int j = 0; j < gog.alias.Length; j++)
                    {
                        dic_go[gog.alias[j]] = i;
                    }
                }
            }
        }

        public static Dictionary<string, PrefabClassEncoding> GetClassGroups()
        {
            if (classDictionary != null)
            {  
                return classDictionary;
            }

            classDictionary = new Dictionary<string, PrefabClassEncoding>();
            TextAsset txtAsset = Resources.Load<TextAsset>(DATA_RESOURCES_FOLDER + "PrefabClass");
            PrefabClassHolder pch = JsonUtility.FromJson<PrefabClassHolder>(txtAsset.text);

            for (int i = 0; i < pch.prefabClasses.Length; ++i)
            {
                PrefabClass pc = pch.prefabClasses[i];
                PrefabClassEncoding pce = new PrefabClassEncoding(ScriptUtils.TransformClassName(pc.className), i);

                foreach (string pName in pc.prefabs)
                {
                    classDictionary[pName] = pce;
                }
            }
            return classDictionary;
        }

        // for each class name (lowercase) a list of possible properties (uppercase)
        public static Dictionary<string, List<string>> getObjectProperties(NameEquivalenceProvider nameEquivalenceProvider)
        {
            if (propertiesDictionary != null) {
                return propertiesDictionary;
            }
            propertiesDictionary = new Dictionary<string, List<string>>();

            TextAsset txtAsset = Resources.Load<TextAsset>(DATA_RESOURCES_FOLDER + "properties_data");
            Dictionary<string, List<string>> propMap = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, List<string>>>(txtAsset.text);

            foreach (var e in propMap)
            {
                List<string> properties = e.Value.Select(s => s.ToUpper()).ToList();
                string className = e.Key.ToLower();

                propertiesDictionary[className] = properties;

                ISet<string> synonyms = nameEquivalenceProvider.GetSynonyms(className);
                foreach (string eqClassName in synonyms)
                {
                    propertiesDictionary[eqClassName.ToLower()] = properties;
                }

                string classNameReduced = e.Key.Replace(" ", "").ToLower();

                if (classNameReduced != className)
                {
                    propertiesDictionary[classNameReduced] = properties;

                    synonyms = nameEquivalenceProvider.GetSynonyms(classNameReduced);
                    foreach (string eqClassName in synonyms)
                    {
                        propertiesDictionary[eqClassName.ToLower()] = properties;
                    }
                }

            }
            return propertiesDictionary;
        }


        public static Dictionary<string, Annotation> GetAnnotations()
        {
            if (annotationDictionary == null)
            {
                TextAsset txtAsset = Resources.Load<TextAsset>(DATA_RESOURCES_FOLDER + "object_annotation");

                // AnnotationRoot ar = JsonUtility.FromJson<AnnotationRoot> (txtAsset.text);
                JsonSerializerSettings settings = new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore,
                    MissingMemberHandling = MissingMemberHandling.Ignore
                };
                AnnotationRoot ar = JsonConvert.DeserializeObject<AnnotationRoot>(txtAsset.text, settings);

                annotationDictionary = new Dictionary<string, Annotation>();
                foreach (AnnotationContainer ac in ar.models)
                {
                    foreach (string model_name in ac.model_names)
                    {
                        annotationDictionary[model_name] = ac.annotation;
                    }
                }

            }

            return annotationDictionary;
        }


        public static List<string> GetAllClasses()
        {
            List<string> allClasses = new List<string>();
            TextAsset txtAsset = Resources.Load<TextAsset>(DATA_RESOURCES_FOLDER + "PrefabClass.json");
            PrefabClassHolder pch = JsonUtility.FromJson<PrefabClassHolder>(txtAsset.text);

            foreach (PrefabClass pc in pch.prefabClasses)
            {
                allClasses.Add(ScriptUtils.TransformClassName(pc.className));
            }
            return allClasses;
        }

    }

    // http://answers.unity3d.com/questions/554729/best-practice-extending-gameobject.html
    public static class ComponentExtension
    {
        // http://answers.unity3d.com/questions/530178/how-to-get-a-component-from-an-object-and-add-it-t.html
        public static T GetCopyOf<T>(this Component comp, T other) where T : Component
        {
            System.Type type = comp.GetType();
            if (type != other.GetType()) return null; // type mis-match
            BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Default | BindingFlags.DeclaredOnly;
            PropertyInfo[] pinfos = type.GetProperties(flags);
            foreach (var pinfo in pinfos)
            {
                if (pinfo.CanWrite)
                {
                    try
                    {
                        pinfo.SetValue(comp, pinfo.GetValue(other, null), null);
                    }
                    catch { } // In case of NotImplementedException being thrown. For some reason specifying that exception didn't seem to catch it, so I didn't catch anything specific.
                }
            }

            FieldInfo[] finfos = type.GetFields(flags);
            foreach (var finfo in finfos)
            {
                finfo.SetValue(comp, finfo.GetValue(other));
            }
            return comp as T;
        }
        // Usage: var copy = myComp.GetCopyOf(someOtherComponent)
    }

    public static class GameObjectExtension
    {
        public static T AddComponent<T>(this GameObject go, T toAdd) where T : Component
        {
            return go.AddComponent<T>().GetCopyOf(toAdd) as T;
        }

//         Usage: Health myHealth = gameObject.AddComponent<Health>(enemy.health)
    }

    // To obtain GameObject's prefab class and color encoding from it's name
    // Dictionary will map name to this class
    public class PrefabClassEncoding
    {
        public string className;
        public Color32 encodedColor;

        const int BINS_PER_CHANNEL = 9;
        const int CHANNEL_GAP = 255 / (BINS_PER_CHANNEL - 1);

        public PrefabClassEncoding(string name, int idx)
        {
            className = name;
            encodedColor = EncodeColor32(idx);
        }

        public static Color32 EncodeColor32(int idx)
        {
            byte R = (byte) ( ((idx / (BINS_PER_CHANNEL * BINS_PER_CHANNEL)) % BINS_PER_CHANNEL) * CHANNEL_GAP );
            byte G = (byte) ( ((idx / BINS_PER_CHANNEL) % BINS_PER_CHANNEL) * CHANNEL_GAP );
            byte B = (byte) ( (idx % BINS_PER_CHANNEL) * CHANNEL_GAP );

            // Debug.Log(idx + "->" + R + ", " + G + ", " + B);
            return new Color32(R, G, B, 255);
        }
    }


    // Classes for JSON file parsing
    // This is for group instances of the prefabs
    [System.Serializable]
    public class InstanceGroupHolder
    {
        public PrefabGroup[] prefabGroups;
        public GameObjectGroup[] gameObjectGroups;
    }

    // This is for specifying prefab class
    [System.Serializable]
    public class PrefabClassHolder
    {
        public PrefabClass[] prefabClasses;
    }

    [System.Serializable]
    public class PrefabClass
    {
        public string className;
        public string[] prefabs;
    }    

    [System.Serializable]
    public class PrefabGroup
    {
        public string prefab_name;
    }

    [System.Serializable]
    public class GameObjectGroup
    {
        public string group_name;
        // More than one name can refer to the group
        public string[] alias;
    }

    [System.Serializable]
    public class PropertiesDataHolder
    {
        public string[] objects;
        public string[] properties;
        public int[,] property_matrix;
        public string[] rooms;
    }

}
