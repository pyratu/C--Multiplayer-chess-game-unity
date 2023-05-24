using System.Collections.Generic;
using UnityEngine;

public class Pawn : ChessPiece
{
    public override List<Vector2Int> GetAvailableMoves(ref ChessPiece[,] board, int tileCountX, int tileCountY)
    {
        List<Vector2Int> r = new List<Vector2Int>();
        
        int direction = (team == 0) ? 1 : -1;

        // o patratica in fata
        if (board[currentX, currentY + direction] == null)
            r.Add(new Vector2Int(currentX, currentY + direction));

        //doua patratici in fata
        if (board[currentX, currentY + direction] == null)
        {
            if(team == 0 && currentY == 1 && board[currentX,currentY+(direction*2)]==null)
                r.Add(new Vector2Int(currentX, currentY+(direction*2)));
            if (team == 1 && currentY == 6 && board[currentX, currentY + (direction * 2)] == null)
                r.Add(new Vector2Int(currentX, currentY + (direction * 2)));
        }

        //atak
        if(currentX != tileCountX-1)
            if(board[currentX+1,currentY+direction]!= null && board[currentX + 1, currentY + direction].team != team)
                r.Add(new Vector2Int(currentX+1, currentY+direction));
        if (currentX != 0)
            if (board[currentX - 1, currentY + direction] != null && board[currentX - 1, currentY + direction].team != team)
                r.Add(new Vector2Int(currentX - 1, currentY + direction));

        return r;
    }

    public override SpecialMove GetSpecialMoves(ref ChessPiece[,] board, ref List<Vector2Int[]> movelist, ref List<Vector2Int> availableMoves)
    {
        int direction = (team == 0) ? 1 : -1;
        if ((team == 0 && currentY == 6) || (team == 1 && currentY == 1))
            return SpecialMove.Promotion;


        //en passant
        if(movelist.Count>0)
        {
            Vector2Int[] lastMove = movelist[movelist.Count-1];
            if(board[lastMove[1].x, lastMove[1].y].type == ChessPieceType.Pawn) // daca ultima piesa mutata a fost pion
            {
                if(Mathf.Abs(lastMove[0].y - lastMove[1].y)==2) // daca ultima piesa a fost +2 in orice directie
                {
                    if(board[lastMove[1].x, lastMove[1].y].team !=team) // daca mutarea a fost de la cealalta echipa
                    {
                        if(lastMove[1].y==currentY) // daca ambii pioni sunt pe acelasi Y
                        {
                            if (lastMove[1].x == currentX - 1) // in stanga
                            {
                                availableMoves.Add(new Vector2Int(currentX - 1, currentY + direction));
                                return SpecialMove.EnPassant;
                            }
                            if (lastMove[1].x == currentX + 1) // in dreapta
                            {
                                availableMoves.Add(new Vector2Int(currentX + 1, currentY + direction));
                                return SpecialMove.EnPassant;
                            }
                        }
                    }
                }
            }
        }

        return SpecialMove.None;
    }

}

    