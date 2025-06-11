using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Google.Apis.Auth.OAuth2;
using static ThinkAndJobSolution.Controllers._Helper.HelperMethods;

namespace ThinkAndJobSolution.Controllers.Commons
{
    [Route("api/v1/pushnotification")]
    [ApiController]
    [Authorize]
    public class PushNotificationController : ControllerBase
    {
        private static FirebaseApp firebase;
        private static FirebaseMessaging fcm;

        static PushNotificationController()
        {
            try
            {
                firebase = FirebaseApp.Create(new AppOptions()
                {
                    Credential = GoogleCredential.FromFile(Path.Combine(Directory.GetCurrentDirectory(), "Resources", "fcmkey.json"))
                });
                fcm = FirebaseMessaging.GetMessaging(firebase);
            }
            catch (Exception)
            {

            }
        }

        //------------------------------------------ENDPOINTS INICIO------------------------------------------

        [HttpPost]
        [Route(template: "set-token/{type}/{identifier}")]
        public async Task<IActionResult> SetToken(string type, string identifier)
        {
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };

            using StreamReader bodyReader = new StreamReader(Request.Body, System.Text.Encoding.UTF8);
            string token = await bodyReader.ReadToEndAsync();

            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();
                try
                {
                    if (tryGetUID(type, identifier, out UID uid, conn))
                    {
                        setToken(uid, token, conn);
                        result = new { error = false };
                    }
                    else
                    {
                        result = new { error = "Error 4591, usuario no encontrado." };
                    }
                }
                catch (Exception)
                {
                    result = new { error = "Error 5591, no se ha podido establecer el token" };
                }
            }

            return Ok(result);
        }

        [HttpDelete]
        [Route(template: "unset-token/{type}/{identifier}")]
        public IActionResult UnsetToken(string type, string identifier)
        {
            object result = new
            {
                error = "Error 2932, no se ha podido procesar la petición."
            };

            using (SqlConnection conn = new SqlConnection(CONNECTION_STRING))
            {
                conn.Open();
                try
                {
                    if (tryGetUID(type, identifier, out UID uid, conn))
                    {
                        unsetToken(uid, conn);
                        result = new { error = false };
                    }
                    else
                    {
                        result = new { error = "Error 4591, usuario no encontrado." };
                    }
                }
                catch (Exception)
                {
                    result = new { error = "Error 5592, no se ha podido eliminar el token" };
                }
            }

            return Ok(result);
        }

        //------------------------------------------ENDPOINTS FIN---------------------------------------------
        //------------------------------------------CLASES INICIO---------------------------------------------
        public struct UID
        {
            public string type { get; set; }
            public string id { get; set; }
        }

        public struct Notification
        {
            public string title { get; set; }
            public string body { get; set; }
            public string type { get; set; }
            public Dictionary<string, string> data { get; set; }
        }

        //------------------------------------------CLASES FIN------------------------------------------------
        //------------------------------------------FUNCIONES INI---------------------------------------------

        private static bool tryGetUID(string type, string identifier, out UID uid, SqlConnection conn, SqlTransaction transaction = null)
        {
            switch (type)
            {
                case "ca":
                    //Comprobar que el candidato existe
                    bool exists;
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText = "SELECT COUNT(*) FROM candidatos WHERE id = @ID";
                        command.Parameters.AddWithValue("@ID", identifier);
                        exists = (int)command.ExecuteScalar() > 0;
                    }
                    if (exists)
                    {
                        uid = new() { id = identifier, type = type };
                        return true;
                    }
                    break;
                case "cl":
                    //Obtener la Id del cliente por su token
                    string clientUserId = null;
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText = "SELECT id FROM client_users WHERE token = @TOKEN";
                        command.Parameters.AddWithValue("@TOKEN", identifier);
                        using SqlDataReader reader = command.ExecuteReader();
                        if (reader.Read())
                            clientUserId = reader.GetString(0);
                    }
                    if (clientUserId != null)
                    {
                        uid = new() { id = clientUserId, type = type };
                        return true;
                    }
                    break;
                case "rj":
                    //Obtener la Id del usuario por su token
                    string userId = null;
                    using (SqlCommand command = conn.CreateCommand())
                    {
                        command.CommandText = "SELECT id FROM users WHERE securityToken = @TOKEN";
                        command.Parameters.AddWithValue("@TOKEN", identifier);
                        using SqlDataReader reader = command.ExecuteReader();
                        if (reader.Read())
                            userId = reader.GetString(0);
                    }
                    if (userId != null)
                    {
                        uid = new() { id = userId, type = type };
                        return true;
                    }
                    break;
            }

            uid = default;
            return false;
        }

        private static string setToken(UID uid, string token, SqlConnection lastConn = null, SqlTransaction transaction = null)
        {
            string result = null;
            SqlConnection conn = lastConn ?? new SqlConnection(CONNECTION_STRING);
            if (lastConn == null) conn.Open();

            //Obtener el token que tiene ahora para saber si hay que actualizarlo o no
            string lastToken = null;
            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }
                command.CommandText = "SELECT token FROM fcm_tokens WHERE type = @TYPE AND id = @ID";
                command.Parameters.AddWithValue("@TYPE", uid.type);
                command.Parameters.AddWithValue("@ID", uid.id);
                using SqlDataReader reader = command.ExecuteReader();
                if (reader.Read())
                    lastToken = reader.GetString(0);
            }

            if (lastToken == null)
            {
                //Si no tiene token, insertarlo
                using (SqlCommand command = conn.CreateCommand())
                {
                    if (transaction != null)
                    {
                        command.Connection = conn;
                        command.Transaction = transaction;
                    }
                    command.CommandText = "INSERT INTO fcm_tokens (type, id, token) VALUES (@TYPE, @ID, @TOKEN)";
                    command.Parameters.AddWithValue("@TYPE", uid.type);
                    command.Parameters.AddWithValue("@ID", uid.id);
                    command.Parameters.AddWithValue("@TOKEN", token);
                    command.ExecuteNonQuery();
                }
            }
            else if (lastToken != token)
            {
                //Si lo tiene y es distinto, actualizarlo
                using (SqlCommand command = conn.CreateCommand())
                {
                    if (transaction != null)
                    {
                        command.Connection = conn;
                        command.Transaction = transaction;
                    }
                    command.CommandText = "UPDATE fcm_tokens SET token = @TOKEN WHERE type = @TYPE AND id = @ID";
                    command.Parameters.AddWithValue("@TYPE", uid.type);
                    command.Parameters.AddWithValue("@ID", uid.id);
                    command.Parameters.AddWithValue("@TOKEN", token);
                    command.ExecuteNonQuery();
                }
            }

            //Borrar todas las otras ocurrencias de este token
            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }
                command.CommandText = "DELETE FROM fcm_tokens WHERE token = @TOKEN AND NOT (type = @TYPE AND id = @ID)";
                command.Parameters.AddWithValue("@TYPE", uid.type);
                command.Parameters.AddWithValue("@ID", uid.id);
                command.Parameters.AddWithValue("@TOKEN", token);
                command.ExecuteNonQuery();
            }


            if (lastConn == null) conn.Close();
            return result;
        }

        private static string unsetToken(UID uid, SqlConnection lastConn = null, SqlTransaction transaction = null)
        {
            string result = null;
            SqlConnection conn = lastConn ?? new SqlConnection(CONNECTION_STRING);
            if (lastConn == null) conn.Open();

            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }
                command.CommandText = "DELETE FROM fcm_tokens WHERE type = @TYPE AND id = @ID";
                command.Parameters.AddWithValue("@TYPE", uid.type);
                command.Parameters.AddWithValue("@ID", uid.id);
                command.ExecuteNonQuery();
            }

            if (lastConn == null) conn.Close();
            return result;
        }

        private static string getToken(UID uid, SqlConnection lastConn = null, SqlTransaction transaction = null)
        {
            string result = null;
            SqlConnection conn = lastConn ?? new SqlConnection(CONNECTION_STRING);
            if (lastConn == null) conn.Open();

            using (SqlCommand command = conn.CreateCommand())
            {
                if (transaction != null)
                {
                    command.Connection = conn;
                    command.Transaction = transaction;
                }
                command.CommandText = "SELECT token FROM fcm_tokens WHERE type = @TYPE AND id = @ID";
                command.Parameters.AddWithValue("@TYPE", uid.type);
                command.Parameters.AddWithValue("@ID", uid.id);
                using SqlDataReader reader = command.ExecuteReader();
                if (reader.Read())
                    result = reader.GetString(0);
            }

            if (lastConn == null) conn.Close();
            return result;
        }
        private static async Task sendNotification(string token, Notification notification)
        {
            if (notification.data != null)
                notification.data["type"] = notification.type;
            Message msg = new()
            {
                Token = token,
                Notification = new()
                {
                    Title = notification.title,
                    Body = notification.body
                },
                Data = notification.data
            };
            await fcm.SendAsync(msg);
        }
        private static async Task sendNotifications(IEnumerable<string> tokens, Notification notification)
        {
            if (tokens.Count() == 0) return;
            if (notification.data == null) notification.data = new();
            notification.data["type"] = notification.type;
            MulticastMessage mmsg = new()
            {
                Tokens = tokens.ToList(),
                Notification = new()
                {
                    Title = notification.title,
                    Body = notification.body
                },
                Data = notification.data
            };
            await fcm.SendMulticastAsync(mmsg);
        }
        public static async Task sendNotification(UID uid, Notification notification, SqlConnection lastConn = null, SqlTransaction transaction = null)
        {
            try
            {
                string token = getToken(uid, lastConn, transaction);
                if (token != null)
                {
                    await sendNotification(token, notification);
                }
            }
            catch (Exception) { }
        }
        public static async Task sendNotifications(IEnumerable<UID> uids, Notification notification, SqlConnection lastConn = null, SqlTransaction transaction = null)
        {
            try
            {
                IEnumerable<string> tokens = uids.Select(uid => getToken(uid, lastConn, transaction)).Where(token => token != null);
                if (tokens.Count() == 0) return;
                await sendNotifications(tokens, notification);
            }
            catch (Exception) { }
        }

        //------------------------------------------FUNCIONES FIN---------------------------------------------

    }
}
