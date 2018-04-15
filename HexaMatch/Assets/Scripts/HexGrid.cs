using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HexGrid : MonoBehaviour
{
    // Original source: https://www.youtube.com/watch?v=konL0iB5gPI

    //TODO: Correct grid startPos calculations (7x6 grid is centered correctly, 6x5 is not?)

    public delegate void ListListIntVector2(List<List<IntVector2>> listListVec2);
    public event ListListIntVector2 OnAutoMatchesFound;

    public Transform hexBasePrefab;
    public ElementType[] elementTypes;

    public string gridElementTransformPoolTag;

    public int gridWidth = 6;
    public int gridHeight = 6;
    public int minViableConnection = 2;
    public int minAutoMatchConnection = 10;

    public float gap = 0.1f;
    public float minNewElementSpawnYPos = 2f;

    public bool spawnHexBases = true;
    public bool offsetIndividualSpawns = true;

    private GridElementData[,] gridElements;
    private PoolManager PoolManager;
    private EffectManager effectManager;

    private Coroutine elementMovementCoroutine;
    private Vector3 startPos;
    private Vector3 fallDirection = Vector3.down; //TODO: Currently changing fall direction causes in infinite loop resulting in a freeze
                                                   //(So keep fallDirection as Vector3.down)
                                                   //Fix if necessary

    private float hexBaseZPos = 0.25f;
    private float hexWidth = 1f;
    private float hexHeight = 0.866f;
    private float elementWidth = 0.725f;
    private float spawnOffsetPerRow = 1.25f;
    private float SpawnOffsetPerElementCount = 0.05f;

    private bool isElementMovementDone = true;

    private void Start()
    {
        PoolManager = GetComponent<PoolManager>();
        effectManager = GetComponent<EffectManager>();

        fallDirection.Normalize();
        AddGap();
        CalculateStartPos();
        CreateGrid();
    }

    private void AddGap()
    {
        hexWidth += hexWidth * gap;
        hexHeight += hexHeight * gap;
    }

    private void CalculateStartPos()
    {
        float x, y = 0;

        float yOffset = (gridWidth / 2 % 2 == 0) ? 0 : hexHeight / 2;

        x = -hexWidth * 0.75f * (gridWidth / 2f);
        y = hexHeight * (gridHeight / 2f) - yOffset;

        startPos = new Vector3(x, y, 0);
        //print("startPos: " + startPos);
    }

    private void CreateGrid()
    {
        isElementMovementDone = false;

        if (spawnHexBases)
        {
            CreateGridBase();
        }

        InitializeGridElements();
    }

    private void CreateGridBase()
    {
        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                SpawnHexBaseTile(new IntVector2(x, y));
            }
        }
    }

    private void InitializeGridElements()
    {
        gridElements = new GridElementData[gridWidth, gridHeight];

        FillGrid(true);
        ClearGridOfAutoMatches();
        RecalculateSpawnPositions(); ;
        MoveElementsToCorrectPositions();
    }

    private void SpawnHexBaseTile(IntVector2 gridIndex)
    {
        Transform hex = Instantiate(hexBasePrefab) as Transform;
        Vector3 spawnPos = CalculateWorldPos(gridIndex);
        spawnPos.z = hexBaseZPos;
        hex.position = spawnPos;
        hex.eulerAngles = new Vector3(-90f, 0, 0);

        hex.parent = this.transform;
        hex.name = "HexTile " + gridIndex.x + "|" + gridIndex.y;
    }

    private void CreateNewGridElement(ElementType elementType, Vector3 spawnPos, IntVector2 gridIndex, Transform parent)
    {
        GameObject element = PoolManager.SpawnFromPool(gridElementTransformPoolTag);
        element.GetComponentInChildren<Renderer>().material = elementType.elementMaterial;
        element.transform.position = spawnPos;
        element.transform.parent = parent;
        element.SetActive(true);
        gridElements[gridIndex.x, gridIndex.y] = new GridElementData(elementType, element.transform, CalculateWorldPos(gridIndex));
    }

    private ElementType ChooseRandomElementType()
    {
        return elementTypes[Random.Range(0, elementTypes.Length)];
    }

    private List<List<IntVector2>> FindMatchesOfElementType(ElementType elementType)
    {
        List<List<IntVector2>> allMatchingElementIndices = new List<List<IntVector2>>();
        List<IntVector2> indicesAlreadyChecked = new List<IntVector2>();

        //Loop through grid elements
        for (int x = 0; x < gridElements.GetLength(0); x++)
        {
            for (int y = 0; y < gridElements.GetLength(1); y++)
            {
                if (gridElements[x, y].flaggedForRemovalByAutoMatch) continue;
                if (indicesAlreadyChecked.Contains(new IntVector2(x, y))) continue;

                //If the element is of matching type with the element to check against
                if (IsOfMatchingElementType(elementType, gridElements[x, y].elementType))
                {
                    //print("Found element matching with the type to check at " + x + "|" + y);
                    //Store the matching neighbours on a one list
                    List<IntVector2> matchingNeighbours = new List<IntVector2>();
                    //And the matching neighbours whose neighbours we have yet to check, on another list
                    List<IntVector2> matchingNeighboursToCheck = new List<IntVector2>();
                    //Add the current element as the first item on both lists
                    matchingNeighbours.Add(new IntVector2(x, y));
                    matchingNeighboursToCheck.Add(new IntVector2(x, y));

                    //Check neighbours of all the neighbouring matches, until no unchecked matching neighbours are left
                    while (matchingNeighboursToCheck.Count > 0)
                    {
                        if (!indicesAlreadyChecked.Contains(matchingNeighboursToCheck[0]))
                            indicesAlreadyChecked.Add(matchingNeighboursToCheck[0]);

                        List<IntVector2> neighbouringIndices = GetNeighbouringIndices(matchingNeighboursToCheck[0]);

                        for (int i = 0; i < neighbouringIndices.Count; i++)
                        {
                            if (gridElements[neighbouringIndices[i].x, neighbouringIndices[i].y].flaggedForRemovalByAutoMatch) continue;

                            if (IsOfMatchingElementType(elementType, gridElements[neighbouringIndices[i].x, neighbouringIndices[i].y].elementType))
                            {
                                if (!matchingNeighbours.Contains(neighbouringIndices[i]))
                                {
                                    matchingNeighbours.Add(neighbouringIndices[i]);
                                    matchingNeighboursToCheck.Add(neighbouringIndices[i]);
                                    gridElements[neighbouringIndices[i].x, neighbouringIndices[i].y].flaggedForRemovalByAutoMatch = true;
                                }
                            }
                        }

                        //print("Removing matching neighbour at index " + matchingNeighboursToCheck[0] + " to check from list.");
                        matchingNeighboursToCheck.RemoveAt(0);
                    }

                    if (matchingNeighbours.Count >= minAutoMatchConnection)
                    {
                        //print("Connected matches of index " + x + "|" + y + " checked, adding " + matchingNeighbours.Count + " indices to the match list.");
                        allMatchingElementIndices.Add(matchingNeighbours);
                    }
                    //else
                    //{
                    //    print("Connected matches of index " + x + "|" + y + " checked, connected element count not big enough for match, ignoring indices (matchingNeighbours.Count: " + matchingNeighbours.Count + ").");
                    //}
                }
            }
        }

        return allMatchingElementIndices;
    }

    private bool IsOfMatchingElementType(ElementType mainType, ElementType otherType)
    {
        if (otherType == mainType)
        {
            return true;
        }

        for (int i = 0; i < mainType.matchingElements.Length; i++)
        {
            if (otherType == mainType.matchingElements[i])
            {
                return true;
            }
        }

        return false;
    }

    private void RecalculateSpawnPositions()
    {
        int spawnCount = 0;

        for (int x = gridElements.GetLength(0) - 1; x >= 0; x--)
        {
            for (int y = gridElements.GetLength(1) - 1; y >= 0; y--)
            {
                int numberOfElementsUnderThisOne = gridElements.GetLength(1) - (y + 1);

                Vector3 spawnPosOffset = -fallDirection * hexHeight
                    * (numberOfElementsUnderThisOne + 1 + gridElements.GetLength(1));

                if (offsetIndividualSpawns)
                {
                    spawnPosOffset *= (1 + (SpawnOffsetPerElementCount * spawnCount));
                }

                spawnPosOffset.y += minNewElementSpawnYPos;
                Vector3 spawnPos = CalculateWorldPos(new IntVector2(x, y)) + spawnPosOffset;

                gridElements[x, y].elementTransform.position = spawnPos;
                spawnCount++;
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
            isElementMovementDone = true;
        }
    }

    private IEnumerator MoveAllElementsTowardsCorrectWorldPositions(float movementSpeedIncrementMultiplier = 1f)
    {
        //print("Starting drop 'animations'.");
        float movementSpeed = 0f;
        float movementSpeedIncrementPerSecond = 25f;

        List<IntVector2> indicesToMove = new List<IntVector2>();

        for (int x = 0; x < gridElements.GetLength(0); x++)
        {
            for (int y = 0; y < gridElements.GetLength(1); y++)
            {
                if (gridElements[x, y].elementTransform.position != gridElements[x, y].correctWorldPos)
                {
                    indicesToMove.Add(new IntVector2(x, y));
                }
            }
        }

        //print("Indices to move count " + indicesToMove.Count);

        while (indicesToMove.Count > 0)
        {
            for (int i = 0; i < indicesToMove.Count; i++)
            {
                GridElementData elementToMove = gridElements[indicesToMove[i].x, indicesToMove[i].y];

                Vector3 directionToCorrectPos = elementToMove.correctWorldPos - elementToMove.elementTransform.position;
                float distanceToCorrectPos = directionToCorrectPos.magnitude;

                if (distanceToCorrectPos <= movementSpeed * Time.deltaTime)
                {
                    elementToMove.elementTransform.position = elementToMove.correctWorldPos;
                    gridElements[indicesToMove[i].x, indicesToMove[i].y].justSpawned = false;
                    //print("Element at " + indicesToMove[i] + " has arrived at correct world pos. indicesToMove.Count: "
                    //    + indicesToMove.Count + ", i: " + i);
                    indicesToMove.RemoveAt(i);
                    i--;
                }
                else
                {
                    directionToCorrectPos /= distanceToCorrectPos;
                    elementToMove.elementTransform.position += directionToCorrectPos * movementSpeed * Time.deltaTime;
                }
            }

            movementSpeed += movementSpeedIncrementPerSecond * movementSpeedIncrementMultiplier * Time.deltaTime;

            yield return new WaitForEndOfFrame();
        }

        //print("Finished drop 'animations'.");
        ElementMovementFinished();
    }

    public void MoveElementsToCorrectPositions(float movementSpeedIncrementMultiplier = 1f)
    {
        isElementMovementDone = false;

        if (elementMovementCoroutine != null)
        {
            StopCoroutine(elementMovementCoroutine);
        }

        elementMovementCoroutine = StartCoroutine(MoveAllElementsTowardsCorrectWorldPositions(movementSpeedIncrementMultiplier));
    }

    public bool GetIsElementMovementDone()
    {
        return isElementMovementDone;
    }

    public List<IntVector2> FindMatchesForIndex(IntVector2 gridIndex)
    {
        ElementType elementType = gridElements[gridIndex.x, gridIndex.y].elementType;

        //Store the matching neighbours on a one list
        List<IntVector2> matchingNeighbours = new List<IntVector2>();
        //And the matching neighbours whose neighbours we have yet to check, on another list
        List<IntVector2> matchingNeighboursToCheck = new List<IntVector2>();
        //Add the current element as the first item on both lists
        matchingNeighbours.Add(gridIndex);
        matchingNeighboursToCheck.Add(gridIndex);

        //Check neighbours of all the neighbouring matches, until no unchecked matching neighbours are left
        while (matchingNeighboursToCheck.Count > 0)
        {
            List<IntVector2> neighbouringIndices = GetNeighbouringIndices(matchingNeighboursToCheck[0]);

            for (int i = 0; i < neighbouringIndices.Count; i++)
            {
                if (IsOfMatchingElementType(elementType, gridElements[neighbouringIndices[i].x, neighbouringIndices[i].y].elementType))
                {
                    if (!matchingNeighbours.Contains(neighbouringIndices[i]))
                    {
                        matchingNeighbours.Add(neighbouringIndices[i]);
                        matchingNeighboursToCheck.Add(neighbouringIndices[i]);
                    }
                }
            }

            matchingNeighboursToCheck.RemoveAt(0);
        }

        return matchingNeighbours;
    }

    public void SwapElements(IntVector2 aIndex, IntVector2 bIndex)
    {
        GridElementData oldA = gridElements[aIndex.x, aIndex.y];
        gridElements[aIndex.x, aIndex.y] = gridElements[bIndex.x, bIndex.y];
        gridElements[bIndex.x, bIndex.y] = oldA;

        gridElements[aIndex.x, aIndex.y].correctWorldPos = CalculateWorldPos(aIndex);
        gridElements[bIndex.x, bIndex.y].correctWorldPos = CalculateWorldPos(bIndex);
    }

    public void ResetElementWorldPos(IntVector2 gridIndex)
    {
        GridElementData element = gridElements[gridIndex.x, gridIndex.y];
        if (element.elementTransform != null)
        {
            element.correctWorldPos = CalculateWorldPos(gridIndex);
            element.elementTransform.transform.position = element.correctWorldPos;
            element.elementTransform.transform.parent = this.transform;
        }
    }

    public Vector3 CalculateWorldPos(IntVector2 gridPos)
    {
        float x, y = 0;

        float yOffset = (gridPos.x % 2 == 0) ? 0 : hexHeight / 2;

        x = startPos.x + gridPos.x * hexWidth * 0.75f;
        y = startPos.y - gridPos.y * hexHeight + yOffset;

        return new Vector3(x, y, 0);
    }

    public IntVector2 GetGridIndexFromWorldPosition(Vector3 worldPos, bool limitToElementWidth = false)
    {
        for (int x = 0; x < gridElements.GetLength(0); x++)
        {
            for (int y = 0; y < gridElements.GetLength(1); y++)
            {
                Vector3 gridWorldPos = CalculateWorldPos(new IntVector2(x, y));
                float halfAreaSize = limitToElementWidth ? elementWidth / 2 : (hexHeight - gap) / 2;
                float xMin = gridWorldPos.x - halfAreaSize;
                float xMax = gridWorldPos.x + halfAreaSize;
                float yMin = gridWorldPos.y - halfAreaSize;
                float yMax = gridWorldPos.y + halfAreaSize;

                if (worldPos.x >= xMin && worldPos.x <= xMax && worldPos.y >= yMin && worldPos.y <= yMax)
                {
                    IntVector2 matchingGridIndices = new IntVector2(x, y);
                    return matchingGridIndices;
                }
            }
        }

        return new IntVector2(-1, -1);
    }

    public GridElementData GetGridElementDataFromIndex(IntVector2 gridIndex)
    {
        return gridElements[gridIndex.x, gridIndex.y];
    }

    public List<IntVector2> GetNeighbouringIndices(IntVector2 gridIndex)
    {
        List<IntVector2> neighbours = new List<IntVector2>();

        for (int x = gridIndex.x - 1; x <= gridIndex.x + 1; x++)
        {
            if (x < 0 || x >= gridElements.GetLength(0))
                continue;


            for (int y = gridIndex.y - 1; y <= gridIndex.y + 1; y++)
            {
                if (y < 0 || y >= gridElements.GetLength(1))
                    continue;

                if (CheckIfNeighbours(gridIndex, new IntVector2(x, y)))
                    neighbours.Add(new IntVector2(x, y));
            }
        }

        //print("Element " + gridIndex.x + "|" + gridIndex.y + " neighbour count: " + neighbours.Count);

        return neighbours;
    }

    public bool CheckIfNeighbours(IntVector2 aIndex, IntVector2 bIndex)
    {
        if (bIndex.x >= aIndex.x - 1 && bIndex.x <= aIndex.x + 1 && bIndex.y >= aIndex.y - 1 && bIndex.y <= aIndex.y + 1)
        {
            if (bIndex.x == aIndex.x && bIndex.y == aIndex.y) return false;

            //Is the index on an even column?
            if (aIndex.x % 2 == 0)
            {
                if ((bIndex.x == aIndex.x - 1 || bIndex.x == aIndex.x + 1) && bIndex.y == aIndex.y - 1) return false;
            }
            else if ((bIndex.x == aIndex.x - 1 || bIndex.x == aIndex.x + 1) && bIndex.y == aIndex.y + 1) return false;

            return true;
        }

        return false;
    }

    public int RemoveExistingMatches(bool ignoreCallbackEvent = false, bool spawnCollectionEffect = true)
    {
        List<List<IntVector2>> matchIndices = new List<List<IntVector2>>();
        for (int i = 0; i < elementTypes.Length; i++)
        {
            matchIndices.AddRange(FindMatchesOfElementType(elementTypes[i]));
        }

        //Reset flaggedForRemovalByAutoMatch flags
        for (int x = 0; x < gridElements.GetLength(0); x++)
        {
            for (int y = 0; y < gridElements.GetLength(1); y++)
            {
                gridElements[x, y].flaggedForRemovalByAutoMatch = false;
            }
        }

        if (!ignoreCallbackEvent && OnAutoMatchesFound != null)
            OnAutoMatchesFound(matchIndices);

        int removedElementsCount = 0;
        for (int j = 0; j < matchIndices.Count; j++)
        {
            RemoveElementsAtIndices(matchIndices[j], spawnCollectionEffect);
            removedElementsCount += matchIndices[j].Count;
        }
        //print("Removed " + removedElementsCount + " elements due to auto-matching.");

        return matchIndices.Count;
    }

    public void RemoveElementAtIndex(IntVector2 gridIndex, bool disableElementTransform = true, bool spawnCollectionElement = true)
    {
        if (spawnCollectionElement)
        {
            effectManager.SpawnCollectionEffectOnIndex(gridIndex);
        }

        if (disableElementTransform)
        {
            gridElements[gridIndex.x, gridIndex.y].elementTransform.gameObject.SetActive(false);
        }

        gridElements[gridIndex.x, gridIndex.y].elementTransform = null;
        gridElements[gridIndex.x, gridIndex.y].elementType = null;
    }

    public void RemoveElementsAtIndices(List<IntVector2> gridIndices, bool spawnCollectionEffects = true)
    {
        for (int i = 0; i < gridIndices.Count; i++)
        {
            RemoveElementAtIndex(gridIndices[i], spawnCollectionElement: spawnCollectionEffects);
        }
    }

    public void RemoveAllElements()
    {
        for (int x = 0; x < gridElements.GetLength(0); x++)
        {
            for (int y = 0; y < gridElements.GetLength(1); y++)
            {
                gridElements[x, y].elementTransform.gameObject.SetActive(false);
            }
        }

        gridElements = new GridElementData[gridWidth, gridHeight];
    }

    private bool CheckForEmptyGridIndices()
    {
        for (int x = 0; x < gridElements.GetLength(0); x++)
        {
            for (int y = 0; y < gridElements.GetLength(1); y++)
            {
                if (gridElements[x, y].elementTransform == null)
                {
                    return true;
                }
            }
        }

        return false;
    }

    public void FillGrid(bool fullSpawn = false)
    {
        int spawnCount = 0;

        while (CheckForEmptyGridIndices())
        {
            //Find all empty indices
            for (int x = gridElements.GetLength(0) - 1; x >= 0; x--)
            {
                for (int y = gridElements.GetLength(1) - 1; y >= 0; y--)
                {
                    if (gridElements[x, y].elementTransform == null)
                    {
                        //Find the correct index for the element to descend
                        Vector3 correctWorldPos = CalculateWorldPos(new IntVector2(x, y));
                        Vector3 descendingElementWorldPos = correctWorldPos - fallDirection * hexHeight;
                        IntVector2 descendingElementIndex = (y - 1 >= 0)
                            ? new IntVector2(x, y - 1)
                            : GetGridIndexFromWorldPosition(descendingElementWorldPos);

                        //print(x + "|" + y + " correctWorldPos: " + correctWorldPos + ", fallDirection: " + fallDirection
                        //    + ", hexHeight: " + hexHeight);
                        //print("descendingElementWorldPos: " + descendingElementWorldPos + ", descendingElementIndex: " + descendingElementIndex);
                        if (descendingElementIndex.x >= 0 && descendingElementIndex.x < gridElements.GetLength(0)
                            && descendingElementIndex.y >= 0 && descendingElementIndex.y < gridElements.GetLength(1))
                        {
                            //"Drop" elements above the empty indices to fill the empty ones
                            GridElementData gridElement = gridElements[descendingElementIndex.x, descendingElementIndex.y];
                            if (!(fullSpawn && gridElement.elementTransform == null))
                            {
                                gridElements[x, y] = gridElement;
                                gridElements[x, y].correctWorldPos = correctWorldPos;
                                RemoveElementAtIndex(descendingElementIndex, false, false);
                                //print("Dropped an element from " + descendingElementIndex + " to fill an empty index at " + x + "|" + y);
                                continue;
                            }
                        }

                        //Count the number of elements spawned on the same column on this turn
                        int numberOfNewlySpawnedElementsUnderThisOne = 0;
                        for (int y2 = y + 1; y2 < gridElements.GetLength(1); y2++)
                        {
                            GridElementData gridElement = gridElements[x, y2];
                            if (gridElement.elementTransform != null)
                            {
                                if (gridElement.justSpawned)
                                {
                                    numberOfNewlySpawnedElementsUnderThisOne++;
                                }
                            }
                        }

                        //Calculate the proper spawn position
                        Vector3 spawnPosOffset = -fallDirection * hexHeight
                            * (numberOfNewlySpawnedElementsUnderThisOne + 1);

                        if (!fullSpawn)
                        {
                            spawnPosOffset *= Mathf.Pow(spawnOffsetPerRow, numberOfNewlySpawnedElementsUnderThisOne + 1);
                        }

                        spawnPosOffset.y += minNewElementSpawnYPos;
                        descendingElementWorldPos = correctWorldPos + spawnPosOffset;

                        //print("FillGrid: Creating new element at " + x + "|" + y + ", spawnPos: " + descendingElementWorldPos 
                        //    + ", numberOfNewlySpawnedElementsUnderThisOne: " + numberOfNewlySpawnedElementsUnderThisOne);

                        CreateNewGridElement(ChooseRandomElementType(), descendingElementWorldPos, new IntVector2(x, y), this.transform);
                        spawnCount++;
                    }
                }
            }
        }
    }

    public void Restart()
    {
        isElementMovementDone = false;

        RemoveAllElements();
        InitializeGridElements();
    }
}

public struct GridElementData
{
    public ElementType elementType;
    public Transform elementTransform;
    public Vector3 correctWorldPos;
    public bool justSpawned;
    public bool flaggedForRemovalByAutoMatch;

    public GridElementData(ElementType _elementType, Transform _elementTransform, Vector3 _correctWorldPos)
    {
        elementType = _elementType;
        elementTransform = _elementTransform;
        correctWorldPos = _correctWorldPos;
        justSpawned = true;
        flaggedForRemovalByAutoMatch = false;
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

