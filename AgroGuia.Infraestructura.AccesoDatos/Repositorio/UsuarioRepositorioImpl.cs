using AgroGuia.Dominio.Modelo.Abstracciones;
using AgroGuia.Dominio.Modelo.Entidades;
using Microsoft.EntityFrameworkCore;

namespace AgroGuia.Infraestructura.AccesoDatos.Repositorio
{
    public class UsuarioRepositorioImpl : IUsuarioRepositorio
    {
        private readonly AgroGuiaIA_DBContext _context;

        public UsuarioRepositorioImpl(AgroGuiaIA_DBContext context)
        {
            _context = context;
        }

        public async Task CrearUsuarioAsync(Usuarios usuario)
        {
            try
            {
                await _context.Usuarios.AddAsync(usuario);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                throw new Exception("Error al crear usuario", ex);
            }
        }

        public async Task<Usuarios?> ObtenerPorEmailAsync(string email)
        {
            try
            {
                return await _context.Usuarios
                    .FirstOrDefaultAsync(u => u.Email == email);
            }
            catch (Exception ex)
            {
                throw new Exception("Error al obtener usuario por email", ex);
            }
        }

        public async Task<Usuarios?> ObtenerPorIdAsync(long id)
        {
            try
            {
                return await _context.Usuarios
                    .FirstOrDefaultAsync(u => u.Id == id);
            }
            catch (Exception ex)
            {
                throw new Exception("Error al obtener usuario por ID", ex);
            }
        }

        public async Task<List<Usuarios>> ObtenerTodosAsync()
        {
            try
            {
                return await _context.Usuarios.ToListAsync();
            }
            catch (Exception ex)
            {
                throw new Exception("Error al listar usuarios", ex);
            }
        }

        public async Task ActualizarUsuarioAsync(Usuarios usuario)
        {
            try
            {
                _context.Usuarios.Update(usuario);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                throw new Exception("Error al actualizar usuario", ex);
            }
        }

        public async Task EliminarUsuarioAsync(long id)
        {
            try
            {
                var usuario = await _context.Usuarios.FindAsync(id);

                if (usuario == null)
                    throw new Exception("Usuario no encontrado");

                _context.Usuarios.Remove(usuario);

                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                throw new Exception("Error al eliminar usuario", ex);
            }
        }
    }
}