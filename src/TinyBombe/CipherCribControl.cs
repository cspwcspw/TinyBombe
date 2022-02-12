using System;
using System.Collections.Generic;

using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace TinyBombe
{
    public class CipherCribControl : Canvas
    {

      //  public event EventHandler EnterKeyPressed;
        public Action<bool> TextChanged;
        public Action EnterKeyPressed;


        const string fontName = "Consolas";
        const double emSize = 18;
        const double fontPitch = 9.8966666;

        public bool ValidCrib { get; private set; } = true;

   
        int NumChars;


        public TextBox Cipher { get; set; }
        public TextBox Crib { get; set; }

        Polygon Whoops;

        public CipherCribControl(int length)
        {
            NumChars = length;
            addGridlines();
            Cipher = makeTextBox(NumChars);
            SetLeft(Cipher, 0);
            SetTop(Cipher, 0);
            Cipher.Padding = new Thickness(0, 0, 0, 0);
            Cipher.Text = "ABCDEFGH";
            this.Children.Add(Cipher);

            Crib = makeTextBox(NumChars);
            Crib.Text = "BCDEFGHA";
            SetLeft(Crib, 0);
            SetTop(Crib, 20);
            Crib.Padding = new Thickness(0, 0, 0, 0);

            this.Children.Add(Crib);

         //   Background = Brushes.LightYellow;
            Width = NumChars * fontPitch;
            Height = 40;
            VerticalAlignment = VerticalAlignment.Top;
            HorizontalAlignment = HorizontalAlignment.Left;

            Crib.PreviewKeyDown += Crib_PreviewKeyDown;
            Crib.TextChanged += Either_TextChanged;
            Cipher.TextChanged += Either_TextChanged;

            Crib.KeyDown += Either_KeyDown;
            Cipher.KeyDown += Either_KeyDown;


            PointCollection pts = new PointCollection() { new Point(0, 0), new Point(4, -4), new Point(8, 0), new Point(4, 4), new Point(0, 0) };
            Whoops = new Polygon() { Stroke = Brushes.Red, Fill = Brushes.Red, Points = pts };
            SetTop(Whoops, 21.5);
            SetLeft(Whoops, 7.5 * fontPitch);
            Children.Add(Whoops);
        }

        private void Crib_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            
        }

        private void Either_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                EnterKeyPressed?.Invoke();
                e.Handled = true;
            }
        }

        private void Either_TextChanged(object sender, TextChangedEventArgs e)
        {
            TextBox tb = (TextBox)sender;
            //  tb.DeclareChangeBlock();
            // https://stackoverflow.com/questions/18971198/can-you-replace-characters-in-a-textbox-as-you-type
            //foreach (TextChange tc in e.Changes)
            //{
            //    if (tc.AddedLength == 0)
            //    {
            //        continue;
            //    }
            //    int offset = tc.Offset;
            //    char c = tb.Text[offset];
            //    if (c != '?')
            //    {
            //        if (c < 'A' || c > 'H')
            //        {
            //            tb.Select(offset, 1);
            //            tb.SelectedText = tb.SelectedText.Replace(c, '?');
            //            // tb.Text.Replace(( = 'Q';
            //        }
            //    }
            //}

            string cip = Cipher.Text.ToUpper();
            string crb = Crib.Text.ToUpper();
            int n = Math.Min(cip.Length, crb.Length);
            if (n == 0)
            {
                Whoops.Visibility = Visibility.Visible;
                SetLeft(Whoops, 0);
                ValidCrib = false;
                TextChanged?.Invoke(false);
                return;
            }

            for (int i = 0; i < n; i++)
            {
                if (cip[i] == crb[i])
                {
                    Whoops.Visibility = Visibility.Visible;
                    SetLeft(Whoops, (i + 0.5) * fontPitch);
                    ValidCrib = false;
                    TextChanged?.Invoke(false);

                    return;
                }
            }
            Whoops.Visibility = Visibility.Hidden;
            ValidCrib = true;
            TextChanged?.Invoke(true);
        }

        private void addGridlines()
        {
            for (int i = 0; i < NumChars; i++)
            {
                double x = i * fontPitch + 4;
                Line ln = new Line() { X1 = x, X2 = x, Y1 = 21, Y2 = 24, Stroke = Brushes.Black };
                Children.Add(ln);
            }
        }

        private TextBox makeTextBox(int numChars)
        {
            TextBox result = new TextBox()
            {
                FontFamily = new FontFamily(fontName),
                FontSize = emSize,
                FontWeight = FontWeights.Bold,
                Width = NumChars * fontPitch,
                Background = Brushes.Transparent,
                BorderBrush = Brushes.Transparent,
                CharacterCasing =  CharacterCasing.Upper,
            };
            return result;
        }

        internal void ReplaceCribText(string normalizedText)
        {
            // Replace text in the textbox while preserving the caret.
            int caret = Crib.CaretIndex;
            Crib.Text = normalizedText;
            Crib.CaretIndex = caret;
        }


        //double GetFontWidth() // Don't even ask where I got this obsolete stuff from, I used it to compute the fontPitch
        //{
        //    FormattedText formattedText = new FormattedText("ABCDEFGHIJ", System.Globalization.CultureInfo.GetCultureInfo("en-us"),
        //                 FlowDirection.LeftToRight, new Typeface(fontName), emSize, Brushes.Black);
        //    return formattedText.WidthIncludingTrailingWhitespace / 10;
        //}
    }
}
