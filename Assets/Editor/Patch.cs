using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;

public class Patch : Editor
{

    [MenuItem("Patch/CHIP")]
    static void PatchCHIP()
    {
        string path_resources = CreateOutDir("Assets", new List<string> () {"Resources"});
        const string CHIP_PATH_JSON = "Assets/Editor/CHIP_patch.json";
        
        // Change the emissive material intensity
        Material lamp_mat = (Material)AssetDatabase.LoadAssetAtPath( "Assets/Resources/Complete_Home_Interior_Pack/CHIP_Materials/MAT_Furniture/MAT_FUR_White_lamp.mat", typeof(Material) );
        lamp_mat.SetColor( "_EmissionColor", new Color(4.5f, 4.5f, 4.5f, 0.0f) );
        
        PrefabDirs pd = PrefabDirs.CreateFromJson(CHIP_PATH_JSON);
        pd.Process(path_resources);
    }

    [MenuItem("Patch/MHIP")]
    static void PatchMHIP()
    {
        string path_resources = CreateOutDir("Assets", new List<string> () {"Resources"});
        const string MHIP_PATH_JSON = "Assets/Editor/MHIP_patch.json";

        PrefabDirs pd = PrefabDirs.CreateFromJson(MHIP_PATH_JSON);
        pd.Process(path_resources);
    }

    static string CreateOutDir(string p_base, IEnumerable<string> folders_out)
    {
        string p = p_base;
        foreach (var folder in folders_out)
        {
            // Check if this folder already exists
            string p_check = Path.Combine(p, folder);
            // Proceed only if the folder doesn't exists
            if ( AssetDatabase.IsValidFolder(p_check) )
            {
                p = p_check;
            }
            else
            {
                string guid_folder = AssetDatabase.CreateFolder(p, folder);
                p = AssetDatabase.GUIDToAssetPath(guid_folder);
                Debug.Log("Created direcotry:" + p);
            }
        }
        return p;
    }

    static string AssembleAssetPath(string p_base, string pf_name, string ext)
    {
        return $"{p_base}/{pf_name}{ext}";
    }

    static Vector3 V3fromArry(float[] arry)
    {
        return new Vector3(arry[0], arry[1], arry[2]);
    }

    [System.Serializable]
    public class PrefabDirs
    {
        public Dir[] prefab_dirs = null;
        public string package_old_path = null;
        public string package_new_path = null;

        public void Process(string path_resources)
        {
            if (prefab_dirs != null)
            {            
                foreach (Dir d in prefab_dirs)
                {
                    d.Process(path_resources);
                }
            }

            AssetDatabase.MoveAsset(package_old_path, package_new_path);
        }

        public static PrefabDirs CreateFromJson(string path_json)
        {
            PrefabDirs pd = null;
            try
            {
                using ( StreamReader sr = new StreamReader(path_json) )
                {
                    string str_file = sr.ReadToEnd();
                    pd = JsonUtility.FromJson<PrefabDirs> (str_file);
                }
            }
            catch (System.Exception e)
            {
                Debug.Log(e.Message);
            }

            return pd;
        }
    }

    [System.Serializable]
    public class Dir
    {
        public string[] path_out = null;
        public DirList[] extra_dirs = null;
        public string path_in;

        // Transform relative to the parent which will be created.
        public float[] relative_pos = null;
        public float[] relative_rot = null;

        // Material replacement options.
        public string path_base_mat = null;
        public string mat_variation_tsfm = null;
        public int mat_change_idx = -1;
        public string mat_change_contains = null;

        public bool keep = false;

        public Prefab[] prefabs = null;
        public Model[] models = null;

        public void Process(string p_resources)
        {
            // If path_out isn't given, it's just renaming the prefab.
            if (path_out == null)
            {
                RenamePrefabs();
                return;
            }
            string p_base_out = CreateDir(p_resources);

            ProcessPrefabs(p_base_out);
            ProcessModels(p_base_out);
        }

        void RenamePrefabs()
        {
            foreach (Prefab pf in prefabs)
            {
                if (pf.names_out.Length == 1)
                {
                    string p_pf_in = AssembleAssetPath(path_in, pf.name, ".prefab");
                    string msg = AssetDatabase.RenameAsset(p_pf_in, $"{pf.names_out[0]}.prefab");
                    if (msg.Length > 0)
                    {
                        Debug.LogError(msg);
                    }
                }
                else
                {
                    Debug.LogError($"The should be only one names_out for {pf.name}");
                }
            }
        }

        string CreateDir(string p_resources)
        {
            string p_base_pf_out = CreateOutDir(p_resources, path_out);

            if (extra_dirs != null)
            {
                for (int i = 0; i < extra_dirs.Length; i++)
                {
                    extra_dirs[i].Create(p_base_pf_out);
                }
            }

            return p_base_pf_out;
        }

        void ProcessPrefabs(string p_base_pf_out)
        {
            if (prefabs != null)
            {
                foreach (Prefab pf in prefabs)
                {
                    if (pf.IsValid())
                    {
                        // Find the input prefab
                        string p_pf_in = AssembleAssetPath(path_in, pf.name, ".prefab");
                        Object obj_pf = AssetDatabase.LoadAssetAtPath(p_pf_in, typeof(Object) );
                        if (obj_pf == null)
                        {
                            Debug.LogError("Cannot load " + p_pf_in);
                            continue;
                        }

                        // Create variations only if specified.
                        if (pf.names_out != null)
                        {
                            for (int i = 0; i < pf.names_out.Length; i++)
                            {
                                // check the existance of the prefab name
                                string p_pf_out = pf.PrefabPathOut(p_base_pf_out, i);
                                Object obj_tst = AssetDatabase.LoadAssetAtPath( p_pf_out, typeof(Object) );
                                if (obj_tst == null)
                                {
                                    pf.CreateDir(p_base_pf_out);
                                    bool success;
                                    GameObject go = pf.CreateMaterialVariation(obj_pf, this, i, out success);
                                    pf.AdjustTsfm(go);
                                    
                                    // If failed to find the material, skip to the next.
                                    if (success)
                                    {
                                        CreateParent(pf, ref go);
                                        PrefabUtility.SaveAsPrefabAsset(go, p_pf_out, out success);

                                        if (! success)
                                        {
                                            Debug.LogError("Failed to create " + p_pf_out);
                                        }
                                    }

                                    DestroyImmediate(go);
                                }
                                else
                                {
                                    Debug.LogWarning(p_pf_out + " already exists. Skipping creation.");
                                }
                            }
                        }

                        // We are done with this prefab so remove it.
                        if (!keep && !pf.keep)
                        {
                          AssetDatabase.MoveAssetToTrash(p_pf_in);
                        }
                    }
                }
            }
        }

        void ProcessModels(string p_base_m_out)
        {
            if (models != null)
            {
                foreach (Model m in models)
                {
                    // Find the input model
                    string p_mdl_in = AssembleAssetPath(path_in, m.name, m.ext);
                    Object obj_pf = AssetDatabase.LoadAssetAtPath(p_mdl_in, typeof(Object) );
                    if (obj_pf == null)
                    {
                        Debug.LogError("Cannot load " + p_mdl_in);
                        continue;
                    }

                    // check the existance of the output prefab name
                    string p_mdl_out = AssembleAssetPath(p_base_m_out, m.name_out, ".prefab");
                    Object obj_tst = AssetDatabase.LoadAssetAtPath( p_mdl_out, typeof(Object) );
                    if (obj_tst == null)
                    {
                        bool success;
                        GameObject go = m.CreateMaterialVariation(obj_pf, out success);
                        
                        // If failed to find the material, skip to the next.
                        if (success)
                        {
                            PrefabUtility.SaveAsPrefabAsset(go, p_mdl_out, out success);

                            if (! success)
                            {
                                Debug.LogError("Failed to create " + p_mdl_out);
                            }
                        }

                        DestroyImmediate(go);
                    }
                    else
                    {
                        Debug.LogWarning(p_mdl_out + " already exists. Skipping creation.");
                    }
                }
            }
        }

        void CreateParent(Prefab pf, ref GameObject go)
        {
            float[] relative_pos_use = null, relative_rot_use = null;
            if (pf.relative_pos != null)
            {
                relative_pos_use = pf.relative_pos;
            }
            else if (relative_pos != null)
            {
                relative_pos_use = relative_pos;
            }
            if (pf.relative_rot != null)
            {
                relative_rot_use = pf.relative_rot;
            }
            else if (relative_rot != null)
            {
                relative_rot_use = relative_rot;
            }

            if (relative_pos_use != null || relative_rot_use != null)
            {
                GameObject go_parent = new GameObject();

                // Remove the "(Clone)" suffix of the GameObject
                go.name = go.name.Substring(0, go.name.Length - 7);

                Transform tsfm = go.transform;
                tsfm.SetParent(go_parent.transform);

                if (relative_pos_use != null)
                {
                    tsfm.localPosition = V3fromArry(relative_pos_use);
                }
                if (relative_rot_use != null)
                {
                    tsfm.localRotation = Quaternion.Euler(relative_rot_use[0], relative_rot_use[1], relative_rot_use[2]);
                }
                // Overwrite go variable with the parent.
                go = go_parent;
            }
        }
    }

    [System.Serializable]
    public class Prefab
    {
        public string name;
        public string[] dir_create = null;

        public float[] relative_pos = null;
        public float[] relative_rot = null;
        public float[] scale = null;

        public string[] names_out = null;
        public string[] mats = null;
        public string[] mats_full_path = null;
        public string mat_variation_tsfm = null;
        public int mat_change_idx = -1;
        public string mat_change_contains = null;

        public bool keep = false;

        public bool IsValid()
        {
            if (names_out != null && mats != null)
            {
                if (names_out.Length != mats.Length)
                {
                    Debug.LogError(name + ":\nThe number of new names and the material variance doesn't match.");
                    return false;
                }
                if ( Duplicates(names_out) || Duplicates(mats) )
                {
                    return false;
                }
            }
            return true;
        }

        public string PrefabPathOut(string p_base, int idx)
        {
            if (dir_create != null)
            {
                string p_dir_create = dir_create[0];
                for (int i = 1; i < dir_create.Length; i++)
                {
                    p_dir_create = $"{p_dir_create}/{dir_create[i]}";
                }
                p_base = $"{p_base}/{p_dir_create}";
            }
            return AssembleAssetPath(p_base, names_out[idx], ".prefab");
        }

        public void CreateDir(string path_base)
        {
            if (dir_create != null)
            {
                CreateOutDir(path_base, dir_create);
            }
        }

        public GameObject CreateMaterialVariation(Object pf, Dir dir, int idx, out bool success)
        {
            GameObject go = Instantiate(pf, Vector3.zero, Quaternion.identity) as GameObject;

            if ( ShouldChangeMat(idx) )
            {
                string path_mat = GetMaterialPath(dir, idx);
                Material new_mat = (Material)AssetDatabase.LoadAssetAtPath( path_mat, typeof(Material) );
                if (new_mat == null)
                {
                    Debug.LogError("Cannot find material: " + path_mat);
                    success = false;
                    return go;
                }

                SwapMaterial( GetRenderer(go, dir), dir, new_mat );
            }
            success = true;
            return go;
        }

        public void AdjustTsfm(GameObject go)
        {
            if (scale != null)
            {
                go.transform.localScale = V3fromArry(scale);
            }
        }

        bool Duplicates(IEnumerable<string> strs)
        {
            HashSet<string> hash_strs = new HashSet<string> ();
            foreach (var s in strs)
            {
                // Returns false if the element is already present
                if (! hash_strs.Add(s) )
                {
                    return true;
                }
            }
            return false;
        }

        bool ShouldChangeMat(int idx)
        {
            if (mats_full_path != null)
            {
                return (mats_full_path[idx].Length > 0);
            }
            if (mats != null)
            {
                return (mats[idx].Length > 0);
            }
            return false;
        }

        string GetMaterialPath(Dir dir, int idx)
        {
            const string MAT_EXT = ".mat";

            if (mats_full_path != null)
            {
                return (mats_full_path[idx] + MAT_EXT);
            }
            return AssembleAssetPath(dir.path_base_mat, mats[idx], MAT_EXT);
        }

        Renderer GetRenderer(GameObject go, Dir dir)
        {
            Renderer rdr;

            if (mat_variation_tsfm != null)
            {
                Transform tsfm_child = go.transform.Find(mat_variation_tsfm);
                rdr = tsfm_child.GetComponent<Renderer> ();
            }
            else if (dir.mat_variation_tsfm != null)
            {
                Transform tsfm_child = go.transform.Find(dir.mat_variation_tsfm);
                rdr = tsfm_child.GetComponent<Renderer> ();
            }
            else
            {
                rdr = go.GetComponent<Renderer> ();
            }
            return rdr;
        }

        void SwapMaterial(Renderer rdr, Dir dir, Material new_mat)
        {
            Material[] mats_rdr = rdr.sharedMaterials;
            if (mat_change_idx != -1)
            {
                mats_rdr[mat_change_idx] = new_mat;
            }
            else if (dir.mat_change_idx != -1)
            {
                mats_rdr[dir.mat_change_idx] = new_mat;
            }
            else if (mat_change_contains != null || dir.mat_change_contains != null)
            {
                string contain_str = null;
                if (mat_change_contains != null)
                {
                    contain_str = mat_change_contains;
                }
                else
                {
                    contain_str = dir.mat_change_contains;
                }
                for (int i = 0; i < mats_rdr.Length; i++)
                {
                    if ( mats_rdr[i].name.Contains(contain_str) )
                    {
                        mats_rdr[i] = new_mat;
                        break;
                    }
                }
            }
            else // If nothing is specified, always replace the first material.
            {
                mats_rdr[0] = new_mat;
            }
            rdr.sharedMaterials = mats_rdr;
        }
    }

    [System.Serializable]
    public class DirList
    {
        public string[] path;

        public void Create(string path_base)
        {
            CreateOutDir(path_base, path);
        }
    }

    [System.Serializable]
    public class Model
    {
        public string name;
        public string ext;
        public string mat_full_path;

        public string name_out;

        public GameObject CreateMaterialVariation(Object pf, out bool success)
        {
            GameObject go = Instantiate(pf, Vector3.zero, Quaternion.identity) as GameObject;

            string path_mat = mat_full_path + ".mat";

            Material new_mat = (Material)AssetDatabase.LoadAssetAtPath( path_mat, typeof(Material) );
            if (new_mat == null)
            {
                Debug.LogError("Cannot find material: " + path_mat);
                success = false;
                return go;
            }

            Renderer rdr = go.GetComponent<Renderer> ();
            Material[] mats_rdr = rdr.sharedMaterials;
            mats_rdr[0] = new_mat;
            rdr.sharedMaterials = mats_rdr;

            success = true;
            return go;
        }
    }
    
}

