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

        public async Task<List<string>> ChunksBuscarRelevantesAsync(
            string consulta,
            int topK = 4)
        {
            if (string.IsNullOrWhiteSpace(consulta))
                return new List<string>();

            string consultaLower = consulta.ToLower();

            return await _context.EmbeddingChunks
                .Where(e => e.Activo == true)
                .OrderByDescending(e =>

                    (e.Contenido != null &&
                     e.Contenido.ToLower().Contains(consultaLower) ? 100 : 0)

                    +

                    (e.Titulo != null &&
                     e.Titulo.ToLower().Contains(consultaLower) ? 60 : 0)

                    +

                    (e.PalabrasClave != null &&
                     e.PalabrasClave.ToLower().Contains(consultaLower) ? 40 : 0)

                    +

                    (e.Cultivo != null &&
                     e.Cultivo.ToLower().Contains(consultaLower) ? 30 : 0)

                )
                .Take(topK)
                .Select(e => e.Contenido)
                .ToListAsync();
        }

        // Alias opcional para compatibilidad

        public async Task<List<string>> BuscarChunksRelevantesAsync(
            string preguntaUsuario)
        {
            return await ChunksBuscarRelevantesAsync(preguntaUsuario);
        }
    }
}