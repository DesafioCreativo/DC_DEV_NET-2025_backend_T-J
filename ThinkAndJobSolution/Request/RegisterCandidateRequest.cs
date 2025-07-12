using System.ComponentModel.DataAnnotations;

namespace ThinkAndJobSolution.Request
{
    public class RegisterCandidateRequest
    {
        [Required]
        [StringLength(100)]
        public string Nombre { get; set; }

        [Required]
        [StringLength(100)]
        public string Apellido1 { get; set; }

        [StringLength(100)]
        public string? Apellido2 { get; set; }

        [Required]
        [StringLength(20)]
        public string Telefono { get; set; }

        [Required]
        [StringLength(100)]
        [EmailAddress]
        public string Email { get; set; }

        [Required]
        [StringLength(250)]
        public string Password { get; set; }

        [Required]
        [StringLength(10)]
        public string Dni { get; set; }

        [Required]
        public int CategoriaId { get; set; }

        [Required]
        public int RegionId { get; set; }

        [Required]
        public int LocalidadId { get; set; }

        public IFormFile? CurriculumVitae { get; set; }
    }
}
