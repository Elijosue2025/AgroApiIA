using AgroGuia.Dominio.Modelo.Abstracciones;
using AgroGuia.Dominio.Modelo.Entidades;
using Microsoft.EntityFrameworkCore;

namespace AgroGuia.Infraestructura.AccesoDatos.Repositorio
{
    public class MensajeRepositorioImpl : RepositorioImpl<Mensajes>, IMensajeRepositorio
    {
        private new readonly AgroGuiaIA_DBContext _context;

        public MensajeRepositorioImpl(AgroGuiaIA_DBContext context)
            : base(context)
        {
            _context = context;
        }

        public async Task CrearMensajeAsync(Mensajes mensaje)
        {
            await _context.Mensajes.AddAsync(mensaje);
            await _context.SaveChangesAsync();
        }

        public async Task<List<Mensajes>> ObtenerMensajesConversacionAsync(long conversacionId)
        {
            return await _context.Mensajes
                .Where(x => x.ConversacionId == conversacionId)
                .OrderBy(x => x.Fecha)
                .ToListAsync();
        }
    }
}