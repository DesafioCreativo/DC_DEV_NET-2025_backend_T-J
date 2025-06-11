namespace ThinkAndJobSolution.Utils.Interfaces
{
    public interface ICl_Libreria
    {
        Cl_Paginacion DevuelveObjetoPaginacion(HttpRequest Request);
        Cl_Filter DevuelveObjetoFilter(HttpRequest Request);
        List<Cl_comparador> devuelveLstComparador(HttpRequest Request);
        string DevuelveStringWhere(Cl_Filter objfil, string as_tipo = null);
        void validacionComparadorKpis(List<Cl_comparador> lstKpi);
    }
}
