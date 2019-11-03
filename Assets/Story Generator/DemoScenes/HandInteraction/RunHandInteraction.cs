using UnityEngine;
using System.Collections;
using StoryGenerator.ChairProperties;
using StoryGenerator.CharInteraction;
using RootMotion.FinalIK;
using System.Collections.Generic;

public class RunHandInteraction : MonoBehaviour
{
    public GameObject[] chars;
    public GameObject[] objs;
    public Transform putBackLocation;

    List<InteractionSystem> list_is;
    List<HandInteraction> list_hi;

    const int IDX_COFFEE = 0;
    const int IDX_COMPUTER = 1;
    const int IDX_TOAST = 2;
    const int IDX_STOVE = 3;
    const int IDX_COUNTER = 4;
    const int IDX_HAIRPRODUCT = 5;

    void Start()
    {
        list_is = new List<InteractionSystem> ();
        list_hi = new List<HandInteraction> ();

        list_is.Add( chars[IDX_COFFEE].GetComponent<InteractionSystem> () );
        list_is.Add( chars[IDX_COMPUTER].GetComponent<InteractionSystem> () );
        list_is.Add( chars[IDX_TOAST].GetComponent<InteractionSystem> () );
        list_is.Add( chars[IDX_STOVE].GetComponent<InteractionSystem> () );
        list_is.Add( chars[IDX_COUNTER].GetComponent<InteractionSystem> () );
        list_is.Add( chars[IDX_HAIRPRODUCT].GetComponent<InteractionSystem> () );

        list_hi.Add( objs[IDX_COFFEE].GetComponent<HandInteraction> () );
        list_hi.Add( objs[IDX_COMPUTER].GetComponent<HandInteraction> () );
        list_hi.Add( objs[IDX_TOAST].GetComponent<HandInteraction> () );
        list_hi.Add( objs[IDX_STOVE].GetComponent<HandInteraction> () );
        list_hi.Add( objs[IDX_COUNTER].GetComponent<HandInteraction> () );
        list_hi.Add( objs[IDX_HAIRPRODUCT].GetComponent<HandInteraction> () );
    }
                
    void OnGUI()
    {
        if ( GUILayout.Button("Use CoffeeMaker - Left Hand") )
        {
            InteractionObject io = list_hi[IDX_COFFEE].Get_IO_interaction(0, list_is[IDX_COFFEE].name, 
                FullBodyBipedEffector.LeftHand);
            list_is[IDX_COFFEE].StartInteraction(FullBodyBipedEffector.LeftHand, io, false);
        }

        if ( GUILayout.Button("Use CoffeeMaker - Right Hand") )
        {
            InteractionObject io = list_hi[IDX_COFFEE].Get_IO_interaction(0, list_is[IDX_COFFEE].name, 
                FullBodyBipedEffector.RightHand);
            list_is[IDX_COFFEE].StartInteraction(FullBodyBipedEffector.RightHand, io, false);
        }

        if ( GUILayout.Button("Use Computer - Left Hand") )
        {
            InteractionObject io = list_hi[IDX_COMPUTER].Get_IO_interaction(0, list_is[IDX_COMPUTER].name, 
                FullBodyBipedEffector.LeftHand);
            list_is[IDX_COMPUTER].StartInteraction(FullBodyBipedEffector.LeftHand, io, false);
        }

        if ( GUILayout.Button("Use Computer - Right Hand") )
        {
            InteractionObject io = list_hi[IDX_COMPUTER].Get_IO_interaction(0, list_is[IDX_COMPUTER].name, 
                FullBodyBipedEffector.RightHand);
            list_is[IDX_COMPUTER].StartInteraction(FullBodyBipedEffector.RightHand, io, false);
        }
        if ( GUILayout.Button("Use Toast - Left Hand") )
        {
            InteractionObject io = list_hi[IDX_TOAST].Get_IO_interaction(0, list_is[IDX_TOAST].name, 
                FullBodyBipedEffector.LeftHand);
            list_is[IDX_TOAST].StartInteraction(FullBodyBipedEffector.LeftHand, io, false);
        }

        if ( GUILayout.Button("Use Toast - Right Hand") )
        {
            InteractionObject io = list_hi[IDX_TOAST].Get_IO_interaction(0, list_is[IDX_TOAST].name, 
                FullBodyBipedEffector.RightHand);
            list_is[IDX_TOAST].StartInteraction(FullBodyBipedEffector.RightHand, io, false);
        }

        if ( GUILayout.Button("Open/Close Stove Door - Left Hand") )
        {
            InteractionObject io = list_hi[IDX_STOVE].Get_IO_interaction(0, list_is[IDX_STOVE].name, 
                FullBodyBipedEffector.LeftHand);
            list_is[IDX_STOVE].StartInteraction(FullBodyBipedEffector.LeftHand, io, false);
        }

        if ( GUILayout.Button("Open/Close Stove Door - Right Hand") )
        {
            InteractionObject io = list_hi[IDX_STOVE].Get_IO_interaction(0, list_is[IDX_STOVE].name, 
                FullBodyBipedEffector.RightHand);
            list_is[IDX_STOVE].StartInteraction(FullBodyBipedEffector.RightHand, io, false);
        }

        if ( GUILayout.Button("Open/Close Counter Drawer - Left Hand") )
        {
            InteractionObject io = list_hi[IDX_COUNTER].Get_IO_interaction(0, list_is[IDX_COUNTER].name, 
                FullBodyBipedEffector.LeftHand);
            list_is[IDX_COUNTER].StartInteraction(FullBodyBipedEffector.LeftHand, io, false);
        }

        if ( GUILayout.Button("Open/Close Counter Drawer - Right Hand") )
        {
            InteractionObject io = list_hi[IDX_COUNTER].Get_IO_interaction(0, list_is[IDX_COUNTER].name, 
                FullBodyBipedEffector.RightHand);
            list_is[IDX_COUNTER].StartInteraction(FullBodyBipedEffector.RightHand, io, false);
        }

        if ( GUILayout.Button("Grab Hair Product - Left Hand") )
        {
            FullBodyBipedEffector fbbe = FullBodyBipedEffector.LeftHand;
            InteractionObject io = list_hi[IDX_HAIRPRODUCT].Get_IO_grab(chars[IDX_HAIRPRODUCT].transform, fbbe);
            list_is[IDX_HAIRPRODUCT].StartInteraction(fbbe, io, false);
        }

        if ( GUILayout.Button("Grab Hair Product - Right Hand") )
        {
            FullBodyBipedEffector fbbe = FullBodyBipedEffector.RightHand;
            InteractionObject io = list_hi[IDX_HAIRPRODUCT].Get_IO_grab(chars[IDX_HAIRPRODUCT].transform, fbbe);
            list_is[IDX_HAIRPRODUCT].StartInteraction(fbbe, io, false);
        }

        if ( GUILayout.Button("Putback Hair Product - Left Hand") )
        {
            GameObject go = list_hi[IDX_HAIRPRODUCT].invisibleCpy;
            go.transform.position = putBackLocation.position;
            FullBodyBipedEffector fbbe = FullBodyBipedEffector.LeftHand;
            InteractionObject io = go.GetComponent<HandInteraction> ().Get_IO_grab(chars[IDX_HAIRPRODUCT].transform, fbbe);
            list_is[IDX_HAIRPRODUCT].StartInteraction(fbbe, io, false);
        }

        if ( GUILayout.Button("Putback Hair Product - Right Hand") )
        {
            GameObject go = list_hi[IDX_HAIRPRODUCT].invisibleCpy;
            go.transform.position = putBackLocation.position;
            FullBodyBipedEffector fbbe = FullBodyBipedEffector.RightHand;
            InteractionObject io = go.GetComponent<HandInteraction> ().Get_IO_grab(chars[IDX_HAIRPRODUCT].transform, fbbe);
            list_is[IDX_HAIRPRODUCT].StartInteraction(fbbe, io, false);
        }
    }
}
