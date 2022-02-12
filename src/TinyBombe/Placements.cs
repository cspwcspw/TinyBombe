using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TinyBombe
{
    public class Placements
    {
        List<Placement> inUse;

        public Placements()
        {
            inUse = new List<Placement>();
        }

        public int PlaceIt(int leftBus, int col, int rightBus)
        {
            int row = 0;
            while (true)
            {
                if (!hasClash(row, leftBus, rightBus)) break;
                row++;
            }

            inUse.Add(new Placement(leftBus, col, rightBus, row));
            return row;
        }

        private bool hasClash(int row, int w, int x)
        {
            foreach (Placement p in inUse)
            {
                if (p.Row == row)
                {
                    int u = p.LeftBus;
                    int v = p.RightBus;
                     //     if (x > u && w < v) return true;
                    if (x >= u && w <= v) return true;
                }
            }
            return false;
        }
    }

    public struct Placement
    {
        public int LeftBus { get; set; }
        public int RightBus { get; set; }

        public int Col { get; set; }

        public int Row { get; set; }

        public Placement(int lb, int col, int rb, int row)
        {
            LeftBus = lb;
            Col = col;
            RightBus = rb;
            Row = row;
        }
    }
}
