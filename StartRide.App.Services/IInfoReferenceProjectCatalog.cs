using System.Collections.Generic;
using StartRide.App.Models;

namespace StartRide.App.Services;

public interface IInfoReferenceProjectCatalog
{
	IReadOnlyList<InfoReferenceProjectItem> GetProjects();
}
