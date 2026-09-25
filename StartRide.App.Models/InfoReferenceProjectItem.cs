using System.Text.Json.Serialization;

namespace StartRide.App.Models;

public sealed record InfoReferenceProjectItem([property: JsonPropertyName("name")] string Name, [property: JsonPropertyName("copyrightNotice")] string CopyrightNotice, [property: JsonPropertyName("projectUrl")] string ProjectUrl, [property: JsonPropertyName("licenseText")] string LicenseText);
