using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EffectManager : MonoBehaviour
{
    public Material valid_selection_material;
    public Material invalid_selection_material;

    public Color non_highlighted_element_color;
    public Color default_element_color;

    public string selection_effect_pool_tag;

    public bool display_connected_matches_highlight = true;

    private PoolManager pool_manager;
    private HexGrid grid;
    private LineRenderer selection_line;

    private List<SelectionEffectInfo> selection_effect_infos;
    private Queue<float> collection_effect_spawn_times;
    private Queue<GameObject> active_collection_effects;

    private float selection_effect_y_pos = -0.1f;
    private float selection_line_height = 1f;
    private float collection_effect_y_pos = 0.1f;
    private float collection_effect_lifetime = 1f;

    private void Start()
    {
        pool_manager = GetComponent<PoolManager>();
        grid = GetComponent<HexGrid>();
        selection_line = GetComponent<LineRenderer>();

        selection_effect_infos = new List<SelectionEffectInfo>();
        ClearSelectionLine();

        collection_effect_spawn_times = new Queue<float>();
        active_collection_effects = new Queue<GameObject>();
    }

    private void Update()
    {
        if (collection_effect_spawn_times.Count > 0)
        {
            //int effects_disabled = 0;
            while (collection_effect_spawn_times.Count > 0 && Time.time >= collection_effect_spawn_times.Peek() + collection_effect_lifetime)
            {
                collection_effect_spawn_times.Dequeue();
                GameObject finished_effect = active_collection_effects.Dequeue();
                finished_effect.SetActive(false);
                //effects_disabled++;
            }

            //if (effects_disabled > 0)
            //    print("Disabled " + effects_disabled + " finished effects.");
        }
    }

    public void SpawnSelectionEffectAtIndex(Vector2 grid_index)
    {
        Vector3 spawn_pos = Vector3.zero;
        spawn_pos.y = selection_effect_y_pos;
        GameObject new_effect = pool_manager.SpawnFromPool(selection_effect_pool_tag);
        new_effect.transform.SetParent(grid.GetGridElementDataFromIndex(grid_index).element_transform);
        new_effect.transform.localPosition = spawn_pos;
        new_effect.SetActive(true);

        selection_effect_infos.Add(new SelectionEffectInfo(grid_index, new_effect));
    }

    public void ClearSelectionEffectAtIndex(Vector2 grid_index)
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
    }

    public void StartSelectionLine(Vector2 start_index)
    {
        Vector3 start_pos = grid.CalculateWorldPos(start_index);
        start_pos.y = selection_line_height;

        Vector3[] new_line_positions = new Vector3[1] { start_pos };
        selection_line.positionCount = new_line_positions.Length;
        selection_line.SetPositions(new_line_positions);
    }

    public void AddPointToSelectionLine(Vector2 new_point_index)
    {
        Vector3 new_point = grid.CalculateWorldPos(new_point_index);
        new_point.y = selection_line_height;

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

    public void HighlightIndices(List<Vector2> indices_to_highlight)
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
                        if (indices_to_highlight[i] == new Vector2(x, y))
                        {
                            ignore_index = true;
                        }
                    }

                    if (ignore_index)
                    {
                        continue;
                    }

                    grid.GetGridElementDataFromIndex(new Vector2(x, y)).element_transform.GetComponentInChildren<Renderer>().material.color = non_highlighted_element_color;
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
                    grid.GetGridElementDataFromIndex(new Vector2(x, y)).element_transform.GetComponentInChildren<Renderer>().material.color = default_element_color;
                }
            }
        }
    }

    public void SpawnCollectionEffectOnIndex(Vector2 grid_index)
    {
        GameObject effect = pool_manager.SpawnFromPool(grid.GetGridElementDataFromIndex(grid_index).element_type.collection_effect_pool_tag);
        Vector3 spawn_pos = grid.CalculateWorldPos(grid_index);
        spawn_pos.y = collection_effect_y_pos;
        effect.transform.position = spawn_pos;

        collection_effect_spawn_times.Enqueue(Time.time);
        active_collection_effects.Enqueue(effect);
        effect.SetActive(true);
        effect.GetComponent<ParticleSystem>().Play(true);
    }
}

public struct SelectionEffectInfo
{
    public Vector2 grid_index;
    public GameObject selection_effect;

    public SelectionEffectInfo(Vector2 _grid_index, GameObject _selection_effect)
    {
        grid_index = _grid_index;
        selection_effect = _selection_effect;
    }
}
