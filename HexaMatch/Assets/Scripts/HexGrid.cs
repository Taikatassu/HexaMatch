using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HexGrid : MonoBehaviour
{
    // Original source: https://www.youtube.com/watch?v=konL0iB5gPI

    //TODO: Implement more effects, both visual and audio (match collection, points received, etc.)

    //TODO: Correct grid start_pos calculations (7x6 grid is centered correctly, 6x5 is not?)

    public delegate void ListListVector2(List<List<Vector2>> list_list_vec2);
    public event ListListVector2 OnAutoMatchesFound;

    public Transform hex_base_prefab;
    public ElementType[] element_types;

    public string grid_element_transform_pool_tag;

    public int grid_width = 6;
    public int grid_height = 6;
    public int min_viable_connection = 2;
    public int min_auto_match_connection = 10;

    public float gap = 0.1f;
    public float min_new_element_spawn_height = 2f;

    public bool spawn_hex_bases = true;
    public bool offset_individual_spawns = true;

    private float hex_base_y_pos = -0.25f;
    private float hex_width = 1f;
    private float hex_height = 0.866f;
    private float element_width = 0.725f;
    private float spawn_offset_per_row = 1.25f;
    private float spawn_offset_per_count = 0.05f;

    private bool is_element_movement_done = true;

    private GridElementData[,] grid_elements;
    private PoolManager pool_manager;
    private EffectManager effect_manager;

    private Coroutine element_movement;
    private Vector3 start_pos;

    private void Start()
    {
        pool_manager = GetComponent<PoolManager>();
        effect_manager = GetComponent<EffectManager>();

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
        //print("Start_pos: " + start_pos);
    }

    private void CreateGrid()
    {
        is_element_movement_done = false;

        if (spawn_hex_bases)
        {
            CreateGridBase();
        }

        InitializeGridElements();
    }

    private void CreateGridBase()
    {
        for (int x = 0; x < grid_width; x++)
        {
            for (int y = 0; y < grid_height; y++)
            {
                SpawnHexBaseTile(x, y);
            }
        }
    }

    private void InitializeGridElements()
    {
        grid_elements = new GridElementData[grid_width, grid_height];

        FillGrid(new Vector2(0, -1f), true);
        ClearGridOfAutoMatches();
        RecalculateSpawnPositions(new Vector2(0, -1f));
        MoveElementsToCorrectPositions();
    }

    private void SpawnHexBaseTile(int grid_index_x, int grid_index_y)
    {
        Transform hex = Instantiate(hex_base_prefab) as Transform;
        Vector3 spawn_pos = CalculateWorldPos(new Vector2(grid_index_x, grid_index_y));
        spawn_pos.y = hex_base_y_pos;
        hex.position = spawn_pos;

        hex.parent = this.transform;
        hex.name = "HexTile " + grid_index_x + "|" + grid_index_y;
    }

    private void CreateNewGridElement(ElementType element_type, Vector3 spawn_pos, Vector2 grid_index, Transform parent)
    {
        GameObject element = pool_manager.SpawnFromPool(grid_element_transform_pool_tag); //Instantiate(element_type.element_prefab).transform;
        element.GetComponentInChildren<Renderer>().material = element_type.element_material;
        element.transform.position = spawn_pos;
        element.transform.parent = parent;
        element.SetActive(true);
        grid_elements[(int)grid_index.x, (int)grid_index.y] = new GridElementData(element_type, element.transform, CalculateWorldPos(grid_index));
    }

    private ElementType ChooseRandomElementType()
    {
        return element_types[Random.Range(0, element_types.Length)];
    }

    private List<List<Vector2>> FindMatchesOfElementType(ElementType element_type)
    {
        List<List<Vector2>> all_matching_element_indices = new List<List<Vector2>>();
        List<Vector2> indices_already_checked = new List<Vector2>();

        //Loop through grid elements
        for (int x = 0; x < grid_elements.GetLength(0); x++)
        {
            for (int y = 0; y < grid_elements.GetLength(1); y++)
            {
                if (indices_already_checked.Contains(new Vector2(x, y)))
                {
                    //print("Skipping index already checked at " + x + "|" + y);
                    continue;
                }

                //If the element is of matching type with the element to check against
                if (IsOfMatchingElementType(element_type, grid_elements[x, y].element_type))
                {
                    //print("Found element matching with the type to check at " + x + "|" + y);
                    //Store the matching neighbours on a one list
                    List<Vector2> matching_neighbours = new List<Vector2>();
                    //And the matching neighbours whose neighbours we have yet to check, on another list
                    List<Vector2> matching_neighbours_to_check = new List<Vector2>();
                    //Add the current element as the first item on both lists
                    matching_neighbours.Add(new Vector2(x, y));
                    matching_neighbours_to_check.Add(new Vector2(x, y));

                    //Check neighbours of all the neighbouring matches, until no unchecked matching neighbours are left
                    while (matching_neighbours_to_check.Count > 0)
                    {
                        if (!indices_already_checked.Contains(matching_neighbours_to_check[0]))
                            indices_already_checked.Add(matching_neighbours_to_check[0]);

                        List<Vector2> neighbouring_indices = GetNeighbouringIndices(matching_neighbours_to_check[0]);

                        for (int i = 0; i < neighbouring_indices.Count; i++)
                        {
                            if (IsOfMatchingElementType(element_type, grid_elements[(int)neighbouring_indices[i].x, (int)neighbouring_indices[i].y].element_type))
                            {
                                if (!matching_neighbours.Contains(neighbouring_indices[i]))
                                {
                                    matching_neighbours.Add(neighbouring_indices[i]);
                                    matching_neighbours_to_check.Add(neighbouring_indices[i]);
                                }
                            }
                        }

                        //print("Removing matching neighbour at index " + matching_neighbours_to_check[0] + " to check from list.");
                        matching_neighbours_to_check.RemoveAt(0);
                    }

                    if (matching_neighbours.Count >= min_auto_match_connection)
                    {
                        //print("Connected matches of index " + x + "|" + y + " checked, adding " + matching_neighbours.Count + " indices to the match list.");
                        all_matching_element_indices.Add(matching_neighbours);
                    }
                    //else
                    //{
                    //    print("Connected matches of index " + x + "|" + y + " checked, connected element count not big enough for match, ignoring indices (matching_neighbours.Count: " + matching_neighbours.Count + ").");
                    //}
                }
            }
        }

        return all_matching_element_indices;
    }

    private bool IsOfMatchingElementType(ElementType main_type, ElementType other_type)
    {
        if (other_type == main_type)
        {
            return true;
        }

        for (int i = 0; i < main_type.matching_elements.Length; i++)
        {
            if (other_type == main_type.matching_elements[i])
            {
                return true;
            }
        }

        return false;
    }

    private void RecalculateSpawnPositions(Vector2 fall_direction)
    {
        Vector3 fall_direction_3d_normalized = new Vector3(fall_direction.x, 0, fall_direction.y).normalized;

        int spawn_count = 0;

        for (int x = grid_elements.GetLength(0) - 1; x >= 0; x--)
        {
            for (int y = grid_elements.GetLength(1) - 1; y >= 0; y--)
            {
                int number_of_elements_under_this_one = grid_elements.GetLength(1) - (y + 1);

                Vector3 spawn_pos_offset = -fall_direction_3d_normalized * hex_height
                    * (number_of_elements_under_this_one + 1 + grid_elements.GetLength(1));

                if (offset_individual_spawns)
                {
                    spawn_pos_offset *= (1 + (spawn_offset_per_count * spawn_count));
                }

                spawn_pos_offset.z += min_new_element_spawn_height;
                Vector3 spawn_pos = CalculateWorldPos(new Vector2(x, y)) + spawn_pos_offset;

                grid_elements[x, y].element_transform.position = spawn_pos;
                spawn_count++;
            }
        }
    }

    private void ClearGridOfAutoMatches()
    {
        while (RemoveExistingMatches(true, false) > 0)
        {
            //print("Found and removed auto-matches; filling grid and redoing.");
            FillGrid(new Vector2(0, -1f));
        }
    }

    private void ElementMovementFinished()
    {
        if (RemoveExistingMatches() > 0)
        {
            //print("Removed more matches; filling grid and starting drop animations again.");
            FillGrid(new Vector2(0, -1f));
            MoveElementsToCorrectPositions();
        }
        else
        {
            is_element_movement_done = true;
        }
    }

    private IEnumerator MoveAllElementsTowardsCorrectWorldPositions(float movement_speed_increment_multiplier = 1f)
    {
        //print("Starting drop 'animations'.");
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

        //print("Indices to move count " + indices_to_move.Count);

        while (indices_to_move.Count > 0)
        {
            for (int i = 0; i < indices_to_move.Count; i++)
            {
                GridElementData element_to_move = grid_elements[(int)indices_to_move[i].x, (int)indices_to_move[i].y];

                Vector3 direction_to_correct_pos = element_to_move.correct_world_pos - element_to_move.element_transform.position;
                float distance_to_correct_pos = direction_to_correct_pos.magnitude;

                if (distance_to_correct_pos <= movement_speed * Time.deltaTime)
                {
                    element_to_move.element_transform.position = element_to_move.correct_world_pos;
                    grid_elements[(int)indices_to_move[i].x, (int)indices_to_move[i].y].just_spawned = false;
                    //print("Element at " + indices_to_move[i] + " has arrived at correct world pos. indices_to_move.Count: "
                    //    + indices_to_move.Count + ", i: " + i);
                    indices_to_move.RemoveAt(i);
                    i--;
                }
                else
                {
                    direction_to_correct_pos /= distance_to_correct_pos;
                    element_to_move.element_transform.position += direction_to_correct_pos * movement_speed * Time.deltaTime;
                }
            }

            movement_speed += movement_speed_increment_per_second * movement_speed_increment_multiplier * Time.deltaTime;

            yield return new WaitForEndOfFrame();
        }

        //print("Finished drop 'animations'.");
        ElementMovementFinished();
    }

    public void MoveElementsToCorrectPositions(float movement_speed_increment_multiplier = 1f)
    {
        is_element_movement_done = false;

        if (element_movement != null)
        {
            StopCoroutine(element_movement);
        }

        element_movement = StartCoroutine(MoveAllElementsTowardsCorrectWorldPositions(movement_speed_increment_multiplier));
    }

    public bool GetIsElementMovementDone()
    {
        return is_element_movement_done;
    }

    public List<Vector2> FindMatchesForIndex(Vector2 grid_index)
    {
        ElementType element_type = grid_elements[(int)grid_index.x, (int)grid_index.y].element_type;

        //Store the matching neighbours on a one list
        List<Vector2> matching_neighbours = new List<Vector2>();
        //And the matching neighbours whose neighbours we have yet to check, on another list
        List<Vector2> matching_neighbours_to_check = new List<Vector2>();
        //Add the current element as the first item on both lists
        matching_neighbours.Add(grid_index);
        matching_neighbours_to_check.Add(grid_index);

        //Check neighbours of all the neighbouring matches, until no unchecked matching neighbours are left
        while (matching_neighbours_to_check.Count > 0)
        {
            List<Vector2> neighbouring_indices = GetNeighbouringIndices(matching_neighbours_to_check[0]);

            for (int i = 0; i < neighbouring_indices.Count; i++)
            {
                if (IsOfMatchingElementType(element_type, grid_elements[(int)neighbouring_indices[i].x, (int)neighbouring_indices[i].y].element_type))
                {
                    if (!matching_neighbours.Contains(neighbouring_indices[i]))
                    {
                        matching_neighbours.Add(neighbouring_indices[i]);
                        matching_neighbours_to_check.Add(neighbouring_indices[i]);
                    }
                }
            }

            matching_neighbours_to_check.RemoveAt(0);
        }

        return matching_neighbours;
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

        //ResetElementWorldPos(a_index);
        //ResetElementWorldPos(b_index);

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
    
    public GridElementData GetGridElementDataFromIndex(Vector2 grid_index)
    {
        return grid_elements[(int)grid_index.x, (int)grid_index.y];
    }

    public List<Vector2> GetNeighbouringIndices(Vector2 grid_index)
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

        //print("Element " + index_x + "|" + index_y + " neighbour count: " + neighbours.Count);

        return neighbours;
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

    public int RemoveExistingMatches(bool ignore_callback_event = false, bool spawn_collection_effects = true)
    {
        List<List<Vector2>> match_indices = new List<List<Vector2>>();
        for (int i = 0; i < element_types.Length; i++)
        {
            match_indices.AddRange(FindMatchesOfElementType(element_types[i]));
        }

        if (!ignore_callback_event && OnAutoMatchesFound != null)
            OnAutoMatchesFound(match_indices);

        int removed_elements_count = 0;
        for (int j = 0; j < match_indices.Count; j++)
        {
            RemoveElementsAtIndices(match_indices[j], spawn_collection_effects);
            removed_elements_count += match_indices[j].Count;
        }
        //print("Removed " + removed_elements_count + " elements due to auto-matching.");

        return match_indices.Count;
    }

    public void RemoveElementAtIndex(Vector2 grid_index, bool disable_element_transform = true, bool spawn_collection_effect = true)
    {
        int x = (int)grid_index.x;
        int y = (int)grid_index.y;

        if (spawn_collection_effect)
        {
            effect_manager.SpawnCollectionEffectOnIndex(grid_index);
        }

        if (disable_element_transform)
        {
            grid_elements[x, y].element_transform.gameObject.SetActive(false);
        }

        grid_elements[x, y].element_transform = null;
        grid_elements[x, y].element_type = null;
    }

    public void RemoveElementsAtIndices(List<Vector2> grid_indices, bool spawn_collection_effects = true)
    {
        for (int i = 0; i < grid_indices.Count; i++)
        {
            RemoveElementAtIndex(grid_indices[i], spawn_collection_effect: spawn_collection_effects);
        }
    }

    public void RemoveAllElements()
    {
        for (int x = 0; x < grid_elements.GetLength(0); x++)
        {
            for (int y = 0; y < grid_elements.GetLength(1); y++)
            {
                grid_elements[x, y].element_transform.gameObject.SetActive(false);
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
        int spawn_count = 0;

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
                                RemoveElementAtIndex(descending_element_index, false, false);
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
                                if (grid_element.just_spawned)
                                {
                                    number_of_newly_spawned_elements_under_this_one++;
                                }
                            }
                        }

                        //Calculate the proper spawn position
                        Vector3 spawn_pos_offset = -fall_direction_3d_normalized * hex_height
                            * (number_of_newly_spawned_elements_under_this_one + 1);

                        //if (offset_individual_spawns)
                        //{
                        //    spawn_pos_offset *= (1 + (spawn_offset_per_count * spawn_count)); //+= spawn_pos_offset
                        //}
                        if (!full_spawn)
                        {
                            spawn_pos_offset *= Mathf.Pow(spawn_offset_per_row, number_of_newly_spawned_elements_under_this_one + 1);
                        }

                        spawn_pos_offset.z += min_new_element_spawn_height;
                        descending_element_world_pos = correct_world_pos + spawn_pos_offset;

                        //print("FillGrid: Creating new element at " + x + "|" + y + ", spawn_pos: " + descending_element_world_pos 
                        //    + ", number_of_newly_spawned_elements_under_this_one: " + number_of_newly_spawned_elements_under_this_one);

                        CreateNewGridElement(ChooseRandomElementType(), descending_element_world_pos, new Vector2(x, y), this.transform);
                        spawn_count++;
                    }
                }
            }
        }
    }

    public void Restart()
    {
        is_element_movement_done = false;

        RemoveAllElements();
        InitializeGridElements();
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

