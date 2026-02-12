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
