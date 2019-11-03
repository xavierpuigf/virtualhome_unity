using RootMotion.FinalIK;
using StoryGenerator;
using StoryGenerator.ChairProperties;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RunSit : MonoBehaviour
{
  public GameObject chr;
  public GameObject chair;
  public float timeOut = 5.0f;

  // Uncomment below to use Character control code
  void Start ()
  {
    StartCoroutine( SitAndStand() );
  }

  IEnumerator SitAndStand()
  {
    CharacterControl cc = chr.GetComponent<CharacterControl>();
    Properties_chair pc = chair.GetComponent<Properties_chair> ();
    List<Properties_chair.SittableUnit> list_su = pc.GetSittableUnits();
    while (true)
    {
      Properties_chair.SittableUnit su = list_su[ Random.Range(0, list_su.Count) ];

      yield return StartCoroutine( cc.walkOrRunTo(true, su.GetTsfm_positionInteraction().position) );
      yield return StartCoroutine( cc.Sit(chair, su.GetTsfm_lookAtBody().gameObject, su.tsfm_group.gameObject) );
      yield return new WaitForSeconds(timeOut);
      yield return StartCoroutine( cc.Stand() );
    }
  }
}
