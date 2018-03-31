using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class InputManager : MonoBehaviour
{
    public enum ESelectionMode
    {
        SWAP,
        CONNECT
    }

    public ESelectionMode selection_mode = ESelectionMode.SWAP;

    public Material valid_selection_material;
    public Material invalid_selection_material;
    public Button restart_button;
    public Text score_text;
    public Text moves_text;

    HexGrid grid;
    ElementType selected_element_type;
    LineRenderer selection_line;
    Transform selected_element_transform;
    List<Vector2> selected_element_indices;
    Vector2 last_grid_index_to_mouse_over;
    Vector3 last_mouse_pos;

    public float selection_line_height = 1f;

    float swap_movement_speed_increment_multiplier = 8f;
    int score = 0;
    int moves = 0;
    bool invalid_selection = false;

    private void Start()
    {
        grid = GetComponent<HexGrid>();
        selection_line = GetComponent<LineRenderer>();
        ClearSelectionLine();
        selected_element_indices = new List<Vector2>();
        last_grid_index_to_mouse_over = new Vector3(-1f, -1f);
        last_mouse_pos = Vector2.zero;

        restart_button.onClick.AddListener(OnRestartButtonPressed);

        ResetCounters();
    }

    private void Update()
    {
        switch (selection_mode)
        {
            case ESelectionMode.SWAP:
                #region SWAP
                if (Input.GetMouseButtonDown(0))
                {
                    Vector2 hit_grid_index = GetGridIndexUnderMousePos();
                    if (hit_grid_index.x >= 0 && hit_grid_index.y >= 0)
                    {
                        selected_element_indices = new List<Vector2>();
                        selected_element_indices.Add(hit_grid_index);
                        GridElementData element_data = grid.GetElementDataFromIndex(selected_element_indices[0]);
                        selected_element_type = element_data.element_type;
                        print("Selected_element_type: " + selected_element_type);
                        if (selected_element_type != null)
                        {
                            selected_element_transform = element_data.element_transform;
                            grid.SpawnSelectionEffectAtIndex(hit_grid_index);

                            //TODO: Highlight available directions

                            print("Grabbing tile at index " + selected_element_indices[0]);
                        }
                        else
                        {
                            print("INVALID SELECTION: Selected element type is null!");
                        }
                    }
                    else
                    {
                        print("INVALID SELECTION: Selected position is outside of the grid.");
                    }

                    last_grid_index_to_mouse_over = hit_grid_index;
                }

                if (Input.GetMouseButton(0) && selected_element_indices.Count > 0)
                {
                    if (last_mouse_pos != Input.mousePosition)
                    {
                        Vector2 hit_grid_index = GetGridIndexUnderMousePos();
                        if (last_grid_index_to_mouse_over != hit_grid_index)
                        {
                            if (hit_grid_index.x >= 0 && hit_grid_index.y >= 0)
                            {
                                if (!selected_element_indices.Contains(hit_grid_index))
                                {
                                    ElementType hit_element_type = grid.GetElementDataFromIndex(hit_grid_index).element_type;

                                    if (hit_element_type != null)
                                    {
                                        grid.SpawnSelectionEffectAtIndex(hit_grid_index);
                                    }
                                }
                            }

                            if ((last_grid_index_to_mouse_over.x >= 0 && last_grid_index_to_mouse_over.y >= 0) && last_grid_index_to_mouse_over != selected_element_indices[0])
                                grid.ClearSelectionEffectAtIndex(last_grid_index_to_mouse_over);
                            last_grid_index_to_mouse_over = hit_grid_index;
                        }

                        last_mouse_pos = Input.mousePosition;
                    }
                }

                if (Input.GetMouseButtonUp(0) && selected_element_transform != null)
                {
                    Vector2 hit_grid_index = GetGridIndexUnderMousePos();
                    if (hit_grid_index.x >= 0 && hit_grid_index.y >= 0)
                    {
                        if (selected_element_indices.Contains(hit_grid_index))
                        {
                            print("Grabbed element released at it's original index, resetting element position.");
                            //grid.ResetElementWorldPos(selected_element_indices[0]);

                            grid.MoveElementsToCorrectPositions(swap_movement_speed_increment_multiplier);
                        }
                        else
                        {
                            Vector2 release_point_index = hit_grid_index;
                            print("Released element at index " + release_point_index + ", swapping positions");

                            //TODO: Check if hit_tile is on a viable lane (e.g. if swap is restricted on the same lanes as the grabbed element)

                            //TODO: Check if valid move (e.g. if swap allowed only when it results in a match; pre-check match)

                            grid.SwapElements(selected_element_indices[0], release_point_index);

                            grid.MoveElementsToCorrectPositions(swap_movement_speed_increment_multiplier);

                            IncrementMoves();
                        }
                    }
                    else
                    {
                        print("Released element outside of the grid, resetting element position.");
                        //grid.ResetElementWorldPos(selected_element_indices[0]);

                        grid.MoveElementsToCorrectPositions(swap_movement_speed_increment_multiplier);
                    }

                    selected_element_indices.Clear();
                    selected_element_transform = null;
                    grid.ClearAllSelectionEffects();
                }

                if (selected_element_transform != null)
                {
                    Vector3 mouse_position = Camera.main.ScreenToWorldPoint(Input.mousePosition);
                    mouse_position.y = 0;
                    selected_element_transform.position = mouse_position;
                }
                #endregion
                break;

            case ESelectionMode.CONNECT:
                #region CONNECT
                if (Input.GetMouseButtonDown(0))
                {
                    selected_element_indices = new List<Vector2>();

                    Vector2 hit_grid_index = GetGridIndexUnderMousePos();
                    if (hit_grid_index.x >= 0 && hit_grid_index.y >= 0)
                    {
                        selected_element_indices.Add(hit_grid_index);
                        selected_element_type = grid.GetElementDataFromIndex(hit_grid_index).element_type;
                        print("Selected_element_type: " + selected_element_type + " at " + hit_grid_index);
                        if (selected_element_type != null)
                        {
                            invalid_selection = false;
                            StartSelectionLine(grid.CalculateWorldPos(hit_grid_index));
                            grid.SpawnSelectionEffectAtIndex(hit_grid_index);

                            //TODO: Highlight neighbouring elements of the matching type

                        }
                        else
                        {
                            InvalidateSelection();
                            print("INVALID SELECTION: Selected element type is null!");
                        }
                    }
                    else
                    {
                        print("INVALID SELECTION: Selection started outside of the grid.");
                    }
                }

                if (!invalid_selection && Input.GetMouseButton(0) && selected_element_indices.Count > 0)
                {
                    if (last_mouse_pos != Input.mousePosition)
                    {
                        Vector2 hit_grid_index = GetGridIndexUnderMousePos();
                        if (hit_grid_index.x >= 0 && hit_grid_index.y >= 0)
                        {
                            if (!selected_element_indices.Contains(hit_grid_index))
                            {
                                ElementType hit_element_type = grid.GetElementDataFromIndex(hit_grid_index).element_type;

                                if (hit_element_type != null)
                                {
                                    if (!grid.CheckIfNeighbours(selected_element_indices[selected_element_indices.Count - 1], hit_grid_index))
                                    {
                                        InvalidateSelection();
                                        print("INVALID SELECTION: Is not a neighbouring tile.");
                                    }

                                    bool is_of_matching_type = false;

                                    for (int i = 0; i < selected_element_type.matching_elements.Length; i++)
                                    {
                                        if (hit_element_type == selected_element_type.matching_elements[i])
                                            is_of_matching_type = true;
                                    }

                                    if (is_of_matching_type)
                                    {
                                        selected_element_indices.Add(hit_grid_index);
                                        AddPointToSelectionLine(grid.CalculateWorldPos(hit_grid_index));
                                        grid.SpawnSelectionEffectAtIndex(hit_grid_index);
                                        print("Selected matching element at index " + selected_element_indices[selected_element_indices.Count - 1] + ", added to the selection.");
                                    }
                                    else
                                    {
                                        AddPointToSelectionLine(grid.CalculateWorldPos(hit_grid_index));
                                        grid.SpawnSelectionEffectAtIndex(hit_grid_index);
                                        InvalidateSelection();
                                        print("INVALID SELECTION: Selected non-matching element at index " + selected_element_indices[selected_element_indices.Count - 1] + ", selection invalidated.");
                                    }
                                }
                            }
                        }

                        last_mouse_pos = Input.mousePosition;
                    }
                }

                if (Input.GetMouseButtonUp(0))
                {
                    if (selected_element_indices.Count > 0)
                    {
                        if (invalid_selection)
                        {
                            print("INVALID SELECTION: Finished with an invalid selection");
                        }
                        else
                        {
                            if (selected_element_indices.Count >= grid.min_viable_connection)
                            {
                                print("Finished with a valid selection, collecting selected elements");
                                grid.RemoveElementsAtIndices(selected_element_indices);

                                //TODO: Implement and call a score pop up, which displays the amount of score gained
                                int score_to_add = selected_element_indices.Count * selected_element_indices.Count;
                                AddToScore(score_to_add);
                                IncrementMoves();
                            }
                            else
                            {
                                print("INVALID SELECTION: Not enough elements selected to collect.");
                            }
                        }
                    }

                    selected_element_indices.Clear();
                    selected_element_transform = null;
                    ClearSelectionLine();
                    grid.ClearAllSelectionEffects();
                }
                #endregion
                break;

            default:
                break;
        }

        if (Input.GetMouseButtonDown(1))
        {
            Vector2 hit_grid_index = GetGridIndexUnderMousePos();
            if (hit_grid_index.x >= 0 && hit_grid_index.y >= 0)
            {
                grid.GetNeighbouringIndices(hit_grid_index);
            }
        }
    }

    /* - Currently obsolete functions - 
    private GridElementData GetGridElementDataUnderMousePos()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        Debug.DrawRay(ray.origin, ray.direction * 50f, Color.red, 0.5f);
        RaycastHit hit;
        if (Physics.Raycast(ray, out hit))
        {
            return grid.GetGridElementDataFromWorldPosition(hit.point);
        }

        return new GridElementData();
    }
    */

    private Vector2 GetGridIndexUnderMousePos()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        Debug.DrawRay(ray.origin, ray.direction * 50f, Color.red, 0.5f);
        RaycastHit hit;
        if (Physics.Raycast(ray, out hit))
        {
            return grid.GetGridIndexFromWorldPosition(hit.point, true);
        }

        return new Vector2(-1f, -1f);
    }

    private void InvalidateSelection()
    {
        grid.ClearAllSelectionEffects();
        invalid_selection = true;
        selection_line.material = invalid_selection_material;
    }

    private void StartSelectionLine(Vector3 start_pos)
    {
        start_pos.y = selection_line_height;

        Vector3[] new_line_positions = new Vector3[1] { start_pos };
        selection_line.positionCount = new_line_positions.Length;
        selection_line.SetPositions(new_line_positions);
    }

    private void AddPointToSelectionLine(Vector3 new_point)
    {
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

    private void ClearSelectionLine()
    {
        selection_line.positionCount = 0;
        selection_line.material = valid_selection_material;
    }

    private void OnRestartButtonPressed()
    {
        print("Restart button pressed");

        ResetCounters();
        grid.Restart();
    }

    private void ResetCounters()
    {
        score = 0;
        score_text.text = "SCORE: " + score.ToString();
        moves = 0;
        moves_text.text = "MOVES: " + moves.ToString();
    }

    private void AddToScore(int score_to_add)
    {
        score += score_to_add;
        score_text.text = "SCORE: " + score.ToString();
    }

    private void IncrementMoves()
    {
        moves++;
        moves_text.text = "MOVES: " + moves.ToString();
    }
}
