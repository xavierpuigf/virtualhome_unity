using System.Collections;
using UnityEngine;

public class DisableBoolean : StateMachineBehaviour
{
	public string keyToDisable;
	override public void OnStateExit(Animator animator, AnimatorStateInfo animatorStateInfo, int layerIndex)
	{
		animator.SetBool(keyToDisable, false);
	}
}
