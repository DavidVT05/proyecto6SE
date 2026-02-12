using System;
using System.IO;
using System.Windows.Forms;
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

		// --- TABLA DE SÍMBOLOS Y SEMÁNTICA ---
		public struct Simbolo
		{
			public string Nombre;
			public string Tipo;
			public string Categoria; // "Variable" o "Funcion"
		}
		private List<Simbolo> TablaSimbolos = new List<Simbolo>();
		private string tipoFuncionActual = ""; // Para validar el 'return'

		public Analizador_Sintactico(string archivoIntermedio, RichTextBox caja)
		{
			CajaTexto2 = caja;
			N_error = 0;
			tokenIndex = 0;
			tokenBuffer = new List<string>();

			try
			{
				if (File.Exists(archivoIntermedio))
				{
					foreach (string line in File.ReadLines(archivoIntermedio))
					{
						if (!string.IsNullOrWhiteSpace(line)) tokenBuffer.Add(line.Trim());
					}
				}
				tokenBuffer.Add("EOF");
			}
			catch
			{
				tokenBuffer.Add("EOF");
			}
		}

		// --- MÉTODOS DE APOYO ---
		private string GetToken() => (tokenIndex < tokenBuffer.Count) ? tokenBuffer[tokenIndex++] : "EOF";
		private string PeekToken() => (tokenIndex < tokenBuffer.Count) ? tokenBuffer[tokenIndex] : "EOF";
		private void SkipLF() { while (PeekToken() == "LF") GetToken(); }

		private void MatchToken(string expected)
		{
			SkipLF();
			if (PeekToken() == expected) GetToken();
			else
			{
				N_error++;
				CajaTexto2.AppendText($"Error Sintáctico: Se esperaba '{expected}', se encontró '{PeekToken()}'.\n");
			}
		}

		private void Error(string msg) { N_error++; CajaTexto2.AppendText($"{msg}\n"); }
		private bool IsTipoDato(string t) => Array.Exists(TiposDeDatos, e => e == t);
		private bool ExisteSimbolo(string nombre) => TablaSimbolos.Exists(s => s.Nombre == nombre);

		// --- ANÁLISIS PRINCIPAL ---
		public void Analisis_Sintactico()
		{
			CajaTexto2.AppendText("\n--- Iniciando Análisis Sintáctico y Semántico ---\n");
			AnalizarPrograma();
			if (N_error == 0) CajaTexto2.AppendText("\nResultado: Compilación Exitosa.\n");
			else CajaTexto2.AppendText($"\nResultado: {N_error} errores detectados.\n");
		}

		private void AnalizarPrograma()
		{
			SkipLF();
			while (PeekToken() == "#") { AnalizarDirectiva(); SkipLF(); }

			while (PeekToken() != "EOF")
			{
				SkipLF();
				string t = PeekToken();
				if (IsTipoDato(t))
				{
					if (PeekNextNoLF() == "main") AnalizarMain();
					else if (EsDefinicionFuncion()) AnalizarDefinicionFuncion();
					else AnalizarDeclaracion();
				}
				else if (t != "EOF")
				{
					Error($"Error: Token inesperado '{t}' fuera de contexto.");
					GetToken();
				}
				SkipLF();
			}
		}

		private void AnalizarDirectiva()
		{
			MatchToken("#");
			if (GetToken() == "include")
			{
				MatchToken("<"); GetToken(); MatchToken(">");
			}
		}

		private void AnalizarDeclaracion()
		{
			SkipLF();
			string tipo = GetToken();
			string nombre = GetToken();

			// Validación Semántica: Duplicados
			if (ExisteSimbolo(nombre)) Error($"Error Semántico: La variable '{nombre}' ya fue declarada.");
			else TablaSimbolos.Add(new Simbolo { Nombre = nombre, Tipo = tipo, Categoria = "Variable" });

			if (PeekToken() == "=")
			{
				MatchToken("=");
				string valor = PeekToken();
				// Validación Semántica: Tipo int vs float
				if (tipo == "int" && valor.Contains("."))
					Error($"Error Semántico: No se puede asignar float '{valor}' a int '{nombre}'.");

				AnalizarExpresionSimple("inicialización", ";");
			}
			MatchToken(";");
		}

		private void AnalizarDefinicionFuncion()
		{
			tipoFuncionActual = GetToken(); // Tipo de retorno
			string nombre = GetToken();

			if (!ExisteSimbolo(nombre))
				TablaSimbolos.Add(new Simbolo { Nombre = nombre, Tipo = tipoFuncionActual, Categoria = "Funcion" });

			MatchToken("(");
			if (IsTipoDato(PeekToken())) AnalizarParametros();
			MatchToken(")");
			AnalizarBloque();
			tipoFuncionActual = "";
		}

		private void AnalizarParametros()
		{
			do
			{
				if (PeekToken() == ",") GetToken();
				string tipo = GetToken();
				string nombre = GetToken();
				if (!ExisteSimbolo(nombre))
					TablaSimbolos.Add(new Simbolo { Nombre = nombre, Tipo = tipo, Categoria = "Variable" });
			} while (PeekToken() == ",");
		}

		private void AnalizarMain()
		{
			tipoFuncionActual = GetToken(); // int
			MatchToken("main"); MatchToken("("); MatchToken(")");
			AnalizarBloque();
			tipoFuncionActual = "";
		}

		private void AnalizarBloque()
		{
			MatchToken("{");
			while (PeekToken() != "}" && PeekToken() != "EOF")
			{
				AnalizarSentencia();
				SkipLF();
			}
			MatchToken("}");
		}

		private void AnalizarSentencia()
		{
			SkipLF();
			string t = PeekToken();
			if (IsTipoDato(t)) AnalizarDeclaracion();
			else if (t == "if") { MatchToken("if"); ConsumirParentesis(); AnalizarBloque(); }
			else if (t == "return" || t == "Return")
			{
				GetToken();
				string val = PeekToken();
				// Validación Semántica: Return tipo
				if (tipoFuncionActual == "int" && val.Contains("."))
					Error("Error Semántico: El valor de retorno no coincide con el tipo 'int' de la función.");

				AnalizarExpresionSimple("return", ";");
				MatchToken(";");
			}
			else
			{
				// Uso de variable o función
				string id = GetToken();
				if (id == "}" || id == "EOF") return;

				if (!ExisteSimbolo(id) && id != "printf" && id != "main")
					Error($"Error Semántico: El identificador '{id}' no ha sido declarado.");

				string next = PeekToken();
				if (next == "=") { MatchToken("="); AnalizarExpresionSimple("asignación", ";"); MatchToken(";"); }
				else if (next == "(") { MatchToken("("); AnalizarExpresionSimple("argumentos", ")"); MatchToken(")"); MatchToken(";"); }
				else if (next == "++" || next == "--") { GetToken(); MatchToken(";"); }
			}
		}

		private void AnalizarExpresionSimple(string ctx, string delim1, string delim2 = null)
		{
			int p = 0;
			while (PeekToken() != "EOF")
			{
				string t = PeekToken();
				if (p == 0 && (t == delim1 || t == delim2)) break;
				if (t == ";" || t == "}")
				{ // Freno de seguridad
					if (p > 0) Error($"Error Sintáctico: Se esperaba cerrar paréntesis en {ctx}.");
					break;
				}
				t = GetToken();
				if (t == "(") p++; else if (t == ")") p--;
			}
		}

		private void ConsumirParentesis() { MatchToken("("); AnalizarExpresionSimple("paréntesis", ")"); MatchToken(")"); }

		private bool EsDefinicionFuncion()
		{
			int offset = 1;
			while (tokenIndex + offset < tokenBuffer.Count && tokenBuffer[tokenIndex + offset] == "LF") offset++;
			offset++; // Saltar ID
			while (tokenIndex + offset < tokenBuffer.Count && tokenBuffer[tokenIndex + offset] == "LF") offset++;
			return (tokenIndex + offset < tokenBuffer.Count && tokenBuffer[tokenIndex + offset] == "(");
		}

		private string PeekNextNoLF()
		{
			int offset = 1;
			while (tokenIndex + offset < tokenBuffer.Count && tokenBuffer[tokenIndex + offset] == "LF") offset++;
			return (tokenIndex + offset < tokenBuffer.Count) ? tokenBuffer[tokenIndex + offset] : "EOF";
		}
	}
}