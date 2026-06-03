using AgroGuia.Dominio.Modelo.Abstracciones;
using AgroGuia.Dominio.Modelo.Entidades;
using AgroGuia.Infraestructura.ServicioExterno.Interfaces;
using System.Text;
using System.Text.Json;
using UglyToad.PdfPig;

namespace AgroGuia.Infraestructura.ServicioExterno.DocumentLoader
{
    public class DocumentLoaderService
    {
        private readonly IEmbeddingRepositorio _embeddingRepositorio;
        private readonly IEmbeddingService _embeddingService;

        // ✅ Cultivos específicos de la Sierra Norte del Ecuador
        private static readonly Dictionary<string, string> _cultivos = new()
        {
            { "papa", "Papa" },
            { "quinua", "Quinua" },
            { "quinoa", "Quinua" },
            { "maiz", "Maíz" },
            { "maíz", "Maíz" },
            { "frejol", "Fréjol" },
            { "fréjol", "Fréjol" },
            { "hortaliza", "Hortalizas" },
            { "zanahoria", "Zanahoria" },
            { "cebolla", "Cebolla" },
            { "lechuga", "Lechuga" },
            { "brocoli", "Brócoli" },
            { "brócoli", "Brócoli" },
            { "tomate", "Tomate" },
            { "arveja", "Arveja" },
            { "haba", "Haba" },
            { "trigo", "Trigo" },
            { "cebada", "Cebada" },
            { "pasto", "Pastos" },
            { "forraje", "Forrajes" },
            { "bioinsumo", "Bioinsumos" },
            { "abono", "Abonos" }
        };

        // ✅ Temas agronómicos ampliados
        private static readonly Dictionary<string, string> _temas = new()
        {
            { "abono", "Fertilización" },
            { "fertiliz", "Fertilización" },
            { "organico", "Fertilización Orgánica" },
            { "orgánico", "Fertilización Orgánica" },
            { "bioinsumo", "Bioinsumos" },
            { "bokashi", "Bioinsumos" },
            { "compost", "Bioinsumos" },
            { "biol", "Bioinsumos" },
            { "bpa", "Buenas Prácticas Agrícolas" },
            { "buenas practicas", "Buenas Prácticas Agrícolas" },
            { "plaga", "Sanidad Vegetal" },
            { "enfermedad", "Sanidad Vegetal" },
            { "fungicida", "Sanidad Vegetal" },
            { "insecticida", "Sanidad Vegetal" },
            { "riego", "Riego y Agua" },
            { "agua", "Riego y Agua" },
            { "cosecha", "Cosecha y Postcosecha" },
            { "postcosecha", "Cosecha y Postcosecha" },
            { "almacenamiento", "Cosecha y Postcosecha" },
            { "suelo", "Manejo de Suelos" },
            { "labranza", "Manejo de Suelos" },
            { "siembra", "Siembra y Trasplante" },
            { "trasplante", "Siembra y Trasplante" },
            { "semilla", "Semillas" },
            { "variedad", "Semillas" },
            { "clima", "Clima y Altitud" },
            { "altitud", "Clima y Altitud" },
            { "temperatura", "Clima y Altitud" }
        };

        // ✅ Palabras vacías en español
        private static readonly HashSet<string> _stopWords = new()
        {
            "para", "como", "este", "esta", "estos", "estas", "desde",
            "hasta", "entre", "sobre", "todos", "todas", "puede", "tiene",
            "hacer", "debe", "cada", "también", "cuando", "donde", "cuanto",
            "según", "segun", "dicha", "dicho", "mientras", "además", "ademas",
            "través", "traves", "durante", "mediante", "tanto", "siendo",
            "otros", "otras", "mismo", "misma", "mayor", "menor", "mejor"
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

            Console.WriteLine($"🎉 Carga completada. Total: {totalCargados} documentos.");
            return totalCargados;
        }

        private async Task ProcesarPdfAsync(string pdfPath)
        {
            string titulo = Path.GetFileNameWithoutExtension(pdfPath);

            using var document = PdfDocument.Open(pdfPath);
            var contenidoCompleto = new StringBuilder();

            foreach (var page in document.GetPages())
            {
                contenidoCompleto.AppendLine(page.Text);
            }

            string textoCompleto = contenidoCompleto.ToString().Trim();
            if (string.IsNullOrWhiteSpace(textoCompleto)) return;

            var chunks = DividirEnChunks(textoCompleto, 1100);

            // ✅ Detectar cultivo y tema una sola vez por documento
            string cultivo = DetectarCultivo(titulo);
            string tema = DetectarTema(titulo);

            Console.WriteLine($"📄 {titulo} → {chunks.Count} chunks | Cultivo: {cultivo} | Tema: {tema}");

            foreach (var chunk in chunks)
            {
                if (string.IsNullOrWhiteSpace(chunk)) continue;

                var embeddingResult = await _embeddingService.GenerarEmbeddingAsync(chunk);

                if (!embeddingResult.Exito)
                {
                    Console.WriteLine($"❌ [EMBEDDING FALLÓ] Error: {embeddingResult.ErrorMensaje}");
                    Console.WriteLine($"   Chunk preview: {chunk.Substring(0, Math.Min(80, chunk.Length))}...");
                }
                else
                {
                    Console.WriteLine($"✅ [EMBEDDING OK] Vector de {embeddingResult.Vector.Count} dimensiones");
                }

                // ✅ Calcular una sola vez para VectorEmbedding
                string? vectorJson = embeddingResult.Exito
                    ? JsonSerializer.Serialize(embeddingResult.Vector)
                    : null;

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
                    VectorEmbedding = vectorJson,
                    Metadata = JsonSerializer.Serialize(new
                    {
                        archivoOriginal = titulo,
                        tipo = "manual_tecnico",
                        cultivo,
                        tema,
                        caracteres = chunk.Length,
                        fechaCarga = DateTime.UtcNow
                    })
                };

                await _embeddingRepositorio.CrearChunkAsync(nuevoChunk);
            }
        }

        private List<string> DividirEnChunks(string texto, int maxCaracteres)
        {
            var chunks = new List<string>();
            int posicion = 0;

            while (posicion < texto.Length)
            {
                int longitud = Math.Min(maxCaracteres, texto.Length - posicion);
                string chunk = texto.Substring(posicion, longitud);
                chunks.Add(chunk.Trim());
                posicion += longitud;
            }
            return chunks;
        }

        // ✅ Detecta cultivos con diccionario ampliado
        private string DetectarCultivo(string titulo)
        {
            string t = titulo.ToLower();
            foreach (var kvp in _cultivos)
                if (t.Contains(kvp.Key)) return kvp.Value;
            return "General";
        }

        // ✅ Detecta temas con diccionario ampliado
        private string DetectarTema(string titulo)
        {
            string t = titulo.ToLower();
            foreach (var kvp in _temas)
                if (t.Contains(kvp.Key)) return kvp.Value;
            return "General";
        }

        // ✅ Palabras clave con stopwords + cultivo y tema prioritarios
        private string GenerarPalabrasClave(string texto, string cultivo, string tema)
        {
            if (string.IsNullOrWhiteSpace(texto)) return string.Empty;

            var palabras = texto
                .Split(new[] { ' ', '\n', '\r', ',', '.', ';', ':', '(', ')', '/', '\\' },
                    StringSplitOptions.RemoveEmptyEntries)
                .Select(w => w.ToLower().Trim())
                .Where(w => w.Length > 4)
                .Where(w => !_stopWords.Contains(w))
                .Where(w => !double.TryParse(w, out _))
                .Distinct()
                .Take(25)
                .ToList();

            if (cultivo != "General") palabras.Insert(0, cultivo.ToLower());
            if (tema != "General") palabras.Insert(0, tema.ToLower().Replace(" ", "_"));

            string resultado = string.Join(",", palabras.Distinct());

            return resultado.Length > 490
                ? resultado[..490]
                : resultado;
        }
    }
}