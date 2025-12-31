using System.Collections.Generic;
using System.Threading.Tasks;
using SMTx.Models;

namespace SMTx.Services;

public interface IDataService
{
    Task<List<RenderSolarSystem>> LoadSolarSystemsAsync();
    Task<List<StargateLink>> LoadStargateLinksAsync();
}

