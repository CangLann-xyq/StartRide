using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using Launcher.App.Models;
using Microsoft.Extensions.Logging;

namespace Launcher.App.Services;

public sealed class EmbeddedInfoReferenceProjectCatalog : IInfoReferenceProjectCatalog
{
	internal const string ResourceName = "Launcher.App.Resources.ReferenceProjects.json";

	private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
	{
		PropertyNameCaseInsensitive = false
	};

	private readonly Func<Stream?> resourceStreamFactory;

	private readonly ILogger<EmbeddedInfoReferenceProjectCatalog> logger;

	private readonly Lazy<IReadOnlyList<InfoReferenceProjectItem>> projects;

	public EmbeddedInfoReferenceProjectCatalog(ILogger<EmbeddedInfoReferenceProjectCatalog> logger)
		: this(() => typeof(EmbeddedInfoReferenceProjectCatalog).Assembly.GetManifestResourceStream("Launcher.App.Resources.ReferenceProjects.json"), logger)
	{
	}

	internal EmbeddedInfoReferenceProjectCatalog(Func<Stream?> resourceStreamFactory, ILogger<EmbeddedInfoReferenceProjectCatalog> logger)
	{
		this.resourceStreamFactory = resourceStreamFactory;
		this.logger = logger;
		projects = new Lazy<IReadOnlyList<InfoReferenceProjectItem>>(LoadProjects, LazyThreadSafetyMode.ExecutionAndPublication);
	}

	public IReadOnlyList<InfoReferenceProjectItem> GetProjects()
	{
		return projects.Value;
	}

	private IReadOnlyList<InfoReferenceProjectItem> LoadProjects()
	{
		try
		{
			using Stream utf8Json = resourceStreamFactory() ?? throw new InvalidDataException("Embedded resource 'Launcher.App.Resources.ReferenceProjects.json' was not found.");
			InfoReferenceProjectItem[] array = (JsonSerializer.Deserialize<InfoReferenceProjectItem[]>(utf8Json, JsonOptions) ?? throw new InvalidDataException("The reference-project catalog does not contain a JSON array.")).Select(NormalizeAndValidate).ToArray();
			string text = array.GroupBy<InfoReferenceProjectItem, string>((InfoReferenceProjectItem project) => project.Name, StringComparer.OrdinalIgnoreCase).FirstOrDefault((IGrouping<string, InfoReferenceProjectItem> group) => group.Count() > 1)?.Key;
			if (text != null)
			{
				throw new InvalidDataException("The reference-project catalog contains duplicate name '" + text + "'.");
			}
			logger.LogDebug("Loaded embedded reference-project catalog. ProjectCount={ProjectCount}", array.Length);
			return new ReadOnlyCollection<InfoReferenceProjectItem>(array);
		}
		catch (Exception ex) when (((ex is JsonException || ex is IOException || ex is InvalidDataException) ? 1 : 0) != 0)
		{
			logger.LogError(ex, "Failed to load the embedded reference-project catalog.");
			return Array.Empty<InfoReferenceProjectItem>();
		}
	}

	private static InfoReferenceProjectItem NormalizeAndValidate(InfoReferenceProjectItem? project)
	{
		if ((object)project == null)
		{
			throw new InvalidDataException("The reference-project catalog contains a null entry.");
		}
		InfoReferenceProjectItem infoReferenceProjectItem = new InfoReferenceProjectItem(RequireValue(project.Name, "name"), RequireValue(project.CopyrightNotice, "copyrightNotice"), RequireValue(project.ProjectUrl, "projectUrl"), RequireValue(project.LicenseText, "licenseText"));
		bool flag = !Uri.TryCreate(infoReferenceProjectItem.ProjectUrl, UriKind.Absolute, out Uri result);
		if (!flag)
		{
			string scheme = result.Scheme;
			bool flag2 = ((scheme == "http" || scheme == "https") ? true : false);
			flag = !flag2;
		}
		if (flag)
		{
			throw new InvalidDataException("Reference project '" + infoReferenceProjectItem.Name + "' has an invalid HTTP(S) project URL.");
		}
		return infoReferenceProjectItem;
	}

	private static string RequireValue(string? value, string propertyName)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			throw new InvalidDataException("Reference-project property '" + propertyName + "' is required.");
		}
		return value.Trim();
	}
}
