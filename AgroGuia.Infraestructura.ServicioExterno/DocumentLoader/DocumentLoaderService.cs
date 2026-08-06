using AgroGuia.Dominio.Modelo.Abstracciones;
using AgroGuia.Dominio.Modelo.Entidades;
using AgroGuia.Infraestructura.ServicioExterno.Interfaces;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;

namespace AgroGuia.Infraestructura.ServicioExterno.DocumentLoader
{
    public class DocumentLoaderService
    {
        private readonly IEmbeddingRepositorio _embeddingRepositorio;
        private readonly IEmbeddingService _embeddingService;

        // Diccionarios mejorados y más completos
        private static readonly Dictionary<string, string> _cultivos = new(StringComparer.OrdinalIgnoreCase)
        {
            { "papa", "Papa" }, { "quinua", "Quinua" }, { "quinoa", "Quinua" },
            { "maiz", "Maíz" }, { "maíz", "Maíz" }, { "frejol", "Fréjol" },
            { "hortaliza", "Hortalizas" }, { "zanahoria", "Zanahoria" },
            { "cebolla", "Cebolla" }, { "lechuga", "Lechuga" }, { "brocoli", "Brócoli" },
            { "tomate", "Tomate" }, { "arveja", "Arveja" }, { "haba", "Haba" },
            { "trigo", "Trigo" }, { "cebada", "Cebada" }, { "arroz", "Arroz" }
        };

        private static readonly Dictionary<string, string> _temas = new(StringComparer.OrdinalIgnoreCase)
        {
            { "abono", "Fertilización" }, { "fertiliz", "Fertilización" },
            { "organico", "Fertilización Orgánica" }, { "bioinsumo", "Bioinsumos" },
            { "biol", "Bioinsumos" }, { "bokashi", "Bioinsumos" },
            { "bpa", "Buenas Prácticas Agrícolas" }, { "buenas practicas", "Buenas Prácticas Agrícolas" },
            { "plaga", "Sanidad Vegetal" }, { "enfermedad", "Sanidad Vegetal" },
            { "fungicida", "Sanidad Vegetal" }, { "insecticida", "Sanidad Vegetal" },
            { "riego", "Riego y Agua" }, { "cosecha", "Cosecha y Postcosecha" },
            { "suelo", "Manejo de Suelos" }, { "siembra", "Siembra y Trasplante" },
            { "semilla", "Semillas" }, { "clima", "Clima y Altitud" }
        };

        private static readonly HashSet<string> _stopWords = new(StringComparer.OrdinalIgnoreCase)
        {
            "para", "como", "este", "esta", "estos", "estas", "desde", "hasta", "entre",
            "sobre", "todos", "todas", "puede", "tiene", "hacer", "debe", "cada", "también",
            "cuando", "donde", "cuanto", "según", "dicha", "dicho", "mientras", "además",
            "través", "durante", "mediante", "tanto", "siendo", "otros", "otras", "mismo"
        };

        public DocumentLoaderService(
            IEmbeddingRepositorio embeddingRepositorio,
            IEmbeddingService embeddingService)
        {
            _embeddingRepositorio = embeddingRepositorio;
            _embeddingService = embeddingService;
        }

        public async Task<int> CargarDocumentosDesdeCarpetaAsync(string carpetaPath)
        {
            if (!Directory.Exists(carpetaPath))
                throw new DirectoryNotFoundException($"Carpeta no encontrada: {carpetaPath}");

            var archivosPdf = Directory.GetFiles(carpetaPath, "*.pdf");
            int totalCargados = 0;

            Console.WriteLine($"🚀 Iniciando carga de {archivosPdf.Length} PDFs...");

            foreach (var pdfPath in archivosPdf)
            {
                try
                {
                    await ProcesarPdfAsync(pdfPath);
                    totalCargados++;
                    Console.WriteLine($"✅ Cargado: {Path.GetFileName(pdfPath)}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Error en {Path.GetFileName(pdfPath)}: {ex.Message}");
                }
            }

            Console.WriteLine($"🎉 Carga completada. Total documentos: {totalCargados}");
            return totalCargados;
        }

        private async Task ProcesarPdfAsync(string pdfPath)
        {
            string titulo = Path.GetFileNameWithoutExtension(pdfPath);

            using var document = PdfDocument.Open(pdfPath);
            var sb = new StringBuilder();

            foreach (var page in document.GetPages())
            {
                sb.AppendLine(page.Text);
            }

            string textoCompleto = LimpiarTexto(sb.ToString().Trim());

            if (string.IsNullOrWhiteSpace(textoCompleto) || textoCompleto.Length < 50)
            {
                Console.WriteLine($"⚠️ PDF sin contenido útil: {titulo}");
                return;
            }

            var chunks = DividirEnChunksInteligente(textoCompleto, 1150, 180);

            string cultivo = DetectarCultivo(titulo, textoCompleto);
            string tema = DetectarTema(titulo, textoCompleto);

            Console.WriteLine($"📄 {titulo} | Chunks: {chunks.Count} | Cultivo: {cultivo} | Tema: {tema}");

            foreach (var chunk in chunks)
            {
                if (string.IsNullOrWhiteSpace(chunk)) continue;

                var embeddingResult = await _embeddingService.GenerarEmbeddingAsync(chunk);

                var nuevoChunk = new EmbeddingChunks
                {
                    Titulo = titulo.Length > 250 ? titulo.Substring(0, 250) : titulo,
                    Contenido = chunk,
                    Fuente = "Manual Técnico",
                    Cultivo = cultivo,
                    Tema = tema,
                    PalabrasClave = GenerarPalabrasClave(chunk, cultivo, tema),
                    FechaCarga = DateTime.UtcNow,
                    Activo = true,
                    VectorEmbedding = embeddingResult.Exito
                        ? JsonSerializer.Serialize(embeddingResult.Vector)
                        : null,
                    Metadata = JsonSerializer.Serialize(new
                    {
                        archivoOriginal = titulo,
                        tipo = "manual_tecnico",
                        cultivo = cultivo,
                        tema = tema,
                        longitud = chunk.Length
                    })
                };

                await _embeddingRepositorio.CrearChunkAsync(nuevoChunk);
            }
        }

        private string LimpiarTexto(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto)) return string.Empty;

            texto = Regex.Replace(texto, @"\.{3,}", " ");           // Eliminar puntos suspensivos
            texto = Regex.Replace(texto, @"\s+", " ");              // Normalizar espacios
            texto = texto.Replace("–", "-").Replace("—", "-");

            return texto.Trim();
        }

        private List<string> DividirEnChunksInteligente(string texto, int chunkSize = 1150, int overlap = 180)
        {
            var chunks = new List<string>();
            int posicion = 0;

            while (posicion < texto.Length)
            {
                int fin = Math.Min(posicion + chunkSize, texto.Length);
                string chunk = texto.Substring(posicion, fin - posicion);

                // Mejorar corte en oraciones
                if (fin < texto.Length)
                {
                    int ultimoPunto = chunk.LastIndexOfAny(new[] { '.', '!', '?' });
                    if (ultimoPunto > chunkSize * 0.65)
                        chunk = chunk.Substring(0, ultimoPunto + 1);
                }

                chunks.Add(chunk.Trim());
                posicion = fin - overlap;

                if (posicion < 0) posicion = 0;
                if (fin >= texto.Length) break;
            }

            return chunks;
        }

        private string DetectarCultivo(string titulo, string contenido)
        {
            string texto = (titulo + " " + contenido.Substring(0, Math.Min(1500, contenido.Length))).ToLower();
            foreach (var kv in _cultivos)
                if (texto.Contains(kv.Key)) return kv.Value;
            return "General";
        }

        private string DetectarTema(string titulo, string contenido)
        {
            string texto = (titulo + " " + contenido.Substring(0, Math.Min(1500, contenido.Length))).ToLower();
            foreach (var kv in _temas)
                if (texto.Contains(kv.Key)) return kv.Value;
            return "General";
        }

        private string GenerarPalabrasClave(string texto, string cultivo, string tema)
        {
            if (string.IsNullOrWhiteSpace(texto)) return "";

            var palabras = texto.Split(new[] { ' ', '\n', '\r', ',', '.', ';', ':', '(', ')', '/', '\\' },
                StringSplitOptions.RemoveEmptyEntries)
                .Select(w => w.ToLower().Trim())
                .Where(w => w.Length > 4 && !_stopWords.Contains(w) && !double.TryParse(w, out _))
                .Distinct()
                .Take(28)
                .ToList();

            if (cultivo != "General") palabras.Insert(0, cultivo.ToLower());
            if (tema != "General") palabras.Insert(0, tema.ToLower().Replace(" ", "_"));

            string resultado = string.Join(",", palabras);
            return resultado.Length > 490 ? resultado.Substring(0, 490) : resultado;
        }
    }
}