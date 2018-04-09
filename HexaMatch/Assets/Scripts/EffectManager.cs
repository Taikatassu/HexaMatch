using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class EffectManager : MonoBehaviour
{
    public delegate void IntVoid(int integer);
    public event IntVoid OnPointPopupEffectFinished;

    public Material valid_selection_material;
    public Material invalid_selection_material;

    public Color non_highlighted_element_color;
    public Color default_element_color;

    public Transform point_popup_destination;

    public string selection_effect_pool_tag;
    public string point_popup_pool_tag;

    public bool display_connected_matches_highlight = true;

    private PoolManager pool_manager;
    private HexGrid grid;
    private LineRenderer selection_line;

    private List<Transform> active_point_popups;
    private List<SelectionEffectInfo> selection_effect_infos;
    private Queue<float> collection_effect_spawn_times;
    private Queue<GameObject> active_collection_effects;

    private float selection_effect_z_pos = 0.1f;
    private float selection_line_z_pos = -1f;
    private float collection_effect_z_pos = -0.1f;
    private float collection_effect_lifetime = 1f;
    private float point_popup_movement_speed = 800f;

    private void Start()
    {
        pool_manager = GetComponent<PoolManager>();
        grid = GetComponent<HexGrid>();
        selection_line = GetComponent<LineRenderer>();

        active_point_popups = new List<Transform>();
        selection_effect_infos = new List<SelectionEffectInfo>();
        collection_effect_spawn_times = new Queue<float>();
        active_collection_effects = new Queue<GameObject>();

        ClearSelectionLine();
    }

    private void Update()
    {
        if (collection_effect_spawn_times.Count > 0)
        {
            //int effects_disabled = 0;
            while (collection_effect_spawn_times.Count > 0 && Time.time >= collection_effect_spawn_times.Peek() + collection_effect_lifetime)
            {
                collection_effect_spawn_times.Dequeue();
                active_collection_effects.Dequeue().SetActive(false);
                //effects_disabled++;
            }

            //if (effects_disabled > 0)
            //    print("Disabled " + effects_disabled + " finished effects.");
        }

        if (active_point_popups.Count > 0)
        {
            for (int i = 0; i < active_point_popups.Count; i++)
            {
                if (active_point_popups[i].position == point_popup_destination.position)
                {
                    //TOOD: Spawn sparkle (or other suitable) small effect here

                    if (OnPointPopupEffectFinished != null)
                    {
                        OnPointPopupEffectFinished(int.Parse(active_point_popups[i].GetComponentInChildren<Text>().text));
                    }

                    active_point_popups[i].gameObject.SetActive(false);
                    active_point_popups.RemoveAt(i);
                    i--;

                    continue;
                }

                Vector3 direction_to_destination = point_popup_destination.position - active_point_popups[i].position;
                float distance_to_destination = direction_to_destination.magnitude;

                if (distance_to_destination <= point_popup_movement_speed * Time.deltaTime)
                {
                    active_point_popups[i].position = point_popup_destination.position;
                }
                else
                {
                    active_point_popups[i].position += direction_to_destination / distance_to_destination * point_popup_movement_speed * Time.deltaTime;
                }
            }
        }
    }

    public void SpawnSelectionEffectAtIndex(IntVector2 grid_index)
    {
        Vector3 spawn_pos = Vector3.zero;
        spawn_pos.z = selection_effect_z_pos;
        GameObject new_effect = pool_manager.SpawnFromPool(selection_effect_pool_tag);
        new_effect.transform.SetParent(grid.GetGridElementDataFromIndex(grid_index).element_transform);
        new_effect.transform.localPosition = spawn_pos;
        new_effect.SetActive(true);

        selection_effect_infos.Add(new SelectionEffectInfo(grid_index, new_effect));
    }

    public void ClearSelectionEffectAtIndex(IntVector2 grid_index)
    {
        for (int i = 0; i < selection_effect_infos.Count; i++)
        {
            if (selection_effect_infos[i].grid_index == grid_index)
            {
                if (selection_effect_infos[i].selection_effect != null)
                {
                    selection_effect_infos[i].selection_effect.SetActive(false);
                    selection_effect_infos[i].selection_effect.transform.SetParent(this.transform);
                }
                selection_effect_infos.RemoveAt(i);
                //print("Removed selection effect at index: " + grid_index);
                break;
            }
        }
    }

    public void ClearAllSelectionEffects()
    {
        while (selection_effect_infos.Count > 0)
        {
            if (selection_effect_infos[0].selection_effect != null)
            {
                selection_effect_infos[0].selection_effect.SetActive(false);
                selection_effect_infos[0].selection_effect.transform.SetParent(this.transform);
            }
            selection_effect_infos.RemoveAt(0);
        }

        selection_effect_infos = new List<SelectionEffectInfo>();
    }

    public void StartSelectionLine(IntVector2 start_index)
    {
        Vector3 start_pos = grid.CalculateWorldPos(start_index);
        start_pos.z = selection_line_z_pos;

        Vector3[] new_line_positions = new Vector3[1] { start_pos };
        selection_line.positionCount = new_line_positions.Length;
        selection_line.SetPositions(new_line_positions);
    }

    public void AddPointToSelectionLine(IntVector2 new_point_index)
    {
        Vector3 new_point = grid.CalculateWorldPos(new_point_index);
        new_point.z = selection_line_z_pos;

        Vector3[] new_line_positions = new Vector3[selection_line.positionCount + 1];
        for (int i = 0; i < selection_line.positionCount; i++)
        {
            new_line_positions[i] = selection_line.GetPosition(i);
        }

        new_line_positions[new_line_positions.Length - 1] = new_point;
        selection_line.positionCount = new_line_positions.Length;
        selection_line.SetPositions(new_line_positions);
    }

    public void ClearSelectionLine()
    {
        selection_line.positionCount = 0;
        selection_line.material = valid_selection_material;
    }

    public void InvalidateSelectionLine()
    {
        selection_line.material = invalid_selection_material;
    }

    public void HighlightIndices(List<IntVector2> indices_to_highlight)
    {
        if (display_connected_matches_highlight)
        {
            for (int x = 0; x < grid.grid_width; x++)
            {
                for (int y = 0; y < grid.grid_height; y++)
                {
                    bool ignore_index = false;
                    for (int i = 0; i < indices_to_highlight.Count; i++)
                    {
                        if (indices_to_highlight[i] == new IntVector2(x, y))
                        {
                            ignore_index = true;
                        }
                    }

                    if (ignore_index)
                    {
                        continue;
                    }

                    grid.GetGridElementDataFromIndex(new IntVector2(x, y)).element_transform.GetComponentInChildren<Renderer>().material.color = non_highlighted_element_color;
                }
            }
        }
    }

    public void ClearHighlights()
    {
        if (display_connected_matches_highlight)
        {
            for (int x = 0; x < grid.grid_width; x++)
            {
                for (int y = 0; y < grid.grid_height; y++)
                {
                    grid.GetGridElementDataFromIndex(new IntVector2(x, y)).element_transform.GetComponentInChildren<Renderer>().material.color = default_element_color;
                }
            }
        }
    }

    public void SpawnCollectionEffectOnIndex(IntVector2 grid_index)
    {
        GameObject effect = pool_manager.SpawnFromPool(grid.GetGridElementDataFromIndex(grid_index).element_type.collection_effect_pool_tag);
        Vector3 spawn_pos = grid.CalculateWorldPos(grid_index);
        spawn_pos.z = collection_effect_z_pos;
        effect.transform.position = spawn_pos;

        collection_effect_spawn_times.Enqueue(Time.time);
        active_collection_effects.Enqueue(effect);
        effect.SetActive(true);
        effect.GetComponent<ParticleSystem>().Play(true);
    }

    public void SpawnPointPopUpsForMatch(List<IntVector2> match_element_indices)
    {
        //print("match_element_indices.Count: " + match_element_indices.Count);
        int element_count = match_element_indices.Count;
        for (int i = 0; i < element_count; i++)
        {
            GameObject new_point_popup = pool_manager.SpawnFromPool(point_popup_pool_tag);
            new_point_popup.GetComponentInChildren<Text>().text = element_count.ToString();
            new_point_popup.transform.position = Camera.main.WorldToScreenPoint(grid.GetGridElementDataFromIndex(match_element_indices[i]).correct_world_pos);
            new_point_popup.SetActive(true);
            active_point_popups.Add(new_point_popup.transform);
        }
    }

    public void Restart()
    {
        //Reset and clear collection effects
        while(active_collection_effects.Count > 0)
        {
            active_collection_effects.Dequeue().SetActive(false);
        }

        collection_effect_spawn_times = new Queue<float>();
        active_collection_effects = new Queue<GameObject>();

        //Reset and clear point popup effects
        for (int i = 0; i < active_point_popups.Count; i++)
        {
            active_point_popups[i].gameObject.SetActive(false);
        }

        active_point_popups = new List<Transform>();

        ClearSelectionLine();
        ClearAllSelectionEffects();
        ClearHighlights();
    }
}

public struct SelectionEffectInfo
{
    public IntVector2 grid_index;
    public GameObject selection_effect;

    public SelectionEffectInfo(IntVector2 _grid_index, GameObject _selection_effect)
    {
        grid_index = _grid_index;
        selection_effect = _selection_effect;
    }
}
