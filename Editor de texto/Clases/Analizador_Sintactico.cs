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

		// Tipos de datos soportados
		private readonly string[] TiposDeDatos = { "int", "float", "char", "double", "long", "void", "Int", "Float" };

		// --- TABLA DE SÍMBOLOS ---
		public struct Simbolo
		{
			public string Nombre;
			public string Tipo;
			public string Categoria; // "Variable" o "Funcion"
		}
		private List<Simbolo> TablaSimbolos = new List<Simbolo>();
		private string tipoFuncionActual = "";

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
			catch (Exception ex)
			{
				CajaTexto2.AppendText("Error al leer archivo: " + ex.Message);
				tokenBuffer.Add("EOF");
			}
		}

		// --- NAVEGACIÓN DE TOKENS ---
		private string GetToken() => (tokenIndex < tokenBuffer.Count) ? tokenBuffer[tokenIndex++] : "EOF";
		private string PeekToken() => (tokenIndex < tokenBuffer.Count) ? tokenBuffer[tokenIndex] : "EOF";
		private void SkipLF() { while (PeekToken() == "LF") GetToken(); }

		private void MatchToken(string expected)
		{
			SkipLF();
			if (PeekToken() == expected)
			{
				GetToken();
			}
			else
			{
				N_error++;
				CajaTexto2.AppendText($"Error Sintáctico: Se esperaba '{expected}', se encontró '{PeekToken()}'.\n");
			}
		}

		private void Error(string msg)
		{
			N_error++;
			CajaTexto2.AppendText($"{msg}\n");
		}

		private bool IsTipoDato(string t) => Array.Exists(TiposDeDatos, e => e.Equals(t, StringComparison.OrdinalIgnoreCase));
		private bool ExisteSimbolo(string nombre) => TablaSimbolos.Exists(s => s.Nombre == nombre);

		// --- LÓGICA DE ANÁLISIS ---

		public void Analisis_Sintactico()
		{
			CajaTexto2.AppendText("\n--- Iniciando Análisis Sintáctico y Semántico ---\n");
			TablaSimbolos.Clear();
			AnalizarPrograma();

			if (N_error == 0) CajaTexto2.AppendText("\nResultado: Compilación Exitosa (0 errores).\n");
			else CajaTexto2.AppendText($"\nResultado: {N_error} errores detectados.\n");
		}

		private void AnalizarPrograma()
		{
			SkipLF();
			// 1. Directivas #include
			while (PeekToken() == "#")
			{
				MatchToken("#");
				if (GetToken() == "include")
				{
					MatchToken("<"); GetToken(); MatchToken(">");
				}
				SkipLF();
			}

			// 2. Variables Globales o Funciones
			while (PeekToken() != "EOF")
			{
				SkipLF();
				string t = PeekToken();

				if (IsTipoDato(t))
				{
					if (PeekNextNoLF().ToLower() == "main")
					{
						AnalizarMain();
					}
					else if (EsDefinicionFuncion())
					{
						AnalizarDefinicionFuncion();
					}
					else
					{
						AnalizarDeclaracion();
					}
				}
				else if (t != "EOF")
				{
					Error($"Error: Token '{t}' no permitido fuera de funciones.");
					GetToken();
				}
				SkipLF();
			}
		}

		private void AnalizarDeclaracion()
		{
			SkipLF();
			string tipo = GetToken();
			string nombre = GetToken();

			// Semántica: Declaración Duplicada
			if (ExisteSimbolo(nombre))
			{
				Error($"Error Semántico: La variable '{nombre}' ya existe.");
			}
			else
			{
				TablaSimbolos.Add(new Simbolo { Nombre = nombre, Tipo = tipo, Categoria = "Variable" });
			}

			if (PeekToken() == "=")
			{
				MatchToken("=");
				string valor = PeekToken();
				// Semántica: Validación de tipo (int = float)
				if (tipo.ToLower() == "int" && valor.Contains("."))
				{
					Error($"Error Semántico: No puedes asignar un decimal '{valor}' a la variable entera '{nombre}'.");
				}
				AnalizarExpresionSimple("asignación", ";");
			}
			MatchToken(";");
		}

		private void AnalizarDefinicionFuncion()
		{
			SkipLF();
			tipoFuncionActual = GetToken(); // Tipo de la función
			string nombre = GetToken();

			if (!ExisteSimbolo(nombre))
				TablaSimbolos.Add(new Simbolo { Nombre = nombre, Tipo = tipoFuncionActual, Categoria = "Funcion" });

			MatchToken("(");
			if (IsTipoDato(PeekToken()))
			{
				AnalizarParametros();
			}
			MatchToken(")");
			AnalizarBloque();
			tipoFuncionActual = "";
		}

		private void AnalizarParametros()
		{
			do
			{
				if (PeekToken() == ",") GetToken();
				SkipLF();

				string tipo = PeekToken();
				if (!IsTipoDato(tipo))
				{
					Error($"Error Sintáctico: Se esperaba tipo de dato en parámetros, se encontró '{tipo}'.");
					break;
				}
				GetToken(); // Consume tipo

				string nombre = PeekToken();
				if (nombre == ")" || nombre == ",")
				{
					Error("Error Sintáctico: Falta el nombre del parámetro.");
				}
				else
				{
					nombre = GetToken();
					if (!ExisteSimbolo(nombre))
						TablaSimbolos.Add(new Simbolo { Nombre = nombre, Tipo = tipo, Categoria = "Variable" });
				}
			} while (PeekToken() == ",");
		}

		private void AnalizarMain()
		{
			GetToken(); // int
			MatchToken("main");
			MatchToken("("); MatchToken(")");
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

		private void AnalizarSentencia()
		{
			SkipLF();
			string t = PeekToken();

			if (IsTipoDato(t))
			{
				AnalizarDeclaracion();
			}
			else if (t == "if")
			{
				MatchToken("if");
				MatchToken("("); AnalizarExpresionSimple("condición if", ")"); MatchToken(")");
				AnalizarBloque();
			}
			else if (t.ToLower() == "return")
			{
				GetToken();
				string val = PeekToken();
				// Semántica: Validar retorno de función int
				if (tipoFuncionActual.ToLower() == "int" && val.Contains("."))
					Error("Error Semántico: La función int no puede retornar un valor decimal.");

				AnalizarExpresionSimple("return", ";");
				MatchToken(";");
			}
			else
			{
				string id = GetToken();
				if (id == "}" || id == "EOF") return;

				// Semántica: Uso de variable no declarada
				if (!ExisteSimbolo(id) && id != "printf" && id != "main")
					Error($"Error Semántico: '{id}' no ha sido declarado.");

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
					AnalizarExpresionSimple("argumentos", ")");
					MatchToken(")");
					MatchToken(";");
				}
				else if (next == "++" || next == "--")
				{
					GetToken(); MatchToken(";");
				}
			}
		}

		private void AnalizarExpresionSimple(string ctx, string d1, string d2 = null)
		{
			int p = 0;
			while (PeekToken() != "EOF")
			{
				string t = PeekToken();
				if (p == 0 && (t == d1 || t == d2)) break;
				if (t == ";" || t == "}")
				{
					if (p > 0) Error($"Error Sintáctico: Paréntesis sin cerrar en {ctx}.");
					break;
				}
				t = GetToken();
				if (t == "(") p++; else if (t == ")") p--;
			}
		}

		private bool EsDefinicionFuncion()
		{
			int off = 1;
			while (tokenIndex + off < tokenBuffer.Count && tokenBuffer[tokenIndex + off] == "LF") off++;
			off++; // Salta ID
			while (tokenIndex + off < tokenBuffer.Count && tokenBuffer[tokenIndex + off] == "LF") off++;
			return (tokenIndex + off < tokenBuffer.Count && tokenBuffer[tokenIndex + off] == "(");
		}

		private string PeekNextNoLF()
		{
			int off = 1;
			while (tokenIndex + off < tokenBuffer.Count && tokenBuffer[tokenIndex + off] == "LF") off++;
			return (tokenIndex + off < tokenBuffer.Count) ? tokenBuffer[tokenIndex + off] : "EOF";
		}
	}
}