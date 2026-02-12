using Editor_de_texto.Clases;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics.Eventing.Reader;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Editor_de_texto
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
            compilarSolucionToolStripMenuItem1.Enabled = false;
            //inicializa la opcion de compilar como inhabilitada
        }
        private void nuevoAToolStripMenuItem_Click(object sender, EventArgs e)
        {
            CajaTexto1.Clear();
            archivo = null;
            Form1.ActiveForm.Text = "Mi compildor";
        }
        private void abrirToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog VentanaAbrir = new OpenFileDialog();
            VentanaAbrir.Filter = "Texto|*.c";
            if(VentanaAbrir.ShowDialog() == DialogResult.OK)
            {
                archivo = VentanaAbrir.FileName;
                using (StreamReader Leer = new StreamReader(archivo))
                {
                    CajaTexto1.Text = Leer.ReadToEnd();
                }
            }
            Form1.ActiveForm.Text = "Mi compilador -" + archivo;
            compilarSolucionToolStripMenuItem1.Enabled = true;
            //Habilita la opcion compilar cuando se carga un archivo.
        }
        private void guardarToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog VentanaAbrir = new OpenFileDialog();
            VentanaAbrir.Filter = "Texto|*.c"; //Establecemos un filtro para mostrar solo los archivos .c
            if (VentanaAbrir.ShowDialog() == DialogResult.OK) //Se muestra la ventana dialogo solo si el usuario selecciona un archivo y presiona aceptar
            {
                archivo = VentanaAbrir.FileName; //Se guarda la ruta del archivo
                using (StreamReader Leer = new StreamReader(archivo))  //Abrimos el archivo para leer su contenido
                {
                    CajaTexto1.Text = Leer.ReadToEnd(); // Se carga todo el contenido del archivo en la caja de texto 1
                }
                Form1.ActiveForm.Text = "Mi Compilador - " + archivo; //Actualizamos el titulo de la ventana principal con el nombre del archivo
                compilarSolucionToolStripMenuItem1.Enabled = true; //Se habilita la opción del menú para compilar el archivo cargado
            }
        }
        private void guardar() //Metodo para guardar el contenido de la caja de texto en u narchivo .c
        {
            OpenFileDialog VentanaAbrir = new OpenFileDialog(); //Se crea una ventana de seleccion de archivos
            VentanaAbrir.Filter = "Texto|*.c"; // Se realiza un filtro que nos mostrara solo los archivos .c
            if (archivo != null) //Se verifica si hay un archivo cargado previamente
            {
                using (StreamWriter Escribir = new StreamWriter(archivo)) //Si existe un archivo, se abre para escritura
                {
                    Escribir.Write(CajaTexto1.Text); //Se escribe el contenido de la caja de texto 1 en el archivo actual
                }
            }
            else //Si no hay un archivo cargado, se solicita al usuario que seleccione uno
            {
                if (VentanaAbrir.ShowDialog() == DialogResult.OK) //Se abre el cuadrode diálogo y espera a que el usuario seleccione un archivo
                {
                    archivo = VentanaAbrir.FileName; //Se guarda el nombre del archivo seleccionado
                    using (StreamWriter Escribir = new StreamWriter(archivo)) //Se abre el archivo selecionado para escritura
                    {
                        Escribir.Write(CajaTexto1.Text); //Se guarda el contenido en la caja de texto 1
                    }
                }
            }
            Form1.ActiveForm.Text = "Mi Compilador - " + archivo; //Actualiza el titulo de la ventana principal con el nombre del archivo
        }
        private void guardarComoToolStripMenuItem_Click(object sender, EventArgs e)
        {
            guardar();
        }
        private void salirToolStripMenuItem_Click(object sender, EventArgs e)
        {
           Application.Exit();
        }
        private void compilarSolucionToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            // 1. Validaciones iniciales
            if (string.IsNullOrEmpty(archivo))
            {
                MessageBox.Show("Debe abrir un archivo primero.");
                return;
            }

            // 2. Preparar interfaz y guardar cambios
            CajaTexto2.Text = "";
            guardar();

            // 3. Definir ruta del archivo intermedio (.back)
            archivoback = Path.ChangeExtension(archivo, ".back");

            // Variable local para contar errores léxicos
            int erroresLexicos = 0;

            // 4. EJECUTAR ANÁLISIS LÉXICO
            // El 'using' asegura que el archivo se cierre antes de pasar al sintáctico
            using (Leer = new StreamReader(archivo))
            using (Escribir = new StreamWriter(archivoback, false, Encoding.UTF8))
            {
                // Instanciamos el léxico
                var analizadorLexico = new Analizador_Lexico(Escribir, CajaTexto2, 0, 0);

                int numero_linea_local = 0;
                string linea;

                // Leemos línea por línea
                while ((linea = Leer.ReadLine()) != null)
                {
                    numero_linea_local++;
                    analizadorLexico.Numero_linea = numero_linea_local;
                    analizadorLexico.Analisis_Lexico(linea);
                }

                // Recuperamos la cantidad de errores encontrados
                erroresLexicos = analizadorLexico.N_error;
            }

            CajaTexto2.AppendText($"\n--- Fin Análisis Léxico. Errores: {erroresLexicos} ---\n");

            // 5. DECISIÓN: ¿Pasamos al Sintáctico?
            if (erroresLexicos > 0)
            {
                CajaTexto2.AppendText("\nNo se puede continuar con el análisis sintáctico debido a errores léxicos.\n");
                MessageBox.Show($"Se encontraron {erroresLexicos} errores léxicos. Corríjalos para continuar.", "Error de Compilación", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return; // Detenemos la ejecución aquí
            }

            // 6. EJECUTAR ANÁLISIS SINTÁCTICO
            // (Solo llegamos aquí si el léxico fue exitoso)
            try
            {
                Analizador_Sintactico sintactico = new Analizador_Sintactico(archivoback, CajaTexto2);
                sintactico.Analisis_Sintactico();

                // 7. RESULTADO FINAL
                if (sintactico.N_error == 0)
                {
                    MessageBox.Show("¡Compilación Exitosa! El programa no contiene errores.", "Compilación", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show($"Se encontraron {sintactico.N_error} errores sintácticos.", "Errores de Sintaxis", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                CajaTexto2.AppendText($"\nError crítico al ejecutar el analizador sintáctico: {ex.Message}\n");
            }

        }

        private void Cabecera()
        {
            string token = Leer.ReadLine();
            while (token != null)
            {
                if (token == "#")
                {
                    Directiva_Proc();
                }
                else if (token == "Palabra Reservada" || token == "identificador")
                {
                    break;
                }
                else
                {
                    ErrorSintactico("Se esperaba una directiva de preprocesador", token);
                }

                token = Leer.ReadLine();
            }
        }
        private void Directiva_Proc()
        {
            string token = Leer.ReadLine();

            if (token == "Palabra Reservada")
                token = Leer.ReadLine();

            switch (token)
            {
                case "include":
                    token = Leer.ReadLine();
                    if (token == "Libreria" || token == "cadena")
                    {
                        CajaTexto2.AppendText("Directiva include válida\n");
                    }
                    else
                    {
                        ErrorSintactico("Se esperaba una libreria o cadena despues de include", token);
                    }
                    break;

                case "define":
                    CajaTexto2.AppendText("Directiva define valida\n");
                    break;

                default:
                    ErrorSintactico("Se esperaba 'include' o 'define' despues de '#'", token);
                    break;
            }
        }
        private void ErrorSintactico(string mensaje, string token)
        {
            N_error++;
            Numero_linea++;
            CajaTexto2.AppendText($"Error sintactico en linea {Numero_linea}: {mensaje} (Token: {token})\n");
        }
        private void traducirToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(archivo) || !File.Exists(archivo))
            {
                MessageBox.Show("Primero agrega algo");
                return;
            }

            string codigoOriginal = File.ReadAllText(archivo);
            string codigoTraducido = codigoOriginal;

            string[] reservadas = P_Reservadas.Split(',');
            string[] traducciones = P_trad.Split(',');

            for (int i = 0; i < reservadas.Length; i++)
            {
                string original = reservadas[i];
                string traduccion = traducciones[i];

                string patron = $@"(?<![A-Za-z0-9_]){Regex.Escape(original)}(?![A-Za-z0-9_])";

                codigoTraducido = Regex.Replace(
                    codigoTraducido,
                    patron,
                    traduccion
                );
            }

            archivotrad = Path.ChangeExtension(archivo, ".trad");
            File.WriteAllText(archivotrad, codigoTraducido);

            CajaTexto2.Text = codigoTraducido;
        }
        private char Tipo_caracter(int caracter) //Realizamos la clasificación de los caracteres leidos segun su tipo.
        {
            //Se realiza la operación para ver si los valores ASCII corresponden a una letra
            if(caracter >= 65 && caracter <= 90 || caracter >= 97 && caracter <= 122)
            {
                return 'l'; // letra
            }
            else
            {
                //Verifica si el valor ASCII está entre 48 y 57 que corresponden a los digitos numéricos
                if(caracter >= 48 && caracter <= 57)
                {
                    return 'd'; // Digito 
                }
                //Si el no es letra ni numero, se considera otro tipo de simbolo
                else
                {
                    switch (caracter)
                    {
                        case 10: 
                            return 'n'; //Salto de linea
                        case 34: 
                            return '"'; //Comillas
                        case 39:
                            return 'c'; //Comilla simple
                        case 32:
                            return 'e'; //Espacio
                        case 47:
                            return '/'; //Comentarios
                        default:
                            return 's'; //Simbolo 
                    }
                }
            }
        }
        private bool Palabra_Reservada(string palabra)
        {
            return P_Reservadas.Contains(palabra);
        }
        private void Error(int i_caracter) //Función para mostrar un mensaje de error léxico 
        {
            CajaTexto2.AppendText("Error lexico " + (char)i_caracter + ", linea" + Numero_linea + "\n"); //Muestra en la caja de texto 2 un mensaje
            CajaTexto2.SelectionStart = CajaTexto2.Text.Length; // posición al final
            CajaTexto2.ScrollToCaret(); // scrollea hasta el final
        }
        private void Cadena() //Función que lee una cadena de texto entre comillas dobles
        {
            do
            {
                i_caracter = Leer.Read(); //Lee el siguietne carácter desde el archivo
                if (i_caracter == 10)  //Se realiza un ciclo hasta que se encuentre el cierre de comillas
                { 
                   Numero_linea++; 
                } 
            } while (i_caracter != 34 && i_caracter != -1); //Ciclo para detectar cuando llega al final y no encuentre comillas, nos madara un erro
            if (i_caracter == -1) 
            {
                Error(-1);
            } 
        }
        private void Caracter() //Realiza el proceso de carácter individual entre comillas simples
        {
            i_caracter = Leer.Read(); //Lee el primer carácter del contenido 
            i_caracter = Leer.Read(); //Lee la comilla simple de cierre
            if (i_caracter != 39) //Si el segundo caracter  leído no es una comilla simple marcas error 
            {
                Error(39);
            }
            
        }
        private void Simbolo()
        {
            elemento = "Simbolo: " + (char)i_caracter + "\n";
        }        
        private void Archivo_Libreria()
        {
            i_caracter = Leer.Read();
            if ((char)i_caracter == 'h') 
            { 
                elemento = "Archivo Libreria\n"; i_caracter = Leer.Read(); 
            }
            else 
            { 
                Error(i_caracter); 
            }
        }
        private void Identificador()
        {
            do
            {
                elemento = elemento + (char)i_caracter;
                i_caracter = Leer.Read();
            } while (Tipo_caracter(i_caracter) == 'l' || Tipo_caracter(i_caracter) == 'd');

            if ((char)i_caracter == '.') { Archivo_Libreria(); }
            else
            {
                if (Palabra_Reservada(elemento)) elemento = "Palabra Reservada: " + elemento + "\n";
                else elemento = "Identificador: " + elemento + "\n";
            }
        }
        private void Numero_Real()
        {
            do
            {
                i_caracter = Leer.Read();
            } while (Tipo_caracter(i_caracter) == 'd');
            elemento = "numero_real\n";
        }
        private void Numero()
        {
            do
            {
                i_caracter = Leer.Read();
            } while (Tipo_caracter(i_caracter) == 'd');
            if ((char)i_caracter == '.') { Numero_Real(); }
            else
            {
                elemento = "numero_entero\n";
            }
        }
        private void Comentario()
        {
            int siguiente = Leer.Read(); // Lee el caracter después de '/'

            // Comentario de línea //
            if (siguiente == '/')
            {
                int c;
                while ((c = Leer.Read()) != -1 && c != '\n')
                {
                    // Consumir caracteres hasta fin de línea
                }
                elemento = "Comentario de línea\n";
                i_caracter = c; // '\n' o -1
                if (i_caracter == '\n') Numero_linea++;
            }
            // Comentario de bloque /* ... */
            else if (siguiente == '*')
            {
                int previo = 0;
                int c;
                do
                {
                    previo = i_caracter;
                    c = Leer.Read();
                    i_caracter = c;
                    if (c == '\n') Numero_linea++;
                }
                while (!(previo == '*' && c == '/') && c != -1);

                elemento = "Comentario de bloque\n";

                // Avanzamos solo si no es fin de archivo
                if (i_caracter != -1)
                    i_caracter = Leer.Read();
            }
            else
            {
                elemento = "Operador division\n";
                i_caracter = siguiente; // devolvemos el caracter leído
            }
        }
        private void traductorToolStripMenuItem_Click(object sender, EventArgs e) //Evento para traducir y guardar el código
        {
            // Verificamos si el editor está vacío
            if (string.IsNullOrWhiteSpace(CajaTexto1.Text))
            {
                MessageBox.Show("No hay código para traducir.",
                                "Advertencia",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Warning);
                return; // Salir del método y no traduce ni guarda
            }
            Traductor traductor = new Traductor(); //Se crea un objeto traductor
            string CodigoOriginal = CajaTexto1.Text; //Se obtiene el código del editor
            string CodigoTraducido = traductor.TraducirCodigo(CodigoOriginal); //Se traduce el código
            //Cuadro de diálogo para elegir dónde se guardara el archivo
            SaveFileDialog GuardarDialogo = new SaveFileDialog(); 
            GuardarDialogo.Filter = "Código traducido |*.trad";//Solo permitiremos guardar con .esp.c
            GuardarDialogo.Title = "Guardar codigo traducido en español"; //Se asigna el título a la ventana
            //Sie el usuario elige un nombre de archivo valido
            if(GuardarDialogo.ShowDialog() == DialogResult.OK)
            {
                //Se crea el archivo y se escribe el código traducido
                using (StreamWriter sw = new StreamWriter(GuardarDialogo.FileName))
                {
                    sw.Write(CodigoTraducido);
                }
                //Mensaje de confirmación
                MessageBox.Show("Código traducido, guardado en:" + GuardarDialogo.FileName,
                    "Traducción completa", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void analizarToolStripMenuItem_Click(object sender, EventArgs e)
        {

        }
    }
}
