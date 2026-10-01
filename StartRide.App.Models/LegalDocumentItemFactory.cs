using System.Collections.Generic;
using StartRide.App.Resources;
using StartRide.Core;

namespace StartRide.App.Models;

public static class LegalDocumentItemFactory
{
    public static IReadOnlyList<LegalDocumentItem> CreateFirstRunAgreements()
        => Project(LegalDocuments.FirstRunAgreement);

    public static IReadOnlyList<LegalDocumentItem> CreateAll()
        => Project(LegalDocuments.All);

    private static IReadOnlyList<LegalDocumentItem> Project(IReadOnlyList<LegalDocument> documents)
    {
        var items = new List<LegalDocumentItem>(documents.Count);
        foreach (LegalDocument document in documents)
        {
            items.Add(new LegalDocumentItem(
                document.Id,
                ResolveText(document.TitleKey),
                ResolveText(document.DescriptionKey)));
        }

        return items;
    }

    private static string ResolveText(string key)
        => string.IsNullOrWhiteSpace(key) ? string.Empty : Strings.GetOrDefault(key);
}
