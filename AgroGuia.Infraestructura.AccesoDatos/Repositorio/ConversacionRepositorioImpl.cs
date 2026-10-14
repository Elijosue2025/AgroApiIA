using AgroGuia.Dominio.Modelo.Abstracciones;
using AgroGuia.Dominio.Modelo.Entidades;
using Microsoft.EntityFrameworkCore;

namespace AgroGuia.Infraestructura.AccesoDatos.Repositorio
{
    public class ConversacionRepositorioImpl : IConversacionRepositorio
    {
        private readonly AgroGuiaIA_DBContext _context;

        public ConversacionRepositorioImpl(AgroGuiaIA_DBContext context)
        {
            _context = context;
        }

        public async Task<Conversaciones> ConversacionCrearAsync(
            long usuarioId,
            string titulo)
        {
            var conversacion = new Conversaciones
            {
                UsuarioId = usuarioId,
                Titulo = titulo ?? "Nueva Consulta Agronómica",
                FechaCreacion = DateTime.UtcNow,
                FechaActualizacion = DateTime.UtcNow,
                Activo = true
            };

            await _context.Conversaciones.AddAsync(conversacion);

            await _context.SaveChangesAsync();

            return conversacion;
        }

        public async Task<Conversaciones?> ConversacionObtenerConMensajesAsync(long id)
        {
            return await _context.Conversaciones
                .Include(x => x.Mensajes)
                .FirstOrDefaultAsync(x => x.Id == id && x.Activo == true);
        }

        public async Task<List<Conversaciones>> ConversacionObtenerPorUsuarioAsync(long usuarioId)
        {
            return await _context.Conversaciones
                .Where(x => x.UsuarioId == usuarioId && x.Activo == true)
                .OrderByDescending(x => x.FechaActualizacion)
                .ToListAsync();
        }

        public async Task MensajeGuardarAsync(Mensajes mensaje)
        {
            await _context.Mensajes.AddAsync(mensaje);

            await _context.SaveChangesAsync();
        }
        public async Task<List<string>> ChunksBuscarRelevantesAsync(string consulta, int topK = 4)
        {
            if (string.IsNullOrWhiteSpace(consulta))
                return new List<string>();

            // Palabras clave (mín. 4 letras) — no la frase completa
            var stop = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "como", "cuál", "cual", "qué", "que", "para", "sobre", "tiene", "hacer",
        "puedo", "puede", "controlo", "controlar", "dónde", "donde", "esta", "este"
    };

            var palabras = consulta
                .ToLowerInvariant()
                .Split(new[] { ' ', '?', '¿', '!', '¡', ',', '.', ';', ':', '\n', '\t', '-', '_' },
                    StringSplitOptions.RemoveEmptyEntries)
                .Where(p => p.Length >= 4 && !stop.Contains(p))
                .Distinct()
                .Take(8)
                .ToList();

            if (palabras.Count == 0)
                return new List<string>();

            // Candidatos limitados (evita timeout en tablas grandes)
            var candidatos = await _context.EmbeddingChunks
                .AsNoTracking()
                .Where(e => e.Activo == true)
                .Select(e => new
                {
                    e.Contenido,
                    e.Titulo,
                    e.PalabrasClave,
                    e.Cultivo,
                    e.Tema
                })
                .Take(800)
                .ToListAsync();

            var rankeados = candidatos
                .Select(e =>
                {
                    int score = 0;
                    var cont = (e.Contenido ?? "").ToLowerInvariant();
                    var tit = (e.Titulo ?? "").ToLowerInvariant();
                    var kw = (e.PalabrasClave ?? "").ToLowerInvariant();
                    var cult = (e.Cultivo ?? "").ToLowerInvariant();
                    var tema = (e.Tema ?? "").ToLowerInvariant();

                    foreach (var p in palabras)
                    {
                        if (cont.Contains(p)) score += 10;
                        if (tit.Contains(p)) score += 6;
                        if (kw.Contains(p)) score += 5;
                        if (cult.Contains(p)) score += 8;
                        if (tema.Contains(p)) score += 4;
                    }

                    return new { e.Contenido, score };
                })
                .Where(x => x.score > 0 && !string.IsNullOrWhiteSpace(x.Contenido))
                .OrderByDescending(x => x.score)
                .Take(topK)
                .Select(x => x.Contenido!)
                .ToList();

            return rankeados;
        }
        // Alias opcional para compatibilidad

        public async Task<List<string>> BuscarChunksRelevantesAsync(
            string preguntaUsuario)
        {
            return await ChunksBuscarRelevantesAsync(preguntaUsuario);
        }
    }
}