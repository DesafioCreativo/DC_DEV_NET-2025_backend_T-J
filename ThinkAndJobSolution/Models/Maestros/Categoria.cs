using System.ComponentModel.DataAnnotations;

namespace ThinkAndJobSolution.Controllers._Model
{
    public class Categoria
    {
        public int id { get; set; }

        [Required]
        [StringLength(250, ErrorMessage = "El nombre de la categoría no puede tener más de 250 caracteres.")]
        public string name { get; set; }

        [StringLength(1000, ErrorMessage = "Los detalles no pueden tener más de 1000 caracteres.")]
        public string details { get; set; }

        public bool status { get; set; }

        public bool isNew { get; set; }
    }
}
