using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
//using WebApi.Models;
using ThinkAndJobSolution.AccesoDato;
using ThinkAndJobSolution.General;
//using WebApi.Areas.ModuloSeguridad.Models;
//using WebApi.ModelsEFUPC;

namespace WebApi.HostedServices
{
    public class AlertaHostedService : IHostedService, IDisposable
    {
        private readonly IHubContext<AlertaHub> _modulohub;
        //----
        private static DataAccess _dataAccess2 = new DataAccess();
        private readonly IServiceScopeFactory _scopeFactory;

        //----
        private Timer _timer;

        public AlertaHostedService(IHubContext<AlertaHub> modulohub, IServiceScopeFactory scopeFactory)//, IDataAccess dataAccess 
        {
            _modulohub = modulohub;
            _scopeFactory = scopeFactory;
            //_dataAccess = dataAccess;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            //_timer = new Timer(SendInfo, null, TimeSpan.Zero
            //    , TimeSpan.FromSeconds(5));
            _timer = new Timer(SendInfo, null, TimeSpan.Zero, TimeSpan.FromSeconds(30));
            return Task.CompletedTask;

            //throw new NotImplementedException();
        }

        private void SendInfo(object state)
        {
            //IEnumerable<Kpi> lista;
            //using (var context = new UPCContext())
            //{
            //    lista = context.Kpis.ToList();
            //}

            using (var scope = _scopeFactory.CreateScope())
            {
                
                var _dataContext = scope.ServiceProvider.GetRequiredService<IDataAccess>();
                //List<Alerta> dato = JsonConvert.DeserializeObject<List<Alerta>>(_dataContext.QueryReturnJSON(string.Format(@"sb_sp_alertman_cn_alertas")));
                //_modulohub.Clients.All.SendAsync("Recibe", dato);                
            }

            //ModelDataSetUsuario dato = JsonConvert.DeserializeObject<ModelDataSetUsuario>(_dataAccess.QueryReturnDataset(string.Format(@"sb_sp_segman_cn_valinic_page_seguridad_usuario_index")));.
            //_dataAccess2.QueryReturnDataset
            //_modulohub.Clients.All.SendAsync("Recibe", dato);
            //throw new NotImplementedException();
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _timer?.Change(Timeout.Infinite, 0);
            return Task.CompletedTask;
            //throw new NotImplementedException();
        }

        public void Dispose()
        {
            _timer?.Dispose();
            //throw new NotImplementedException();
        }


        /*facho ddf*/ 
    }
}
