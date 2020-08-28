using RootMotion.FinalIK;
using System.Collections;
using UnityEngine;

public class BlendHandRelativePos : StateMachineBehaviour
{
    IKEffector m_ike_handLeft;
    IKEffector m_ike_handRight;

    float m_initVal;
    float m_delta;

    const string ANIM_STR_MRP_WEIGHT = "MRPWeight";

    override public void OnStateEnter(Animator animator, AnimatorStateInfo animatorStateInfo, int layerIndex)
	{
        IKSolverFullBodyBiped slvr = animator.gameObject.GetComponent<FullBodyBipedIK> ().solver;

        m_ike_handLeft = slvr.GetEffector(FullBodyBipedEffector.LeftHand);
        m_ike_handRight = slvr.GetEffector(FullBodyBipedEffector.RightHand);

        // Left and the right hand has the same weights so quering one is sufficient
        m_initVal = m_ike_handLeft.maintainRelativePositionWeight;
		m_delta = 1 - m_initVal;
	}

    override public void OnStateUpdate(Animator animator, AnimatorStateInfo animatorStateInfo, int layerIndex)
    {
        float newWeight = m_initVal + m_delta * animator.GetFloat(ANIM_STR_MRP_WEIGHT);
        m_ike_handLeft.maintainRelativePositionWeight = newWeight;
        m_ike_handRight.maintainRelativePositionWeight = newWeight;
    }

    override public void OnStateExit(Animator animator, AnimatorStateInfo animatorStateInfo, int layerIndex)
	{
        m_ike_handLeft.maintainRelativePositionWeight = m_initVal;
        m_ike_handRight.maintainRelativePositionWeight = m_initVal;
	}
}
