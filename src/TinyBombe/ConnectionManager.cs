using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Media;
using System.Windows.Shapes;
// Pete, Feb 2022.

namespace TinyBombe
{

    /// <summary>
    /// Represents  electrical connections between bus wires, diagonal boards and scrambler pins.  
    /// Also encapsulates the GUI objects and manipulates their voltage state and appearance by
    /// cchanging their Stroke brushes.
    /// 
    /// A key assumption is that the bus wiring and the presence (and individual switch states) on
    /// the diagonal board don't change during a bombe run / scan.  The machine will be rebuilt or revired
    /// from scratch if, say, the diagonal board is removed, or any of its switches is changed.
    /// </summary>
    /// 

    public class ConnectionManager 
    {
        Brush HotBrush;
        Brush ColdBrush;

        BusLine[] busLines = null;

        public ConnectionManager(Brush hot, Brush cold) 
        {
            HotBrush = hot;
            ColdBrush = cold;
        }

        public void AddBusLines(Line[] guiLines)
        {
            Debug.Assert(guiLines.Length == 64);
            busLines = new BusLine[64];
            for (int i = 0; i < 64; i++)
            {
                busLines[i] = new BusLine(guiLines[i], i);
            }
        }

        internal void AddScramblerCrossConnect(int leftBus, int inWireIndx, int rightBus, int outWireIndx, Polyline pLine)
        {
            int leftWire = leftBus * 8 + inWireIndx;
            int rightWire = rightBus * 8 + outWireIndx;
            ScramblerEdge sp = new ScramblerEdge(pLine, leftWire, rightWire);
            busLines[leftWire].ScramblerEdges.Add(sp);
            busLines[rightWire].ScramblerEdges.Add(sp);
        }

        internal void AddDiagonalBoardWire(int srcBus, Line pLeft, int dstBus, Line pRight, double y2Posn, bool isClosed)
        {
            int leftWire = srcBus * 8 + dstBus;
            int rightWire = dstBus * 8 + srcBus;

            // First, a visual cheat.The vertical bus wires are extended or cut to exactly
            // terminate at the crosswire channel of the diagonal bus.
            BusLine b1 = busLines[leftWire];
            b1.TheLine.Y2 = y2Posn;
            BusLine b2 = busLines[rightWire];
            b2.TheLine.Y2 = y2Posn;

            // Now attach each end line electrically to its wire.
            // Wires that represent self-steckered hypotheses (A steckered to a)
            // don't cross-connect to others via the diagonal board.
            b1.DbAttachment = pLeft;
            b1.IsDBSwitchClosed = isClosed;
            b1.BusPartner = rightWire;

            b2.DbAttachment = pRight;
            b2.IsDBSwitchClosed = isClosed;
            b2.BusPartner = leftWire;
        }

        internal void ApplyVoltage(int bus, int wire)
        {
            int wireNum = bus * 8 + wire;
            propagateLiveWire(wireNum);
        }

        private void propagateLiveWire(int lineNum)
        {
            BusLine bl = busLines[lineNum];
            if (bl.TheLine.Stroke == HotBrush) return;
            bl.TheLine.Stroke = HotBrush;

            // First deal with a diagonal board connection, if the line has one.
            if (bl.DbAttachment != null)
            {
                // We can safely assume it is not already Hot.
                bl.DbAttachment.Stroke = HotBrush;
                if (bl.IsDBSwitchClosed)
                {
                    propagateLiveWire(bl.BusPartner);
                }
            }

            foreach (ScramblerEdge se in bl.ScramblerEdges)
            {
                if (se.TheLine.Stroke != HotBrush)
                {
                    se.TheLine.Stroke = HotBrush;
                    propagateLiveWire(se.LeftWire);
                    propagateLiveWire(se.RightWire);
                }
            }
        }

        internal void ClearTempsAndVoltage()
        {
            for (int i = 0; i < 64; i++)
            {
                BusLine b = busLines[i];
                b.TheLine.Stroke = ColdBrush;
                if (b.DbAttachment != null)
                {
                    b.DbAttachment.Stroke = ColdBrush;
                }
                b.ScramblerEdges.Clear();
            }
        }

        internal int CountHotWires(int testBus)
        {
            int firstWire = testBus * 8;
            int hotCount = 0;
            for (int w = firstWire; w < firstWire + 8; w++)
            {
                if (busLines[w].TheLine.Stroke == HotBrush)
                {
                    hotCount++;
                }
            }
            return hotCount;
        }
    }

    class BusLine
    {
        public Line TheLine { get; set; }
        public int Indx { get; set; }
        public List<ScramblerEdge> ScramblerEdges { get; set; }

        public Line DbAttachment;
        public bool IsDBSwitchClosed;
        public int BusPartner;

        public BusLine(Line theLine, int indx)
        {
            TheLine = theLine;
            Indx = indx;
            ScramblerEdges = new List<ScramblerEdge>();
            DbAttachment = null;    
            IsDBSwitchClosed = true;
            BusPartner = -1;  // Some buse lines do not have diagonal partners
        }
    }

    class ScramblerEdge
    {
        public Polyline TheLine { get; set; }
        public int LeftWire { get; set; }
        public int RightWire { get; set; }

        public ScramblerEdge(Polyline theLine, int leftLine, int rightLine)
        {
            this.TheLine = theLine;
            this.LeftWire = leftLine;
            this.RightWire = rightLine;
        }
    }
}
