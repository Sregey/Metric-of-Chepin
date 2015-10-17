using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace MetricChepina
{
    static class Program
    {
        [Flags]
        enum VaribleKind : byte
        {
            Неиспользуемая = 0,
            Вводимая = 1,
            Модифицируемая = 2,
            Управляющая = 4,
            Испоьзуемая = 8
        }

        struct Vareble
        {
            public string name;
            public VaribleKind kind;
            public Vareble(string name, VaribleKind kind)
            {
                this.name = name;
                this.kind = kind;
            }
        }

        static StreamReader programCode;    //поток файла с исходным кодом программы на Pascal
        static Vareble[] GlobalVaribles = new Vareble[0], LocalVaribles = new Vareble[0];        
        static string[] DescriptionWords = { "const", "begin", "function", "procedure", "type", "uses", "label", "var" };
        static string[] WordsOfEndOperator = { "do", "then", "else", "end" };
        static int beginEndCounter = 0;   //если был найден begin, то +1; если end, то -1
        static int totalP = 0, totalM = 0, totalC = 0, totalT = 0;
        static double ChepinValue = 0;

        static bool IsLetterOfIdent(char c)
        /*возвращает true если символ является идентификатора*/
        {
            return (('a' <= c && c <= 'z') || ('A' <= c && c <= 'Z') || ('0' <= c && c <= '9') || (c == '_'));
        }

        static void DeleteLineComment(ref string str)
        /*удаляет строчные комментарии из строки*/
        {
            int startIndex = str.IndexOf("//");
            if (startIndex != -1)
                str = str.Remove(startIndex); 
        }

        static void DeleteMultiLineComment(ref string str)
        /*вырезает многострочные комментарии*/
        {
            int startIndex;
            string commentType = "";
            if ((startIndex = str.IndexOf("{")) != -1)
                commentType = "}";
            else if ((startIndex = str.IndexOf("(*")) != -1)
                commentType = "*)";

            if (startIndex != -1)
            {
                string tempStr = str;
                str = str.Remove(startIndex) + " ";
                while ((startIndex = tempStr.IndexOf(commentType)) == -1)
                {
                    tempStr = programCode.ReadLine();
                    DeleteLineComment(ref tempStr);
                }
                str = str + tempStr.Remove(0, startIndex+1);
            } 
        }

        static void DeleteTextFromString(ref string str)
        {
            int startInd = str.IndexOf("'", 0), endInd;
            while (startInd != -1) 
            {
            endInd = str.IndexOf("'", startInd + 1);
            str = str.Remove(startInd, endInd - startInd + 1);
            startInd = str.IndexOf("'", startInd);
            } 
        }

        static string GetString()
        /*возращает следующую строку кода на Pascal*/
        {
            string result = programCode.ReadLine();
            if (result != null)
            {
                DeleteTextFromString(ref result);
                DeleteLineComment(ref result);
                DeleteMultiLineComment(ref result);
                result = result + ' ';
            }
            return result;
        }

        static int IncPos(ref string str, ref int pos)
        /*увеличивает позицию строки на 1, если если позиция вышла за пределы строки,
          то берётся новая строкаи позиция становится равной нулю*/
        {
            if (++pos >= str.Length)
            {
                str = GetString();
                pos = 0;
            }
            return pos;
        }

        static string FindNextIdentifier(ref string str, ref int pos, bool beginPos)
        /*ищет идентификатор в коде начиная в строке str с позиции pos,
         если не находит, то возврщает null. Если beginPos==true, то pos будет указывать на начало
         идентификатора, иначе на конец*/
        {
            while (!IsLetterOfIdent(str[pos]))
            {
                IncPos(ref str, ref pos);
                if (str == null)    //если текст кода кончился
                    return null;
            }

            int tempPos = pos;
            string ident = "";
            while (IsLetterOfIdent(str[pos]))
            {
                ident = ident + str[pos];
                IncPos(ref str, ref pos);
            }

            if (beginPos)
                pos = tempPos;

            return ident;
        }

        static char FindNextSymbol(ref string str, ref int pos)
        /*возращает следующий символ кода на Pascal*/
        {
            while (str[pos] == ' ')
                IncPos(ref str, ref pos);
            char result = str[pos];
            IncPos(ref str, ref pos);
            return result;
        }

        static bool FindWordInArray(ref string word, ref string[] arr)
        /*определяет есть ли слово в массиве строк*/
        {
            bool result = false;
            for (int i = 0; i < arr.Length; i++)
                if (String.Compare(word, arr[i], true) == 0)
                    result = true;
            return result;
        }

        static void AddToVarArray(ref Vareble[] arr, string str)
        /*добавляет новый элемент в массив строк*/
        {
            Array.Resize(ref arr, arr.Length + 1);
            arr[arr.Length - 1] = new Vareble(str, VaribleKind.Неиспользуемая);
        }

        static void FindVaribles(ref string str, ref int pos, ref Vareble[] varArray)
        /*выписывает все переменные, стоящие после ближайшего слова var, в массив VariblesArr*/
        {
            string ident;
            do
            {
                do
                {
                    ident = FindNextIdentifier(ref str, ref pos, false);
                    AddToVarArray(ref varArray, ident);
                } while (FindNextSymbol(ref str, ref pos) != ':');
                while (FindNextSymbol(ref str, ref pos) != ';') ;   //пропускаем тип переменных
                ident = FindNextIdentifier(ref str, ref pos, true);
            } while (!FindWordInArray(ref ident, ref DescriptionWords)); //пока не найден новый раздел описания
        }

        static bool DetermineVaribleInArr(ref Vareble[] arr, ref string name, VaribleKind kind)
        /*инициализирует вид переменной с именем name в массиве arr*/
        {
            int i = 0;
            bool result = false;
            while ((i < arr.Length) && !result)
            {
                result = String.Compare(arr[i].name, name, true) == 0;
                if (result)
                    arr[i].kind = arr[i].kind | kind;
                i++;
            }
            return result;
        }

        static bool DetermineVarible(string name, VaribleKind kind)
        /*инициализирует вид переменной с именем name*/
        {
            bool result;
            if (!(result = DetermineVaribleInArr(ref LocalVaribles, ref name, kind)))
                result = DetermineVaribleInArr(ref GlobalVaribles, ref name, kind);
            return result;
        }

        static void AnalyzeOperatorFor(ref string str, ref int pos)
        /*определяет назночение переменных в заголовке for*/
        {
            FindNextIdentifier(ref str, ref pos, false);   //пропускаем слово "for"
            string ident =  FindNextIdentifier(ref str, ref pos, false);   //находим переменную цикла
            DetermineVarible(ident, VaribleKind.Испоьзуемая | VaribleKind.Модифицируемая);
            do
            {
                ident =  FindNextIdentifier(ref str, ref pos, false);
                DetermineVarible(ident, VaribleKind.Испоьзуемая);
            } while (String.Compare(ident, "do", true) != 0);
        }

        static void AnalyzeMethod(ref string str, ref int pos, VaribleKind kind)
        /*определяет назночение переменных в подпрограммах*/
        {
            string ident;
            char symbol;
            do
            {
                ident = FindNextIdentifier(ref str, ref pos, false);
                DetermineVarible(ident, kind);
                do
                {
                    symbol = FindNextSymbol(ref str, ref pos);
                } while ((symbol != ';') && !IsLetterOfIdent(symbol));
                if (IsLetterOfIdent(symbol))
                {
                    pos--;
                    ident = FindNextIdentifier(ref str, ref pos, true);
                }
            } while (!(FindWordInArray(ref ident, ref WordsOfEndOperator) || symbol == ';'));
        }

        static void AnalyzeCase(ref string str, ref int pos)
        /*определяет назночение переменной в операторе case*/
        {
            FindNextIdentifier(ref str, ref pos, false); //пропускаем слово case
            string ident = FindNextIdentifier(ref str, ref pos, false);
            DetermineVarible(ident, VaribleKind.Испоьзуемая | VaribleKind.Управляющая);
            while (String.Compare(ident, "of", true) != 0)
                ident = FindNextIdentifier(ref str, ref pos, false);
        }

        static void AnalyzeConditionalOperator(ref string str, ref int pos)
        {
            string ident;
            char symbol;
            VaribleKind kind = VaribleKind.Управляющая | VaribleKind.Испоьзуемая;
            do
            {
                ident = FindNextIdentifier(ref str, ref pos, false);
                DetermineVarible(ident, kind);
                do
                {
                    symbol = FindNextSymbol(ref str, ref pos);
                    if (symbol == '[') kind = VaribleKind.Испоьзуемая;
                    else if (symbol == ']') kind = VaribleKind.Управляющая | VaribleKind.Испоьзуемая;
                } while ((symbol != ';') && !IsLetterOfIdent(symbol));
                if (IsLetterOfIdent(symbol))
                {
                    pos--;
                    ident = FindNextIdentifier(ref str, ref pos, true);
                }

            } while (!(FindWordInArray(ref ident, ref WordsOfEndOperator) || symbol == ';'));
        }

        static void AnalyzeAssignmentOperator(ref string str, ref int pos)
        /*анализирует оператор присваивания*/
        {
            string firstVar = FindNextIdentifier(ref str, ref pos, false);
            string ident;
            char symbol;
            bool random = false;
            do
            {
                ident = FindNextIdentifier(ref str, ref pos, false);
                if (String.Compare(ident, "random", true) == 0)
                    random = true;
                if (String.Compare(ident, firstVar, true) != 0)
                    DetermineVarible(ident, VaribleKind.Испоьзуемая);
                do
                {
                    symbol = FindNextSymbol(ref str, ref pos);
                } while ((symbol != ';') && !IsLetterOfIdent(symbol));
                if (IsLetterOfIdent(symbol))
                {
                    pos--;
                    ident = FindNextIdentifier(ref str, ref pos, true);
                }
            } while (!(FindWordInArray(ref ident, ref WordsOfEndOperator) || symbol == ';'));
            if (random) DetermineVarible(firstVar, VaribleKind.Вводимая);
            else DetermineVarible(firstVar, VaribleKind.Модифицируемая);
        }

        static void AnalyzeOperator(ref string str, ref int pos)
        /*определяет тип оператора*/
        {
            string ident = FindNextIdentifier(ref str, ref pos, true);
            if (ident == null)
                return;
            switch (ident.ToLower())
            {
                case "for":
                    AnalyzeOperatorFor(ref str, ref pos);
                    break;
                case "while":
                case "repeat":
                case "if":
                    AnalyzeConditionalOperator(ref str, ref pos);//AnalyzeMethod(ref str, ref pos, VaribleKind.Управляющая | VaribleKind.Испоьзуемая);
                    break;
                case "case":
                    AnalyzeCase(ref str, ref pos);
                    break;
                case "begin":
                    beginEndCounter++;
                    FindNextIdentifier(ref str, ref pos, false);
                    break;
                case "end":
                    beginEndCounter--;
                    FindNextIdentifier(ref str, ref pos, false);
                    break;
                case "else":
                    FindNextIdentifier(ref str, ref pos, false);
                    break;
                case "read":
                case "readln":
                    AnalyzeMethod(ref str, ref pos, VaribleKind.Вводимая);
                    break;
                case "write":
                case "writeln":
                    AnalyzeMethod(ref str, ref pos, VaribleKind.Испоьзуемая);
                    break;
                case "inc":
                case "dec":
                    AnalyzeMethod(ref str, ref pos, VaribleKind.Модифицируемая);
                    break;
                default:
                    if (DetermineVarible(ident, VaribleKind.Неиспользуемая))
                        AnalyzeAssignmentOperator(ref str, ref pos);  //оператор присваивания
                    else
                        AnalyzeMethod(ref str, ref pos, VaribleKind.Испоьзуемая);   //подпрограмма
                    break;
            }
        }

        static void AnalayzeMethodHead(ref string str, ref int pos)
        /*находит переменные в заголовке подпрограммы*/
        {
            Console.WriteLine("\n{0}", FindNextIdentifier(ref str, ref pos, false));
            string ident;
            char symbol;
            do
            {
                VaribleKind kind = VaribleKind.Вводимая;
                do
                {
                    ident = FindNextIdentifier(ref str, ref pos, false);
                    switch (ident)
                    {
                        case "const": kind = VaribleKind.Вводимая; pos--; break;
                        case "var": kind = VaribleKind.Вводимая | VaribleKind.Испоьзуемая; pos--; break;
                        case "out": kind = VaribleKind.Испоьзуемая; pos--; break;
                        default:
                            AddToVarArray(ref LocalVaribles, ident);
                            DetermineVarible(ident, kind);
                            break;
                    }
                } while (FindNextSymbol(ref str, ref pos) != ':');
                while (((symbol = FindNextSymbol(ref str, ref pos)) != ';') && (symbol != ')')) ;   //пропускаем тип переменных
                kind = VaribleKind.Вводимая;
            } while (symbol != ')');    //пока не закончится описание переменных
        }

        static void PrintValues(int P, int M, int C, int T, double Res)
        /*выводит значения метрики*/
        {
            Console.WriteLine("P = {0}, M = {1}, C = {2}, T = {3}", P, M, C, T);
            Console.WriteLine("Q = P + 2*M + 3*C + 0.5*T = {0}", Res);
        }

        static void CalculateChepinValue(Vareble[] arr)
        {
            int P, M, C, T;
            P = M = C = T = 0;
            for (int i = 0; i < arr.Length; i++)
                if ((arr[i].kind & VaribleKind.Испоьзуемая) != 0)
                {
                    if ((arr[i].kind & VaribleKind.Вводимая) != 0)
                        P++;
                    if ((arr[i].kind & VaribleKind.Модифицируемая) != 0)
                        M++;
                    if ((arr[i].kind & VaribleKind.Управляющая) != 0)
                        C++;
                }
                else
                    T++;
            double result = P + 2 * M + 3 * C + 0.5 * T;
            PrintValues(P, M, C, T, result);
            totalP += P; totalM += M; totalC += C; totalT += T;
            ChepinValue += result;
        }

        static void PrintVaribles(Vareble[] arr)
        /*выводит переменные и их характеристики*/
        {
            for (int i = 0; i < arr.Length; i++)
            {
                if  (arr[i].kind == VaribleKind.Испоьзуемая)
                    arr[i].kind = arr[i].kind | VaribleKind.Модифицируемая;
                Console.WriteLine("{0} {1}", arr[i].name, arr[i].kind); 
            }
            CalculateChepinValue(arr);
        }

        static void AnalyzeProgram(ref string str, ref int pos)
        /*анализирует программный код находя в нём основные разделы описания*/
        {
            bool global = true;  
            string ident = FindNextIdentifier(ref str, ref pos, false);
            while (ident != null)
            {
                switch (ident.ToLower())
                {
                    case "var":
                        if (global)  FindVaribles(ref str, ref pos, ref GlobalVaribles);
                        else         FindVaribles(ref str, ref pos, ref LocalVaribles);
                        break;
                    case "function":
                        global = false;
                        LocalVaribles = new Vareble[0];
                        AnalayzeMethodHead(ref str, ref pos);
                        AddToVarArray(ref LocalVaribles, "Result");
                        DetermineVarible("Result", VaribleKind.Испоьзуемая);
                        break;
                    case "procedure":
                        global = false;
                        LocalVaribles = new Vareble[0];
                        AnalayzeMethodHead(ref str, ref pos);
                        break;
                    case "begin":
                        beginEndCounter++;
                        while (beginEndCounter != 0)
                            AnalyzeOperator(ref str, ref pos);
                        if (!global) PrintVaribles(LocalVaribles);
                        LocalVaribles = new Vareble[0];
                        global = true;
                        break;
                    default:
                        break;
                }
                ident = FindNextIdentifier(ref str, ref pos, false);
            }
        }

        static string GetFileName()
        /*возращает введённое с клавиатуры имя файла, но если он несуществует то возр. null*/
        {
            string fileName = Console.ReadLine();
            if (File.Exists(fileName))
                return fileName;
            else
                return null;
        }

        static void Main(string[] args)
        {
            Console.WriteLine("Введите имя файла:");
            string fileName;
            while ((fileName = GetFileName()) == null)
                Console.WriteLine("Файла с таким именем не найдено!\nВведите новое имя:");

            programCode = new StreamReader(fileName);

            string s = GetString();
            int pos = 0;
            AnalyzeProgram(ref s, ref pos);

            Console.WriteLine("\nГлобальные переменные");
            PrintVaribles(GlobalVaribles);

            Console.WriteLine("\nИтоговые результаты");
            PrintValues(totalP, totalM, totalC, totalT, ChepinValue);

            programCode.Close();
            Console.ReadLine();
        }
    }
}

//D:\БГУИР\ОАиП\Задачи\Длинные вычисления\Умножение\Umnoshenie.dpr
//D:\Laba_1.dpr
//D:\БГУИР\ОАиП\Лабы 2 сем\Лаба 2\Sravnenie_sortirovok.dpr