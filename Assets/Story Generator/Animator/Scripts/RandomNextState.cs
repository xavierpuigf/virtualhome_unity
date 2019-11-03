using UnityEngine;
using System.Collections;

public class RandomNextState : StateMachineBehaviour
{
	public string parameter;
	public int numberOfStates;
	 // OnStateEnter is called when a transition starts and the state machine starts to evaluate this state
	override public void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
	{
		animator.SetInteger( parameter, Random.Range(0, numberOfStates) );
	}
}
