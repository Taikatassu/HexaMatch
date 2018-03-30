using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "ElementType")]
public class ElementType : ScriptableObject
{
    public string element_name;
    public GameObject element_prefab;
    public ElementType[] matching_elements;
}
