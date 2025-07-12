using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Text.Json;
using System.Text;
using ThinkAndJobSolution.Controllers._Model;
using ThinkAndJobSolution.Interfaces;
using static ThinkAndJobSolution.Controllers._Helper.HelperMethods;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.VisualStudio.Web.CodeGenerators.Mvc.Templates.Blazor;
using ThinkAndJobSolution.Controllers.MainHome.Comercial;
using ThinkAndJobSolution.Controllers.MainHome.Prl;

namespace ThinkAndJobSolution.Servicios
{
    public class CategoriaService : ICategoriaService
    {
        private readonly string _connectionString;

        public CategoriaService(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
        }


        public async Task<IActionResult> CrearCategoriaAsync(Categoria categoria)
        {
            object result = new { error = "Error 2932, no se ha podido procesar la petición." };

            try
            {
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync().ConfigureAwait(false);

                // 1️. Verificar nombre único
                const string checkQuery = "SELECT COUNT(*) FROM categories WHERE name = @NAME";
                await using (var checkCommand = conn.CreateCommand())
                {
                    checkCommand.CommandText = checkQuery;
                    checkCommand.Parameters.AddWithValue("@NAME", categoria.name);

                    var exists = (int)await checkCommand.ExecuteScalarAsync().ConfigureAwait(false) > 0;

                    if (exists)
                    {
                        result = new { error = "Error 4510, ya existe una categoría con este nombre." };
                        return new ConflictObjectResult(result);
                    }
                }

                // 2️. Insertar categoría y obtener ID
                const string insertQuery = @"
                    INSERT INTO categories (name, details, status, isnew) 
                    VALUES (@NAME, @DETAILS, @STATUS, @ISNEW);
                    SELECT CAST(SCOPE_IDENTITY() AS INT);
                    ";

                int newCategoryId;

                await using (var insertCommand = conn.CreateCommand())
                {
                    insertCommand.CommandText = insertQuery;
                    insertCommand.Parameters.AddWithValue("@NAME", categoria.name);
                    insertCommand.Parameters.AddWithValue("@DETAILS", categoria.details ?? (object)DBNull.Value);
                    insertCommand.Parameters.AddWithValue("@STATUS", categoria.status);
                    insertCommand.Parameters.AddWithValue("@ISNEW", categoria.isNew);

                    newCategoryId = (int)await insertCommand.ExecuteScalarAsync().ConfigureAwait(false);
                }

                // 3️. Devolver objeto completo
                categoria.id = newCategoryId;
                result = categoria;
            }
            catch (Exception)
            {
                result = new { error = "Error 5892, no se ha podido crear la categoría." };
            }

            return new OkObjectResult(result);
        }


        public async Task<IActionResult> EditarCategoriaAsync(Categoria categoria)
        {
            object result = new { error = "Error 2932, no se ha podido procesar la petición." };

            try
            {
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync().ConfigureAwait(false);

                // 1️. Verificar si ya existe otra categoría con el mismo nombre y un ID diferente
                const string checkQuery = "SELECT COUNT(*) FROM categories WHERE name = @NAME AND id <> @ID";
                await using (var checkCommand = conn.CreateCommand())
                {
                    checkCommand.CommandText = checkQuery;
                    checkCommand.Parameters.AddWithValue("@NAME", categoria.name);
                    checkCommand.Parameters.AddWithValue("@ID", categoria.id);

                    var exists = (int)await checkCommand.ExecuteScalarAsync().ConfigureAwait(false) > 0;

                    if (exists)
                    {
                        result = new { error = "Error 4510, ya existe una categoría con este nombre." };
                        return new ConflictObjectResult(result);
                    }
                }

                // 2️. Actualizar la categoría
                const string updateQuery = @"
                    UPDATE categories
                    SET name = @NAME,
                    details = @DETAILS,
                    status = @STATUS,
                    isnew = @ISNEW
                    WHERE id = @ID;
                    ";

                int affectedRows;

                await using (var updateCommand = conn.CreateCommand())
                {
                    updateCommand.CommandText = updateQuery;
                    updateCommand.Parameters.AddWithValue("@ID", categoria.id);
                    updateCommand.Parameters.AddWithValue("@NAME", categoria.name);
                    updateCommand.Parameters.AddWithValue("@DETAILS", categoria.details ?? (object)DBNull.Value);
                    updateCommand.Parameters.AddWithValue("@STATUS", categoria.status);
                    updateCommand.Parameters.AddWithValue("@ISNEW", categoria.isNew);

                    affectedRows = await updateCommand.ExecuteNonQueryAsync().ConfigureAwait(false);
                }

                if (affectedRows == 0)
                {
                    result = new { error = "Error 4040, no se encontró la categoría a actualizar." };
                    return new NotFoundObjectResult(result);
                }

                // 3️. Devolver objeto actualizado
                result = categoria;
            }
            catch (Exception)
            {
                result = new { error = "Error 5893, no se ha podido actualizar la categoría." };
                return new StatusCodeResult(500);
            }

            return new OkObjectResult(result);
        }


        public async Task<IActionResult> EliminarCategoriaAsync(int categoryId)
        {
            object result = new { error = "Error 2932, no se ha podido procesar la petición." };

            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            await using var transaction = conn.BeginTransaction();

            try
            {
                await using (var command = conn.CreateCommand())
                {
                    command.Transaction = transaction;
                    command.CommandText = "DELETE FROM categories WHERE id = @ID";
                    command.Parameters.AddWithValue("@ID", categoryId);

                    int rowsAffected = await command.ExecuteNonQueryAsync();

                    if (rowsAffected == 0)
                    {
                        await transaction.RollbackAsync();
                        return new OkObjectResult(new { error = "Error 5840, no se encontró la categoría." });
                    }
                }

                await transaction.CommitAsync();
                result = new { error = false };
            }
            catch (SqlException ex) when (ex.Number == 547) // Violación de FK
            {
                await transaction.RollbackAsync();
                result = new { error = "Error 5842, no se puede eliminar la categoría porque tiene elementos asociados." };
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                result = new { error = "Error 5834, no se ha podido eliminar la categoría." };
            }

            return new OkObjectResult(result);
        }


        public async Task<Categoria?> LeerCategoriaPorIdAsync(string categoriaId)
        {
            const string query = "SELECT * FROM categories WHERE id = @ID";

            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync().ConfigureAwait(false);

            await using var command = conn.CreateCommand();
            command.CommandText = query;
            command.Parameters.Add(new SqlParameter("@ID", SqlDbType.NVarChar, 50) { Value = categoriaId });

            await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);

            return await reader.ReadAsync().ConfigureAwait(false)
                ? MapCategoria(reader)
                : null;
        }


        public async Task<List<Categoria>> ListarCategoriasAsync()
        {
            const string query = "SELECT * FROM categories ORDER BY name";
            var categorias = new List<Categoria>();

            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync().ConfigureAwait(false);

            await using var command = conn.CreateCommand();
            command.CommandText = query;

            await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);

            while (await reader.ReadAsync().ConfigureAwait(false))
            {
                categorias.Add(MapCategoria(reader));
            }

            return categorias;
        }


        private Categoria MapCategoria(SqlDataReader reader)
        {
            int idxId = reader.GetOrdinal("id");
            int idxName = reader.GetOrdinal("name");
            int idxDetails = reader.GetOrdinal("details");
            int idxStatus = reader.GetOrdinal("status");
            int idxIsNew = reader.GetOrdinal("isNew");

            return new Categoria
            {
                id = reader.IsDBNull(idxId) ? 0 : reader.GetInt32(idxId),
                name = reader.IsDBNull(idxName) ? string.Empty : reader.GetString(idxName),
                details = reader.IsDBNull(idxDetails) ? string.Empty : reader.GetString(idxDetails),
                status = !reader.IsDBNull(idxStatus) && reader.GetBoolean(idxStatus),
                isNew = !reader.IsDBNull(idxIsNew) && reader.GetBoolean(idxIsNew)
            };
        }
    }
}