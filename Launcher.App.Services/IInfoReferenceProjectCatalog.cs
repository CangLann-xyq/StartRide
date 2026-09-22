using System.Collections.Generic;
using Launcher.App.Models;

namespace Launcher.App.Services;

public interface IInfoReferenceProjectCatalog
{
	IReadOnlyList<InfoReferenceProjectItem> GetProjects();
}
