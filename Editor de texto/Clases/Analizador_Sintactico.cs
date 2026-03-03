using System;
using System.IO;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Editor_de_texto.Clases
{
	public class Analizador_Sintactico
	{
		public class Variable
		{
			public string Nombre { get; set; }
			public string Tipo { get; set; }
			public string Origen { get; set; }
		}

		public class Funcion
		{
			public string TipoRetorno { get; set; }
			public string Nombre { get; set; }
			public int NumParametros { get; set; }
			public string Parametros { get; set; }
		}

		private List<Variable> TablaVariables = new List<Variable>();
		private List<Funcion> TablaFunciones = new List<Funcion>();

		private List<string> tokenBuffer;
		private int tokenIndex;
		private RichTextBox CajaTexto2;
		private string ambitoActual = "global";
		private string tipoFuncionActual = "";
		public int N_error { get; private set; }

		private readonly string[] TiposDeDatos = { "int", "float", "char", "void", "double", "Int", "Float" };

		public Analizador_Sintactico(string archivoIntermedio, RichTextBox caja)
		{
			CajaTexto2 = caja;
			tokenIndex = 0;
			tokenBuffer = new List<string>();

			try
			{
				if (File.Exists(archivoIntermedio))
				{
					tokenBuffer.AddRange(File.ReadAllLines(archivoIntermedio)
						.Select(l => l.Trim())
						.Where(l => !string.IsNullOrEmpty(l)));
				}
				tokenBuffer.Add("EOF");
			}
			catch { tokenBuffer.Add("EOF"); }
		}

		public void Analisis_Sintactico()
		{
			CajaTexto2.AppendText("\n--- Iniciando Fase Sintáctica y Semántica ---\n");
			TablaVariables.Clear();
			TablaFunciones.Clear();
			ambitoActual = "global";
			N_error = 0;
			tokenIndex = 0;

			ProcesarEstructuraBase();

			if (N_error == 0) MostrarResultadosEnPantalla();
		}

		private void ProcesarEstructuraBase()
		{
			SkipLF();
			while (PeekToken() == "#")
			{
				Match("#"); Match("include"); Match("<"); GetToken(); Match(">");
				SkipLF();
			}

			while (PeekToken() != "EOF")
			{
				SkipLF();
				if (IsTipoDato(PeekToken()))
				{
					if (PeekNextNoLF().ToLower() == "main") ProcesarMain();
					else if (EsDefinicionDeFuncion()) ProcesarFuncion();
					else ProcesarDeclaracionVariable();
				}
				else if (PeekToken() != "EOF")
				{
					Error($"Error Sintáctico: Token inesperado '{PeekToken()}'.");
					GetToken();
				}
				SkipLF();
			}
		}

		private void ProcesarDeclaracionVariable()
		{
			string tipo = GetToken();
			string nombre = GetToken();

			if (TablaVariables.Exists(v => v.Nombre == nombre && v.Origen == ambitoActual))
			{
				Error($"Error Semántico: La variable '{nombre}' ya fue declarada en {ambitoActual}.");
			}
			else
			{
				TablaVariables.Add(new Variable { Nombre = nombre, Tipo = tipo, Origen = ambitoActual });
			}

			if (PeekToken() == "=")
			{
				Match("=");
				if (tipo.ToLower() == "int" && PeekToken().Contains("."))
					Error($"Error Semántico: Intento de asignar decimal a variable entera '{nombre}'.");

				// NUEVO: Validamos matemáticamente la expresión hasta encontrar el ';'
				ValidarExpresion(";");
			}
			Match(";");
		}

		private void ProcesarFuncion()
		{
			tipoFuncionActual = GetToken();
			string nombre = GetToken();
			string ambitoAnterior = ambitoActual;
			ambitoActual = nombre;

			Match("(");
			int countParams = 0;
			string detalleParams = "";

			if (IsTipoDato(PeekToken()))
			{
				do
				{
					if (PeekToken() == ",") Match(",");
					string pTipo = GetToken();
					string pNom = GetToken();

					detalleParams += (countParams > 0 ? ", " : "") + $"{pTipo} {pNom}";
					TablaVariables.Add(new Variable { Nombre = pNom, Tipo = pTipo, Origen = nombre });
					countParams++;
				} while (PeekToken() == ",");
			}
			Match(")");

			TablaFunciones.Add(new Funcion
			{
				TipoRetorno = tipoFuncionActual,
				Nombre = nombre,
				NumParametros = countParams,
				Parametros = detalleParams
			});

			AnalizarCuerpo();
			ambitoActual = ambitoAnterior;
			tipoFuncionActual = "";
		}

		private void ProcesarMain()
		{
			GetToken(); Match("main");
			TablaFunciones.Add(new Funcion { TipoRetorno = "int", Nombre = "main", NumParametros = 0, Parametros = "" });
			ambitoActual = "main";
			Match("("); Match(")");
			AnalizarCuerpo();
			ambitoActual = "global";
		}

		private void AnalizarCuerpo()
		{
			SkipLF();
			Match("{");
			while (PeekToken() != "}" && PeekToken() != "EOF")
			{
				SkipLF();
				string t = PeekToken();

				if (IsTipoDato(t)) ProcesarDeclaracionVariable();
				else if (t.ToLower() == "if")
				{
					Match("if"); Match("(");
					ValidarExpresion(")"); // Valida la condición interna
					Match(")");
					AnalizarCuerpo();
				}
				else if (t.ToLower() == "return")
				{
					GetToken();
					if (tipoFuncionActual.ToLower() == "int" && PeekToken().Contains("."))
						Error("Error Semántico: Retorno incompatible (decimal en función int).");

					ValidarExpresion(";");
					Match(";");
				}
				else
				{
					string idActual = GetToken();

					if (EsIdentificadorValido(idActual))
					{
						if (!ExisteSimbolo(idActual) && idActual != "printf")
						{
							Error($"Error Semántico: '{idActual}' no ha sido declarado.");
						}
					}

					if (PeekToken() == "=")
					{
						Match("=");
						ValidarExpresion(";");
						Match(";");
					}
					else if (PeekToken() == "(")
					{
						Match("(");
						ValidarExpresion(")");
						Match(")");
						Match(";");
					}
				}
				SkipLF();
			}
			Match("}");
		}

		// --- CORRECCIÓN CLAVE: El analizador de expresiones ---
		// Se encarga de revisar que los valores estén separados por comas u operadores
		private void ValidarExpresion(string delimitador)
		{
			bool ultimoFueValor = false;

			while (PeekToken() != delimitador && PeekToken() != ";" && PeekToken() != "EOF" && PeekToken() != "LF")
			{
				string token = PeekToken();

				if (token == "(")
				{
					GetToken(); // Consumimos el '('
					ValidarExpresion(")"); // Analizamos lo de adentro de forma recursiva
					Match(")");
					ultimoFueValor = true;
				}
				else if (token == ",")
				{
					GetToken();
					ultimoFueValor = false; // Después de una coma, esperamos otro valor
				}
				else if (EsOperador(token))
				{
					GetToken();
					ultimoFueValor = false; // Después de un operador (+, -), esperamos otro valor
				}
				else
				{
					// Si el token es un número, variable o cadena
					if (ultimoFueValor)
					{
						// ¡ERROR! Encontramos dos valores pegados sin coma ni operador (Ej: 10 20)
						Error($"Error Sintáctico: Falta ',' u operador antes de '{token}'.");
					}
					GetToken();
					ultimoFueValor = true;
				}
			}
		}

		// Diccionario de operadores permitidos
		private bool EsOperador(string t)
		{
			string[] ops = { "+", "-", "*", "/", "%", "==", "!=", "<", ">", "<=", ">=", "&&", "||", "!" };
			return ops.Contains(t);
		}

		private bool EsIdentificadorValido(string t)
		{
			if (string.IsNullOrEmpty(t)) return false;
			if (t.ToLower() == "cadena") return false;
			if (t.StartsWith("\"") || t.StartsWith("'")) return false;
			if (t.Length == 1 && !char.IsLetter(t[0])) return false;
			if (double.TryParse(t, out _)) return false;
			return char.IsLetter(t[0]);
		}

		public void ExportarTablasCSV(string rutaBase)
		{
			if (N_error > 0) return;

			try
			{
				string ruta = Path.ChangeExtension(rutaBase, ".csv");
				using (StreamWriter sw = new StreamWriter(ruta, false, Encoding.UTF8))
				{
					sw.WriteLine("TABLA DE VARIABLES");
					sw.WriteLine("Nombre,Tipo,Origen");
					foreach (var v in TablaVariables) sw.WriteLine($"{v.Nombre},{v.Tipo},{v.Origen}");

					sw.WriteLine("\nTABLA DE FUNCIONES");
					sw.WriteLine("Tipo de retorno,Nombre,Núm de Parametro,Parametros");
					foreach (var f in TablaFunciones)
					{
						string paramSeguros = string.IsNullOrEmpty(f.Parametros) ? "" : f.Parametros.Replace(",", " |");
						sw.WriteLine($"{f.TipoRetorno},{f.Nombre},{f.NumParametros},{paramSeguros}");
					}
				}
			}
			catch (Exception ex) { Error("Error CSV: " + ex.Message); }
		}

		private void MostrarResultadosEnPantalla()
		{
			CajaTexto2.AppendText("\n--- TABLA DE VARIABLES ---\n");
			CajaTexto2.AppendText("Nombre\t| Tipo\t| Origen\n");
			CajaTexto2.AppendText(new string('-', 30) + "\n");
			foreach (var v in TablaVariables) CajaTexto2.AppendText($"{v.Nombre}\t| {v.Tipo}\t| {v.Origen}\n");

			CajaTexto2.AppendText("\n--- TABLA DE FUNCIONES ---\n");
			CajaTexto2.AppendText("Retorno\t| Nombre\t| Parametros\n");
			CajaTexto2.AppendText(new string('-', 45) + "\n");
			foreach (var f in TablaFunciones)
				CajaTexto2.AppendText($"{f.TipoRetorno}\t| {f.Nombre}\t| ({f.NumParametros}) {f.Parametros}\n");
		}

		// --- MÉTODOS AUXILIARES ---
		private string GetToken() => (tokenIndex < tokenBuffer.Count) ? tokenBuffer[tokenIndex++] : "EOF";
		private string PeekToken() => (tokenIndex < tokenBuffer.Count) ? tokenBuffer[tokenIndex] : "EOF";
		private void SkipLF() { while (PeekToken() == "LF") GetToken(); }

		private void Match(string esperado)
		{
			SkipLF();
			if (PeekToken() == esperado)
			{
				GetToken();
			}
			else
			{
				Error($"Error Sintáctico: Se esperaba '{esperado}' pero se encontró '{PeekToken()}'.");
			}
		}

		private void Error(string msg)
		{
			N_error++;
			CajaTexto2.AppendText($"> {msg}\n");
		}

		private bool IsTipoDato(string t) => TiposDeDatos.Contains(t);
		private bool ExisteSimbolo(string n) => TablaVariables.Exists(v => v.Nombre == n) || TablaFunciones.Exists(f => f.Nombre == n);

		private string PeekNextNoLF()
		{
			int i = 1;
			while (tokenIndex + i < tokenBuffer.Count && tokenBuffer[tokenIndex + i] == "LF") i++;
			return (tokenIndex + i < tokenBuffer.Count) ? tokenBuffer[tokenIndex + i] : "EOF";
		}

		private bool EsDefinicionDeFuncion()
		{
			int i = 1;
			while (tokenIndex + i < tokenBuffer.Count && tokenBuffer[tokenIndex + i] == "LF") i++;
			i++;
			while (tokenIndex + i < tokenBuffer.Count && tokenBuffer[tokenIndex + i] == "LF") i++;
			return (tokenIndex + i < tokenBuffer.Count && tokenBuffer[tokenIndex + i] == "(");
		}
	}
}