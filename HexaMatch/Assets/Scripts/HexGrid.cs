using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HexGrid : MonoBehaviour
{
    // Original source: https://www.youtube.com/watch?v=konL0iB5gPI

    //TODO: Correct grid start_pos calculations (7x6 grid is centered correctly, 6x5 is not?)

    public delegate void ListListIntVector2(List<List<IntVector2>> list_list_vec2);
    public event ListListIntVector2 OnAutoMatchesFound;

    public Transform hex_base_prefab;
    public ElementType[] element_types;

    public string grid_element_transform_pool_tag;

    public int grid_width = 6;
    public int grid_height = 6;
    public int min_viable_connection = 2;
    public int min_auto_match_connection = 10;

    public float gap = 0.1f;
    public float min_new_element_spawn_y_pos = 2f;

    public bool spawn_hex_bases = true;
    public bool offset_individual_spawns = true;

    private GridElementData[,] grid_elements;
    private PoolManager pool_manager;
    private EffectManager effect_manager;

    private Coroutine element_movement;
    private Vector3 start_pos;
    private Vector3 fall_direction = Vector3.down; //TODO: Currently changing fall direction causes in infinite loop resulting in a freeze
                                                   //(So keep fall_direction as Vector3.down)
                                                   //Fix if necessary

    private float hex_base_z_pos = 0.25f;
    private float hex_width = 1f;
    private float hex_height = 0.866f;
    private float element_width = 0.725f;
    private float spawn_offset_per_row = 1.25f;
    private float spawn_offset_per_count = 0.05f;

    private bool is_element_movement_done = true;

    private void Start()
    {
        pool_manager = GetComponent<PoolManager>();
        effect_manager = GetComponent<EffectManager>();

        fall_direction.Normalize();
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
        float x, y = 0;

        float y_offset = (grid_width / 2 % 2 == 0) ? 0 : hex_height / 2;

        x = -hex_width * 0.75f * (grid_width / 2f);
        y = hex_height * (grid_height / 2f) - y_offset;

        start_pos = new Vector3(x, y, 0);
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
                SpawnHexBaseTile(new IntVector2(x, y));
            }
        }
    }

    private void InitializeGridElements()
    {
        grid_elements = new GridElementData[grid_width, grid_height];

        FillGrid(true);
        ClearGridOfAutoMatches();
        RecalculateSpawnPositions(); ;
        MoveElementsToCorrectPositions();
    }

    private void SpawnHexBaseTile(IntVector2 grid_index)
    {
        Transform hex = Instantiate(hex_base_prefab) as Transform;
        Vector3 spawn_pos = CalculateWorldPos(grid_index);
        spawn_pos.z = hex_base_z_pos;
        hex.position = spawn_pos;
        hex.eulerAngles = new Vector3(-90f, 0, 0);

        hex.parent = this.transform;
        hex.name = "HexTile " + grid_index.x + "|" + grid_index.y;
    }

    private void CreateNewGridElement(ElementType element_type, Vector3 spawn_pos, IntVector2 grid_index, Transform parent)
    {
        GameObject element = pool_manager.SpawnFromPool(grid_element_transform_pool_tag);
        element.GetComponentInChildren<Renderer>().material = element_type.element_material;
        element.transform.position = spawn_pos;
        element.transform.parent = parent;
        element.SetActive(true);
        grid_elements[grid_index.x, grid_index.y] = new GridElementData(element_type, element.transform, CalculateWorldPos(grid_index));
    }

    private ElementType ChooseRandomElementType()
    {
        return element_types[Random.Range(0, element_types.Length)];
    }

    private List<List<IntVector2>> FindMatchesOfElementType(ElementType element_type)
    {
        List<List<IntVector2>> all_matching_element_indices = new List<List<IntVector2>>();
        List<IntVector2> indices_already_checked = new List<IntVector2>();

        //Loop through grid elements
        for (int x = 0; x < grid_elements.GetLength(0); x++)
        {
            for (int y = 0; y < grid_elements.GetLength(1); y++)
            {
                if (grid_elements[x, y].flagged_for_removal_by_auto_match) continue;
                if (indices_already_checked.Contains(new IntVector2(x, y))) continue;

                //If the element is of matching type with the element to check against
                if (IsOfMatchingElementType(element_type, grid_elements[x, y].element_type))
                {
                    //print("Found element matching with the type to check at " + x + "|" + y);
                    //Store the matching neighbours on a one list
                    List<IntVector2> matching_neighbours = new List<IntVector2>();
                    //And the matching neighbours whose neighbours we have yet to check, on another list
                    List<IntVector2> matching_neighbours_to_check = new List<IntVector2>();
                    //Add the current element as the first item on both lists
                    matching_neighbours.Add(new IntVector2(x, y));
                    matching_neighbours_to_check.Add(new IntVector2(x, y));

                    //Check neighbours of all the neighbouring matches, until no unchecked matching neighbours are left
                    while (matching_neighbours_to_check.Count > 0)
                    {
                        if (!indices_already_checked.Contains(matching_neighbours_to_check[0]))
                            indices_already_checked.Add(matching_neighbours_to_check[0]);

                        List<IntVector2> neighbouring_indices = GetNeighbouringIndices(matching_neighbours_to_check[0]);

                        for (int i = 0; i < neighbouring_indices.Count; i++)
                        {
                            if (grid_elements[neighbouring_indices[i].x, neighbouring_indices[i].y].flagged_for_removal_by_auto_match) continue;

                            if (IsOfMatchingElementType(element_type, grid_elements[neighbouring_indices[i].x, neighbouring_indices[i].y].element_type))
                            {
                                if (!matching_neighbours.Contains(neighbouring_indices[i]))
                                {
                                    matching_neighbours.Add(neighbouring_indices[i]);
                                    matching_neighbours_to_check.Add(neighbouring_indices[i]);
                                    grid_elements[neighbouring_indices[i].x, neighbouring_indices[i].y].flagged_for_removal_by_auto_match = true;
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

    private void RecalculateSpawnPositions()
    {
        int spawn_count = 0;

        for (int x = grid_elements.GetLength(0) - 1; x >= 0; x--)
        {
            for (int y = grid_elements.GetLength(1) - 1; y >= 0; y--)
            {
                int number_of_elements_under_this_one = grid_elements.GetLength(1) - (y + 1);

                Vector3 spawn_pos_offset = -fall_direction * hex_height
                    * (number_of_elements_under_this_one + 1 + grid_elements.GetLength(1));

                if (offset_individual_spawns)
                {
                    spawn_pos_offset *= (1 + (spawn_offset_per_count * spawn_count));
                }

                spawn_pos_offset.y += min_new_element_spawn_y_pos;
                Vector3 spawn_pos = CalculateWorldPos(new IntVector2(x, y)) + spawn_pos_offset;

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
            FillGrid();
        }
    }

    private void ElementMovementFinished()
    {
        if (RemoveExistingMatches() > 0)
        {
            //print("Removed more matches; filling grid and starting drop animations again.");
            FillGrid();
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

        List<IntVector2> indices_to_move = new List<IntVector2>();

        for (int x = 0; x < grid_elements.GetLength(0); x++)
        {
            for (int y = 0; y < grid_elements.GetLength(1); y++)
            {
                if (grid_elements[x, y].element_transform.position != grid_elements[x, y].correct_world_pos)
                {
                    indices_to_move.Add(new IntVector2(x, y));
                }
            }
        }

        //print("Indices to move count " + indices_to_move.Count);

        while (indices_to_move.Count > 0)
        {
            for (int i = 0; i < indices_to_move.Count; i++)
            {
                GridElementData element_to_move = grid_elements[indices_to_move[i].x, indices_to_move[i].y];

                Vector3 direction_to_correct_pos = element_to_move.correct_world_pos - element_to_move.element_transform.position;
                float distance_to_correct_pos = direction_to_correct_pos.magnitude;

                if (distance_to_correct_pos <= movement_speed * Time.deltaTime)
                {
                    element_to_move.element_transform.position = element_to_move.correct_world_pos;
                    grid_elements[indices_to_move[i].x, indices_to_move[i].y].just_spawned = false;
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

    public List<IntVector2> FindMatchesForIndex(IntVector2 grid_index)
    {
        ElementType element_type = grid_elements[grid_index.x, grid_index.y].element_type;

        //Store the matching neighbours on a one list
        List<IntVector2> matching_neighbours = new List<IntVector2>();
        //And the matching neighbours whose neighbours we have yet to check, on another list
        List<IntVector2> matching_neighbours_to_check = new List<IntVector2>();
        //Add the current element as the first item on both lists
        matching_neighbours.Add(grid_index);
        matching_neighbours_to_check.Add(grid_index);

        //Check neighbours of all the neighbouring matches, until no unchecked matching neighbours are left
        while (matching_neighbours_to_check.Count > 0)
        {
            List<IntVector2> neighbouring_indices = GetNeighbouringIndices(matching_neighbours_to_check[0]);

            for (int i = 0; i < neighbouring_indices.Count; i++)
            {
                if (IsOfMatchingElementType(element_type, grid_elements[neighbouring_indices[i].x, neighbouring_indices[i].y].element_type))
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

    public void SwapElements(IntVector2 a_index, IntVector2 b_index)
    {
        GridElementData old_a = grid_elements[a_index.x, a_index.y];
        grid_elements[a_index.x, a_index.y] = grid_elements[b_index.x, b_index.y];
        grid_elements[b_index.x, b_index.y] = old_a;

        grid_elements[a_index.x, a_index.y].correct_world_pos = CalculateWorldPos(a_index);
        grid_elements[b_index.x, b_index.y].correct_world_pos = CalculateWorldPos(b_index);
    }

    public void ResetElementWorldPos(IntVector2 grid_index)
    {
        GridElementData element = grid_elements[grid_index.x, grid_index.y];
        if (element.element_transform != null)
        {
            element.correct_world_pos = CalculateWorldPos(grid_index);
            element.element_transform.transform.position = element.correct_world_pos;
            element.element_transform.transform.parent = this.transform;
        }
    }

    public Vector3 CalculateWorldPos(IntVector2 grid_pos)
    {
        float x, y = 0;

        float y_offset = (grid_pos.x % 2 == 0) ? 0 : hex_height / 2;

        x = start_pos.x + grid_pos.x * hex_width * 0.75f;
        y = start_pos.y - grid_pos.y * hex_height + y_offset;

        return new Vector3(x, y, 0);
    }

    public IntVector2 GetGridIndexFromWorldPosition(Vector3 world_pos, bool limit_to_element_width = false)
    {
        for (int x = 0; x < grid_elements.GetLength(0); x++)
        {
            for (int y = 0; y < grid_elements.GetLength(1); y++)
            {
                Vector3 grid_world_pos = CalculateWorldPos(new IntVector2(x, y));
                float half_area_size = limit_to_element_width ? element_width / 2 : (hex_height - gap) / 2;
                float x_min = grid_world_pos.x - half_area_size;
                float x_max = grid_world_pos.x + half_area_size;
                float y_min = grid_world_pos.y - half_area_size;
                float y_max = grid_world_pos.y + half_area_size;

                if (world_pos.x >= x_min && world_pos.x <= x_max && world_pos.y >= y_min && world_pos.y <= y_max)
                {
                    IntVector2 matching_grid_index = new IntVector2(x, y);
                    return matching_grid_index;
                }
            }
        }

        return new IntVector2(-1, -1);
    }

    public GridElementData GetGridElementDataFromIndex(IntVector2 grid_index)
    {
        return grid_elements[grid_index.x, grid_index.y];
    }

    public List<IntVector2> GetNeighbouringIndices(IntVector2 grid_index)
    {
        List<IntVector2> neighbours = new List<IntVector2>();

        for (int x = grid_index.x - 1; x <= grid_index.x + 1; x++)
        {
            if (x < 0 || x >= grid_elements.GetLength(0))
                continue;


            for (int y = grid_index.y - 1; y <= grid_index.y + 1; y++)
            {
                if (y < 0 || y >= grid_elements.GetLength(1))
                    continue;

                if (CheckIfNeighbours(grid_index, new IntVector2(x, y)))
                    neighbours.Add(new IntVector2(x, y));
            }
        }

        //print("Element " + index_x + "|" + index_y + " neighbour count: " + neighbours.Count);

        return neighbours;
    }

    public bool CheckIfNeighbours(IntVector2 a, IntVector2 b)
    {
        if (b.x >= a.x - 1 && b.x <= a.x + 1 && b.y >= a.y - 1 && b.y <= a.y + 1)
        {
            if (b.x == a.x && b.y == a.y) return false;

            //Is the index on an even column?
            if (a.x % 2 == 0)
            {
                if ((b.x == a.x - 1 || b.x == a.x + 1) && b.y == a.y - 1) return false;
            }
            else if ((b.x == a.x - 1 || b.x == a.x + 1) && b.y == a.y + 1) return false;

            return true;
        }

        return false;
    }

    public int RemoveExistingMatches(bool ignore_callback_event = false, bool spawn_collection_effects = true)
    {
        List<List<IntVector2>> match_indices = new List<List<IntVector2>>();
        for (int i = 0; i < element_types.Length; i++)
        {
            match_indices.AddRange(FindMatchesOfElementType(element_types[i]));
        }

        //Reset flagged_for_removal_by_auto_match flags
        for (int x = 0; x < grid_elements.GetLength(0); x++)
        {
            for (int y = 0; y < grid_elements.GetLength(1); y++)
            {
                grid_elements[x, y].flagged_for_removal_by_auto_match = false;
            }
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

    public void RemoveElementAtIndex(IntVector2 grid_index, bool disable_element_transform = true, bool spawn_collection_effect = true)
    {
        if (spawn_collection_effect)
        {
            effect_manager.SpawnCollectionEffectOnIndex(grid_index);
        }

        if (disable_element_transform)
        {
            grid_elements[grid_index.x, grid_index.y].element_transform.gameObject.SetActive(false);
        }

        grid_elements[grid_index.x, grid_index.y].element_transform = null;
        grid_elements[grid_index.x, grid_index.y].element_type = null;
    }

    public void RemoveElementsAtIndices(List<IntVector2> grid_indices, bool spawn_collection_effects = true)
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

    public void FillGrid(bool full_spawn = false)
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
                        Vector3 correct_world_pos = CalculateWorldPos(new IntVector2(x, y));
                        Vector3 descending_element_world_pos = correct_world_pos - fall_direction * hex_height;
                        IntVector2 descending_element_index = (y - 1 >= 0)
                            ? new IntVector2(x, y - 1)
                            : GetGridIndexFromWorldPosition(descending_element_world_pos);

                        //print(x + "|" + y + " correct_world_pos: " + correct_world_pos + ", fall_direction_3d_normalized: " + fall_direction_3d_normalized
                        //    + ", hex_height: " + hex_height);
                        //print("descending_element_world_pos: " + descending_element_world_pos + ", descending_element_index: " + descending_element_index);
                        if (descending_element_index.x >= 0 && descending_element_index.x < grid_elements.GetLength(0)
                            && descending_element_index.y >= 0 && descending_element_index.y < grid_elements.GetLength(1))
                        {
                            //"Drop" elements above the empty indices to fill the empty ones
                            GridElementData grid_element = grid_elements[descending_element_index.x, descending_element_index.y];
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
                        Vector3 spawn_pos_offset = -fall_direction * hex_height
                            * (number_of_newly_spawned_elements_under_this_one + 1);

                        if (!full_spawn)
                        {
                            spawn_pos_offset *= Mathf.Pow(spawn_offset_per_row, number_of_newly_spawned_elements_under_this_one + 1);
                        }

                        spawn_pos_offset.y += min_new_element_spawn_y_pos;
                        descending_element_world_pos = correct_world_pos + spawn_pos_offset;

                        //print("FillGrid: Creating new element at " + x + "|" + y + ", spawn_pos: " + descending_element_world_pos 
                        //    + ", number_of_newly_spawned_elements_under_this_one: " + number_of_newly_spawned_elements_under_this_one);

                        CreateNewGridElement(ChooseRandomElementType(), descending_element_world_pos, new IntVector2(x, y), this.transform);
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
    public bool flagged_for_removal_by_auto_match;

    public GridElementData(ElementType _element_type, Transform _element_transform, Vector3 _correct_world_pos)
    {
        element_type = _element_type;
        element_transform = _element_transform;
        correct_world_pos = _correct_world_pos;
        just_spawned = true;
        flagged_for_removal_by_auto_match = false;
    }
}

public class IntVector2
{
    public int x, y;

    public IntVector2(int _x, int _y)
    {
        x = _x;
        y = _y;
    }

    public override int GetHashCode()
    {
        return x ^ y;
    }

    public override bool Equals(object obj)
    {
        if (obj == null) return false;

        IntVector2 iv2 = obj as IntVector2;
        if ((object)iv2 == null) return false;

        return Equals(iv2);
    }

    public bool Equals(IntVector2 other)
    {
        return (x == other.x && y == other.y);
    }

    public static bool operator ==(IntVector2 a, IntVector2 b)
    {
        return (a.x == b.x && a.y == b.y);
    }

    public static bool operator !=(IntVector2 a, IntVector2 b)
    {
        return (a.x != b.x || a.y != b.y);
    }

}

