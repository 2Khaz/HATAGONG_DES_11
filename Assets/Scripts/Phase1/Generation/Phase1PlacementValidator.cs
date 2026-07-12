using System.Collections.Generic;
using System.Linq;

namespace HATAGONG.Phase1
{
    public static class Phase1PlacementValidator
    {
        public static bool ValidateFinal(IReadOnlyList<Phase1TilePlacement> tiles,int boardSize,out string error)
        {
            error=null; var grid=new int[boardSize,boardSize];
            foreach(var t in tiles) { if(t.GridX<0||t.GridY<0||t.GridX+t.GridWidth>boardSize||t.GridY+t.GridHeight>boardSize){error="board bounds";return false;}
                for(int y=t.GridY;y<t.GridY+t.GridHeight;y++) for(int x=t.GridX;x<t.GridX+t.GridWidth;x++){if(grid[x,y]!=0){error="overlap";return false;}grid[x,y]=t.TileId+1;} }
            for(int y=0;y<boardSize;y++) for(int x=0;x<boardSize;x++) if(grid[x,y]==0){error="empty cell";return false;}
            var cores=tiles.Where(t=>t.Shape==Phase1TileShape.OneByOne).ToList();
            for(int i=0;i<cores.Count;i++) for(int j=i+1;j<cores.Count;j++) if(EdgeTouch(cores[i],cores[j])){error="adjacent cores";return false;}
            foreach(var group in tiles.GroupBy(t=>t.Shape)) if(HasThreeConnected(group.ToList())||HasThreeInLine(group.ToList())){error="three same-shape tiles connected";return false;}
            return true;
        }
        public static bool EdgeTouch(Phase1TilePlacement a,Phase1TilePlacement b) =>
            ((a.GridX+a.GridWidth==b.GridX||b.GridX+b.GridWidth==a.GridX)&&Overlap(a.GridY,a.GridY+a.GridHeight,b.GridY,b.GridY+b.GridHeight))||
            ((a.GridY+a.GridHeight==b.GridY||b.GridY+b.GridHeight==a.GridY)&&Overlap(a.GridX,a.GridX+a.GridWidth,b.GridX,b.GridX+b.GridWidth));
        private static bool Overlap(int a0,int a1,int b0,int b1)=>a0<b1&&b0<a1;
        private static bool HasThreeConnected(List<Phase1TilePlacement> list)
        { var seen=new HashSet<int>(); for(int i=0;i<list.Count;i++){if(seen.Contains(i))continue;int count=0;var q=new Queue<int>();q.Enqueue(i);seen.Add(i);while(q.Count>0){int n=q.Dequeue();count++;for(int j=0;j<list.Count;j++)if(!seen.Contains(j)&&EdgeTouch(list[n],list[j])){seen.Add(j);q.Enqueue(j);}}if(count>=3)return true;}return false; }
        private static bool HasThreeInLine(List<Phase1TilePlacement> list)
        { for(int i=0;i<list.Count;i++)for(int j=i+1;j<list.Count;j++)for(int k=j+1;k<list.Count;k++){var a=list[i];var b=list[j];var c=list[k];if((EdgeTouch(a,b)&&EdgeTouch(b,c))||(EdgeTouch(a,c)&&EdgeTouch(c,b))||(EdgeTouch(b,a)&&EdgeTouch(a,c))) return true;}return false; }
    }
}
