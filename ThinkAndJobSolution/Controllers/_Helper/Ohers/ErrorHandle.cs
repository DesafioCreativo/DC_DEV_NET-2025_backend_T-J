using System.Text;

namespace ThinkAndJobSolution.Controllers._Helper.Ohers
{
    public class ErrorHandle
    {
        private static readonly int MAX_DEPTH = 10;

        public struct APIError
        {
            public int code { get; set; }
            public int timestamp { get; set; }
        }

        public static APIError newError(int code, Exception exception = null, string remarks = null)
        {
            DateTime now = DateTime.Now;
            APIError error = new APIError()
            {
                code = code,
                timestamp = HelperMethods.DateToEpoch(now)
            };

            if (exception != null)
            {
                StringBuilder sb = new StringBuilder();

                sb.AppendLine(now.ToString("yyyy-MM-dd hh:mm:ss"));
                if (remarks != null) sb.AppendLine(remarks);
                recAddException(sb, exception);

                HelperMethods.SaveFile(new[] { "error", $"{error.timestamp}-{error.code}" }, sb.ToString());
            }

            return error;
        }

        private static void recAddException(StringBuilder sb, Exception e, int depth = 0)
        {
            sb.AppendLine();
            sb.AppendLine(e.Message);
            sb.AppendLine("=========================================");
            sb.AppendLine(e.ToString());
            sb.AppendLine("=========================================");
            sb.AppendLine();

            if (e.GetBaseException() != e && e.GetBaseException() != null && depth < MAX_DEPTH)
                recAddException(sb, e.GetBaseException(), depth + 1);
        }
    }
}
