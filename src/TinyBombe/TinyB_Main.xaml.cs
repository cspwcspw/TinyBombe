using System;
using System.Collections.Generic;
using System.Windows;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Input;
using System.Text;
using System.Windows.Threading;

// Pete, Jan/Feb 2022.

// Purpose of this program is to help us animate and understand the Bombe, particularly the impact of Welchman's
// diagonal board. So I'm drawing primary inspiration, etc. from http://www.ellsbury.com/bombe3.htm 

// In particular, the tiny Bombe here only has an 8-symbol alphabet.  In my case, three 8-wire rotors and a reflector,
// and all scramblers are "hardwired" to always use the same mapping at each of their 512 rotor positions AAA - HHH.
//
// There is a little "game" built in: you can generate a random intercepted ciphertext message with with a known crib
// in it somewhere, and try to find the crib - wheel positions and plugboard settings.
// Or you can see what happens if you weaken or strenghten the crib, or forego the use of Welchman's diagonal
// board entirely, or open switches to umplug some of the diagonal board connections.
//
// There is also a feature to encrypt your own plaintext (limited to A-H, of course, and the plugs in the plugboard.)

namespace TinyBombe
{
    public partial class MainWindow : Window
    {

        #region Fields and constant declarations that are used for positioning things on the Bombe's Canvas
        // Layout is on a (conceptual) grid of rows and columns.  Each column contains a bus of wires,
        // and a channel to the right for scramblers.
        // Layout is mainly lots of hacks and magic coordinates until I think it looks pretty.


        const int WindowTopMargin = 100; // reserve area at top for menus, buttons, etc above the canvas.
        const int ColWidth = 140;
        const int RowHeight = 76;
        const double WireChannelWidth = 7;
        const double ScramblerSize = 8*WireChannelWidth;
        const double WireThickness = 1.0;
        const int LeftMargin = 30;
        
        // This margin is space on the canvas above the rows is used for layout/wiring of the diagonal board cross-connects.
        const double DiagonalBoardHeight = 18 * WireChannelWidth;
        const int TopBusMargin = 30;
        const char leftRightArrow = '\u2194';  //https://www.fileformat.info/info/unicode/char/search.htm

        List<Scrambler> Scramblers;

        ConnectionManager cManager;

        Brush HotBrush = Brushes.Red;
        Brush ColdBrush = Brushes.Blue;
        Brush bombeBackgroundBrush = Brushes.LightCyan;

        Dictionary<string, bool> diagonalSwitchClosed = null;   // Persistent across calls to ResetToInitialState, built on first call to makeDiagonalBoard
        ScrollViewer theScroller;  // The WPF designer and I are not particularly good friends, so a lot of GUI stuff is built in code instead. 

        Canvas backLayer;            // Holds the other layers, resizes, scrollable, etc..
        Canvas dotCanvas;            // Connector dots that I want behind everything else and scrambler icons without wiring or connections
        Canvas persistentCanvas;     // Middle layer, persistent wiring 
        Canvas scramblerFixedCanvas;
        Canvas scramblerCrossconnectCanvas;  // Foreground layer, connections from a bus through the scramblers to the other bus.

        Polygon Vcc;            // The voltage source (Voltage at the Common Collector)
        int VccAttachesAt = 34; // this is a packed Column*8+Wire telling us which bus and wire to apply the VCC source voltage to.
        bool VccIsHot = true;   // Persist whether the user wants voltage applied when we advance to other wheel positions

        double botY;  // Calculated depending on crib length and how many scrambler layout rows are needed, used in a couple of places for layout and sizing.

        DateTime startScanCycle; // For measuring performance for development tunings. 

        string uppers = "ABCDEFGH";
        string lowers = "abcdefgh";

        string hintText = "Generate a random puzzle, then ask for it's hint";
  

        bool isFreeRunning = false; // The machine can single-step, or scan all options.

        CipherCribControl ccText;   // Where we set the ciphertext and slide the crib along to find menus

        #endregion

        #region Constructor and Rebuilding Bombe Canvas when anything changes 
        public MainWindow()
        {
            InitializeComponent();
            this.Title = "Pete's Tiny Bombe Playground, V1.4";
            AddSamplesToMenu(); // Oops, there are none yet
             
            backLayer = new Canvas()
            {
                Background = bombeBackgroundBrush,
                
                Width = ColWidth * 8 + LeftMargin
            };

            dotCanvas = makeLayer();
            persistentCanvas = makeLayer();
            scramblerFixedCanvas = makeLayer();
            scramblerCrossconnectCanvas = makeLayer();

            theScroller = new ScrollViewer()    // A scrollviewer liets us deal with canvases that are too large
            {
                Margin = new Thickness(0, WindowTopMargin, 0, 0),
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto
            };

            theScroller.Content = backLayer;
            mainGrid.Children.Add(theScroller);

            // Ciphers and cribs are handled in a special component that keeps things aligned, etc.
            ccText = new CipherCribControl(96);
            ccText.Margin = new Thickness(70, 55, 0, 0);
            mainGrid.Children.Add(ccText);

            // Some testdata to start with.  
            tbWindow.Text = "AAA";

          
            // Initial puzzle is hardwired
            string fullPlainMessage = "ACEDGBEACHHEADGAGBADGBEADEDGBEACHBABEGFEDGDAD";
            ccText.Crib.Text = "BEACHHEAD";
            ccText.Cipher.Text = getFullInterceptedMessage("CAA", fullPlainMessage, "EA DG");

            RebuildBombe();
           
            this.Height = WindowTopMargin + backLayer.Height + 50;   // And first time only, change the Window size. 

            // Attach a few more handlers that we don't want firing from the XAML
            useDiagonals.Click += cbUseDiagonalBoard_Click;
            btnOpenAll.Click += btnOpenAll_Click;
            btnCloseAll.Click += btnCloseAll_Click;
            btnToggleAll.Click += btnToggleAll_Click;
            ccText.EnterKeyPressed += () => { RebuildBombe(); };
            ccText.TextChanged += (isValid) => { 
                RebuildBombe(); 
            };

            setupCribContextMenu();
        }

        private void setupCribContextMenu()
        {
            ContextMenu mnu = new ContextMenu();

            MenuItem pzl = new MenuItem() { Header = "Gereate a random puzzle containing the crib word (must be a single contiguous word)" };
            pzl.Click += setRandomPuzzle;
            mnu.Items.Add(pzl);

            
            MenuItem hintItem = new MenuItem() { Header = "Put hint puzzle in window titlebar" };
            hintItem.Click += btnHint_Click;
            mnu.Items.Add(hintItem);
            MenuItem encryptItem = new MenuItem() { Header = "Encrypt crib using these rotor and plug settings" };
            encryptItem.Click += EncryptItem_Click;
            mnu.Items.Add(encryptItem);



            btnCrib.ContextMenu = mnu;
        }

        Canvas makeLayer()
        {
            Canvas result = new Canvas()
            {
                Background = Brushes.Transparent,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
            };
            backLayer.Children.Add(result);
            return result;
        }

        private void AddSamplesToMenu()
        {
             // ToDo
        }

        double xFor(int bus, int wire)  // General helper for x layout
        {
            double x0 = bus * ColWidth + (wire + 1) * WireChannelWidth + LeftMargin;
            return x0;
        }

        void RebuildBombe()
        {
            // Precompute layout info for the GUI, and fiddle with the canvas height.
            List<int> scramblerRows = getScramblerRowsNeeded(ccText.Crib.Text, ccText.Cipher.Text); 
            int maxRow = scramblerRows.Count == 0 ? 0 : scramblerRows.Max();
            if (maxRow < 5) maxRow = 5;  // Enforce a minimum height
            botY = TopBusMargin + maxRow * RowHeight + DiagonalBoardHeight;
            double heightThatIsAvailable = this.Height - WindowTopMargin;         
            backLayer.Height = Math.Max(botY + DiagonalBoardHeight, heightThatIsAvailable);

            // Build the Frame of the machine, the buses and diagonal board wiring
            persistentCanvas.Children.Clear();
            cManager = new ConnectionManager(HotBrush, ColdBrush); 
            cManager.AddBusLines(makeBuses());

            if ((bool)useDiagonals.IsChecked) { makeDiagonalBoard(); }

            dotCanvas.Children.Clear();

            scramblerFixedCanvas.Children.Clear();
            scramblerCrossconnectCanvas.Children.Clear();


            if (!ccText.ValidCrib) return;
            normalizeCrib();

            // Create and set up scramblers, as dictated by the crib/cipertext alignment
            Scramblers = createScramblers(ccText.Crib.Text, ccText.Cipher.Text, scramblerRows);
            placeScramblerFixedparts();
            addVoltageSource();         
            runUntilStopOrEnd();   // Or just a single-step
            RecoverMessage();
        }

        void runUntilStopOrEnd()
        {
            while (true)   // Within this loop, the machine advances on each step so the new cross-connects (visual and electrical) 
                           // are be recreated for each scrambler.
            {
                cManager.ClearTempsAndVoltage();   // Take all voltage off, rewire cross-connects, and then push voltage through the new network
                addScramberCrossconnects();
                if (VccIsHot) 
                {
                    int bus = VccAttachesAt / 8;
                    int wire = VccAttachesAt % 8;
                    Vcc.Stroke = HotBrush;
                    Vcc.Fill = HotBrush;
                    cManager.ApplyVoltage(bus, wire);
                }
                else
                {
                    Vcc.Stroke = ColdBrush;
                    Vcc.Fill = ColdBrush;
                }

                bool atStop = isTestRegisterTriggered();
                if (atStop)
                {
                    Label stop = new Label() { Content = "Stop!", ToolTip = "Test register detects this as a candidate - Send it for futher manual analysis.", Foreground = Brushes.Black, FontSize = 20, FontWeight = FontWeights.Bold, Background = Brushes.Magenta };
                    Canvas.SetTop(stop, 30);
                    Canvas.SetLeft(stop, xFor(7, 8));
                    persistentCanvas.Children.Add(stop);
                    isFreeRunning = false;
                }

                if (!isFreeRunning) break;

                forceGuiUpdate();

                bool endOfRun = advanceWindow();
                if (endOfRun)
                {
                    Label endRun = new Label() { Content = "End", Foreground = Brushes.Black, FontSize = 20, FontWeight = FontWeights.Bold, Background = Brushes.Yellow };
                    Canvas.SetTop(endRun, 30);
                    Canvas.SetLeft(endRun, xFor(7, 8));
                    persistentCanvas.Children.Add(endRun);
                    isFreeRunning = false;
                    break;
                }
            }
        }


        private void forceGuiUpdate()
        {
            // The freeRunning case / closed processing loops in general in WPF can be tricky.  We need a magic spell.
            // Painting the display is a low-prioity deferred task for WPF. so we have to force WPF to paint before doing the next round,
            // we demand execution of some (empty) code at an even-lower-priority-than-display-updating.
            // In old Windows Forms this was called DoEvents();
            Application.Current.Dispatcher.Invoke(DispatcherPriority.Background, new Action(delegate { }));
        }

        private string getFullInterceptedMessage(string windowKey, string fullPlainMessage, string plugs)
        {
            Scrambler sc = new Scrambler(0, 0, 0, 0);
            sc.Index = Scrambler.FromWindowView(windowKey);
            string error = sc.SetPlugboard(plugs);
          
            if (!String.IsNullOrEmpty(error))
            {
                MessageBox.Show(error, "TinyBombe: Plugboard settings invalid.");
            }
            string cipherText = sc.EncryptText(fullPlainMessage);
            return cipherText;
        }


        private void normalizeCrib()
        {
            string s = ccText.Crib.Text.ToUpper();
            StringBuilder sb = new StringBuilder();
            foreach (char c in s)
            {
                if (c >= 'A' && c <= 'H')
                {
                    sb.Append(c);
                }
                else
                {
                    sb.Append(' ');
                }
            }
            ccText.ReplaceCribText(sb.ToString());
        }

        private void RecoverMessage()
        {
            Scrambler sc = new Scrambler(0, 0, 0, 0);
            sc.Index = Scrambler.FromWindowView(tbWindow.Text);
            string cp = ccText.Cipher.Text;
            string plain = sc.EncryptText(cp);
            if ((bool)cbReplaceSpaces.IsChecked)
            {
                plain = plain.Replace("G", " ");
            }
      
            // Recover plaintext, using plugboard settings
            sc.Index = Scrambler.FromWindowView(tbWindow.Text);
            string error = sc.SetPlugboard(PlugGuesses.Text);
            if (!string.IsNullOrEmpty(error))
            {
                MessageBox.Show(error, "TinyBombe: Plugboard settings invalid.");
            }

            plain = sc.EncryptText(cp);
            if ((bool)cbReplaceSpaces.IsChecked)
            {
                plain = plain.Replace("G", " ");
            }
            RecoveredWithPlugs.Content = plain;
        }

        #endregion

        #region // Freerunning logic to step through all possibilities, stopping on candidate solutions

        bool advanceWindow()
        {
          
            int index = Scrambler.FromWindowView(tbWindow.Text);
            index = (index + 1) % 512;
            tbWindow.Text = Scrambler.ToWindowView(index);
            if (index == 0)
            {
                DateTime endCycle = DateTime.Now;
                this.Title = $"Run time = {(endCycle - startScanCycle).TotalSeconds} secs";
                return true;
            }
            return false;
        }

        private bool isTestRegisterTriggered()
        {
            int testBus = VccAttachesAt / 8;
            int hotCount = cManager.CountHotWires(testBus);
            bool atStop = (hotCount == 1 || hotCount == 7);
            return atStop;
        }

        private void btnScan_Click(object sender, RoutedEventArgs e)
        {
            tbWindow.Text = "AAA";
            PlugGuesses.Text = "";
            startScanCycle = DateTime.Now;
            isFreeRunning = true;
            RebuildBombe();
        }

        private void btnResume_Click(object sender, RoutedEventArgs e)
        {
            // Kick the machine to the next position
            advanceWindow();
            isFreeRunning = true;
            RebuildBombe();
        }

        private void btnPause_Click(object sender, RoutedEventArgs e)
        {
            isFreeRunning = false;
        }

        private void StepTimer_Tick(object sender, EventArgs e)
        {
            int index = Scrambler.FromWindowView(tbWindow.Text);
            index = (index + 1) % 256;
            tbWindow.Text = Scrambler.ToWindowView(index);
            if (index == 0)
            {
                DateTime endCycle = DateTime.Now;
                this.Title = $"Run time = {(endCycle - startScanCycle).TotalMilliseconds} msecs";
            }
            RebuildBombe();
        }
        #endregion

        #region Vcc Source Tag.  Put voltage on the tag, test Steckering hypothesis
        private void addVoltageSource()
        {
            // Add the VCC source
            PointCollection pc = new PointCollection() { new Point(0, 0), new Point(-10, -20), new Point(0, -14), new Point(10, -20), new Point(0, 0) };
            Vcc = new Polygon() { Name = "VccTag", Points = pc, Fill = ColdBrush, Stroke = ColdBrush };
            Vcc.MouseUp += Vcc_MouseUp;
            int attachedBus = VccAttachesAt / 8;
            int attachedWire = VccAttachesAt % 8;

            string tip = $"Tests the hypothesis that {uppers[attachedBus]} is steckered to {uppers[attachedWire]}. If so, no other wire in this bus can light up.";
            Vcc.ToolTip = tip;
            Canvas.SetLeft(Vcc, xFor(attachedBus, attachedWire));
            Canvas.SetTop(Vcc, TopBusMargin);

            persistentCanvas.Children.Add(Vcc);
   
            //if (VccIsHot) // Push the voltage through the wiring
            //{
            //    //  Joints.MakeItLive(busWires[attachedBus, attachedWire]);
            //    theCm.ApplyVoltage(attachedBus, attachedWire);
            //    Vcc.Fill = HotBrush;
            //    Vcc.Stroke = HotBrush;
            //}

            ContextMenu mnu = new ContextMenu();
            for (char bus='A'; bus <= 'H'; bus++)
            {
                MenuItem theItem = new MenuItem() { Header = bus };
                int p = mnu.Items.Add(theItem);
                for (int wire = 0; wire < 8; wire++)
                {
                    MenuItem subItem = new MenuItem() { Header = $"{bus} {leftRightArrow} {(char)('a'+wire)}", Tag = 8*(bus-'A')+wire };
                    subItem.Click += VccSubItem_Click;
                    theItem.Items.Add(subItem);
                }
            }       
            Vcc.ContextMenu = mnu;
        }

        private void Vcc_MouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left)
            {
                return;
            }
            Shape p = sender as Shape;
            if (p.Stroke == HotBrush)
            {
                VccIsHot = false;
                p.Fill = ColdBrush;
                p.Stroke = ColdBrush;
            }
            else
            {
                VccIsHot = true;
                int bus = VccAttachesAt / 8;
                int wire = VccAttachesAt % 8;
                p.Fill = HotBrush;
                p.Stroke = HotBrush;
            }
            RebuildBombe();
        }

        private void VccSubItem_Click(object sender, RoutedEventArgs e)
        {
            MenuItem item = sender as MenuItem;
            int t = (int) item.Tag;
            VccAttachesAt = t;
            RebuildBombe();
            e.Handled = true;
        }

#endregion

        #region Scramblers - decide what is needed, create them with crib offsets, lay them out, plug them into buses, etc.
        private List<int> getScramblerRowsNeeded(string origCrib, string cipher)
        {
            List<int> rows = new List<int>();
            Placements places = new Placements();
            string crib = origCrib.ToUpper();
            for (int i = 0; i < Math.Min(crib.Length, cipher.Length); i++)
            {
                char ch = crib[i];
                if (ch < 'A' || ch > 'H')    // step over wildcard don't-cares in crib, 
                {
                    rows.Add(-1);
                }
                else
                {
                    int leftBus = ch - 'A';
                    int rightBus = cipher[i] - 'A';
                    if (leftBus > rightBus) // wrong way around?
                    {
                        int temp = leftBus;
                        leftBus = rightBus;
                        rightBus = temp;
                    }
                    int col = (leftBus + rightBus) / 2;
                    int row = places.PlaceIt(leftBus, col, rightBus);
                    rows.Add(row);
                }
             }
            return rows;
        }

        private List<Scrambler> createScramblers(string crib, string cipher, List<int> scramblerRow)
        {
            List<Scrambler> scramblers = new List<Scrambler>();
            for (int i = 0; i < scramblerRow.Count; i++)
            {
                if (crib[i] == ' ') continue;  // ignore wildcard don't-cares in crib
                int leftBus = crib[i] - 'A';
                int rightBus = cipher[i] - 'A';
                if (leftBus > rightBus) // wrong way around?
                {
                    // Fortunately scramblers and Enigmas are symmetrical so we can call either side left or right
                    int temp = leftBus;
                    leftBus = rightBus;
                    rightBus = temp;
                }

                Label lbl = new Label()
                {
                    Content = "???",
                    FontFamily = new FontFamily("Consolas"),
                    FontStyle = FontStyles.Normal,
                    FontWeight = FontWeights.Bold,
                    FontSize = 12,
                };

                Canvas cnvs = new Canvas() { Background = new SolidColorBrush(Color.FromArgb(64,64,64,64)), Width = ScramblerSize, Height = ScramblerSize };
                Scrambler sc = new Scrambler(i, scramblerRow[i], leftBus, rightBus, cnvs, lbl);
                scramblerFixedCanvas.Children.Add(cnvs);
                scramblerFixedCanvas.Children.Add(lbl);
                scramblers.Add(sc);
            }
            return scramblers;
        }

        void placeScramblerFixedparts()
        {
            dotCanvas.Children.Clear();
            foreach (Scrambler sc in Scramblers)
            {
                int leftBus = sc.LeftBus;
                int rightBus = sc.RightBus;
                int posCol = (leftBus + rightBus) / 2;
                int row = sc.Row;

                Canvas cnvs = sc.Tag as Canvas;

                double xMid = (xFor(posCol, 7) + xFor(posCol + 1, 0)) / 2; // middle of channel
                double leftEdge = xMid - cnvs.Width / 2; // Left edge of scramber icon
                                                         //   double rightEdge = xMid + cnvs.Width / 2; // Right edge of scramber icon
                double y0 = row * RowHeight + TopBusMargin + 5 * WireChannelWidth;
                // Move the scrambler box
                Canvas.SetLeft(cnvs, leftEdge);
                Canvas.SetTop(cnvs, y0);

                Label caption = sc.Caption as Label;
                // move its label 
                Canvas.SetLeft(caption, leftEdge - 2);
                Canvas.SetTop(caption, y0 - 17);


                double yTop = y0 + WireChannelWidth / 2;
                double xTopL = xFor(leftBus, 0);    // Where to plug into the left bus
                double xTopR = xFor(rightBus, 0);   // where to plug into the right Bus
                for (int i = 0; i < 8; i++)  // Place dots where the wires will eventually join
                {
                    double w0 = xTopL + i * WireChannelWidth;
                    double w1 = xTopR + i * WireChannelWidth;
                    double y = yTop + i * WireChannelWidth;
                    addJoinerDot(w0, y);
                    addJoinerDot(w1, y);
                }
            }
        }

        private void addJoinerDot(double x, double y)
        {
            const double dotSize = WireChannelWidth; // - 2;
            const double halfDotSz = dotSize / 2;

            Ellipse theDot = new Ellipse() { Width = dotSize, Height = dotSize, Stroke = Brushes.Gray };
            Canvas.SetTop(theDot, y - halfDotSz);
            Canvas.SetLeft(theDot, x - halfDotSz);

            dotCanvas.Children.Add(theDot); // Put the dots on the canvas behind the wires
        }

        void addScramberCrossconnects()
        {
            scramblerCrossconnectCanvas.Children.Clear();
            foreach (Scrambler sc in Scramblers)
            {
                // Recreate the cross-connects because the window mapping and scrambling has changed.
                int leftBus = sc.LeftBus;
                int rightBus = sc.RightBus;
                int posCol = (leftBus + rightBus) / 2;
                int row = sc.Row;

                Canvas cnvs = sc.Tag as Canvas;
                double xMid = (xFor(posCol, 7) + xFor(posCol + 1, 0)) / 2; // middle of channel
                double rightEdge = xMid + cnvs.Width / 2; // Right edge of scramber icon
                double leftEdge = xMid - cnvs.Width / 2; // Left edge of scramber icon

                double y0 = row * RowHeight + TopBusMargin + 5 * WireChannelWidth;

                double xTopL = xFor(leftBus, 0);    // Where to plug into the left bus
                double xTopR = xFor(rightBus, 0);   // where to plug into the right Bus
                double yTop = y0 + WireChannelWidth / 2;

                // Find the mapping, remove previous children, and build the cross-connect wiring ... also decorate the scrambler canvas

                string txt = tbWindow.Text.ToUpper().Trim();
                int baseIndex = Scrambler.FromWindowView(txt);
                int stepsAhead = sc.StepOffsetInMenu;
                int myIndex = (baseIndex + stepsAhead) % 512;
                string map = sc[myIndex];

                string myWindowText = $"{Scrambler.ToWindowView(myIndex)} (+{stepsAhead})";
                Label caption2 = sc.Caption as Label;
                caption2.Content = myWindowText;

                cnvs.Children.Clear();
                for (int i = 0; i < 8; i++)
                {
                    PointCollection pts = new PointCollection();
                    double y = yTop + i * WireChannelWidth;
                    double w0 = xTopL + i * WireChannelWidth;

                    pts.Add(new Point(w0, y));
                    pts.Add(new Point(leftEdge, y));

                    double w1 = xTopR + i * WireChannelWidth;

                    int outWireIndx = map[i] - 'A';
                    double y1 = WireChannelWidth / 2 + WireChannelWidth * i;
                    double y2 = WireChannelWidth / 2 + WireChannelWidth * (outWireIndx);

                    double rhs = yTop + WireChannelWidth * (outWireIndx);


                    pts.Add(new Point(rightEdge, rhs));
                    pts.Add(new Point(xTopR + outWireIndx * WireChannelWidth, rhs));

                    Polyline pLine = new Polyline()
                    {
                        Stroke = ColdBrush,
                        Points = pts,
                    };
                    //    pLine.Name=$"SC_at_offset_{sc.StepOffsetInMenu}_LeftBus{leftBus}_{i}__RightBus{rightBus}_{outWireIndx}";  

                    scramblerCrossconnectCanvas.Children.Add(pLine);
                    cManager.AddScramblerCrossConnect(leftBus, i, rightBus, outWireIndx, pLine);
                }
            }
        }

        #endregion

        #region Diagonal Board:  Build the diagonal board wiring, provide switches to selectively disconnect lines, etc.

        // Why the diagonal board works and is justified was a tricky concept for me.  So a lot of 
        // tedious over-engineered effort around this feature to provide an interactive playground. 
        // My "aha" moment came from the Ellsbury article.  Each bus line in the machine represents
        // an hypothesis, e.g. the E.a bus line represents the hypothesis that E is steckered to A.
        // And the machine's purpose is not to find the correct wheel settings.  The machine's purpose
        // is in negative logic: to discard the wheel settings that cannot be true.  
        // If E is steckered to A, it cannot also be steckered to any other letter. So that creates the 
        // contradiction, and the evidence to reject the wheel setting.

        private void makeDiagonalBoard()
        {
            // There are 28 wires to place on the diagonal board. We add a switch to each.  
            // Thanks to Sue for tediously producing this layout table.
            string[] layout = {
                "AB15",
                "AC14",
                "AD12",
                "AE9",
                "AF5",
                "AG2",
                "AH0",
                "BC15",
                "BD13",
                "BE11",
                "BF8",
                "BG4",
                "BH1",
                "CD15",
                "CE14",
                "CF10",
                "CG7",
                "CH3",
                "DE15",
                "DF13",
                "DG12",
                "DH6",
                "EF15",
                "EG14",
                "EH11",
                "FG15",
                "FH13",
                "GH15"
            };
            if (diagonalSwitchClosed == null) // this code done in First time only, after this the table is established and updated
            {
                diagonalSwitchClosed = new Dictionary<string, bool>();
                foreach (string s in layout)
                {
                    string wireName = s.Substring(0,2);
                    diagonalSwitchClosed.Add(wireName, true);
                }
            }

            double dbXMargin = 10;
            double dbYPosn = botY - WireChannelWidth;
            Canvas diagBoard = new Canvas() { Height = 18 * WireChannelWidth, Width = backLayer.Width-2*dbXMargin, Background = Brushes.LightGray};
            Canvas.SetLeft(diagBoard, dbXMargin);
            Canvas.SetTop(diagBoard, dbYPosn);
            Label waterMark = new Label() { Content = "Diagonal Board", FontSize = 32, Foreground = new SolidColorBrush(Color.FromArgb(128,0,0,255)) };
            Canvas.SetTop(waterMark, diagBoard.Height - 30);
            Canvas.SetLeft(waterMark, diagBoard.Width *0.72);
            waterMark.RenderTransform = new RotateTransform(-30);
            diagBoard.Children.Add(waterMark);


            foreach (string s in layout)
            {
                string wireName = s.Substring(0, 2);
                bool isClosed = diagonalSwitchClosed[wireName];
                int srcBus = s[0] - 'A';
                int dstBus = s[1] - 'A';

                double chan = 15 - double.Parse(s.Substring(2));
                double switchPos = xFor(srcBus, 12);

                double x0 = xFor(srcBus, dstBus) -dbXMargin;
                double x4 = xFor(dstBus, srcBus) - dbXMargin;

                //    double y = botY + chan * WireChannelWidth;  // y coordinate of the channel
                double y = (chan + 1.5) * WireChannelWidth;  // y coordinate of the channel



                string toolTip = $"{uppers[srcBus]}.{lowers[dstBus]} {leftRightArrow} {uppers[dstBus]}.{lowers[srcBus]} ";
                Canvas theSwitch = makeSwitch(switchPos, y, isClosed, toolTip, wireName);

                Brush bWire = ColdBrush;   // isClosed ? ColdBrush : Brushes.Gray;


                double leftedge = Canvas.GetLeft(theSwitch);
                double rightEdge = leftedge + theSwitch.Width;
                Line pLeft = new Line() {X1 = x0, X2 = leftedge, Y1 = y, Y2 = y, Stroke = bWire, StrokeThickness = WireThickness };
                // pLeft.Name = Name = $"DBL_{s}";
                Canvas.SetLeft(pLeft, 0);
                Canvas.SetTop(pLeft, 0);
                diagBoard.Children.Add(pLeft);

                Line pRight = new Line() {  X1 = rightEdge, X2 = x4, Y1 = y, Y2 = y, Stroke = bWire, StrokeThickness = WireThickness };
                // pRight.Name = Name = $"DBR_{s}";
                Canvas.SetLeft(pRight, 0);
                Canvas.SetTop(pRight, 0);
                diagBoard.Children.Add(pRight);

                cManager.AddDiagonalBoardWire(srcBus, pLeft, dstBus, pRight, y + dbYPosn, isClosed);
                diagBoard.Children.Add(theSwitch); // Put it down last so it stays foremost.
            }
            persistentCanvas.Children.Insert(0, diagBoard);
         }

        private Canvas makeSwitch(double x, double y, bool isClosed, string myToolTip, string wireName)
        {   double cWidth = 16;
            double cHeight = 6;
            double delta = isClosed ? cHeight : 2.5;
            double y2 = y;
            if (!isClosed)
            {
                y2 += delta;
            }
            Canvas cnvs = new Canvas() { Width = cWidth, Height = cHeight, Background = Brushes.Transparent };
            Line sw = new Line() { X1 = 0, X2 = cWidth, Y1 = cHeight-2, Y2 = delta-2, Stroke = Brushes.Black, StrokeThickness = WireThickness };
            Canvas.SetLeft(sw, 0);
            Canvas.SetTop(sw, 0);
            cnvs.Children.Add(sw);
            double eSz = 5;
            Ellipse eLeft = new Ellipse() { Width = eSz, Height = eSz, Stroke = Brushes.Black, Fill = Brushes.Black };
            Canvas.SetLeft(eLeft, 0-eSz);
            Canvas.SetTop(eLeft, cHeight-eSz/2-2);
            cnvs.Children.Add(eLeft);

            Ellipse eRight = new Ellipse() { Width = eSz, Height = eSz, Stroke = Brushes.Black, Fill = Brushes.Black };
            Canvas.SetLeft(eRight, cWidth);
            Canvas.SetTop(eRight,  cHeight-eSz/2-2);
            cnvs.Children.Add(eRight);

            Canvas.SetLeft(cnvs, x);
            Canvas.SetTop(cnvs, y - cHeight+2);
            cnvs.ToolTip = myToolTip;
            cnvs.Tag = wireName;
            cnvs.MouseUp += DiagonalSwitch_MouseUp;
            cnvs.Cursor = Cursors.Hand;
            return cnvs;
        }

        private void DiagonalSwitch_MouseUp(object sender, MouseButtonEventArgs e)
        {
            Canvas elem = sender as Canvas;
            string wireName = elem.Tag as String;
            diagonalSwitchClosed[wireName] = !diagonalSwitchClosed[wireName];
            RebuildBombe();
        }

        private void btnCloseAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (string wireName in diagonalSwitchClosed.Keys)
            {
                diagonalSwitchClosed[wireName] = true;
            }
            RebuildBombe();
        }

        private void btnToggleAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (string wireName in diagonalSwitchClosed.Keys)
            {
                diagonalSwitchClosed[wireName] = !diagonalSwitchClosed[wireName]; 
            }
            RebuildBombe();
        }

        private void btnOpenAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (string wireName in diagonalSwitchClosed.Keys)
            {
                diagonalSwitchClosed[wireName] = false;
            }
            RebuildBombe();
        }

        private void cbUseDiagonalBoard_Click(object sender, RoutedEventArgs e)
        {
            RebuildBombe();
        }

        #endregion

        #region Buses: Build the bus wiring
        Line[] makeBuses()
        {
            Line[] result = new Line[64];
            int topY = TopBusMargin;
            int next = 0;
            for (int bus = 0; bus < 8; bus++)
            {
                for (int wire = 0; wire < 8; wire++)
                {
                    double x0 = xFor(bus, wire);
                    Line p = new Line() { X1 = x0, X2 = x0, Y1 = topY, Y2 = botY, Stroke = ColdBrush, StrokeThickness = WireThickness, Name=$"bus{bus}{wire}" };

                  //  busWires[bus, wire] = p;
                    result[next++] = p;
                    Canvas.SetLeft(p, 0);
                    Canvas.SetTop(p, 0);
                    persistentCanvas.Children.Add(p);
                }

                // Labels near top of each bus
                Label name = new Label() { Foreground = Brushes.Black, Content = (char)('A' + bus), FontFamily = new FontFamily("Consolas"), FontSize = 20, FontWeight = FontWeights.Bold };
                Canvas.SetLeft(name, xFor(bus, -3));
                Canvas.SetTop(name, topY-28);
                persistentCanvas.Children.Add(name);

                Label minorNames = new Label() { Foreground = Brushes.Black, Content = "abcdefgh", FontFamily = new FontFamily("Consolas"), FontSize =13, FontWeight = FontWeights.Bold };
                Canvas.SetLeft(minorNames, xFor(bus, -1) -2);
                Canvas.SetTop(minorNames, topY - 22);
                persistentCanvas.Children.Add(minorNames);
            }
            return result;
        }

        #endregion

        #region Other GUI handlers to single-step the scramblers, etc.
        private void btnFwd_Click(object sender, RoutedEventArgs e)
        {
            int win = Scrambler.FromWindowView(tbWindow.Text);
            win = (win + 1) % 512;
            tbWindow.Text = Scrambler.ToWindowView(win);
            RebuildBombe();
        }

        private void btnBack_Click(object sender, RoutedEventArgs e)
        {
            int win = Scrambler.FromWindowView(tbWindow.Text);
            win = (512 + win - 1) % 512;
            tbWindow.Text = Scrambler.ToWindowView(win);
            RebuildBombe();
        }

        private void cbReplaceSpaces_Click(object sender, RoutedEventArgs e)
        {
            RecoverMessage();
        }

        private void tbWindow_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                RebuildBombe();
            }
        }

        private void setRandomPuzzle(object sender, RoutedEventArgs e)
        {
            Scrambler sc = Scrambler.MakeRandomlySetupScrambler();
            string phrase = ccText.Crib.Text.TrimEnd().Replace(" ", "G");
            string plainText = Scrambler.MakeRandomMessage(phrase);
            string cipherText = sc.EncryptText(plainText);

            ccText.Cipher.Text = cipherText;

            int indx = (sc.Index - plainText.Length + 512) % 512;
            string hintReText = plainText.Replace("G", " ");
            int pos = plainText.IndexOf(ccText.Crib.Text);
            hintText = $"wheels={Scrambler.ToWindowView(indx)} PlugboardMap={sc.plugboardMap} plainText=\"{hintReText}\" index={pos + 1}";


            tbWindow.Text = "AAA";
            RebuildBombe();
        }


        private void EncryptItem_Click(object sender, RoutedEventArgs e)
        {
            Scrambler sc = Scrambler.MakeRandomlySetupScrambler();
            sc.Index = Scrambler.FromWindowView(tbWindow.Text);
            sc.SetPlugboard(PlugGuesses.Text);
            string plainText = ccText.Crib.Text;
            string cipherText = sc.EncryptText(plainText);
            ccText.Cipher.Text = cipherText;
            RebuildBombe();
        }

        private void btnHint_Click(object sender, RoutedEventArgs e)
        {
            this.Title = hintText;
        }

        private void PlugGuesses_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                RecoverMessage();
            }
        }

        #endregion
    }
}
