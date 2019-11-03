using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace StoryGenerator.DoorProperties
{
	
	// Disables isKinematic flag of door's Rigidbody once it enters closing rotation.
	public class DoorStopper : MonoBehaviour
	{
		void OnCollisionEnter(Collision c)
		{
			// This method can be called when:
			// 1. Door is getting close from opened position
			// 2. Door is is about to be opened. Trigged by setting "isKinematic" = false
			// In order to differentiate case 1 and 2, we are going to use rigidbody's angular velocity
			if (c.rigidbody.angularVelocity != Vector3.zero)
			{
				c.rigidbody.isKinematic = true;
			}
		}
	}

}