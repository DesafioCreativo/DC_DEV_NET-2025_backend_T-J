using System.Data;
using System.Reflection;
using ThinkAndJobSolution.Utils.Interfaces;

namespace ThinkAndJobSolution.Utils
{
    public class Cl_Helpers : ICl_Helpers
    {
        public List<T> generateListObjGeneric<T>(T obj, DataTable dt)
        {
            string name = "";
            Type baseTipo = typeof(List<>);
            Type tipo = obj.GetType();
            PropertyInfo[] propiedades = obj.GetType().GetProperties();
            var genericType = baseTipo.MakeGenericType(tipo);
            List<T> myList = (List<T>)Activator.CreateInstance(genericType);
            T objNew = (T)Activator.CreateInstance(tipo);
            string[] lstColumnName = dt.Columns.Cast<DataColumn>().Select(dc => dc.ColumnName).ToArray();
            int c = Array.FindIndex(propiedades, item => item.Name == "ESTREG");
            foreach (DataRow dr in dt.Rows)
            {
                objNew = (T)Activator.CreateInstance(tipo);
                for (int i = 0; i <= c; i++)
                {

                    name = propiedades[i].Name;
                    Type tipoTemp = propiedades[i].PropertyType;
                    if (tipoTemp.FullName == "System.String" || tipoTemp.FullName == "System.Int32" || tipoTemp.FullName == "System.DateTime" || tipoTemp.FullName == "System.Decimal " || tipoTemp.Name == "Nullable`1")
                    {
                        if (dr[lstColumnName[i]] == null || DBNull.Value.Equals(dr[lstColumnName[i]])) propiedades[i].SetValue(objNew, null, null);
                        //else propiedades[i].SetValue(objNew, Convert.ChangeType(dr[lstColumnName[i]], tipoTemp), null);
                        else propiedades[i].SetValue(objNew, Convert.ChangeType(dr[lstColumnName[i]], tipoTemp.Name == "Nullable`1" ? Nullable.GetUnderlyingType(tipoTemp) : tipoTemp), null);
                    }

                }
                myList.Add(objNew);
            }
            return myList;
        }
        public T generateObjGenericWithCod<T>(T obj, int cod, DataSet dsGlobal)
        {
            string name = "";
            DataRow[] drs = null;
            string[] lstColumnName = null;

            Type tipo = obj.GetType();
            PropertyInfo[] propiedades = obj.GetType().GetProperties();
            T objNew = (T)Activator.CreateInstance(tipo);

            foreach (DataTable dt in dsGlobal.Tables)
            {
                if (dt.TableName.Contains(tipo.Name))
                {
                    drs = dt.Select(String.Format("CODIGO = {0}", cod));
                    lstColumnName = dt.Columns.Cast<DataColumn>().Select(dc => dc.ColumnName).ToArray();
                    break;
                }
            }
            int c = Array.FindIndex(propiedades, item => item.Name == "ESTREG");
            if (drs != null)
            {
                if (drs.Length > 0)
                {
                    for (int i = 0; i <= c; i++)
                    {
                        name = propiedades[i].Name;
                        Type tipoTemp = propiedades[i].PropertyType;
                        if (tipoTemp.FullName == "System.String" || tipoTemp.FullName == "System.Int32" || tipoTemp.FullName == "System.DateTime" || tipoTemp.FullName == "System.Decimal" || tipoTemp.FullName == "System.Boolean" || tipoTemp.Name == "Nullable`1")
                        {
                            if (drs[0][lstColumnName[i]] == null || DBNull.Value.Equals(drs[0][lstColumnName[i]])) propiedades[i].SetValue(objNew, null, null);
                            else propiedades[i].SetValue(objNew, Convert.ChangeType(drs[0][lstColumnName[i]], tipoTemp.Name == "Nullable`1" ? Nullable.GetUnderlyingType(tipoTemp) : tipoTemp), null);
                        }
                        else propiedades[i].SetValue(objNew, generateObjGenericWithCod(Activator.CreateInstance(tipoTemp), Convert.ToInt32(drs[0][lstColumnName[i]].ToString()), dsGlobal), null);
                    }
                }
            }

            return objNew;
        }
    }
}
