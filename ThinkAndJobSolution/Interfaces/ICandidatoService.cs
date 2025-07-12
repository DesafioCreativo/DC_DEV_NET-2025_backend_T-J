using Microsoft.AspNetCore.Mvc;
using ThinkAndJobSolution.Request;

namespace ThinkAndJobSolution.Interfaces
{
    public interface ICandidatoService
    {
        Task<IActionResult> RegistrarCandidatoAsync(RegisterCandidateRequest request);
        Task<IActionResult> ActivarEmailCandidatoAsync(string code);
    }
}