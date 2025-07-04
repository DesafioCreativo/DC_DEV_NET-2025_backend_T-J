namespace ThinkAndJobSolution.Request
{
    public class ChangePasswordRequest
    {
        public string? RecoveryCode { get; set; }
        public string? Password { get; set; }
    }
}
