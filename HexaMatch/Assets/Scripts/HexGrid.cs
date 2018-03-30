using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HexGrid : MonoBehaviour
{
    // Original source: https://www.youtube.com/watch?v=konL0iB5gPI

    //TODO: Implement area-highlight functionality (e.g. highlight all matching elements connected to the selected element) 

    //TODO: Implement proper effect manager and relocate selection effect management to that script

    //TODO: Modify elements to use the same prefab, just change the material / texture to match the correct element type
    //          - Implement pooling for the element visuals

    //TODO: Correct grid start_pos calculations (7x6 grid is centered correctly, 6x5 is not?)

    public Transform hex_base_prefab;
    public ElementType[] element_types;
    public GameObject selection_effect_prefab;
    public List<SelectionEffectInfo> selection_effect_infos;

    public bool spawn_hex_bases;

    public int grid_width = 7;
    public int grid_height = 6;
    public int min_viable_connection = 2;

    public float selection_effect_height = -0.1f;
    public float hex_base_height = -0.25f;
    public float gap = 0.0f;
    float hex_width = 1f;
    float hex_height = 0.866f;
    float element_width = 0.725f;

    Vector3 start_pos;

    GridElementData[,] grid_elements;

    private void Start()
    {
        selection_effect_infos = new List<SelectionEffectInfo>();

        AddGap();
        CalculateStartPos();
        CreateGrid();
    }

    private void AddGap()
    {
        hex_width += hex_width * gap;
        hex_height += hex_height * gap;
    }

    private void CalculateStartPos()
    {
        float x, z = 0;

        float z_offset = (grid_width / 2 % 2 == 0) ? 0 : hex_height / 2;

        x = -hex_width * 0.75f * (grid_width / 2f);
        z = hex_height * (grid_height / 2f) - z_offset;

        start_pos = new Vector3(x, 0, z);
        print("Start_pos: " + start_pos);
    }

    private void CreateGrid()
    {
        grid_elements = new GridElementData[grid_width, grid_height];

        for (int x = 0; x < grid_width; x++)
        {
            for (int y = 0; y < grid_height; y++)
            {
                if (spawn_hex_bases)
                {
                    SpawnHexBaseTile(x, y);
                }
            }
        }


        FillGrid(new Vector2(0, -1f), true);
    }

    private void SpawnHexBaseTile(int grid_index_x, int grid_index_y)
    {
        Transform hex = Instantiate(hex_base_prefab) as Transform;
        Vector3 spawn_pos = CalculateWorldPos(new Vector2(grid_index_x, grid_index_y));
        spawn_pos.y = hex_base_height;
        hex.position = spawn_pos;

        hex.parent = this.transform;
        hex.name = "HexTile " + grid_index_x + "|" + grid_index_y;
    }

    private void CreateNewGridElement(ElementType element_type, Vector3 spawn_pos, Vector2 grid_index, Transform parent)
    {
        Transform element = Instantiate(element_type.element_prefab).transform; 
        element.position = spawn_pos;
        element.parent = parent;
        grid_elements[(int)grid_index.x, (int)grid_index.y] = new GridElementData(element_type, element, CalculateWorldPos(grid_index));
    }

    private ElementType ChooseRandomElementType()
    {
        return element_types[Random.Range(0, element_types.Length)];
    }

    private IEnumerator MoveAllDroppingElementsTowardsCorrectWorldPositions()
    {
        print("Starting drop 'animations'.");

        float movement_speed = 0f;
        float movement_speed_increment_per_second = 25f;

        List<Vector2> indices_to_move = new List<Vector2>();

        for (int x = 0; x < grid_elements.GetLength(0); x++)
        {
            for (int y = 0; y < grid_elements.GetLength(1); y++)
            {
                if (grid_elements[x, y].element_transform.position != grid_elements[x, y].correct_world_pos)
                {
                    indices_to_move.Add(new Vector2(x, y));
                }
            }
        }

        print("Indices to move count " + indices_to_move.Count);

        while (indices_to_move.Count > 0)
        {
            for (int i = 0; i < indices_to_move.Count; i++)
            {
                GridElementData element_to_move = grid_elements[(int)indices_to_move[i].x, (int)indices_to_move[i].y];

                Vector3 direction_to_correct_pos = element_to_move.correct_world_pos - element_to_move.element_transform.position;
                float distance_to_correct_pos = direction_to_correct_pos.magnitude;

                if (distance_to_correct_pos <= movement_speed * Time.deltaTime)
                {
                    //print("Element " + indices_to_move[i].x + "|" + indices_to_move[i].y + ", distance_to_correct_pos: " + distance_to_correct_pos);
                    element_to_move.element_transform.position = element_to_move.correct_world_pos;
                    grid_elements[(int)indices_to_move[i].x, (int)indices_to_move[i].y].just_spawned = false;
                    indices_to_move.RemoveAt(i);
                    i--;
                }
                else
                {
                    direction_to_correct_pos /= distance_to_correct_pos;
                    element_to_move.element_transform.position += direction_to_correct_pos * movement_speed * Time.deltaTime;
                }
            }

            movement_speed += movement_speed_increment_per_second * Time.deltaTime;

            yield return new WaitForEndOfFrame();
        }

        print("Finished drop 'animations'.");
    }

    public void SwapElements(Vector2 a_index, Vector2 b_index)
    {
        int a_x = (int)a_index.x;
        int a_y = (int)a_index.y;
        int b_x = (int)b_index.x;
        int b_y = (int)b_index.y;

        GridElementData old_a = grid_elements[a_x, a_y];
        Vector3 b_world_pos = grid_elements[b_x, b_y].correct_world_pos;
        grid_elements[b_x, b_y].correct_world_pos = grid_elements[a_x, a_y].correct_world_pos;
        grid_elements[a_x, a_y] = grid_elements[b_x, b_y];
        grid_elements[b_x, b_y] = old_a;
        grid_elements[b_x, b_y].correct_world_pos = b_world_pos;

        ResetElementWorldPos(a_index);
        ResetElementWorldPos(b_index);

        //grid_elements[a_x, a_y].world_pos = CalculateWorldPos(a_index);
        //grid_elements[b_x, b_y].world_pos = CalculateWorldPos(b_index);
    }

    public void ResetElementWorldPos(Vector2 grid_index)
    {
        GridElementData element = grid_elements[(int)grid_index.x, (int)grid_index.y];
        if (element.element_transform != null)
        {
            //TODO: Recalculate world_pos from grid_index here??
            element.element_transform.transform.position = element.correct_world_pos;
            element.element_transform.transform.parent = this.transform;
        }
    }

    public Vector3 CalculateWorldPos(Vector2 grid_pos)
    {
        float x, z = 0;

        float z_offset = (grid_pos.x % 2 == 0) ? 0 : hex_height / 2;

        x = start_pos.x + grid_pos.x * hex_width * 0.75f;
        z = start_pos.z - grid_pos.y * hex_height + z_offset;

        return new Vector3(x, 0, z);
    }

    public Vector2 GetGridIndexFromWorldPosition(Vector3 world_pos, bool limit_to_element_width = false)
    {
        for (int x = 0; x < grid_elements.GetLength(0); x++)
        {
            for (int y = 0; y < grid_elements.GetLength(1); y++)
            {
                Vector3 grid_world_pos = CalculateWorldPos(new Vector2(x, y));
                float half_area_size = limit_to_element_width ? element_width / 2 : (hex_height - gap) / 2;
                float x_min = grid_world_pos.x - half_area_size;
                float x_max = grid_world_pos.x + half_area_size;
                float z_min = grid_world_pos.z - half_area_size;
                float z_max = grid_world_pos.z + half_area_size;

                if (world_pos.x >= x_min && world_pos.x <= x_max && world_pos.z >= z_min && world_pos.z <= z_max)
                {
                    Vector2 matching_grid_index = new Vector2(x, y);
                    return matching_grid_index;
                }
            }
        }

        return new Vector2(-1f, -1f);
    }

    /* - Currently obsolete functions - 
    public GridElementData GetGridElementDataFromWorldPosition(Vector3 world_pos)
    {
        for (int x = 0; x < grid_elements.GetLength(0); x++)
        {
            for (int y = 0; y < grid_elements.GetLength(1); y++)
            {
                Vector3 grid_world_pos = grid_elements[x, y].correct_world_pos;
                float half_hex_height = (hex_height - gap) / 2;
                float x_min = grid_world_pos.x - half_hex_height;
                float x_max = grid_world_pos.x + half_hex_height;
                float z_min = grid_world_pos.z - half_hex_height;
                float z_max = grid_world_pos.z + half_hex_height;

                if (world_pos.x >= x_min && world_pos.x <= x_max && world_pos.z >= z_min && world_pos.z <= z_max)
                {
                    print("Matching grid index found: " + x + "|" + y);
                    return grid_elements[x, y];
                }
            }
        }

        print("Position is outside of the grid.");
        return new GridElementData();
    }
    */

    public GridElementData GetElementDataFromIndex(Vector2 grid_index)
    {
        return grid_elements[(int)grid_index.x, (int)grid_index.y];
    }

    public Vector2[] GetNeighbouringIndices(Vector2 grid_index)
    {
        int index_x = (int)grid_index.x;
        int index_y = (int)grid_index.y;

        List<Vector2> neighbours = new List<Vector2>();

        for (int x = index_x - 1; x <= index_x + 1; x++)
        {
            if (x < 0 || x >= grid_elements.GetLength(0))
                continue;


            for (int y = index_y - 1; y <= index_y + 1; y++)
            {
                if (y < 0 || y >= grid_elements.GetLength(1))
                    continue;

                if (CheckIfNeighbours(index_x, index_y, x, y))
                    neighbours.Add(new Vector2(x, y));
            }
        }

        print("Element " + index_x + "|" + index_y + " neighbour count: " + neighbours.Count);

        return neighbours.ToArray();
    }

    public bool CheckIfNeighbours(Vector2 a_index, Vector2 b_index)
    {
        int a_x = (int)a_index.x;
        int a_y = (int)a_index.y;
        int b_x = (int)b_index.x;
        int b_y = (int)b_index.y;

        return CheckIfNeighbours(a_x, a_y, b_x, b_y);
    }

    public bool CheckIfNeighbours(int a_x, int a_y, int b_x, int b_y)
    {
        if (b_x >= a_x - 1 && b_x <= a_x + 1 && b_y >= a_y - 1 && b_y <= a_y + 1)
        {
            if (b_x == a_x && b_y == a_y)
                return false;

            if (a_x % 2 == 0)
            {
                if ((b_x == a_x - 1 || b_x == a_x + 1) && b_y == a_y - 1)
                    return false;
            }
            else if ((b_x == a_x - 1 || b_x == a_x + 1) && b_y == a_y + 1)
            {
                return false;
            }

            return true;
        }

        return false;
    }

    public void RemoveElementAtIndex(Vector2 grid_index, bool destroy_attached_transform = true)
    {
        int x = (int)grid_index.x;
        int y = (int)grid_index.y;

        if (destroy_attached_transform)
        {
            Destroy(grid_elements[x, y].element_transform.gameObject);
        }

        grid_elements[x, y].element_transform = null;
        grid_elements[x, y].element_type = null;
    }

    public void RemoveElementsAtIndices(List<Vector2> grid_indices)
    {
        for (int i = 0; i < grid_indices.Count; i++)
        {
            RemoveElementAtIndex(grid_indices[i]);
        }

        FillGrid(new Vector2(0, -1f));
    }

    public void RemoveAllElements()
    {
        for (int x = 0; x < grid_elements.GetLength(0); x++)
        {
            for (int y = 0; y < grid_elements.GetLength(1); y++)
            {
                Destroy(grid_elements[x, y].element_transform.gameObject);
            }
        }

        grid_elements = new GridElementData[grid_width, grid_height];
    }

    private bool CheckForEmptyGridIndices()
    {
        for (int x = 0; x < grid_elements.GetLength(0); x++)
        {
            for (int y = 0; y < grid_elements.GetLength(1); y++)
            {
                if (grid_elements[x, y].element_transform == null)
                {
                    return true;
                }
            }
        }

        return false;
    }

    public void FillGrid(Vector2 fall_direction, bool full_spawn = false)
    {
        float spawn_offset_per_row = 1.25f;
        float spawn_offset_per_count = 0.05f;
        int spawn_count = 0;
        bool offset_individual_spawns = true;

        while (CheckForEmptyGridIndices())
        {
            //Find all empty indices
            for (int x = grid_elements.GetLength(0) - 1; x >= 0; x--)
            {
                for (int y = grid_elements.GetLength(1) - 1; y >= 0; y--)
                {
                    if (grid_elements[x, y].element_transform == null)
                    {
                        //Find the correct index for the element to descend
                        Vector3 correct_world_pos = CalculateWorldPos(new Vector2(x, y)); //grid_elements[x, y].correct_world_pos;
                        Vector3 fall_direction_3d_normalized = new Vector3(fall_direction.x, 0, fall_direction.y).normalized;
                        Vector3 descending_element_world_pos = correct_world_pos - fall_direction_3d_normalized * hex_height;
                        Vector2 descending_element_index = GetGridIndexFromWorldPosition(descending_element_world_pos);

                        //print(x + "|" + y + " correct_world_pos: " + correct_world_pos + ", fall_direction_3d_normalized: " + fall_direction_3d_normalized
                        //    + ", hex_height: " + hex_height);
                        //print("descending_element_world_pos: " + descending_element_world_pos + ", descending_element_index: " + descending_element_index);
                        if (descending_element_index.x >= 0 && descending_element_index.x < grid_elements.GetLength(0)
                            && descending_element_index.y >= 0 && descending_element_index.y < grid_elements.GetLength(1))
                        {
                            //"Drop" elements above the empty indices to fill the empty ones
                            GridElementData grid_element = grid_elements[(int)descending_element_index.x, (int)descending_element_index.y];
                            if (!(full_spawn && grid_element.element_transform == null))
                            {
                                grid_elements[x, y] = grid_element;
                                grid_elements[x, y].correct_world_pos = correct_world_pos;
                                RemoveElementAtIndex(descending_element_index, false);
                                //print("Dropped an element from " + descending_element_index + " to fill an empty index at " + x + "|" + y);
                                continue;
                            }
                        }


                        //Count the number of elements spawned on the same column on this turn
                        //TODO: Modify this to work with any fall direction (currently only works with a fall direction where x == 0 && y < 0)
                        int number_of_newly_spawned_elements_under_this_one = 0;
                        for (int y_2 = y + 1; y_2 < grid_elements.GetLength(1); y_2++)
                        {
                            GridElementData grid_element = grid_elements[x, y_2];
                            if (grid_element.element_transform != null)
                            {
                                if(grid_element.just_spawned)
                                {
                                    number_of_newly_spawned_elements_under_this_one++;
                                }
                            }
                        }

                        //Calculate the proper spawn position
                        Vector3 spawn_pos_offset = -fall_direction_3d_normalized * hex_height
                            * (number_of_newly_spawned_elements_under_this_one + 1);

                        if (offset_individual_spawns)
                        {
                            spawn_pos_offset  *= (1 + (spawn_offset_per_count * spawn_count)); //+= spawn_pos_offset
                        }
                        if (!full_spawn)
                        {
                            spawn_pos_offset *= Mathf.Pow(spawn_offset_per_row, number_of_newly_spawned_elements_under_this_one + 1);
                        }

                        descending_element_world_pos = correct_world_pos + spawn_pos_offset;

                        //print("FillGrid: Creating new element at " + x + "|" + y + ", spawn_pos: " + descending_element_world_pos 
                        //    + ", number_of_newly_spawned_elements_under_this_one: " + number_of_newly_spawned_elements_under_this_one);

                        CreateNewGridElement(ChooseRandomElementType(), descending_element_world_pos, new Vector2(x, y), this.transform);
                        spawn_count++;
                    }
                }
            }
        }

        StartCoroutine(MoveAllDroppingElementsTowardsCorrectWorldPositions());
    }

    public void Restart()
    {
        spawn_hex_bases = false;
        RemoveAllElements();
        CreateGrid();
    }

    public void SpawnSelectionEffectAtIndex(Vector2 grid_index)
    {
        //TODO: Pool management

        Vector3 spawn_pos = GetElementDataFromIndex(grid_index).correct_world_pos;
        spawn_pos.y = selection_effect_height;
        GameObject new_effect = Instantiate(selection_effect_prefab);
        new_effect.transform.position = spawn_pos;
        new_effect.transform.SetParent(GetElementDataFromIndex(grid_index).element_transform);

        selection_effect_infos.Add(new SelectionEffectInfo(grid_index, new_effect));
    }

    public void ClearSelectionEffectAtIndex(Vector2 grid_index)
    {
        for (int i = 0; i < selection_effect_infos.Count; i++)
        {
            if (selection_effect_infos[i].grid_index == grid_index)
            {
                if (selection_effect_infos[i].selection_effect != null)
                    Destroy(selection_effect_infos[i].selection_effect);
                selection_effect_infos.RemoveAt(i);
                print("Removed selection effect at index: " + grid_index);
                break;
            }
        }
    }

    public void ClearAllSelectionEffects()
    {
        while (selection_effect_infos.Count > 0)
        {
            if (selection_effect_infos[0].selection_effect != null)
                Destroy(selection_effect_infos[0].selection_effect);
            selection_effect_infos.RemoveAt(0);
        }
    }
}

public struct GridElementData
{
    public ElementType element_type;
    public Transform element_transform;
    public Vector3 correct_world_pos;
    public bool just_spawned;

    public GridElementData(ElementType _element_type, Transform _element_transform, Vector3 _correct_world_pos)
    {
        element_type = _element_type;
        element_transform = _element_transform;
        correct_world_pos = _correct_world_pos;
        just_spawned = true;
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
