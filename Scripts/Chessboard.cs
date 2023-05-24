using System;
using System.Collections.Generic;
using Unity.Networking.Transport;
using UnityEngine;
using UnityEngine.UI;

public enum SpecialMove
{
    None = 0,
    EnPassant,
    Castling,
    Promotion
}

public class Chessboard : MonoBehaviour
{
    [Header("Art stuff")]

    [SerializeField] private Material tileMaterial;
    [SerializeField] private float tileSize = 1.0f;
    [SerializeField] private float yOffset = 0.2f;
    [SerializeField] private Vector3 boardCenter = Vector3.zero;
    [SerializeField] private float deathSize = 0.3f;
    [SerializeField] private float deathSpacing = 0.3f;
    [SerializeField] private float dragOffset = 1.5f;
    [SerializeField] private GameObject victoryScreen;
    [SerializeField] private Transform rematchIndicator;
    [SerializeField] private Button rematchButton;



    [Header("prefabs si materiale")]
    [SerializeField] private GameObject[] prefabs;
    [SerializeField] private Material[] teamMaterials;


    // LOGIC
    private const int TILE_COUNT_X = 8;
    private const int TILE_COUNT_Y = 8;
    private List<Vector2Int> availableMoves = new List<Vector2Int>();
    private List<ChessPiece> deadWhites = new List<ChessPiece>();
    private List<ChessPiece> deadBlacks = new List<ChessPiece>();
    private GameObject[,] tiles;
    private Camera currentCamera;
    private Vector2Int currentHover;
    private Vector3 bounds;
    private ChessPiece[,] ChessPieces;
    private ChessPiece currentlyDragging;
    private bool isWhiteTurn;
    private SpecialMove specialMove;
    private List<Vector2Int[]> moveList = new List<Vector2Int[]>();

    public static bool MenuInJoc = false;
    public GameObject MenuUI;

    //mai multa logica
    private int playerCount = -1;
    private int currentTeam = -1;
    private bool localGame = true;
    private bool[] playerRematch = new bool[2];

    private void Start()
    {
        isWhiteTurn = true;
        GenerateAllTiles(tileSize, TILE_COUNT_X, TILE_COUNT_Y);
        SpawnAllPieces();
        PositionAllPieces();

        RegisterEvents();
    }

    private void Update()
    {
        void Dispare()
        {
            MenuUI.SetActive(false);
            MenuInJoc = false;
        }
        void Apare()
        {
            MenuUI.SetActive(true);
            MenuInJoc = true;
        }
        if (playerCount > -1)
        {
                if (Input.GetKeyUp(KeyCode.Escape))
                    if (MenuInJoc)
                    {
                        Dispare();
                    }
                    else
                    {

                        Apare();
                    }
        }
       if(playerCount == -1)
        {
            Dispare();
        }

        if (!currentCamera)
        {
            currentCamera = Camera.main;
            return;
        }

        RaycastHit info;
        Ray ray = currentCamera.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out info, 100, LayerMask.GetMask("Tile", "Hover", "Highlight")))
        {
            // Get the indexes of the tile i've hit
            Vector2Int hitPosition = LookupTileIndex(info.transform.gameObject);

            // If we're hovering a tile after not hovering any tiles
            if (currentHover == -Vector2Int.one)
            {
                currentHover = hitPosition;
                tiles[hitPosition.x, hitPosition.y].layer = LayerMask.NameToLayer("Hover");
            }

            // If we were already hovering a tile, change the previous one
            if (currentHover != hitPosition)
            {
                tiles[currentHover.x, currentHover.y].layer = (ContainsValidMove(ref availableMoves, currentHover)) ? LayerMask.NameToLayer("Highlight") : LayerMask.NameToLayer("Tile");
                currentHover = hitPosition;
                tiles[hitPosition.x, hitPosition.y].layer = LayerMask.NameToLayer("Hover");
            }

            //daca apasam pe click
            if (Input.GetMouseButtonDown(0))
            {
                if (ChessPieces[hitPosition.x,hitPosition.y]!=null)
                {
                    // este randu meu?
                    if ((ChessPieces[hitPosition.x, hitPosition.y].team == 0 && isWhiteTurn && currentTeam == 0) || (ChessPieces[hitPosition.x, hitPosition.y].team == 1 && !isWhiteTurn && currentTeam == 1))
                    {
                        currentlyDragging = ChessPieces[hitPosition.x, hitPosition.y];


                        //fa o lista unde pot sa mut si hasureaza patratelele 
                        availableMoves = currentlyDragging.GetAvailableMoves(ref ChessPieces, TILE_COUNT_X, TILE_COUNT_Y);
                        //fa o lista cu mutari speciale
                        specialMove = currentlyDragging.GetSpecialMoves(ref ChessPieces, ref moveList, ref availableMoves);

                        PreventCheck();
                        HighlightTiles();
                    }
                }
                
            }

            //daca dam drumu la click
            if (currentlyDragging != null && Input.GetMouseButtonUp(0))
            {
                Vector2Int previousPosition = new Vector2Int(currentlyDragging.currentX, currentlyDragging.currentY);
                if (ContainsValidMove(ref availableMoves, new Vector2Int(hitPosition.x, hitPosition.y)))
                {
                MoveTo(previousPosition.x, previousPosition.y, hitPosition.x, hitPosition.y);

                    //implementare net
                    NetMakeMove mm = new NetMakeMove();
                    mm.originalX = previousPosition.x;
                    mm.originalY = previousPosition.y;
                    mm.destinationX = hitPosition.x;
                    mm.destinationY = hitPosition.y;
                    mm.teamId = currentTeam;
                    Client.Instance.SendToServer(mm);
                }
                else
                {
                    currentlyDragging.SetPosition(GetTileCenter(previousPosition.x, previousPosition.y));

                    currentlyDragging = null;
                    RemoveHighlightTiles();

                }


            }
        }
        else
        {
            if (currentHover != -Vector2Int.one)
            {
                tiles[currentHover.x, currentHover.y].layer = (ContainsValidMove(ref availableMoves, currentHover)) ? LayerMask.NameToLayer("Highlight") : LayerMask.NameToLayer("Tile");
                currentHover = -Vector2Int.one;
            }

            if (currentlyDragging && Input.GetMouseButtonUp(0))
            {
                currentlyDragging.SetPosition(GetTileCenter(currentlyDragging.currentX, currentlyDragging.currentY));
                currentlyDragging = null;
                RemoveHighlightTiles();
            }
        }

        // sa arate frumos cand tragem o piesa
        if (currentlyDragging)
        {
            Plane horizontalPlane = new Plane(Vector3.up, Vector3.up * yOffset);
            float distance = 0.0f;
            if (horizontalPlane.Raycast(ray, out distance))
                currentlyDragging.SetPosition(ray.GetPoint(distance) + Vector3.up * dragOffset);
        }
    }





    // Generate tabla
    private void GenerateAllTiles(float tileSize, int tileCountX, int tileCountY)
    {

        yOffset += transform.position.y;
        bounds = new Vector3((tileCountX / 2) * tileSize, 0, (tileCountX / 2) * tileSize) + boardCenter;

        tiles = new GameObject[tileCountX, tileCountY];
        for (int x = 0; x < tileCountX; x++)
            for (int y = 0; y < tileCountY; y++)
                tiles[x, y] = GenerateSingleTile(tileSize, x, y);
    }
    private GameObject GenerateSingleTile(float tileSize, int x, int y)
    {
        GameObject tileObject = new GameObject(string.Format("X:{0}, Y:{1}", x, y));
        tileObject.transform.parent = transform;

        Mesh mesh = new Mesh();
        tileObject.AddComponent<MeshFilter>().mesh = mesh;
        tileObject.AddComponent<MeshRenderer>().material = tileMaterial;

        Vector3[] vertices = new Vector3[4];
        vertices[0] = new Vector3(x * tileSize, yOffset, y * tileSize) - bounds;
        vertices[1] = new Vector3(x * tileSize, yOffset, (y + 1) * tileSize) - bounds;
        vertices[2] = new Vector3((x + 1) * tileSize, yOffset, y * tileSize) - bounds;
        vertices[3] = new Vector3((x + 1) * tileSize, yOffset, (y + 1) * tileSize) - bounds;

        int[] trins = new int[] { 0, 1, 2, 1, 3, 2 };

        mesh.vertices = vertices;
        mesh.triangles = trins;

        mesh.RecalculateNormals();

        tileObject.layer = LayerMask.NameToLayer("Tile");
        tileObject.AddComponent<BoxCollider>();

        return tileObject;
    }

    //spawneaza piesele
    private void SpawnAllPieces()
    {
        ChessPieces = new ChessPiece[TILE_COUNT_X, TILE_COUNT_Y];

        int whiteTeam = 0;
        int blackTeam = 1;

        //white team
        ChessPieces[0, 0] = SpawnSinglePiece(ChessPieceType.Rook, whiteTeam);
        ChessPieces[1, 0] = SpawnSinglePiece(ChessPieceType.Knight, whiteTeam);
        ChessPieces[2, 0] = SpawnSinglePiece(ChessPieceType.Bishop, whiteTeam);
        ChessPieces[3, 0] = SpawnSinglePiece(ChessPieceType.Queen, whiteTeam);
        ChessPieces[4, 0] = SpawnSinglePiece(ChessPieceType.King, whiteTeam);
        ChessPieces[5, 0] = SpawnSinglePiece(ChessPieceType.Bishop, whiteTeam);
        ChessPieces[6, 0] = SpawnSinglePiece(ChessPieceType.Knight, whiteTeam);
        ChessPieces[7, 0] = SpawnSinglePiece(ChessPieceType.Rook, whiteTeam);
          for (int i = 0; i < TILE_COUNT_X; i++)
             ChessPieces[i, 1] = SpawnSinglePiece(ChessPieceType.Pawn, whiteTeam);

        //black team
        ChessPieces[0, 7] = SpawnSinglePiece(ChessPieceType.Rook, blackTeam);
        ChessPieces[1, 7] = SpawnSinglePiece(ChessPieceType.Knight, blackTeam);
        ChessPieces[2, 7] = SpawnSinglePiece(ChessPieceType.Bishop, blackTeam);
        ChessPieces[3, 7] = SpawnSinglePiece(ChessPieceType.Queen, blackTeam);
        ChessPieces[4, 7] = SpawnSinglePiece(ChessPieceType.King, blackTeam);
        ChessPieces[5, 7] = SpawnSinglePiece(ChessPieceType.Bishop, blackTeam);
        ChessPieces[6, 7] = SpawnSinglePiece(ChessPieceType.Knight, blackTeam);
        ChessPieces[7, 7] = SpawnSinglePiece(ChessPieceType.Rook, blackTeam);
           for (int i = 0; i < TILE_COUNT_X; i++)
           ChessPieces[i, 6] = SpawnSinglePiece(ChessPieceType.Pawn, blackTeam);

    }
    private ChessPiece SpawnSinglePiece(ChessPieceType type, int team)
    {
        ChessPiece cp = Instantiate(prefabs[(int)type - 1], transform).GetComponent<ChessPiece>();

        cp.type = type;
        cp.team = team;
        cp.GetComponent<MeshRenderer>().material = teamMaterials[((team == 0) ? 0 : 6) + ((int)type - 1)];

        return cp;
    }

    //pozitionarea pieselor
    private void PositionAllPieces()
    {
        for (int x = 0; x < TILE_COUNT_X; x++)
            for (int y = 0; y < TILE_COUNT_Y; y++)
                if (ChessPieces[x, y] != null)
                    PositionSinglePiece(x, y, true);
    }

    private void PositionSinglePiece(int x, int y, bool force = false)
    {
        ChessPieces[x, y].currentX = x;
        ChessPieces[x, y].currentY = y;
        ChessPieces[x, y].SetPosition(GetTileCenter(x, y), force);
    }

    private Vector3 GetTileCenter(int x, int y)
    {
        return new Vector3(x * tileSize, yOffset, y * tileSize) - bounds + new Vector3(tileSize / 2, 0, tileSize / 2);
    }

    //HighlightTiles()
    private void HighlightTiles()
    {
        for (int i = 0; i < availableMoves.Count; i++)
        {
            tiles[availableMoves[i].x, availableMoves[i].y].layer = LayerMask.NameToLayer("Highlight");
        }
    }

    private void RemoveHighlightTiles()
    {
        for (int i = 0; i < availableMoves.Count; i++)

            tiles[availableMoves[i].x, availableMoves[i].y].layer = LayerMask.NameToLayer("Tile");
        availableMoves.Clear();

    }

    //sah mat
    private void CheckMate(int team)
    {
        DisplayVictory(team);
    }
    private void DisplayVictory(int winningTeam)
    {
        victoryScreen.SetActive(true);
        victoryScreen.transform.GetChild(winningTeam).gameObject.SetActive(true);
        
        
    }
    public void OnRematchButton()
    {
        if(localGame)
        {
            NetRematch wrm = new NetRematch();
            wrm.teamId = 0;
            wrm.wantRematch = 1;
            Client.Instance.SendToServer(wrm);

            NetRematch brm = new NetRematch();
            brm.teamId = 1;
            brm.wantRematch = 1;
            Client.Instance.SendToServer(brm);
        }
        else
        {
        NetRematch rm = new NetRematch();
        rm.teamId = currentTeam;
        rm.wantRematch = 1;
        Client.Instance.SendToServer(rm);

        }
        

    }
    public void GameReset()
    {
        rematchButton.interactable = true;


        rematchIndicator.transform.GetChild(0).gameObject.SetActive(false);
        rematchIndicator.transform.GetChild(1).gameObject.SetActive(false);

        victoryScreen.transform.GetChild(0).gameObject.SetActive(false);
        victoryScreen.transform.GetChild(1).gameObject.SetActive(false);
        victoryScreen.SetActive(false);

        //field reset
        currentlyDragging = null;
        availableMoves.Clear();
        moveList.Clear();
        playerRematch[0] = playerRematch[1] = false;


        //curatam tabla

        for(int x = 0; x< TILE_COUNT_X; x++)
        {
            for(int y = 0; y< TILE_COUNT_Y; y++)
            {
                if (ChessPieces[x,y]!=null)
                    Destroy(ChessPieces[x,y].gameObject);

                ChessPieces[x, y] = null;
            }
        }
        for (int i = 0; i < deadWhites.Count; i++)
            Destroy(deadWhites[i].gameObject);
        for (int i = 0; i < deadBlacks.Count; i++)
            Destroy(deadBlacks[i].gameObject);

        deadWhites.Clear();
        deadBlacks.Clear();
        
        SpawnAllPieces();
        PositionAllPieces();
        isWhiteTurn = true;

    }
    public void OnMenuButton()
    {
      
        NetRematch rm = new NetRematch();
        rm.teamId = currentTeam;
        rm.wantRematch = 0;
        Client.Instance.SendToServer(rm);
        
        GameReset();
        GameUI.Instance.OnLeaveFromGameMenu();

        Invoke(nameof(ShutDownRelay), 1.0f);

        //resetare valori
        playerCount = -1;
        currentTeam = -1;
    }

    //mutari speciale
    private void ProcessSpecialMove()
    {
        if(specialMove == SpecialMove.EnPassant)
        {
            var newMove = moveList[moveList.Count-1];
            ChessPiece myPawn = ChessPieces[newMove[1].x, newMove[1].y];
            var targetPawnPosition = moveList[moveList.Count - 2];
            ChessPiece enemyPawn = ChessPieces[targetPawnPosition[1].x, targetPawnPosition[1].y];

            if(myPawn.currentX==enemyPawn.currentX)
            {
                if(myPawn.currentY == enemyPawn.currentY-1 || myPawn.currentY == enemyPawn.currentY + 1)
                {
                    if(enemyPawn.team ==0)
                    {
                        deadWhites.Add(enemyPawn);
                        enemyPawn.SetScale(Vector3.one * deathSize);
                        enemyPawn.SetPosition(new Vector3(8 * tileSize, yOffset, -1 * tileSize) - bounds + new Vector3(tileSize / 2, 0, tileSize / 2) + (Vector3.forward * deathSpacing) * deadWhites.Count);
                    }
                    else
                    {
                        deadBlacks.Add(enemyPawn);
                        enemyPawn.SetScale(Vector3.one * deathSize);
                        enemyPawn.SetPosition(new Vector3(-1 * tileSize, yOffset, 8 * tileSize) - bounds + new Vector3(tileSize / 2, 0, tileSize / 2) + (Vector3.back * deathSpacing) * deadBlacks.Count);
                    }
                    ChessPieces[enemyPawn.currentX, enemyPawn.currentY] = null;
                }
            }
        }
        
        if(specialMove == SpecialMove.Promotion)
        {
            Vector2Int[] lastMove = moveList[moveList.Count - 1];
            ChessPiece targetPawn = ChessPieces[lastMove[1].x,lastMove[1].y];

            if(targetPawn.type == ChessPieceType.Pawn)
            {
                if(targetPawn.team == 0 && lastMove[1].y == 7)
                {
                 //alb promotie
                    ChessPiece newQueen = SpawnSinglePiece(ChessPieceType.Queen, 0);
                    newQueen.transform.position = ChessPieces[lastMove[1].x, lastMove[1].y].transform.position;
                    Destroy(ChessPieces[lastMove[1].x, lastMove[1].y].gameObject);
                    ChessPieces[lastMove[1].x, lastMove[1].y] = newQueen;
                    PositionSinglePiece(lastMove[1].x, lastMove[1].y);

                }
                //negru promotie
            
                    if (targetPawn.team == 1 && lastMove[1].y == 0)
                    {
                        ChessPiece newQueen = SpawnSinglePiece(ChessPieceType.Queen, 1);
                        newQueen.transform.position = ChessPieces[lastMove[1].x, lastMove[1].y].transform.position;
                        Destroy(ChessPieces[lastMove[1].x, lastMove[1].y].gameObject);
                        ChessPieces[lastMove[1].x, lastMove[1].y] = newQueen;
                        PositionSinglePiece(lastMove[1].x, lastMove[1].y);

                    }
                }
        }

        if(specialMove == SpecialMove.Castling)
        {
            Vector2Int[] lastMove = moveList[moveList.Count - 1];

            //stanga rook
            if(lastMove[1].x == 2)
            {
                if(lastMove[1].y == 0) // albu\
                {
                    ChessPiece rook = ChessPieces[0,0];
                    ChessPieces[3, 0] = rook;
                    PositionSinglePiece(3, 0);
                    ChessPieces[0, 0] = null;
                }
                else if (lastMove[1].y == 7) //negru
                {
                    ChessPiece rook = ChessPieces[0, 7];
                    ChessPieces[3, 7] = rook;
                    PositionSinglePiece(3, 7);
                    ChessPieces[0, 7] = null;
                }
            }
            //dreapta rook
            else if(lastMove[1].x == 6)
            {
                if (lastMove[1].y == 0) // albu\
                {
                    ChessPiece rook = ChessPieces[7, 0];
                    ChessPieces[5, 0] = rook;
                    PositionSinglePiece(5, 0);
                    ChessPieces[7, 0] = null;
                }
                else if (lastMove[1].y == 7) //negru
                {
                    ChessPiece rook = ChessPieces[7, 7];
                    ChessPieces[5, 7] = rook;
                    PositionSinglePiece(5, 7);
                    ChessPieces[7, 7] = null;
                }
            }

        }
    }
    private void PreventCheck()
    {
        ChessPiece targetKing = null;
        for (int x = 0; x < TILE_COUNT_X; x++)
            for (int y = 0; y < TILE_COUNT_Y; y++)
                if(ChessPieces[x, y] != null)
                  if(ChessPieces[x,y].type == ChessPieceType.King)
                    if(ChessPieces[x,y].team == currentlyDragging.team)
                        targetKing = ChessPieces[x,y];


        // deoarece trimitem ref pe mutari disponibile, o sa stergem mutarile care ne pune in sah
        SimulateMoveForSinglePiece(currentlyDragging,ref availableMoves, targetKing);
    }
    private void SimulateMoveForSinglePiece(ChessPiece cp, ref List<Vector2Int> moves, ChessPiece targetKing)
    {
        //salveaza valorile curente, pt a le reseta in apelul functiei
        int actualX = cp.currentX;
        int actualY = cp.currentY;
        List<Vector2Int> movesToRemove = new List<Vector2Int>();

        //verificam toate mutarile, verificam daca suntem in sah
        for (int i = 0; i < moves.Count; i++)
        {
            int simX = moves[i].x;
            int simY = moves[i].y;

            Vector2Int kingPositionThisSim = new Vector2Int(targetKing.currentX,targetKing.currentY);
            //am simulat mutarile regelui
            if(cp.type == ChessPieceType.King)
                kingPositionThisSim = new Vector2Int(simX,simY);

            //copiam  [,] ci nu referenta
            ChessPiece[,] simulation = new ChessPiece[TILE_COUNT_X, TILE_COUNT_Y];
            List<ChessPiece> simAttackingPieces = new List<ChessPiece>();
            for (int x = 0; x < TILE_COUNT_X; x++)
            {
                for (int y = 0; y < TILE_COUNT_Y; y++)
                {
                    if(ChessPieces[x,y] != null)
                    {
                        simulation[x,y] = ChessPieces[x,y];
                        if (simulation[x, y].team != cp.team)
                            simAttackingPieces.Add(simulation[x, y]);

                    }
                }
            }

            // simulam miscarea
            simulation[actualX, actualY] = null;
            cp.currentX = simX;
            cp.currentY = simY;
            simulation[simX,simY] = cp;

            // a murit vreo piesa in timp ce simulam?
            var deadPiece = simAttackingPieces.Find(c=> c.currentX == simX && c.currentY == simY);
            if(deadPiece != null)
                simAttackingPieces.Remove(deadPiece);

            //ia toate simularile miscari atacatoare

            List<Vector2Int> simMoves = new List<Vector2Int>();
            for(int a = 0; a< simAttackingPieces.Count; a++)
            {
                var pieceMoves = simAttackingPieces[a].GetAvailableMoves(ref simulation, TILE_COUNT_X,TILE_COUNT_Y);
                for (int b = 0; b < pieceMoves.Count; b++)
                    simMoves.Add(pieceMoves[b]);
            }

            //are regele probleme? omagaaa daca da n avem voie
            if(ContainsValidMove(ref simMoves, kingPositionThisSim))
            {
                movesToRemove.Add(moves[i]);
            }

            //retauram data cp
            cp.currentX = actualX;
            cp.currentY = actualY;
        }


        //sterge din mutarile disponibile lista
        for (int i = 0; i < movesToRemove.Count; i++)
            moves.Remove(movesToRemove[i]);
        
    }
    private bool CheckForCheckmate()
    {
        var lastMove = moveList[moveList.Count - 1];
        int targetTeam = (ChessPieces[lastMove[1].x, lastMove[1].y].team == 0) ? 1 : 0;

        List<ChessPiece> attackingPieces = new List<ChessPiece>();
        List<ChessPiece> defendingPieces = new List<ChessPiece>();
        ChessPiece targetKing = null;
        for (int x = 0; x < TILE_COUNT_X; x++)
            for (int y = 0; y < TILE_COUNT_Y; y++)
                if (ChessPieces[x, y] != null)
                {

                    if (ChessPieces[x, y].team == targetTeam)
                    {
                        defendingPieces.Add(ChessPieces[x, y]);
                        if (ChessPieces[x, y].type == ChessPieceType.King)
                            targetKing = ChessPieces[x, y];
                    }
                    else
                    {
                        attackingPieces.Add(ChessPieces[x, y]);
                    }    
                }

        //este regele atacat acu?
        List<Vector2Int> currentAvailableMoves = new List<Vector2Int>();
        for (int i = 0; i < attackingPieces.Count; i++)
        {
            var pieceMoves = attackingPieces[i].GetAvailableMoves(ref ChessPieces, TILE_COUNT_X, TILE_COUNT_Y);
            for (int b = 0; b < pieceMoves.Count; b++)
                currentAvailableMoves.Add(pieceMoves[b]);
        }
        //suntem in sah acu?
        if (ContainsValidMove(ref currentAvailableMoves, new Vector2Int(targetKing.currentX, targetKing.currentY)))
        {
            // regele e atacat, putem sa mutam ceva sa l ajutam?
            for (int i = 0; i < defendingPieces.Count; i++)
            {
                List<Vector2Int> defendingMoves = defendingPieces[i].GetAvailableMoves(ref ChessPieces, TILE_COUNT_X, TILE_COUNT_Y);
                SimulateMoveForSinglePiece(defendingPieces[i], ref defendingMoves, targetKing);

                if (defendingMoves.Count != 0)
                    return false;
            }

            return true; //sah mat
        }
        return false;
    }

    //Operatii
    private bool ContainsValidMove(ref List<Vector2Int> moves, Vector2 pos)
    {
        for (int i = 0; i < moves.Count; i++)
            if (moves[i].x == pos.x && moves[i].y == pos.y)
                return true;

        return false;
    }
    private void MoveTo(int originalX, int originalY, int x, int y)
    {

        ChessPiece cp = ChessPieces[originalX, originalY];


        Vector2Int previousPosition = new Vector2Int(originalX, originalY);

        //mai exista piesa pe casuta?
        if(ChessPieces[x,y] !=null)
        {
            ChessPiece ocp = ChessPieces[x, y];

            if(cp.team == ocp.team)
                return;
        // daca e inamic    
        if(ocp.team == 0)
            {
                if (ocp.type == ChessPieceType.King)
                    CheckMate(1);

                deadWhites.Add(ocp);
                ocp.SetScale(Vector3.one * deathSize);
                ocp.SetPosition(new Vector3(8 * tileSize, yOffset, -1 * tileSize) - bounds + new Vector3(tileSize/2,0,tileSize/2)+(Vector3.forward*deathSpacing)*deadWhites.Count);
            }
        else
            {
                if (ocp.type == ChessPieceType.King)
                    CheckMate(0);

                deadBlacks.Add(ocp);
                ocp.SetScale(Vector3.one * deathSize);
                ocp.SetPosition(new Vector3(-1 * tileSize, yOffset, 8 * tileSize) - bounds + new Vector3(tileSize / 2, 0, tileSize / 2) + (Vector3.back * deathSpacing) * deadBlacks.Count);
        }
        }

        ChessPieces[x, y] = cp;
        ChessPieces[previousPosition.x, previousPosition.y] = null;

        PositionSinglePiece(x, y);

        isWhiteTurn = !isWhiteTurn;
        if (localGame)
            currentTeam = (currentTeam == 0) ? 1 : 0;
        moveList.Add(new Vector2Int[] { previousPosition, new Vector2Int(x, y) });

        ProcessSpecialMove();

        if(currentlyDragging)
        currentlyDragging = null;
        RemoveHighlightTiles();

        if (CheckForCheckmate())
            CheckMate(cp.team);

        return;
    }
    private Vector2Int LookupTileIndex(GameObject hitInfo)
    {
        for (int x = 0; x < TILE_COUNT_X; x++)
            for (int y = 0; y < TILE_COUNT_Y; y++)
                if (tiles[x, y] == hitInfo)
                    return new Vector2Int(x, y);

        return -Vector2Int.one; // tzaca

    }
    #region
    private void RegisterEvents()
    {
        NetUtility.S_WELCOME += OnWelcomeServer;
        NetUtility.S_MAKE_MOVE += OnMakeMoveServer;
        NetUtility.S_REMATCH += OnRematchServer;

        NetUtility.C_WELCOME += OnWelcomeClient;
        NetUtility.C_START_GAME += OnStartGameClient;
        NetUtility.C_MAKE_MOVE += OnMakeMoveClient;
        NetUtility.C_REMATCH += OnRematchClient;

        GameUI.Instance.SetLocalGame += OnSetLocalGame;

    }

 

    private void UnregisterEvents()
    {
        NetUtility.S_WELCOME -= OnWelcomeServer;
        NetUtility.S_MAKE_MOVE -= OnMakeMoveServer;
        NetUtility.S_REMATCH -= OnRematchServer;

        NetUtility.C_WELCOME -= OnWelcomeClient;
        NetUtility.C_START_GAME -= OnStartGameClient;
        NetUtility.C_MAKE_MOVE -= OnMakeMoveClient;
        NetUtility.C_REMATCH -= OnRematchClient;

        GameUI.Instance.SetLocalGame -= OnSetLocalGame;
    }
    //server
    private void OnWelcomeServer(NetMessage msg, NetworkConnection cnn)
    {
        //clientul s a conectat, i s a dat echipa si a dat mesaj inapoi la el
        NetWelcome nw = msg as NetWelcome;

        // da i echipa
        nw.AssignedTeam = ++playerCount;

        //inapoiaza la client
        Server.Instance.SendToClient(cnn, nw);

        // daca e plin incepe jocu
        if(playerCount == 1)
        {
            Server.Instance.Broadcast(new NetStartGame());
        }
    }
    private void OnMakeMoveServer(NetMessage msg, NetworkConnection cnn)
    {
        //primeste mesaju, da l inapoi
        NetMakeMove mm = msg as NetMakeMove;

        //primeste si da si la celalalt broadcast
        Server.Instance.Broadcast(mm); // msg ?? mm
    }
    private void OnRematchServer(NetMessage msg, NetworkConnection cnn)
    {

        Server.Instance.Broadcast(msg);
    }
    //client
    private void OnWelcomeClient(NetMessage msg)
    {
        //am primit mesaju de conexiune
        NetWelcome nw = msg as NetWelcome;

        //da i echipa
        currentTeam = nw.AssignedTeam;

        Debug.Log($"my assinged team is {nw.AssignedTeam} ");

        if (localGame && currentTeam == 0)
        {
            Server.Instance.Broadcast(new NetStartGame());
        }

    }
    private void OnStartGameClient(NetMessage msg)
    {
        GameUI.Instance.ChangedCamera((currentTeam == 0) ? CameraAngle.whiteTeam : CameraAngle.blackTeam);
    }

    private void OnMakeMoveClient(NetMessage msg)
    {
        NetMakeMove mm = msg as NetMakeMove;

        Debug.Log($"MM : {mm.teamId} : {mm.originalX} {mm.originalY} -> {mm.destinationX} {mm.destinationY}");

        if(mm.teamId != currentTeam)
        {
            ChessPiece target = ChessPieces[mm.originalX, mm.originalY];

            availableMoves = target.GetAvailableMoves(ref ChessPieces, TILE_COUNT_X, TILE_COUNT_Y);
            specialMove = target.GetSpecialMoves(ref ChessPieces, ref moveList, ref availableMoves);
            MoveTo(mm.originalX, mm.originalY, mm.destinationX, mm.destinationY);
        }
    }

    private void OnRematchClient(NetMessage msg)
    {
        //primeste mesaju cu conexiune
        NetRematch rm = msg as NetRematch;

        // seteaza bool pt rematch
        playerRematch[rm.teamId] = rm.wantRematch == 1;

        //activeaza ui
        if(rm.teamId != currentTeam)
        { 
            rematchIndicator.transform.GetChild((rm.wantRematch == 1) ? 0 : 1).gameObject.SetActive(true);
            //rematchIndicator.transform.GetChild((rm.wantRematch == 1) ? 0 : 1).gameObject.SetActive(true);

        if(rm.wantRematch != 1)
        {
            rematchButton.interactable = false;
        }
       }
        //daca ambii jucatori vor rematch
        if (playerRematch[0] && playerRematch[1])
            GameReset();
    }

    //
    private void ShutDownRelay()
    {
        Client.Instance.Shutdown();
        Server.Instance.Shutdown();
    }
    private void OnSetLocalGame(bool v)
    {
        playerCount = -1;
        currentTeam = -1;
        localGame = v;
    }
    #endregion
}

