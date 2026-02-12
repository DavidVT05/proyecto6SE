using System;
using System.IO;
using System.Windows.Forms;
using System.Linq;
using System.Collections.Generic;

namespace Editor_de_texto.Clases
{
    public class Analizador_Sintactico
    {
        private List<string> tokenBuffer;
        private int tokenIndex;
        private RichTextBox CajaTexto2;
        public int N_error { get; private set; }
        private readonly string[] TiposDeDatos = { "int", "float", "char", "double", "long", "void" };

        public Analizador_Sintactico(string archivoIntermedio, RichTextBox caja)
        {
            CajaTexto2 = caja;
            N_error = 0;
            tokenIndex = 0;

            try
            {
                tokenBuffer = new List<string>();
                if (File.Exists(archivoIntermedio))
                {
                    using (StreamReader reader = new StreamReader(archivoIntermedio))
                    {
                        string line;
                        while ((line = reader.ReadLine()) != null)
                        {
                            if (!string.IsNullOrWhiteSpace(line))
                                tokenBuffer.Add(line.Trim());
                        }
                    }
                }
                tokenBuffer.Add("EOF");
            }
            catch (Exception ex)
            {
                caja.AppendText($"Error crítico: No se pudo leer el archivo intermedio: {ex.Message}\n");
                tokenBuffer = new List<string>() { "EOF" };
            }
        }
        // --- MÉTODOS AUXILIARES ---
        private string GetToken() => (tokenIndex < tokenBuffer.Count) ? tokenBuffer[tokenIndex++] : "EOF";
        private string PeekToken() => (tokenIndex < tokenBuffer.Count) ? tokenBuffer[tokenIndex] : "EOF";
        private string PeekNextToken() => (tokenIndex + 1 < tokenBuffer.Count) ? tokenBuffer[tokenIndex + 1] : "EOF";
        private void MatchToken(string expected)
        {
            string token = GetToken();
            if (token != expected)
            {
                N_error++;
                CajaTexto2.AppendText($"Error Sintáctico: Se esperaba '{expected}', se encontró '{token}'.\n");
                // Intento de sincronización simple
                if (PeekToken() == expected) GetToken();
            }
        }
        private void Error(string msg) { N_error++; CajaTexto2.AppendText($"Error: {msg}\n"); }
        private bool IsTipoDato(string t) { return Array.Exists(TiposDeDatos, e => e == t); }
        // MÉTODOS DE EXPRESIÓN
        // Función para consumir y validar una expresión simple hasta un separador (';', ')')
        private void AnalizarExpresionSimple(string contexto, string finalDelimiter1, string finalDelimiter2 = null)
        {
            string t = PeekToken();

            if (t == "EOF")
            {
                Error($"EOF inesperado en la expresión de {contexto}.");
                return;
            }

            // Si el token es el delimitador, la expresión está vacía y es válida (ej. for(;;))
            if (t == finalDelimiter1 || t == finalDelimiter2)
            {
                return;
            }

            // Comprobación heurística: asume que si no es un símbolo o palabra clave, es un ID, NUM o CADENA.
            bool isExpressionStarter =
                !string.IsNullOrEmpty(t) &&
                (t.All(c => char.IsLetterOrDigit(c) || c == '_') || t == "cadena" || t == "(" || t == "++" || t == "--");

            if (IsTipoDato(t) && t != "void")
            {
                Error($"Error sintáctico: No se puede declarar un tipo de dato ('{t}') dentro de la expresión de {contexto}.");
                GetToken();
            }

            if (isExpressionStarter)
            {
                // Consume la expresión completa, manejando paréntesis anidados
                int balance = 0;
                while (PeekToken() != finalDelimiter1 && PeekToken() != (finalDelimiter2 ?? finalDelimiter1) && PeekToken() != "EOF")
                {
                    string token = GetToken();
                    if (token == "(") balance++;
                    else if (token == ")") balance--;

                    if (balance < 0)
                    {
                        Error($"Paréntesis desbalanceados en la expresión de {contexto}.");
                        break;
                    }
                }
                if (balance != 0) Error($"Paréntesis desbalanceados en la expresión de {contexto}.");
            }
            else
            {
                // ¡ERROR! Se esperaba una expresión pero se encontró un token inesperado (por ejemplo, un operador solo).
                Error($"Error sintáctico: Se esperaba un valor o inicio de expresión en {contexto}. Se encontró '{t}'.");

                // Estrategia de pánico para recuperarse al siguiente separador
                while (PeekToken() != finalDelimiter1 && PeekToken() != (finalDelimiter2 ?? finalDelimiter1) && PeekToken() != "EOF")
                {
                    GetToken();
                }
            }
        }
        private void ConsumirExpresionEnParentesis(string estructura)
        {
            while (PeekToken() == "LF") GetToken();
            MatchToken("(");
            AnalizarExpresionSimple($"la condición de {estructura}", ")");
            MatchToken(")");
        }
        // -----------------------------------------------------
        // MÉTODOS AUXILIARES ESPECÍFICOS PARA FOR (STRICT)
        // -----------------------------------------------------
        private void AnalizarCondicionFor()
        {
            while (PeekToken() == "LF") GetToken();
            string contexto = "condición del for";

            // Si la condición está vacía, es válida (for(; ;))
            if (PeekToken() == ";") return;

            // --- 1. PRIMER OPERANDO (Variable/Valor) ---
            string t_op1 = PeekToken();
            if (!t_op1.All(c => char.IsLetterOrDigit(c) || c == '_') && !t_op1.All(char.IsDigit) && t_op1 != "(" && t_op1 != "cadena")
            {
                Error($"Error sintáctico: Se esperaba un identificador o valor (ej: 'i') al inicio de la {contexto}. Se encontró '{t_op1}'.");
                while (PeekToken() != ";" && PeekToken() != "EOF") GetToken();
                return;
            }
            GetToken(); // Consume el primer operando (ej: i)

            // --- 2. OPERADOR RELACIONAL ---
            while (PeekToken() == "LF") GetToken();
            string op = PeekToken();
            if (op == "<" || op == ">" || op == "==" || op == "!=" || op == "<=" || op == ">=")
            {
                GetToken(); // Consume el operador (ej: <)
            }
            else
            {
                Error($"Error sintáctico: Se esperaba un operador relacional ('<', '>', '==', etc.) en la {contexto}. Se encontró '{op}'.");
                while (PeekToken() != ";" && PeekToken() != "EOF") GetToken();
                return;
            }

            // --- 3. SEGUNDO OPERANDO (Valor numérico o Variable) ---
            while (PeekToken() == "LF") GetToken();
            string t_op2 = PeekToken();

            // Si el token siguiente es el ';' significa que falta el valor numérico (ej: i < ;)
            if (t_op2 == ";")
            {
                Error($"Error sintáctico: Se esperaba un valor numérico o variable después del operador '{op}' en la {contexto}.");
                return;
            }

            // Verificamos que el segundo operando sea un ID o un número
            if (!t_op2.All(c => char.IsLetterOrDigit(c) || c == '_') && !t_op2.All(char.IsDigit) && t_op2 != "(" && t_op2 != "cadena")
            {
                Error($"Error sintáctico: Se esperaba un valor numérico o variable después del operador en la {contexto}. Se encontró '{t_op2}'.");
                while (PeekToken() != ";" && PeekToken() != "EOF") GetToken();
                return;
            }

            // 4. Consumir el resto de la expresión hasta el ';' (por si hay más operadores lógicos &&, ||)
            AnalizarExpresionSimple(contexto, ";");
        }
        private void AnalizarIncrementoFor()
        {
            while (PeekToken() == "LF") GetToken();
            string contexto = "incremento del for";

            // Si el incremento está vacío, es válido (for(;;))
            if (PeekToken() == ")") return;

            // --- 1. PRIMER OPERANDO (Variable) ---
            string t_op1 = PeekToken();
            if (!t_op1.All(c => char.IsLetterOrDigit(c) || c == '_') && !t_op1.All(char.IsDigit) && t_op1 != "(")
            {
                Error($"Error sintáctico: El {contexto} debe comenzar con un identificador o valor. Se encontró '{t_op1}'.");
                while (PeekToken() != ")" && PeekToken() != "EOF") GetToken();
                return;
            }
            GetToken(); // Consume el primer operando (ej: i)

            // --- 2. OPERADOR DE INCREMENTO/ASIGNACIÓN ---
            while (PeekToken() == "LF") GetToken();
            string op = PeekToken();
            if (op == "=" || op == "++" || op == "--" || op == "+" || op == "-" || op == "*" || op == "/")
            {
                GetToken(); // Consume el operador (ej: +)
            }
            else
            {
                Error($"Error sintáctico: Se esperaba un operador de asignación o incremento ('=', '++', '+', etc.) en el {contexto}. Se encontró '{op}'.");
                while (PeekToken() != ")" && PeekToken() != "EOF") GetToken();
                return;
            }

            // --- 3. SEGUNDO OPERANDO (Valor numérico o Variable) ---
            // Solo se requiere si el operador NO es unario (++, --)
            if (op != "++" && op != "--")
            {
                while (PeekToken() == "LF") GetToken();
                string t_op2 = PeekToken();

                // Si el token siguiente es el ')' significa que falta el valor numérico (ej: i + )
                if (t_op2 == ")")
                {
                    Error($"Error sintáctico: Se esperaba un valor numérico o variable después del operador '{op}' en el {contexto}.");
                    return;
                }

                if (!t_op2.All(c => char.IsLetterOrDigit(c) || c == '_') && !t_op2.All(char.IsDigit) && t_op2 != "(")
                {
                    Error($"Error sintáctico: Se esperaba un identificador o valor después del operador en el {contexto}. Se encontró '{t_op2}'.");
                    while (PeekToken() != ")" && PeekToken() != "EOF") GetToken();
                    return;
                }
            }

            // 4. Consumir el resto de la expresión hasta el ')'
            AnalizarExpresionSimple(contexto, ")");
        }
        // --- INICIO DEL ANÁLISIS ---
        public void Analisis_Sintactico()
        {
            if (tokenBuffer == null || tokenBuffer.Count <= 1) return;
            CajaTexto2.AppendText("\n--- Iniciando Análisis Sintáctico ---\n");

            AnalizarPrograma();

            if (N_error == 0) CajaTexto2.AppendText("\nAnálisis Sintáctico Exitoso. Estructura Correcta.\n");
            else CajaTexto2.AppendText($"\nAnálisis finalizado con {N_error} errores.\n");
        }
        private void AnalizarPrograma()
        {
            while (PeekToken() == "LF") GetToken();
            AnalizarDirectivas();

            while (IsTipoDato(PeekToken()))
            {
                if (PeekToken() == "int" && PeekNextToken() == "main") break;
                while (PeekToken() == "LF") GetToken();
                AnalizarDeclaracion();
                while (PeekToken() == "LF") GetToken();
            }

            AnalizarFuncionMain();
            while (PeekToken() == "LF") GetToken();
            MatchToken("EOF");
        }
        private void AnalizarDirectivas()
        {
            while (PeekToken() == "LF") GetToken();
            while (PeekToken() == "#")
            {
                MatchToken("#");
                string includeToken = GetToken();
                if (includeToken == "include")
                {
                    MatchToken("<");
                    GetToken();
                    MatchToken(">");
                }
                else Error($"Directiva no reconocida después de '#': {includeToken}");
                while (PeekToken() == "LF") GetToken();
            }
        }
        private void AnalizarFuncionMain()
        {
            while (PeekToken() == "LF") GetToken();
            MatchToken("int");
            MatchToken("main");
            MatchToken("(");
            MatchToken(")");
            AnalizarBloque();
        }
        private void AnalizarBloque()
        {
            while (PeekToken() == "LF") GetToken();
            MatchToken("{");
            while (PeekToken() != "}" && PeekToken() != "EOF")
            {
                AnalizarSentencia();
            }
            MatchToken("}");
            while (PeekToken() == "LF") GetToken();
        }
        private void AnalizarSentencia()
        {
            while (PeekToken() == "LF") GetToken();
            string t = PeekToken();

            if (t == "}" || t == "EOF") return;

            if (IsTipoDato(t)) AnalizarDeclaracion();
            else if (t == "if") AnalizarIf();
            else if (t == "while") AnalizarWhile();
            else if (t == "do") AnalizarDoWhile();
            else if (t == "for") AnalizarFor();
            else if (t == "switch") AnalizarSwitch();
            else if (t == "return")
            {
                MatchToken("return");
                AnalizarExpresionSimple("sentencia return", ";");
                MatchToken(";");
                while (PeekToken() == "LF") GetToken();
            }
            else if (t == "printf")
            {
                MatchToken("printf"); MatchToken("(");
                AnalizarExpresionSimple("argumentos de printf", ")");
                MatchToken(")");
                MatchToken(";");
                while (PeekToken() == "LF") GetToken();
            }
            else
            {
                // Asignación, Llamada, Incremento/Decremento

                GetToken(); // Consume ID

                while (PeekToken() == "[")
                {
                    MatchToken("[");
                    AnalizarExpresionSimple("índice de array", "]");
                    MatchToken("]");
                }

                string next = PeekToken();

                if (next == "=")
                {
                    MatchToken("=");
                    AnalizarExpresionSimple("asignación", ";");
                    MatchToken(";");
                    while (PeekToken() == "LF") GetToken();
                }
                else if (next == "++" || next == "--")
                {
                    GetToken();
                    MatchToken(";");
                    while (PeekToken() == "LF") GetToken();
                }
                else if (next == "(") // Llamada a función
                {
                    MatchToken("(");
                    AnalizarExpresionSimple("argumentos de función", ")");
                    MatchToken(")");
                    MatchToken(";");
                    while (PeekToken() == "LF") GetToken();
                }
                else
                {
                    Error($"Sentencia inválida: Se esperaba '=', '++', '--', o '(' después de '{t}'.");
                    while (PeekToken() != ";" && PeekToken() != "LF" && PeekToken() != "EOF") GetToken();
                    if (PeekToken() == ";") MatchToken(";");
                    while (PeekToken() == "LF") GetToken();
                }
            }
        }
        private void AnalizarDeclaracion()
        {
            while (PeekToken() == "LF") GetToken();
            GetToken(); // Consume Tipo
            GetToken(); // Consume ID

            while (PeekToken() == "[")
            {
                MatchToken("[");
                AnalizarExpresionSimple("tamaño de array", "]");
                MatchToken("]");
            }

            if (PeekToken() == "=")
            {
                MatchToken("=");

                // Consumir el valor de inicialización (puede incluir llaves para arrays)
                int braces = 0;
                while (true)
                {
                    string next = PeekToken();
                    if (next == ";" && braces == 0) break;
                    if (next == "EOF") { Error("EOF inesperado en inicialización de declaración."); break; }
                    if (next == "{") braces++;
                    if (next == "}") braces--;
                    GetToken();
                }
                if (braces != 0) Error("Llaves desbalanceadas en la inicialización de la variable.");
            }

            MatchToken(";");
            if (PeekToken() == "LF") MatchToken("LF");
        }
        // --- ESTRUCTURAS DE CONTROL ---
        private void AnalizarFor()
        {
            while (PeekToken() == "LF") GetToken();
            MatchToken("for");
            MatchToken("(");

            // 1. Inicialización (Sintaxis estricta: Asignación/Declaración)
            while (PeekToken() == "LF") GetToken();
            if (PeekToken() != ";")
            {
                string initialToken = GetToken();
                string nextToken = PeekToken();

                if (nextToken == "=" || IsTipoDato(initialToken))
                {
                    if (nextToken == "=") MatchToken("=");
                    AnalizarExpresionSimple("inicialización del for", ";");
                }
                else if (initialToken.All(c => char.IsLetterOrDigit(c) || c == '_') && (nextToken == "++" || nextToken == "--"))
                {
                    GetToken();
                    AnalizarExpresionSimple("inicialización del for", ";");
                }
                else
                {
                    Error($"Error sintáctico: Se esperaba un operador de asignación ('='), '++'/'--', o una declaración en la inicialización del bucle 'for'. Se encontró '{initialToken}' seguido de '{nextToken}'.");
                    while (PeekToken() != ";" && PeekToken() != "EOF") GetToken();
                }
            }
            MatchToken(";");

            // 2. Condición (Usa el método estricto)
            AnalizarCondicionFor();
            MatchToken(";");

            // 3. Incremento/Decremento (Usa el método estricto)
            AnalizarIncrementoFor();
            MatchToken(")");

            AnalizarBloque();
        }
        private void AnalizarIf()
        {
            while (PeekToken() == "LF") GetToken();
            MatchToken("if");
            ConsumirExpresionEnParentesis("if");
            AnalizarBloque();
            if (PeekToken() == "else")
            {
                while (PeekToken() == "LF") GetToken();
                MatchToken("else");
                AnalizarBloque();
            }
        }
        private void AnalizarWhile()
        {
            while (PeekToken() == "LF") GetToken();
            MatchToken("while");
            ConsumirExpresionEnParentesis("while");
            AnalizarBloque();
        }
        private void AnalizarSwitch()
        {
            while (PeekToken() == "LF") GetToken();
            MatchToken("switch");
            ConsumirExpresionEnParentesis("switch");

            while (PeekToken() == "LF") GetToken();
            MatchToken("{");

            bool defaultFound = false;

            while (PeekToken() != "}" && PeekToken() != "EOF")
            {
                while (PeekToken() == "LF") GetToken();

                if (PeekToken() == "case")
                {
                    MatchToken("case");
                    AnalizarExpresionSimple("valor de case", ":");
                    MatchToken(":");
                    while (PeekToken() == "LF") GetToken();

                    while (PeekToken() != "case" && PeekToken() != "default" && PeekToken() != "}" && PeekToken() != "EOF")
                    {
                        if (PeekToken() == "break") { MatchToken("break"); MatchToken(";"); }
                        else AnalizarSentencia();
                        while (PeekToken() == "LF") GetToken();
                    }
                }
                else if (PeekToken() == "default")
                {
                    if (defaultFound) Error("Declaración 'default' duplicada en el switch.");
                    defaultFound = true;
                    MatchToken("default");
                    MatchToken(":");
                    while (PeekToken() == "LF") GetToken();

                    while (PeekToken() != "}" && PeekToken() != "EOF")
                    {
                        if (PeekToken() == "break") { MatchToken("break"); MatchToken(";"); }
                        else AnalizarSentencia();
                        while (PeekToken() == "LF") GetToken();
                    }
                }
                else
                {
                    Error($"Se esperaba 'case' o 'default', se encontró '{PeekToken()}' dentro de switch.");
                    GetToken();
                }
            }
            MatchToken("}");
            while (PeekToken() == "LF") GetToken();
        }
        private void AnalizarDoWhile()
        {
            while (PeekToken() == "LF") GetToken();
            MatchToken("do");
            AnalizarBloque();

            while (PeekToken() == "LF") GetToken();
            MatchToken("while");
            ConsumirExpresionEnParentesis("do-while");

            MatchToken(";");
            if (PeekToken() == "LF") MatchToken("LF");
        }
    }
}