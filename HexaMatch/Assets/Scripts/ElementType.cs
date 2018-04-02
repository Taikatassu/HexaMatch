using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "ElementType")]
public class ElementType : ScriptableObject
{
    public string element_name;
    public string collection_effect_pool_tag;
    public Material element_material;
    public ElementType[] matching_elements;
}
