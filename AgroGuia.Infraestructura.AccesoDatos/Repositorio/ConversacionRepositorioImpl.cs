using AgroGuia.Dominio.Modelo.Abstracciones;
using AgroGuia.Dominio.Modelo.Entidades;
using Microsoft.EntityFrameworkCore;

namespace AgroGuia.Infraestructura.AccesoDatos.Repositorio
{
    public class ConversacionRepositorioImpl : IConversacionRepositorio
    {
        private readonly AgroGuiaIA_DBContext _context;

        // ===== Métodos nuevos (gestión de chats) =====

        public async Task<Conversaciones> CrearAsync(Conversaciones entidad)
        {
            if (string.IsNullOrWhiteSpace(entidad.Titulo))
                entidad.Titulo = "Nueva Consulta Agronómica";

            entidad.FechaCreacion ??= DateTime.UtcNow;
            entidad.FechaActualizacion = DateTime.UtcNow;
            entidad.Activo ??= true;

            await _context.Conversaciones.AddAsync(entidad);
            await _context.SaveChangesAsync();
            return entidad;
        }

        public async Task<Conversaciones?> ObtenerPorIdAsync(long id)
        {
            // Con tracking: sirve para Renombrar / Eliminar (ActualizarAsync)
            return await _context.Conversaciones
                .FirstOrDefaultAsync(x => x.Id == id);
        }

        public async Task<IReadOnlyList<Conversaciones>> ListarPorUsuarioAsync(
            long usuarioId, bool soloActivas = true)
        {
            var q = _context.Conversaciones
                .AsNoTracking()
                .Where(x => x.UsuarioId == usuarioId);

            if (soloActivas)
                q = q.Where(x => x.Activo == true);

            return await q
                .OrderByDescending(x => x.FechaActualizacion)
                .ToListAsync();
        }

        public async Task<IReadOnlyList<Mensajes>> ListarMensajesAsync(
            long conversacionId, int take = 200)
        {
            if (take <= 0) take = 200;
            if (take > 500) take = 500;

            return await _context.Mensajes
                .AsNoTracking()
                .Where(m => m.ConversacionId == conversacionId)
                .OrderBy(m => m.Fecha)
                .Take(take)
                .ToListAsync();
        }

        public async Task ActualizarAsync(Conversaciones entidad)
        {
            entidad.FechaActualizacion = DateTime.UtcNow;
            _context.Conversaciones.Update(entidad);
            await _context.SaveChangesAsync();
        }

        public async Task<IReadOnlyList<Conversaciones>> AdminListarAsync(
            long? usuarioId,
            DateTime? desde,
            DateTime? hasta,
            string? tituloContiene,
            int take = 50)
        {
            if (take <= 0) take = 50;
            if (take > 100) take = 100;

            var q = _context.Conversaciones.AsNoTracking().AsQueryable();

            if (usuarioId is > 0)
                q = q.Where(x => x.UsuarioId == usuarioId.Value);

            if (desde.HasValue)
                q = q.Where(x => x.FechaActualizacion >= desde.Value);

            if (hasta.HasValue)
                q = q.Where(x => x.FechaActualizacion <= hasta.Value);

            if (!string.IsNullOrWhiteSpace(tituloContiene))
                q = q.Where(x => x.Titulo != null && x.Titulo.Contains(tituloContiene));

            return await q
                .OrderByDescending(x => x.FechaActualizacion)
                .Take(take)
                .ToListAsync();
        }
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