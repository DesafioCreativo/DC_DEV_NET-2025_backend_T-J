using System.Text.Json;

namespace ThinkAndJobSolution.Controllers._Helper.Ohers.Extensions
{
    public static class JsonExtensions
    {
        public static bool TryGetString(this JsonElement je, out string parsed)
        {
            var (p, r) = je.ValueKind switch
            {
                JsonValueKind.String => (je.GetString(), true),
                JsonValueKind.Null => (null, true),
                _ => (default, false)
            };
            parsed = p;
            return r;
        }
        public static bool TryGetBoolean(this JsonElement je, out bool parsed)
        {
            var (p, r) = je.ValueKind switch
            {
                JsonValueKind.True => (true, true),
                JsonValueKind.False => (false, true),
                _ => (default, false)
            };
            parsed = p;
            return r;
        }
        public static bool TryGetChar(this JsonElement je, string property, out char parsed)
        {
            if (je.TryGetProperty(property, out JsonElement jeProperty) && jeProperty.TryGetString(out string parsedString) && parsedString != null)
            {
                parsed = parsedString[0];
                return true;
            }
            else
            {
                parsed = default;
                return false;
            }
        }
        public static bool TryGetString(this JsonElement je, string property, out string parsed)
        {
            if (je.TryGetProperty(property, out JsonElement jeProperty) && jeProperty.TryGetString(out parsed))
            {
                return true;
            }
            else
            {
                parsed = default;
                return false;
            }
        }
        public static bool TryGetStringList(this JsonElement je, string property, out List<string> parsed)
        {
            if (je.TryGetProperty(property, out JsonElement jeProperty) && jeProperty.ValueKind == JsonValueKind.Array)
            {
                parsed = HelperMethods.GetJsonStringList(jeProperty);
                return true;
            }
            else
            {
                parsed = default;
                return false;
            }
        }
        public static bool TryGetDateTime(this JsonElement je, string property, out DateTime parsed)
        {
            if (je.TryGetProperty(property, out JsonElement jeProperty) && jeProperty.ValueKind == JsonValueKind.String && jeProperty.TryGetDateTime(out parsed))
            {
                return true;
            }
            else
            {
                parsed = default;
                return false;
            }
        }
        public static bool TryGetDate(this JsonElement je, string property, out DateTime parsed)
        {
            if (je.TryGetProperty(property, out JsonElement jeProperty))
            {
                DateTime? date = HelperMethods.GetJsonDate(jeProperty);
                if (date == null)
                {
                    parsed = default;
                    return false;
                }
                parsed = date.Value;
                return true;
            }
            else
            {
                parsed = default;
                return false;
            }
        }
        public static bool TryGetTime(this JsonElement je, string property, out TimeSpan parsed)
        {
            if (je.TryGetProperty(property, out JsonElement jeProperty))
            {
                TimeSpan? time = HelperMethods.GetJsonTime(jeProperty);
                if (time == null)
                {
                    parsed = default;
                    return false;
                }
                parsed = time.Value;
                return true;
            }
            else
            {
                parsed = default;
                return false;
            }
        }
        public static bool TryGetDateOptional(this JsonElement je, string property, out DateTime? parsed)
        {
            if (je.TryGetProperty(property, out JsonElement jeProperty) && (jeProperty.ValueKind == JsonValueKind.String || jeProperty.ValueKind == JsonValueKind.Null))
            {
                if (jeProperty.ValueKind == JsonValueKind.String)
                {
                    parsed = HelperMethods.GetJsonDate(jeProperty);
                }
                else
                {
                    parsed = null;
                }
                return true;
            }
            else
            {
                parsed = null;
                return false;
            }
        }
        public static bool TryGetBoolean(this JsonElement je, string property, out bool parsed)
        {
            if (je.TryGetProperty(property, out JsonElement jeProperty) && jeProperty.TryGetBoolean(out parsed))
            {
                return true;
            }
            else
            {
                parsed = default;
                return false;
            }
        }
        public static bool TryGetInt32(this JsonElement je, string property, out int parsed)
        {
            if (je.TryGetProperty(property, out JsonElement jeProperty) && jeProperty.ValueKind == JsonValueKind.Number && jeProperty.TryGetInt32(out parsed))
            {
                return true;
            }
            else
            {
                parsed = default;
                return false;
            }
        }
        public static bool TryGetDouble(this JsonElement je, string property, out double parsed)
        {
            if (je.TryGetProperty(property, out JsonElement jeProperty) && jeProperty.ValueKind == JsonValueKind.Number && jeProperty.TryGetDouble(out parsed))
            {
                return true;
            }
            else
            {
                parsed = default;
                return false;
            }
        }
        public static bool TryGetSingle(this JsonElement je, string property, out float parsed)
        {
            if (je.TryGetProperty(property, out JsonElement jeProperty) && jeProperty.ValueKind == JsonValueKind.Number && jeProperty.TryGetSingle(out parsed))
            {
                return true;
            }
            else
            {
                parsed = default;
                return false;
            }
        }


    }
}
