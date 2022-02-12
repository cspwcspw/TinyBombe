
using System;
using System.Collections.Generic;
using System.Text;

// Pete, Jan/Feb 2022

namespace TinyBombe
{
    // A scrambler is the core of a 3-wheel Enigma with only 8 letter mappings, used in the TinyBombe.
    // The 512 possible input-output mappings at each machine position AAA-HHH are hardwired into a table.

    public class Scrambler
    {
        const int mapSz = 8 * 8 * 8;
        public int Index { get; set; }  // an integer representing the scrambler's present position
        public int StepOffsetInMenu { get; private set; }

        public string plugboardMap { get; private set; } = "ABCDEFGH";
 

        public int LeftBus { get; private set; }  // Some properties relating to placement on the GUI
        public int RightBus { get; private set; }
        public int Row { get; private set; }
        public object Tag { get; set; }        // Keep a reference to the Canvas when used with a WPF GUI
        public object Caption { get; set; }    // Keep a reference to this scrambler's caption object, when used with a WPF GUI

        public Scrambler(int stepOffset, int row, int left, int right, object tag = null, object caption = null)
        {
            StepOffsetInMenu = stepOffset;
            LeftBus = left;
            RightBus = right;
            Row = row;
            Tag = tag;
            Caption = caption;
        }

        /// <summary>
        ///  Create a new encryptor with random whell settings and plugboard plugs
        /// </summary>
        /// <returns></returns>
        public static Scrambler MakeRandomlySetupScrambler()
        {
            Random rnd = new Random();
            Scrambler result = new Scrambler(0, 0, 0, 0);
            result.Index = rnd.Next(128);
            char[] map = "ABCDEFGH".ToCharArray();
            List<int> indexes = new List<int>() { 0, 1, 2, 3, 4, 5, 6, 7 };
            int numPlugs = rnd.Next(4) + 1;
            for (int i = 0; i < numPlugs; i++)
            {
                int k = rnd.Next(indexes.Count);
                int a = indexes[k];
                indexes.RemoveAt(k);

                int m = rnd.Next(indexes.Count);
                int b = indexes[m];
                indexes.RemoveAt(m);

                // swap a and b in the map
                char tmp = map[a];
                map[a] = map[b];
                map[b] = tmp;
            }
            result.plugboardMap = new string(map);
            return result;
        }

        /// <summary>
        /// Make a random encrrypted message that contains a cribword somewhere near the beginning.
        /// </summary>
        /// <returns></returns>
        public static string MakeRandomMessage(string cribword)
        {
            Random rnd = new Random();
            // EGGHEAD EGGED BEG BEGGED CAGED AGED GAFF
            string[] cribbable = {  };

            string[] shortWord = {
                   "A", "ABE", "BE",  "BED", "BEE",  "ADD", "ADA", "FAB",  "FED", "CAB", "CAD", "DAD", "BAD", "FAD",  "FEE", "EBB"};
            string[] scrabbleWord = {
                  "ACED", "BEAD",  "BEACHBABE", "BEACHHEAD", "BEACHED", "BEDDED", "BABE", "BEHEAD", "DEAD", "DEAF", "DEADHEAD", "DEADHEADED", "EACH", "DEED",  
                  "FACE", "FEED",  "FACED"  };

            int cribPos = rnd.Next(3) + 1;
            StringBuilder sb = new StringBuilder();
            int numWords = 6;
            for (int i = 0; i < numWords; i++)
            {
                if (i == cribPos)
                {
                    sb.Append(cribword);
                }
                else if (i < cribPos)
                {
                    sb.Append(shortWord[rnd.Next(shortWord.Length)]);
                }
                else 
                {
                    sb.Append(scrabbleWord[rnd.Next(scrabbleWord.Length)]);
                }
                if (i < numWords - 1)
                {
                    sb.Append('G');   // we use this as a space 
                }
            }
        
            string plainText = sb.ToString();
            return plainText;
        }

        // The Core in-out wiring map is exposed via indexing
        public string this[int index]
        {
            get
            {
                return theMap[index % mapSz];
            }
        }
        /// <summary>
        /// Returns and empty string if successful, or an error message.
        /// </summary>
        /// <param name="plugs"></param>
        /// <returns></returns>
        public string SetPlugboard(string plugs)
        {
            char[] newMap = "ABCDEFGH".ToCharArray();
            bool[] isUsed = { false, false, false, false, false, false, false, false };
            string withoutSpaces = plugs.Replace(" ", "");
            if (withoutSpaces.Length % 2 != 0)
            {
                return "Plugboard has odd length, invalid, must be pairs of letters";
            }
            for (int i = 1; i < withoutSpaces.Length; i += 2)
            {
                int c1 = withoutSpaces[i-1] - 'A';
                int c2 = withoutSpaces[i] - 'A';
                if (c1 < 0 || c1 >= 8 || c2 < 0 || c2 >= 8)
                {
                    plugboardMap = "ABCDEFGH";
                    return "Plugboard invalid, ignored.  Working with no plugs inserted";
                }
                if (isUsed[c1] || isUsed[c2])
                {
                    plugboardMap = "ABCDEFGH";
                    return $"Plug {withoutSpaces[i-1]}{withoutSpaces[i]} clashes with other plugs. All plugs discarded";
                }

                char temp = newMap[c1];
                newMap[c1] = newMap[c2];
                newMap[c2] = temp;

                isUsed[c1] = true;
                isUsed[c2] = true;
            }

            plugboardMap = new string(newMap);
            return ""; // success
        }

        public string EncryptText(string plainText)
        {
            StringBuilder sb = new StringBuilder();
            foreach (char c in plainText)
            {
                int prePlug = c - 'A';
                int k = plugboardMap[prePlug] - 'A';
                int preCrypt = this[Index][k] - 'A';
                char postPlugCrypted = plugboardMap[preCrypt];
                sb.Append(postPlugCrypted);
                Index = (Index + 1) % 512;  // OK, so this machine steps after the encoding, not before.
            }
            return sb.ToString();
        }

        public static string ToWindowView(int indx)
        {
            char[] buf = new char[3];
            buf[2] = (char)('A' + indx % 8);
            indx = indx / 8;
            buf[1] = (char)('A' + indx % 8);
            indx = indx / 8;
            buf[0] = (char)('A' + indx % 8);
            return new string(buf);
        }

        public static int FromWindowView(string s)
        {
            return (s[0] - 'A') * 8 * 8 + (s[1] - 'A') * 8 + (s[2] - 'A');
        }

        //static Scrambler()   // Once-off code generated the all-time map
        //{
        //    theMap = new string[mapSz];
        //    // Ensure we always get the same shared map for all scramber instances, and all runs.
        //    // We'll have some test data, cribs, and menus that we'll want to use permanently.
        //    Random rnd = new Random(42); 
        //    for (int i = 0; i < mapSz; i++)
        //    {
        //        // A valid mapping at any position of the rotors cannot map any letter to itself,
        //        // and it must honour enigma symmetry, e.g. E->A implies A->E 

        //        // Start with all the pins and pads available for potential cross-wiring
        //        List<int> pinsAvailable = new List<int>() { 0, 1, 2, 3, 4, 5, 6, 7 };
        //        List<int> padsAvailable = new List<int>() { 0, 1, 2, 3, 4, 5, 6, 7 };
        //        // These spaces tell us this pin position is unsoldered right now.
        //        char[] rowWiring = new char[8] { ' ', ' ', ' ', ' ', ' ', ' ', ' ', ' ' };
        //        while (padsAvailable.Count > 0)
        //        {
        //            int pad = padsAvailable[0];  // always pick the next pad
        //            int pin = pad;
        //            do
        //            {
        //                pin = pinsAvailable[rnd.Next(pinsAvailable.Count)];  // Choose a random available pin
        //            }
        //            while (pin == pad); // but don'e allow self-mapping || rowWiring[pin] != ' ');
        //            pinsAvailable.Remove(pin); // so they cannot be used again
        //            pinsAvailable.Remove(pad);
        //            padsAvailable.Remove(pin);
        //            padsAvailable.Remove(pad);
        //            // Now map pad->pin and pin->pad
        //            rowWiring[pad] = (char)('A' + pin);
        //            rowWiring[pin] = (char)('A' + pad);
        //        }
        //        theMap[i] = new string(rowWiring);
        //    }
        //}

        static private string[] theMap = {
"FEDCBAHG", "ECBHAGFD", "CEAFBDHG", "GEDCBHAF", "GEFHBCAD", "FEDCBAHG", "GEHFBDAC", "DGFAHCBE",
"CFAGHBDE", "DGFAHCBE", "DGEACHBF", "DGFAHCBE", "HDEBCGFA", "CEAHBGFD", "DCBAHGFE", "FHGEDACB",
"BAEFCDHG", "BADCGHEF", "HFEGCBDA", "CFAHGBED", "CHAEDGFB", "DHEACGFB", "GHFEDCAB", "GCBFHDAE",
"FCBEDAHG", "HFGEDBCA", "EGFHACBD", "DEHABGFC", "HFGEDBCA", "DFEACBHG", "HGDCFEBA", "EHGFADCB",
"CEAGBHDF", "BAHFGDEC", "FDHBGAEC", "GHFEDCAB", "HEFGBCDA", "FGDCHABE", "FCBGHADE", "ECBHAGFD",
"DEFABCHG", "FHDCGAEB", "CDABHGFE", "EDHBAGFC", "CHAGFEDB", "CFAHGBED", "FHDCGAEB", "GDFBHCAE",
"EDFBACHG", "FEDCBAHG", "BAHEDGFC", "CFAEDBHG", "HCBFGDEA", "FHDCGAEB", "DEHABGFC", "EGDCAHBF",
"HFDCGBEA", "GFDCHBAE", "GDHBFEAC", "GDEBCHAF", "DHEACGFB", "DCBAHGFE", "HEGFBDCA", "FEHGBADC",
"CHAFGDEB", "EDFBACHG", "BAHGFEDC", "DFHAGBEC", "FEHGBADC", "FHEGCADB", "CEAHBGFD", "GHFEDCAB",
"GHFEDCAB", "HFGEDBCA", "BADCHGFE", "HGFEDCBA", "DHGAFECB", "HEFGBCDA", "BAEFCDHG", "HFDCGBEA",
"DFGAHBCE", "HGFEDCBA", "GFDCHBAE", "GHDCFEAB", "BAFGHCDE", "DGEACHBF", "CEAGBHDF", "GDHBFEAC",
"CDABFEHG", "BAHEDGFC", "EDHBAGFC", "DCBAGHEF", "CGAEDHBF", "DHFAGCEB", "CFAEDBHG", "CHAEDGFB",
"BAFGHCDE", "GEHFBDAC", "HCBEDGFA", "FDGBHACE", "HEDCBGFA", "HCBFGDEA", "GDHBFEAC", "ECBHAGFD",
"GHFEDCAB", "DHEACGFB", "FDGBHACE", "CEAFBDHG", "HCBFGDEA", "GCBEDHAF", "CGAHFEBD", "GEFHBCAD",
"ECBGAHDF", "BAEFCDHG", "FEHGBADC", "FCBGHADE", "FHEGCADB", "BAFHGCED", "HFDCGBEA", "DHGAFECB",
"HGEFCDBA", "BAEFCDHG", "GHEFCDAB", "GFHEDBAC", "DEGABHCF", "CGAFHDBE", "HEFGBCDA", "HCBEDGFA",
"DGFAHCBE", "CEAFBDHG", "EHGFADCB", "GFEHCBAD", "BAHGFEDC", "CHAEDGFB", "BADCGHEF", "FEGHBACD",
"CEAHBGFD", "FCBHGAED", "EFHGABDC", "EDGBAHCF", "CDABFEHG", "CEAFBDHG", "GEDCBHAF", "CDABFEHG",
"BAHGFEDC", "FCBEDAHG", "BAEHCGFD", "EFHGABDC", "FGEHCABD", "BAFEDCHG", "HEDCBGFA", "FHGEDACB",
"BAGHFECD", "DGFAHCBE", "BAHEDGFC", "DHGAFECB", "BAFGHCDE", "DFEACBHG", "GCBEDHAF", "HCBEDGFA",
"CFAHGBED", "FEHGBADC", "HCBFGDEA", "GDEBCHAF", "HDFBGCEA", "EHDCAGFB", "FDEBCAHG", "HDEBCGFA",
"BAHGFEDC", "CFAHGBED", "FGHEDABC", "GHEFCDAB", "GDEBCHAF", "HEFGBCDA", "GHFEDCAB", "FDHBGAEC",
"HCBEDGFA", "HEDCBGFA", "BAGHFECD", "BAGHFECD", "EDFBACHG", "ECBGAHDF", "GFDCHBAE", "FHGEDACB",
"GEFHBCAD", "HDFBGCEA", "BAGEDHCF", "BAEHCGFD", "FEHGBADC", "GEDCBHAF", "CFAGHBDE", "HFGEDBCA",
"EGDCAHBF", "DHGAFECB", "ECBFADHG", "HDGBFECA", "CHAGFEDB", "EFHGABDC", "EFHGABDC", "HCBEDGFA",
"BAHGFEDC", "DCBAGHEF", "BAHGFEDC", "GCBHFEAD", "CDABHGFE", "DEHABGFC", "CEAGBHDF", "CEAHBGFD",
"EDFBACHG", "GCBEDHAF", "BAEHCGFD", "FCBGHADE", "EDHBAGFC", "DHEACGFB", "CHAGFEDB", "HFEGCBDA",
"DCBAHGFE", "HCBEDGFA", "GFEHCBAD", "DCBAFEHG", "DGHAFEBC", "DHFAGCEB", "EGHFADBC", "CFAEDBHG",
"BAEHCGFD", "GEFHBCAD", "BAEHCGFD", "DFEACBHG", "FGHEDABC", "BAGFHDCE", "BAFHGCED", "HGEFCDBA",
"GEFHBCAD", "GFHEDBAC", "HEGFBDCA", "BAEFCDHG", "CGAEDHBF", "CFAEDBHG", "FGEHCABD", "HDGBFECA",
"BADCGHEF", "EDHBAGFC", "CGAFHDBE", "BAFHGCED", "BAEFCDHG", "CHAFGDEB", "EFGHABCD", "GCBHFEAD",
"CHAGFEDB", "CDABFEHG", "BADCGHEF", "GDFBHCAE", "CEAGBHDF", "CEAFBDHG", "BADCGHEF", "DHEACGFB",
"DGEACHBF", "BAHEDGFC", "FCBHGAED", "BAHFGDEC", "BAGEDHCF", "EHFGACDB", "BADCFEHG", "BAHFGDEC",
"HCBGFEDA", "HCBEDGFA", "FCBHGAED", "GCBFHDAE", "ECBFADHG", "FGDCHABE", "EGDCAHBF", "CDABFEHG",
"CDABFEHG", "DCBAHGFE", "GCBFHDAE", "BAGFHDCE", "GEFHBCAD", "EHDCAGFB", "GCBHFEAD", "GFHEDBAC",
"GEHFBDAC", "DFGAHBCE", "FGHEDABC", "BADCGHEF", "HGFEDCBA", "HCBGFEDA", "EGFHACBD", "FGHEDABC",
"ECBHAGFD", "EHFGACDB", "FGDCHABE", "BADCGHEF", "DHEACGFB", "HDFBGCEA", "EFGHABCD", "FEDCBAHG",
"GDFBHCAE", "CHAFGDEB", "EGFHACBD", "GFDCHBAE", "EFDCABHG", "GFHEDBAC", "BAGFHDCE", "GFDCHBAE",
"GFEHCBAD", "GDEBCHAF", "HDEBCGFA", "DFGAHBCE", "GDFBHCAE", "HFEGCBDA", "EDFBACHG", "CGAHFEBD",
"DCBAHGFE", "BAFEDCHG", "BAEGCHDF", "EFGHABCD", "CEAFBDHG", "GHEFCDAB", "DGFAHCBE", "DCBAGHEF",
"EFHGABDC", "DFHAGBEC", "BAGEDHCF", "DFHAGBEC", "FEHGBADC", "DHFAGCEB", "GFEHCBAD", "HDGBFECA",
"HCBGFEDA", "ECBGAHDF", "HGDCFEBA", "EHGFADCB", "ECBGAHDF", "ECBGAHDF", "FCBHGAED", "HGDCFEBA",
"HCBGFEDA", "FEDCBAHG", "FGHEDABC", "CFAEDBHG", "DGHAFEBC", "GEHFBDAC", "FDEBCAHG", "CFAHGBED",
"CGAFHDBE", "BADCHGFE", "HFGEDBCA", "GHEFCDAB", "EGDCAHBF", "EGHFADBC", "BAHEDGFC", "CHAEDGFB",
"ECBGAHDF", "DFHAGBEC", "EGHFADBC", "FGHEDABC", "GHFEDCAB", "GHDCFEAB", "BAEHCGFD", "HEDCBGFA",
"DHEACGFB", "GCBFHDAE", "FDHBGAEC", "BAHFGDEC", "GDFBHCAE", "HFGEDBCA", "BADCGHEF", "FDHBGAEC",
"FEGHBACD", "HFEGCBDA", "GDFBHCAE", "BAEFCDHG", "DCBAFEHG", "FEDCBAHG", "BAFHGCED", "HFEGCBDA",
"DEFABCHG", "EGDCAHBF", "GDHBFEAC", "GHEFCDAB", "BAFGHCDE", "FGHEDABC", "GHFEDCAB", "BADCGHEF",
"HEDCBGFA", "HFGEDBCA", "CEAGBHDF", "CEAHBGFD", "BAGEDHCF", "HCBGFEDA", "FDGBHACE", "FEHGBADC",
"EDGBAHCF", "EHFGACDB", "DFEACBHG", "FEDCBAHG", "HEDCBGFA", "FGEHCABD", "BAGHFECD", "BAGFHDCE",
"HDGBFECA", "FCBHGAED", "DHFAGCEB", "BAEHCGFD", "EFGHABCD", "HCBEDGFA", "HDEBCGFA", "FHDCGAEB",
"DEGABHCF", "EFDCABHG", "CGAFHDBE", "BAGEDHCF", "HCBGFEDA", "CDABHGFE", "BAGHFECD", "HCBFGDEA",
"DFHAGBEC", "HDFBGCEA", "GFHEDBAC", "BADCGHEF", "DHFAGCEB", "EGFHACBD", "CGAFHDBE", "HEGFBDCA",
"FEDCBAHG", "GHDCFEAB", "DFEACBHG", "CGAEDHBF", "CFAGHBDE", "HFDCGBEA", "GHDCFEAB", "HGFEDCBA",
"HDFBGCEA", "FDEBCAHG", "EFGHABCD", "BAGEDHCF", "CFAEDBHG", "DEFABCHG", "BADCGHEF", "EGFHACBD",
"GFDCHBAE", "BAFHGCED", "BAFHGCED", "CEAFBDHG", "FGDCHABE", "BADCFEHG", "BAHGFEDC", "EGHFADBC",
"BADCGHEF", "BADCGHEF", "GDFBHCAE", "HGDCFEBA", "GEDCBHAF", "ECBGAHDF", "FCBEDAHG", "HDFBGCEA",
"EFHGABDC", "BAFGHCDE", "DHGAFECB", "EDGBAHCF", "FDGBHACE", "BAEHCGFD", "BAHFGDEC", "FHGEDACB",
"GDHBFEAC", "GHDCFEAB", "GDEBCHAF", "GFHEDBAC", "FHGEDACB", "BADCFEHG", "FCBGHADE", "FEDCBAHG",
"EFHGABDC", "DEFABCHG", "HFEGCBDA", "EHGFADCB", "GDHBFEAC", "CGAEDHBF", "DGHAFEBC", "EDHBAGFC",
"BAFHGCED", "EDHBAGFC", "ECBGAHDF", "EFGHABCD", "BAFHGCED", "GCBEDHAF", "HFEGCBDA", "CEAFBDHG",
"HEFGBCDA", "CFAHGBED", "BAEHCGFD", "CEAHBGFD", "CFAEDBHG", "EFDCABHG", "FGEHCABD", "FEHGBADC",
"BAGFHDCE", "CFAEDBHG", "EFGHABCD", "DHGAFECB", "CGAFHDBE", "DFEACBHG", "EDGBAHCF", "BADCGHEF",
"GCBFHDAE", "FGDCHABE", "DFGAHBCE", "HFGEDBCA", "GDFBHCAE", "CGAHFEBD", "HGEFCDBA", "HEFGBCDA",
            };


    }

}
