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

        // ✅ Palabras vacías en español para no contaminar palabras clave
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
                // ✅ Limpieza básica del texto extraído del PDF
                string textoPagina = LimpiarTextoPdf(page.Text);
                if (!string.IsNullOrWhiteSpace(textoPagina))
                    contenidoCompleto.AppendLine(textoPagina);
            }

            string textoCompleto = contenidoCompleto.ToString().Trim();
            if (string.IsNullOrWhiteSpace(textoCompleto)) return;

            // ✅ Chunks más inteligentes: corte por párrafos, no por caracteres ciegos
            var chunks = DividirEnChunksPorParrafos(textoCompleto, 1000, 200);

            string cultivo = DetectarCultivo(titulo);
            string tema = DetectarTema(titulo);

            Console.WriteLine($"📄 {titulo} → {chunks.Count} chunks | Cultivo: {cultivo} | Tema: {tema}");

            foreach (var chunk in chunks)
            {
                if (string.IsNullOrWhiteSpace(chunk)) continue;

                var embeddingResult = await _embeddingService.GenerarEmbeddingAsync(chunk);

                if (!embeddingResult.Exito)
                    Console.WriteLine($"⚠️ Embedding falló: {embeddingResult.ErrorMensaje}");
                else
                    Console.WriteLine($"✅ Embedding OK: {embeddingResult.Vector.Count} dims");

                var nuevoChunk = new EmbeddingChunks
                {
                    Titulo = titulo.Length > 250 ? titulo[..250] : titulo,
                    Contenido = chunk,
                    Fuente = "Manual Técnico",
                    Cultivo = cultivo,
                    Tema = tema,
                    // Palabras clave agrícolas mejoradas
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
                        cultivo,
                        tema,
                        caracteres = chunk.Length,
                        fechaCarga = DateTime.UtcNow
                    })
                };

                await _embeddingRepositorio.CrearChunkAsync(nuevoChunk);
            }
        }

        // ✅ NUEVO: Divide por párrafos respetando el contexto semántico
        private List<string> DividirEnChunksPorParrafos(
            string texto,
            int maxCaracteres,
            int overlap) // overlap = solapamiento para no perder contexto entre chunks
        {
            var chunks = new List<string>();

            // Separar por párrafos (doble salto de línea)
            var parrafos = texto
                .Split(new[] { "\n\n", "\r\n\r\n" }, StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Trim())
                .Where(p => p.Length > 30) // ignorar párrafos muy cortos
                .ToList();

            var chunkActual = new StringBuilder();

            foreach (var parrafo in parrafos)
            {
                // Si agregar este párrafo excede el límite, guardar chunk actual
                if (chunkActual.Length + parrafo.Length > maxCaracteres && chunkActual.Length > 0)
                {
                    chunks.Add(chunkActual.ToString().Trim());

                    // Overlap: conservar últimas palabras del chunk anterior
                    string textoActual = chunkActual.ToString();
                    string solapamiento = textoActual.Length > overlap
                        ? textoActual[^overlap..]
                        : textoActual;

                    chunkActual.Clear();
                    chunkActual.AppendLine(solapamiento);
                }

                chunkActual.AppendLine(parrafo);
            }

            // Agregar el último chunk
            if (chunkActual.Length > 0)
                chunks.Add(chunkActual.ToString().Trim());

            // Fallback: si no hubo párrafos, dividir por caracteres
            if (chunks.Count == 0)
                return DividirEnChunks(texto, maxCaracteres);

            return chunks;
        }

        // Fallback original por si acaso
        private List<string> DividirEnChunks(string texto, int maxCaracteres)
        {
            var chunks = new List<string>();
            int posicion = 0;
            while (posicion < texto.Length)
            {
                int longitud = Math.Min(maxCaracteres, texto.Length - posicion);
                chunks.Add(texto.Substring(posicion, longitud).Trim());
                posicion += longitud;
            }
            return chunks;
        }

        // ✅ NUEVO: Limpieza de texto extraído de PDFs
        private string LimpiarTextoPdf(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto)) return string.Empty;

            return texto
                // Eliminar múltiples espacios en blanco
                .Replace("  ", " ")
                // Eliminar caracteres de control extraños de PDFs
                .Replace("\f", "\n")
                .Replace("\r\n", "\n")
                // Normalizar guiones largos comunes en PDFs
                .Replace("–", "-")
                .Replace("—", "-")
                .Trim();
        }

        // ✅ MEJORADO: Detecta cultivos con diccionario ampliado
        private string DetectarCultivo(string titulo)
        {
            string t = titulo.ToLower();
            foreach (var kvp in _cultivos)
                if (t.Contains(kvp.Key)) return kvp.Value;
            return "General";
        }

        // ✅ MEJORADO: Detecta temas con diccionario ampliado
        private string DetectarTema(string titulo)
        {
            string t = titulo.ToLower();
            foreach (var kvp in _temas)
                if (t.Contains(kvp.Key)) return kvp.Value;
            return "General";
        }

        // Palabras clave agrícolas + incluye cultivo y tema como contexto
        private string GenerarPalabrasClave(string texto, string cultivo, string tema)
        {
            if (string.IsNullOrWhiteSpace(texto)) return string.Empty;

            var palabras = texto
                .Split(new[] { ' ', '\n', '\r', ',', '.', ';', ':', '(', ')', '/', '\\' },
                    StringSplitOptions.RemoveEmptyEntries)
                .Select(w => w.ToLower().Trim())
                .Where(w => w.Length > 4)
                .Where(w => !_stopWords.Contains(w))     // filtrar stopwords
                .Where(w => !double.TryParse(w, out _))  // filtrar números puros
                .Distinct()
                .Take(25)
                .ToList();

            //  Agregar cultivo y tema como palabras clave prioritarias
            if (cultivo != "General") palabras.Insert(0, cultivo.ToLower());
            if (tema != "General") palabras.Insert(0, tema.ToLower().Replace(" ", "_"));

            string resultado = string.Join(",", palabras.Distinct());

            return resultado.Length > 490
                ? resultado[..490]
                : resultado;
        }
    }
}
