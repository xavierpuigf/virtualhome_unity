using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Gravity : MonoBehaviour
{
    // To-do minimize code length
    public void ActivateGravity() 
    {   
        // Traverse all objetcs in the scene
        object[] obj = GameObject.FindObjectsOfType(typeof (GameObject));
        foreach (object o in obj)
        {   
            GameObject g = (GameObject) o;

            // Destroy box colliders and replace it with mesh colliders and rigidbody
            if (g.name.ToLower().Contains("apple_1024"))
            { 
                g.AddComponent(typeof(MeshCollider));
                g.GetComponent<MeshCollider>().convex = true;
                Destroy(g.GetComponent<BoxCollider>());
                g.AddComponent(typeof(Rigidbody));
                for (int i = 0; i < g.transform.childCount; i++)
                {
                    GameObject child = g.transform.GetChild(i).gameObject;
                    if (child.name.ToLower().Contains("collider"))
                    {
                        Destroy(child);
                    }
                }
            }
            else if (g.name.ToLower().Contains("bowl"))
            { 
                g.AddComponent(typeof(MeshCollider));
                // g.GetComponent<MeshCollider>().convex = true;
                Destroy(g.GetComponent<BoxCollider>());
                // g.AddComponent(typeof(Rigidbody));
                g.isStatic = false;
                for (int i = 0; i < g.transform.childCount; i++)
                {
                    GameObject child = g.transform.GetChild(i).gameObject;
                    if (child.name.ToLower().Contains("collider"))
                    {
                        Destroy(child);
                    }
                }
            }
            else if (g.name.ToLower().Contains("bowl_of"))
            { 
                for (int i = 0; i < g.transform.childCount; i++)
                {
                    GameObject child = g.transform.GetChild(i).gameObject;
                    if (child.name.ToLower().Contains("collider"))
                    {
                        Destroy(child);
                    }
                }
            }
            else if (g.name.ToLower().Contains("bananas"))
            { 
                g.AddComponent(typeof(MeshCollider));
                g.GetComponent<MeshCollider>().convex = true;
                Destroy(g.GetComponent<BoxCollider>());
                g.AddComponent(typeof(Rigidbody));
                for (int i = 0; i < g.transform.childCount; i++)
                {
                    GameObject child = g.transform.GetChild(i).gameObject;
                    if (child.name.ToLower().Contains("collider"))
                    {
                        Destroy(child);
                    }
                }
            }
            else if (g.name.ToLower().Contains("plum"))
            { 
                g.AddComponent(typeof(MeshCollider));
                g.GetComponent<MeshCollider>().convex = true;
                Destroy(g.GetComponent<BoxCollider>());
                g.AddComponent(typeof(Rigidbody));
                for (int i = 0; i < g.transform.childCount; i++)
                {
                    GameObject child = g.transform.GetChild(i).gameObject;
                    if (child.name.ToLower().Contains("collider"))
                    {
                        Destroy(child);
                    }
                }
            }
            else if (g.name.ToLower().Contains("lime"))
            { 
                g.AddComponent(typeof(MeshCollider));
                g.GetComponent<MeshCollider>().convex = true;
                Destroy(g.GetComponent<BoxCollider>());
                g.AddComponent(typeof(Rigidbody));
                for (int i = 0; i < g.transform.childCount; i++)
                {
                    GameObject child = g.transform.GetChild(i).gameObject;
                    if (child.name.ToLower().Contains("collider"))
                    {
                        Destroy(child);
                    }
                }
            }
            else if (g.name.ToLower().Contains("peach"))
            { 
                g.AddComponent(typeof(MeshCollider));
                g.GetComponent<MeshCollider>().convex = true;
                Destroy(g.GetComponent<BoxCollider>());
                g.AddComponent(typeof(Rigidbody));
                for (int i = 0; i < g.transform.childCount; i++)
                {
                    GameObject child = g.transform.GetChild(i).gameObject;
                    if (child.name.ToLower().Contains("collider"))
                    {
                        Destroy(child);
                    }
                }
            }
            else if (g.name.ToLower().Contains("pear"))
            { 
                g.AddComponent(typeof(MeshCollider));
                g.GetComponent<MeshCollider>().convex = true;
                Destroy(g.GetComponent<BoxCollider>());
                g.AddComponent(typeof(Rigidbody));
                for (int i = 0; i < g.transform.childCount; i++)
                {
                    GameObject child = g.transform.GetChild(i).gameObject;
                    if (child.name.ToLower().Contains("collider"))
                    {
                        Destroy(child);
                    }
                }
            }
            else if (g.name.ToLower().Contains("kiwi"))
            { 
                g.AddComponent(typeof(MeshCollider));
                g.GetComponent<MeshCollider>().convex = true;
                Destroy(g.GetComponent<BoxCollider>());
                g.AddComponent(typeof(Rigidbody));
                for (int i = 0; i < g.transform.childCount; i++)
                {
                    GameObject child = g.transform.GetChild(i).gameObject;
                    if (child.name.ToLower().Contains("collider"))
                    {
                        Destroy(child);
                    }
                }
            }
            else if (g.name.ToLower().Contains("bell_pepper_1024"))
            {
                g.AddComponent(typeof(MeshCollider));
                g.GetComponent<MeshCollider>().convex = true;
                Destroy(g.GetComponent<BoxCollider>());
                g.AddComponent(typeof(Rigidbody));
                for (int i = 0; i < g.transform.childCount; i++)
                {
                    GameObject child = g.transform.GetChild(i).gameObject;
                    if (child.name.ToLower().Contains("collider"))
                    {
                        Destroy(child);
                    }
                }
            }
            else if (g.name.ToLower().Contains("tomato"))
            {
                g.AddComponent(typeof(MeshCollider));
                g.GetComponent<MeshCollider>().convex = true;
                Destroy(g.GetComponent<BoxCollider>());
                g.AddComponent(typeof(Rigidbody));
                for (int i = 0; i < g.transform.childCount; i++)
                {
                    GameObject child = g.transform.GetChild(i).gameObject;
                    if (child.name.ToLower().Contains("collider"))
                    {
                        Destroy(child);
                    }
                }
            }
            else if (g.name.ToLower().Contains("potato"))
            {
                g.AddComponent(typeof(MeshCollider));
                g.GetComponent<MeshCollider>().convex = true;
                Destroy(g.GetComponent<BoxCollider>());
                g.AddComponent(typeof(Rigidbody));
                for (int i = 0; i < g.transform.childCount; i++)
                {
                    GameObject child = g.transform.GetChild(i).gameObject;
                    if (child.name.ToLower().Contains("collider"))
                    {
                        Destroy(child);
                    }
                }
            }
            else if (g.name.ToLower().Contains("beer"))
            {
                g.AddComponent(typeof(MeshCollider));
                g.GetComponent<MeshCollider>().convex = true;
                Destroy(g.GetComponent<BoxCollider>());
                g.AddComponent(typeof(Rigidbody));
                for (int i = 0; i < g.transform.childCount; i++)
                {
                    GameObject child = g.transform.GetChild(i).gameObject;
                    if (child.name.ToLower().Contains("collider"))
                    {
                        Destroy(child);
                    }
                }
            }
            else if (g.name.ToLower().Contains("cake"))
            { 
                g.AddComponent(typeof(MeshCollider));
                g.GetComponent<MeshCollider>().convex = true;
                Destroy(g.GetComponent<BoxCollider>());
                g.AddComponent(typeof(Rigidbody));
                for (int i = 0; i < g.transform.childCount; i++)
                {
                    GameObject child = g.transform.GetChild(i).gameObject;
                    if (child.name.ToLower().Contains("collider"))
                    {
                        Destroy(child);
                    }
                }
            }
            else if (g.name.ToLower().Contains("pie"))
            { 
                g.AddComponent(typeof(MeshCollider));
                g.GetComponent<MeshCollider>().convex = true;
                Destroy(g.GetComponent<BoxCollider>());
                g.AddComponent(typeof(Rigidbody));
                for (int i = 0; i < g.transform.childCount; i++)
                {
                    GameObject child = g.transform.GetChild(i).gameObject;
                    if (child.name.ToLower().Contains("collider"))
                    {
                        Destroy(child);
                    }
                }
            }
            else if (g.name.ToLower().Contains("chicken"))
            { 
                g.AddComponent(typeof(MeshCollider));
                g.GetComponent<MeshCollider>().convex = true;
                Destroy(g.GetComponent<BoxCollider>());
                g.AddComponent(typeof(Rigidbody));
                for (int i = 0; i < g.transform.childCount; i++)
                {
                    GameObject child = g.transform.GetChild(i).gameObject;
                    if (child.name.ToLower().Contains("collider"))
                    {
                        Destroy(child);
                    }
                }
            }
            else if (g.name.ToLower().Contains("cutlet"))
            { 
                g.AddComponent(typeof(MeshCollider));
                g.GetComponent<MeshCollider>().convex = true;
                Destroy(g.GetComponent<BoxCollider>());
                g.AddComponent(typeof(Rigidbody));
                for (int i = 0; i < g.transform.childCount; i++)
                {
                    GameObject child = g.transform.GetChild(i).gameObject;
                    if (child.name.ToLower().Contains("collider"))
                    {
                        Destroy(child);
                    }
                }
            }
            else if (g.name.ToLower().Contains("meat"))
            { 
                g.AddComponent(typeof(MeshCollider));
                g.GetComponent<MeshCollider>().convex = true;
                Destroy(g.GetComponent<BoxCollider>());
                g.AddComponent(typeof(Rigidbody));
                for (int i = 0; i < g.transform.childCount; i++)
                {
                    GameObject child = g.transform.GetChild(i).gameObject;
                    if (child.name.ToLower().Contains("collider"))
                    {
                        Destroy(child);
                    }
                }
            }
            else if (g.name.ToLower().Contains("candy"))
            { 
                g.AddComponent(typeof(MeshCollider));
                g.GetComponent<MeshCollider>().convex = true;
                Destroy(g.GetComponent<BoxCollider>());
                g.AddComponent(typeof(Rigidbody));
                for (int i = 0; i < g.transform.childCount; i++)
                {
                    GameObject child = g.transform.GetChild(i).gameObject;
                    if (child.name.ToLower().Contains("collider"))
                    {
                        Destroy(child);
                    }
                }
            }
            else if (g.name.ToLower().Contains("crackers"))
            { 
                g.AddComponent(typeof(MeshCollider));
                g.GetComponent<MeshCollider>().convex = true;
                Destroy(g.GetComponent<BoxCollider>());
                g.AddComponent(typeof(Rigidbody));
                for (int i = 0; i < g.transform.childCount; i++)
                {
                    GameObject child = g.transform.GetChild(i).gameObject;
                    if (child.name.ToLower().Contains("collider"))
                    {
                        Destroy(child);
                    }
                }
            }
            else if (g.name.ToLower().Contains("cereal"))
            { 
                g.AddComponent(typeof(MeshCollider));
                g.GetComponent<MeshCollider>().convex = true;
                Destroy(g.GetComponent<BoxCollider>());
                g.AddComponent(typeof(Rigidbody));
                for (int i = 0; i < g.transform.childCount; i++)
                {
                    GameObject child = g.transform.GetChild(i).gameObject;
                    if (child.name.ToLower().Contains("collider"))
                    {
                        Destroy(child);
                    }
                }
            }
            else if (g.name.ToLower().Contains("buns"))
            { 
                g.AddComponent(typeof(MeshCollider));
                g.GetComponent<MeshCollider>().convex = true;
                Destroy(g.GetComponent<BoxCollider>());
                g.AddComponent(typeof(Rigidbody));
                for (int i = 0; i < g.transform.childCount; i++)
                {
                    GameObject child = g.transform.GetChild(i).gameObject;
                    if (child.name.ToLower().Contains("collider"))
                    {
                        Destroy(child);
                    }
                }
            }
            else if (g.name.ToLower().Contains("chips"))
            { 
                g.AddComponent(typeof(MeshCollider));
                g.GetComponent<MeshCollider>().convex = true;
                Destroy(g.GetComponent<BoxCollider>());
                g.AddComponent(typeof(Rigidbody));
                for (int i = 0; i < g.transform.childCount; i++)
                {
                    GameObject child = g.transform.GetChild(i).gameObject;
                    if (child.name.ToLower().Contains("collider"))
                    {
                        Destroy(child);
                    }
                }
            }
            else if (g.name.ToLower().Contains("chocolate"))
            { 
                g.AddComponent(typeof(MeshCollider));
                g.GetComponent<MeshCollider>().convex = true;
                Destroy(g.GetComponent<BoxCollider>());
                g.AddComponent(typeof(Rigidbody));
                for (int i = 0; i < g.transform.childCount; i++)
                {
                    GameObject child = g.transform.GetChild(i).gameObject;
                    if (child.name.ToLower().Contains("collider"))
                    {
                        Destroy(child);
                    }
                }
            }
            else if (g.name.ToLower().Contains("juice"))
            { 
                g.AddComponent(typeof(MeshCollider));
                g.GetComponent<MeshCollider>().convex = true;
                Destroy(g.GetComponent<BoxCollider>());
                g.AddComponent(typeof(Rigidbody));
                for (int i = 0; i < g.transform.childCount; i++)
                {
                    GameObject child = g.transform.GetChild(i).gameObject;
                    if (child.name.ToLower().Contains("collider"))
                    {
                        Destroy(child);
                    }
                }
            }
            else if (g.name.ToLower().Contains("wine"))
            { 
                g.AddComponent(typeof(MeshCollider));
                g.GetComponent<MeshCollider>().convex = true;
                Destroy(g.GetComponent<BoxCollider>());
                g.AddComponent(typeof(Rigidbody));
                for (int i = 0; i < g.transform.childCount; i++)
                {
                    GameObject child = g.transform.GetChild(i).gameObject;
                    if (child.name.ToLower().Contains("collider"))
                    {
                        Destroy(child);
                    }
                }
            }
            else if (g.name.ToLower().Contains("cream"))
            { 
                g.AddComponent(typeof(MeshCollider));
                g.GetComponent<MeshCollider>().convex = true;
                Destroy(g.GetComponent<BoxCollider>());
                g.AddComponent(typeof(Rigidbody));
                for (int i = 0; i < g.transform.childCount; i++)
                {
                    GameObject child = g.transform.GetChild(i).gameObject;
                    if (child.name.ToLower().Contains("collider"))
                    {
                        Destroy(child);
                    }
                }
            }
            else if (g.name.ToLower().Contains("milk"))
            { 
                g.AddComponent(typeof(MeshCollider));
                g.GetComponent<MeshCollider>().convex = true;
                Destroy(g.GetComponent<BoxCollider>());
                g.AddComponent(typeof(Rigidbody));
                for (int i = 0; i < g.transform.childCount; i++)
                {
                    GameObject child = g.transform.GetChild(i).gameObject;
                    if (child.name.ToLower().Contains("collider"))
                    {
                        Destroy(child);
                    }
                }
            }
            else if (g.name.ToLower().Contains("shaker"))
            { 
                g.AddComponent(typeof(MeshCollider));
                g.GetComponent<MeshCollider>().convex = true;
                Destroy(g.GetComponent<BoxCollider>());
                g.AddComponent(typeof(Rigidbody));
                for (int i = 0; i < g.transform.childCount; i++)
                {
                    GameObject child = g.transform.GetChild(i).gameObject;
                    if (child.name.ToLower().Contains("collider"))
                    {
                        Destroy(child);
                    }
                }
            }
            else if (g.name.ToLower().Contains("condiment"))
            { 
                g.AddComponent(typeof(MeshCollider));
                g.GetComponent<MeshCollider>().convex = true;
                Destroy(g.GetComponent<BoxCollider>());
                g.AddComponent(typeof(Rigidbody));
                for (int i = 0; i < g.transform.childCount; i++)
                {
                    GameObject child = g.transform.GetChild(i).gameObject;
                    if (child.name.ToLower().Contains("collider"))
                    {
                        Destroy(child);
                    }
                }
            }
            else if (g.name.ToLower().Contains("glass"))
            { 
                g.AddComponent(typeof(MeshCollider));
                g.GetComponent<MeshCollider>().convex = true;
                Destroy(g.GetComponent<BoxCollider>());
                g.AddComponent(typeof(Rigidbody));
                for (int i = 0; i < g.transform.childCount; i++)
                {
                    GameObject child = g.transform.GetChild(i).gameObject;
                    if (child.name.ToLower().Contains("collider"))
                    {
                        Destroy(child);
                    }
                }
            }
            else if (g.name.ToLower().Contains("mug"))
            { 
                g.AddComponent(typeof(MeshCollider));
                g.GetComponent<MeshCollider>().convex = true;
                Destroy(g.GetComponent<BoxCollider>());
                g.AddComponent(typeof(Rigidbody));
                for (int i = 0; i < g.transform.childCount; i++)
                {
                    GameObject child = g.transform.GetChild(i).gameObject;
                    if (child.name.ToLower().Contains("collider"))
                    {
                        Destroy(child);
                    }
                }
            }
            else if (g.name.ToLower().Contains("plate"))
            { 
                g.AddComponent(typeof(MeshCollider));
                g.GetComponent<MeshCollider>().convex = true;
                Destroy(g.GetComponent<BoxCollider>());
                g.AddComponent(typeof(Rigidbody));
                for (int i = 0; i < g.transform.childCount; i++)
                {
                    GameObject child = g.transform.GetChild(i).gameObject;
                    if (child.name.ToLower().Contains("collider"))
                    {
                        Destroy(child);
                    }
                }
            }
            else if (g.name.ToLower().Contains("pan"))
            { 
                g.AddComponent(typeof(MeshCollider));
                g.GetComponent<MeshCollider>().convex = true;
                Destroy(g.GetComponent<BoxCollider>());
                g.AddComponent(typeof(Rigidbody));
                for (int i = 0; i < g.transform.childCount; i++)
                {
                    GameObject child = g.transform.GetChild(i).gameObject;
                    if (child.name.ToLower().Contains("collider"))
                    {
                        Destroy(child);
                    }
                }
            }
            else if (g.name.ToLower().Contains("cooking"))
            { 
                g.AddComponent(typeof(MeshCollider));
                g.GetComponent<MeshCollider>().convex = true;
                Destroy(g.GetComponent<BoxCollider>());
                g.AddComponent(typeof(Rigidbody));
                for (int i = 0; i < g.transform.childCount; i++)
                {
                    GameObject child = g.transform.GetChild(i).gameObject;
                    if (child.name.ToLower().Contains("collider"))
                    {
                        Destroy(child);
                    }
                }
            }
            else if (g.name.ToLower().Contains("liquid"))
            { 
                g.AddComponent(typeof(MeshCollider));
                g.GetComponent<MeshCollider>().convex = true;
                Destroy(g.GetComponent<BoxCollider>());
                g.AddComponent(typeof(Rigidbody));
                for (int i = 0; i < g.transform.childCount; i++)
                {
                    GameObject child = g.transform.GetChild(i).gameObject;
                    if (child.name.ToLower().Contains("collider"))
                    {
                        Destroy(child);
                    }
                }
            }
            else if (g.name.ToLower().Contains("sponge"))
            { 
                g.AddComponent(typeof(MeshCollider));
                g.GetComponent<MeshCollider>().convex = true;
                Destroy(g.GetComponent<BoxCollider>());
                g.AddComponent(typeof(Rigidbody));
                for (int i = 0; i < g.transform.childCount; i++)
                {
                    GameObject child = g.transform.GetChild(i).gameObject;
                    if (child.name.ToLower().Contains("collider"))
                    {
                        Destroy(child);
                    }
                }
            }
            else if (g.name.ToLower().Contains("knife_0"))
            { 
                g.AddComponent(typeof(MeshCollider));
                g.GetComponent<MeshCollider>().convex = true;
                Destroy(g.GetComponent<BoxCollider>());
                g.AddComponent(typeof(Rigidbody));
                for (int i = 0; i < g.transform.childCount; i++)
                {
                    GameObject child = g.transform.GetChild(i).gameObject;
                    if (child.name.ToLower().Contains("collider"))
                    {
                        Destroy(child);
                    }
                }
            }
            else if (g.name.ToLower().Contains("fork"))
            { 
                g.AddComponent(typeof(MeshCollider));
                g.GetComponent<MeshCollider>().convex = true;
                Destroy(g.GetComponent<BoxCollider>());
                g.AddComponent(typeof(Rigidbody));
                for (int i = 0; i < g.transform.childCount; i++)
                {
                    GameObject child = g.transform.GetChild(i).gameObject;
                    if (child.name.ToLower().Contains("collider"))
                    {
                        Destroy(child);
                    }
                }
            }
            else if (g.name.ToLower().Contains("cellphone"))
            { 
                // g.AddComponent(typeof(MeshCollider));
                // g.GetComponent<MeshCollider>().convex = true;
                // Destroy(g.GetComponent<BoxCollider>());
                g.AddComponent(typeof(Rigidbody));
                // for (int i = 0; i < g.transform.childCount; i++)
                // {
                //     GameObject child = g.transform.GetChild(i).gameObject;
                //     if (child.name.ToLower().Contains("collider"))
                //     {
                //         Destroy(child);
                //     }
                // }
            }
            else if (g.name.ToLower().Contains("remote"))
            { 
                g.AddComponent(typeof(Rigidbody));
                for (int i = 0; i < g.transform.childCount; i++)
                {
                    GameObject child = g.transform.GetChild(i).gameObject;
                    if (child.name.ToLower().Contains("collider"))
                    {
                        Destroy(child);
                    }
                }
            }
            else if (g.name.ToLower().Contains("folder"))
            { 
                // g.AddComponent(typeof(MeshCollider));
                // g.GetComponent<MeshCollider>().convex = true;
                // Destroy(g.GetComponent<BoxCollider>());
                g.AddComponent(typeof(Rigidbody));
                for (int i = 0; i < g.transform.childCount; i++)
                {
                    // GameObject child = g.transform.GetChild(i).gameObject;
                    // if (child.name.ToLower().Contains("collider"))
                    // {
                    //     Destroy(child);
                    // }
                }
            }
            // else if (g.name.ToLower().Contains("paper_"))
            // { 
            //     g.AddComponent(typeof(MeshCollider));
            //     Destroy(g.GetComponent<BoxCollider>());
            //     g.AddComponent(typeof(Rigidbody));
                // for (int i = 0; i < g.transform.childCount; i++)
                // {
                //     GameObject child = g.transform.GetChild(i).gameObject;
                //     if (child.name.ToLower().Contains("collider"))
                //     {
                //         Destroy(child);
                //     }
                // }
            // }
            else if (g.name.ToLower().Contains("book_"))
            { 
                g.AddComponent(typeof(MeshCollider));
                g.GetComponent<MeshCollider>().convex = true;
                Destroy(g.GetComponent<BoxCollider>());
                g.AddComponent(typeof(Rigidbody));
                for (int i = 0; i < g.transform.childCount; i++)
                {
                    GameObject child = g.transform.GetChild(i).gameObject;
                    if (child.name.ToLower().Contains("collider"))
                    {
                        Destroy(child);
                    }
                }
            }
            else if (g.name.ToLower().Contains("box_"))
            { 
                g.AddComponent(typeof(MeshCollider));
                g.GetComponent<MeshCollider>().convex = true;
                Destroy(g.GetComponent<BoxCollider>());
                g.AddComponent(typeof(Rigidbody));
                for (int i = 0; i < g.transform.childCount; i++)
                {
                    GameObject child = g.transform.GetChild(i).gameObject;
                    if (child.name.ToLower().Contains("collider"))
                    {
                        Destroy(child);
                    }
                }
            }
            else if (g.name.ToLower().Contains("photo"))
            { 
                g.AddComponent(typeof(MeshCollider));
                g.GetComponent<MeshCollider>().convex = true;
                Destroy(g.GetComponent<BoxCollider>());
                g.AddComponent(typeof(Rigidbody));
                for (int i = 0; i < g.transform.childCount; i++)
                {
                    GameObject child = g.transform.GetChild(i).gameObject;
                    if (child.name.ToLower().Contains("collider"))
                    {
                        Destroy(child);
                    }
                }
            }
            else if (g.name.ToLower().Contains("pillow"))
            { 
                g.AddComponent(typeof(MeshCollider));
                g.GetComponent<MeshCollider>().convex = true;
                Destroy(g.GetComponent<BoxCollider>());
                g.AddComponent(typeof(Rigidbody));
                for (int i = 0; i < g.transform.childCount; i++)
                {
                    GameObject child = g.transform.GetChild(i).gameObject;
                    if (child.name.ToLower().Contains("collider"))
                    {
                        Destroy(child);
                    }
                }
            }
            else if (g.name.ToLower().Contains("keyboard"))
            { 
                g.AddComponent(typeof(MeshCollider));
                g.GetComponent<MeshCollider>().convex = true;
                Destroy(g.GetComponent<BoxCollider>());
                g.AddComponent(typeof(Rigidbody));
                for (int i = 0; i < g.transform.childCount; i++)
                {
                    GameObject child = g.transform.GetChild(i).gameObject;
                    if (child.name.ToLower().Contains("collider"))
                    {
                        Destroy(child);
                    }
                }
            }
            else if (g.name.ToLower().Contains("mouse"))
            { 
                g.AddComponent(typeof(MeshCollider));
                g.GetComponent<MeshCollider>().convex = true;
                Destroy(g.GetComponent<BoxCollider>());
                g.AddComponent(typeof(Rigidbody));
                for (int i = 0; i < g.transform.childCount; i++)
                {
                    GameObject child = g.transform.GetChild(i).gameObject;
                    if (child.name.ToLower().Contains("collider"))
                    {
                        Destroy(child);
                    }
                }
            }
            else if (g.name.ToLower().Contains("toothpaste"))
            { 
                g.AddComponent(typeof(MeshCollider));
                g.GetComponent<MeshCollider>().convex = true;
                Destroy(g.GetComponent<BoxCollider>());
                g.AddComponent(typeof(Rigidbody));
                for (int i = 0; i < g.transform.childCount; i++)
                {
                    GameObject child = g.transform.GetChild(i).gameObject;
                    if (child.name.ToLower().Contains("collider"))
                    {
                        Destroy(child);
                    }
                }
            }
            else if (g.name.ToLower().Contains("toothbrush"))
            { 
                g.AddComponent(typeof(MeshCollider));
                g.GetComponent<MeshCollider>().convex = true;
                Destroy(g.GetComponent<BoxCollider>());
                g.AddComponent(typeof(Rigidbody));
                for (int i = 0; i < g.transform.childCount; i++)
                {
                    GameObject child = g.transform.GetChild(i).gameObject;
                    if (child.name.ToLower().Contains("collider"))
                    {
                        Destroy(child);
                    }
                }
            }
            else if (g.name.ToLower().Contains("shampoo"))
            { 
                g.AddComponent(typeof(MeshCollider));
                g.GetComponent<MeshCollider>().convex = true;
                Destroy(g.GetComponent<BoxCollider>());
                g.AddComponent(typeof(Rigidbody));
                for (int i = 0; i < g.transform.childCount; i++)
                {
                    GameObject child = g.transform.GetChild(i).gameObject;
                    if (child.name.ToLower().Contains("collider"))
                    {
                        Destroy(child);
                    }
                }
            }
            else if (g.name.ToLower().Contains("conditioner"))
            { 
                g.AddComponent(typeof(MeshCollider));
                g.GetComponent<MeshCollider>().convex = true;
                Destroy(g.GetComponent<BoxCollider>());
                g.AddComponent(typeof(Rigidbody));
                for (int i = 0; i < g.transform.childCount; i++)
                {
                    GameObject child = g.transform.GetChild(i).gameObject;
                    if (child.name.ToLower().Contains("collider"))
                    {
                        Destroy(child);
                    }
                }
            }
            else if (g.name.ToLower().Contains("deodorant"))
            { 
                g.AddComponent(typeof(MeshCollider));
                g.GetComponent<MeshCollider>().convex = true;
                Destroy(g.GetComponent<BoxCollider>());
                g.AddComponent(typeof(Rigidbody));
                for (int i = 0; i < g.transform.childCount; i++)
                {
                    GameObject child = g.transform.GetChild(i).gameObject;
                    if (child.name.ToLower().Contains("collider"))
                    {
                        Destroy(child);
                    }
                }
            }
            else if (g.name.ToLower().Contains("soap"))
            { 
                g.AddComponent(typeof(MeshCollider));
                g.GetComponent<MeshCollider>().convex = true;
                Destroy(g.GetComponent<BoxCollider>());
                g.AddComponent(typeof(Rigidbody));
                for (int i = 0; i < g.transform.childCount; i++)
                {
                    GameObject child = g.transform.GetChild(i).gameObject;
                    if (child.name.ToLower().Contains("collider"))
                    {
                        Destroy(child);
                    }
                }
            }
            else if (g.name.ToLower().Contains("clothe"))
            { 
                g.AddComponent(typeof(MeshCollider));
                g.GetComponent<MeshCollider>().convex = true;
                Destroy(g.GetComponent<BoxCollider>());
                g.AddComponent(typeof(Rigidbody));
                for (int i = 0; i < g.transform.childCount; i++)
                {
                    GameObject child = g.transform.GetChild(i).gameObject;
                    if (child.name.ToLower().Contains("collider"))
                    {
                        Destroy(child);
                    }
                }
            }
            else if (g.name.ToLower().Contains("candle"))
            { 
                g.AddComponent(typeof(MeshCollider));
                g.GetComponent<MeshCollider>().convex = true;
                Destroy(g.GetComponent<BoxCollider>());
                g.AddComponent(typeof(Rigidbody));
                for (int i = 0; i < g.transform.childCount; i++)
                {
                    GameObject child = g.transform.GetChild(i).gameObject;
                    if (child.name.ToLower().Contains("collider"))
                    {
                        Destroy(child);
                    }
                }
            }

        }

    }

}
