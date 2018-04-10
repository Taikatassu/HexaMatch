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

    //All mouse inputs currently commented to allow unity to ignore mouse events when building the project
    //https://answers.unity.com/questions/1064394/onmousedown-and-mobile.html

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
    private List<IntVector2> selected_element_indices;
    private IntVector2 last_grid_index_to_hover_over;
    //private Vector3 last_mouse_pos;

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
        selected_element_indices = new List<IntVector2>();
        last_grid_index_to_hover_over = new IntVector2(-1, -1);
        //last_mouse_pos = Vector3.zero;

        restart_button.onClick.AddListener(OnRestartButtonPressed);

        grid.OnAutoMatchesFound -= AutoMatchCallback;
        grid.OnAutoMatchesFound += AutoMatchCallback;

        effect_manager.OnPointPopupEffectFinished -= PointPopupEffectFinishCallback;
        effect_manager.OnPointPopupEffectFinished += PointPopupEffectFinishCallback;

        ResetCounters();
    }

    private void Update()
    {
        if (Input.touchCount > 0 && grid.GetIsElementMovementDone())
        {
            Touch current_touch = Input.GetTouch(0);
            IntVector2 hit_grid_index = new IntVector2(0, 0);

            switch (selection_mode)
            {
                case ESelectionMode.SWAP:
                    #region SWAP
                    #region Touch input
                    switch (current_touch.phase)
                    {
                        case TouchPhase.Began:
                            hit_grid_index = grid.GetGridIndexFromWorldPosition(Camera.main.ScreenToWorldPoint(current_touch.position));
                            if (hit_grid_index.x >= 0 && hit_grid_index.y >= 0)
                            {
                                selected_element_indices = new List<IntVector2>();
                                selected_element_indices.Add(hit_grid_index);
                                GridElementData element_data = grid.GetGridElementDataFromIndex(selected_element_indices[0]);
                                selected_element_type = element_data.element_type;

                                if (selected_element_type != null)
                                {
                                    selected_element_transform = element_data.element_transform;
                                    effect_manager.SpawnSelectionEffectAtIndex(hit_grid_index);

                                    //TODO HERE: Highlight available directions
                                }
                            }

                            last_grid_index_to_hover_over = hit_grid_index;
                            break;

                        case TouchPhase.Moved:
                            if (selected_element_transform != null)
                            {
                                hit_grid_index = grid.GetGridIndexFromWorldPosition(Camera.main.ScreenToWorldPoint(current_touch.position));
                                if (last_grid_index_to_hover_over != hit_grid_index)
                                {
                                    if (hit_grid_index.x >= 0 && hit_grid_index.y >= 0)
                                    {
                                        if (selected_element_indices[0] != hit_grid_index)
                                        {
                                            ElementType hit_element_type = grid.GetGridElementDataFromIndex(hit_grid_index).element_type;

                                            if (hit_element_type != null)
                                                effect_manager.SpawnSelectionEffectAtIndex(hit_grid_index);
                                        }
                                    }

                                    //If the last_grid_index_to_hover_over is a valid index and not the same as the grabbed element's index
                                    if ((last_grid_index_to_hover_over.x >= 0 && last_grid_index_to_hover_over.y >= 0) && last_grid_index_to_hover_over != selected_element_indices[0])
                                        effect_manager.ClearSelectionEffectAtIndex(last_grid_index_to_hover_over);

                                    last_grid_index_to_hover_over = hit_grid_index;
                                }
                            }
                            break;

                        case TouchPhase.Ended:
                            if (selected_element_transform != null)
                            {
                                hit_grid_index = grid.GetGridIndexFromWorldPosition(Camera.main.ScreenToWorldPoint(current_touch.position));
                                if (hit_grid_index.x >= 0 && hit_grid_index.y >= 0)
                                {
                                    if (selected_element_indices.Contains(hit_grid_index))
                                        grid.MoveElementsToCorrectPositions(swap_movement_speed_increment_multiplier);
                                    else
                                    {
                                        IntVector2 release_point_index = hit_grid_index;
                                        grid.SwapElements(selected_element_indices[0], release_point_index);
                                        IncrementMoves();
                                    }
                                }

                                grid.MoveElementsToCorrectPositions(swap_movement_speed_increment_multiplier);
                            }

                            selected_element_indices.Clear();
                            selected_element_transform = null;
                            effect_manager.ClearAllSelectionEffects();
                            break;

                        case TouchPhase.Canceled:
                            selected_element_indices.Clear();
                            selected_element_transform = null;
                            effect_manager.ClearAllSelectionEffects();
                            break;

                        default:
                            break;
                    }
                    #endregion

                    #region Mouse input
                    /*
                    #region LeftMouseButton Down
                    if (Input.GetMouseButtonDown(0) && grid.GetIsElementMovementDone())
                    {
                        hit_grid_index = grid.GetGridIndexFromWorldPosition(Camera.main.ScreenToWorldPoint(Input.mousePosition));
                        if (hit_grid_index.x >= 0 && hit_grid_index.y >= 0)
                        {
                            selected_element_indices = new List<IntVector2>();
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
                            hit_grid_index = grid.GetGridIndexFromWorldPosition(Camera.main.ScreenToWorldPoint(Input.mousePosition));
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
                        hit_grid_index = grid.GetGridIndexFromWorldPosition(Camera.main.ScreenToWorldPoint(Input.mousePosition));
                        if (hit_grid_index.x >= 0 && hit_grid_index.y >= 0)
                        {
                            if (selected_element_indices.Contains(hit_grid_index))
                            {
                                //print("Grabbed element released at it's original index, resetting element position.");

                                grid.MoveElementsToCorrectPositions(swap_movement_speed_increment_multiplier);
                            }
                            else
                            {
                                IntVector2 release_point_index = hit_grid_index;
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
                    */
                    #endregion

                    if (selected_element_transform != null)
                    {
                        Vector3 touch_position = Camera.main.ScreenToWorldPoint(current_touch.position);
                        touch_position.z = 0;
                        if (touch_position.y > element_max_y_pos_during_swap_grab)
                            touch_position.y = element_max_y_pos_during_swap_grab;

                        selected_element_transform.position = touch_position;
                    }
                    #endregion
                    break;

                case ESelectionMode.CONNECT:
                    #region CONNECT    
                    #region Touch Input
                    switch (current_touch.phase)
                    {
                        #region TouchPhase.Began
                        case TouchPhase.Began:
                            selected_element_indices = new List<IntVector2>();

                            hit_grid_index = grid.GetGridIndexFromWorldPosition(Camera.main.ScreenToWorldPoint(current_touch.position));
                            //If the hit_grid_index is a valid index
                            if (hit_grid_index.x >= 0 && hit_grid_index.y >= 0)
                            {
                                selected_element_indices.Add(hit_grid_index);
                                selected_element_type = grid.GetGridElementDataFromIndex(hit_grid_index).element_type;

                                if (selected_element_type != null)
                                {
                                    //Start selection on the selected element
                                    invalid_selection = false;
                                    effect_manager.StartSelectionLine(hit_grid_index);
                                    effect_manager.SpawnSelectionEffectAtIndex(hit_grid_index);
                                    effect_manager.HighlightIndices(grid.FindMatchesForIndex(hit_grid_index));
                                }
                            }
                            break;
                        #endregion

                        #region TouchPhase.Moved
                        case TouchPhase.Moved:
                            if (!invalid_selection && selected_element_indices.Count > 0)
                            {
                                hit_grid_index = grid.GetGridIndexFromWorldPosition(Camera.main.ScreenToWorldPoint(current_touch.position));
                                //If the hit_grid_index is a valid index
                                if (hit_grid_index.x >= 0 && hit_grid_index.y >= 0)
                                {
                                    if (!selected_element_indices.Contains(hit_grid_index))
                                    {
                                        ElementType hit_element_type = grid.GetGridElementDataFromIndex(hit_grid_index).element_type;

                                        if (hit_element_type != null)
                                        {
                                            //If the newly selected element is not a neighbour of the previously selected one
                                            if (!grid.CheckIfNeighbours(selected_element_indices[selected_element_indices.Count - 1], hit_grid_index))
                                            {
                                                InvalidateSelection();
                                            }

                                            bool is_of_matching_type = false;

                                            //Check that the newly selected element is of matching type with the first selected element
                                            for (int i = 0; i < selected_element_type.matching_elements.Length; i++)
                                            {
                                                if (hit_element_type == selected_element_type.matching_elements[i])
                                                    is_of_matching_type = true;
                                            }

                                            if (is_of_matching_type)
                                            {
                                                //Add to selection
                                                selected_element_indices.Add(hit_grid_index);
                                                effect_manager.AddPointToSelectionLine(hit_grid_index);
                                                effect_manager.SpawnSelectionEffectAtIndex(hit_grid_index);
                                            }
                                            else
                                            {
                                                //Add to selection visually, but invalidate selection
                                                effect_manager.AddPointToSelectionLine(hit_grid_index);
                                                effect_manager.SpawnSelectionEffectAtIndex(hit_grid_index);
                                                InvalidateSelection();
                                            }
                                        }
                                    }
                                }
                            }
                            break;
                        #endregion

                        #region TouchPhase.Ended
                        case TouchPhase.Ended:
                            if (selected_element_indices.Count > 0)
                            {
                                if (!invalid_selection)
                                {
                                    if (selected_element_indices.Count >= grid.min_viable_connection)
                                    {
                                        //Increase score
                                        effect_manager.SpawnPointPopUpsForMatch(selected_element_indices);
                                        score_should_be += selected_element_indices.Count * selected_element_indices.Count;

                                        IncrementMoves();

                                        grid.RemoveElementsAtIndices(selected_element_indices);
                                        grid.FillGrid();
                                        grid.MoveElementsToCorrectPositions();
                                    }
                                }
                            }

                            ClearSelectionsAndRelatedEffects();
                            break;
                        #endregion

                        case TouchPhase.Canceled:
                            ClearSelectionsAndRelatedEffects();
                            break;

                        default:
                            break;
                    }
                    #endregion

                    #region Mouse Input
                    /*
                    #region LeftMouseButton Down
                    if (Input.GetMouseButtonDown(0) && grid.GetIsElementMovementDone())
                    {
                        selected_element_indices = new List<IntVector2>();

                        hit_grid_index = grid.GetGridIndexFromWorldPosition(Camera.main.ScreenToWorldPoint(Input.mousePosition));
                        if (hit_grid_index.x >= 0 && hit_grid_index.y >= 0)
                        {
                            selected_element_indices.Add(hit_grid_index);
                            selected_element_type = grid.GetGridElementDataFromIndex(hit_grid_index).element_type;

                            if (selected_element_type != null)
                            {
                                invalid_selection = false;
                                effect_manager.StartSelectionLine(hit_grid_index);
                                effect_manager.SpawnSelectionEffectAtIndex(hit_grid_index);

                                effect_manager.HighlightIndices(grid.FindMatchesForIndex(hit_grid_index));

                            }
                        }
                    }
                    #endregion

                    #region LeftMouseButton Held
                    if (!invalid_selection && Input.GetMouseButton(0) && selected_element_indices.Count > 0)
                    {
                        if (last_mouse_pos != Input.mousePosition)
                        {
                            hit_grid_index = grid.GetGridIndexFromWorldPosition(Camera.main.ScreenToWorldPoint(Input.mousePosition));
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
                                        }
                                        else
                                        {
                                            effect_manager.AddPointToSelectionLine(hit_grid_index);
                                            effect_manager.SpawnSelectionEffectAtIndex(hit_grid_index);
                                            InvalidateSelection();
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
                                    effect_manager.SpawnPointPopUpsForMatch(selected_element_indices);
                                    score_should_be += selected_element_indices.Count * selected_element_indices.Count;
                                    IncrementMoves();

                                    grid.RemoveElementsAtIndices(selected_element_indices);
                                    grid.FillGrid();
                                    grid.MoveElementsToCorrectPositions();
                                }
                            }
                        }

                        selected_element_indices.Clear();
                        selected_element_transform = null;
                        effect_manager.ClearSelectionLine();
                        effect_manager.ClearAllSelectionEffects();
                        effect_manager.ClearHighlights();
                    }
                    #endregion
                    */
                    #endregion
                    #endregion
                    break;

                default:
                    break;
            }
        }
    }

    private void ClearSelectionsAndRelatedEffects()
    {
        selected_element_indices.Clear();
        selected_element_transform = null;
        effect_manager.ClearSelectionLine();
        effect_manager.ClearAllSelectionEffects();
        effect_manager.ClearHighlights();
    }

    private void InvalidateSelection()
    {
        effect_manager.ClearAllSelectionEffects();
        invalid_selection = true;
        effect_manager.InvalidateSelectionLine();
    }

    private void OnRestartButtonPressed()
    {
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
    }

    private void IncrementMoves()
    {
        moves++;
        moves_text.text = "MOVES: " + moves.ToString();
    }

    public void AutoMatchCallback(List<List<IntVector2>> matches)
    {
        int score_to_add = 0;
        for (int i = 0; i < matches.Count; i++)
        {
            //Count score
            int score_from_match = matches[i].Count * matches[i].Count;
            score_to_add += score_from_match;

            //Call effects
            effect_manager.SpawnPointPopUpsForMatch(matches[i]);

        }

        score_should_be += score_to_add;
    }

    public void PointPopupEffectFinishCallback(int points_to_add)
    {
        AddToScore(points_to_add);
    }

}
