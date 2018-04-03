using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MatchGameManager : MonoBehaviour
{
    //Swap selecttion mode:
    //TODO: Highlight available directions
    //TODO: Check if hit_tile is on a viable lane (e.g. if swap is restricted on the same lanes as the grabbed element)
    //TODO: Check if valid move (e.g. if swap allowed only when it results in a match; pre-check match)

    //TODO: Create easily modifiable implementation of match points calculation, and have it in only one place
    //          - Currently both this script and effect manager (for points popups) calculate the points separately
    //          - Current formula: matched element count ^ 2

    public enum ESelectionMode
    {
        SWAP,
        CONNECT
    }

    public ESelectionMode selection_mode = ESelectionMode.SWAP;
    
    public Button restart_button;
    public Text score_text;
    public Text moves_text;

    private HexGrid grid;
    private EffectManager effect_manager;
    private ElementType selected_element_type;
    private Transform selected_element_transform;
    private List<Vector2> selected_element_indices;
    private Vector2 last_grid_index_to_mouse_over;
    private Vector3 last_mouse_pos;
    
    private float element_max_y_pos_during_swap_grab = 4f;
    private float swap_movement_speed_increment_multiplier = 8f;
    private int score_should_be = 0;
    private int score = 0;
    private int moves = 0;
    private bool invalid_selection = false;

    private void Start()
    {
        grid = GetComponent<HexGrid>();
        effect_manager = GetComponent<EffectManager>();
        selected_element_indices = new List<Vector2>();
        last_grid_index_to_mouse_over = new Vector3(-1f, -1f);
        last_mouse_pos = Vector2.zero;

        restart_button.onClick.AddListener(OnRestartButtonPressed);

        grid.OnAutoMatchesFound -= AutoMatchCallback;
        grid.OnAutoMatchesFound += AutoMatchCallback;

        effect_manager.OnPointPopupEffectFinished -= PointPopupEffectFinishCallback;
        effect_manager.OnPointPopupEffectFinished += PointPopupEffectFinishCallback;

        ResetCounters();
    }

    private void Update()
    {
        switch (selection_mode)
        {
            case ESelectionMode.SWAP:
                #region SWAP
                #region LeftMouseButton Down
                if (Input.GetMouseButtonDown(0) && grid.GetIsElementMovementDone())
                {
                    Vector2 hit_grid_index = GetGridIndexUnderMousePos();
                    if (hit_grid_index.x >= 0 && hit_grid_index.y >= 0)
                    {
                        selected_element_indices = new List<Vector2>();
                        selected_element_indices.Add(hit_grid_index);
                        GridElementData element_data = grid.GetGridElementDataFromIndex(selected_element_indices[0]);
                        selected_element_type = element_data.element_type;
                        //print("Selected_element_type: " + selected_element_type);
                        if (selected_element_type != null)
                        {
                            selected_element_transform = element_data.element_transform;
                            effect_manager.SpawnSelectionEffectAtIndex(hit_grid_index);

                            //TODO HERE: Highlight available directions

                            //print("Grabbing tile at index " + selected_element_indices[0]);
                        }
                        //else
                        //{
                        //    print("INVALID SELECTION: Selected element type is null!");
                        //}
                    }
                    //else
                    //{
                    //    print("INVALID SELECTION: Selected position is outside of the grid.");
                    //}

                    last_grid_index_to_mouse_over = hit_grid_index;
                }
                #endregion

                #region LeftMouseButton Held
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
                                    ElementType hit_element_type = grid.GetGridElementDataFromIndex(hit_grid_index).element_type;

                                    if (hit_element_type != null)
                                    {
                                        effect_manager.SpawnSelectionEffectAtIndex(hit_grid_index);
                                    }
                                }
                            }

                            if ((last_grid_index_to_mouse_over.x >= 0 && last_grid_index_to_mouse_over.y >= 0) && last_grid_index_to_mouse_over != selected_element_indices[0])
                                effect_manager.ClearSelectionEffectAtIndex(last_grid_index_to_mouse_over);
                            last_grid_index_to_mouse_over = hit_grid_index;
                        }

                        last_mouse_pos = Input.mousePosition;
                    }
                }
                #endregion

                #region LeftMouseButton Up
                if (Input.GetMouseButtonUp(0) && selected_element_transform != null)
                {
                    Vector2 hit_grid_index = GetGridIndexUnderMousePos();
                    if (hit_grid_index.x >= 0 && hit_grid_index.y >= 0)
                    {
                        if (selected_element_indices.Contains(hit_grid_index))
                        {
                            //print("Grabbed element released at it's original index, resetting element position.");

                            grid.MoveElementsToCorrectPositions(swap_movement_speed_increment_multiplier);
                        }
                        else
                        {
                            Vector2 release_point_index = hit_grid_index;
                            //print("Released element at index " + release_point_index + ", swapping positions");

                            //TODO HERE: Check if hit_tile is on a viable lane (e.g. if swap is restricted on the same lanes as the grabbed element)

                            //TODO HERE: Check if valid move (e.g. if swap allowed only when it results in a match; pre-check match)

                            grid.SwapElements(selected_element_indices[0], release_point_index);

                            grid.MoveElementsToCorrectPositions(swap_movement_speed_increment_multiplier);
                            IncrementMoves();
                        }
                    }
                    else
                    {
                        //print("Released element outside of the grid, resetting element position.");

                        grid.MoveElementsToCorrectPositions(swap_movement_speed_increment_multiplier);
                    }

                    selected_element_indices.Clear();
                    selected_element_transform = null;
                    effect_manager.ClearAllSelectionEffects();
                }
                #endregion

                if (selected_element_transform != null)
                {
                    Vector3 mouse_position = Camera.main.ScreenToWorldPoint(Input.mousePosition);
                    mouse_position.y = 0;
                    if (mouse_position.z > element_max_y_pos_during_swap_grab)
                        mouse_position.z = element_max_y_pos_during_swap_grab;

                    selected_element_transform.position = mouse_position;
                }
                #endregion
                break;

            case ESelectionMode.CONNECT:
                #region CONNECT
                #region LeftMouseButton Down
                if (Input.GetMouseButtonDown(0) && grid.GetIsElementMovementDone())
                {
                    selected_element_indices = new List<Vector2>();

                    Vector2 hit_grid_index = GetGridIndexUnderMousePos();
                    if (hit_grid_index.x >= 0 && hit_grid_index.y >= 0)
                    {
                        selected_element_indices.Add(hit_grid_index);
                        selected_element_type = grid.GetGridElementDataFromIndex(hit_grid_index).element_type;
                        //print("Selected_element_type: " + selected_element_type + " at " + hit_grid_index);
                        if (selected_element_type != null)
                        {
                            invalid_selection = false;
                            effect_manager.StartSelectionLine(hit_grid_index);
                            effect_manager.SpawnSelectionEffectAtIndex(hit_grid_index);

                            //TODO: Highlight neighbouring elements of the matching type
                            effect_manager.HighlightIndices(grid.FindMatchesForIndex(hit_grid_index));

                        }
                        //else
                        //{
                        //    InvalidateSelection();
                        //    print("INVALID SELECTION: Selected element type is null!");
                        //}
                    }
                    //else
                    //{
                    //    print("INVALID SELECTION: Selection started outside of the grid.");
                    //}
                }
                #endregion

                #region LeftMouseButton Held
                if (!invalid_selection && Input.GetMouseButton(0) && selected_element_indices.Count > 0)
                {
                    if (last_mouse_pos != Input.mousePosition)
                    {
                        Vector2 hit_grid_index = GetGridIndexUnderMousePos();
                        if (hit_grid_index.x >= 0 && hit_grid_index.y >= 0)
                        {
                            if (!selected_element_indices.Contains(hit_grid_index))
                            {
                                ElementType hit_element_type = grid.GetGridElementDataFromIndex(hit_grid_index).element_type;

                                if (hit_element_type != null)
                                {
                                    if (!grid.CheckIfNeighbours(selected_element_indices[selected_element_indices.Count - 1], hit_grid_index))
                                    {
                                        InvalidateSelection();
                                        //print("INVALID SELECTION: Is not a neighbouring tile.");
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
                                        effect_manager.AddPointToSelectionLine(hit_grid_index);
                                        effect_manager.SpawnSelectionEffectAtIndex(hit_grid_index);
                                        //print("Selected matching element at index " + selected_element_indices[selected_element_indices.Count - 1] + ", added to the selection.");
                                    }
                                    else
                                    {
                                        effect_manager.AddPointToSelectionLine(hit_grid_index);
                                        effect_manager.SpawnSelectionEffectAtIndex(hit_grid_index);
                                        InvalidateSelection();
                                        //print("INVALID SELECTION: Selected non-matching element at index " + selected_element_indices[selected_element_indices.Count - 1] + ", selection invalidated.");
                                    }
                                }
                            }
                        }

                        last_mouse_pos = Input.mousePosition;
                    }
                }
                #endregion

                #region LeftMouseButton Up
                if (Input.GetMouseButtonUp(0))
                {
                    if (selected_element_indices.Count > 0)
                    {
                        if (!invalid_selection)
                        {
                            if (selected_element_indices.Count >= grid.min_viable_connection)
                            {
                                //print("Finished with a valid selection, collecting selected elements");
                                effect_manager.SpawnPointPopUpsForMatch(selected_element_indices);
                                //int score_to_add = selected_element_indices.Count * selected_element_indices.Count;
                                //AddToScore(score_to_add);
                                score_should_be += selected_element_indices.Count * selected_element_indices.Count;
                                IncrementMoves();

                                grid.RemoveElementsAtIndices(selected_element_indices);
                                grid.FillGrid(new Vector2(0, -1f));
                                grid.MoveElementsToCorrectPositions();
                            }
                            //else
                            //{
                            //    print("INVALID SELECTION: Not enough elements selected to collect.");
                            //}
                        }
                        //else
                        //{
                        //    print("INVALID SELECTION: Finished with an invalid selection");
                        //}
                    }

                    selected_element_indices.Clear();
                    selected_element_transform = null;
                    effect_manager.ClearSelectionLine();
                    effect_manager.ClearAllSelectionEffects();
                    effect_manager.ClearHighlights();
                }
                #endregion
                #endregion
                break;

            default:
                break;
        }
    }

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
        effect_manager.ClearAllSelectionEffects();
        invalid_selection = true;
        effect_manager.InvalidateSelectionLine();
    }

    private void OnRestartButtonPressed()
    {
        //print("Restart button pressed");

        grid.Restart();
        effect_manager.Restart();
        ResetCounters();
    }

    private void ResetCounters()
    {
        score_should_be = 0;
        score = 0;
        score_text.text = "SCORE: " + score.ToString();
        moves = 0;
        moves_text.text = "MOVES: " + moves.ToString();
    }

    private void AddToScore(int score_to_add)
    {
        score += score_to_add;
        score_text.text = "SCORE: " + score.ToString();
        //print("Score should be: " + score_should_be);
    }

    private void IncrementMoves()
    {
        moves++;
        moves_text.text = "MOVES: " + moves.ToString();
    }

    public void AutoMatchCallback(List<List<Vector2>> matches)
    {
        int score_to_add = 0;
        for (int i = 0; i < matches.Count; i++)
        {
            //TODO: Check element type

            //Count score
            int score_from_match = matches[i].Count * matches[i].Count;
            score_to_add += score_from_match;
            //print("Gained score from auto-match: " + score_from_match);

            //TODO: Call effects
            effect_manager.SpawnPointPopUpsForMatch(matches[i]);

        }

        //AddToScore(score_to_add);
        score_should_be += score_to_add;
    }

    public void PointPopupEffectFinishCallback(int points_to_add)
    {
        AddToScore(points_to_add);
    }

}
