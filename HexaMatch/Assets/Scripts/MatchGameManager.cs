using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MatchGameManager : MonoBehaviour
{
    //Swap selecttion mode:
    //TODO: Highlight available directions
    //TODO: Check if hitTile is on a viable lane (e.g. if swap is restricted on the same lanes as the grabbed element)
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

    public ESelectionMode selectionMode = ESelectionMode.SWAP;

    public Button restartButton;
    public Text scoreText;
    public Text movesText;

    private HexGrid grid;
    private EffectManager effectManager;
    private ElementType selectedElementType;
    private Transform selectedElementTransform;
    private List<IntVector2> selectedElementIndices;
    private IntVector2 lastGridIndexToHoverOver;
    //private Vector3 lastMousePos;

    private float elementMaxYPosDuringSwapGrab = 4f;
    private float swapMovementSpeedIncrementMultiplier = 8f;
    private int scoreShouldBe = 0;
    private int score = 0;
    private int moves = 0;
    private bool invalidSelection = false;

    private void Start()
    {
        grid = GetComponent<HexGrid>();
        effectManager = GetComponent<EffectManager>();
        selectedElementIndices = new List<IntVector2>();
        lastGridIndexToHoverOver = new IntVector2(-1, -1);
        //lastMousePos = Vector3.zero;

        restartButton.onClick.AddListener(OnRestartButtonPressed);

        grid.OnAutoMatchesFound -= AutoMatchCallback;
        grid.OnAutoMatchesFound += AutoMatchCallback;

        effectManager.OnPointPopupEffectFinished -= PointPopupEffectFinishCallback;
        effectManager.OnPointPopupEffectFinished += PointPopupEffectFinishCallback;

        ResetCounters();
    }

    private void Update()
    {
        if (Input.touchCount > 0 && grid.GetIsElementMovementDone())
        {
            Touch currentTouch = Input.GetTouch(0);
            IntVector2 hitGridIndex = new IntVector2(0, 0);

            switch (selectionMode)
            {
                case ESelectionMode.SWAP:
                    #region SWAP
                    #region Touch input
                    switch (currentTouch.phase)
                    {
                        #region TouchPhase.Began
                        case TouchPhase.Began:
                            hitGridIndex = grid.GetGridIndexFromWorldPosition(Camera.main.ScreenToWorldPoint(currentTouch.position));
                            if (hitGridIndex.x >= 0 && hitGridIndex.y >= 0)
                            {
                                selectedElementIndices = new List<IntVector2>();
                                selectedElementIndices.Add(hitGridIndex);
                                GridElementData elementData = grid.GetGridElementDataFromIndex(selectedElementIndices[0]);
                                selectedElementType = elementData.elementType;

                                if (selectedElementType != null)
                                {
                                    selectedElementTransform = elementData.elementTransform;
                                    effectManager.SpawnSelectionEffectAtIndex(hitGridIndex);

                                    //TODO HERE: Highlight available directions
                                }
                            }

                            lastGridIndexToHoverOver = hitGridIndex;
                            break;
                        #endregion

                        #region TouchPhase.Moved
                        case TouchPhase.Moved:
                            if (selectedElementTransform != null)
                            {
                                hitGridIndex = grid.GetGridIndexFromWorldPosition(Camera.main.ScreenToWorldPoint(currentTouch.position));
                                if (lastGridIndexToHoverOver != hitGridIndex)
                                {
                                    if (hitGridIndex.x >= 0 && hitGridIndex.y >= 0)
                                    {
                                        if (selectedElementIndices[0] != hitGridIndex)
                                        {
                                            ElementType hitElementType = grid.GetGridElementDataFromIndex(hitGridIndex).elementType;

                                            if (hitElementType != null)
                                                effectManager.SpawnSelectionEffectAtIndex(hitGridIndex);
                                        }
                                    }

                                    //If the lastGridIndexToHoverOver is a valid index and not the same as the grabbed element's index
                                    if ((lastGridIndexToHoverOver.x >= 0 && lastGridIndexToHoverOver.y >= 0) && lastGridIndexToHoverOver != selectedElementIndices[0])
                                        effectManager.ClearSelectionEffectAtIndex(lastGridIndexToHoverOver);

                                    lastGridIndexToHoverOver = hitGridIndex;
                                }
                            }
                            break;
                        #endregion

                        #region TouchPhase.Ended
                        case TouchPhase.Ended:
                            if (selectedElementTransform != null)
                            {
                                hitGridIndex = grid.GetGridIndexFromWorldPosition(Camera.main.ScreenToWorldPoint(currentTouch.position));
                                if (hitGridIndex.x >= 0 && hitGridIndex.y >= 0)
                                {
                                    if (selectedElementIndices.Contains(hitGridIndex))
                                        grid.MoveElementsToCorrectPositions(swapMovementSpeedIncrementMultiplier);
                                    else
                                    {
                                        IntVector2 releasePointIndex = hitGridIndex;
                                        grid.SwapElements(selectedElementIndices[0], releasePointIndex);
                                        IncrementMoves();
                                    }
                                }

                                grid.MoveElementsToCorrectPositions(swapMovementSpeedIncrementMultiplier);
                            }

                            selectedElementIndices.Clear();
                            selectedElementTransform = null;
                            effectManager.ClearAllSelectionEffects();
                            break;
                        #endregion

                        case TouchPhase.Canceled:
                            selectedElementIndices.Clear();
                            selectedElementTransform = null;
                            effectManager.ClearAllSelectionEffects();
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
                        hitGridIndex = grid.GetGridIndexFromWorldPosition(Camera.main.ScreenToWorldPoint(Input.mousePosition));
                        if (hitGridIndex.x >= 0 && hitGridIndex.y >= 0)
                        {
                            selectedElementIndices = new List<IntVector2>();
                            selectedElementIndices.Add(hitGridIndex);
                            GridElementData elementData = grid.GetGridElementDataFromIndex(selectedElementIndices[0]);
                            selectedElementType = elementData.elementType;
                            //print("selectedElementType: " + selectedElementType);
                            if (selectedElementType != null)
                            {
                                selectedElementTransform = elementData.elementTransform;
                                effectManager.SpawnSelectionEffectAtIndex(hitGridIndex);

                                //TODO HERE: Highlight available directions

                                //print("Grabbing tile at index " + selectedElementIndices[0]);
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

                        lastGridIndexToHoverOver = hitGridIndex;
                    }
                    #endregion

                    #region LeftMouseButton Held
                    if (Input.GetMouseButton(0) && selectedElementIndices.Count > 0)
                    {
                        if (lastMousePos != Input.mousePosition)
                        {
                            hitGridIndex = grid.GetGridIndexFromWorldPosition(Camera.main.ScreenToWorldPoint(Input.mousePosition));
                            if (lastGridIndexToHoverOver != hitGridIndex)
                            {
                                if (hitGridIndex.x >= 0 && hitGridIndex.y >= 0)
                                {
                                    if (!selectedElementIndices.Contains(hitGridIndex))
                                    {
                                        ElementType hitElementType = grid.GetGridElementDataFromIndex(hitGridIndex).elementType;

                                        if (hitElementType != null)
                                        {
                                            effectManager.SpawnSelectionEffectAtIndex(hitGridIndex);
                                        }
                                    }
                                }

                                if ((lastGridIndexToHoverOver.x >= 0 && lastGridIndexToHoverOver.y >= 0) && lastGridIndexToHoverOver != selectedElementIndices[0])
                                    effectManager.ClearSelectionEffectAtIndex(lastGridIndexToHoverOver);
                                lastGridIndexToHoverOver = hitGridIndex;
                            }

                            lastMousePos = Input.mousePosition;
                        }
                    }
                    #endregion

                    #region LeftMouseButton Up
                    if (Input.GetMouseButtonUp(0) && selectedElementTransform != null)
                    {
                        hitGridIndex = grid.GetGridIndexFromWorldPosition(Camera.main.ScreenToWorldPoint(Input.mousePosition));
                        if (hitGridIndex.x >= 0 && hitGridIndex.y >= 0)
                        {
                            if (selectedElementIndices.Contains(hitGridIndex))
                            {
                                //print("Grabbed element released at it's original index, resetting element position.");

                                grid.MoveElementsToCorrectPositions(swapMovementSpeedIncrementMultiplier);
                            }
                            else
                            {
                                IntVector2 releasePointIndex = hitGridIndex;
                                //print("Released element at index " + releasePointIndex + ", swapping positions");

                                //TODO HERE: Check if hitTile is on a viable lane (e.g. if swap is restricted on the same lanes as the grabbed element)

                                //TODO HERE: Check if valid move (e.g. if swap allowed only when it results in a match; pre-check match)

                                grid.SwapElements(selectedElementIndices[0], releasePointIndex);

                                grid.MoveElementsToCorrectPositions(swapMovementSpeedIncrementMultiplier);
                                IncrementMoves();
                            }
                        }
                        else
                        {
                            //print("Released element outside of the grid, resetting element position.");

                            grid.MoveElementsToCorrectPositions(swapMovementSpeedIncrementMultiplier);
                        }

                        selectedElementIndices.Clear();
                        selectedElementTransform = null;
                        effectManager.ClearAllSelectionEffects();
                    }
                    #endregion
                    */
                    #endregion

                    if (selectedElementTransform != null)
                    {
                        Vector3 touchPosition = Camera.main.ScreenToWorldPoint(currentTouch.position);
                        touchPosition.z = 0;
                        if (touchPosition.y > elementMaxYPosDuringSwapGrab)
                            touchPosition.y = elementMaxYPosDuringSwapGrab;

                        selectedElementTransform.position = touchPosition;
                    }
                    #endregion
                    break;

                case ESelectionMode.CONNECT:
                    #region CONNECT    
                    #region Touch Input
                    switch (currentTouch.phase)
                    {
                        #region TouchPhase.Began
                        case TouchPhase.Began:
                            selectedElementIndices = new List<IntVector2>();

                            hitGridIndex = grid.GetGridIndexFromWorldPosition(Camera.main.ScreenToWorldPoint(currentTouch.position));
                            //If the hitGridIndex is a valid index
                            if (hitGridIndex.x >= 0 && hitGridIndex.y >= 0)
                            {
                                selectedElementIndices.Add(hitGridIndex);
                                selectedElementType = grid.GetGridElementDataFromIndex(hitGridIndex).elementType;

                                if (selectedElementType != null)
                                {
                                    //Start selection on the selected element
                                    invalidSelection = false;
                                    effectManager.StartSelectionLine(hitGridIndex);
                                    effectManager.SpawnSelectionEffectAtIndex(hitGridIndex);
                                    effectManager.HighlightIndices(grid.FindMatchesForIndex(hitGridIndex));
                                }
                            }
                            break;
                        #endregion

                        #region TouchPhase.Moved
                        case TouchPhase.Moved:
                            if (!invalidSelection && selectedElementIndices.Count > 0)
                            {
                                hitGridIndex = grid.GetGridIndexFromWorldPosition(Camera.main.ScreenToWorldPoint(currentTouch.position));
                                //If the hitGridIndex is a valid index
                                if (hitGridIndex.x >= 0 && hitGridIndex.y >= 0)
                                {
                                    if (!selectedElementIndices.Contains(hitGridIndex))
                                    {
                                        ElementType hitElementType = grid.GetGridElementDataFromIndex(hitGridIndex).elementType;

                                        if (hitElementType != null)
                                        {
                                            //If the newly selected element is not a neighbour of the previously selected one
                                            if (!grid.CheckIfNeighbours(selectedElementIndices[selectedElementIndices.Count - 1], hitGridIndex))
                                            {
                                                InvalidateSelection();
                                            }

                                            bool isOfMatchingType = false;

                                            //Check that the newly selected element is of matching type with the first selected element
                                            for (int i = 0; i < selectedElementType.matchingElements.Length; i++)
                                            {
                                                if (hitElementType == selectedElementType.matchingElements[i])
                                                    isOfMatchingType = true;
                                            }

                                            if (isOfMatchingType)
                                            {
                                                //Add to selection
                                                selectedElementIndices.Add(hitGridIndex);
                                                effectManager.AddPointToSelectionLine(hitGridIndex);
                                                effectManager.SpawnSelectionEffectAtIndex(hitGridIndex);
                                            }
                                            else
                                            {
                                                //Add to selection visually, but invalidate selection
                                                effectManager.AddPointToSelectionLine(hitGridIndex);
                                                effectManager.SpawnSelectionEffectAtIndex(hitGridIndex);
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
                            if (selectedElementIndices.Count > 0)
                            {
                                if (!invalidSelection)
                                {
                                    if (selectedElementIndices.Count >= grid.minViableConnection)
                                    {
                                        //Increase score
                                        effectManager.SpawnPointPopUpsForMatch(selectedElementIndices);
                                        scoreShouldBe += selectedElementIndices.Count * selectedElementIndices.Count;

                                        IncrementMoves();

                                        grid.RemoveElementsAtIndices(selectedElementIndices);
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
                        selectedElementIndices = new List<IntVector2>();

                        hitGridIndex = grid.GetGridIndexFromWorldPosition(Camera.main.ScreenToWorldPoint(Input.mousePosition));
                        if (hitGridIndex.x >= 0 && hitGridIndex.y >= 0)
                        {
                            selectedElementIndices.Add(hitGridIndex);
                            selectedElementType = grid.GetGridElementDataFromIndex(hitGridIndex).elementType;

                            if (selectedElementType != null)
                            {
                                invalidSelection = false;
                                effectManager.StartSelectionLine(hitGridIndex);
                                effectManager.SpawnSelectionEffectAtIndex(hitGridIndex);

                                effectManager.HighlightIndices(grid.FindMatchesForIndex(hitGridIndex));

                            }
                        }
                    }
                    #endregion

                    #region LeftMouseButton Held
                    if (!invalidSelection && Input.GetMouseButton(0) && selectedElementIndices.Count > 0)
                    {
                        if (lastMousePos != Input.mousePosition)
                        {
                            hitGridIndex = grid.GetGridIndexFromWorldPosition(Camera.main.ScreenToWorldPoint(Input.mousePosition));
                            if (hitGridIndex.x >= 0 && hitGridIndex.y >= 0)
                            {
                                if (!selectedElementIndices.Contains(hitGridIndex))
                                {
                                    ElementType hitElementType = grid.GetGridElementDataFromIndex(hitGridIndex).elementType;

                                    if (hitElementType != null)
                                    {
                                        if (!grid.CheckIfNeighbours(selectedElementIndices[selectedElementIndices.Count - 1], hitGridIndex))
                                        {
                                            InvalidateSelection();
                                        }

                                        bool isOfMatchingType = false;

                                        for (int i = 0; i < selectedElementType.matchingElements.Length; i++)
                                        {
                                            if (hitElementType == selectedElementType.matchingElements[i])
                                                isOfMatchingType = true;
                                        }

                                        if (isOfMatchingType)
                                        {
                                            selectedElementIndices.Add(hitGridIndex);
                                            effectManager.AddPointToSelectionLine(hitGridIndex);
                                            effectManager.SpawnSelectionEffectAtIndex(hitGridIndex);
                                        }
                                        else
                                        {
                                            effectManager.AddPointToSelectionLine(hitGridIndex);
                                            effectManager.SpawnSelectionEffectAtIndex(hitGridIndex);
                                            InvalidateSelection();
                                        }
                                    }
                                }
                            }

                            lastMousePos = Input.mousePosition;
                        }
                    }
                    #endregion

                    #region LeftMouseButton Up
                    if (Input.GetMouseButtonUp(0))
                    {
                        if (selectedElementIndices.Count > 0)
                        {
                            if (!invalidSelection)
                            {
                                if (selectedElementIndices.Count >= grid.minViableConnection)
                                {
                                    effectManager.SpawnPointPopUpsForMatch(selectedElementIndices);
                                    scoreShouldBe += selectedElementIndices.Count * selectedElementIndices.Count;
                                    IncrementMoves();

                                    grid.RemoveElementsAtIndices(selectedElementIndices);
                                    grid.FillGrid();
                                    grid.MoveElementsToCorrectPositions();
                                }
                            }
                        }

                        selectedElementIndices.Clear();
                        selectedElementTransform = null;
                        effectManager.ClearSelectionLine();
                        effectManager.ClearAllSelectionEffects();
                        effectManager.ClearHighlights();
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
        selectedElementIndices.Clear();
        selectedElementTransform = null;
        effectManager.ClearSelectionLine();
        effectManager.ClearAllSelectionEffects();
        effectManager.ClearHighlights();
    }

    private void InvalidateSelection()
    {
        effectManager.ClearAllSelectionEffects();
        invalidSelection = true;
        effectManager.InvalidateSelectionLine();
    }

    private void OnRestartButtonPressed()
    {
        grid.Restart();
        effectManager.Restart();
        ResetCounters();
    }

    private void ResetCounters()
    {
        scoreShouldBe = 0;
        score = 0;
        scoreText.text = "SCORE: " + score.ToString();
        moves = 0;
        movesText.text = "MOVES: " + moves.ToString();
    }

    private void AddToScore(int scoreToAdd)
    {
        score += scoreToAdd;
        scoreText.text = "SCORE: " + score.ToString();
    }

    private void IncrementMoves()
    {
        moves++;
        movesText.text = "MOVES: " + moves.ToString();
    }

    public void AutoMatchCallback(List<List<IntVector2>> matches)
    {
        int scoreToAdd = 0;
        for (int i = 0; i < matches.Count; i++)
        {
            //Count score
            int scoreFromMatch = matches[i].Count * matches[i].Count;
            scoreToAdd += scoreFromMatch;

            //Call effects
            effectManager.SpawnPointPopUpsForMatch(matches[i]);
        }

        scoreShouldBe += scoreToAdd;
    }

    public void PointPopupEffectFinishCallback(int pointsToAdd)
    {
        AddToScore(pointsToAdd);
    }

}
