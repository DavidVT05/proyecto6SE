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
		private readonly string[] TiposDeDatos = { "int", "float", "char", "double", "long", "void", "Int", "Float" };

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

		// Salta tokens de salto de línea (LF)
		private void SkipLF() { while (PeekToken() == "LF") GetToken(); }

		private void MatchToken(string expected)
		{
			SkipLF();
			string token = GetToken();
			if (token != expected)
			{
				N_error++;
				CajaTexto2.AppendText($"Error Sintáctico: Se esperaba '{expected}', se encontró '{token}'.\n");
			}
		}

		private void Error(string msg) { N_error++; CajaTexto2.AppendText($"Error: {msg}\n"); }
		private bool IsTipoDato(string t) { return Array.Exists(TiposDeDatos, e => e == t); }

		// --- LÓGICA DE ANÁLISIS PRINCIPAL ---

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
			SkipLF();
			AnalizarDirectivas();

			while (PeekToken() != "EOF")
			{
				SkipLF();
				string t = PeekToken();

				if (IsTipoDato(t))
				{
					// Si es "int main", vamos directo a la función main
					if (t.ToLower() == "int" && PeekNextNoLF() == "main")
					{
						AnalizarFuncionMain();
					}
					else
					{
						// Decidimos si es función o variable mirando si hay un '(' después del ID
						if (EsDefinicionFuncion())
							AnalizarDefinicionFuncion();
						else
							AnalizarDeclaracion();
					}
				}
				else if (t == "EOF") break;
				else
				{
					Error($"Token inesperado fuera de función: '{t}'");
					GetToken(); // Evitar bucle infinito
				}
				SkipLF();
			}
			MatchToken("EOF");
		}

		// Mira hacia adelante saltando los LF para ver si sigue un '('
		private bool EsDefinicionFuncion()
		{
			int tempIndex = tokenIndex + 1; // Saltamos el Tipo
			while (tempIndex < tokenBuffer.Count && tokenBuffer[tempIndex] == "LF") tempIndex++;
			tempIndex++; // Saltamos el Identificador
			while (tempIndex < tokenBuffer.Count && tokenBuffer[tempIndex] == "LF") tempIndex++;

			return tempIndex < tokenBuffer.Count && tokenBuffer[tempIndex] == "(";
		}

		private string PeekNextNoLF()
		{
			int tempIndex = tokenIndex + 1;
			while (tempIndex < tokenBuffer.Count && tokenBuffer[tempIndex] == "LF") tempIndex++;
			return (tempIndex < tokenBuffer.Count) ? tokenBuffer[tempIndex] : "EOF";
		}

		private void AnalizarDirectivas()
		{
			while (PeekToken() == "#")
			{
				MatchToken("#");
				string includeToken = GetToken();
				if (includeToken == "include")
				{
					MatchToken("<");
					GetToken(); // Nombre del archivo
					MatchToken(">");
				}
				else Error($"Directiva no reconocida: {includeToken}");
				SkipLF();
			}
		}

		// --- DEFINICIÓN DE FUNCIONES (BASADO EN TU DIAGRAMA) ---

		private void AnalizarDefinicionFuncion()
		{
			SkipLF();
			string tipo = GetToken(); // Consume Tipo
			string nombre = GetToken(); // Consume Identificador

			MatchToken("(");

			// Si el siguiente token es un tipo, hay parámetros
			if (IsTipoDato(PeekToken()))
			{
				AnalizarParametros();
			}

			MatchToken(")");
			AnalizarBloque(); // Bloque de sentencias
		}

		private void AnalizarParametros()
		{
			// Parámetro inicial: Tipo + Identificador
			GetToken(); // Tipo
			GetToken(); // ID

			// Bucle para comas (el camino de retorno en tu diagrama)
			while (PeekToken() == ",")
			{
				MatchToken(",");
				if (IsTipoDato(PeekToken()))
				{
					GetToken(); // Tipo
					GetToken(); // ID
				}
				else Error("Se esperaba tipo de dato después de ',' en parámetros.");
			}
		}

		private void AnalizarFuncionMain()
		{
			SkipLF();
			MatchToken("int");
			MatchToken("main");
			MatchToken("(");
			MatchToken(")");
			AnalizarBloque();
		}

		private void AnalizarBloque()
		{
			SkipLF();
			MatchToken("{");
			while (PeekToken() != "}" && PeekToken() != "EOF")
			{
				AnalizarSentencia();
				SkipLF();
			}
			MatchToken("}");
		}

		// --- SENTENCIAS Y EXPRESIONES ---

		private void AnalizarSentencia()
		{
			SkipLF();
			string t = PeekToken();

			if (t == "}" || t == "EOF") return;

			if (IsTipoDato(t)) AnalizarDeclaracion();
			else if (t == "if") AnalizarIf();
			else if (t == "while") AnalizarWhile();
			else if (t == "do") AnalizarDoWhile();
			else if (t == "for") AnalizarFor();
			else if (t == "switch") AnalizarSwitch();
			else if (t == "return" || t == "Return")
			{
				GetToken();
				AnalizarExpresionSimple("sentencia return", ";");
				MatchToken(";");
			}
			else if (t == "printf")
			{
				MatchToken("printf"); MatchToken("(");
				AnalizarExpresionSimple("argumentos de printf", ")");
				MatchToken(")");
				MatchToken(";");
			}
			else
			{
				// Asignación o Llamada
				string id = GetToken();
				string next = PeekToken();

				if (next == "=")
				{
					MatchToken("=");
					AnalizarExpresionSimple("asignación", ";");
					MatchToken(";");
				}
				else if (next == "(")
				{
					MatchToken("(");
					AnalizarExpresionSimple("argumentos de función", ")");
					MatchToken(")");
					MatchToken(";");
				}
				else if (next == "++" || next == "--")
				{
					GetToken(); MatchToken(";");
				}
				else
				{
					Error($"Sentencia no reconocida: {id}");
					while (PeekToken() != ";" && PeekToken() != "LF" && PeekToken() != "EOF") GetToken();
					if (PeekToken() == ";") GetToken();
				}
			}
		}

		private void AnalizarDeclaracion()
		{
			SkipLF();
			GetToken(); // Tipo
			GetToken(); // ID

			while (PeekToken() == "[")
			{
				MatchToken("[");
				AnalizarExpresionSimple("tamaño array", "]");
				MatchToken("]");
			}

			if (PeekToken() == "=")
			{
				MatchToken("=");
				AnalizarExpresionSimple("inicialización", ";");
			}

			MatchToken(";");
		}

		// --- MÉTODOS DE EXPRESIÓN Y CONTROL (Mantenidos de tu código original) ---

		private void AnalizarExpresionSimple(string contexto, string finalDelimiter1, string finalDelimiter2 = null)
		{
			SkipLF();
			string t = PeekToken();
			if (t == finalDelimiter1 || t == finalDelimiter2 || t == "EOF") return;

			int balance = 0;
			while (PeekToken() != "EOF")
			{
				string token = PeekToken();
				if (balance == 0 && (token == finalDelimiter1 || (finalDelimiter2 != null && token == finalDelimiter2))) break;

				token = GetToken();
				if (token == "(") balance++;
				else if (token == ")") balance--;

				if (balance < 0) { Error($"Paréntesis desbalanceados en {contexto}"); break; }
			}
		}

		private void ConsumirExpresionEnParentesis(string estructura)
		{
			MatchToken("(");
			AnalizarExpresionSimple($"condición de {estructura}", ")");
			MatchToken(")");
		}

		private void AnalizarIf() { MatchToken("if"); ConsumirExpresionEnParentesis("if"); AnalizarBloque(); if (PeekToken() == "else") { GetToken(); AnalizarBloque(); } }
		private void AnalizarWhile() { MatchToken("while"); ConsumirExpresionEnParentesis("while"); AnalizarBloque(); }
		private void AnalizarDoWhile() { MatchToken("do"); AnalizarBloque(); MatchToken("while"); ConsumirExpresionEnParentesis("do-while"); MatchToken(";"); }

		private void AnalizarFor()
		{
			MatchToken("for");
			MatchToken("(");
			if (PeekToken() != ";") AnalizarSentencia(); else MatchToken(";");
			AnalizarExpresionSimple("condición for", ";"); MatchToken(";");
			AnalizarExpresionSimple("incremento for", ")"); MatchToken(")");
			AnalizarBloque();
		}

		private void AnalizarSwitch()
		{
			MatchToken("switch");
			ConsumirExpresionEnParentesis("switch");
			MatchToken("{");
			while (PeekToken() != "}" && PeekToken() != "EOF")
			{
				if (PeekToken() == "case") { MatchToken("case"); AnalizarExpresionSimple("valor case", ":"); MatchToken(":"); }
				else if (PeekToken() == "default") { MatchToken("default"); MatchToken(":"); }
				else if (PeekToken() == "break") { MatchToken("break"); MatchToken(";"); }
				else AnalizarSentencia();
				SkipLF();
			}
			MatchToken("}");
		}
	}
}