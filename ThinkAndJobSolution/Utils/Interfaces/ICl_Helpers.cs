using System.Data;

namespace ThinkAndJobSolution.Utils.Interfaces
{
    public interface ICl_Helpers
    {
        List<T> generateListObjGeneric<T>(T obj, DataTable dt);
        T generateObjGenericWithCod<T>(T obj, int cod, DataSet dsGlobal);       
    }
}
