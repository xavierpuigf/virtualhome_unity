using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SMPLXBenchmark : MonoBehaviour
{
    public GameObject target;
    public Text bodiesText;
    public Text statusText;

    private List<SkinnedMeshRenderer> _smrList;
    private bool _animate = false;
    private int _numShapes = 0;
    private int _numBodies = 1;

    // Start is called before the first frame update
    void Start()
    {
        _smrList = new List<SkinnedMeshRenderer>();
        _smrList.Add(target.GetComponentInChildren<SkinnedMeshRenderer>());
        if (statusText != null)
            statusText.text = "Active Blendshapes: 0";

    }

    void ResetBlendshapes()
    {
        foreach (SkinnedMeshRenderer smr in _smrList)
        {
            int blendShapeCount = smr.sharedMesh.blendShapeCount;
            for (int i=0; i<blendShapeCount; i++)
                smr.SetBlendShapeWeight(i, 0.0f);
        }
    }

    int GetActiveBlendshapes()
    {
        int activeBlendshapes = 0;
        foreach (SkinnedMeshRenderer smr in _smrList)
        {
            int blendShapeCount = smr.sharedMesh.blendShapeCount;
            for (int i=0; i<blendShapeCount; i++)
            {
                if (smr.GetBlendShapeWeight(i) != 0.0f)
                    activeBlendshapes++;
            }
        }
        return activeBlendshapes;
        
    }

    void SetWeights(float value)
    {
        foreach (SkinnedMeshRenderer smr in _smrList)
        {
            for (int i=0; i<_numShapes; i++)
                smr.SetBlendShapeWeight(i, value);
        }
        UpdateStatus();
    }

    void UpdateStatus()
    {
        if (statusText != null)
            statusText.text = string.Format("Active Blendshapes: {0}", GetActiveBlendshapes());
    }

    // Update is called once per frame
    void Update()
    {
        if (_animate)
        {
            foreach (SkinnedMeshRenderer smr in _smrList)
            {
                for (int i=0; i<_numShapes; i++)
                {
                    float value = Mathf.Sin(2*Mathf.PI * Time.time)*50 + 100.0f;

                    smr.SetBlendShapeWeight(i, value);
                }
            }
        }

        if (Input.GetKeyDown(KeyCode.Escape))
            Application.Quit();

        if (Input.GetKeyDown(KeyCode.Alpha0))
        {
            _animate = false;
            ResetBlendshapes();
            UpdateStatus();
        }
        else if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            ResetBlendshapes();
            _numShapes = 10;
            SetWeights(200.0f);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            ResetBlendshapes();
            _numShapes = 20;
            SetWeights(200.0f);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            ResetBlendshapes();
            _numShapes = 506;
            SetWeights(200.0f);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha4))
        {
            _animate = !_animate;
        }
        else if (Input.GetKeyDown(KeyCode.Alpha5))
        {
            int newBodies = _numBodies;
            for (int i=0; i<newBodies; i++)
            {
                _numBodies++;
                float x = 2.5f * Mathf.Sin(2*Mathf.PI*(_numBodies)/20.0f);
                float y = target.transform.position.y;
                float z = _numBodies * 0.25f;
                Vector3 pos = new Vector3(x, y, z);
                GameObject go = Instantiate(target, pos, Quaternion.Euler(0.0f, 180.0f, 0.0f)) as GameObject;
                _smrList.Add(go.GetComponentInChildren<SkinnedMeshRenderer>());
            }

            if (bodiesText != null)
                bodiesText.text = string.Format("Bodies: {0}", _numBodies);

            UpdateStatus();
        }        
    }
}
