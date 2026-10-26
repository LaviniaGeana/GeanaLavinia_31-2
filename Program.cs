using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace ExtragereCuvinte
{
    class Program
    {
        static void Main(string[] args)
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;

            // 1. Căutare fișiere 
            string[] files = Directory.GetFiles(baseDir, "*NEWS.XML", SearchOption.AllDirectories);

            if (files.Length == 0)
            {
                string projectDir = Path.GetFullPath(Path.Combine(baseDir, @"..\..\..\"));
                if (Directory.Exists(projectDir))
                {
                    files = Directory.GetFiles(projectDir, "*NEWS.XML", SearchOption.AllDirectories);
                }
            }

            if (files.Length == 0)
            {
                Console.WriteLine("[EROARE] Nu s-au găsit fișiere *NEWS.XML!");
                Console.ReadLine();
                return;
            }

            PorterStemmer stemmer = new PorterStemmer();
            Dictionary<string, List<string>> documentWords = new Dictionary<string, List<string>>();

            // parsare 
            foreach (string inputFilePath in files)
            {
                string filename = Path.GetFileName(inputFilePath);
                string rawContent = File.ReadAllText(inputFilePath, Encoding.UTF8);
                List<string> extractedWords = new List<string>();

                Match titleMatch = Regex.Match(rawContent, @"<title[^>]*>(.*?)</title>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                if (titleMatch.Success)
                    extractedWords.AddRange(ExtractWords(titleMatch.Groups[1].Value));

                Match textMatch = Regex.Match(rawContent, @"<text[^>]*>(.*?)</text>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                if (textMatch.Success)
                    extractedWords.AddRange(ExtractWords(textMatch.Groups[1].Value));

                Match metadataMatch = Regex.Match(rawContent, @"<metadata[^>]*>(.*?)</metadata>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                if (metadataMatch.Success)
                {
                    string metadataContent = metadataMatch.Groups[1].Value;
                    MatchCollection codesMatches = Regex.Matches(metadataContent, @"<codes[^>]+class=[""'].*?[""'][^>]*>(.*?)</codes>", RegexOptions.Singleline | RegexOptions.IgnoreCase);

                    foreach (Match codeBlock in codesMatches)
                    {
                        MatchCollection codeAttrMatches = Regex.Matches(codeBlock.Value, @"code=[""'](.*?)[""']", RegexOptions.IgnoreCase);
                        foreach (Match attr in codeAttrMatches)
                        {
                            string val = attr.Groups[1].Value;
                            if (!string.IsNullOrWhiteSpace(val))
                                extractedWords.AddRange(ExtractWords(val));
                        }
                    }
                }

                if (extractedWords.Count == 0)
                    extractedWords = ExtractWords(rawContent);

                List<string> stemmedWords = new List<string>();
                foreach (string word in extractedWords)
                {
                    string stemmed = stemmer.StemWord(word);
                    if (!string.IsNullOrWhiteSpace(stemmed))
                        stemmedWords.Add(stemmed);
                }

                documentWords[filename] = stemmedWords;
                Console.WriteLine($" parsat: {filename} ({stemmedWords.Count} cuvinte)");
            }

            // construire dictionar
            HashSet<string> uniqueTerms = new HashSet<string>();
            foreach (var docList in documentWords.Values)
            {
                foreach (var word in docList)
                {
                    uniqueTerms.Add(word);
                }
            }

            List<string> globalDictionary = uniqueTerms.OrderBy(t => t).ToList();

            Dictionary<string, int> termToId = new Dictionary<string, int>();
            for (int i = 0; i < globalDictionary.Count; i++)
            {
                termToId[globalDictionary[i]] = i;
            }

            // 3. CONSTRUIRE CONȚINUT FIȘIER UNIC
            List<string> finalOutputLines = new List<string>();

            // Antet Dicționar Global
            finalOutputLines.Add("Dictionar global:");
            for (int i = 0; i < globalDictionary.Count; i++)
            {
                finalOutputLines.Add($"{i}: {globalDictionary[i]}");
            }

            finalOutputLines.Add(""); // Linie goală de separare
            finalOutputLines.Add("Vectori rari:");

            // Antet Vectori Rari
            int vectorIndex = 1;
            foreach (var kvp in documentWords)
            {
                string filename = kvp.Key;
                List<string> words = kvp.Value;

                SortedDictionary<int, int> sparseVector = new SortedDictionary<int, int>();

                foreach (var word in words)
                {
                    int termId = termToId[word];
                    if (sparseVector.ContainsKey(termId))
                        sparseVector[termId]++;
                    else
                        sparseVector[termId] = 1;
                }

                string vectorStr = string.Join(" ", sparseVector.Select(pair => $"{pair.Key}:{pair.Value}"));
                finalOutputLines.Add($"v{vectorIndex} ({filename}): {vectorStr}");

                finalOutputLines.Add("");
                vectorIndex++;
             

            }

            //salvare
            string fileInBin = Path.Combine(baseDir, "rezultat_final.txt");
            File.WriteAllLines(fileInBin, finalOutputLines);

            string projectDirectory = Path.GetFullPath(Path.Combine(baseDir, @"..\..\..\"));
            if (Directory.Exists(projectDirectory))
            {
                string fileInProject = Path.Combine(projectDirectory, "rezultat_final.txt");
                File.WriteAllLines(fileInProject, finalOutputLines);
            }

            Console.WriteLine($"\n  salvat în fișier: 'rezultat_final.txt'");
            
            Console.ReadLine();
        }

        static List<string> ExtractWords(string input)
        {
            string cleanText = Regex.Replace(input, "<.*?>", " ");
            MatchCollection matches = Regex.Matches(cleanText, @"[a-zA-Z]+");

            List<string> words = new List<string>();
            foreach (Match match in matches)
            {
                words.Add(match.Value.ToLower());
            }
            return words;
        }
    }

    class PorterStemmer
    {
        private char[] wordArray;
        private int endIndex;
        private int stemIndex;

        public string StemWord(string word)
        {
            if (string.IsNullOrWhiteSpace(word) || word.Length <= 2) return word;

            wordArray = word.ToCharArray();
            stemIndex = 0;
            endIndex = word.Length - 1;

            Step1();
            Step2();
            Step3();
            Step4();
            Step5();
            Step6();

            return new String(wordArray, 0, endIndex + 1);
        }

        private void Step1()
        {
            if (wordArray[endIndex] == 's')
            {
                if (EndsWith("sses")) endIndex -= 2;
                else if (EndsWith("ies")) SetEnd("i");
                else if (wordArray[endIndex - 1] != 's') endIndex--;
            }
            if (EndsWith("eed"))
            {
                if (MeasureConsontantSequence() > 0) endIndex--;
            }
            else if ((EndsWith("ed") || EndsWith("ing")) && VowelInStem())
            {
                endIndex = stemIndex;
                if (EndsWith("at")) SetEnd("ate");
                else if (EndsWith("bl")) SetEnd("ble");
                else if (EndsWith("iz")) SetEnd("ize");
                else if (IsDoubleConsontant(endIndex))
                {
                    endIndex--;
                    int ch = wordArray[endIndex];
                    if (ch == 'l' || ch == 's' || ch == 'z') endIndex++;
                }
                else if (MeasureConsontantSequence() == 1 && IsCVC(endIndex)) SetEnd("e");
            }
        }

        private void Step2()
        {
            if (EndsWith("y") && VowelInStem()) wordArray[endIndex] = 'i';
        }

        private void Step3()
        {
            if (endIndex == 0) return;
            switch (wordArray[endIndex - 1])
            {
                case 'a':
                    if (EndsWith("ational")) ReplaceEnd("ate");
                    else if (EndsWith("tional")) ReplaceEnd("tion");
                    break;
                case 'c':
                    if (EndsWith("enci")) ReplaceEnd("ence");
                    else if (EndsWith("anci")) ReplaceEnd("ance");
                    break;
                case 'e':
                    if (EndsWith("izer")) ReplaceEnd("ize");
                    break;
                case 'l':
                    if (EndsWith("bli")) ReplaceEnd("ble");
                    else if (EndsWith("alli")) ReplaceEnd("al");
                    else if (EndsWith("entli")) ReplaceEnd("ent");
                    else if (EndsWith("eli")) ReplaceEnd("e");
                    else if (EndsWith("ousli")) ReplaceEnd("ous");
                    break;
                case 'o':
                    if (EndsWith("ization")) ReplaceEnd("ize");
                    else if (EndsWith("ation")) ReplaceEnd("ate");
                    else if (EndsWith("ator")) ReplaceEnd("ate");
                    break;
                case 's':
                    if (EndsWith("alism")) ReplaceEnd("al");
                    else if (EndsWith("iveness")) ReplaceEnd("ive");
                    else if (EndsWith("fulness")) ReplaceEnd("ful");
                    else if (EndsWith("ousness")) ReplaceEnd("ous");
                    break;
                case 't':
                    if (EndsWith("aliti")) ReplaceEnd("al");
                    else if (EndsWith("iviti")) ReplaceEnd("ive");
                    else if (EndsWith("biliti")) ReplaceEnd("ble");
                    break;
                case 'g':
                    if (EndsWith("logi")) ReplaceEnd("log");
                    break;
            }
        }

        private void Step4()
        {
            switch (wordArray[endIndex])
            {
                case 'e':
                    if (EndsWith("icate")) ReplaceEnd("ic");
                    else if (EndsWith("ative")) ReplaceEnd("");
                    else if (EndsWith("alize")) ReplaceEnd("al");
                    break;
                case 'i':
                    if (EndsWith("iciti")) ReplaceEnd("ic");
                    break;
                case 'l':
                    if (EndsWith("ical")) ReplaceEnd("ic");
                    else if (EndsWith("ful")) ReplaceEnd("");
                    break;
                case 's':
                    if (EndsWith("ness")) ReplaceEnd("");
                    break;
            }
        }

        private void Step5()
        {
            if (endIndex == 0) return;
            switch (wordArray[endIndex - 1])
            {
                case 'a': if (EndsWith("al")) break; return;
                case 'c': if (EndsWith("ance") || EndsWith("ence")) break; return;
                case 'e': if (EndsWith("er")) break; return;
                case 'i': if (EndsWith("ic")) break; return;
                case 'l': if (EndsWith("able") || EndsWith("ible")) break; return;
                case 'n': if (EndsWith("ant") || EndsWith("ement") || EndsWith("ment") || EndsWith("ent")) break; return;
                case 'o': if (EndsWith("ion") && stemIndex >= 0 && (wordArray[stemIndex] == 's' || wordArray[stemIndex] == 't')) break; if (EndsWith("ou")) break; return;
                case 's': if (EndsWith("ism")) break; return;
                case 't': if (EndsWith("ate") || EndsWith("iti")) break; return;
                case 'u': if (EndsWith("ous")) break; return;
                case 'v': if (EndsWith("ive")) break; return;
                case 'z': if (EndsWith("ize")) break; return;
                default: return;
            }
            if (MeasureConsontantSequence() > 1) endIndex = stemIndex;
        }

        private void Step6()
        {
            stemIndex = endIndex;
            if (wordArray[endIndex] == 'e')
            {
                var a = MeasureConsontantSequence();
                if (a > 1 || (a == 1 && !IsCVC(endIndex - 1))) endIndex--;
            }
            if (wordArray[endIndex] == 'l' && IsDoubleConsontant(endIndex) && MeasureConsontantSequence() > 1) endIndex--;
        }

        private bool IsConsonant(int index)
        {
            var c = wordArray[index];
            if (c == 'a' || c == 'e' || c == 'i' || c == 'o' || c == 'u') return false;
            return c != 'y' || (index == 0 || !IsConsonant(index - 1));
        }

        private int MeasureConsontantSequence()
        {
            var n = 0;
            var index = 0;
            while (true)
            {
                if (index > stemIndex) return n;
                if (!IsConsonant(index)) break;
                index++;
            }
            index++;
            while (true)
            {
                while (true)
                {
                    if (index > stemIndex) return n;
                    if (IsConsonant(index)) break;
                    index++;
                }
                index++;
                n++;
                while (true)
                {
                    if (index > stemIndex) return n;
                    if (!IsConsonant(index)) break;
                    index++;
                }
                index++;
            }
        }

        private bool VowelInStem()
        {
            for (int i = 0; i <= stemIndex; i++) if (!IsConsonant(i)) return true;
            return false;
        }

        private bool IsDoubleConsontant(int index)
        {
            if (index < 1) return false;
            return wordArray[index] == wordArray[index - 1] && IsConsonant(index);
        }

        private bool IsCVC(int index)
        {
            if (index < 2 || !IsConsonant(index) || IsConsonant(index - 1) || !IsConsonant(index - 2)) return false;
            var c = wordArray[index];
            return c != 'w' && c != 'x' && c != 'y';
        }

        private bool EndsWith(string s)
        {
            var length = s.Length;
            var index = endIndex - length + 1;
            if (index < 0) return false;
            for (var i = 0; i < length; i++) if (wordArray[index + i] != s[i]) return false;
            stemIndex = endIndex - length;
            return true;
        }

        private void SetEnd(string s)
        {
            var length = s.Length;
            var index = stemIndex + 1;
            for (var i = 0; i < length; i++) wordArray[index + i] = s[i];
            endIndex = stemIndex + length;
        }

        private void ReplaceEnd(string s)
        {
            if (MeasureConsontantSequence() > 0) SetEnd(s);
        }
    }
}