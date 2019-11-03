using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using StoryGenerator;

public class SimpleAction : MonoBehaviour
{
	string ANIM_PARAM_SIT = "Sit";	
	Animator anim;	
	CharacterControl cc;

	void Start ()
	{
		anim = GetComponent<Animator> ();
		cc = GetComponent<CharacterControl> ();
	}
	
	void OnGUI()
	{
		if ( GUILayout.Button("Sit") )
        {
			anim.SetBool(ANIM_PARAM_SIT, true);
		}
		if ( GUILayout.Button("Stand") )
        {
			anim.SetBool(ANIM_PARAM_SIT, false);
		}
		if ( GUILayout.Button("Drink Left") )
        {
			StartCoroutine( cc.DrinkLeft() );
		}
		if ( GUILayout.Button("Drink Right") )
        {
			StartCoroutine( cc.DrinkRight() );
		}
		if ( GUILayout.Button("TextLeft") )
		{
			StartCoroutine( cc.TextLeft() );
		}
		if ( GUILayout.Button("TextRight") )
		{
			StartCoroutine( cc.TextRight() );
		}
		if ( GUILayout.Button("TalkLeft") )
		{
			StartCoroutine( cc.TalkLeft() );
		}
		if ( GUILayout.Button("TalkRight") )
		{
			StartCoroutine( cc.TalkRight() );
		}
	}
}
