using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Linq;

namespace Editor_de_texto.Clases
{
    internal class Analizador_Lexico
    {
        private StreamWriter Escribir;
        private RichTextBox CajaTexto2;
        private bool inBlockComment = false;

        public int Numero_linea { get; set; }
        public int N_error { get; private set; }

        public Analizador_Lexico(StreamWriter escribir, RichTextBox caja, int numLinea = 0, int nError = 0)
        {
            Escribir = escribir;
            CajaTexto2 = caja;
            Numero_linea = numLinea;
            N_error = nError;
        }

        public void Analisis_Lexico(string linea)
        {
            Numero_linea++;
            string processedLine = linea;

            // A. MANEJO DE COMENTARIOS DE BLOQUE (/* ... */)
            if (inBlockComment)
            {
                int endIndex = processedLine.IndexOf("*/");
                if (endIndex >= 0)
                {
                    inBlockComment = false;
                    processedLine = processedLine.Substring(endIndex + 2);
                }
                else { Escribir.WriteLine("LF"); Escribir.Flush(); return; }
            }

            if (string.IsNullOrWhiteSpace(processedLine)) { Escribir.WriteLine("LF"); Escribir.Flush(); return; }

            // Limpieza de comentarios de una línea (//) y bloque (/*)
            int blockStart = processedLine.IndexOf("/*");
            int lineStart = processedLine.IndexOf("//");
            int commentStart = -1;

            if (blockStart != -1 && (lineStart == -1 || blockStart < lineStart)) commentStart = blockStart;
            else if (lineStart != -1) commentStart = lineStart;

            if (commentStart != -1)
            {
                if (blockStart == commentStart && processedLine.IndexOf("*/", blockStart + 2) == -1) inBlockComment = true;
                processedLine = processedLine.Substring(0, commentStart);
            }

            if (string.IsNullOrWhiteSpace(processedLine)) { Escribir.WriteLine("LF"); Escribir.Flush(); return; }

            // C. REGEX
            var regex = new Regex(
                @"(?<string>""[^""]*"")|" +
                @"(?<lib><[^>]+>)|" +
                @"(?<op>==|!=|<=|>=|&&|\|\||\+\+|--)|" +
                @"(?<id>[A-Za-z_][A-Za-z0-9_]*)|" +
                @"(?<num>\d+(\.\d+)?)|" +
                @"(?<sym>[#\{\}\(\);\[\]<>=!+\-*/%,&|:])|" +
                @"(?<invalid>.)",
                RegexOptions.Compiled);

            var coincidencias = regex.Matches(processedLine);

            // CORRECCIÓN: Se eliminó la lógica de 'despuesDePuntoYComa' para permitir leer 'for(...; ...; ...)'

            foreach (Match match in coincidencias)
            {
                string texto = match.Value.Trim();
                if (string.IsNullOrWhiteSpace(texto)) continue;

                string tipo;
                if (match.Groups["string"].Success) tipo = "cadena";
                else if (match.Groups["lib"].Success) tipo = "lib";
                else if (match.Groups["op"].Success) tipo = "op";
                else if (match.Groups["id"].Success) tipo = "id";
                else if (match.Groups["num"].Success) tipo = "num";
                else if (match.Groups["sym"].Success) tipo = "sym";
                else tipo = "invalid";

                switch (tipo)
                {
                    case "cadena": Escribir.WriteLine("cadena"); break;
                    case "lib":
                        // Desglosamos la librería para el sintáctico
                        string nombreLib = texto.Substring(1, texto.Length - 2);
                        Escribir.WriteLine("<");
                        Escribir.WriteLine(nombreLib);
                        Escribir.WriteLine(">");
                        break;
                    case "op": Escribir.WriteLine(texto); break;
                    case "id": Escribir.WriteLine(texto); break;
                    case "num": Escribir.WriteLine(texto); break;
                    case "sym": Escribir.WriteLine(texto); break;
                    case "invalid":
                        N_error++;
                        CajaTexto2.AppendText($"Error Léxico en línea {Numero_linea}: Caracter inválido '{texto}'\n");
                        break;
                }
            }
            Escribir.WriteLine("LF");
            Escribir.Flush();
        }
    }
}