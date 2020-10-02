﻿using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Sistema.Datos;
using Sistema.Entidades.Usuarios;
using Sistema.Web.Models.Usuarios.Usuario;

namespace Sistema.Web.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UsuariosController : ControllerBase
    {
        private readonly DbContextSistema _context;
        private readonly IConfiguration _config;

        public UsuariosController(DbContextSistema context, IConfiguration config)
        {
            _context = context;
            _config = config;
        }

        // GET: api/Usuarios/Listar
        [HttpGet("[action]")]
        public async Task<IEnumerable<UsuarioViewModel>> Listar()
        {
            var usuario = await _context.Usuarios.Include(u => u.rol).ToListAsync();
            return usuario.Select(u => new UsuarioViewModel
            {
               idusuario = u.idusuario,
               idrol = u.idrol,
               rol = u.rol.nombre,
               nombre = u.nombre,
               tipo_documento = u.tipo_documento,
               num_documento = u.num_documento,
               direccion = u.direccion,
               telefono = u.telefono,
               email = u.email,
               password_hash = u.password_hash,
               condicion = u.condicion,

            });
        }
        // POST: api/Usuarios/crear
        [HttpPost("[action]")]
        public async Task<IActionResult> Crear([FromBody] CrearViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var email = model.email.ToLower();

            if (await _context.Usuarios.AnyAsync(u => u.email == email))
                return BadRequest("El Email ya existe");
    
            CreatePasswordHas(model.password, out byte[] passwordHash, out byte[] passwordsalt);

            Usuario usuario = new Usuario
            {
                idrol = model.idrol,
                nombre = model.nombre,
                tipo_documento = model.tipo_documento,
                num_documento = model.num_documento,
                direccion = model.direccion,
                telefono = model.telefono,
                email = model.email.ToLower(),
                password_hash = passwordHash,
                password_salt = passwordsalt,
                condicion= true,

            };

            _context.Usuarios.Add(usuario);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {

                return BadRequest(ex);
            }
            await _context.SaveChangesAsync();

            return Ok();

        }

        private void CreatePasswordHas(string password, out byte[] passwordHash, out byte[] passwordSalt)
        {
            using(var hmac = new System.Security.Cryptography.HMACSHA512())
            {
                passwordSalt = hmac.Key;
                passwordHash = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(password));
            }
        }

        // PUT: api/Usuarios/Actualizar
        [HttpPut("[action]")]
        public async Task<IActionResult> Actualizar([FromBody] ActualizarViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (model.idusuario <= 0)
            {
                return BadRequest();
            }

            var usuario = await _context.Usuarios.FirstOrDefaultAsync(u => u.idusuario == model.idusuario);
            if (usuario == null)
            {
                return NotFound();
            }

            usuario.idrol = model.idrol;
            usuario.nombre = model.nombre;
            usuario.tipo_documento = model.tipo_documento;
            usuario.num_documento = model.num_documento;
            usuario.direccion = model.direccion;
            usuario.telefono = model.telefono;
            usuario.email = model.email;

            if(model.act_password == true)
            {
                CreatePasswordHas(model.password, out byte[] passwordHash, out byte[] passwordSalt);
                usuario.password_hash = passwordHash;
                usuario.password_hash = passwordSalt;
            }

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                return BadRequest();
            }

            return Ok();
        }
        // PUT: api/Usuarios/Desactivar/1
        [HttpPut("[action]/{id}")]
        public async Task<IActionResult> Desactivar([FromRoute] int id)
        {

            if (id <= 0)
            {
                return BadRequest();
            }

            var usuario = await _context.Usuarios.FirstOrDefaultAsync(u => u.idusuario == id);
            if (usuario == null)
            {
                return NotFound();
            }
            usuario.condicion = false;
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                return BadRequest();
            }

            return Ok();
        }

        // PUT: api/Usuarios/Activar/1
        [HttpPut("[action]/{id}")]
        public async Task<IActionResult> Activar([FromRoute] int id)
        {

            if (id <= 0)
            {
                return BadRequest();
            }

            var usuario = await _context.Usuarios.FirstOrDefaultAsync(u => u.idusuario == id);
            if (usuario == null)
            {
                return NotFound();
            }
            usuario.condicion = true;
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                return BadRequest();
            }

            return Ok();
        }

        // POST: api/Usuarios/Login
        [HttpPost("[action]")]
        public async Task<IActionResult>Login(LoginViewModel model)
        {
            var email = model.email.ToLower();

            var usuario = await _context.Usuarios.Where(u=>u.condicion==true).Include(u => u.rol).FirstOrDefaultAsync(u => u.email == email);

            if (usuario == null)
            {
                return NotFound();
            }
            if(!VerificarPasswordHash(model.password, usuario.password_hash, usuario.password_salt))
            {
                return NotFound();
            }

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, usuario.idusuario.ToString()),
                new Claim(ClaimTypes.Email, email),
                new Claim(ClaimTypes.Role, usuario.rol.nombre),
                new Claim("idusuario", usuario.idusuario.ToString()),
                new Claim("rol", usuario.rol.nombre),
                new Claim("nombre", usuario.nombre),
            };

            return Ok(
                new {token = GenerarToken(claims)}

                );

        }
        private bool VerificarPasswordHash(string password, byte[] passwordHashAlmacenado, byte[] passwordSalt)
        {
            using (var hmac = new System.Security.Cryptography.HMACSHA512(passwordSalt))
            {
                var passwordhasNuevo = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(password));
                return new ReadOnlySpan<byte>(passwordHashAlmacenado).SequenceEqual(new ReadOnlySpan<byte>(passwordhasNuevo));
            }

        }

        private string GenerarToken(List<Claim> claims)
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:key"]));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var token = new JwtSecurityToken(
                _config["Jwt:Issuer"],
                _config["Jwt:Issuer"],
                expires: DateTime.Now.AddMinutes(30),
                claims: claims);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }



        private bool UsuarioExists(int id)
        {
            return _context.Usuarios.Any(e => e.idusuario == id);
        }
    }
}