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
		// --- CLASES DE APOYO ---
		public class Variable
		{
			public string Nombre { get; set; }
			public string Tipo { get; set; }
			public string Origen { get; set; }
			public double ValorNum { get; set; } // Guarda el valor real para cálculos
		}

		public class Funcion
		{
			public string TipoRetorno { get; set; }
			public string Nombre { get; set; }
			public int NumParametros { get; set; }
			public string Parametros { get; set; }
		}

		public class NodoExpresion
		{
			public string Valor { get; set; }
			public NodoExpresion Izquierdo { get; set; }
			public NodoExpresion Derecho { get; set; }
			public NodoExpresion(string valor)
			{
				Valor = valor;
				Izquierdo = null;
				Derecho = null;
			}
		}

		// --- VARIABLES DE CONTROL ---
		private List<Variable> TablaVariables = new List<Variable>();
		private List<Funcion> TablaFunciones = new List<Funcion>();
		private List<string> tokenBuffer;
		private int tokenIndex;
		private RichTextBox CajaTexto2;
		private string ambitoActual = "global";
		public int N_error { get; private set; }

		private NodoExpresion UltimoArbolGenerado = null;
		private double ResultadoExpresion = 0;

		private readonly string[] TiposDeDatos = { "int", "float", "char", "void", "double" };

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

		// --- FLUJO PRINCIPAL ---
		public void Analisis_Sintactico()
		{
			CajaTexto2.Clear();
			CajaTexto2.AppendText("--- Iniciando Fase Sintáctica y Semántica ---\n");
			TablaVariables.Clear();
			TablaFunciones.Clear();
			UltimoArbolGenerado = null;
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
			Variable nuevaVar = null;

			if (TablaVariables.Exists(v => v.Nombre == nombre && v.Origen == ambitoActual))
			{
				Error($"Error Semántico: La variable '{nombre}' ya existe en {ambitoActual}.");
			}
			else
			{
				nuevaVar = new Variable { Nombre = nombre, Tipo = tipo, Origen = ambitoActual, ValorNum = 0 };
				TablaVariables.Add(nuevaVar);
			}

			if (PeekToken() == "=")
			{
				Match("=");
				NodoExpresion exp = AnalizarExpresion(";");
				if (nuevaVar != null) nuevaVar.ValorNum = EvaluarArbol(exp);
			}
			Match(";");
		}

		private void ProcesarFuncion()
		{
			string tipoRet = GetToken();
			string nombre = GetToken();
			string anterior = ambitoActual;
			ambitoActual = nombre;

			Match("(");
			int countParams = 0;
			string detalle = "";
			if (IsTipoDato(PeekToken()))
			{
				do
				{
					if (PeekToken() == ",") Match(",");
					string pT = GetToken(); string pN = GetToken();
					detalle += (countParams > 0 ? ", " : "") + $"{pT} {pN}";
					TablaVariables.Add(new Variable { Nombre = pN, Tipo = pT, Origen = nombre, ValorNum = 0 });
					countParams++;
				} while (PeekToken() == ",");
			}
			Match(")");

			TablaFunciones.Add(new Funcion { TipoRetorno = tipoRet, Nombre = nombre, NumParametros = countParams, Parametros = detalle });
			AnalizarCuerpo();
			ambitoActual = anterior;
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
			SkipLF(); Match("{");
			while (PeekToken() != "}" && PeekToken() != "EOF")
			{
				int tokenIndexInicial = tokenIndex;
				SkipLF();
				string t = PeekToken();

				if (IsTipoDato(t)) ProcesarDeclaracionVariable();
				else if (t.ToLower() == "if") { Match("if"); Match("("); AnalizarExpresion(")"); Match(")"); AnalizarCuerpo(); }
				else if (t.ToLower() == "return") { GetToken(); AnalizarExpresion(";"); Match(";"); }
				else
				{
					if (t == "}") break;

					if (EsIdentificadorValido(t) && PeekNextNoLF() == "=")
					{
						string nombreVar = GetToken();
						Match("=");
						UltimoArbolGenerado = AnalizarExpresion(";");
						ResultadoExpresion = EvaluarArbol(UltimoArbolGenerado);

						var v = TablaVariables.Find(var => var.Nombre == nombreVar);
						if (v != null) v.ValorNum = ResultadoExpresion;
					}
					else { AnalizarFactor(); }
					Match(";");
				}
				if (tokenIndex == tokenIndexInicial) GetToken();
				SkipLF();
			}
			Match("}");
		}

		// --- MOTOR DE EXPRESIONES (ÁRBOL BINARIO) ---
		private NodoExpresion AnalizarExpresion(string delimitadorFinal)
		{
			NodoExpresion izq = AnalizarTermino();
			while (PeekToken() == "+" || PeekToken() == "-")
			{
				string op = GetToken();
				NodoExpresion der = AnalizarTermino();
				NodoExpresion nuevo = new NodoExpresion(op);
				nuevo.Izquierdo = izq;
				nuevo.Derecho = der;
				izq = nuevo;
			}
			return izq;
		}

		private NodoExpresion AnalizarTermino()
		{
			NodoExpresion izq = AnalizarFactor();
			while (PeekToken() == "*" || PeekToken() == "/" || PeekToken() == "%")
			{
				string op = GetToken();
				NodoExpresion der = AnalizarFactor();
				NodoExpresion nuevo = new NodoExpresion(op);
				nuevo.Izquierdo = izq;
				nuevo.Derecho = der;
				izq = nuevo;
			}
			return izq;
		}

		private NodoExpresion AnalizarFactor()
		{
			string token = PeekToken();
			NodoExpresion nodo = null;

			if (token == "(")
			{
				Match("(");
				nodo = AnalizarExpresion(")");
				Match(")");
			}
			else if (EsOperadorUnario(token))
			{
				string op = GetToken();
				nodo = new NodoExpresion(op);
				nodo.Derecho = AnalizarFactor();
			}
			else if (EsIdentificadorValido(token))
			{
				string id = GetToken();
				if (!ExisteSimbolo(id) && id != "printf") Error($"Error Semántico: '{id}' no declarado.");
				nodo = new NodoExpresion(id);

				if (PeekToken() == "(")
				{
					Match("(");
					if (PeekToken() != ")")
					{
						AnalizarExpresion(")");
						while (PeekToken() == ",") { Match(","); AnalizarExpresion(")"); }
					}
					Match(")");
					nodo = new NodoExpresion("Funcion_" + id);
				}
				else if (PeekToken() == "++" || PeekToken() == "--")
				{
					string op = GetToken();
					NodoExpresion post = new NodoExpresion(op);
					post.Izquierdo = nodo;
					nodo = post;
				}
			}
			else if (EsConstante(token)) { nodo = new NodoExpresion(GetToken()); }
			else
			{
				Error($"Error Sintáctico: Operando no válido '{token}'.");
				GetToken();
				nodo = new NodoExpresion("ERROR");
			}
			return nodo;
		}

		private double EvaluarArbol(NodoExpresion nodo)
		{
			if (nodo == null) return 0;
			if (double.TryParse(nodo.Valor, out double num)) return num;

			var variable = TablaVariables.Find(v => v.Nombre == nodo.Valor);
			if (variable != null) return variable.ValorNum;

			if (EsOperador(nodo.Valor))
			{
				double izq = EvaluarArbol(nodo.Izquierdo);
				double der = EvaluarArbol(nodo.Derecho);
				switch (nodo.Valor)
				{
					case "+": return izq + der;
					case "-": return izq - der;
					case "*": return izq * der;
					case "/": return der != 0 ? izq / der : 0;
					case "%": return der != 0 ? izq % der : 0;
				}
			}

			if (nodo.Valor == "++") return EvaluarArbol(nodo.Izquierdo ?? nodo.Derecho) + 1;
			if (nodo.Valor == "--") return EvaluarArbol(nodo.Izquierdo ?? nodo.Derecho) - 1;

			return 0;
		}

		// --- UTILIDADES ---
		private bool EsOperador(string t) => new[] { "+", "-", "*", "/", "%", "==", "!=", "<", ">", "<=", ">=", "&&", "||" }.Contains(t);
		private bool EsOperadorUnario(string t) => new[] { "++", "--", "!", "&", "*" }.Contains(t);
		private bool EsConstante(string t) => double.TryParse(t, out _) || t.StartsWith("'") || t.StartsWith("\"") || t == "true" || t == "false";
		private bool EsIdentificadorValido(string t) => !string.IsNullOrEmpty(t) && !IsTipoDato(t) && !EsConstante(t) && !EsOperador(t) && (char.IsLetter(t[0]) || t[0] == '_');
		private bool IsTipoDato(string t) => TiposDeDatos.Contains(t);
		private bool ExisteSimbolo(string n) => TablaVariables.Exists(v => v.Nombre == n) || TablaFunciones.Exists(f => f.Nombre == n);
		private string GetToken() => (tokenIndex < tokenBuffer.Count) ? tokenBuffer[tokenIndex++] : "EOF";
		private string PeekToken() => (tokenIndex < tokenBuffer.Count) ? tokenBuffer[tokenIndex] : "EOF";
		private void SkipLF() { while (PeekToken() == "LF") GetToken(); }
		private void Match(string esp) { SkipLF(); if (PeekToken() == esp) GetToken(); else Error($"Se esperaba '{esp}' pero se halló '{PeekToken()}'."); }
		private void Error(string msg) { N_error++; CajaTexto2.AppendText($"> {msg}\n"); }

		private string PeekNextNoLF()
		{
			int i = 1; while (tokenIndex + i < tokenBuffer.Count && tokenBuffer[tokenIndex + i] == "LF") i++;
			return (tokenIndex + i < tokenBuffer.Count) ? tokenBuffer[tokenIndex + i] : "EOF";
		}

		private bool EsDefinicionDeFuncion()
		{
			int i = 1; while (tokenIndex + i < tokenBuffer.Count && tokenBuffer[tokenIndex + i] == "LF") i++;
			i++; while (tokenIndex + i < tokenBuffer.Count && tokenBuffer[tokenIndex + i] == "LF") i++;
			return (tokenIndex + i < tokenBuffer.Count && tokenBuffer[tokenIndex + i] == "(");
		}

		public void ExportarTablasCSV(string rutaBase)
		{
			if (N_error > 0) return;
			try
			{
				using (StreamWriter sw = new StreamWriter(Path.ChangeExtension(rutaBase, ".csv"), false, Encoding.UTF8))
				{
					sw.WriteLine("TABLA DE VARIABLES\nNombre,Tipo,Origen,Valor");
					foreach (var v in TablaVariables) sw.WriteLine($"{v.Nombre},{v.Tipo},{v.Origen},{v.ValorNum}");
					sw.WriteLine("\nTABLA DE FUNCIONES\nRetorno,Nombre,Params,Detalle");
					foreach (var f in TablaFunciones) sw.WriteLine($"{f.TipoRetorno},{f.Nombre},{f.NumParametros},{f.Parametros.Replace(",", "|")}");
				}
			}
			catch (Exception ex) { MessageBox.Show(ex.Message); }
		}

		private void MostrarResultadosEnPantalla()
		{
			CajaTexto2.AppendText("\n VARIABLES \n");
			foreach (var v in TablaVariables) CajaTexto2.AppendText($"{v.Nombre}\t| {v.Tipo}\t| {v.Origen}\t| Val: {v.ValorNum}\n");
			CajaTexto2.AppendText("\n FUNCIONES \n");
			foreach (var f in TablaFunciones) CajaTexto2.AppendText($"{f.TipoRetorno}\t| {f.Nombre}\t| ({f.NumParametros}) {f.Parametros}\n");

			if (UltimoArbolGenerado != null)
			{
				CajaTexto2.AppendText($" Raíz del Árbol : {UltimoArbolGenerado.Valor}\n");
				CajaTexto2.AppendText($"\n Resultado matemático: {ResultadoExpresion}");
			}
		}
	}
}